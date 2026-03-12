$ErrorActionPreference = "Stop"

function Replace-Exact {
    param(
        [string]$Path,
        [string]$Old,
        [string]$New
    )

    $content = Get-Content -Path $Path -Raw
    if (-not $content.Contains($Old)) {
        throw "Snippet not found in $Path"
    }

    $content = $content.Replace($Old, $New)
    Set-Content -Path $Path -Value $content -Encoding UTF8
}

Replace-Exact 'App.xaml.cs' @"
    public static CredentialRepository Credentials { get; } = new CredentialRepository();
    public static WorkspaceRepository WorkspaceData { get; } = new WorkspaceRepository();
    public static CalendarRepository CalendarEvents { get; } = new CalendarRepository();
"@ @"
    public static CredentialRepository Credentials { get; } = new CredentialRepository();
    public static WorkspaceRepository WorkspaceData { get; } = new WorkspaceRepository();
    public static BillingRepository BillingData { get; } = new BillingRepository();
    public static CalendarRepository CalendarEvents { get; } = new CalendarRepository();
"@

Replace-Exact 'App.xaml.cs' @"
            await Task.WhenAll(
                Credentials.EnsureSeededAsync(),
                WorkspaceData.EnsureInitializedAsync(),
                CalendarEvents.EnsureSeededAsync());
            storesTiming.Checkpoint("credentials-ready");
            storesTiming.Checkpoint("workspace-ready");
            storesTiming.Checkpoint("calendar-ready");
"@ @"
            await Task.WhenAll(
                Credentials.EnsureSeededAsync(),
                WorkspaceData.EnsureInitializedAsync(),
                BillingData.EnsureInitializedAsync(),
                CalendarEvents.EnsureSeededAsync());
            storesTiming.Checkpoint("credentials-ready");
            storesTiming.Checkpoint("workspace-ready");
            storesTiming.Checkpoint("billing-ready");
            storesTiming.Checkpoint("calendar-ready");
"@

Replace-Exact 'window2.xaml.cs' @"
    private readonly CredentialRepository credentialRepository;
    private readonly WorkspaceRepository workspaceRepository;
    private readonly CalendarRepository calendarRepository;
    private readonly SupportSubmissionRepository supportSubmissionRepository;
"@ @"
    private readonly CredentialRepository credentialRepository;
    private readonly WorkspaceRepository workspaceRepository;
    private readonly CalendarRepository calendarRepository;
    private readonly BillingRepository billingRepository;
    private readonly SupportSubmissionRepository supportSubmissionRepository;
"@

Replace-Exact 'window2.xaml.cs' @"
    private readonly ObservableCollection<SocialPlatformRow> dataWatchRows = new ObservableCollection<SocialPlatformRow>();
    private readonly ObservableCollection<ActivityRow> activityRows = new ObservableCollection<ActivityRow>();
    private string? selectedManagedAccountUsername;
"@ @"
    private readonly ObservableCollection<SocialPlatformRow> dataWatchRows = new ObservableCollection<SocialPlatformRow>();
    private readonly ObservableCollection<ActivityRow> activityRows = new ObservableCollection<ActivityRow>();
    private BillingSnapshot billingSnapshot = BillingSnapshot.Empty;
    private string? selectedManagedAccountUsername;
"@

Replace-Exact 'window2.xaml.cs' @"
    public Window2()
        : this(new AuthenticatedUser("Admin", "Admin", AccountTier: AccountTiers.Master), App.Credentials, App.WorkspaceData, App.CalendarEvents, new SupportSubmissionRepository())
    {
    }

    public Window2(AuthenticatedUser currentUser)
        : this(currentUser, App.Credentials, App.WorkspaceData, App.CalendarEvents, new SupportSubmissionRepository())
    {
    }

    internal Window2(
        AuthenticatedUser currentUser,
        CredentialRepository credentialRepository,
        WorkspaceRepository workspaceRepository,
        CalendarRepository calendarRepository,
        SupportSubmissionRepository supportSubmissionRepository)
