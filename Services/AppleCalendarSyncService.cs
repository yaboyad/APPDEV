using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class AppleCalendarSyncService
{
    private static readonly XNamespace DavNamespace = "DAV:";
    private static readonly XNamespace CalDavNamespace = "urn:ietf:params:xml:ns:caldav";
    private readonly HttpClient httpClient;

    public AppleCalendarSyncService(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
    }

    public async Task<AppleCalendarConnection> ConnectAsync(AppleCalendarConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!connection.IsConfigured)
        {
            throw new InvalidOperationException("Enter your Apple ID email and an app-specific password first.");
        }

        var rootUri = new Uri("https://caldav.icloud.com/");
        var principalUri = await DiscoverPrincipalAsync(connection, rootUri, cancellationToken);
        var homeUri = await DiscoverCalendarHomeAsync(connection, principalUri, cancellationToken);
        var calendars = await DiscoverCalendarsAsync(connection, homeUri, cancellationToken);
        var selected = calendars.FirstOrDefault(calendar =>
            !calendar.Href.Contains("/inbox/", StringComparison.OrdinalIgnoreCase) &&
            !calendar.Href.Contains("/outbox/", StringComparison.OrdinalIgnoreCase))
            ?? calendars.FirstOrDefault()
            ?? throw new InvalidOperationException("No Apple calendars were returned for this account.");

        connection.CalendarHref = selected.Href;
        connection.CalendarName = selected.DisplayName;
        return connection;
    }

    public async Task<ApplePullResult> PullAsync(AppleCalendarConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!connection.IsConnected)
        {
            connection = await ConnectAsync(connection, cancellationToken);
        }

        var calendarUri = EnsureDirectoryUri(new Uri(connection.CalendarHref));
        var body = BuildCalendarQueryBody(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow.AddDays(120));

        using var response = await SendDavAsync(
            connection,
            new HttpMethod("REPORT"),
            calendarUri,
            body,
            "application/xml",
            "1",
            cancellationToken);

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = XDocument.Parse(xml);
        var events = new List<CalendarEventRecord>();

        foreach (var davResponse in document.Descendants(DavNamespace + "response"))
        {
            var href = davResponse.Element(DavNamespace + "href")?.Value;
            var calendarData = davResponse
                .Descendants(CalDavNamespace + "calendar-data")
                .FirstOrDefault()?.Value;

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(calendarData))
            {
                continue;
            }

            var eventUri = new Uri(calendarUri, href);
            events.AddRange(IcsCalendarCodec.ParseEvents(calendarData, "Apple", eventUri.AbsoluteUri));
        }

        connection.LastPullUtc = DateTimeOffset.UtcNow;
        return new ApplePullResult(connection, events);
    }

    public async Task<ApplePushResult> PushAsync(
        AppleCalendarConnection connection,
        IEnumerable<CalendarEventRecord> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(events);

        if (!connection.IsConnected)
        {
            connection = await ConnectAsync(connection, cancellationToken);
        }

        var calendarUri = EnsureDirectoryUri(new Uri(connection.CalendarHref));
        var syncedEvents = new List<CalendarEventRecord>();

        foreach (var item in events.Where(candidate => candidate.End >= DateTimeOffset.Now.AddDays(-2)).OrderBy(candidate => candidate.Start))
        {
            var uid = string.IsNullOrWhiteSpace(item.ExternalUid) ? item.Id : item.ExternalUid;
            var targetUri = string.IsNullOrWhiteSpace(item.AppleEventHref)
                ? new Uri(calendarUri, uid + ".ics")
                : new Uri(item.AppleEventHref);

            using var response = await SendDavAsync(
                connection,
                HttpMethod.Put,
                targetUri,
                IcsCalendarCodec.CreateSingleEventCalendar(item, uid),
                "text/calendar",
                null,
                cancellationToken);

            syncedEvents.Add(CreateSyncedCopy(item, uid, targetUri.AbsoluteUri));
        }

        connection.LastPushUtc = DateTimeOffset.UtcNow;
        return new ApplePushResult(connection, syncedEvents);
    }

    private async Task<Uri> DiscoverPrincipalAsync(AppleCalendarConnection connection, Uri rootUri, CancellationToken cancellationToken)
    {
        const string body = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\"><prop><current-user-principal /></prop></propfind>";

        using var response = await SendDavAsync(connection, new HttpMethod("PROPFIND"), rootUri, body, "application/xml", "0", cancellationToken);
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = XDocument.Parse(xml);
        var href = document.Descendants(DavNamespace + "current-user-principal")
            .Elements(DavNamespace + "href")
            .FirstOrDefault()?.Value;

        if (string.IsNullOrWhiteSpace(href))
        {
            throw new InvalidOperationException("Apple Calendar did not return the account principal URL.");
        }

        return new Uri(rootUri, href);
    }

    private async Task<Uri> DiscoverCalendarHomeAsync(AppleCalendarConnection connection, Uri principalUri, CancellationToken cancellationToken)
    {
        const string body = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\" xmlns:cd=\"urn:ietf:params:xml:ns:caldav\"><prop><cd:calendar-home-set /></prop></propfind>";

        using var response = await SendDavAsync(connection, new HttpMethod("PROPFIND"), principalUri, body, "application/xml", "0", cancellationToken);
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = XDocument.Parse(xml);
        var href = document.Descendants(CalDavNamespace + "calendar-home-set")
            .Elements(DavNamespace + "href")
            .FirstOrDefault()?.Value;

        if (string.IsNullOrWhiteSpace(href))
        {
            throw new InvalidOperationException("Apple Calendar did not return the calendar home set.");
        }

        return new Uri(principalUri, href);
    }

    private async Task<List<AppleCalendarDescriptor>> DiscoverCalendarsAsync(AppleCalendarConnection connection, Uri homeUri, CancellationToken cancellationToken)
    {
        const string body = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\" xmlns:cd=\"urn:ietf:params:xml:ns:caldav\"><prop><displayname /><resourcetype /></prop></propfind>";

        using var response = await SendDavAsync(connection, new HttpMethod("PROPFIND"), homeUri, body, "application/xml", "1", cancellationToken);
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = XDocument.Parse(xml);
        var calendars = new List<AppleCalendarDescriptor>();

        foreach (var davResponse in document.Descendants(DavNamespace + "response"))
        {
            var href = davResponse.Element(DavNamespace + "href")?.Value;
            var prop = davResponse.Descendants(DavNamespace + "prop").FirstOrDefault();
            var hasCalendarType = prop?.Element(DavNamespace + "resourcetype")?.Element(CalDavNamespace + "calendar") is not null;

            if (!hasCalendarType || string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var displayName = prop?.Element(DavNamespace + "displayname")?.Value;
            calendars.Add(new AppleCalendarDescriptor(
                new Uri(homeUri, href).AbsoluteUri,
                string.IsNullOrWhiteSpace(displayName) ? "Apple Calendar" : displayName.Trim()));
        }

        return calendars;
    }

    private async Task<HttpResponseMessage> SendDavAsync(
        AppleCalendarConnection connection,
        HttpMethod method,
        Uri requestUri,
        string? body,
        string contentType,
        string? depth,
        CancellationToken cancellationToken)
    {
        var currentUri = requestUri;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var request = new HttpRequestMessage(method, currentUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", CreateBasicAuthValue(connection));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            if (!string.IsNullOrWhiteSpace(depth))
            {
                request.Headers.Add("Depth", depth);
            }

            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            request.Dispose();

            if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
            {
                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);
                response.Dispose();
                continue;
            }

            if (response.IsSuccessStatusCode || response.StatusCode == (HttpStatusCode)207)
            {
                return response;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new InvalidOperationException($"Apple Calendar request failed with {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}");
        }

        throw new InvalidOperationException("Apple Calendar redirected too many times while discovering the calendar endpoint.");
    }

    private static string BuildCalendarQueryBody(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?><c:calendar-query xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:prop><d:getetag /><c:calendar-data /></d:prop><c:filter><c:comp-filter name=\"VCALENDAR\"><c:comp-filter name=\"VEVENT\"><c:time-range start=\"{0}\" end=\"{1}\" /></c:comp-filter></c:comp-filter></c:filter></c:calendar-query>",
            startUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture),
            endUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
    }

    private static Uri EnsureDirectoryUri(Uri uri)
    {
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/");
    }

    private static string CreateBasicAuthValue(AppleCalendarConnection connection)
    {
        var bytes = Encoding.UTF8.GetBytes($"{connection.AppleId}:{connection.AppSpecificPassword}");
        return Convert.ToBase64String(bytes);
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static CalendarEventRecord CreateSyncedCopy(CalendarEventRecord original, string uid, string appleHref)
    {
        return new CalendarEventRecord
        {
            Id = original.Id,
            ExternalUid = uid,
            Title = original.Title,
            Category = original.Category,
            Description = original.Description,
            Location = original.Location,
            Start = original.Start,
            End = original.End,
            Source = string.IsNullOrWhiteSpace(original.GoogleEventId) ? "Apple" : "Google + Apple",
            GoogleEventId = original.GoogleEventId,
            AppleEventHref = appleHref,
            LastModifiedUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed record AppleCalendarDescriptor(string Href, string DisplayName);
}

public sealed record ApplePullResult(AppleCalendarConnection Connection, IReadOnlyList<CalendarEventRecord> Events);

public sealed record ApplePushResult(AppleCalendarConnection Connection, IReadOnlyList<CalendarEventRecord> Events);

