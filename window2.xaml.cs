using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Label_CRM_demo.Models;
using Label_CRM_demo.Services;

namespace Label_CRM_demo;

public partial class Window2 : Window
{
    private const string ManagedCalendarEventPrefix = "workspace-";
    private static readonly Brush SupportNeutralBrush = CreateBrush("#A9C0CA");
    private static readonly Brush SupportSuccessBrush = CreateBrush("#8EE7AF");
    private static readonly Brush SupportUrgentBrush = CreateBrush("#F7C86D");
    private static readonly Brush SupportErrorBrush = CreateBrush("#FCA5A5");

    private readonly DispatcherTimer clockTimer = new DispatcherTimer();
    private readonly AuthenticatedUser currentUser;
    private readonly WorkspaceRepository workspaceRepository;
    private readonly CalendarRepository calendarRepository;
    private readonly SupportConversationRepository supportConversationRepository;
    private readonly ObservableCollection<SupportMessageRow> supportMessages = new ObservableCollection<SupportMessageRow>();
    private readonly ObservableCollection<ContactRecord> contacts = new ObservableCollection<ContactRecord>();
    private readonly ObservableCollection<ContractRecord> contracts = new ObservableCollection<ContractRecord>();
    private readonly ObservableCollection<CalendarEventRecord> calendarItems = new ObservableCollection<CalendarEventRecord>();
    private readonly ObservableCollection<ActivityRow> activityRows = new ObservableCollection<ActivityRow>();
    private string? editingContactId;
    private string? editingContractId;

    public Window2()
        : this(new AuthenticatedUser("Admin", "Admin"), App.WorkspaceData, App.CalendarEvents, new SupportConversationRepository())
    {
    }

    public Window2(AuthenticatedUser currentUser)
        : this(currentUser, App.WorkspaceData, App.CalendarEvents, new SupportConversationRepository())
    {
    }

    internal Window2(
        AuthenticatedUser currentUser,
        WorkspaceRepository workspaceRepository,
        CalendarRepository calendarRepository,
        SupportConversationRepository supportConversationRepository)
    {
        this.currentUser = currentUser;
        this.workspaceRepository = workspaceRepository;
        this.calendarRepository = calendarRepository;
        this.supportConversationRepository = supportConversationRepository;

        InitializeComponent();
        InitializeInteractiveStates();

        SupportMessagesItemsControl.ItemsSource = supportMessages;
        ContactsGrid.ItemsSource = contacts;
        ContractsGrid.ItemsSource = contracts;
        CalendarGrid.ItemsSource = calendarItems;
        ActivityGrid.ItemsSource = activityRows;

        SetSupportStatus("Support inbox ready.", SupportNeutralBrush);
        SetContactStatus("Add a contact or select one below to edit it.", SupportNeutralBrush);
        SetContractStatus("Add a contract or select one below to edit it.", SupportNeutralBrush);
        ContractTypeBox.SelectedIndex = 0;
        ContractStateBox.SelectedIndex = 0;
        ContractStartPicker.SelectedDate = DateTime.Today;

        Activated += (_, _) =>
        {
            UpdateSupportSummary();
            LoadDashboardData();
        };

        clockTimer.Interval = TimeSpan.FromSeconds(1);
        clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm:ss tt", CultureInfo.CurrentCulture);
    }

    private static SolidColorBrush CreateBrush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        clockTimer.Start();
        ApplyUserState();
        LoadSupportConversation();
        LoadDashboardData();

