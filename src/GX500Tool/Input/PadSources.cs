namespace GX500Tool.Input;

public interface IPadSource : IDisposable
{
    string Name { get; }
    bool TryRead(out PadState state);
    void SetRumble(double large, double small) { }
}

public sealed class XInputSource : IPadSource
{
    readonly int _slot;

    XInputSource(int slot) => _slot = slot;

    public string Name => $"XInput slot {_slot + 1}";

    /// <summary>Must be called before the virtual pad connects, so we lock onto the physical slot.</summary>
    public static XInputSource? FindFirst()
    {
        for (int i = 0; i < 4; i++)
            if (XInput.TryGetState(i, out _)) return new XInputSource(i);
        return null;
    }

    public bool TryRead(out PadState state)
    {
        state = default;
        if (!XInput.TryGetState(_slot, out var s)) return false;
        var g = s.Gamepad;
        var buttons = PadButton.None;

        void Map(XButtons x, PadButton p) { if (g.Buttons.HasFlag(x)) buttons |= p; }
        Map(XButtons.A, PadButton.A); Map(XButtons.B, PadButton.B);
        Map(XButtons.X, PadButton.X); Map(XButtons.Y, PadButton.Y);
        Map(XButtons.LeftShoulder, PadButton.LB); Map(XButtons.RightShoulder, PadButton.RB);
        Map(XButtons.Back, PadButton.Back); Map(XButtons.Start, PadButton.Start);
        Map(XButtons.LeftThumb, PadButton.LS); Map(XButtons.RightThumb, PadButton.RS);
        Map(XButtons.Guide, PadButton.Guide);
        Map(XButtons.DPadUp, PadButton.Up); Map(XButtons.DPadDown, PadButton.Down);
        Map(XButtons.DPadLeft, PadButton.Left); Map(XButtons.DPadRight, PadButton.Right);
        state.Buttons = buttons;

        state.LX = g.ThumbLX / 32768.0; state.LY = g.ThumbLY / 32768.0;
        state.RX = g.ThumbRX / 32768.0; state.RY = g.ThumbRY / 32768.0;
        state.LT = g.LeftTrigger / 255.0; state.RT = g.RightTrigger / 255.0;
        return true;
    }

    public void SetRumble(double large, double small) => XInput.SetVibration(_slot, large, small);

    public void Dispose() => XInput.SetVibration(_slot, 0, 0);
}

/// <summary>GX500 in Switch mode (Pro Controller protocol, report 0x30). Provides gyro.</summary>
public sealed class SwitchSource : IPadSource
{
    const double StickCenter = 2048, StickRange = 1600;

    readonly HidMonitor _monitor;
    readonly object _lock = new();
    PadState _latest;
    DateTime _lastReport = DateTime.MinValue;

    public string Name => "Switch mode (Pro Controller)";
    public string DevicePath { get; }

    SwitchSource(HidSharp.HidDevice device)
    {
        DevicePath = device.DevicePath;
        _monitor = new HidMonitor(device);
        _monitor.ReportReceived += OnReport;
        _monitor.Start();
    }

    public static SwitchSource? TryOpen()
    {
        var dev = HidMonitor.FindControllers().FirstOrDefault(d => d.VendorID == 0x057E);
        return dev == null ? null : new SwitchSource(dev);
    }

    void OnReport(byte[] r)
    {
        if (r[0] != 0x30 || r.Length < 25) return;
        var s = new PadState();

        byte right = r[3], shared = r[4], left = r[5];
        void Map(byte reg, int bit, PadButton p) { if ((reg & bit) != 0) s.Buttons |= p; }
        // The GX500 reports face buttons by their printed label in Switch mode (A bit = button labeled A),
        // so map label-to-label. Bits: Y 0x01, X 0x02, B 0x04, A 0x08.
        Map(right, 0x08, PadButton.A); Map(right, 0x04, PadButton.B);
        Map(right, 0x02, PadButton.X); Map(right, 0x01, PadButton.Y);
        Map(right, 0x40, PadButton.RB); Map(right, 0x80, PadButton.RT);
        Map(shared, 0x01, PadButton.Back); Map(shared, 0x02, PadButton.Start);
        Map(shared, 0x04, PadButton.RS); Map(shared, 0x08, PadButton.LS);
        Map(shared, 0x10, PadButton.Guide); Map(shared, 0x20, PadButton.Capture);
        Map(left, 0x01, PadButton.Down); Map(left, 0x02, PadButton.Up);
        Map(left, 0x04, PadButton.Right); Map(left, 0x08, PadButton.Left);
        Map(left, 0x40, PadButton.LB); Map(left, 0x80, PadButton.LT);

        // Switch triggers are digital.
        s.LT = s.IsPressed(PadButton.LT) ? 1 : 0;
        s.RT = s.IsPressed(PadButton.RT) ? 1 : 0;

        static double Axis(int raw) => Math.Clamp((raw - StickCenter) / StickRange, -1, 1);
        s.LX = Axis(r[6] | (r[7] & 0x0F) << 8);
        s.LY = Axis(r[7] >> 4 | r[8] << 4);
        s.RX = Axis(r[9] | (r[10] & 0x0F) << 8);
        s.RY = Axis(r[10] >> 4 | r[11] << 4);

        // Each report carries 3 IMU samples (12 bytes apart); average them to reduce noise.
        short Avg(int o) => (short)((BitConverter.ToInt16(r, o) + BitConverter.ToInt16(r, o + 12) + BitConverter.ToInt16(r, o + 24)) / 3);
        s.Imu = r.Length >= 49 ? new ImuSample(Avg(13), Avg(15), Avg(17), Avg(19), Avg(21), Avg(23)) : null;

        lock (_lock) { _latest = s; _lastReport = DateTime.UtcNow; }
    }

    public bool TryRead(out PadState state)
    {
        lock (_lock)
        {
            state = _latest;
            return DateTime.UtcNow - _lastReport < TimeSpan.FromSeconds(1);
        }
    }

    public void Dispose() => _monitor.Dispose();
}
