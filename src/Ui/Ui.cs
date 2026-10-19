// Window theme: loads the styles, sets light/dark colours with the Windows accent, and adds Mica on Windows 11.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

static class Ui
{
    static Application app;

    public static bool Dark { get; private set; }

    // Mica needs Windows 11 22H2 (build 22621) or newer.
    public static bool Mica { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    public static void Init(Application a)
    {
        app = a;
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Styles.xaml"))
            app.Resources = (ResourceDictionary)XamlReader.Load(s);
        Mica = WindowsBuild() >= 22621;
        ApplyTheme();
    }

    public static void ApplyTheme()
    {
        if (app == null) return;
        Dark = !AppsLight();
        var accent = Accent(Dark);
        Set("Accent", accent);
        Set("AccentHover", Color.FromArgb(0xE0, accent.R, accent.G, accent.B));
        Set("AccentSoft", Color.FromArgb(0x30, accent.R, accent.G, accent.B));
        Set("AccentFg", Dark ? Colors.Black : Colors.White);

        if (Dark)
        {
            Set("Bg", Mica ? "#00000000" : "#FF202020");
            Set("Layer", Mica ? "#4D3A3A3A" : "#FF272727");
            Set("LayerStroke", "#14FFFFFF");
            Set("Card", "#0DFFFFFF");
            Set("CardHover", "#15FFFFFF");
            Set("CardStroke", "#12FFFFFF");
            Set("Text", "#FFFFFFFF");
            Set("Text2", "#C8FFFFFF");
            Set("Text3", "#8AFFFFFF");
            Set("Ctrl", "#0FFFFFFF");
            Set("CtrlHover", "#18FFFFFF");
            Set("CtrlStroke", "#14FFFFFF");
            Set("ToggleOffStroke", "#9AFFFFFF");
            Set("NavHover", "#0FFFFFFF");
            Set("NavSel", "#14FFFFFF");
            Set("Danger", "#FFFF99A4");
        }
        else
        {
            Set("Bg", Mica ? "#00000000" : "#FFF3F3F3");
            Set("Layer", Mica ? "#80FFFFFF" : "#FFF9F9F9");
            Set("LayerStroke", "#0F000000");
            Set("Card", "#B3FFFFFF");
            Set("CardHover", "#F2FBFBFB");
            Set("CardStroke", "#12000000");
            Set("Text", "#E4000000");
            Set("Text2", "#9E000000");
            Set("Text3", "#72000000");
            Set("Ctrl", "#B3FFFFFF");
            Set("CtrlHover", "#E6F9F9F9");
            Set("CtrlStroke", "#1F000000");
            Set("ToggleOffStroke", "#72000000");
            Set("NavHover", "#09000000");
            Set("NavSel", "#0F000000");
            Set("Danger", "#FFC42B1C");
        }
    }

    // Dark title bar to match, and Mica behind the window where available.
    public static void Chrome(Window w)
    {
        IntPtr h = new WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero) return;
        int dark = Dark ? 1 : 0;
        DwmSetWindowAttribute(h, 20, ref dark, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
        if (!Mica) return;
        var source = HwndSource.FromHwnd(h);
        if (source != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(h, ref margins);
        int mica = 2; // DWMSBT_MAINWINDOW
        DwmSetWindowAttribute(h, 38, ref mica, 4); // DWMWA_SYSTEMBACKDROP_TYPE
    }

    // The arca icon at the closest size to the one asked for.
    public static BitmapSource AppIcon(int size)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("arca.ico"))
        {
            var decoder = new IconBitmapDecoder(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame best = null;
            foreach (var f in decoder.Frames)
                if (best == null || Math.Abs(f.PixelWidth - size) < Math.Abs(best.PixelWidth - size)) best = f;
            return best;
        }
    }

    static void Set(string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        app.Resources[key] = b;
    }

    static void Set(string key, string hex)
    {
        Set(key, (Color)ColorConverter.ConvertFromString(hex));
    }

    static bool AppsLight()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(Shell.ThemeKey))
            return k == null || Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) != 0;
    }

    // Windows' own accent shades: a lighter one on dark backgrounds, a darker one on light backgrounds.
    static Color Accent(bool dark)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
            {
                var palette = k == null ? null : k.GetValue("AccentPalette") as byte[];
                if (palette != null && palette.Length >= 32)
                {
                    int i = (dark ? 1 : 4) * 4; // Light2 / Dark1
                    return Color.FromRgb(palette[i], palette[i + 1], palette[i + 2]);
                }
            }
        }
        catch (Exception) { }
        return dark ? Color.FromRgb(0x60, 0xCD, 0xFF) : Color.FromRgb(0x00, 0x5F, 0xB8);
    }

    static int WindowsBuild()
    {
        try
        {
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                return int.Parse((string)k.GetValue("CurrentBuildNumber", "0"));
        }
        catch (Exception) { return 0; }
    }
}
