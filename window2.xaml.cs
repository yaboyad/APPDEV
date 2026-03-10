using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace Label_CRM_demo
{
    public partial class Window2 : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();

        // Tab indexes (match the order in XAML)
        private const int TAB_OVERVIEW = 0;
        private const int TAB_ACCOUNT = 1;
        private const int TAB_PAYMENTS = 2;
        private const int TAB_CONTRACTS = 3;
        private const int TAB_CALENDAR = 4;
        private const int TAB_SMS = 5;
        private const int TAB_EMAIL = 6;

        public Window2()
        {
            InitializeComponent();

            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("h:mm:ss tt");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _clockTimer.Start();
            LoadDemoData();
        }

        private void LoadDemoData()
        {
            // Overview grids
            PaymentsGrid.ItemsSource = new List<dynamic>
            {
                new { Date = "Feb 18", Amount = "$49.00", Method = "Card", Status = "Paid" },
                new { Date = "Jan 18", Amount = "$49.00", Method = "Card", Status = "Paid" },
                new { Date = "Dec 18", Amount = "$49.00", Method = "Card", Status = "Paid" },
            };

            CalendarGrid.ItemsSource = new List<dynamic>
            {
                new { Date = "Feb 22", Title = "Drop Prep Check-in", Type = "Task" },
                new { Date = "Feb 24", Title = "Artist Outreach", Type = "Call" },
                new { Date = "Mar 01", Title = "Payment Due", Type = "Billing" },
            };

            // Payments tab history
            PaymentHistoryGrid.ItemsSource = new List<dynamic>
            {
                new { Invoice = "INV-1001", Date = "Feb 18", Amount = "$49.00", Status = "Paid" },
                new { Invoice = "INV-1000", Date = "Jan 18", Amount = "$49.00", Status = "Paid" },
                new { Invoice = "INV-0999", Date = "Dec 18", Amount = "$49.00", Status = "Paid" },
            };

            // Contracts tab
            ContractsGrid.ItemsSource = new List<dynamic>
            {
                new { Client = "Demo Artist", Type = "Mgmt", Start = "Feb 01", Status = "Active" },
                new { Client = "Demo Producer", Type = "Split", Start = "Jan 10", Status = "Active" },
            };
        }

        private void SelectTab(int index)
        {
            if (MainTabs == null) return;
            if (index < 0 || index >= MainTabs.Items.Count) return;
            MainTabs.SelectedIndex = index;
        }

        // Nav clicks
        private void Nav_Overview_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_OVERVIEW);
        private void Nav_Account_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_ACCOUNT);
        private void Nav_Payments_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_PAYMENTS);
        private void Nav_Contracts_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_CONTRACTS);
        private void Nav_Calendar_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_CALENDAR);
        private void Nav_SMSManager_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_SMS);
        private void Nav_EmailManager_Click(object sender, RoutedEventArgs e) => SelectTab(TAB_EMAIL);

        // Quick actions (stubs for now)
        private void AddPayment_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Add Payment (hook this to a form later).", "Titan");

        private void CreateContract_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Create Contract (hook this to a contract builder later).", "Titan");

        private void NewContact_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("New Contact (hook this to CRM later).", "Titan");

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _clockTimer.Stop();

            // If you have MainWindow as login, show it:
            var login = new MainWindow();
            login.Show();

            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _clockTimer.Stop();
            base.OnClosed(e);
        }
    }
}