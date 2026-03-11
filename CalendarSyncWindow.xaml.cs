using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Label_CRM_demo.Models;
using Label_CRM_demo.Services;

namespace Label_CRM_demo;

public partial class CalendarSyncWindow : SnapWindow
{
    private readonly CalendarRepository calendarRepository;
    private readonly CalendarSyncCredentialRepository syncCredentialRepository;
    private readonly GoogleCalendarSyncService googleCalendarSyncService;
    private readonly AppleCalendarSyncService appleCalendarSyncService;
    private CalendarSyncSettings settings;
    private bool isBusy;

    public CalendarSyncWindow()
        : this(App.CalendarEvents, App.CalendarSyncCredentials, App.GoogleCalendar, App.AppleCalendar)
    {
    }

    internal CalendarSyncWindow(
        CalendarRepository calendarRepository,
        CalendarSyncCredentialRepository syncCredentialRepository,
        GoogleCalendarSyncService googleCalendarSyncService,
        AppleCalendarSyncService appleCalendarSyncService)
    {
        this.calendarRepository = calendarRepository;
        this.syncCredentialRepository = syncCredentialRepository;
        this.googleCalendarSyncService = googleCalendarSyncService;
        this.appleCalendarSyncService = appleCalendarSyncService;
        settings = syncCredentialRepository.Load();

        OpenDurationMs = 360;
        StartScale = 0.88;
        StartOffsetY = 32;

        InitializeComponent();
        InitializeInteractiveStates();

        Loaded += (_, _) =>
        {
            LoadState();
            UiAnimator.PlayEntrance(new FrameworkElement[]
            {
                HeaderCard,
                GoogleCard,
                AppleCard,
                ComposerCard,
                EventsCard,
                FooterBand
            }, 24, 65);
        };
    }

