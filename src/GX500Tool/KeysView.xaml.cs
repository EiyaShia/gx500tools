using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GX500Tool.Input;
using GX500Tool.Output;

namespace GX500Tool;

public partial class KeysView : UserControl
{
    const string NoKey = "(tidak ada)";

    readonly Dictionary<PadButton, ComboBox> _combos = new();
    bool _loading;

    public KeysView()
    {
        InitializeComponent();
        var options = new List<string> { NoKey };
        options.AddRange(Keyboard.Targets);

        int row = 0;
        foreach (var b in PadState.AllButtons)
        {
            KeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new Border
            {
                Background = (Brush)FindResource("KeyBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 3, 10, 3), Padding = new Thickness(0, 5, 0, 5),
                Child = new TextBlock { Text = b.ToString(), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            };
            var combo = new ComboBox { ItemsSource = options, Margin = new Thickness(2) };
            combo.SelectionChanged += (_, _) =>
            {
                if (_loading) return;
                if (combo.SelectedItem is string s && s != NoKey) Session.Profile.KeyBinds[b] = s;
                else Session.Profile.KeyBinds.Remove(b);
                Session.Save();
            };
            Grid.SetRow(label, row); Grid.SetRow(combo, row); Grid.SetColumn(combo, 1);
            KeyGrid.Children.Add(label); KeyGrid.Children.Add(combo);
            _combos[b] = combo;
            row++;
        }

        SpeedSlider.ValueChanged += (_, _) =>
        {
            SpeedLabel.Text = $"Kecepatan mouse: {SpeedSlider.Value:0} piksel/detik";
            if (_loading) return;
            Session.Profile.StickMouseSpeed = SpeedSlider.Value;
            Session.Save();
        };
        Session.Changed += Reload;
        Reload();
    }

    void Reload()
    {
        _loading = true;
        var p = Session.Profile;
        foreach (var (b, combo) in _combos)
            combo.SelectedItem = p.KeyBinds.TryGetValue(b, out var k) ? k : NoKey;
        WasdCheck.IsChecked = p.LeftStickWasd;
        MouseCheck.IsChecked = p.RightStickMouse;
        SpeedSlider.Value = p.StickMouseSpeed;
        SpeedLabel.Text = $"Kecepatan mouse: {p.StickMouseSpeed:0} piksel/detik";
        _loading = false;
    }

    void Check_Click(object sender, RoutedEventArgs e)
    {
        Session.Profile.LeftStickWasd = WasdCheck.IsChecked == true;
        Session.Profile.RightStickMouse = MouseCheck.IsChecked == true;
        Session.Save();
    }
}
