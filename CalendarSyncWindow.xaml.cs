using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Label_CRM_demo.Models;
using Label_CRM_demo.Services;

namespace Label_CRM_demo;

public partial class CalendarSyncWindow : SnapWindow
{
    private static readonly CalendarPalette TaskPalette = new("#1B6FD9", "#EAF2FF", "#FFFFFF", "#D9E6F5", "#1450A8");
    private static readonly CalendarPalette CallPalette = new("#E05A6F", "#FDECEF", "#FFFFFF", "#F2D5DB", "#A63B4D");
    private static readonly CalendarPalette BillingPalette = new("#D48A14", "#FFF3DE", "#FFFFFF", "#F0DFC0", "#8A5B0F");
    private static readonly CalendarPalette ContactPalette = new("#0E8A8A", "#E5F8F6", "#FFFFFF", "#CCE8E4", "#0D6156");
    private static readonly CalendarPalette ContractPalette = new("#2D7A63", "#E9F7F4", "#FFFFFF", "#CFE7DF", "#205949");
    private static readonly CalendarPalette DefaultPalette = new("#4F6B7A", "#EFF5F8", "#FFFFFF", "#D9E5EC", "#36505D");

    private readonly CalendarRepository calendarRepository;
    private readonly CalendarSyncCredentialRepository syncCredentialRepository;
    private readonly GoogleCalendarSyncService googleCalendarSyncService;
    private readonly AppleCalendarSyncService appleCalendarSyncService;
    private CalendarSyncSettings settings;
    private bool isBusy;
    private IReadOnlyList<CalendarEventRecord> currentEvents = Array.Empty<CalendarEventRecord>();
    private DateTime visibleMonthStart = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime selectedDate = DateTime.Today;
    private string? selectedEventId;

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

        calendarRepository.EventsChanged += CalendarRepository_EventsChanged;
        Closed += (_, _) => calendarRepository.EventsChanged -= CalendarRepository_EventsChanged;

