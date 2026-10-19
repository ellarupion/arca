// Small helpers shared by the modules: tray icons, autostart, opening the settings file.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Microsoft.Win32;

static class Shell
{
    public const string ThemeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool LightTaskbar()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(ThemeKey))
            return k != null && Convert.ToInt32(k.GetValue("SystemUsesLightTheme", 0)) != 0;
    }

    public static Color Ink()
    {
        return LightTaskbar() ? Color.Black : Color.White;
    }

    public static int IconSize()
    {
        return SystemInformation.SmallIconSize.Width;
    }

    // A glyph from the system icon font (the one the other tray icons use), optionally with a dot in the corner.
    public static Icon GlyphIcon(string glyph, bool dot)
    {
        using (var bmp = Glyph(IconSize(), glyph, dot, Ink())) return ToIcon(bmp);
    }

    public static Bitmap Glyph(int s, string glyph, bool dot, Color ink)
    {
        var bmp = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font(IconFont(), s, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(ink))
        using (var fmt = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoClip })
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(glyph, font, brush, new RectangleF(0, 0, s, s), fmt);
            if (dot)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float r = s * 0.2f, cx = s - r - 0.5f, cy = s - r - 0.5f, gap = Math.Max(1f, s / 16f);
                g.CompositingMode = CompositingMode.SourceCopy;
                using (var clear = new SolidBrush(Color.Transparent))
                    g.FillEllipse(clear, cx - r - gap, cy - r - gap, 2 * (r + gap), 2 * (r + gap));
                g.CompositingMode = CompositingMode.SourceOver;
                g.FillEllipse(brush, cx - r, cy - r, 2 * r, 2 * r);
            }
        }
        return bmp;
    }

    public static Icon ToIcon(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h).Clone();
        Native.DestroyIcon(h);
        return icon;
    }

    static string iconFont;
    static string IconFont()
    {
        if (iconFont == null)
        {
            iconFont = "Segoe MDL2 Assets";
            foreach (var f in FontFamily.Families)
                if (f.Name == "Segoe Fluent Icons") { iconFont = f.Name; break; }
        }
        return iconFont;
    }

    // Swaps a tray icon and frees the old one.
    public static void SetIcon(NotifyIcon tray, Icon icon)
    {
        var old = tray.Icon;
        tray.Icon = icon;
        if (old != null) old.Dispose();
    }

    // NotifyIcon.Text throws past 63 characters on older .NET.
    public static string Tip(string text)
    {
        return text.Length <= 63 ? text : text.Substring(0, 62) + "…";
    }

    public static ToolStripMenuItem Item(string text, EventHandler click)
    {
        return new ToolStripMenuItem(text, null, click);
    }

    public static ToolStripMenuItem Bold(ToolStripMenuItem item)
    {
        item.Font = new Font(item.Font, FontStyle.Bold);
        return item;
    }

    public static ToolStripMenuItem Label(string text)
    {
        return new ToolStripMenuItem(text) { Enabled = false };
    }

    public static void OpenSettings()
    {
        Ini.Save();
        Process.Start("notepad.exe", "\"" + Ini.File + "\"");
    }

    public static bool InStartup()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue("arca") != null;
    }

    public static void SetStartup(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            if (on) k.SetValue("arca", "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue("arca", false);
    }
}
