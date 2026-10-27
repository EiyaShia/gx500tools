using System.Windows;
using System.Windows.Controls;
using GX500Tool.Input;
using GX500Tool.Mapping;

namespace GX500Tool;

public partial class MacroView : UserControl
{
    static readonly (MacroMode Mode, string Label)[] Modes =
    [
        (MacroMode.Once, "Sekali jalan saat ditekan"),
        (MacroMode.WhileHeld, "Ulang terus selama ditahan"),
        (MacroMode.Toggle, "Tekan = mulai ulang, tekan lagi = berhenti"),
    ];

    public MacroView()
    {
        InitializeComponent();
        TriggerCombo.ItemsSource = PadState.AllButtons.ToList();
        ModeCombo.ItemsSource = Modes.Select(m => m.Label).ToList();
        Session.Changed += Reload;
        Reload();
    }

    Macro? Selected => MacroList.SelectedItem as Macro;

    void Reload()
    {
        var keep = Selected;
        MacroList.ItemsSource = null;
        MacroList.ItemsSource = Session.Profile.Macros;
        MacroList.SelectedItem = Session.Profile.Macros.Contains(keep!) ? keep : Session.Profile.Macros.FirstOrDefault();
        RecordButton.IsEnabled = Session.Engine != null;
    }

    void MacroList_Changed(object sender, SelectionChangedEventArgs e)
    {
        Editor.IsEnabled = Selected != null;
        MessageText.Text = "";
        if (Selected is not { } m) return;
        NameBox.Text = m.Name;
        TriggerCombo.SelectedItem = m.Trigger == PadButton.None ? null : m.Trigger;
        ModeCombo.SelectedIndex = Array.FindIndex(Modes, x => x.Mode == m.Mode);
        StepsBox.Text = Macro.ToText(m.Steps);
    }

    void New_Click(object sender, RoutedEventArgs e)
    {
        var m = new Macro { Steps = [new MacroStep { Buttons = PadButton.A, DurationMs = 50 }, new MacroStep { DurationMs = 50 }] };
        Session.Profile.Macros.Add(m);
        Session.Save();
        Reload();
        MacroList.SelectedItem = m;
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } m) return;
        Session.Profile.Macros.Remove(m);
        Session.Save();
        Reload();
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } m) return;
        List<MacroStep> steps;
        try { steps = Macro.Parse(StepsBox.Text); }
        catch (FormatException ex) { MessageText.Text = ex.Message; return; }

        var trigger = TriggerCombo.SelectedItem is PadButton b ? b : PadButton.None;
        var clash = Session.Profile.Macros.FirstOrDefault(x => x != m && x.Trigger == trigger && trigger != PadButton.None);

        m.Name = NameBox.Text.Trim().Length > 0 ? NameBox.Text.Trim() : "Macro";
        m.Trigger = trigger;
        m.Mode = Modes[Math.Max(ModeCombo.SelectedIndex, 0)].Mode;
        m.Steps = steps;
        Session.Save();
        Reload();
        MessageText.Text = trigger == PadButton.None ? "Tersimpan, tapi belum ada tombol pemicu."
            : clash != null ? $"Tersimpan. Perhatian: {trigger} juga dipakai macro \"{clash.Name}\"."
            : $"Tersimpan. Tombol {trigger} sekarang menjalankan macro ini (tidak lagi dikirim ke game).";
    }

    void Record_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Engine is not { } engine) return;
        if (!engine.Recorder.IsRecording)
        {
            engine.Recorder.Start();
            RecordButton.Content = "■ Stop rekam";
            MessageText.Text = "Merekam... tekan kombinasi tombol di gamepad. Waktu mulai dihitung dari tombol pertama.";
            return;
        }
        var steps = engine.Recorder.Stop();
        RecordButton.Content = "● Rekam";
        StepsBox.Text = Macro.ToText(steps);
        MessageText.Text = steps.Count == 0 ? "Tidak ada tombol yang terekam." : $"{steps.Count} langkah terekam. Cek lalu klik Simpan macro.";
    }
}