        UiAnimator.PlayEntrance(new FrameworkElement[]
        {
            SidebarPanel,
            HeroCard,
            LaunchCard,
            AccountCard,
            PaymentMetricCard,
            RevenueCard,
            TasksCard,
            PaymentsSection,
            CalendarSection,
            ContactsSection,
            ContractsManagerSection,
            SupportSection,
            SupportSummaryCard,
            ActivitySection
        }, 28, 70);
    }

    private void InitializeInteractiveStates()
    {
        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            HeroCard,
            LaunchCard,
            AccountCard,
            PaymentMetricCard,
            RevenueCard,
            TasksCard,
            StoreCard,
            ContactsSection,
            ContractsManagerSection,
            SupportSection,
            SupportComposerCard,
            SupportSummaryCard
        }, -7, 1.012);

        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            OverviewButton,
            AccountButton,
            PaymentsButton,
            ContractsButton,
            CalendarButton,
            SmsButton,
            EmailButton,
            SupportButton,
            OpenPaymentsButton,
            OpenContractsButton,
            OpenSmsButton,
            OpenSupportButton,
            SaveContactButton,
            ResetContactButton,
            SaveContractButton,
            ResetContractButton,
            SendSupportButton,
            ClearSupportButton,
            BillingPromptButton,
            AccessPromptButton,
            CalendarPromptButton,
            ContractsPromptButton,
            LaunchPromptButton,
            LogoutButton
        }, -4, 1.01);
    }

    private void ApplyUserState()
    {
        UserNameText.Text = currentUser.DisplayName;
        WelcomeText.Text = $"Welcome back, {currentUser.DisplayName}";
        AccountStatusText.Text = "Protected";
        NextPaymentText.Text = "$49.00";
        NextPaymentDateText.Text = $"Due {GetNextPaymentDueDate(DateTime.Today):MMMM dd}";
    }

    private void LoadDashboardData()
    {
        PaymentsGrid.ItemsSource = BuildPaymentRows();
        LoadWorkspaceData();
    }

    private void LoadWorkspaceData()
    {
        var snapshot = workspaceRepository.LoadForUser(currentUser.Username);
        ReplaceCollection(contacts, snapshot.Contacts);
        ReplaceCollection(contracts, snapshot.Contracts);
        SyncWorkspaceCalendarEvents();
        RefreshCalendarGrid();
        RefreshDashboardSummary();
        RefreshStorageSummary();
        RefreshActivityGrid();
    }

    private void RefreshCalendarGrid()
    {
        ReplaceCollection(calendarItems, calendarRepository.GetUpcomingEvents(8));
    }

    private void RefreshDashboardSummary()
    {
        RevenueText.Text = contracts.Count(contract => IsActiveContract(contract.Status)).ToString(CultureInfo.InvariantCulture);
        TasksText.Text = CountUpcomingFollowUps().ToString(CultureInfo.InvariantCulture);
        NextPaymentText.Text = "$49.00";
        NextPaymentDateText.Text = $"Due {GetNextPaymentDueDate(DateTime.Today):MMMM dd}";
    }

    private void RefreshStorageSummary()
    {
        StorageText.Text = $"{contacts.Count} contacts / {contracts.Count} contracts{Environment.NewLine}CRM: {workspaceRepository.StoragePath}{Environment.NewLine}Calendar: {calendarRepository.StoragePath}";
    }

    private void RefreshActivityGrid()
    {
        var latestContact = contacts.OrderByDescending(contact => contact.UpdatedUtc).FirstOrDefault();
        var latestContract = contracts.OrderByDescending(contract => contract.UpdatedUtc).FirstOrDefault();
        var latestSupportMessage = supportMessages.LastOrDefault();
        var nextCalendarEvent = calendarItems.FirstOrDefault();
        var calendarStatus = calendarRepository.GetUpcomingEvents(12).Any(item =>
            !string.IsNullOrWhiteSpace(item.GoogleEventId) ||
            !string.IsNullOrWhiteSpace(item.AppleEventHref))
            ? "Synced"
            : "Local";

        ReplaceCollection(activityRows, new[]
        {
            new ActivityRow(
                "Contacts",
                latestContact is null ? "No contacts saved yet" : $"{latestContact.FullName} saved locally",
                currentUser.DisplayName,
                latestContact is null ? "Ready" : latestContact.UpdatedUtc.ToLocalTime().ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture)),
            new ActivityRow(
                "Contracts",
                latestContract is null ? "No contracts saved yet" : $"{latestContract.Title} saved locally",
                currentUser.DisplayName,
                latestContract is null ? "Ready" : latestContract.UpdatedUtc.ToLocalTime().ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture)),
            new ActivityRow(
                "Calendar",
                nextCalendarEvent is null ? "No upcoming events scheduled yet" : $"{nextCalendarEvent.Title} is on deck",
                currentUser.DisplayName,
                calendarStatus),
            new ActivityRow(
                "Support",
                latestSupportMessage is null ? "Support inbox is standing by" : $"{latestSupportMessage.Channel} thread updated",
                latestSupportMessage?.SenderName ?? "Titan Support",
                latestSupportMessage?.IsUrgent == true ? "Priority" : "Open")
        });
    }

    private void LoadSupportConversation()
    {
        supportMessages.Clear();

        foreach (var message in supportConversationRepository.LoadConversation(currentUser))
        {
            supportMessages.Add(message);
        }

        UpdateSupportSummary();
        SetSupportStatus("Support inbox ready.", SupportNeutralBrush);
        ScrollSupportTranscriptToEnd();
    }

    private void UpdateSupportSummary()
    {
        var latestMessage = supportMessages.LastOrDefault();
        var latestReply = supportMessages.LastOrDefault(message => !message.IsFromUser);

        SupportCoverageText.Text = "Automated 24/7";
        SupportThreadStateText.Text = supportMessages.Count == 0 ? "Idle" : "Open";
        SupportLastReplyText.Text = latestReply is null ? "Waiting" : latestReply.CreatedAt.ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture);
        SupportRoutingText.Text = latestMessage?.Channel ?? "General";
        SupportMessageCountText.Text = $"{supportMessages.Count} saved message(s)";
        SupportStoragePathText.Text = supportConversationRepository.GetStoragePath(currentUser);
    }
    private void Nav_Overview_Click(object sender, RoutedEventArgs e)
    {
        DashboardScrollViewer.ScrollToTop();
        Activate();
    }

    private void Nav_Account_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateAccount(currentUser));

    private void Nav_Payments_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreatePayments());

    private void Nav_Contracts_Click(object sender, RoutedEventArgs e)
        => FocusSection(ContractsManagerSection);

    private void Nav_Calendar_Click(object sender, RoutedEventArgs e)
        => OpenCalendarWorkspace();

    private void Nav_SMSManager_Click(object sender, RoutedEventArgs e)
        => FocusSection(ContactsSection);

    private void Nav_EmailManager_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateEmailManager());

    private void Nav_Support_Click(object sender, RoutedEventArgs e)
        => FocusSection(SupportSection);

    private void AddPayment_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreatePayments());

    private void CreateContract_Click(object sender, RoutedEventArgs e)
        => FocusSection(ContractsManagerSection);

    private void NewContact_Click(object sender, RoutedEventArgs e)
        => FocusSection(ContactsSection);

    private void OpenSupportCenter_Click(object sender, RoutedEventArgs e)
        => FocusSection(SupportSection);

    private void SendSupportMessage_Click(object sender, RoutedEventArgs e)
    {
        var messageText = SupportComposerTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(messageText))
        {
            SetSupportStatus("Write a support message before sending it.", SupportErrorBrush);
            SupportComposerTextBox.Focus();
            return;
        }

        var userMessage = supportConversationRepository.CreateUserMessage(currentUser, messageText);
        var automatedReply = supportConversationRepository.CreateAutomatedReply(currentUser, messageText);

        supportMessages.Add(userMessage);
        supportMessages.Add(automatedReply);
        SaveSupportConversation();

        SupportComposerTextBox.Clear();
        UpdateSupportSummary();
        RefreshActivityGrid();
        ScrollSupportTranscriptToEnd();

        var statusBrush = automatedReply.IsUrgent ? SupportUrgentBrush : SupportSuccessBrush;
        SetSupportStatus($"Reply queued in {automatedReply.Channel} support.", statusBrush);
    }

    private void ClearSupportMessage_Click(object sender, RoutedEventArgs e)
    {
        SupportComposerTextBox.Clear();
        SetSupportStatus("Support composer cleared.", SupportNeutralBrush);
    }

    private void SupportQuickPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string prompt)
        {
            return;
        }

        SupportComposerTextBox.Text = prompt;
        SupportComposerTextBox.Focus();
        SupportComposerTextBox.CaretIndex = SupportComposerTextBox.Text.Length;
        SetSupportStatus("Quick prompt loaded into the support composer.", SupportNeutralBrush);
    }

    private void SaveContact_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = !string.IsNullOrWhiteSpace(editingContactId);
        if (!TryBuildContact(out var contact, out var errorMessage))
        {
            SetContactStatus(errorMessage, SupportErrorBrush);
            ContactNameBox.Focus();
            return;
        }

        UpsertContact(contact);
        PersistWorkspaceData();
        ResetContactEditor(false);
        SetContactStatus(
            isEditing
                ? $"{contact.FullName} updated and synced to the calendar."
                : $"{contact.FullName} saved and synced to the calendar.",
            SupportSuccessBrush);
    }

    private void ResetContact_Click(object sender, RoutedEventArgs e)
    {
        ContactsGrid.SelectedItem = null;
        ResetContactEditor();
    }

    private void ContactsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContactsGrid.SelectedItem is not ContactRecord selectedContact)
        {
            return;
        }

        editingContactId = selectedContact.Id;
        ContactNameBox.Text = selectedContact.FullName;
        ContactCompanyBox.Text = selectedContact.Company;
        ContactPhoneBox.Text = selectedContact.PhoneNumber;
        ContactEmailBox.Text = selectedContact.Email;
        ContactFollowUpPicker.SelectedDate = selectedContact.FollowUpDate;
        ContactNotesBox.Text = selectedContact.Notes;
        SetContactStatus($"Editing {selectedContact.FullName}. Save to update the local store.", SupportNeutralBrush);
    }

    private void SaveContract_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = !string.IsNullOrWhiteSpace(editingContractId);
        if (!TryBuildContract(out var contract, out var errorMessage))
        {
            SetContractStatus(errorMessage, SupportErrorBrush);
            ContractTitleBox.Focus();
            return;
        }

        UpsertContract(contract);
        PersistWorkspaceData();
        ResetContractEditor(false);
        SetContractStatus(
            isEditing
                ? $"{contract.Title} updated and synced to the calendar."
                : $"{contract.Title} saved and synced to the calendar.",
            SupportSuccessBrush);
    }

    private void ResetContract_Click(object sender, RoutedEventArgs e)
    {
        ContractsGrid.SelectedItem = null;
        ResetContractEditor();
    }

    private void ContractsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContractsGrid.SelectedItem is not ContractRecord selectedContract)
        {
            return;
        }

        editingContractId = selectedContract.Id;
        ContractTitleBox.Text = selectedContract.Title;
        ContractClientBox.Text = selectedContract.ClientName;
        SelectComboBoxValue(ContractTypeBox, selectedContract.ContractType);
        SelectComboBoxValue(ContractStateBox, selectedContract.Status);
        ContractStartPicker.SelectedDate = selectedContract.StartDate;
        ContractReminderPicker.SelectedDate = selectedContract.ReminderDate;
        ContractNotesBox.Text = selectedContract.Notes;
        SetContractStatus($"Editing {selectedContract.Title}. Save to update the local store.", SupportNeutralBrush);
    }

    private bool TryBuildContact(out ContactRecord contact, out string errorMessage)
    {
        var fullName = ContactNameBox.Text.Trim();
        var company = ContactCompanyBox.Text.Trim();
        var phoneNumber = ContactPhoneBox.Text.Trim();
        var email = ContactEmailBox.Text.Trim();
        var notes = ContactNotesBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errorMessage = "Add the contact's full name before saving.";
            contact = new ContactRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber) && string.IsNullOrWhiteSpace(email))
        {
            errorMessage = "Add at least an email address or phone number so the contact is usable.";
            contact = new ContactRecord();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber) && !HasEnoughDigits(phoneNumber))
        {
            errorMessage = "Enter a phone number with at least 7 digits.";
            contact = new ContactRecord();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
        {
            errorMessage = "Enter a valid email address for the contact.";
            contact = new ContactRecord();
            return false;
        }

        contact = new ContactRecord
        {
            Id = editingContactId ?? Guid.NewGuid().ToString("N"),
            OwnerUsername = currentUser.Username,
            FullName = fullName,
            Company = company,
            PhoneNumber = phoneNumber,
            Email = email,
            FollowUpDate = ContactFollowUpPicker.SelectedDate?.Date,
            Notes = notes,
            UpdatedUtc = DateTime.UtcNow
        };

        errorMessage = string.Empty;
        return true;
    }

    private bool TryBuildContract(out ContractRecord contract, out string errorMessage)
    {
        var title = ContractTitleBox.Text.Trim();
        var clientName = ContractClientBox.Text.Trim();
        var contractType = GetComboBoxValue(ContractTypeBox);
        var status = GetComboBoxValue(ContractStateBox);
        var notes = ContractNotesBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            errorMessage = "Add a contract title before saving.";
            contract = new ContractRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientName))
        {
            errorMessage = "Add the client name before saving.";
            contract = new ContractRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(contractType))
        {
            errorMessage = "Choose a contract type before saving.";
            contract = new ContractRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            errorMessage = "Choose a contract status before saving.";
            contract = new ContractRecord();
            return false;
        }

        if (!ContractStartPicker.SelectedDate.HasValue)
        {
            errorMessage = "Pick a start date for the contract.";
            contract = new ContractRecord();
            return false;
        }

        contract = new ContractRecord
        {
            Id = editingContractId ?? Guid.NewGuid().ToString("N"),
            OwnerUsername = currentUser.Username,
            Title = title,
            ClientName = clientName,
            ContractType = contractType,
            Status = status,
            StartDate = ContractStartPicker.SelectedDate!.Value.Date,
            ReminderDate = ContractReminderPicker.SelectedDate?.Date,
            Notes = notes,
            UpdatedUtc = DateTime.UtcNow
        };

        errorMessage = string.Empty;
        return true;
    }
    private void PersistWorkspaceData()
    {
        workspaceRepository.SaveForUser(currentUser.Username, contacts, contracts);
        LoadWorkspaceData();
    }

    private void UpsertContact(ContactRecord contact)
    {
        var existingIndex = contacts
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => string.Equals(entry.item.Id, contact.Id, StringComparison.OrdinalIgnoreCase))
            ?.index;

        if (existingIndex.HasValue)
        {
            contacts[existingIndex.Value] = contact;
            return;
        }

        contacts.Add(contact);
    }

    private void UpsertContract(ContractRecord contract)
    {
        var existingIndex = contracts
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => string.Equals(entry.item.Id, contract.Id, StringComparison.OrdinalIgnoreCase))
            ?.index;

        if (existingIndex.HasValue)
        {
            contracts[existingIndex.Value] = contract;
            return;
        }

        contracts.Add(contract);
    }

    private void ResetContactEditor(bool resetStatus = true)
    {
        editingContactId = null;
        ContactNameBox.Clear();
        ContactCompanyBox.Clear();
        ContactPhoneBox.Clear();
        ContactEmailBox.Clear();
        ContactFollowUpPicker.SelectedDate = null;
        ContactNotesBox.Clear();

        if (resetStatus)
        {
            SetContactStatus("Add a contact or select one below to edit it.", SupportNeutralBrush);
        }
    }

    private void ResetContractEditor(bool resetStatus = true)
    {
        editingContractId = null;
        ContractTitleBox.Clear();
        ContractClientBox.Clear();
        SelectComboBoxValue(ContractTypeBox, "Management");
        SelectComboBoxValue(ContractStateBox, "Draft");
        ContractStartPicker.SelectedDate = DateTime.Today;
        ContractReminderPicker.SelectedDate = null;
        ContractNotesBox.Clear();

        if (resetStatus)
        {
            SetContractStatus("Add a contract or select one below to edit it.", SupportNeutralBrush);
        }
    }

    private void SyncWorkspaceCalendarEvents()
    {
        var existingManagedEvents = calendarRepository.GetEvents()
            .Where(item => item.Id.StartsWith(ManagedCalendarEventPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        var desiredEvents = new List<CalendarEventRecord>();

        foreach (var contact in contacts.Where(item => item.FollowUpDate.HasValue))
        {
            var eventId = ManagedCalendarEventPrefix + "contact-" + contact.Id;
            existingManagedEvents.TryGetValue(eventId, out var existingEvent);
            desiredEvents.Add(CreateManagedEvent(
                eventId,
                existingEvent,
                $"{contact.FullName} follow-up",
                "Contact",
                BuildContactDescription(contact),
                string.IsNullOrWhiteSpace(contact.Company) ? "Contacts" : contact.Company,
                contact.FollowUpDate!.Value,
                9,
                0,
                30));
        }

        foreach (var contract in contracts)
        {
            var startEventId = ManagedCalendarEventPrefix + "contract-start-" + contract.Id;
            existingManagedEvents.TryGetValue(startEventId, out var existingStartEvent);
            desiredEvents.Add(CreateManagedEvent(
                startEventId,
                existingStartEvent,
                $"{contract.Title} start",
                "Contract",
                BuildContractDescription(contract),
                contract.ClientName,
                contract.StartDate,
                10,
                0,
                60));

            if (contract.ReminderDate.HasValue)
            {
                var reminderEventId = ManagedCalendarEventPrefix + "contract-reminder-" + contract.Id;
                existingManagedEvents.TryGetValue(reminderEventId, out var existingReminderEvent);
                desiredEvents.Add(CreateManagedEvent(
                    reminderEventId,
                    existingReminderEvent,
                    $"{contract.Title} reminder",
                    "Contract reminder",
                    BuildContractDescription(contract),
                    contract.ClientName,
                    contract.ReminderDate.Value,
                    9,
                    0,
                    30));
            }
        }

        if (desiredEvents.Count > 0)
        {
            calendarRepository.SaveMany(desiredEvents);
        }

        var desiredIds = desiredEvents
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var staleId in existingManagedEvents.Keys.Where(id => !desiredIds.Contains(id)).ToList())
        {
            calendarRepository.Delete(staleId);
        }
    }

    private static CalendarEventRecord CreateManagedEvent(
        string id,
        CalendarEventRecord? existing,
        string title,
        string category,
        string description,
        string location,
        DateTime date,
        int startHour,
        int startMinute,
        int durationMinutes)
    {
        var startLocal = date.Date.AddHours(startHour).AddMinutes(startMinute);
        var start = new DateTimeOffset(startLocal, TimeZoneInfo.Local.GetUtcOffset(startLocal));

        return new CalendarEventRecord
        {
            Id = id,
            ExternalUid = string.IsNullOrWhiteSpace(existing?.ExternalUid) ? id : existing.ExternalUid,
            Title = title,
            Category = category,
            Description = description,
            Location = location,
            Start = start,
            End = start.AddMinutes(durationMinutes),
            Source = string.IsNullOrWhiteSpace(existing?.Source) ? "Local" : existing.Source,
            GoogleEventId = existing?.GoogleEventId ?? string.Empty,
            AppleEventHref = existing?.AppleEventHref ?? string.Empty,
            LastModifiedUtc = DateTimeOffset.UtcNow
        };
    }

    private static string BuildContactDescription(ContactRecord contact)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(contact.Company))
        {
            parts.Add($"Company: {contact.Company}");
        }

        if (!string.IsNullOrWhiteSpace(contact.PhoneNumber))
        {
            parts.Add($"Phone: {contact.PhoneNumber}");
        }

        if (!string.IsNullOrWhiteSpace(contact.Email))
        {
            parts.Add($"Email: {contact.Email}");
        }

        if (!string.IsNullOrWhiteSpace(contact.Notes))
        {
            parts.Add($"Notes: {contact.Notes}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string BuildContractDescription(ContractRecord contract)
    {
        var parts = new List<string>
        {
            $"Client: {contract.ClientName}",
            $"Type: {contract.ContractType}",
            $"Status: {contract.Status}"
        };

        if (!string.IsNullOrWhiteSpace(contract.Notes))
        {
            parts.Add($"Notes: {contract.Notes}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static IReadOnlyList<PaymentRow> BuildPaymentRows()
    {
        var nextDue = GetNextPaymentDueDate(DateTime.Today);
        var lastPaid = nextDue.AddMonths(-1);

        return new[]
        {
            new PaymentRow(lastPaid.ToString("MMM dd", CultureInfo.CurrentCulture), "$49.00", "Card", "Paid"),
            new PaymentRow(lastPaid.AddMonths(-1).ToString("MMM dd", CultureInfo.CurrentCulture), "$49.00", "Card", "Paid"),
            new PaymentRow(lastPaid.AddMonths(-2).ToString("MMM dd", CultureInfo.CurrentCulture), "$49.00", "Card", "Paid")
        };
    }

    private static DateTime GetNextPaymentDueDate(DateTime anchor)
    {
        var dueDate = new DateTime(anchor.Year, anchor.Month, Math.Min(18, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
        if (anchor.Date > dueDate.Date)
        {
            var nextMonth = anchor.AddMonths(1);
            dueDate = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(18, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
        }

        return dueDate;
    }

    private int CountUpcomingFollowUps()
    {
        return contacts.Count(contact => IsWithinNextWeek(contact.FollowUpDate))
            + contracts.Count(contract => IsWithinNextWeek(contract.ReminderDate));
    }

    private static bool IsWithinNextWeek(DateTime? value)
        => value.HasValue && value.Value.Date >= DateTime.Today && value.Value.Date <= DateTime.Today.AddDays(7);

    private static bool IsActiveContract(string status)
        => string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Expiring", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasEnoughDigits(string phoneNumber)
        => phoneNumber.Count(char.IsDigit) >= 7;

    private static string GetComboBoxValue(ComboBox comboBox)
        => (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? comboBox.Text.Trim();

    private static void SelectComboBoxValue(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void SaveSupportConversation()
    {
        supportConversationRepository.SaveConversation(currentUser, supportMessages);
    }

    private void SetSupportStatus(string message, Brush brush)
    {
        SupportStatusText.Text = message;
        SupportStatusText.Foreground = brush;
    }

    private void SetContactStatus(string message, Brush brush)
    {
        ContactStatusText.Text = message;
        ContactStatusText.Foreground = brush;
    }

    private void SetContractStatus(string message, Brush brush)
    {
        ContractStatusText.Text = message;
        ContractStatusText.Foreground = brush;
    }

    private void ScrollSupportTranscriptToEnd()
    {
        Dispatcher.BeginInvoke(() => SupportTranscriptScrollViewer.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void FocusSection(FrameworkElement section)
    {
        section.BringIntoView();
        section.Focus();
        Activate();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        clockTimer.Stop();
        WindowManager.CloseAll();

        var loginWindow = new MainWindow(App.Credentials);
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        clockTimer.Stop();
        base.OnClosed(e);
    }

    private void OpenCalendarWorkspace()
    {
        WindowManager.ShowOrFocus(
            "calendar-sync",
            () => new CalendarSyncWindow(),
            this);
    }

    private void OpenModule(ModuleWindowState state)
    {
        WindowManager.ShowOrFocus(
            state.WindowKey,
            () => new ModuleWindow(state),
            this);
    }
}

public sealed record PaymentRow(string Date, string Amount, string Method, string Status);
public sealed record ActivityRow(string Workspace, string Action, string Owner, string Status);
