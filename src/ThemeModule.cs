// Light/dark theme toggle with a daily schedule (formerly umbra).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

class ThemeModule : Module
{
    public override string Id { get { return "theme"; } }
    public override string Title { get { return L.T("Light/dark theme"); } }
    public override string Description { get { return L.T("Switch Windows between light and dark with one click, or let it follow a daily schedule."); } }
    public override string Glyph { get { return ""; } }

    public override IList<Setting> Settings
    {
        get
        {
            return new[]
            {
                new Setting("auto", SettingKind.Bool, "Switch on a schedule", "A manual switch stays until the next scheduled one"),
                new Setting("light", SettingKind.Time, "Light from", "Time of day, like 07:00"),
                new Setting("dark", SettingKind.Time, "Dark from", "Time of day, like 19:00"),
            };
        }
    }

    public override IList<string> Usage
    {
        get
        {
            return new[]
            {
                L.T("Click the ◐ icon next to the clock to switch the theme."),
                L.T("Right-click it to turn the schedule on or off."),
            };
        }
    }

    NotifyIcon tray;
    TimeSpan lightAt, darkAt;
    bool auto;
    bool? lastScheduled, shown;

    public ThemeModule()
    {
        Ini.Default(Id, "light", "07:00");
        Ini.Default(Id, "dark", "19:00");
        Ini.Default(Id, "auto", "1");
    }

    protected override void OnStart()
    {
        tray = new NotifyIcon { ContextMenuStrip = Shell.Menu(BuildMenu) };
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        lastScheduled = shown = null;
        Tick();
        tray.Visible = true;
    }

    void BuildMenu(ToolStripItemCollection items)
    {
        items.Add(Shell.Bold(Shell.Item(L.T("Toggle theme"), delegate { Toggle(); })));
        items.Add(Shell.Check(L.F("Auto ({0:hh\\:mm} light, {1:hh\\:mm} dark)", lightAt, darkAt), auto,
            delegate { Ini.SetBool(Id, "auto", !auto); Tick(); }));
        items.Add(Shell.Item(L.T("Edit schedule…"), delegate { Shell.OpenSettings(); }));
        items.Add(new ToolStripSeparator());
        items.Add(TurnOffItem());
    }

    protected override void OnStop()
    {
        tray.Visible = false;
        tray.Dispose();
        tray = null;
    }

    // Applies the schedule only when crossing a boundary, so manual toggles stick until the next one.
    public override void Tick()
    {
        lightAt = Ini.GetTime(Id, "light", new TimeSpan(7, 0, 0));
        darkAt = Ini.GetTime(Id, "dark", new TimeSpan(19, 0, 0));
        auto = Ini.GetBool(Id, "auto");
        if (auto)
        {
            bool want = LightNow();
            if (lastScheduled != want)
            {
                lastScheduled = want;
                if (IsLight() != want) SetLight(want);
            }
        }
        else lastScheduled = null;
        Refresh();
    }

    public override void ThemeChanged()
    {
        Refresh();
    }

    void Toggle()
    {
        SetLight(!IsLight());
        Refresh();
    }

    bool LightNow()
    {
        var now = DateTime.Now.TimeOfDay;
        return lightAt < darkAt ? now >= lightAt && now < darkAt : now >= lightAt || now < darkAt;
    }

    static bool IsLight()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(Shell.ThemeKey))
            return k == null || Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) != 0;
    }

    static void SetLight(bool light)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(Shell.ThemeKey))
        {
            k.SetValue("AppsUseLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("SystemUsesLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
        }
        IntPtr r;
        Native.SendMessageTimeout((IntPtr)0xFFFF, 0x1A, IntPtr.Zero, "ImmersiveColorSet", 2, 100, out r);
    }

    void Refresh()
    {
        if (tray == null) return;
        bool light = IsLight();
        if (shown != light)
        {
            shown = light;
            Shell.SetIcon(tray, MakeIcon(light));
        }
        tray.Text = L.T(light ? "Theme — light" : "Theme — dark");
    }

    // ◐ drawn in the taskbar's contrast color.
    static Icon MakeIcon(bool light)
    {
        int s = Shell.IconSize();
        using (var bmp = new Bitmap(s, s))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var c = light ? Color.Black : Color.White;
                float pad = s / 8f, d = s - 2 * pad - 1;
                using (var pen = new Pen(c, Math.Max(1f, s / 12f))) g.DrawEllipse(pen, pad, pad, d, d);
                using (var brush = new SolidBrush(c)) g.FillPie(brush, pad, pad, d, d, 90, 180);
            }
            return Shell.ToIcon(bmp);
        }
    }
}
