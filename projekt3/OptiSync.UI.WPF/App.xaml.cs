using System.Windows;

namespace OptiSync.UI.WPF;

public partial class App : Application
{
    public const string SingleInstanceMutexName = @"Global\OptiSyncNP_SingleGuiInstance";

    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(false, SingleInstanceMutexName);
        try
        {
            _ownsMutex = _singleInstanceMutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        if (!_ownsMutex)
        {
            MessageBox.Show(
                "Koordynator OptiSync.NP jest już uruchomiony (zajęty Mutex " + SingleInstanceMutexName + ").\nDruga instancja zostanie zamknięta.",
                "OptiSync.NP – druga instancja",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
