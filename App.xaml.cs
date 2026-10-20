using System.Threading;
using System.Windows;

namespace DropSorter;

public partial class App : Application
{
    Mutex? _mutex;
    System.Windows.Forms.NotifyIcon? _tray;
    MainWindow? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "DropSorter.SingleInstance", out bool created);
        if (!created) { Shutdown(); return; }

        base.OnStartup(e);
        SorterService.Load();

        _main = new MainWindow();
        _main.Show();

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "DropSorter",
            Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show / hide widget", null, (_, _) => Toggle());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Toggle();
    }

    void Toggle()
    {
        if (_main == null) return;
        if (_main.IsVisible) _main.Hide();
        else { _main.Show(); _main.Activate(); }
    }

    public static void OpenSettings() => new SettingsWindow().ShowDialog();

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
