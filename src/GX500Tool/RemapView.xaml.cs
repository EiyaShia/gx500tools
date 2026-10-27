using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GX500Tool.Input;
using GX500Tool.Mapping;
using GX500Tool.Output;

namespace GX500Tool;

public partial class RemapView : UserControl
{
    const string Same = "(sama)", Disabled = "Nonaktif";

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    readonly Dictionary<PadButton, (ComboBox Target, CheckBox Turbo, Border Live)> _rows = new();
    bool _loading;

    static Profile Profile => Session.Profile;
    Brush KeyBrush => (Brush)FindResource("KeyBrush");
    Brush AccentBrush => (Brush)FindResource("AccentGradient");

    public RemapView()
    {
        InitializeComponent();
        ProfilePath.Text = "Tersimpan otomatis di:\n" + Session.ProfilesDir;
        BuildRows();
        LoadIntoUi();

        TurboSlider.ValueChanged += (_, _) => { Profile.TurboHz = TurboSlider.Value; Changed(); };
        LdzSlider.ValueChanged += (_, _) => { Profile.LeftDeadzone = LdzSlider.Value / 100; Changed(); };
        RdzSlider.ValueChanged += (_, _) => { Profile.RightDeadzone = RdzSlider.Value / 100; Changed(); };

        _timer.Tick += (_, _) => UpdateLive();
        Session.Changed += OnSessionChanged;
        Dispatcher.ShutdownStarted += (_, _) => Session.Stop();
    }

    void BuildRows()
    {
        var options = new List<string> { Same };
        options.AddRange(PadState.AllButtons.Select(b => b.ToString()));
        options.Add(Disabled);

        int row = 0;
        foreach (var b in PadState.AllButtons)
        {
            MapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new Border
            {
                Background = KeyBrush, BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 3, 10, 3), Padding = new Thickness(0, 5, 0, 5),
                Child = new TextBlock { Text = b.ToString(), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            };
            var combo = new ComboBox { ItemsSource = options, Margin = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };
            var turbo = new CheckBox { Content = "Turbo", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var live = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = KeyBrush, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "Output saat ini" };

            combo.SelectionChanged += (_, _) =>
            {
                var t = ParseTarget(combo.SelectedItem as string);
                if (t == null || t == b) Profile.Remap.Remove(b); else Profile.Remap[b] = t.Value;
                Changed();
            };
            turbo.Click += (_, _) => { if (turbo.IsChecked == true) Profile.Turbo.Add(b); else Profile.Turbo.Remove(b); Changed(); };

            foreach (var (el, col) in new (UIElement, int)[] { (label, 0), (combo, 1), (turbo, 2), (live, 3) })
            {
                Grid.SetRow(el, row); Grid.SetColumn(el, col);
                MapGrid.Children.Add(el);
            }
            _rows[b] = (combo, turbo, live);
            row++;
        }
    }

    /// <summary>null = keep the original button.</summary>
    static PadButton? ParseTarget(string? s) => s switch
    {
        null or Same => null,
        Disabled => PadButton.None,
        _ => Enum.Parse<PadButton>(s),
    };

    void LoadIntoUi()
    {
        _loading = true;
        foreach (var (b, r) in _rows)
        {
            r.Target.SelectedItem = !Profile.Remap.TryGetValue(b, out var t) ? Same : t == PadButton.None ? Disabled : t.ToString();
            r.Turbo.IsChecked = Profile.Turbo.Contains(b);
        }
        TurboSlider.Value = Profile.TurboHz;
        LdzSlider.Value = Profile.LeftDeadzone * 100;
        RdzSlider.Value = Profile.RightDeadzone * 100;
        _loading = false;
        Changed();
    }

    void Changed()
    {
        TurboLabel.Text = $"Kecepatan: {Profile.TurboHz:0} tekan/detik";
        LdzLabel.Text = $"Kiri: {Profile.LeftDeadzone * 100:0}%";
        RdzLabel.Text = $"Kanan: {Profile.RightDeadzone * 100:0}%";
        if (!_loading) Session.Save();
    }

    void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Engine != null) { Session.Stop(); return; }
        try
        {
            if (!Session.Start()) StatusText.Text = "GX500 tidak ditemukan. Colokkan di mode Switch atau X-Input.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gagal: " + ex.Message + " (ViGEmBus sudah terinstal?)";
        }
    }

    void OnSessionChanged()
    {
        var engine = Session.Engine;
        if (engine == null)
        {
            _timer.Stop();
            StartButton.Content = "▶ Aktifkan virtual controller";
            HideButton.IsEnabled = UnhideButton.IsEnabled = false;
            if (!StatusText.Text.StartsWith("G")) StatusText.Text = "Nonaktif"; // keep error messages visible
            foreach (var r in _rows.Values) r.Live.Background = KeyBrush;
        }
        else
        {
            StatusText.Text = $"Aktif · input: {engine.Source.Name} → virtual Xbox 360";
            StartButton.Content = "■ Matikan virtual controller";
            HideButton.IsEnabled = UnhideButton.IsEnabled = engine.Source is SwitchSource && HidHide.IsInstalled;
            _timer.Start();
        }
        LoadIntoUi();
    }

    void Hide_Click(object sender, RoutedEventArgs e) => RunHidHide(HidHide.Hide, "Controller asli disembunyikan dari game.");
    void Unhide_Click(object sender, RoutedEventArgs e) => RunHidHide(HidHide.Unhide, "Controller asli terlihat lagi oleh game.");

    void RunHidHide(Action<string> action, string done)
    {
        if (Session.Engine?.Source is not SwitchSource s) return;
        try { action(s.DevicePath); StatusText.Text = done; }
        catch (Exception ex) { StatusText.Text = "HidHide gagal: " + ex.Message; }
    }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        Profile.ResetRemap();
        Session.Save();
        LoadIntoUi();
    }

    void UpdateLive()
    {
        if (Session.Engine is not { } engine) return;
        var o = engine.Output;
        foreach (var (b, r) in _rows)
            r.Live.Background = o.IsPressed(b) ? AccentBrush : KeyBrush;
    }
}