"@ @"
    public Window2()
        : this(new AuthenticatedUser("Admin", "Admin", AccountTier: AccountTiers.Master), App.Credentials, App.WorkspaceData, App.CalendarEvents, App.BillingData, new SupportSubmissionRepository())
    {
    }

    public Window2(AuthenticatedUser currentUser)
        : this(currentUser, App.Credentials, App.WorkspaceData, App.CalendarEvents, App.BillingData, new SupportSubmissionRepository())
    {
    }

    internal Window2(
        AuthenticatedUser currentUser,
        CredentialRepository credentialRepository,
        WorkspaceRepository workspaceRepository,
        CalendarRepository calendarRepository,
        BillingRepository billingRepository,
        SupportSubmissionRepository supportSubmissionRepository)
"@

Replace-Exact 'window2.xaml.cs' @"
        this.workspaceRepository = workspaceRepository;
        this.calendarRepository = calendarRepository;
        this.supportSubmissionRepository = supportSubmissionRepository;
"@ @"
        this.workspaceRepository = workspaceRepository;
        this.calendarRepository = calendarRepository;
        this.billingRepository = billingRepository;
        this.supportSubmissionRepository = supportSubmissionRepository;
"@

Replace-Exact 'window2.xaml.cs' @"
        AccountStatusText.Text = currentUser.TierLabel;
        NextPaymentText.Text = "$49.00";
        NextPaymentDateText.Text = $"Due {GetNextPaymentDueDate(DateTime.Today):MMMM dd}";
        ConfigureAccountManagementExperience();
"@ @"
        AccountStatusText.Text = currentUser.TierLabel;
        ApplyBillingLoadingState();
        ConfigureAccountManagementExperience();
"@

Replace-Exact 'window2.xaml.cs' @"
    private void ConfigureAccountManagementExperience()
"@ @"
    private void ApplyBillingLoadingState()
    {
        NextPaymentText.Text = "Loading...";
        NextPaymentDateText.Text = "Reading persisted billing data.";
    }

    private void ApplyBillingSummary(BillingSnapshot snapshot)
    {
        var nextCharge = BillingPresentation.GetNextCharge(snapshot, DateTime.Today);
        if (nextCharge is null)
        {
            NextPaymentText.Text = snapshot.HasData ? "Not scheduled" : "No billing data";
            NextPaymentDateText.Text = snapshot.HasData
                ? "No open invoice is scheduled right now."
                : "Connect a provider or save billing records.";
            return;
        }

        NextPaymentText.Text = nextCharge.Amount.HasValue
            ? BillingPresentation.FormatAmount(nextCharge.Amount.Value, nextCharge.CurrencyCode)
            : "Scheduled";
        NextPaymentDateText.Text = $"Due {nextCharge.DueDate:MMMM dd}";
    }

    private void ConfigureAccountManagementExperience()
"@

Replace-Exact 'window2.xaml.cs' @"
    private async Task LoadDashboardDataAsync()
    {
        using var loadTiming = PerformanceInstrumentation.Measure("dashboard.load-data", ("user", currentUser.Username));
        PaymentsGrid.ItemsSource = BuildPaymentRows();
        ReplaceCollection(dataWatchRows, ModuleWindowState.CreateDataWatchRows());
        loadTiming.Checkpoint("payments-and-data-bound", ("dataSources", dataWatchRows.Count));
        await LoadWorkspaceDataAsync();
        loadTiming.Checkpoint("workspace-bound", ("contacts", contacts.Count), ("contracts", contracts.Count), ("calendarItems", calendarItems.Count));
    }
"@ @"
    private async Task LoadDashboardDataAsync()
    {
        using var loadTiming = PerformanceInstrumentation.Measure("dashboard.load-data", ("user", currentUser.Username));
        billingSnapshot = await billingRepository.LoadForUserAsync(currentUser.Username);
        PaymentsGrid.ItemsSource = BuildPaymentRows(billingSnapshot);
        ApplyBillingSummary(billingSnapshot);
        ReplaceCollection(dataWatchRows, ModuleWindowState.CreateDataWatchRows());
        loadTiming.Checkpoint("payments-and-data-bound", ("payments", billingSnapshot.Invoices.Count), ("dataSources", dataWatchRows.Count));
        await LoadWorkspaceDataAsync();
        loadTiming.Checkpoint("workspace-bound", ("contacts", contacts.Count), ("contracts", contracts.Count), ("calendarItems", calendarItems.Count));
    }