        Loaded += async (_, _) =>
        {
            await LoadStateAsync();
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
            NewDraftButton,
            RefreshEventsButton,
            DeleteEventButton,
            PreviousMonthButton,
            TodayMonthButton,
            NextMonthButton
        }, -6, 1.008);
    }

    private async Task LoadStateAsync()
    {
        settings = syncCredentialRepository.Load();
        selectedDate = DateTime.Today;
        visibleMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        selectedEventId = null;
        PopulateConnectionFields();
        await RefreshEventGridAsync();
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

    private async Task RefreshEventGridAsync(string? selectedId = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            selectedEventId = selectedId;
        }

        currentEvents = await calendarRepository.GetEventsAsync();

        if (!string.IsNullOrWhiteSpace(selectedEventId))
        {
            var selected = currentEvents.FirstOrDefault(item => string.Equals(item.Id, selectedEventId, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                selectedEventId = null;
            }
            else
            {
                selectedDate = selected.Start.ToLocalTime().Date;
                visibleMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            }
        }

        LocalSummaryText.Text = BuildLocalSummary(currentEvents);

        var monthEvents = GetEventsForMonth(currentEvents, visibleMonthStart).ToList();
        var selectedDayEvents = GetEventsForDate(currentEvents, selectedDate).ToList();
        var upcomingEvents = currentEvents
            .Where(item => item.End >= DateTimeOffset.Now.AddMinutes(-1))
            .OrderBy(item => item.Start)
            .Take(8)
            .ToList();

        VisibleMonthLabelText.Text = visibleMonthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        VisibleMonthSummaryText.Text = monthEvents.Count == 0
            ? "No items planned"
            : $"{monthEvents.Count} item{(monthEvents.Count == 1 ? string.Empty : "s")} planned";

        var syncedMonthCount = monthEvents.Count(HasExternalSync);
        VisibleMonthSyncText.Text = monthEvents.Count == 0
            ? "0 synced / 0 local"
            : $"{syncedMonthCount} synced / {monthEvents.Count - syncedMonthCount} local";

        SelectedDaySummaryText.Text = BuildSelectedDaySummary(selectedDate, selectedDayEvents.Count);
        MonthDaysItemsControl.ItemsSource = BuildMonthDayCards(currentEvents);

        var selectedDayCards = BuildAgendaCards(selectedDayEvents);
        SelectedDayTitleText.Text = selectedDate.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);
        SelectedDaySubtitleText.Text = selectedDayCards.Count == 0
            ? "No events scheduled for this day yet. Save a new event above or choose a different date."
            : $"{selectedDayCards.Count} scheduled item{(selectedDayCards.Count == 1 ? string.Empty : "s")}. Click one to load it into the editor.";
        SelectedDayCountText.Text = $"{selectedDayCards.Count} item{(selectedDayCards.Count == 1 ? string.Empty : "s")}";
        SelectedDayEmptyText.Visibility = selectedDayCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectedDayEventsItemsControl.ItemsSource = selectedDayCards;

        var upcomingCards = BuildAgendaCards(upcomingEvents);
        UpcomingTimelineCountText.Text = $"{upcomingCards.Count} upcoming";
        UpcomingTimelineEmptyText.Visibility = upcomingCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpcomingTimelineItemsControl.ItemsSource = upcomingCards;
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
            await calendarRepository.MergeRemoteEventsAsync("Google", result.Events);
            SaveSettings();
            PopulateConnectionFields();
            await RefreshEventGridAsync();
            return $"Imported {result.Events.Count} event(s) from Google Calendar.";
        });
    }

    private async void PushGoogle_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Exporting events to Google Calendar...", async () =>
        {
            CaptureGoogleInputs();
            SaveSettings();
            var result = await googleCalendarSyncService.PushAsync(settings.Google, await calendarRepository.GetEventsAsync());
            settings.Google = result.Connection;
            await calendarRepository.SaveManyAsync(result.Events);
            SaveSettings();
            PopulateConnectionFields();
            await RefreshEventGridAsync();
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
            await calendarRepository.MergeRemoteEventsAsync("Apple", result.Events);
            SaveSettings();
            PopulateConnectionFields();
            await RefreshEventGridAsync();
            return $"Imported {result.Events.Count} event(s) from Apple Calendar.";
        });
    }

    private async void PushApple_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Exporting events to Apple Calendar...", async () =>
        {
            CaptureAppleInputs();
            SaveSettings();
            var result = await appleCalendarSyncService.PushAsync(settings.Apple, await calendarRepository.GetEventsAsync());
            settings.Apple = result.Connection;
            await calendarRepository.SaveManyAsync(result.Events);
            SaveSettings();
            PopulateConnectionFields();
            await RefreshEventGridAsync();
            return $"Exported {result.Events.Count} event(s) to Apple Calendar.";
        });
    }

    private async void SaveEvent_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildEditorEvent(out var item, out var errorMessage))
        {
            StatusText.Text = errorMessage;
            UiAnimator.Shake(ComposerCard);
            return;
        }

        var saved = await calendarRepository.SaveAsync(item);
        selectedEventId = saved.Id;
        selectedDate = saved.Start.ToLocalTime().Date;
        visibleMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        await RefreshEventGridAsync(saved.Id);
        LoadEventIntoEditor(GetSelectedEvent() ?? saved);
        StatusText.Text = string.IsNullOrWhiteSpace(item.GoogleEventId) && string.IsNullOrWhiteSpace(item.AppleEventHref)
            ? "Local event saved."
            : "Event updated. Run Export to push the changes to connected calendars.";
    }

    private async void NewDraft_Click(object sender, RoutedEventArgs e)
    {
        selectedEventId = null;
        ClearEditor();
        await RefreshEventGridAsync();
        StatusText.Text = "Started a new local event draft.";
    }

    private async void RefreshEvents_Click(object sender, RoutedEventArgs e)
    {
        await RefreshEventGridAsync(selectedEventId);
        PopulateConnectionFields();
        StatusText.Text = "Calendar board refreshed from local storage.";
    }

    private async void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedEvent();
        if (selected is null)
        {
            StatusText.Text = "Select an event from the calendar board first if you want to delete it.";
            UiAnimator.Shake(FooterBand);
            return;
        }

        await calendarRepository.DeleteAsync(selected.Id);
        selectedEventId = null;
        selectedDate = selected.Start.ToLocalTime().Date;
        ClearEditor();
        await RefreshEventGridAsync();
        StatusText.Text = "Selected event deleted from the local calendar store.";
    }
    private async void MonthDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateTime clickedDate })
        {
            return;
        }

        selectedDate = clickedDate.Date;
        visibleMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        selectedEventId = null;
        await RefreshEventGridAsync();
        StatusText.Text = $"Showing {selectedDate:dddd, MMMM d}.";
    }

    private async void AgendaEvent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string eventId })
        {
            return;
        }

        var selected = currentEvents.FirstOrDefault(item => string.Equals(item.Id, eventId, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            await RefreshEventGridAsync();
            return;
        }

        selectedEventId = selected.Id;
        selectedDate = selected.Start.ToLocalTime().Date;
        visibleMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        await RefreshEventGridAsync(selected.Id);
        LoadEventIntoEditor(GetSelectedEvent() ?? selected);
        StatusText.Text = "Selected event loaded into the editor.";
    }

    private async void PreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        visibleMonthStart = visibleMonthStart.AddMonths(-1);
        selectedDate = visibleMonthStart;
        selectedEventId = null;
        await RefreshEventGridAsync();
        StatusText.Text = $"Showing {visibleMonthStart:MMMM yyyy}.";
    }

    private async void TodayMonth_Click(object sender, RoutedEventArgs e)
    {
        selectedDate = DateTime.Today;
        visibleMonthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        selectedEventId = null;
        await RefreshEventGridAsync();
        StatusText.Text = "Returned to today.";
    }

    private async void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        visibleMonthStart = visibleMonthStart.AddMonths(1);
        selectedDate = visibleMonthStart;
        selectedEventId = null;
        await RefreshEventGridAsync();
        StatusText.Text = $"Showing {visibleMonthStart:MMMM yyyy}.";
    }

    private bool TryBuildEditorEvent(out CalendarEventRecord item, out string errorMessage)
    {
        item = GetSelectedEvent() is { } selected
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

    private Task RefreshVisibleEventsAsync()
        => RefreshEventGridAsync(selectedEventId);

    private void CalendarRepository_EventsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => _ = RefreshVisibleEventsAsync());
            return;
        }

        _ = RefreshVisibleEventsAsync();
    }

    private CalendarEventRecord? GetSelectedEvent()
        => string.IsNullOrWhiteSpace(selectedEventId)
            ? null
            : currentEvents.FirstOrDefault(item => string.Equals(item.Id, selectedEventId, StringComparison.OrdinalIgnoreCase));

    private void LoadEventIntoEditor(CalendarEventRecord selected)
    {
        EventTitleBox.Text = selected.Title;
        EventCategoryBox.Text = selected.Category;
        EventLocationBox.Text = selected.Location;
        EventDescriptionBox.Text = selected.Description;
        EventStartBox.Text = selected.Start.ToLocalTime().ToString("M/d/yyyy h:mm tt", CultureInfo.InvariantCulture);
        EventEndBox.Text = selected.End.ToLocalTime().ToString("M/d/yyyy h:mm tt", CultureInfo.InvariantCulture);
    }

    private string BuildLocalSummary(IReadOnlyList<CalendarEventRecord> events)
    {
        var syncedCount = events.Count(HasExternalSync);
        return $"{events.Count} event(s) in the shared CRM calendar.{Environment.NewLine}{syncedCount} synced / {events.Count - syncedCount} local only.{Environment.NewLine}Store: {calendarRepository.StoragePath}";
    }

    private List<CalendarMonthDayCard> BuildMonthDayCards(IReadOnlyList<CalendarEventRecord> events)
    {
        var cards = new List<CalendarMonthDayCard>(42);
        var firstCalendarDay = StartOfCalendarGrid(visibleMonthStart);

        for (var index = 0; index < 42; index++)
        {
            var date = firstCalendarDay.AddDays(index);
            var dayEvents = GetEventsForDate(events, date).ToList();

            cards.Add(new CalendarMonthDayCard
            {
                Date = date,
                DayNumber = date.Day.ToString(CultureInfo.InvariantCulture),
                MonthLabel = date.Day == 1 ? date.ToString("MMM", CultureInfo.CurrentCulture) : string.Empty,
                MonthLabelVisibility = date.Day == 1 ? Visibility.Visible : Visibility.Collapsed,
                IsCurrentMonth = date.Month == visibleMonthStart.Month && date.Year == visibleMonthStart.Year,
                IsToday = date.Date == DateTime.Today,
                IsSelected = date.Date == selectedDate.Date,
                VisibleEvents = dayEvents.Take(2).Select(BuildDayEventChip).ToList(),
                OverflowCount = Math.Max(0, dayEvents.Count - 2)
            });
        }

        return cards;
    }

    private List<CalendarAgendaCard> BuildAgendaCards(IEnumerable<CalendarEventRecord> events)
    {
        return events
            .OrderBy(item => item.Start)
            .Select(item =>
            {
                var startLocal = item.Start.ToLocalTime();
                var endLocal = item.End.ToLocalTime();
                var palette = GetPalette(item);
                var detailParts = new List<string>();

                if (!string.IsNullOrWhiteSpace(item.Location))
                {
                    detailParts.Add(item.Location.Trim());
                }

                detailParts.Add($"Source: {item.Source}");

                var timeLabel = startLocal.Date == endLocal.Date
                    ? $"{startLocal:ddd, MMM d} â€¢ {startLocal:h:mm tt} - {endLocal:h:mm tt}"
                    : $"{startLocal:ddd, MMM d h:mm tt} - {endLocal:ddd, MMM d h:mm tt}";

                return new CalendarAgendaCard
                {
                    Id = item.Id,
                    MonthLabel = startLocal.ToString("MMM", CultureInfo.CurrentCulture).ToUpperInvariant(),
                    DayNumber = startLocal.Day.ToString(CultureInfo.InvariantCulture),
                    Title = item.Title,
                    TimeLabel = timeLabel,
                    DetailText = string.Join(" â€¢ ", detailParts),
                    DetailVisibility = Visibility.Visible,
                    DescriptionPreview = item.Description,
                    DescriptionVisibility = string.IsNullOrWhiteSpace(item.Description) ? Visibility.Collapsed : Visibility.Visible,
                    Category = item.Category,
                    SyncTargets = item.SyncTargetsDisplay,
                    IsSelected = string.Equals(item.Id, selectedEventId, StringComparison.OrdinalIgnoreCase),
                    SurfaceBrush = palette.SurfaceBrush,
                    BorderBrush = palette.BorderBrush,
                    AccentBrush = palette.AccentBrush,
                    AccentSoftBrush = palette.AccentSoftBrush,
                    AccentForegroundBrush = palette.AccentForegroundBrush
                };
            })
            .ToList();
    }

    private static CalendarDayEventChip BuildDayEventChip(CalendarEventRecord item)
    {
        var palette = GetPalette(item);
        return new CalendarDayEventChip
        {
            Label = $"{item.Start.ToLocalTime():h:mm tt} {item.Title}",
            BackgroundBrush = palette.AccentSoftBrush,
            ForegroundBrush = palette.AccentForegroundBrush
        };
    }

    private static IEnumerable<CalendarEventRecord> GetEventsForMonth(IEnumerable<CalendarEventRecord> events, DateTime monthStart)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1).Date;
        return events.Where(item => OccursInRange(item, monthStart.Date, monthEnd));
    }

    private static IEnumerable<CalendarEventRecord> GetEventsForDate(IEnumerable<CalendarEventRecord> events, DateTime date)
        => events.Where(item => OccursInRange(item, date.Date, date.Date));
    private static bool OccursInRange(CalendarEventRecord item, DateTime rangeStart, DateTime rangeEnd)
    {
        var startDate = item.Start.ToLocalTime().Date;
        var endDate = item.End.ToLocalTime().Date;
        return startDate <= rangeEnd && endDate >= rangeStart;
    }

    private static DateTime StartOfCalendarGrid(DateTime monthStart)
    {
        var firstCalendarDay = monthStart.Date;
        while (firstCalendarDay.DayOfWeek != DayOfWeek.Sunday)
        {
            firstCalendarDay = firstCalendarDay.AddDays(-1);
        }

        return firstCalendarDay;
    }

    private static string BuildSelectedDaySummary(DateTime date, int count)
    {
        var label = date.Date == DateTime.Today
            ? "Today"
            : date.ToString("MMM d", CultureInfo.CurrentCulture);

        return $"{label} â€¢ {count} item{(count == 1 ? string.Empty : "s")}";
    }

    private static bool HasExternalSync(CalendarEventRecord item)
        => !string.IsNullOrWhiteSpace(item.GoogleEventId) || !string.IsNullOrWhiteSpace(item.AppleEventHref);

    private static CalendarPalette GetPalette(CalendarEventRecord item)
    {
        var category = item.Category.Trim().ToLowerInvariant();

        if (category.Contains("bill") || category.Contains("payment"))
        {
            return BillingPalette;
        }

        if (category.Contains("call"))
        {
            return CallPalette;
        }

        if (category.Contains("contract"))
        {
            return ContractPalette;
        }

        if (category.Contains("contact"))
        {
            return ContactPalette;
        }

        if (category.Contains("task"))
        {
            return TaskPalette;
        }

        return DefaultPalette;
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

internal sealed class CalendarMonthDayCard
{
    public DateTime Date { get; init; }

    public string DayNumber { get; init; } = string.Empty;

    public string MonthLabel { get; init; } = string.Empty;

    public Visibility MonthLabelVisibility { get; init; }

    public bool IsCurrentMonth { get; init; }

    public bool IsToday { get; init; }

    public bool IsSelected { get; init; }

    public IReadOnlyList<CalendarDayEventChip> VisibleEvents { get; init; } = Array.Empty<CalendarDayEventChip>();

    public int OverflowCount { get; init; }

    public string OverflowLabel => OverflowCount > 0 ? $"+{OverflowCount} more" : string.Empty;

    public Visibility OverflowVisibility => OverflowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
}

internal sealed class CalendarDayEventChip
{
    public string Label { get; init; } = string.Empty;

    public Brush BackgroundBrush { get; init; } = Brushes.Transparent;

    public Brush ForegroundBrush { get; init; } = Brushes.Black;
}

internal sealed class CalendarAgendaCard
{
    public string Id { get; init; } = string.Empty;

    public string MonthLabel { get; init; } = string.Empty;

    public string DayNumber { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string TimeLabel { get; init; } = string.Empty;

    public string DetailText { get; init; } = string.Empty;

    public Visibility DetailVisibility { get; init; }

    public string DescriptionPreview { get; init; } = string.Empty;

    public Visibility DescriptionVisibility { get; init; }

    public string Category { get; init; } = string.Empty;

    public string SyncTargets { get; init; } = string.Empty;

    public bool IsSelected { get; init; }

    public Brush SurfaceBrush { get; init; } = Brushes.White;

    public Brush BorderBrush { get; init; } = Brushes.Transparent;

    public Brush AccentBrush { get; init; } = Brushes.Transparent;

    public Brush AccentSoftBrush { get; init; } = Brushes.Transparent;

    public Brush AccentForegroundBrush { get; init; } = Brushes.Black;
}

internal sealed class CalendarPalette
{
    public CalendarPalette(string accentHex, string accentSoftHex, string surfaceHex, string borderHex, string accentForegroundHex)
    {
        AccentBrush = CreateBrush(accentHex);
        AccentSoftBrush = CreateBrush(accentSoftHex);
        SurfaceBrush = CreateBrush(surfaceHex);
        BorderBrush = CreateBrush(borderHex);
        AccentForegroundBrush = CreateBrush(accentForegroundHex);
    }

    public Brush AccentBrush { get; }

    public Brush AccentSoftBrush { get; }

    public Brush SurfaceBrush { get; }

    public Brush BorderBrush { get; }

    public Brush AccentForegroundBrush { get; }

    private static SolidColorBrush CreateBrush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
}

















