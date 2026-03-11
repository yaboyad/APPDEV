using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class GoogleCalendarSyncService
{
    private const string RedirectUri = "http://127.0.0.1:45873/";
    private static readonly Uri TokenUri = new("https://oauth2.googleapis.com/token");
    private static readonly Uri UserInfoUri = new("https://openidconnect.googleapis.com/v1/userinfo");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;

    public GoogleCalendarSyncService(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<GoogleCalendarConnection> ConnectAsync(GoogleCalendarConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ClientId))
        {
            throw new InvalidOperationException("Enter a Google OAuth desktop app client ID first.");
        }

        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = CreateCodeVerifier();
        var codeChallenge = CreateCodeChallenge(codeVerifier);

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        var authorizationUrl = BuildAuthorizationUrl(connection.ClientId, state, codeChallenge);
        Process.Start(new ProcessStartInfo(authorizationUrl) { UseShellExecute = true });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        var context = await WaitForContextAsync(listener, timeout.Token);
        var query = ParseQuery(context.Request.Url?.Query);

        if (query.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            await WriteBrowserResponseAsync(context.Response, "Google sign-in was cancelled. You can close this tab and return to the app.");
            throw new InvalidOperationException($"Google sign-in failed: {oauthError}.");
        }

        if (!query.TryGetValue("state", out var responseState) || !string.Equals(state, responseState, StringComparison.Ordinal))
        {
            await WriteBrowserResponseAsync(context.Response, "This Google callback did not match the app request. You can close this tab.");
            throw new InvalidOperationException("Google sign-in returned an invalid state value.");
        }

        if (!query.TryGetValue("code", out var authorizationCode) || string.IsNullOrWhiteSpace(authorizationCode))
        {
            await WriteBrowserResponseAsync(context.Response, "Google did not return an authorization code. You can close this tab.");
            throw new InvalidOperationException("Google sign-in did not return an authorization code.");
        }

        await WriteBrowserResponseAsync(context.Response, "Google Calendar is connected. You can close this tab and return to Label CRM Demo.");

        var token = await ExchangeCodeAsync(connection.ClientId, authorizationCode, codeVerifier, timeout.Token);
        connection.AccessToken = token.AccessToken;
        connection.RefreshToken = string.IsNullOrWhiteSpace(token.RefreshToken) ? connection.RefreshToken : token.RefreshToken;
        connection.AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn)).AddMinutes(-1);
        connection.AccountEmail = await GetAccountEmailAsync(connection.AccessToken, timeout.Token);
        return connection;
    }

    public async Task<GooglePullResult> PullAsync(GoogleCalendarConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var authorized = await EnsureAuthorizedAsync(connection, cancellationToken);
        var calendarId = string.IsNullOrWhiteSpace(authorized.CalendarId) ? "primary" : authorized.CalendarId.Trim();
        var uri = BuildEventsUri(calendarId, DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow.AddDays(120));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorized.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "Google Calendar pull", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GoogleEventsResponse>(stream, SerializerOptions, cancellationToken)
            ?? new GoogleEventsResponse();

        var events = payload.Items
            .Where(item => !string.Equals(item.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            .Select(item => ToCalendarEvent(item, connection.AccountEmail))
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .ToList();

        authorized.LastPullUtc = DateTimeOffset.UtcNow;
        return new GooglePullResult(authorized, events);
    }

    public async Task<GooglePushResult> PushAsync(
        GoogleCalendarConnection connection,
        IEnumerable<CalendarEventRecord> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(events);

        var authorized = await EnsureAuthorizedAsync(connection, cancellationToken);
        var calendarId = string.IsNullOrWhiteSpace(authorized.CalendarId) ? "primary" : authorized.CalendarId.Trim();
        var syncedEvents = new List<CalendarEventRecord>();

        foreach (var item in events.Where(candidate => candidate.End >= DateTimeOffset.Now.AddDays(-2)).OrderBy(candidate => candidate.Start))
        {
            var requestModel = new GoogleEventRequest
            {
                Summary = item.Title,
                Description = item.Description,
                Location = item.Location,
                Start = new GoogleEventDateTimeRequest { DateTime = item.Start.ToUniversalTime().ToString("o") },
                End = new GoogleEventDateTimeRequest { DateTime = item.End.ToUniversalTime().ToString("o") }
            };

            var method = string.IsNullOrWhiteSpace(item.GoogleEventId) ? HttpMethod.Post : new HttpMethod("PATCH");
            var endpoint = string.IsNullOrWhiteSpace(item.GoogleEventId)
                ? $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events"
                : $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(item.GoogleEventId)}";

            using var request = new HttpRequestMessage(method, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestModel), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorized.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, "Google Calendar push", cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<GoogleEventItem>(stream, SerializerOptions, cancellationToken)
                ?? new GoogleEventItem();

            syncedEvents.Add(CreateSyncedCopy(item, payload));
        }

        authorized.LastPushUtc = DateTimeOffset.UtcNow;
        return new GooglePushResult(authorized, syncedEvents);
    }

    private async Task<GoogleCalendarConnection> EnsureAuthorizedAsync(GoogleCalendarConnection connection, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(connection.AccessToken) && connection.AccessTokenExpiresUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return connection;
        }

        if (string.IsNullOrWhiteSpace(connection.ClientId) || string.IsNullOrWhiteSpace(connection.RefreshToken))
        {
            throw new InvalidOperationException("Connect Google Calendar first so the app can refresh your access token.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = connection.ClientId,
                ["refresh_token"] = connection.RefreshToken,
                ["grant_type"] = "refresh_token"
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "Google token refresh", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Google token refresh returned an empty response.");

        connection.AccessToken = token.AccessToken;
        connection.RefreshToken = string.IsNullOrWhiteSpace(token.RefreshToken) ? connection.RefreshToken : token.RefreshToken;
        connection.AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn)).AddMinutes(-1);
        return connection;
    }

    private async Task<TokenResponse> ExchangeCodeAsync(string clientId, string authorizationCode, string codeVerifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code"] = authorizationCode,
                ["code_verifier"] = codeVerifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = RedirectUri
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "Google authorization", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TokenResponse>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Google authorization returned an empty response.");
    }

    private async Task<string> GetAccountEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<UserInfoResponse>(stream, SerializerOptions, cancellationToken);
        return payload?.Email ?? string.Empty;
    }

    private static async Task<HttpListenerContext> WaitForContextAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                listener.Stop();
            }
            catch
            {
                // Ignore cleanup failures while cancelling the wait.
            }
        });

        try
        {
            return await listener.GetContextAsync();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            throw new TimeoutException("Google sign-in timed out before the browser returned to the app.", ex);
        }
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response, string message)
    {
        var html = $"<html><body style=\"font-family:Segoe UI;padding:24px;background:#07131F;color:#F6FBFD;\"><h2 style=\"margin-top:0;\">Label CRM Demo</h2><p>{WebUtility.HtmlEncode(message)}</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.OutputStream.Close();
    }

    private static string BuildAuthorizationUrl(string clientId, string state, string codeChallenge)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/userinfo.email openid",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        return "https://accounts.google.com/o/oauth2/v2/auth?" + BuildQueryString(parameters);
    }

    private static Uri BuildEventsUri(string calendarId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var parameters = new Dictionary<string, string>
        {
            ["singleEvents"] = "true",
            ["orderBy"] = "startTime",
            ["maxResults"] = "100",
            ["timeMin"] = startUtc.ToString("o", CultureInfo.InvariantCulture),
            ["timeMax"] = endUtc.ToString("o", CultureInfo.InvariantCulture)
        };

        return new Uri($"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events?{BuildQueryString(parameters)}");
    }

    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(query))
        {
            return values;
        }

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex < 0)
            {
                values[Uri.UnescapeDataString(part)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(part[..separatorIndex]);
            var value = Uri.UnescapeDataString(part[(separatorIndex + 1)..]);
            values[key] = value;
        }

        return values;
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static CalendarEventRecord ToCalendarEvent(GoogleEventItem item, string accountEmail)
    {
        var start = ParseGoogleDateTime(item.Start);
        var end = ParseGoogleDateTime(item.End);

        if (end == default || end <= start)
        {
            end = start.AddHours(1);
        }

        return new CalendarEventRecord
        {
            ExternalUid = string.IsNullOrWhiteSpace(item.ICalUid) ? item.Id ?? string.Empty : item.ICalUid,
            Title = item.Summary ?? string.Empty,
            Category = "Google",
            Description = item.Description ?? string.Empty,
            Location = item.Location ?? string.Empty,
            Start = start,
            End = end,
            Source = string.IsNullOrWhiteSpace(accountEmail) ? "Google" : $"Google ({accountEmail})",
            GoogleEventId = item.Id ?? string.Empty
        };
    }

    private static DateTimeOffset ParseGoogleDateTime(GoogleDateTimeContainer? container)
    {
        if (container is null)
        {
            return default;
        }

        if (!string.IsNullOrWhiteSpace(container.DateTime) &&
            DateTimeOffset.TryParse(container.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return timestamp;
        }

        if (!string.IsNullOrWhiteSpace(container.Date) &&
            DateTime.TryParseExact(container.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
        }

        return default;
    }

    private static CalendarEventRecord CreateSyncedCopy(CalendarEventRecord original, GoogleEventItem payload)
    {
        return new CalendarEventRecord
        {
            Id = original.Id,
            ExternalUid = string.IsNullOrWhiteSpace(payload.ICalUid) ? original.ExternalUid : payload.ICalUid,
            Title = original.Title,
            Category = original.Category,
            Description = original.Description,
            Location = original.Location,
            Start = original.Start,
            End = original.End,
            Source = string.IsNullOrWhiteSpace(original.AppleEventHref) ? "Google" : "Google + Apple",
            GoogleEventId = string.IsNullOrWhiteSpace(payload.Id) ? original.GoogleEventId : payload.Id,
            AppleEventHref = original.AppleEventHref,
            LastModifiedUtc = DateTimeOffset.UtcNow
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var compactBody = string.IsNullOrWhiteSpace(body)
            ? string.Empty
            : " " + body.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();

        throw new InvalidOperationException($"{operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}.{compactBody}");
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class UserInfoResponse
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class GoogleEventsResponse
    {
        [JsonPropertyName("items")]
        public List<GoogleEventItem> Items { get; set; } = new();
    }

    private sealed class GoogleEventItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("iCalUID")]
        public string? ICalUid { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("start")]
        public GoogleDateTimeContainer? Start { get; set; }

        [JsonPropertyName("end")]
        public GoogleDateTimeContainer? End { get; set; }
    }

    private sealed class GoogleEventRequest
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("start")]
        public GoogleEventDateTimeRequest Start { get; set; } = new();

        [JsonPropertyName("end")]
        public GoogleEventDateTimeRequest End { get; set; } = new();
    }

    private sealed class GoogleEventDateTimeRequest
    {
        [JsonPropertyName("dateTime")]
        public string DateTime { get; set; } = string.Empty;
    }

    private sealed class GoogleDateTimeContainer
    {
        [JsonPropertyName("dateTime")]
        public string? DateTime { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }
}

public sealed record GooglePullResult(GoogleCalendarConnection Connection, IReadOnlyList<CalendarEventRecord> Events);

public sealed record GooglePushResult(GoogleCalendarConnection Connection, IReadOnlyList<CalendarEventRecord> Events);
