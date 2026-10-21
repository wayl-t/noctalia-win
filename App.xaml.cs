using System.Windows;
using NoctaliaWin.Interop;
using NoctaliaWin.Services;
using Application = System.Windows.Application;

namespace NoctaliaWin;

public partial class App : Application
{
    private Mutex _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "NoctaliaWin_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        ConfigService.Instance.Load();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Гарантированный откат рабочей области: если окно закрыли не через Closing,
        // резерв остался бы висеть и следующий запуск отрезал бы полосу ещё раз.
        (MainWindow as MainWindow)?.ReleaseWorkArea();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