"@

Replace-Exact 'window2.xaml.cs' @"
    private void RefreshDashboardSummary()
    {
        RevenueText.Text = contracts.Count(contract => IsActiveContract(contract.Status)).ToString(CultureInfo.InvariantCulture);
        TasksText.Text = CountUpcomingFollowUps().ToString(CultureInfo.InvariantCulture);
        NextPaymentText.Text = "$49.00";
        NextPaymentDateText.Text = $"Due {GetNextPaymentDueDate(DateTime.Today):MMMM dd}";
        RefreshOutreachSummary();
    }
"@ @"
    private void RefreshDashboardSummary()
    {
        RevenueText.Text = contracts.Count(contract => IsActiveContract(contract.Status)).ToString(CultureInfo.InvariantCulture);
        TasksText.Text = CountUpcomingFollowUps().ToString(CultureInfo.InvariantCulture);
        ApplyBillingSummary(billingSnapshot);
        RefreshOutreachSummary();
    }
"@

Replace-Exact 'window2.xaml.cs' @"
    private void RefreshStorageSummary()
    {
        StorageText.Text = $"{contacts.Count} contacts / {contracts.Count} contracts / {GetOutreachCampaignCount()} outreach campaign(s){Environment.NewLine}CRM: {workspaceRepository.StoragePath}{Environment.NewLine}Calendar: {calendarRepository.StoragePath}";
    }
"@ @"
    private void RefreshStorageSummary()
    {
        StorageText.Text = $"{contacts.Count} contacts / {contracts.Count} contracts / {GetOutreachCampaignCount()} outreach campaign(s){Environment.NewLine}CRM: {workspaceRepository.StoragePath}{Environment.NewLine}Billing: {billingRepository.StoragePath}{Environment.NewLine}Calendar: {calendarRepository.StoragePath}";
    }
"@

Replace-Exact 'window2.xaml.cs' @"
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
"@ @"
    private static IReadOnlyList<PaymentRow> BuildPaymentRows(BillingSnapshot snapshot)
    {
        return BillingPresentation.GetRecentInvoices(snapshot, 8)
            .Select(invoice => new PaymentRow(
                BillingPresentation.GetInvoiceDisplayDate(invoice).ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                BillingPresentation.FormatAmount(invoice.Amount, invoice.CurrencyCode),
                BillingPresentation.GetInvoiceMethodLabel(invoice, snapshot.Profile),
                BillingPresentation.GetInvoiceStatusLabel(invoice)))
            .ToArray();
    }
"@

Replace-Exact 'window2.xaml.cs' @"
    private void OpenPaymentsWorkspace()
    {
        OpenModule(ModuleWindowState.CreatePayments(DateTime.Today));
    }
"@ @"
    private void OpenPaymentsWorkspace()
    {
        OpenModule(ModuleWindowState.CreatePayments(billingSnapshot, DateTime.Today));
    }
"@

Replace-Exact 'Models\ModuleWindowState.cs' @"
using System.Linq;
"@ @"
using System.Linq;
using Label_CRM_demo.Services;
"@

Replace-Exact 'Models\ModuleWindowState.cs' @"
    public static ModuleWindowState CreatePayments() => CreatePayments(DateTime.Today);

    public static ModuleWindowState CreatePayments(DateTime today)
    {
        var nextChargeDate = GetNextPaymentDueDate(today);
        var recentChargeDates = Enumerable.Range(1, 3)
            .Select(monthOffset => nextChargeDate.AddMonths(-monthOffset))
            .ToList();

        return new ModuleWindowState
        {
            WindowKey = "payments",
            Title = "Payments Workspace",
            Subtitle = $"Recurring billing summary through {nextChargeDate:MMMM yyyy}",
            Highlight = "This workspace now opens with the same billing cadence shown on the dashboard so payment review stays in sync with the rest of the CRM.",
            Footer = "Use this focused window to review recent charges while the dashboard stays anchored on contacts, campaigns, data watch, and support.",
            Column1Header = "Date",
            Column2Header = "Amount",
            Column3Header = "Method",
            Column4Header = "Status",
            Metrics = new[]
            {
                new ModuleMetric("Current Plan", "Creator Pro", "Monthly cycle"),
                new ModuleMetric("Next Charge", "$49.00", $"Due {nextChargeDate:MMM dd, yyyy}"),
                new ModuleMetric("Recent Charges", recentChargeDates.Count.ToString(CultureInfo.InvariantCulture), $"Latest {recentChargeDates.FirstOrDefault():MMM dd}")
            },
            Rows = recentChargeDates
                .Select(chargeDate => new ModuleRow(
                    chargeDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                    "$49.00",
                    "Card",
                    "Paid"))
                .ToArray()
        };
    }
