using System;
using System.Threading.Tasks;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        using var startupTiming = PerformanceInstrumentation.Measure("startup.app");
        base.OnStartup(e);
        startupTiming.Checkpoint("wpf-ready");

        try
        {
            using var storesTiming = PerformanceInstrumentation.Measure("startup.initialize-stores");
            await Task.WhenAll(
                Credentials.EnsureSeededAsync(),
                WorkspaceData.EnsureInitializedAsync(),
                CalendarEvents.EnsureSeededAsync());
            storesTiming.Checkpoint("credentials-ready");
            storesTiming.Checkpoint("workspace-ready");
            storesTiming.Checkpoint("calendar-ready");
        }
        catch (Exception ex)
        {
            PerformanceInstrumentation.Log("startup.initialization-failed", ("errorType", ex.GetType().Name));
            MessageBox.Show(
                "The local application stores could not be initialized.\n\n" + ex.Message,
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        var loginWindow = new MainWindow(Credentials);
        startupTiming.Checkpoint("login-window-constructed");
        MainWindow = loginWindow;

        var loginWindowVisibleLogged = false;
        loginWindow.ContentRendered += (_, _) =>
        {
            if (loginWindowVisibleLogged)
            {
                return;
            }

            loginWindowVisibleLogged = true;
            PerformanceInstrumentation.Log("startup.login-window-visible");
        };

        loginWindow.Show();
        startupTiming.Checkpoint("login-window-show-called");
    }
}
