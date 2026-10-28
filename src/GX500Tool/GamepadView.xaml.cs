using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GX500Tool.Input;
using GX500Tool.Mapping;

namespace GX500Tool;

/// <summary>Clickable GX500 drawing: shows remaps/turbo as badges and lights up held buttons.</summary>
public partial class GamepadView : UserControl
{
    // Position on the 640x400 canvas: left, top, width, height, corner radius.
    static readonly (PadButton Button, double X, double Y, double W, double H, double R)[] Layout =
    [
        (PadButton.LT, 125, 32, 100, 26, 9), (PadButton.RT, 415, 32, 100, 26, 9),
        (PadButton.LB, 115, 64, 120, 22, 8), (PadButton.RB, 405, 64, 120, 22, 8),
        (PadButton.LS, 140, 135, 70, 70, 35), (PadButton.RS, 358, 213, 64, 64, 32),
        (PadButton.Up, 237, 203, 26, 30, 5), (PadButton.Down, 237, 257, 26, 30, 5),
        (PadButton.Left, 207, 232, 30, 26, 5), (PadButton.Right, 263, 232, 30, 26, 5),
        (PadButton.Y, 447, 112, 36, 36, 18), (PadButton.A, 447, 182, 36, 36, 18),
        (PadButton.X, 412, 147, 36, 36, 18), (PadButton.B, 482, 147, 36, 36, 18),
        (PadButton.Back, 266, 132, 38, 18, 9), (PadButton.Start, 336, 132, 38, 18, 9),
        (PadButton.Guide, 305, 92, 30, 30, 15), (PadButton.Capture, 305, 166, 30, 18, 6),
    ];

    readonly Dictionary<PadButton, (Border Key, TextBlock Text, TextBlock Badge)> _keys = new();
    readonly Brush _key, _border, _accent, _gold, _selectedGlow;
    PadButton _live;

    public PadButton Selected { get; private set; } = PadButton.None;
    public event Action<PadButton>? ButtonSelected;

    public GamepadView()
    {
        InitializeComponent();
        _key = (Brush)FindResource("KeyBrush");
        _border = (Brush)FindResource("BorderBrush");
        _accent = (Brush)FindResource("AccentGradient");
        _gold = (Brush)FindResource("GoldBrush");
        _selectedGlow = (Brush)FindResource("AccentSoftBrush");

        foreach (var (b, x, y, w, h, r) in Layout)
        {
            var text = new TextBlock
            {
                FontSize = h < 24 ? 10.5 : 13, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            var key = new Border
            {
                Width = w, Height = h, CornerRadius = new CornerRadius(r), BorderThickness = new Thickness(1.5),
                Background = _key, BorderBrush = _border, Child = text, Cursor = Cursors.Hand, ToolTip = b.ToString(),
            };
            key.MouseLeftButtonUp += (_, _) => Select(b);
            Canvas.SetLeft(key, x); Canvas.SetTop(key, y);

            // Badge: a small pill centered on the key's top edge, showing the remap target and turbo.
            var badge = new TextBlock { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.Black };
            var pill = new Border
            {
                Background = _gold, CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 0, 5, 1),
                Child = badge, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center,
            };
            var badgeHost = new Grid { Width = w + 40, Children = { pill } };
            Canvas.SetLeft(badgeHost, x - 20); Canvas.SetTop(badgeHost, y - 9);

            Pad.Children.Add(key);
            Pad.Children.Add(badgeHost);
            _keys[b] = (key, text, badge);
        }
    }

    public void Select(PadButton b)
    {
        Selected = b;
        UpdateStyles();
        ButtonSelected?.Invoke(b);
    }

    /// <summary>Labels and badges for the given profile.</summary>
    public void Refresh(Profile p)
    {
        foreach (var (b, (_, text, badge)) in _keys)
        {
            text.Text = Label(b, p.Type);
            string remap = !p.Remap.TryGetValue(b, out var t) ? ""
                : t == PadButton.None ? "off" : "→" + Label(t, p.Type);
            string turbo = p.Turbo.Contains(b) ? "T" : "";
            badge.Text = string.Join(" ", new[] { remap, turbo }.Where(s => s.Length > 0));
            ((Border)badge.Parent).Visibility = badge.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        UpdateStyles();
    }

    public void SetLive(PadButton pressed)
    {
        if (pressed == _live) return;
        _live = pressed;
        UpdateStyles();
    }

    void UpdateStyles()
    {
        foreach (var (b, (key, _, badge)) in _keys)
        {
            bool live = (_live & b) != 0, selected = b == Selected;
            key.Background = live ? _accent : selected ? _selectedGlow : _key;
            key.BorderBrush = selected ? _gold : badge.Text.Length > 0 ? (Brush)FindResource("AccentBrush") : _border;
        }
    }

    /// <summary>Button name as printed on the chosen controller type.</summary>
    public static string Label(PadButton b, GamepadType type) => type == GamepadType.PlayStation
        ? b switch
        {
            PadButton.A => "✕", PadButton.B => "○", PadButton.X => "□", PadButton.Y => "△",
            PadButton.LB => "L1", PadButton.RB => "R1", PadButton.LT => "L2", PadButton.RT => "R2",
            PadButton.Back => "Share", PadButton.Start => "Options", PadButton.LS => "L3", PadButton.RS => "R3",
            PadButton.Guide => "PS", PadButton.Capture => "Touch",
            _ => Arrow(b),
        }
        : b switch
        {
            PadButton.Guide => "⌂", PadButton.Capture => "◉",
            PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right => Arrow(b),
            _ => b.ToString(),
        };

    static string Arrow(PadButton b) => b switch
    {
        PadButton.Up => "▲", PadButton.Down => "▼", PadButton.Left => "◀", PadButton.Right => "▶",
        _ => b.ToString(),
    };
}
