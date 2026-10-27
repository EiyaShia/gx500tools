using GX500Tool.Input;

namespace GX500Tool.Mapping;

public enum GyroOutput { Off, Mouse, RightStick }
public enum GyroActivation { Always, Hold, Toggle }

public sealed class GyroSettings
{
    public GyroOutput Output { get; set; } = GyroOutput.Off;
    public GyroActivation Activation { get; set; } = GyroActivation.Always;
    public PadButton Button { get; set; } = PadButton.LT;
    /// <summary>Mouse: pixels per degree. Stick: multiplier on FullStickRate.</summary>
    public double MouseSensitivity { get; set; } = 20;
    public double StickSensitivity { get; set; } = 1;
    /// <summary>Angular speed (deg/s) that gives full stick deflection at sensitivity 1.</summary>
    public double FullStickRate { get; set; } = 180;
    public bool InvertX { get; set; }
    public bool InvertY { get; set; }
    /// <summary>Use roll (tilting left/right) instead of yaw (turning) for horizontal movement.</summary>
    public bool UseRoll { get; set; }
    public double Deadzone { get; set; } = 1.5;   // deg/s
    public double Smoothing { get; set; } = 0.3;  // 0 = none, 0.9 = heavy
    public double BiasX { get; set; }
    public double BiasY { get; set; }
    public double BiasZ { get; set; }
}

/// <summary>Turns raw gyro samples into mouse deltas or a right-stick offset.</summary>
public sealed class GyroProcessor
{
    // Switch Pro gyro scale (±2000 dps over int16).
    const double DegPerLsb = 0.061;

    ImuSample? _last;
    long _lastMs;
    bool _toggled, _prevButton;
    double _smoothX, _smoothY;

    int _calibRemaining;
    double _sumX, _sumY, _sumZ;
    int _calibCount;
    Action<double, double, double>? _calibDone;

    /// <summary>Latest yaw/pitch/roll speeds in deg/s (bias removed), for display.</summary>
    public (double Yaw, double Pitch, double Roll) Rates { get; private set; }
    public bool Active { get; private set; }

    public void Calibrate(int samples, Action<double, double, double> done)
    {
        _sumX = _sumY = _sumZ = 0;
        _calibCount = 0;
        _calibDone = done;
        _calibRemaining = samples;
    }

    /// <summary>Returns (dx, dy) in degrees for mouse, or stick deflection -1..1, depending on settings.</summary>
    public (double X, double Y) Process(in PadState input, PadButton physical, GyroSettings g, long nowMs)
    {
        Active = UpdateActivation(physical, g);
        if (input.Imu is not { } imu || ReferenceEquals(imu, _last)) return (0, 0);

        double dt = _last == null ? 0 : Math.Clamp((nowMs - _lastMs) / 1000.0, 0, 0.05);
        _last = imu;
        _lastMs = nowMs;

        if (_calibRemaining > 0)
        {
            _sumX += imu.GyroX; _sumY += imu.GyroY; _sumZ += imu.GyroZ; _calibCount++;
            if (--_calibRemaining == 0) _calibDone?.Invoke(_sumX / _calibCount, _sumY / _calibCount, _sumZ / _calibCount);
            return (0, 0);
        }

        // Axis convention from SDL's Switch driver: pitch = -Y, yaw = Z, roll = -X.
        double pitch = -(imu.GyroY - g.BiasY) * DegPerLsb;
        double yaw = (imu.GyroZ - g.BiasZ) * DegPerLsb;
        double roll = -(imu.GyroX - g.BiasX) * DegPerLsb;
        Rates = (yaw, pitch, roll);

        double h = -(g.UseRoll ? roll : yaw), v = -pitch;
        if (Math.Sqrt(h * h + v * v) < g.Deadzone) h = v = 0;
        if (g.InvertX) h = -h;
        if (g.InvertY) v = -v;

        _smoothX = _smoothX * g.Smoothing + h * (1 - g.Smoothing);
        _smoothY = _smoothY * g.Smoothing + v * (1 - g.Smoothing);

        if (!Active || g.Output == GyroOutput.Off) return (0, 0);
        return g.Output == GyroOutput.Mouse
            ? (_smoothX * dt * g.MouseSensitivity, _smoothY * dt * g.MouseSensitivity)
            : (_smoothX / g.FullStickRate * g.StickSensitivity, -_smoothY / g.FullStickRate * g.StickSensitivity);
    }

    bool UpdateActivation(PadButton physical, GyroSettings g)
    {
        bool pressed = (physical & g.Button) != 0;
        if (pressed && !_prevButton) _toggled = !_toggled;
        _prevButton = pressed;
        return g.Activation switch
        {
            GyroActivation.Hold => pressed,
            GyroActivation.Toggle => _toggled,
            _ => true,
        };
    }
}
