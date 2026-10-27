using System.Windows.Input;

namespace GX500Tool.Output;

/// <summary>Presses/releases keyboard keys and mouse buttons by name, tracking what is held.</summary>
public sealed class Keyboard
{
    public const string MouseLeft = "MouseLeft", MouseRight = "MouseRight", MouseMiddle = "MouseMiddle";

    /// <summary>Names offered in the UI: mouse buttons, then WPF <see cref="Key"/> names.</summary>
    public static IReadOnlyList<string> Targets { get; } = BuildTargets();

    static readonly HashSet<Key> Extended =
        [Key.Up, Key.Down, Key.Left, Key.Right, Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown, Key.RightCtrl, Key.RightAlt];

    readonly HashSet<string> _held = new();

    static List<string> BuildTargets()
    {
        var keys = new List<Key> { Key.Space, Key.Enter, Key.Escape, Key.Tab, Key.LeftShift, Key.LeftCtrl, Key.LeftAlt, Key.Back,
                                   Key.Up, Key.Down, Key.Left, Key.Right };
        for (var k = Key.A; k <= Key.Z; k++) keys.Add(k);
        for (var k = Key.D0; k <= Key.D9; k++) keys.Add(k);
        for (var k = Key.F1; k <= Key.F12; k++) keys.Add(k);
        keys.AddRange([Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown, Key.CapsLock]);
        return [MouseLeft, MouseRight, MouseMiddle, .. keys.Select(k => k.ToString())];
    }

    /// <summary>Makes exactly the given set held: presses new ones, releases ones no longer wanted.</summary>
    public void SetHeld(IReadOnlySet<string> wanted)
    {
        foreach (var t in _held.Where(t => !wanted.Contains(t)).ToList()) { Send(t, false); _held.Remove(t); }
        foreach (var t in wanted.Where(t => !_held.Contains(t)).ToList()) { Send(t, true); _held.Add(t); }
    }

    public void ReleaseAll() => SetHeld(new HashSet<string>());

    static void Send(string target, bool down)
    {
        switch (target)
        {
            case MouseLeft: NativeInput.Mouse(down ? NativeInput.LeftDown : NativeInput.LeftUp); break;
            case MouseRight: NativeInput.Mouse(down ? NativeInput.RightDown : NativeInput.RightUp); break;
            case MouseMiddle: NativeInput.Mouse(down ? NativeInput.MiddleDown : NativeInput.MiddleUp); break;
            default:
                if (Enum.TryParse<Key>(target, out var key))
                    NativeInput.Key((ushort)KeyInterop.VirtualKeyFromKey(key), down, Extended.Contains(key));
                break;
        }
    }
}
