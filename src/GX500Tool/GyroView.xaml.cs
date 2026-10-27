using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GX500Tool.Input;
using GX500Tool.Mapping;

namespace GX500Tool;

public partial class GyroView : UserControl
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    bool _loading;

    static GyroSettings G => Session.Profile.Gyro;

    public GyroView()
    {
        InitializeComponent();
        ButtonCombo.ItemsSource = PadState.AllButtons.ToList();

        MouseSens.ValueChanged += (_, _) => { G.MouseSensitivity = MouseSens.Value; Changed(); };
        StickSens.ValueChanged += (_, _) => { G.StickSensitivity = StickSens.Value; Changed(); };
        DeadzoneSlider.ValueChanged += (_, _) => { G.Deadzone = DeadzoneSlider.Value; Changed(); };
        SmoothSlider.ValueChanged += (_, _) => { G.Smoothing = SmoothSlider.Value; Changed(); };

        _timer.Tick += (_, _) => UpdateLive();
        _timer.Start();
        Session.Changed += LoadIntoUi;
        LoadIntoUi();
    }

    void LoadIntoUi()
    {
        _loading = true;
        OutOff.IsChecked = G.Output == GyroOutput.Off;
        OutMouse.IsChecked = G.Output == GyroOutput.Mouse;
        OutStick.IsChecked = G.Output == GyroOutput.RightStick;
        ActAlways.IsChecked = G.Activation == GyroActivation.Always;
        ActHold.IsChecked = G.Activation == GyroActivation.Hold;
        ActToggle.IsChecked = G.Activation == GyroActivation.Toggle;
        ButtonCombo.SelectedItem = G.Button;
        MouseSens.Value = G.MouseSensitivity;
        StickSens.Value = G.StickSensitivity;
        InvertX.IsChecked = G.InvertX;
        InvertY.IsChecked = G.InvertY;
        UseRoll.IsChecked = G.UseRoll;
        DeadzoneSlider.Value = G.Deadzone;
        SmoothSlider.Value = G.Smoothing;
        CalibText.Text = G.BiasX == 0 && G.BiasY == 0 && G.BiasZ == 0
            ? "Belum dikalibrasi."
            : $"Bias tersimpan: X {G.BiasX:0.0}  Y {G.BiasY:0.0}  Z {G.BiasZ:0.0}";
        _loading = false;
        Changed();
    }

    void Changed()
    {
        MouseSensLabel.Text = $"Sensitivitas mouse: {G.MouseSensitivity:0} piksel/derajat";
        StickSensLabel.Text = $"Sensitivitas analog: {G.StickSensitivity:0.0}×";
        DeadzoneLabel.Text = $"Deadzone gyro: {G.Deadzone:0.0} °/detik (abaikan getaran tangan kecil)";
        SmoothLabel.Text = $"Smoothing: {G.Smoothing * 100:0}%";
        ButtonCombo.IsEnabled = G.Activation != GyroActivation.Always;
        if (!_loading) Session.Save();
    }

    void Output_Click(object sender, RoutedEventArgs e)
    {
        G.Output = OutMouse.IsChecked == true ? GyroOutput.Mouse : OutStick.IsChecked == true ? GyroOutput.RightStick : GyroOutput.Off;
        Changed();
    }

    void Activation_Click(object sender, RoutedEventArgs e)
    {
        G.Activation = ActHold.IsChecked == true ? GyroActivation.Hold : ActToggle.IsChecked == true ? GyroActivation.Toggle : GyroActivation.Always;
        Changed();
    }

    void ButtonCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ButtonCombo.SelectedItem is PadButton b) { G.Button = b; Changed(); }
    }

    void Check_Click(object sender, RoutedEventArgs e)
    {
        G.InvertX = InvertX.IsChecked == true;
        G.InvertY = InvertY.IsChecked == true;
        G.UseRoll = UseRoll.IsChecked == true;
        Changed();
    }

    void Calibrate_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Engine is not { } engine) return;
        CalibButton.IsEnabled = false;
        CalibText.Text = "Mengkalibrasi... jangan sentuh gamepad.";
        // ~66 reports/s from the Switch protocol → ~130 samples ≈ 2 s.
        engine.Gyro.Calibrate(130, (x, y, z) => Dispatcher.BeginInvoke(() =>
        {
            G.BiasX = x; G.BiasY = y; G.BiasZ = z;
            Session.Save();
            CalibButton.IsEnabled = true;
            LoadIntoUi();
        }));
    }

    void UpdateLive()
    {
        var engine = Session.Engine;
        bool hasGyro = engine?.Source is SwitchSource;
        StatusText.Text = engine == null
            ? "Aktifkan virtual controller dulu di tab Remap & Turbo."
            : hasGyro ? "Gyro tersedia (mode Switch)."
                      : "Gyro tidak tersedia di mode ini. Pindahkan GX500 ke mode Switch lalu aktifkan ulang.";
        CalibButton.IsEnabled = hasGyro && !CalibText.Text.StartsWith("Mengkalibrasi");

        if (engine == null || !hasGyro) { LiveText.Text = ""; return; }
        var (yaw, pitch, roll) = engine.Gyro.Rates;
        LiveText.Text = $"Yaw   {yaw,7:0.0} °/s\nPitch {pitch,7:0.0} °/s\nRoll  {roll,7:0.0} °/s\n\nGyro {(engine.Gyro.Active ? "AKTIF" : "tidak aktif")}";
    }
}
