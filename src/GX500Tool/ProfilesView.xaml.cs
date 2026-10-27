using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GX500Tool.Mapping;
using Microsoft.Win32;

namespace GX500Tool;

public partial class ProfilesView : UserControl
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "GX500Tool";

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    bool _loading;

    public ProfilesView()
    {
        InitializeComponent();
        Session.Changed += Reload;
        _timer.Tick += (_, _) => UpdateAddLast();
        _timer.Start();
        Reload();
    }

    void Reload()
    {
        _loading = true;
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = Session.Profiles.Select(p => p.Name).ToList();
        ProfileList.SelectedItem = Session.Profile.Name;
        NameBox.Text = Session.Profile.Name;
        ProcessBox.Text = string.Join(Environment.NewLine, Session.Profile.Processes);
        AutoSwitchCheck.IsChecked = Session.Settings.AutoSwitch;
        AutoStartCheck.IsChecked = Session.Settings.AutoStartEngine;
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            WindowsStartCheck.IsChecked = key?.GetValue(RunName) != null;
        _loading = false;
        UpdateAddLast();
    }

    void UpdateAddLast()
    {
        var exe = App.Switcher?.LastForeground;
        AddLastButton.Content = exe == null ? "Tambah game terakhir (belum ada)" : $"Tambah {exe}";
        AddLastButton.IsEnabled = exe != null;
    }

    void ProfileList_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfileList.SelectedItem is not string name) return;
        Session.Activate(Session.Profiles.First(p => p.Name == name));
    }

    void New_Click(object sender, RoutedEventArgs e) => Session.Create("Profil baru", null);
    void Duplicate_Click(object sender, RoutedEventArgs e) => Session.Create(Session.Profile.Name + " (salinan)", Session.Profile);

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Profiles.Count <= 1) return;
        if (MessageBox.Show($"Hapus profil \"{Session.Profile.Name}\"?", "GX500 Tool", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            Session.Delete(Session.Profile);
    }

    void Rename_Click(object sender, RoutedEventArgs e) => Session.Rename(Session.Profile, NameBox.Text);

    void SaveProcesses_Click(object sender, RoutedEventArgs e)
    {
        Session.Profile.Processes = ProcessBox.Text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0)
            .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s : s + ".exe")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Session.Save();
        Reload();
    }

    void AddLast_Click(object sender, RoutedEventArgs e)
    {
        var exe = App.Switcher?.LastForeground;
        if (exe == null) return;
        // Steal it from any other profile so one game maps to exactly one profile.
        foreach (var p in Session.Profiles) p.Processes.RemoveAll(x => x.Equals(exe, StringComparison.OrdinalIgnoreCase));
        Session.Profile.Processes.Add(exe);
        Session.SaveAll();
        Reload();
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Session.SetSetting(s =>
        {
            s.AutoSwitch = AutoSwitchCheck.IsChecked == true;
            s.AutoStartEngine = AutoStartCheck.IsChecked == true;
        });
    }

    void WindowsStart_Click(object sender, RoutedEventArgs e)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (WindowsStartCheck.IsChecked == true) key.SetValue(RunName, $"\"{Environment.ProcessPath}\" --tray");
        else key.DeleteValue(RunName, false);
    }
}
