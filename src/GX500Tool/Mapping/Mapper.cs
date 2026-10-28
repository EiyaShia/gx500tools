using GX500Tool.Input;

namespace GX500Tool.Mapping;

/// <summary>Applies deadzone, remap and turbo to a physical state, producing the virtual pad state.</summary>
public sealed class Mapper
{
    readonly Dictionary<PadButton, long> _pressedSince = new();

    public PadState Apply(in PadState input, Profile p, long nowMs)
    {
        var o = new PadState { Imu = input.Imu };
        (o.LX, o.LY) = Deadzone(input.LX, input.LY, p.LeftDeadzone);
        (o.RX, o.RY) = Deadzone(input.RX, input.RY, p.RightDeadzone);

        var physical = Physical(input);

        long halfPeriod = (long)(500 / Math.Max(p.TurboHz, 1));
        foreach (var b in PadState.AllButtons)
        {
            if ((physical & b) == 0) { _pressedSince.Remove(b); continue; }
            if (!_pressedSince.TryGetValue(b, out var since)) _pressedSince[b] = since = nowMs;

            var target = p.Target(b);
            if (target == PadButton.None) continue;

            bool turbo = p.Turbo.Contains(b);
            if (turbo && (nowMs - since) / halfPeriod % 2 == 1) continue; // "off" half of the turbo cycle

            o.Buttons |= target;
            // Keep analog trigger travel only when the trigger maps to itself without turbo.
            bool analog = target == b && !turbo;
            if (target == PadButton.LT) o.LT = Math.Max(o.LT, analog ? input.LT : 1);
            if (target == PadButton.RT) o.RT = Math.Max(o.RT, analog ? input.RT : 1);
        }
        return o;
    }

    /// <summary>Swaps A↔B and X↔Y (Nintendo label layout).</summary>
    public static PadButton SwapFaceButtons(PadButton b)
    {
        const PadButton face = PadButton.A | PadButton.B | PadButton.X | PadButton.Y;
        var r = b & ~face;
        if ((b & PadButton.A) != 0) r |= PadButton.B;
        if ((b & PadButton.B) != 0) r |= PadButton.A;
        if ((b & PadButton.X) != 0) r |= PadButton.Y;
        if ((b & PadButton.Y) != 0) r |= PadButton.X;
        return r;
    }

    /// <summary>Pressed buttons including analog triggers past halfway.</summary>
    public static PadButton Physical(in PadState s)
    {
        var b = s.Buttons;
        if (s.LT > 0.5) b |= PadButton.LT;
        if (s.RT > 0.5) b |= PadButton.RT;
        return b;
    }

    /// <summary>Radial deadzone with rescaling so output still reaches full range.</summary>
    static (double, double) Deadzone(double x, double y, double dz)
    {
        double mag = Math.Sqrt(x * x + y * y);
        if (mag <= dz) return (0, 0);
        double scaled = Math.Min((mag - dz) / (1 - dz), 1);
        return (x / mag * scaled, y / mag * scaled);
    }
}
