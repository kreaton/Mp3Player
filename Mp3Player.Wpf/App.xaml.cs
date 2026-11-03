using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Mp3Player.Wpf;

internal partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // MusicPlayer raises its events through the SynchronizationContext current when it is created.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));

        // Optional: a folder passed on the command line is opened and played right away.
        string? folder = e.Args.FirstOrDefault(Directory.Exists);
        new MainWindow(folder).Show();
    }
}
