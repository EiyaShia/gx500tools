using GX500Tool.Input;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace GX500Tool.Output;

public enum PadKind { Xbox360, DualShock4 }

/// <summary>A ViGEmBus virtual controller fed with the mapped (Xbox-layout) state.</summary>
public interface IVirtualPad : IDisposable
{
    PadKind Kind { get; }
    event Action<double, double>? Rumble;
    void Submit(in PadState s);
}

public static class VirtualPad
{
    static readonly Lazy<ViGEmClient> Client = new(() => new ViGEmClient());

    public static IVirtualPad Create(PadKind kind) => kind == PadKind.DualShock4
        ? new DualShock4Pad(Client.Value)
        : new Xbox360Pad(Client.Value);
}

sealed class Xbox360Pad : IVirtualPad
{
    readonly IXbox360Controller _pad;

    public PadKind Kind => PadKind.Xbox360;
    public event Action<double, double>? Rumble;

    static readonly (PadButton, Xbox360Button)[] Buttons =
    [
        (PadButton.A, Xbox360Button.A), (PadButton.B, Xbox360Button.B),
        (PadButton.X, Xbox360Button.X), (PadButton.Y, Xbox360Button.Y),
        (PadButton.LB, Xbox360Button.LeftShoulder), (PadButton.RB, Xbox360Button.RightShoulder),
        (PadButton.Back, Xbox360Button.Back), (PadButton.Start, Xbox360Button.Start),
        (PadButton.LS, Xbox360Button.LeftThumb), (PadButton.RS, Xbox360Button.RightThumb),
        (PadButton.Guide, Xbox360Button.Guide),
        (PadButton.Up, Xbox360Button.Up), (PadButton.Down, Xbox360Button.Down),
        (PadButton.Left, Xbox360Button.Left), (PadButton.Right, Xbox360Button.Right),
    ];

    public Xbox360Pad(ViGEmClient client)
    {
        _pad = client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
        _pad.FeedbackReceived += (_, e) => Rumble?.Invoke(e.LargeMotor / 255.0, e.SmallMotor / 255.0);
        _pad.Connect();
    }

    public void Submit(in PadState s)
    {
        foreach (var (p, x) in Buttons) _pad.SetButtonState(x, s.IsPressed(p));

        static short Axis(double v) => (short)Math.Clamp(v * 32767, -32768, 32767);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, Axis(s.LX));
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, Axis(s.LY));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, Axis(s.RX));
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, Axis(s.RY));
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(s.LT * 255));
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(s.RT * 255));
        _pad.SubmitReport();
    }

    public void Dispose()
    {
        try { _pad.Disconnect(); } catch { }
    }
}

/// <summary>PlayStation 4 controller. Positional: A = Cross, B = Circle, X = Square, Y = Triangle; Capture = touchpad click.</summary>
sealed class DualShock4Pad : IVirtualPad
{
    readonly IDualShock4Controller _pad;

    public PadKind Kind => PadKind.DualShock4;
    public event Action<double, double>? Rumble;

    static readonly (PadButton, DualShock4Button)[] Buttons =
    [
        (PadButton.A, DualShock4Button.Cross), (PadButton.B, DualShock4Button.Circle),
        (PadButton.X, DualShock4Button.Square), (PadButton.Y, DualShock4Button.Triangle),
        (PadButton.LB, DualShock4Button.ShoulderLeft), (PadButton.RB, DualShock4Button.ShoulderRight),
        (PadButton.Back, DualShock4Button.Share), (PadButton.Start, DualShock4Button.Options),
        (PadButton.LS, DualShock4Button.ThumbLeft), (PadButton.RS, DualShock4Button.ThumbRight),
    ];

    public DualShock4Pad(ViGEmClient client)
    {
        _pad = client.CreateDualShock4Controller();
        _pad.AutoSubmitReport = false;
        // Deprecated in favor of AwaitRawOutputReport(), but still delivers rumble values, which is all we need.
#pragma warning disable CS0618
        _pad.FeedbackReceived += (_, e) => Rumble?.Invoke(e.LargeMotor / 255.0, e.SmallMotor / 255.0);
#pragma warning restore CS0618
        _pad.Connect();
    }

    public void Submit(in PadState s)
    {
        foreach (var (p, d) in Buttons) _pad.SetButtonState(d, s.IsPressed(p));
        _pad.SetButtonState(DualShock4SpecialButton.Ps, s.IsPressed(PadButton.Guide));
        _pad.SetButtonState(DualShock4SpecialButton.Touchpad, s.IsPressed(PadButton.Capture));
        // DS4 also reports L2/R2 as digital buttons alongside the analog value.
        _pad.SetButtonState(DualShock4Button.TriggerLeft, s.LT > 0.1);
        _pad.SetButtonState(DualShock4Button.TriggerRight, s.RT > 0.1);
        _pad.SetDPadDirection(DPad(s));

        // DS4 axes are 0..255 with 128 at center and Y growing downward.
        static byte Axis(double v) => (byte)Math.Clamp(Math.Round((v + 1) * 127.5), 0, 255);
        _pad.SetAxisValue(DualShock4Axis.LeftThumbX, Axis(s.LX));
        _pad.SetAxisValue(DualShock4Axis.LeftThumbY, Axis(-s.LY));
        _pad.SetAxisValue(DualShock4Axis.RightThumbX, Axis(s.RX));
        _pad.SetAxisValue(DualShock4Axis.RightThumbY, Axis(-s.RY));
        _pad.SetSliderValue(DualShock4Slider.LeftTrigger, (byte)(s.LT * 255));
        _pad.SetSliderValue(DualShock4Slider.RightTrigger, (byte)(s.RT * 255));
        _pad.SubmitReport();
    }

    static DualShock4DPadDirection DPad(in PadState s)
    {
        bool u = s.IsPressed(PadButton.Up), d = s.IsPressed(PadButton.Down);
        bool l = s.IsPressed(PadButton.Left), r = s.IsPressed(PadButton.Right);
        return (u, d, l, r) switch
        {
            (true, false, true, false) => DualShock4DPadDirection.Northwest,
            (true, false, false, true) => DualShock4DPadDirection.Northeast,
            (false, true, true, false) => DualShock4DPadDirection.Southwest,
            (false, true, false, true) => DualShock4DPadDirection.Southeast,
            (true, false, _, _) => DualShock4DPadDirection.North,
            (false, true, _, _) => DualShock4DPadDirection.South,
            (_, _, true, false) => DualShock4DPadDirection.West,
            (_, _, false, true) => DualShock4DPadDirection.East,
            _ => DualShock4DPadDirection.None,
        };
    }

    public void Dispose()
    {
        try { _pad.Disconnect(); } catch { }
    }
}
