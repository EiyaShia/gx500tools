using System.Drawing;
using System.Windows.Forms;

namespace GX500Tool;

/// <summary>Notification-area icon: show window, toggle the virtual controller, pick a profile, exit.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;

    public TrayIcon(Action show, Action exit)
    {
        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _icon.DoubleClick += (_, _) => show();
        _icon.ContextMenuStrip.Opening += (_, _) => BuildMenu(show, exit);
        Session.Changed += UpdateText;
        Session.BatteryChanged += OnBatteryChanged;
        UpdateText();
    }

    /// <summary>Picks the small (tray-sized) frame from the embedded app.ico instead of downscaling the 32 px one.</summary>
    static Icon LoadIcon()
    {
        var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
        if (res == null) return SystemIcons.Application;
        using var stream = res.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    void BuildMenu(Action show, Action exit)
    {
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();
        menu.Items.Add("Tampilkan GX500 Tool", null, (_, _) => show());
        menu.Items.Add(Session.Engine == null ? "Aktifkan virtual controller" : "Matikan virtual controller", null, (_, _) =>
        {
            if (Session.Engine != null) Session.Stop();
            else
            {
                try { if (!Session.Start()) Notify("GX500 tidak ditemukan."); }
                catch (Exception ex) { Notify("Gagal: " + ex.Message); }
            }
        });

        var profiles = new ToolStripMenuItem("Profil");
        foreach (var p in Session.Profiles)
        {
            var item = new ToolStripMenuItem(p.Name) { Checked = ReferenceEquals(p, Session.Profile) };
            item.Click += (_, _) => Session.Activate(p);
            profiles.DropDownItems.Add(item);
        }
        menu.Items.Add(profiles);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Keluar", null, (_, _) => exit());
    }

    void UpdateText()
    {
        var battery = Session.Battery is { } b ? $" · baterai {b.Percent}%{(b.Charging ? "⚡" : "")}" : "";
        var text = $"GX500 Tool · {Session.Profile.Name} · {(Session.Engine == null ? "nonaktif" : "aktif")}{battery}";
        _icon.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon limit
    }

    void OnBatteryChanged(Input.BatteryInfo? old, Input.BatteryInfo? now) => UpdateText();

    public void Notify(string message) => _icon.ShowBalloonTip(2000, "GX500 Tool", message, ToolTipIcon.None);

    public void Dispose()
    {
        Session.Changed -= UpdateText;
        Session.BatteryChanged -= OnBatteryChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
