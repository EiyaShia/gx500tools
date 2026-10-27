using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace GX500Tool;

/// <summary>Watches the foreground window and activates the profile linked to that game's executable.</summary>
public sealed class AutoSwitcher
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly int _ownPid = Environment.ProcessId;
    string? _lastExe;

    /// <summary>Most recent foreground executable that isn't this app, e.g. "game.exe".</summary>
    public string? LastForeground { get; private set; }

    /// <summary>Raised when a profile was switched automatically (profile name).</summary>
    public event Action<string>? Switched;

    public AutoSwitcher()
    {
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    void Tick()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        if (pid == 0 || pid == _ownPid) return;

        string exe;
        try { exe = Path.GetFileName(Process.GetProcessById((int)pid).MainModule?.FileName ?? "") ; }
        catch { return; } // elevated or exited process
        if (exe.Length == 0 || exe == _lastExe) return;
        _lastExe = LastForeground = exe;
        if (!Session.Settings.AutoSwitch) return;

        var match = Session.Profiles.FirstOrDefault(p => p.Processes.Any(x => x.Equals(exe, StringComparison.OrdinalIgnoreCase)));
        // Leaving a game falls back to the first profile without games ("Default").
        var target = match ?? (Session.Profile.Processes.Count > 0 ? Session.Profiles.FirstOrDefault(p => p.Processes.Count == 0) : null);
        if (target != null && !ReferenceEquals(target, Session.Profile))
        {
            Session.Activate(target);
            Switched?.Invoke(target.Name);
        }
    }
}
