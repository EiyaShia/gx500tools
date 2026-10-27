using System.Diagnostics;
using GX500Tool.Input;

namespace GX500Tool.Mapping;

public enum MacroMode { Once, WhileHeld, Toggle }

public sealed class MacroStep
{
    public PadButton Buttons { get; set; }
    public int DurationMs { get; set; }
}

public sealed class Macro
{
    public string Name { get; set; } = "Macro baru";
    public PadButton Trigger { get; set; } = PadButton.None;
    public MacroMode Mode { get; set; } = MacroMode.Once;
    public List<MacroStep> Steps { get; set; } = new();

    /// <summary>One step per line: "A+RB 80" holds A and RB for 80 ms, "- 50" waits 50 ms.</summary>
    public static string ToText(IEnumerable<MacroStep> steps) => string.Join(Environment.NewLine, steps.Select(s =>
        $"{(s.Buttons == PadButton.None ? "-" : string.Join("+", PadState.AllButtons.Where(b => (s.Buttons & b) != 0)))} {s.DurationMs}"));

    public static List<MacroStep> Parse(string text)
    {
        var steps = new List<MacroStep>();
        int lineNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var ms) || ms <= 0)
                throw new FormatException($"Baris {lineNo}: format harus \"TOMBOL DURASI_MS\", contoh \"A+B 100\".");

            var buttons = PadButton.None;
            if (parts[0] != "-")
                foreach (var name in parts[0].Split('+'))
                {
                    if (!Enum.TryParse<PadButton>(name, true, out var b) || b == PadButton.None)
                        throw new FormatException($"Baris {lineNo}: tombol \"{name}\" tidak dikenal.");
                    buttons |= b;
                }
            steps.Add(new MacroStep { Buttons = buttons, DurationMs = ms });
        }
        return steps;
    }
}

/// <summary>Plays macros: trigger buttons are swallowed and the macro's buttons are injected into the output.</summary>
public sealed class MacroPlayer
{
    readonly Dictionary<Macro, (bool Playing, long Start)> _state = new();
    PadButton _prev;

    public (PadButton Consumed, PadButton Injected) Update(PadButton physical, IReadOnlyList<Macro> macros, long now)
    {
        PadButton consumed = 0, injected = 0;
        foreach (var m in macros)
        {
            if (m.Trigger == PadButton.None || m.Steps.Count == 0) continue;
            consumed |= m.Trigger;
            bool pressed = (physical & m.Trigger) != 0;
            bool edge = pressed && (_prev & m.Trigger) == 0;
            var (playing, start) = _state.GetValueOrDefault(m);

            switch (m.Mode)
            {
                case MacroMode.Once when edge: (playing, start) = (true, now); break;
                case MacroMode.WhileHeld: if (edge) start = now; playing = pressed; break;
                case MacroMode.Toggle when edge: (playing, start) = (!playing, now); break;
            }

            if (playing)
            {
                long total = m.Steps.Sum(s => (long)s.DurationMs);
                long t = now - start;
                if (m.Mode == MacroMode.Once && t >= total) playing = false;
                else
                {
                    t %= total;
                    foreach (var s in m.Steps)
                    {
                        if (t < s.DurationMs) { injected |= s.Buttons; break; }
                        t -= s.DurationMs;
                    }
                }
            }
            _state[m] = (playing, start);
        }
        _prev = physical;
        return (consumed, injected);
    }
}

/// <summary>Records physical button changes into macro steps. Starts timing at the first press.</summary>
public sealed class MacroRecorder
{
    readonly object _lock = new();
    readonly Stopwatch _clock = new();
    List<MacroStep>? _steps;
    PadButton _current;
    long _since;

    public bool IsRecording { get { lock (_lock) return _steps != null; } }

    public void Start()
    {
        lock (_lock) { _steps = new(); _current = PadButton.None; _clock.Reset(); }
    }

    public void Feed(PadButton physical)
    {
        lock (_lock)
        {
            if (_steps == null || physical == _current) return;
            if (!_clock.IsRunning)
            {
                if (physical == PadButton.None) return;
                _clock.Start();
            }
            long now = _clock.ElapsedMilliseconds;
            if (now > _since) _steps.Add(new MacroStep { Buttons = _current, DurationMs = (int)(now - _since) });
            _current = physical;
            _since = now;
        }
    }

    public List<MacroStep> Stop()
    {
        lock (_lock)
        {
            var steps = _steps ?? new();
            if (_current != PadButton.None && _clock.IsRunning)
                steps.Add(new MacroStep { Buttons = _current, DurationMs = Math.Max(1, (int)(_clock.ElapsedMilliseconds - _since)) });
            if (steps.Count > 0 && steps[0].Buttons == PadButton.None) steps.RemoveAt(0);
            _steps = null;
            _since = 0;
            return steps;
        }
    }
}
