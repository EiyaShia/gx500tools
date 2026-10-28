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

    readonly byte[] _report = new byte[63]; // DS4_REPORT_EX: the extended report carries motion data
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    static readonly (PadButton, ushort)[] RawButtons =
    [
        (PadButton.X, 0x0010), (PadButton.A, 0x0020), (PadButton.B, 0x0040), (PadButton.Y, 0x0080),
        (PadButton.LB, 0x0100), (PadButton.RB, 0x0200), (PadButton.Back, 0x1000), (PadButton.Start, 0x2000),
        (PadButton.LS, 0x4000), (PadButton.RS, 0x8000),
    ];

    public void Submit(in PadState s)
    {
        var r = _report;
        Array.Clear(r);

        // DS4 axes are 0..255 with 128 at center and Y growing downward.
        static byte Axis(double v) => (byte)Math.Clamp(Math.Round((v + 1) * 127.5), 0, 255);
        r[0] = Axis(s.LX); r[1] = Axis(-s.LY); r[2] = Axis(s.RX); r[3] = Axis(-s.RY);

        ushort buttons = DPad(s);
        foreach (var (p, bit) in RawButtons) if (s.IsPressed(p)) buttons |= bit;
        // DS4 also reports L2/R2 as digital buttons alongside the analog value.
        if (s.LT > 0.1) buttons |= 0x0400;
        if (s.RT > 0.1) buttons |= 0x0800;
        Write16(r, 4, buttons);
        r[6] = (byte)((s.IsPressed(PadButton.Guide) ? 0x01 : 0) | (s.IsPressed(PadButton.Capture) ? 0x02 : 0));
        r[7] = (byte)(s.LT * 255);
        r[8] = (byte)(s.RT * 255);

        // Timestamp in 5.33 µs units; games integrate gyro over it, so it must follow real time.
        Write16(r, 9, (ushort)(_clock.Elapsed.Ticks * 3 / 160));
        r[11] = 0xFF; // battery

        if (s.Imu is { } m)
        {
            var (gx, gy, gz, ax, ay, az) = ToDs4Axes(m);
            Write16(r, 12, (ushort)gx); Write16(r, 14, (ushort)gy); Write16(r, 16, (ushort)gz);
            Write16(r, 18, (ushort)ax); Write16(r, 20, (ushort)ay); Write16(r, 22, (ushort)az);
        }

        r[34] = 0x80; r[38] = 0x80; // touchpad: both fingers up
        _pad.SubmitRawReport(r);
    }

    /// <summary>
    /// Switch Pro IMU → DS4 IMU. Gyro scale is the same on both (~16.4 LSB per deg/s); DS4 accel is
    /// 8192/g vs ~4096/g on Switch. Axes: DS4 X = pitch, Y = yaw, Z = roll (Switch: pitch = -Y, yaw = Z, roll = -X).
    /// </summary>
    static (short, short, short, short, short, short) ToDs4Axes(ImuSample m)
    {
        static short Clamp(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);
        return (Clamp(-m.GyroY), Clamp(m.GyroZ), Clamp(-m.GyroX),
                Clamp(-m.AccY * 2), Clamp(m.AccZ * 2), Clamp(-m.AccX * 2));
    }

    static void Write16(byte[] b, int offset, ushort v) { b[offset] = (byte)v; b[offset + 1] = (byte)(v >> 8); }

    /// <summary>Low nibble of the buttons word: 0 = N, clockwise to 7 = NW, 8 = released.</summary>
    static ushort DPad(in PadState s)
    {
        bool u = s.IsPressed(PadButton.Up), d = s.IsPressed(PadButton.Down);
        bool l = s.IsPressed(PadButton.Left), r = s.IsPressed(PadButton.Right);
        return (u, d, l, r) switch
        {
            (true, false, false, true) => 1,
            (false, true, false, true) => 3,
            (false, true, true, false) => 5,
            (true, false, true, false) => 7,
            (true, false, _, _) => 0,
            (_, _, false, true) => 2,
            (false, true, _, _) => 4,
            (_, _, true, false) => 6,
            _ => 8,
        };
    }

    public void Dispose()
    {
        try { _pad.Disconnect(); } catch { }
    }
}
