using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GX500Tool.Input;

namespace GX500Tool.Mapping;

/// <summary>What the game sees. Nintendo is a removed preset kept only so old profiles still load; it is read as Xbox.</summary>
public enum GamepadType { Xbox, PlayStation, Nintendo }

public sealed class Profile
{
    public string Name { get; set; } = "Default";
    public GamepadType Type { get; set; } = GamepadType.Xbox;
    /// <summary>Game executables (e.g. "eldenring.exe") that auto-activate this profile.</summary>
    public List<string> Processes { get; set; } = new();

    /// <summary>Physical button → output button. Missing key = unchanged; PadButton.None = disabled.</summary>
    public Dictionary<PadButton, PadButton> Remap { get; set; } = new();
    public HashSet<PadButton> Turbo { get; set; } = new();
    public double TurboHz { get; set; } = 10;
    public double LeftDeadzone { get; set; } = 0.08;
    public double RightDeadzone { get; set; } = 0.08;
    public GyroSettings Gyro { get; set; } = new();
    public List<Macro> Macros { get; set; } = new();

    /// <summary>Physical button → key/mouse button name (see Output.Keyboard.Targets).</summary>
    public Dictionary<PadButton, string> KeyBinds { get; set; } = new();
    public bool LeftStickWasd { get; set; }
    public bool RightStickMouse { get; set; }
    public double StickMouseSpeed { get; set; } = 1200; // pixels/second at full tilt

    public PadButton Target(PadButton b) => Remap.TryGetValue(b, out var t) ? t : b;

    public void ResetRemap()
    {
        Remap.Clear();
        Turbo.Clear();
        TurboHz = 10;
        LeftDeadzone = RightDeadzone = 0.08;
    }

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Profile? Load(string path)
    {
        try
        {
            var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Json);
            if (p?.Type == GamepadType.Nintendo) p.Type = GamepadType.Xbox;
            return p;
        }
        catch { return null; }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }

    public Profile Clone(string newName)
    {
        var copy = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this, Json), Json)!;
        copy.Name = newName;
        copy.Processes.Clear();
        return copy;
    }
}
