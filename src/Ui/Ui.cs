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

        // Glass: a translucent fill, a bright sheen across the top and a light edge that fades down the sides.
        if (Dark)
        {
            Set("Bg", Mica ? "#00000000" : "#FF1C1C21");
            Set("Layer", Mica ? "#8C17171C" : "#FF1C1C21");
            Set("Card", "#12FFFFFF");
            Set("CardHover", "#1CFFFFFF");
            Gradient("CardStroke", "#2EFFFFFF", "#0AFFFFFF", "#14FFFFFF");
            Set("Text", "#FFFFFFFF");
            Set("Text2", "#B8FFFFFF");
            Set("Text3", "#80FFFFFF");
            Set("Ctrl", "#14FFFFFF");
            Set("CtrlHover", "#22FFFFFF");
            Set("ToggleOff", "#33FFFFFF");
            Set("Popup", "#F02A2A30");
            Set("Glass", "#1FFFFFFF");
            Gradient("GlassSheen", "#38FFFFFF", "#00FFFFFF", "#00FFFFFF");
            Gradient("GlassStroke", "#80FFFFFF", "#14FFFFFF", "#40FFFFFF");
            Set("GlassEdge", "#66000000");
            Set("Danger", "#FFFF99A4");
        }
        else
        {
            Set("Bg", Mica ? "#00000000" : "#FFEFF0F4");
            Set("Layer", Mica ? "#A6EFF0F4" : "#FFEFF0F4");
            Set("Card", "#B3FFFFFF");
            Set("CardHover", "#E6FFFFFF");
            Gradient("CardStroke", "#FFFFFFFF", "#33FFFFFF", "#26000000");
            Set("Text", "#E6000000");
            Set("Text2", "#99000000");
            Set("Text3", "#6B000000");
            Set("Ctrl", "#CCFFFFFF");
            Set("CtrlHover", "#FFFFFFFF");
            Set("ToggleOff", "#29000000");
            Set("Popup", "#F7FFFFFF");
            Set("Glass", "#66FFFFFF");
            Gradient("GlassSheen", "#B3FFFFFF", "#00FFFFFF", "#00FFFFFF");
            Gradient("GlassStroke", "#FFFFFFFF", "#59FFFFFF", "#CCFFFFFF");
            Set("GlassEdge", "#24000000");
            Set("Danger", "#FFC42B1C");
        }
    }

    // A top-to-bottom gradient through three colours.
    static void Gradient(string key, string top, string middle, string bottom)
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(top), 0));
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle), 0.5));
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(bottom), 1));
        b.Freeze();
        app.Resources[key] = b;
    }

    // Dark title bar to match, and a blurred see-through backdrop (acrylic) where available.
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
        int acrylic = 3; // DWMSBT_TRANSIENTWINDOW
        DwmSetWindowAttribute(h, 38, ref acrylic, 4); // DWMWA_SYSTEMBACKDROP_TYPE
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