    private void InitializeInteractiveStates()
    {
        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            HeaderCard,
            GoogleCard,
            AppleCard,
            ComposerCard,
            EventsCard,
            ConnectGoogleButton,
            PullGoogleButton,
            PushGoogleButton,
            ConnectAppleButton,
            PullAppleButton,
            PushAppleButton,
            SaveEventButton,
            RefreshEventsButton,
            DeleteEventButton
        }, -6, 1.008);
    }

    private void LoadState()
    {
        settings = syncCredentialRepository.Load();
        PopulateConnectionFields();
        RefreshEventGrid();
        SeedEditorDefaults();
        StatusText.Text = "Calendar sync center ready.";
    }

    private void PopulateConnectionFields()
    {
        GoogleClientIdBox.Text = settings.Google.ClientId;
        GoogleCalendarIdBox.Text = string.IsNullOrWhiteSpace(settings.Google.CalendarId) ? "primary" : settings.Google.CalendarId;
        AppleIdBox.Text = settings.Apple.AppleId;
        ApplePasswordBox.Password = settings.Apple.AppSpecificPassword;

        GoogleSummaryText.Text = settings.Google.IsConnected
            ? $"Connected {(string.IsNullOrWhiteSpace(settings.Google.AccountEmail) ? "to Google Calendar" : $"as {settings.Google.AccountEmail}")}. Calendar: {NormalizeCalendarId(settings.Google.CalendarId)}. Last import: {FormatSyncMoment(settings.Google.LastPullUtc)}. Last export: {FormatSyncMoment(settings.Google.LastPushUtc)}."
            : "Paste a Google OAuth desktop app client ID, keep the calendar ID as primary unless you need another calendar, then click Connect.";

        AppleSummaryText.Text = settings.Apple.IsConnected
            ? $"Connected to {settings.Apple.CalendarName}. Last import: {FormatSyncMoment(settings.Apple.LastPullUtc)}. Last export: {FormatSyncMoment(settings.Apple.LastPushUtc)}."
            : "Enter your Apple ID email plus an iCloud app-specific password, then click Connect to discover your Apple calendar.";
    }

    private void RefreshEventGrid(string? selectedId = null)
    {
        var events = calendarRepository.GetEvents();
        EventsGrid.ItemsSource = events;

        LocalSummaryText.Text = $"{events.Count} event(s) stored locally.\n{calendarRepository.StoragePath}";

        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return;
        }

        var selected = events.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            EventsGrid.SelectedItem = selected;
            EventsGrid.ScrollIntoView(selected);
        }
    }

    private void SeedEditorDefaults()
    {
        if (!string.IsNullOrWhiteSpace(EventStartBox.Text) && !string.IsNullOrWhiteSpace(EventEndBox.Text))
        {
            return;
        }

        var nextHour = DateTime.Now.AddMinutes(60 - DateTime.Now.Minute).AddSeconds(-DateTime.Now.Second).AddMilliseconds(-DateTime.Now.Millisecond);
        EventStartBox.Text = nextHour.ToString("M/d/yyyy h:mm tt", CultureInfo.InvariantCulture);
        EventEndBox.Text = nextHour.AddHours(1).ToString("M/d/yyyy h:mm tt", CultureInfo.InvariantCulture);
    }

    private async void ConnectGoogle_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Opening Google sign-in...", async () =>
        {
            CaptureGoogleInputs();
            SaveSettings();
            settings.Google = await googleCalendarSyncService.ConnectAsync(settings.Google);
            SaveSettings();
            PopulateConnectionFields();
            return string.IsNullOrWhiteSpace(settings.Google.AccountEmail)
                ? "Google Calendar connected."
                : $"Google Calendar connected for {settings.Google.AccountEmail}.";
        });
    }

    private async void PullGoogle_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Importing events from Google Calendar...", async () =>
        {
            CaptureGoogleInputs();
            SaveSettings();
            var result = await googleCalendarSyncService.PullAsync(settings.Google);
            settings.Google = result.Connection;
            calendarRepository.MergeRemoteEvents("Google", result.Events);
            SaveSettings();
            PopulateConnectionFields();
            RefreshEventGrid();
            return $"Imported {result.Events.Count} event(s) from Google Calendar.";
        });
    }

    private async void PushGoogle_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Exporting events to Google Calendar...", async () =>
        {
            CaptureGoogleInputs();
            SaveSettings();
            var result = await googleCalendarSyncService.PushAsync(settings.Google, calendarRepository.GetEvents());
            settings.Google = result.Connection;
            calendarRepository.SaveMany(result.Events);
            SaveSettings();
            PopulateConnectionFields();
            RefreshEventGrid();
            return $"Exported {result.Events.Count} event(s) to Google Calendar.";
        });
    }

    private async void ConnectApple_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Connecting to Apple Calendar...", async () =>
        {
            CaptureAppleInputs();
            SaveSettings();
            settings.Apple = await appleCalendarSyncService.ConnectAsync(settings.Apple);
            SaveSettings();
            PopulateConnectionFields();
            return string.IsNullOrWhiteSpace(settings.Apple.CalendarName)
                ? "Apple Calendar connected."
                : $"Apple Calendar connected to {settings.Apple.CalendarName}.";
        });
    }

    private async void PullApple_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Importing events from Apple Calendar...", async () =>
        {
            CaptureAppleInputs();
            SaveSettings();
            var result = await appleCalendarSyncService.PullAsync(settings.Apple);
            settings.Apple = result.Connection;
            calendarRepository.MergeRemoteEvents("Apple", result.Events);
            SaveSettings();
            PopulateConnectionFields();
            RefreshEventGrid();
            return $"Imported {result.Events.Count} event(s) from Apple Calendar.";
        });
    }

    private async void PushApple_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Exporting events to Apple Calendar...", async () =>
        {
            CaptureAppleInputs();
            SaveSettings();
            var result = await appleCalendarSyncService.PushAsync(settings.Apple, calendarRepository.GetEvents());
            settings.Apple = result.Connection;
            calendarRepository.SaveMany(result.Events);
            SaveSettings();
            PopulateConnectionFields();
            RefreshEventGrid();
            return $"Exported {result.Events.Count} event(s) to Apple Calendar.";
        });
    }

    private void SaveEvent_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildEditorEvent(out var item, out var errorMessage))
        {
            StatusText.Text = errorMessage;
            UiAnimator.Shake(ComposerCard);
            return;
        }

        var saved = calendarRepository.Save(item);
        RefreshEventGrid(saved.Id);
        StatusText.Text = string.IsNullOrWhiteSpace(item.GoogleEventId) && string.IsNullOrWhiteSpace(item.AppleEventHref)
            ? "Local event saved."
            : "Event updated. Run Export to push the changes to connected calendars.";
    }

    private void RefreshEvents_Click(object sender, RoutedEventArgs e)
    {
        RefreshEventGrid();
        PopulateConnectionFields();
        StatusText.Text = "Calendar list refreshed from local storage.";
    }

    private void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        if (EventsGrid.SelectedItem is not CalendarEventRecord selected)
        {
            StatusText.Text = "Select an event first if you want to delete it.";
            UiAnimator.Shake(FooterBand);
            return;
        }

        calendarRepository.Delete(selected.Id);
        EventsGrid.SelectedItem = null;
        ClearEditor();
        RefreshEventGrid();
        StatusText.Text = "Selected event deleted from the local calendar store.";
    }

    private void EventsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EventsGrid.SelectedItem is not CalendarEventRecord selected)
        {
            return;
        }

        EventTitleBox.Text = selected.Title;
        EventCategoryBox.Text = selected.Category;
        EventLocationBox.Text = selected.Location;
        EventDescriptionBox.Text = selected.Description;
        EventStartBox.Text = selected.Start.ToLocalTime().ToString("M/d/yyyy h:mm tt", CultureInfo.InvariantCulture);
        EventEndBox.Text = selected.End.ToLocalTime().ToString("M/d/yyyy h:mm tt", CultureInfo.InvariantCulture);
        StatusText.Text = "Selected event loaded into the editor.";
    }

    private bool TryBuildEditorEvent(out CalendarEventRecord item, out string errorMessage)
    {
        item = EventsGrid.SelectedItem as CalendarEventRecord is { } selected
            ? CloneEvent(selected)
            : new CalendarEventRecord();

        var title = EventTitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            errorMessage = "Enter an event title before saving.";
            return false;
        }

        if (!DateTime.TryParse(EventStartBox.Text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var startLocal))
        {
            errorMessage = "Enter a valid local start date and time.";
            return false;
        }

        if (!DateTime.TryParse(EventEndBox.Text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var endLocal))
        {
            errorMessage = "Enter a valid local end date and time.";
            return false;
        }

        if (endLocal <= startLocal)
        {
            errorMessage = "The end time has to be after the start time.";
            return false;
        }

        item.Title = title;
        item.Category = string.IsNullOrWhiteSpace(EventCategoryBox.Text) ? "Task" : EventCategoryBox.Text.Trim();
        item.Location = EventLocationBox.Text.Trim();
        item.Description = EventDescriptionBox.Text.Trim();
        item.Start = new DateTimeOffset(startLocal, TimeZoneInfo.Local.GetUtcOffset(startLocal));
        item.End = new DateTimeOffset(endLocal, TimeZoneInfo.Local.GetUtcOffset(endLocal));

        if (string.IsNullOrWhiteSpace(item.GoogleEventId) && string.IsNullOrWhiteSpace(item.AppleEventHref))
        {
            item.Source = "Local";
        }

        errorMessage = string.Empty;
        return true;
    }

    private void CaptureGoogleInputs()
    {
        var newClientId = GoogleClientIdBox.Text.Trim();
        var clientIdChanged = !string.Equals(settings.Google.ClientId, newClientId, StringComparison.Ordinal);

        if (clientIdChanged)
        {
            settings.Google.AccessToken = string.Empty;
            settings.Google.RefreshToken = string.Empty;
            settings.Google.AccountEmail = string.Empty;
            settings.Google.AccessTokenExpiresUtc = default;
            settings.Google.LastPullUtc = null;
            settings.Google.LastPushUtc = null;
        }

        settings.Google.ClientId = newClientId;
        settings.Google.CalendarId = NormalizeCalendarId(GoogleCalendarIdBox.Text);
    }

    private void CaptureAppleInputs()
    {
        var newAppleId = AppleIdBox.Text.Trim();
        var newPassword = ApplePasswordBox.Password.Trim();
        var connectionChanged =
            !string.Equals(settings.Apple.AppleId, newAppleId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(settings.Apple.AppSpecificPassword, newPassword, StringComparison.Ordinal);

        if (connectionChanged)
        {
            settings.Apple.CalendarHref = string.Empty;
            settings.Apple.CalendarName = string.Empty;
            settings.Apple.LastPullUtc = null;
            settings.Apple.LastPushUtc = null;
        }

        settings.Apple.AppleId = newAppleId;
        settings.Apple.AppSpecificPassword = newPassword;
    }

    private void SaveSettings() => syncCredentialRepository.Save(settings);

    private async Task RunBusyAsync(string startingMessage, Func<Task<string>> action)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        ContentScroller.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        StatusText.Text = startingMessage;

        try
        {
            var resultMessage = await action();
            StatusText.Text = resultMessage;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            UiAnimator.Shake(FooterBand);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            ContentScroller.IsEnabled = true;
            isBusy = false;
        }
    }

    private static string FormatSyncMoment(DateTimeOffset? value)
    {
        return value is null
            ? "never"
            : value.Value.ToLocalTime().ToString("MMM dd, yyyy h:mm tt", CultureInfo.InvariantCulture);
    }

    private static string NormalizeCalendarId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "primary" : value.Trim();
    }

    private static CalendarEventRecord CloneEvent(CalendarEventRecord original)
    {
        return new CalendarEventRecord
        {
            Id = original.Id,
            ExternalUid = original.ExternalUid,
            Title = original.Title,
            Category = original.Category,
            Description = original.Description,
            Location = original.Location,
            Start = original.Start,
            End = original.End,
            Source = original.Source,
            GoogleEventId = original.GoogleEventId,
            AppleEventHref = original.AppleEventHref,
            LastModifiedUtc = original.LastModifiedUtc
        };
    }

    private void ClearEditor()
    {
        EventTitleBox.Clear();
        EventCategoryBox.Text = "Task";
        EventLocationBox.Clear();
        EventDescriptionBox.Clear();
        EventStartBox.Clear();
        EventEndBox.Clear();
        SeedEditorDefaults();
    }
}
