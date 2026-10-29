using System.ComponentModel;
using System.Windows;

namespace GX500Tool;

public partial class App : Application
{
    TrayIcon? _tray;
    MainWindow? _window;
    bool _exiting;

    public static AutoSwitcher? Switcher { get; private set; }

    const string InstanceName = "GX500Tool.SingleInstance", ShowSignalName = "GX500Tool.ShowWindow";
    static Mutex? _instance;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // One instance only: two would both read the GX500 and add a second tray icon.
        // A second launch asks the running one to show its window, then exits.
        _instance = new Mutex(true, InstanceName, out bool isFirst);
        if (!isFirst)
        {
            AllowSetForegroundWindow(-1); // let the running instance bring itself to the front
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var signal)) signal.Set();
            Shutdown();
            return;
        }
        var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, false);

        _window = new MainWindow();
        _window.Closing += Window_Closing;
        _window.StateChanged += (_, _) => { if (_window.WindowState == WindowState.Minimized) _window.Hide(); };

        _tray = new TrayIcon(ShowWindow, Quit);
        Switcher = new AutoSwitcher();
        Switcher.Switched += name => _tray.Notify($"Profil: {name}");

        Session.BatteryChanged += (old, now) =>
        {
            if (now is { IsLow: true } && old is not { IsLow: true })
                _tray.Notify($"Baterai GX500 rendah ({now.Percent}%). Segera isi ulang.");
        };
        var batteryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        batteryTimer.Tick += (_, _) => Session.PollBattery();
        batteryTimer.Start();

        if (Session.Settings.AutoStartEngine)
        {
            try { Session.Start(); } catch { /* reported when the user starts it manually */ }
        }

        if (Session.HideProblem is { } hideProblem)
            _tray.Notify("HidHide perlu diterapkan: " + hideProblem + " Buka halaman Remap & Turbo.");

        if (!e.Args.Contains("--tray")) ShowWindow();
    }

    void ShowWindow()
    {
        _window!.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        // Activate() alone may only flash the taskbar when triggered from another process.
        _window.Topmost = true;
        _window.Topmost = false;
    }

    /// <summary>Closing the window keeps the app running in the tray; use "Keluar" in the tray menu to quit.</summary>
    void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        _window!.Hide();
        _tray!.Notify("Masih berjalan di tray. Klik kanan ikon untuk keluar.");
    }

    void Quit()
    {
        _exiting = true;
        Session.Stop();
        _tray?.Dispose();
        _window?.Close();
        Shutdown();
    }
}
