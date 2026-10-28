using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GX500Tool.Input;
using HidSharp;

namespace GX500Tool;

public partial class MainWindow : Window
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(8) };
    readonly Dictionary<XButtons, Border> _buttonViews = new();
    readonly StickView _left, _right;
    readonly Brush _keyBrush, _accentBrush;
    int _padIndex = -1;
    HidMonitor? _hid;
    List<HidDevice> _hidDevices = [];

    public MainWindow()
    {
        InitializeComponent();
        _keyBrush = (Brush)FindResource("KeyBrush");
        _accentBrush = (Brush)FindResource("AccentGradient");
        _left = new StickView(LeftStick);
        _right = new StickView(RightStick);

        foreach (XButtons b in Enum.GetValues<XButtons>())
        {
            var border = new Border
            {
                Width = 74, Margin = new Thickness(3), Padding = new Thickness(4, 6, 4, 6), CornerRadius = new CornerRadius(6),
                Background = _keyBrush, BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = b.ToString(), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center },
            };
            _buttonViews[b] = border;
            ButtonPanel.Children.Add(border);
        }

        Session.Changed += UpdateSidebar;
        UpdateSidebar();

        DzSlider.ValueChanged += (_, _) => DzLabel.Text = $"Deadzone tampilan: {DzSlider.Value:0}%";
        _timer.Tick += (_, _) => PollXInput();
        _timer.Start();
        RefreshHid_Click(null, null);
        Closed += (_, _) => { if (_padIndex >= 0) XInput.SetVibration(_padIndex, 0, 0); _hid?.Dispose(); };
    }

    void Nav_Click(object sender, RoutedEventArgs e)
    {
        var nav = (RadioButton)sender;
        foreach (FrameworkElement page in Pages.Children)
            page.Visibility = page.Name == nav.Uid ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = (string)nav.Content;
    }

    void ProfileCard_Click(object sender, RoutedEventArgs e)
    {
        NavProfiles.IsChecked = true;
        Nav_Click(NavProfiles, e);
    }

    void SidebarStart_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Engine != null) { Session.Stop(); return; }
        try
        {
            if (!Session.Start()) SourceLabel.Text = "GX500 tidak ditemukan. Colokkan di mode Switch atau X-Input.";
        }
        catch (Exception ex)
        {
            SourceLabel.Text = "Gagal: " + ex.Message;
        }
    }

    void UpdateSidebar()
    {
        ProfileName.Text = Session.Profile.Name;
        var engine = Session.Engine;
        StatusDot.Fill = (Brush)FindResource(engine != null ? "OkBrush" : "MutedBrush");
        StatusLabel.Text = engine != null ? "Terhubung" : "Nonaktif";
        if (engine != null) SourceLabel.Text = $"{engine.Source.Name} → virtual {engine.PadName}";
        else if (!SourceLabel.Text.StartsWith("G")) SourceLabel.Text = "Virtual controller belum aktif"; // keep error messages
        SidebarStartText.Text = engine != null ? "Matikan" : "Aktifkan";
        SidebarStartIcon.Text = engine != null ? "" : "";
    }

    void PollXInput()
    {
        XInputState state = default;
        if (_padIndex < 0 || !XInput.TryGetState(_padIndex, out state))
        {
            _padIndex = -1;
            for (int i = 0; i < 4; i++)
                if (XInput.TryGetState(i, out state)) { _padIndex = i; break; }
            if (_padIndex < 0)
            {
                XStatus.Text = "Tidak ada controller XInput. Pindahkan GX500 ke mode X-Input, atau buka tab HID untuk mode D-Input/Switch.";
                return;
            }
        }

        XStatus.Text = $"Terhubung: XInput slot {_padIndex + 1}   |   paket #{state.PacketNumber}";
        var g = state.Gamepad;
        foreach (var (b, view) in _buttonViews)
            view.Background = g.Buttons.HasFlag(b) ? _accentBrush : _keyBrush;

        LtBar.Value = g.LeftTrigger;
        RtBar.Value = g.RightTrigger;
        double dz = DzSlider.Value / 100;
        LeftInfo.Text = _left.Update(g.ThumbLX, g.ThumbLY, dz);
        RightInfo.Text = _right.Update(g.ThumbRX, g.ThumbRY, dz);
    }

    void Vib_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_padIndex >= 0) XInput.SetVibration(_padIndex, VibL.Value, VibR.Value);
    }

    void RefreshHid_Click(object? sender, RoutedEventArgs? e)
    {
        _hidDevices = (ShowAllHid.IsChecked == true ? HidMonitor.FindAll() : HidMonitor.FindControllers()).ToList();
        HidList.ItemsSource = _hidDevices.Select(HidMonitor.Describe).ToList();
        if (_hidDevices.Count > 0) HidList.SelectedIndex = 0;
    }

    void HidConnect_Click(object sender, RoutedEventArgs e)
    {
        _hid?.Dispose();
        _hid = null;
        if (HidList.SelectedIndex < 0) return;

        var monitor = new HidMonitor(_hidDevices[HidList.SelectedIndex]);
        var seenIds = new SortedDictionary<byte, int>();
        int total = 0;
        monitor.ReportReceived += r =>
        {
            lock (seenIds) { seenIds[r[0]] = seenIds.GetValueOrDefault(r[0]) + 1; total++; }
            Dispatcher.BeginInvoke(() =>
            {
                string ids;
                lock (seenIds) ids = string.Join(", ", seenIds.Select(kv => $"0x{kv.Key:X2}×{kv.Value}"));
                RawText.Text = $"Total report: {total}   |   ID yang diterima: {ids}\n" +
                               $"Terakhir: ID 0x{r[0]:X2}, {r.Length} byte\n\n" +
                               string.Join(" ", r.Select(x => x.ToString("X2")));
            });
        };
        monitor.ImuReceived += s => Dispatcher.BeginInvoke(() =>
            GyroText.Text = $"Gyro   X {s.GyroX,7}  Y {s.GyroY,7}  Z {s.GyroZ,7}\nAccel  X {s.AccX,7}  Y {s.AccY,7}  Z {s.AccZ,7}");
        try
        {
            monitor.Start();
            _hid = monitor;
        }
        catch (Exception ex)
        {
            RawText.Text = "Gagal membuka device: " + ex.Message;
        }
    }

    /// <summary>Draws a stick position with a deadzone circle and a history trail to reveal drift.</summary>
    sealed class StickView
    {
        const double Size = 200;
        readonly Ellipse _dot;
        readonly Ellipse _dz;
        readonly Polyline _trail = new() { Stroke = new SolidColorBrush(Color.FromArgb(90, 154, 160, 255)), StrokeThickness = 1 };

        public StickView(Canvas canvas)
        {
            canvas.Children.Add(new Ellipse { Width = Size, Height = Size, Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x3F, 0x6E)), StrokeThickness = 2,
                                              Fill = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x26)) });
            _dz = new Ellipse { Fill = new SolidColorBrush(Color.FromArgb(50, 255, 120, 120)) };
            canvas.Children.Add(_dz);
            canvas.Children.Add(_trail);
            _dot = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7)) };
            canvas.Children.Add(_dot);
        }

        public string Update(short rawX, short rawY, double deadzone)
        {
            double x = rawX / 32768.0, y = rawY / 32768.0;
            double mag = Math.Sqrt(x * x + y * y);

            double dzSize = deadzone * Size;
            _dz.Width = _dz.Height = dzSize;
            Canvas.SetLeft(_dz, (Size - dzSize) / 2);
            Canvas.SetTop(_dz, (Size - dzSize) / 2);

            var p = new Point((x + 1) / 2 * Size, (1 - y) / 2 * Size);
            Canvas.SetLeft(_dot, p.X - 6);
            Canvas.SetTop(_dot, p.Y - 6);
            _trail.Points.Add(p);
            if (_trail.Points.Count > 120) _trail.Points.RemoveAt(0);

            string drift = mag > 0.02 && mag < deadzone ? "  (drift, masih di dalam deadzone)" : "";
            return $"X {rawX,6}  Y {rawY,6}\nJarak dari tengah {mag * 100:0.0}%{drift}";
        }
    }
}