"@ @"
    public static ModuleWindowState CreatePayments() => CreatePayments(BillingSnapshot.Empty, DateTime.Today);

    public static ModuleWindowState CreatePayments(DateTime today) => CreatePayments(BillingSnapshot.Empty, today);

    public static ModuleWindowState CreatePayments(BillingSnapshot? snapshot) => CreatePayments(snapshot, DateTime.Today);

    public static ModuleWindowState CreatePayments(BillingSnapshot? snapshot, DateTime today)
    {
        snapshot ??= BillingSnapshot.Empty;

        var recentInvoices = BillingPresentation.GetRecentInvoices(snapshot, 8);
        var nextCharge = BillingPresentation.GetNextCharge(snapshot, today);
        var latestInvoice = recentInvoices.FirstOrDefault();
        var nextChargeValue = nextCharge is null
            ? "Not scheduled"
            : nextCharge.Amount.HasValue
                ? BillingPresentation.FormatAmount(nextCharge.Amount.Value, nextCharge.CurrencyCode)
                : "Scheduled";

        return new ModuleWindowState
        {
            WindowKey = "payments",
            Title = "Payments Workspace",
            Subtitle = !snapshot.HasData
                ? "No persisted billing data saved yet"
                : nextCharge is null
                    ? $"{recentInvoices.Count} invoice(s) saved in the local billing store"
                    : $"Billing summary through {nextCharge.DueDate:MMMM yyyy}",
            Highlight = !snapshot.HasData
                ? "This workspace now reads from persisted billing data instead of generating placeholder invoices."
                : nextCharge is null
                    ? "Recent invoices now come straight from the saved billing store shared with the dashboard."
                    : $"The next scheduled charge is {nextChargeValue.ToLowerInvariant()} on {nextCharge.DueDate:MMM dd, yyyy}.",
            Footer = !snapshot.HasData
                ? "Connect a payment provider or save local billing records to populate this workspace."
                : "This focused window mirrors the same saved billing data shown on the dashboard.",
            Column1Header = "Date",
            Column2Header = "Amount",
            Column3Header = "Method",
            Column4Header = "Status",
            Metrics = new[]
            {
                new ModuleMetric("Current Plan", BillingPresentation.GetPlanLabel(snapshot), BillingPresentation.GetPlanDetail(snapshot)),
                new ModuleMetric("Next Charge", nextChargeValue, nextCharge is null ? (snapshot.HasData ? "No open invoice scheduled" : "No billing records yet") : $"Due {nextCharge.DueDate:MMM dd, yyyy}"),
                new ModuleMetric("Recent Charges", recentInvoices.Count.ToString(CultureInfo.InvariantCulture), latestInvoice is null ? "No invoices saved" : $"Latest {BillingPresentation.GetInvoiceDisplayDate(latestInvoice):MMM dd}")
            },
            Rows = recentInvoices
                .Select(invoice => new ModuleRow(
                    BillingPresentation.GetInvoiceDisplayDate(invoice).ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                    BillingPresentation.FormatAmount(invoice.Amount, invoice.CurrencyCode),
                    BillingPresentation.GetInvoiceMethodLabel(invoice, snapshot.Profile),
                    BillingPresentation.GetInvoiceStatusLabel(invoice)))
                .ToArray()
        };
    }
"@

Replace-Exact 'Models\ModuleWindowState.cs' @"
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
"@ ""
