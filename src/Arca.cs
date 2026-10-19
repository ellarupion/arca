// arca — a box of tiny Windows tools in one exe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("arca")]
[assembly: AssemblyProduct("arca")]
[assembly: AssemblyDescription("A box of tiny Windows tools")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class Arca
{
    static readonly TimeSpan UpdateEvery = TimeSpan.FromHours(12);
    const string BoxGlyph = "";

    static NotifyIcon tray;
    static List<Module> modules;
    static Action balloonClick;
    static DateTime nextUpdateCheck = DateTime.Now.AddSeconds(15);
    static bool stopped;

    [STAThread]
    static void Main(string[] args)
    {
        Native.SetDpiAware();
        bool updated = Array.IndexOf(args, "--updated") >= 0;

        using (var mutex = new Mutex(false, "arca-tray-suite"))
        {
            // After an update the old process may still be shutting down.
            try { if (!mutex.WaitOne(updated ? 15000 : 0)) return; }
            catch (AbandonedMutexException) { }

            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Log(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { Log(e.ExceptionObject as Exception); StopAll(); };
            Updater.Cleanup();

            Ini.Default("arca", "language", "auto");
            Ini.Default("arca", "updates", "1");
            modules = new List<Module> { new ThemeModule(), new BinModule(), new WindowsModule() };
            foreach (var m in modules) Ini.Default("arca", m.Id, "1");
            Ini.Reload();
            L.Load();

            tray = new NotifyIcon { ContextMenuStrip = Shell.Menu(BuildMenu), Text = "arca", Icon = Shell.GlyphIcon(BoxGlyph, false), Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMenu(); };
            tray.BalloonTipClicked += delegate { var a = balloonClick; balloonClick = null; if (a != null) a(); };
            tray.BalloonTipClosed += delegate { balloonClick = null; };

            SystemEvents.UserPreferenceChanged += OnThemeChanged;
            Application.ApplicationExit += delegate { StopAll(); };

            Apply();
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += delegate { Tick(); };
            timer.Start();

            if (updated) Notify(L.F("Updated to v{0}.", Updater.CurrentText));
            Application.Run();
            SystemEvents.UserPreferenceChanged -= OnThemeChanged;
            mutex.ReleaseMutex();
        }
    }

    static void Tick()
    {
        if (Ini.Reload()) { L.Load(); Apply(); }
        foreach (var m in modules)
            if (m.Running) Safe(m.Tick);
        if (Ini.GetBool("arca", "updates") && DateTime.Now >= nextUpdateCheck)
        {
            nextUpdateCheck = DateTime.Now + UpdateEvery;
            Updater.Check(false);
        }
    }

    // Starts and stops modules to match the [arca] section.
    static void Apply()
    {
        foreach (var m in modules)
        {
            var mod = m;
            if (Ini.GetBool("arca", m.Id)) Safe(mod.Start);
            else Safe(mod.Stop);
        }
    }

    public static void SetEnabled(Module m, bool on)
    {
        Ini.SetBool("arca", m.Id, on);
        Apply();
    }

    static void BuildMenu(ToolStripItemCollection items)
    {
        if (Updater.Available != null)
        {
            items.Add(Shell.Bold(Shell.Item(L.F("Update to v{0}", Updater.Available), delegate { Updater.Install(); })));
            items.Add(new ToolStripSeparator());
        }

        int before = items.Count;
        foreach (var m in modules)
            if (m.Running) m.AddMenu(items);
        if (items.Count > before) items.Add(new ToolStripSeparator());

        items.Add(Shell.Label(L.T("Tools")));
        foreach (var m in modules)
        {
            var mod = m;
            items.Add(Shell.Check(m.Title, m.Running, delegate { SetEnabled(mod, !mod.Running); }));
        }
        items.Add(new ToolStripSeparator());

        items.Add(Shell.Item(L.T("Settings…"), delegate { Shell.OpenSettings(); }));
        items.Add(LanguageMenu());
        items.Add(Shell.Check(L.T("Start with Windows"), Shell.InStartup(), delegate { Shell.SetStartup(!Shell.InStartup()); }));
        items.Add(Shell.Check(L.T("Check for updates automatically"), Ini.GetBool("arca", "updates"),
            delegate { Ini.SetBool("arca", "updates", !Ini.GetBool("arca", "updates")); }));
        items.Add(Shell.Item(L.T("Check for updates now"), delegate { Updater.Check(true); }));
        items.Add(Shell.Item(L.F("About arca v{0}", Updater.CurrentText), delegate { Process.Start("https://github.com/" + Updater.Repo); }));
        items.Add(new ToolStripSeparator());
        items.Add(Shell.Item(L.T("Exit"), delegate { Exit(); }));
    }

    // Language names stay in their own language so they can be found whatever is selected.
    static ToolStripMenuItem LanguageMenu()
    {
        var sub = new ToolStripMenuItem(L.T("Language"));
        string current = Ini.Get("arca", "language").Trim().ToLowerInvariant();
        if (current != "en" && current != "ru") current = "auto";
        foreach (var option in new[] { new[] { "auto", L.T("Same as Windows") }, new[] { "en", "English" }, new[] { "ru", "Русский" } })
        {
            string value = option[0];
            sub.DropDownItems.Add(Shell.Check(option[1], current == value, delegate { SetLanguage(value); }));
        }
        return sub;
    }

    static void SetLanguage(string value)
    {
        Ini.Set("arca", "language", value);
        L.Load();
        foreach (var m in modules)
            if (m.Running) Safe(m.Tick);
    }

    // NotifyIcon only opens its menu on right click; this opens it on left click too.
    static void ShowMenu()
    {
        var show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (show != null) show.Invoke(tray, null);
    }

    static void OnThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (tray == null) return;
        Shell.SetIcon(tray, Shell.GlyphIcon(BoxGlyph, false));
        foreach (var m in modules)
            if (m.Running) Safe(m.ThemeChanged);
    }

    public static void Notify(string text, Action onClick = null)
    {
        if (tray == null) return;
        balloonClick = onClick;
        tray.ShowBalloonTip(5000, "arca", text, ToolTipIcon.None);
    }

    public static void Exit()
    {
        StopAll();
        if (tray != null) tray.Visible = false;
        Application.Exit();
    }

    // Puts every window back the way it was, even when arca is going down because of an error.
    static void StopAll()
    {
        if (stopped || modules == null) return;
        stopped = true;
        foreach (var m in modules) Safe(m.Stop);
    }

    // One misbehaving tool shouldn't take the others down.
    static void Safe(Action action)
    {
        try { action(); }
        catch (Exception e) { Log(e); }
    }

    static void Log(Exception e)
    {
        if (e == null) return;
        try { File.AppendAllText(Path.ChangeExtension(Application.ExecutablePath, ".log"), DateTime.Now.ToString("s") + " " + e + "\r\n\r\n"); }
        catch (Exception) { }
    }
}
