using System.Diagnostics;
using System.IO;

namespace GX500Tool.Output;

/// <summary>Hides the physical controller from games via HidHideCLI, so they only see the virtual pad.</summary>
public static class HidHide
{
    const string Cli = @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe";

    public static bool IsInstalled => File.Exists(Cli);

    /// <summary>"\\?\hid#vid_057e&amp;pid_2009#7&amp;abc&amp;0&amp;0000#{guid}" → "HID\VID_057E&amp;PID_2009\7&amp;ABC&amp;0&amp;0000"</summary>
    public static string ToInstanceId(string devicePath)
    {
        var parts = devicePath.TrimStart('\\', '?').Split('#');
        return string.Join('\\', parts.Take(3)).ToUpperInvariant();
    }

    public static void Hide(string devicePath) => Run(
        $"--app-reg \"{Environment.ProcessPath}\" --dev-hide \"{ToInstanceId(devicePath)}\" --cloak-on");

    public static void Unhide(string devicePath) => Run(
        $"--dev-unhide \"{ToInstanceId(devicePath)}\" --cloak-off");

    static void Run(string args)
    {
        // HidHideCLI needs admin rights; Windows will show a UAC prompt.
        using var p = Process.Start(new ProcessStartInfo(Cli, args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
        p?.WaitForExit();
    }
}
