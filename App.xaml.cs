using System;
using System.Windows;
using Label_CRM_demo.Services;

namespace Label_CRM_demo;

public partial class App : Application
{
    public static CredentialRepository Credentials { get; } = new CredentialRepository();
    public static WorkspaceRepository WorkspaceData { get; } = new WorkspaceRepository();
    public static CalendarRepository CalendarEvents { get; } = new CalendarRepository();
    public static CalendarSyncCredentialRepository CalendarSyncCredentials { get; } = new CalendarSyncCredentialRepository();
    public static GoogleCalendarSyncService GoogleCalendar { get; } = new GoogleCalendarSyncService();
    public static AppleCalendarSyncService AppleCalendar { get; } = new AppleCalendarSyncService();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Credentials.EnsureSeeded();
            WorkspaceData.EnsureInitialized();
            CalendarEvents.EnsureSeeded();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The local application stores could not be initialized.\n\n" + ex.Message,
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        var loginWindow = new MainWindow(Credentials);
        MainWindow = loginWindow;
        loginWindow.Show();
    }
}
