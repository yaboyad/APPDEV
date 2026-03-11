using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Label_CRM_demo.Models;

namespace Label_CRM_demo;

public partial class Window2 : Window
{
    private readonly DispatcherTimer clockTimer = new DispatcherTimer();
    private readonly AuthenticatedUser currentUser;

    public Window2()
        : this(new AuthenticatedUser("Admin", "Admin"))
    {
    }

    public Window2(AuthenticatedUser currentUser)
    {
        this.currentUser = currentUser;
        InitializeComponent();
        InitializeInteractiveStates();

        clockTimer.Interval = TimeSpan.FromSeconds(1);
        clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm:ss tt");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        clockTimer.Start();
        ApplyUserState();
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
            StoreCard
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
            OpenPaymentsButton,
            OpenContractsButton,
            OpenSmsButton,
            LogoutButton
        }, -4, 1.01);
    }

    private void ApplyUserState()
    {
        UserNameText.Text = currentUser.DisplayName;
        WelcomeText.Text = $"Welcome back, {currentUser.DisplayName}";
        StorageText.Text = App.Credentials.StoragePath;
        AccountStatusText.Text = "Protected";
        NextPaymentText.Text = "$49.00";
        NextPaymentDateText.Text = "Due March 18";
        RevenueText.Text = "$3,480";
        TasksText.Text = "3 active";
    }

    private void LoadDashboardData()
    {
        PaymentsGrid.ItemsSource = new List<PaymentRow>
        {
            new PaymentRow("Feb 18", "$49.00", "Card", "Paid"),
            new PaymentRow("Jan 18", "$49.00", "Card", "Paid"),
            new PaymentRow("Dec 18", "$49.00", "Card", "Paid")
        };

        CalendarGrid.ItemsSource = new List<CalendarRow>
        {
            new CalendarRow("Mar 12", "Drop Prep Check-in", "Task"),
            new CalendarRow("Mar 14", "Artist Outreach", "Call"),
            new CalendarRow("Mar 18", "Payment Due", "Billing")
        };

        ActivityGrid.ItemsSource = new List<ActivityRow>
        {
            new ActivityRow("Payments", "Recurring invoice collected", currentUser.DisplayName, "Done"),
            new ActivityRow("Contracts", "Single release draft updated", currentUser.DisplayName, "In Progress"),
            new ActivityRow("Calendar", "Artist outreach call scheduled", currentUser.DisplayName, "Ready")
        };
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
        => OpenModule(ModuleWindowState.CreateContracts());

    private void Nav_Calendar_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateCalendar());

    private void Nav_SMSManager_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateSmsManager());

    private void Nav_EmailManager_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateEmailManager());

    private void AddPayment_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreatePayments());

    private void CreateContract_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateContracts());

    private void NewContact_Click(object sender, RoutedEventArgs e)
        => OpenModule(ModuleWindowState.CreateSmsManager());

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

    private void OpenModule(ModuleWindowState state)
    {
        WindowManager.ShowOrFocus(
            state.WindowKey,
            () => new ModuleWindow(state),
            this);
    }
}

public sealed record PaymentRow(string Date, string Amount, string Method, string Status);
public sealed record CalendarRow(string Date, string Title, string Type);
public sealed record ActivityRow(string Workspace, string Action, string Owner, string Status);
