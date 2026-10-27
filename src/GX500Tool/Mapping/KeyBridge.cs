using GX500Tool.Input;
using GX500Tool.Output;

namespace GX500Tool.Mapping;

/// <summary>Gamepad → keyboard/mouse. Bound buttons and converted sticks are removed from the virtual pad output.</summary>
public sealed class KeyBridge
{
    const double StickDeadzone = 0.15, WasdThreshold = 0.5;

    readonly Keyboard _keyboard = new();

    public PadButton Update(PadButton physical, ref PadState input, Profile p, Mouse mouse, double dtSeconds)
    {
        PadButton consumed = 0;
        var wanted = new HashSet<string>();

        foreach (var (button, target) in p.KeyBinds)
        {
            consumed |= button;
            if ((physical & button) != 0) wanted.Add(target);
        }

        if (p.LeftStickWasd)
        {
            if (input.LY > WasdThreshold) wanted.Add("W");
            if (input.LY < -WasdThreshold) wanted.Add("S");
            if (input.LX < -WasdThreshold) wanted.Add("A");
            if (input.LX > WasdThreshold) wanted.Add("D");
            input.LX = input.LY = 0;
        }

        if (p.RightStickMouse)
        {
            double mag = Math.Sqrt(input.RX * input.RX + input.RY * input.RY);
            if (mag > StickDeadzone)
            {
                // Quadratic curve: fine control near center, fast at full tilt.
                double scaled = Math.Pow((mag - StickDeadzone) / (1 - StickDeadzone), 2) * p.StickMouseSpeed * dtSeconds;
                mouse.Move(input.RX / mag * scaled, -input.RY / mag * scaled);
            }
            input.RX = input.RY = 0;
        }

        _keyboard.SetHeld(wanted);
        return consumed;
    }

    public void ReleaseAll() => _keyboard.ReleaseAll();
}
