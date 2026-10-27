using System.IO;
using System.Text.Json;
using GX500Tool.Mapping;

namespace GX500Tool;

public sealed class AppSettings
{
    public string ActiveProfile { get; set; } = "Default";
    public bool AutoSwitch { get; set; } = true;
    public bool AutoStartEngine { get; set; } = true;
}

/// <summary>State shared by all tabs and the tray: profiles, the active one, and the running engine.</summary>
public static class Session
{
    static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GX500Tool");
    static readonly string SettingsPath = Path.Combine(Root, "settings.json");
    public static string ProfilesDir { get; } = Path.Combine(Root, "profiles");

    public static AppSettings Settings { get; }
    public static List<Profile> Profiles { get; } = new();
    public static Profile Profile { get; private set; }
    public static Engine? Engine { get; private set; }

    /// <summary>Raised on the UI thread when the engine starts/stops, the active profile changes or the profile list changes.</summary>
    public static event Action? Changed;

    static Session()
    {
        try { Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch { Settings = new(); }

        if (Directory.Exists(ProfilesDir))
            foreach (var f in Directory.GetFiles(ProfilesDir, "*.json"))
                if (Profile.Load(f) is { } p) Profiles.Add(p);

        if (Profiles.Count == 0)
        {
            // Migrate the single profile.json from earlier versions.
            Profiles.Add(Profile.Load(Path.Combine(Root, "profile.json")) ?? new Profile());
            Profiles[0].Name = "Default";
            SaveProfile(Profiles[0]);
        }
        Profile = Profiles.FirstOrDefault(p => p.Name == Settings.ActiveProfile) ?? Profiles[0];
    }

    static string PathFor(Profile p)
    {
        var safe = string.Concat(p.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(ProfilesDir, safe + ".json");
    }

    static void SaveProfile(Profile p) => p.Save(PathFor(p));

    static void SaveSettings()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Persists the active profile after an edit and pushes it to the engine.</summary>
    public static void Save()
    {
        if (Engine != null) Engine.Profile = Profile;
        SaveProfile(Profile);
    }

    public static void SaveAll()
    {
        foreach (var p in Profiles) SaveProfile(p);
        if (Engine != null) Engine.Profile = Profile;
    }

    public static void SetSetting(Action<AppSettings> change)
    {
        change(Settings);
        SaveSettings();
    }

    public static void Activate(Profile p)
    {
        if (ReferenceEquals(p, Profile)) return;
        Profile = p;
        Settings.ActiveProfile = p.Name;
        SaveSettings();
        if (Engine != null) Engine.Profile = p;
        Changed?.Invoke();
    }

    public static Profile Create(string name, Profile? copyFrom)
    {
        name = UniqueName(name);
        var p = copyFrom?.Clone(name) ?? new Profile { Name = name };
        Profiles.Add(p);
        SaveProfile(p);
        Activate(p);
        return p;
    }

    public static void Rename(Profile p, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name == p.Name) return;
        File.Delete(PathFor(p));
        p.Name = UniqueName(name);
        SaveProfile(p);
        if (ReferenceEquals(p, Profile)) SetSetting(s => s.ActiveProfile = p.Name);
        Changed?.Invoke();
    }

    public static void Delete(Profile p)
    {
        if (Profiles.Count <= 1) return;
        File.Delete(PathFor(p));
        Profiles.Remove(p);
        if (ReferenceEquals(p, Profile)) { Profile = null!; Activate(Profiles[0]); }
        else Changed?.Invoke();
    }

    static string UniqueName(string name)
    {
        var candidate = name;
        for (int i = 2; Profiles.Any(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)); i++) candidate = $"{name} {i}";
        return candidate;
    }

    /// <summary>Starts the engine. Returns false if no GX500 was found; throws if ViGEmBus fails.</summary>
    public static bool Start()
    {
        Engine = Engine.Start(Profile);
        Changed?.Invoke();
        return Engine != null;
    }

    public static void Stop()
    {
        if (Engine == null) return;
        Engine.Dispose();
        Engine = null;
        Changed?.Invoke();
    }

    public static void NotifyChanged() => Changed?.Invoke();
}
