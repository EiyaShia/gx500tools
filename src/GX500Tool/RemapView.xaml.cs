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
    readonly List<string> _options = [Same, .. PadState.AllButtons.Select(b => b.ToString()), Disabled];
    bool _loading, _loadingSelection;

    static Profile Profile => Session.Profile;
    Brush KeyBrush => (Brush)FindResource("KeyBrush");
    Brush AccentBrush => (Brush)FindResource("AccentGradient");

    public RemapView()
    {
        InitializeComponent();
        ProfilePath.Text = "Tersimpan otomatis di:\n" + Session.ProfilesDir;
        SelTarget.ItemsSource = _options;
        PadView.ButtonSelected += ShowSelected;
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
            var combo = new ComboBox { ItemsSource = _options, Margin = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };
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
        TypeXbox.IsChecked = Profile.Type == GamepadType.Xbox;
        TypePlayStation.IsChecked = Profile.Type == GamepadType.PlayStation;
        HideCheck.IsChecked = Session.Settings.HideSwitchMode;
        HideCheck.IsEnabled = HidHide.IsInstalled;
        UpdateHideStatus(null);
        TurboSlider.Value = Profile.TurboHz;
        LdzSlider.Value = Profile.LeftDeadzone * 100;
        RdzSlider.Value = Profile.RightDeadzone * 100;
        _loading = false;
        Changed();
    }

    void Type_Click(object sender, RoutedEventArgs e)
    {
        Profile.Type = TypePlayStation.IsChecked == true ? GamepadType.PlayStation : GamepadType.Xbox;
        Changed();
        Session.NotifyChanged(); // refresh status labels with the new controller name
    }

    void Changed()
    {
        TypeHint.Text = Profile.Type switch
        {
            GamepadType.PlayStation => "Game melihat controller DualShock 4. A = ✕, B = ○, X = □, Y = △. Capture = klik touchpad.",
            _ => "Game melihat controller Xbox 360. Tombol sesuai label di gamepad: A = A, B = B.",
        };
        TurboLabel.Text = $"Kecepatan: {Profile.TurboHz:0} tekan/detik";
        LdzLabel.Text = $"Kiri: {Profile.LeftDeadzone * 100:0}%";
        RdzLabel.Text = $"Kanan: {Profile.RightDeadzone * 100:0}%";
        PadView.Refresh(Profile);
        if (PadView.Selected != PadButton.None) ShowSelected(PadView.Selected);
        if (!_loading) Session.Save();
    }

    /// <summary>Fills the editor card for a button picked on the drawing. Edits go through the list rows.</summary>
    void ShowSelected(PadButton b)
    {
        _loadingSelection = true;
        EditorCard.IsEnabled = true;
        SelLabel.Text = GamepadView.Label(b, Profile.Type);
        SelTarget.SelectedItem = _rows[b].Target.SelectedItem;
        SelTurbo.IsChecked = Profile.Turbo.Contains(b);
        _loadingSelection = false;
    }

    void SelTarget_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSelection || PadView.Selected == PadButton.None) return;
        _rows[PadView.Selected].Target.SelectedItem = SelTarget.SelectedItem; // row handler updates the profile
    }

    void SelTurbo_Click(object sender, RoutedEventArgs e)
    {
        var b = PadView.Selected;
        if (b == PadButton.None) return;
        bool on = SelTurbo.IsChecked == true;
        if (on) Profile.Turbo.Add(b); else Profile.Turbo.Remove(b);
        _rows[b].Turbo.IsChecked = on;
        Changed();
    }

    void SelReset_Click(object sender, RoutedEventArgs e)
    {
        var b = PadView.Selected;
        if (b == PadButton.None) return;
        Profile.Turbo.Remove(b);
        _rows[b].Turbo.IsChecked = false;
        _rows[b].Target.SelectedItem = Same; // row handler removes the remap and saves
        Changed();
    }

    void ShowList_Click(object sender, RoutedEventArgs e) =>
        ListRow.Height = ShowListCheck.IsChecked == true ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

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
            if (!StatusText.Text.StartsWith("G")) StatusText.Text = "Nonaktif"; // keep error messages visible
            foreach (var r in _rows.Values) r.Live.Background = KeyBrush;
            PadView.SetLive(PadButton.None);
        }
        else
        {
            StatusText.Text = $"Aktif · input: {engine.Source.Name} → virtual {engine.PadName}";
            StartButton.Content = "■ Matikan virtual controller";
            _timer.Start();
        }
        LoadIntoUi();
    }

    void HideCheck_Click(object sender, RoutedEventArgs e)
    {
        if (HideCheck.IsChecked == true)
        {
            Session.SetSetting(s => s.HideSwitchMode = true);
            UpdateHideStatus(null);
            return;
        }
        var error = Session.RemoveHide();
        if (error == null) Session.SetSetting(s => s.HideSwitchMode = false);
        else HideCheck.IsChecked = true;
        UpdateHideStatus(error);
    }

    void ApplyHide_Click(object sender, RoutedEventArgs e) => UpdateHideStatus(Session.ApplyHide());

    /// <summary>Shows hiding state; <paramref name="error"/> is the last action's failure, if any.</summary>
    void UpdateHideStatus(string? error)
    {
        var problem = Session.HideProblem;
        ApplyHideButton.Visibility = Session.Settings.HideSwitchMode && problem != null ? Visibility.Visible : Visibility.Collapsed;
        HideStatus.Foreground = (Brush)FindResource(error != null || problem != null ? "GoldBrush" : "MutedBrush");
        HideStatus.Text = error != null ? "⚠ " + error
            : !Session.Settings.HideSwitchMode ? "Mati. Di mode Switch, Windows bisa terus menutup Start menu, tray, dan prompt UAC."
            : problem != null ? $"⚠ {problem} Pindahkan GX500 ke mode X-Input, lalu klik Terapkan HidHide."
            : "✓ Aktif. GX500 mode Switch tersembunyi dari Windows; hanya app ini yang bisa membacanya.";
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
        PadView.SetLive(engine.Physical);
    }
}
