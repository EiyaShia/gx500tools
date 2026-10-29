using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace GX500Tool.Output;

/// <summary>
/// Hides the Switch-mode GX500 from Windows via HidHideCLI. In Switch mode the pad streams IMU reports
/// nonstop, which makes Windows close the Start menu, tray flyouts and even UAC prompts, so hiding is
/// permanent and changes must be applied while the pad is NOT in Switch mode.
/// </summary>
public static class HidHide
{
    const string Cli = @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe";
    const int UacCancelled = 1223;

    public static bool IsInstalled => File.Exists(Cli);

    /// <summary>"\\?\hid#vid_057e&amp;pid_2009#7&amp;abc&amp;0&amp;0000#{guid}" → "HID\VID_057E&amp;PID_2009\7&amp;ABC&amp;0&amp;0000"</summary>
    public static string ToInstanceId(string devicePath)
    {
        var parts = devicePath.TrimStart('\\', '?').Split('#');
        return string.Join('\\', parts.Take(3)).ToUpperInvariant();
    }

    /// <summary>Allows <paramref name="appPath"/> to see hidden devices, hides the instances, and turns hiding on.</summary>
    public static void Apply(string appPath, IEnumerable<string> instanceIds) => Run(
        $"--app-reg \"{appPath}\" " + string.Join(' ', instanceIds.Select(id => $"--dev-hide \"{id}\"")) + " --cloak-on");

    public static void Remove(IEnumerable<string> instanceIds) => Run(
        string.Join(' ', instanceIds.Select(id => $"--dev-unhide \"{id}\"")));

    /// <summary>Runs HidHideCLI elevated (one UAC prompt). Throws <see cref="OperationCanceledException"/> if UAC was declined or closed.</summary>
    static void Run(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Cli, args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            p?.WaitForExit();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == UacCancelled)
        {
            throw new OperationCanceledException("Prompt UAC ditolak atau tertutup.", ex);
        }
    }
}
