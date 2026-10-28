using System.Diagnostics;
using System.Runtime.InteropServices;
using GX500Tool.Input;
using GX500Tool.Mapping;
using GX500Tool.Output;

namespace GX500Tool;

/// <summary>Reads the physical pad, applies the profile and feeds the virtual pad on a background thread.</summary>
public sealed class Engine : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

    readonly IPadSource _source;
    IVirtualPad _pad;
    readonly Mapper _mapper = new();
    readonly MacroPlayer _macros = new();
    readonly KeyBridge _keys = new();
    readonly Mouse _mouse = new();
    readonly Thread _thread;
    volatile bool _running = true;

    public Profile Profile { get; set; }
    public IPadSource Source => _source;
    public string PadName => KindFor(Profile) == PadKind.DualShock4 ? "DualShock 4" : "Xbox 360";
    public GyroProcessor Gyro { get; } = new();
    public MacroRecorder Recorder { get; } = new();
    /// <summary>Latest mapped output, for display.</summary>
    public PadState Output { get; private set; }

    Engine(IPadSource source, Profile profile)
    {
        _source = source;
        Profile = profile;
        _pad = CreatePad(profile);
        _thread = new Thread(Loop) { IsBackground = true, Name = "Engine", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    /// <summary>Prefers Switch mode (has gyro), falls back to XInput. Returns null if no GX500 is found.</summary>
    public static Engine? Start(Profile profile)
    {
        IPadSource? source = SwitchSource.TryOpen();
        source ??= XInputSource.FindFirst(); // before the virtual pad exists, so we don't read our own output
        return source == null ? null : new Engine(source, profile);
    }

    static PadKind KindFor(Profile p) => p.Type == GamepadType.PlayStation ? PadKind.DualShock4 : PadKind.Xbox360;

    IVirtualPad CreatePad(Profile p)
    {
        var pad = VirtualPad.Create(KindFor(p));
        pad.Rumble += (l, s) => _source.SetRumble(l, s);
        return pad;
    }

    void Loop()
    {
        timeBeginPeriod(1);
        var clock = Stopwatch.StartNew();
        long last = 0;
        try
        {
            while (_running)
            {
                if (_source.TryRead(out var input))
                {
                    long now = clock.ElapsedMilliseconds;
                    double dt = (now - last) / 1000.0;
                    last = now;
                    var profile = Profile;
                    if (KindFor(profile) != _pad.Kind)
                    {
                        // Profile switched to another controller type: replug as the new kind.
                        _pad.Dispose();
                        _pad = CreatePad(profile);
                    }
                    var physical = Mapper.Physical(input);

                    Recorder.Feed(physical);
                    var (macroConsumed, injected) = _macros.Update(physical, profile.Macros, now);
                    var keyConsumed = _keys.Update(physical, ref input, profile, _mouse, dt);

                    // Buttons used by macros or keyboard bindings don't reach the virtual pad.
                    var consumed = macroConsumed | keyConsumed;
                    input.Buttons &= ~consumed;
                    if ((consumed & PadButton.LT) != 0) input.LT = 0;
                    if ((consumed & PadButton.RT) != 0) input.RT = 0;

                    var output = _mapper.Apply(input, profile, now);
                    output.Buttons |= injected;
                    if ((injected & PadButton.LT) != 0) output.LT = 1;
                    if ((injected & PadButton.RT) != 0) output.RT = 1;

                    var (gx, gy) = Gyro.Process(input, physical, profile.Gyro, now);
                    if (profile.Gyro.Output == GyroOutput.Mouse) _mouse.Move(gx, gy);
                    else if (profile.Gyro.Output == GyroOutput.RightStick)
                    {
                        output.RX = Math.Clamp(output.RX + gx, -1, 1);
                        output.RY = Math.Clamp(output.RY + gy, -1, 1);
                    }

                    _pad.Submit(output);
                    Output = output;
                }
                Thread.Sleep(1);
            }
        }
        finally
        {
            _keys.ReleaseAll();
            timeEndPeriod(1);
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread.Join(500);
        _pad.Dispose();
        _source.Dispose();
    }
}
