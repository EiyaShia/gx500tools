using GX500Tool.Input;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace GX500Tool.Output;

/// <summary>Virtual Xbox 360 controller via ViGEmBus.</summary>
public sealed class VirtualPad : IDisposable
{
    readonly ViGEmClient _client = new();
    readonly IXbox360Controller _pad;

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

    public VirtualPad()
    {
        _pad = _client.CreateXbox360Controller();
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
        _client.Dispose();
    }
}
