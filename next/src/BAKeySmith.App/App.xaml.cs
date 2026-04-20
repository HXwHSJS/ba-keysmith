using System.Windows;
using System.Windows.Threading;

namespace BAKeySmith.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;

        if (e.Args.Any(arg => string.Equals(arg, "--smoke", StringComparison.OrdinalIgnoreCase)))
        {
            window.Loaded += (_, _) =>
            {
                _ = window.Dispatcher.BeginInvoke(
                    async () =>
                    {
                        await Task.Delay(200);
                        window.Close();
                    },
                    DispatcherPriority.ApplicationIdle);
            };
        }

        window.Show();
    }
}
