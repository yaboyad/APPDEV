using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
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
    private readonly SupportSubmissionRepository supportSubmissionRepository;
    private readonly ObservableCollection<SupportSubmissionRecord> supportSubmissions = new ObservableCollection<SupportSubmissionRecord>();
    private readonly ObservableCollection<ContactRecord> contacts = new ObservableCollection<ContactRecord>();
    private readonly ObservableCollection<ContractRecord> contracts = new ObservableCollection<ContractRecord>();
    private readonly ObservableCollection<CalendarEventRecord> calendarItems = new ObservableCollection<CalendarEventRecord>();
    private readonly ObservableCollection<ActivityRow> activityRows = new ObservableCollection<ActivityRow>();
    private string? editingContactId;
    private string? editingContractId;
    private bool hasCompletedInitialDashboardLoad;
    private bool isRefreshingDashboard;

    public Window2()
        : this(new AuthenticatedUser("Admin", "Admin", AccountTier: AccountTiers.Master), App.WorkspaceData, App.CalendarEvents, new SupportSubmissionRepository())
    {
    }

    public Window2(AuthenticatedUser currentUser)
        : this(currentUser, App.WorkspaceData, App.CalendarEvents, new SupportSubmissionRepository())
    {
    }

    internal Window2(
        AuthenticatedUser currentUser,
        WorkspaceRepository workspaceRepository,
        CalendarRepository calendarRepository,
        SupportSubmissionRepository supportSubmissionRepository)
    {
        using var initTiming = PerformanceInstrumentation.Measure("dashboard.window-init", ("user", currentUser.Username), ("tier", currentUser.TierLabel));
        this.currentUser = currentUser;
        this.workspaceRepository = workspaceRepository;
        this.calendarRepository = calendarRepository;
        this.supportSubmissionRepository = supportSubmissionRepository;

        InitializeComponent();
        InitializeInteractiveStates();

        SupportSubmissionsGrid.ItemsSource = supportSubmissions;
        ContactsGrid.ItemsSource = contacts;
        ContractsGrid.ItemsSource = contracts;
        CalendarGrid.ItemsSource = calendarItems;
        ActivityGrid.ItemsSource = activityRows;

        SetSupportStatus("Support center ready.", SupportNeutralBrush);
        SetContactStatus("Add a contact or select one below to edit it.", SupportNeutralBrush);
        SetContractStatus("Add a contract or select one below to edit it.", SupportNeutralBrush);
        ContractTypeBox.SelectedIndex = 0;
        ContractStateBox.SelectedIndex = 0;
        ContractStartPicker.SelectedDate = DateTime.Today;
        initTiming.Checkpoint("controls-ready");
        calendarRepository.EventsChanged += CalendarRepository_EventsChanged;

        Activated += async (_, _) =>
        {
            var refreshMetric = hasCompletedInitialDashboardLoad ? "dashboard.reactivated-refresh" : "dashboard.initial-activation-refresh";
            await RefreshDashboardDataAsync(refreshMetric);
        };

        clockTimer.Interval = TimeSpan.FromSeconds(1);
        clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm:ss tt", CultureInfo.CurrentCulture);
    }

    private static SolidColorBrush CreateBrush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        using var loadTiming = PerformanceInstrumentation.Measure("dashboard.window-loaded", ("user", currentUser.Username), ("tier", currentUser.TierLabel));
        clockTimer.Start();
        loadTiming.Checkpoint("clock-started");
        ApplyUserState();
        loadTiming.Checkpoint("user-state-applied");
        await RefreshDashboardDataAsync("dashboard.window-loaded-refresh");
        loadTiming.Checkpoint("dashboard-loaded", ("contacts", contacts.Count), ("contracts", contracts.Count), ("calendarItems", calendarItems.Count));

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

        loadTiming.Checkpoint("animations-queued");
        PerformanceInstrumentation.Log("dashboard.ready", ("user", currentUser.Username), ("contacts", contacts.Count), ("contracts", contracts.Count));
    }

    private async Task RefreshDashboardDataAsync(string operationName)
    {
        if (isRefreshingDashboard)
        {
            return;
        }

        isRefreshingDashboard = true;

        try
        {
            using var refreshTiming = PerformanceInstrumentation.Measure(operationName, ("user", currentUser.Username));
            await LoadSupportDataAsync();
            refreshTiming.Checkpoint("support-ready", ("submissions", supportSubmissions.Count));
            await LoadDashboardDataAsync();
            refreshTiming.Checkpoint("workspace-ready", ("contacts", contacts.Count), ("contracts", contracts.Count), ("calendarItems", calendarItems.Count));
            hasCompletedInitialDashboardLoad = true;
        }
        finally
        {
            isRefreshingDashboard = false;
        }
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
        UserNameText.Text = $"{currentUser.DisplayName} | {currentUser.TierLabel}";
        WelcomeText.Text = currentUser.IsMaster
            ? $"Master workspace for {currentUser.DisplayName}"
            : $"Welcome back, {currentUser.DisplayName}";
        AccountStatusText.Text = currentUser.TierLabel;
        NextPaymentText.Text = "$49.00";
        NextPaymentDateText.Text = $"Due {GetNextPaymentDueDate(DateTime.Today):MMMM dd}";
        ConfigureSupportExperience();
    }

    private void ConfigureSupportExperience()
    {
        if (currentUser.IsMaster)
        {
            SupportIntroText.Text = "Master accounts can review every saved support submission across all accounts from one inbox.";
            UserSupportFormPanel.Visibility = Visibility.Collapsed;
            UserSupportSidebarPanel.Visibility = Visibility.Collapsed;
            MasterSupportInboxPanel.Visibility = Visibility.Visible;
            MasterSupportSidebarPanel.Visibility = Visibility.Visible;
            SetSupportStatus("Master inbox ready.", SupportNeutralBrush);
            return;
        }

        SupportIntroText.Text = "User accounts can submit support requests here. Only the master account can review saved submissions.";
        UserSupportFormPanel.Visibility = Visibility.Visible;
        UserSupportSidebarPanel.Visibility = Visibility.Visible;
        MasterSupportInboxPanel.Visibility = Visibility.Collapsed;
        MasterSupportSidebarPanel.Visibility = Visibility.Collapsed;
        SetSupportStatus("Support request form ready.", SupportNeutralBrush);
    }

    private async Task LoadDashboardDataAsync()
    {
        using var loadTiming = PerformanceInstrumentation.Measure("dashboard.load-data", ("user", currentUser.Username));
        PaymentsGrid.ItemsSource = BuildPaymentRows();
        loadTiming.Checkpoint("payments-bound");
        await LoadWorkspaceDataAsync();
        loadTiming.Checkpoint("workspace-bound", ("contacts", contacts.Count), ("contracts", contracts.Count), ("calendarItems", calendarItems.Count));
    }

    private async Task LoadWorkspaceDataAsync()
    {
        using var loadTiming = PerformanceInstrumentation.Measure("dashboard.load-workspace", ("user", currentUser.Username));
        var snapshot = await workspaceRepository.LoadForUserAsync(currentUser.Username);
        loadTiming.Checkpoint("snapshot-loaded", ("contacts", snapshot.Contacts.Count), ("contracts", snapshot.Contracts.Count));
        ReplaceCollection(contacts, snapshot.Contacts);
        ReplaceCollection(contracts, snapshot.Contracts);
        await SyncWorkspaceCalendarEventsAsync();
        loadTiming.Checkpoint("calendar-synced");
        await RefreshCalendarGridAsync();
        RefreshDashboardSummary();
        RefreshStorageSummary();
        await RefreshActivityGridAsync();
        loadTiming.Checkpoint("ui-refreshed", ("calendarItems", calendarItems.Count));
    }

    private async Task RefreshCalendarGridAsync()
    {
        ReplaceCollection(calendarItems, await calendarRepository.GetUpcomingEventsAsync(8));
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

    private async Task RefreshActivityGridAsync()
    {
        var latestContact = contacts.OrderByDescending(contact => contact.UpdatedUtc).FirstOrDefault();
        var latestContract = contracts.OrderByDescending(contract => contract.UpdatedUtc).FirstOrDefault();
        var latestSupportSubmission = supportSubmissions.FirstOrDefault();
        var nextCalendarEvent = calendarItems.FirstOrDefault();
        var syncedEvents = await calendarRepository.GetUpcomingEventsAsync(12);
        var calendarStatus = syncedEvents.Any(item =>
            !string.IsNullOrWhiteSpace(item.GoogleEventId) ||
            !string.IsNullOrWhiteSpace(item.AppleEventHref))
            ? "Synced"
            : "Local";
        var supportAction = latestSupportSubmission is null
            ? currentUser.IsMaster ? "Master inbox is clear" : "Support form is ready"
            : currentUser.IsMaster
                ? $"{latestSupportSubmission.Channel} request from {latestSupportSubmission.SubmittedByLabel}"
                : $"{latestSupportSubmission.Channel} request submitted";
        var supportOwner = latestSupportSubmission?.SubmittedByLabel ?? (currentUser.IsMaster ? "All accounts" : currentUser.DisplayName);
        var supportStatus = latestSupportSubmission is null ? "Ready" : latestSupportSubmission.PriorityLabel;

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
                supportAction,
                supportOwner,
                supportStatus)
        });
    }

    private async Task LoadSupportDataAsync()
    {
        using var loadTiming = PerformanceInstrumentation.Measure("dashboard.load-support", ("user", currentUser.Username), ("master", currentUser.IsMaster));
        var submissions = currentUser.IsMaster
            ? await supportSubmissionRepository.LoadAllAsync()
            : await supportSubmissionRepository.LoadForUserAsync(currentUser);

        ReplaceCollection(supportSubmissions, submissions);

        if (currentUser.IsMaster)
        {
            var selectedSubmission = SupportSubmissionsGrid.SelectedItem as SupportSubmissionRecord;
            if (selectedSubmission is not null)
            {
                var refreshedSelection = supportSubmissions.FirstOrDefault(item => string.Equals(item.Id, selectedSubmission.Id, StringComparison.OrdinalIgnoreCase));
                if (refreshedSelection is not null)
                {
                    SupportSubmissionsGrid.SelectedItem = refreshedSelection;
                    ApplySelectedSupportSubmission(refreshedSelection);
                }
                else
                {
                    SupportSubmissionsGrid.SelectedItem = supportSubmissions.FirstOrDefault();
                    ApplySelectedSupportSubmission(SupportSubmissionsGrid.SelectedItem as SupportSubmissionRecord);
                }
            }
            else
            {
                SupportSubmissionsGrid.SelectedItem = supportSubmissions.FirstOrDefault();
                ApplySelectedSupportSubmission(SupportSubmissionsGrid.SelectedItem as SupportSubmissionRecord);
            }
        }

        UpdateSupportSummary();
        await RefreshActivityGridAsync();
        loadTiming.Checkpoint("support-ready", ("submissions", supportSubmissions.Count));
    }

    private void UpdateSupportSummary()
    {
        if (!currentUser.IsMaster)
        {
            return;
        }

        var latestSubmission = supportSubmissions.FirstOrDefault();
        SupportCoverageText.Text = "All accounts";
        SupportThreadStateText.Text = supportSubmissions.Count == 0 ? "Inbox clear" : $"{supportSubmissions.Count} saved";
        SupportLastReplyText.Text = latestSubmission is null ? "No submissions" : latestSubmission.CreatedAt.ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture);
        SupportRoutingText.Text = latestSubmission?.Channel ?? "General";
        SupportMessageCountText.Text = $"{supportSubmissions.Count} saved submission(s)";
        SupportStoragePathText.Text = supportSubmissionRepository.StoragePath;

        if (SupportSubmissionsGrid.SelectedItem is not SupportSubmissionRecord selectedSubmission)
        {
            ApplySelectedSupportSubmission(latestSubmission);
        }
        else
        {
            ApplySelectedSupportSubmission(selectedSubmission);
        }
    }

    private void ApplySelectedSupportSubmission(SupportSubmissionRecord? submission)
    {
        if (submission is null)
        {
            SelectedSupportSubmissionMetaText.Text = "No support submissions yet.";
            SelectedSupportSubmissionBodyText.Text = "New requests from user accounts will appear here for review.";
            return;
        }

        SelectedSupportSubmissionMetaText.Text = $"{submission.SubmittedByLabel} | {submission.TierLabel} | {submission.Channel} | {submission.PriorityLabel} | {submission.CreatedAt:MMM dd, h:mm tt}";
        SelectedSupportSubmissionBodyText.Text = submission.Body;
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
        => OpenContractsWorkspace();

    private void Nav_Calendar_Click(object sender, RoutedEventArgs e)
        => OpenCalendarWorkspace();

    private void Nav_SMSManager_Click(object sender, RoutedEventArgs e)
        => OpenContactsWorkspace();

    private void Nav_EmailManager_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateEmailManager());

    private void Nav_Support_Click(object sender, RoutedEventArgs e)
        => OpenSupportWorkspace();

    private void AddPayment_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreatePayments());

    private void CreateContract_Click(object sender, RoutedEventArgs e)
        => OpenContractsWorkspace();

    private void NewContact_Click(object sender, RoutedEventArgs e)
        => OpenContactsWorkspace();

    private void OpenSupportCenter_Click(object sender, RoutedEventArgs e)
        => OpenSupportWorkspace();

    private async void SendSupportMessage_Click(object sender, RoutedEventArgs e)
    {
        using var saveTiming = PerformanceInstrumentation.Measure("support.save", ("user", currentUser.Username));
        if (currentUser.IsMaster)
        {
            saveTiming.Checkpoint("skipped-master");
            return;
        }

        var messageText = SupportComposerTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(messageText))
        {
            saveTiming.Checkpoint("validation-failed", ("reason", "empty-message"));
            SetSupportStatus("Write a support message before sending it.", SupportErrorBrush);
            SupportComposerTextBox.Focus();
            return;
        }

        var submission = await supportSubmissionRepository.SubmitAsync(currentUser, messageText);
        saveTiming.Checkpoint("submission-persisted", ("channel", submission.Channel), ("urgent", submission.IsUrgent));

        SupportComposerTextBox.Clear();
        await LoadSupportDataAsync();
        saveTiming.Checkpoint("dashboard-refreshed", ("submissions", supportSubmissions.Count));

        var statusBrush = submission.IsUrgent ? SupportUrgentBrush : SupportSuccessBrush;
        SetSupportStatus($"Support request submitted to the master inbox in {submission.Channel}.", statusBrush);
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

    private void SupportSubmissionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SupportSubmissionsGrid.SelectedItem is SupportSubmissionRecord selectedSubmission)
        {
            ApplySelectedSupportSubmission(selectedSubmission);
            return;
        }

        ApplySelectedSupportSubmission(supportSubmissions.FirstOrDefault());
    }

    private async void SaveContact_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = !string.IsNullOrWhiteSpace(editingContactId);
        using var saveTiming = PerformanceInstrumentation.Measure("workspace.save-contact", ("user", currentUser.Username), ("editing", isEditing));
        if (!TryBuildContact(out var contact, out var errorMessage))
        {
            saveTiming.Checkpoint("validation-failed", ("reason", errorMessage));
            SetContactStatus(errorMessage, SupportErrorBrush);
            ContactNameBox.Focus();
            return;
        }

        UpsertContact(contact);
        await PersistWorkspaceDataAsync();
        saveTiming.Checkpoint("workspace-persisted", ("contacts", contacts.Count), ("contracts", contracts.Count));
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

    private async void SaveContract_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = !string.IsNullOrWhiteSpace(editingContractId);
        using var saveTiming = PerformanceInstrumentation.Measure("workspace.save-contract", ("user", currentUser.Username), ("editing", isEditing));
        if (!TryBuildContract(out var contract, out var errorMessage))
        {
            saveTiming.Checkpoint("validation-failed", ("reason", errorMessage));
            SetContractStatus(errorMessage, SupportErrorBrush);
            ContractTitleBox.Focus();
            return;
        }

        UpsertContract(contract);
        await PersistWorkspaceDataAsync();
        saveTiming.Checkpoint("workspace-persisted", ("contacts", contacts.Count), ("contracts", contracts.Count));
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
    private async Task PersistWorkspaceDataAsync()
    {
        using var persistTiming = PerformanceInstrumentation.Measure("workspace.persist", ("user", currentUser.Username), ("contacts", contacts.Count), ("contracts", contracts.Count));
        await workspaceRepository.SaveForUserAsync(currentUser.Username, contacts.ToList(), contracts.ToList());
        persistTiming.Checkpoint("store-saved");
        await LoadWorkspaceDataAsync();
        persistTiming.Checkpoint("workspace-reloaded", ("calendarItems", calendarItems.Count));
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

    private async Task SyncWorkspaceCalendarEventsAsync()
    {
        using var syncTiming = PerformanceInstrumentation.Measure("workspace.calendar-sync", ("user", currentUser.Username));
        var existingManagedEvents = (await calendarRepository.GetEventsAsync())
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

        var desiredIds = desiredEvents
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleIds = existingManagedEvents.Keys.Where(id => !desiredIds.Contains(id)).ToList();
        syncTiming.Checkpoint("events-shaped", ("existing", existingManagedEvents.Count), ("desired", desiredEvents.Count), ("stale", staleIds.Count));

        if (desiredEvents.Count > 0)
        {
            await calendarRepository.SaveManyAsync(desiredEvents);
            syncTiming.Checkpoint("events-saved", ("saved", desiredEvents.Count));
        }

        if (staleIds.Count > 0)
        {
            await calendarRepository.DeleteManyAsync(staleIds);
        }

        syncTiming.Checkpoint("stale-events-removed", ("removed", staleIds.Count));
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

    private Task RefreshCalendarViewsAsync()
        => RefreshCalendarViewsCoreAsync();

    private async Task RefreshCalendarViewsCoreAsync()
    {
        await RefreshCalendarGridAsync();
        await RefreshActivityGridAsync();
    }

    private void CalendarRepository_EventsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => _ = RefreshCalendarViewsAsync());
            return;
        }

        _ = RefreshCalendarViewsAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        calendarRepository.EventsChanged -= CalendarRepository_EventsChanged;
        clockTimer.Stop();
        base.OnClosed(e);
    }

    private void OpenContractsWorkspace()
    {
        OpenModule(ModuleWindowState.CreateContracts(contracts));
    }

    private void OpenContactsWorkspace()
    {
        OpenModule(ModuleWindowState.CreateContacts(contacts));
    }

    private void OpenSupportWorkspace()
    {
        OpenModule(ModuleWindowState.CreateSupport(currentUser, supportSubmissions));
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

























