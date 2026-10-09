using System.Windows;
using System.Threading;

namespace BuffAssistant;

public partial class App : Application
{
    private Mutex? _instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, "Local\\BlackCardHelper", out var firstInstance);
        if (!firstInstance) { _instance.Dispose(); _instance = null; Shutdown(); return; }
        if (e.Args.Contains("--background") || new Services.AppSettingsService().Load().StartInBackground)
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
            return;
        }
        var splash = new SplashWindow();
        MainWindow = splash;
        splash.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instance is not null) { _instance.ReleaseMutex(); _instance.Dispose(); }
        base.OnExit(e);
    }
}
