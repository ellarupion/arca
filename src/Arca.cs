// arca — a box of tiny Windows tray tools in one exe.
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
[assembly: AssemblyDescription("A box of tiny Windows tray tools")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class Arca
{
    static readonly TimeSpan UpdateEvery = TimeSpan.FromHours(12);

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

            Ini.Default("arca", "updates", "1");
            modules = new List<Module> { new ThemeModule(), new BinModule(), new WindowsModule() };
            foreach (var m in modules) Ini.Default("arca", m.Id, "1");
            Ini.Reload();

            var menu = new ContextMenuStrip();
            menu.Items.Add("…");
            menu.Opening += (s, e) => { BuildMenu(menu); e.Cancel = false; };
            tray = new NotifyIcon { ContextMenuStrip = menu, Text = "arca", Icon = Shell.GlyphIcon("", false), Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMenu(); };
            tray.BalloonTipClicked += delegate { var a = balloonClick; balloonClick = null; if (a != null) a(); };
            tray.BalloonTipClosed += delegate { balloonClick = null; };

            SystemEvents.UserPreferenceChanged += OnThemeChanged;
            Application.ApplicationExit += delegate { StopAll(); };

            Apply();
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += delegate { Tick(); };
            timer.Start();

            if (updated) Notify("Updated to v" + Updater.CurrentText + ".");
            Application.Run();
            SystemEvents.UserPreferenceChanged -= OnThemeChanged;
            mutex.ReleaseMutex();
        }
    }

    static void Tick()
    {
        if (Ini.Reload()) Apply();
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

    static void BuildMenu(ContextMenuStrip menu)
    {
        var items = menu.Items;
        var old = new ToolStripItem[items.Count];
        items.CopyTo(old, 0);
        items.Clear();
        foreach (var item in old) item.Dispose();

        if (Updater.Available != null)
        {
            items.Add(Shell.Bold(Shell.Item("Update to v" + Updater.Available, delegate { Updater.Install(); })));
            items.Add(new ToolStripSeparator());
        }

        int before = items.Count;
        foreach (var m in modules)
            if (m.Running) m.AddMenu(items);
        if (items.Count > before) items.Add(new ToolStripSeparator());

        items.Add(Shell.Label("Tools"));
        foreach (var m in modules)
        {
            var mod = m;
            items.Add(new ToolStripMenuItem(m.Title, null, delegate { SetEnabled(mod, !mod.Running); }) { Checked = m.Running });
        }
        items.Add(new ToolStripSeparator());

        items.Add(Shell.Item("Settings…", delegate { Shell.OpenSettings(); }));
        items.Add(new ToolStripMenuItem("Start with Windows", null, delegate { Shell.SetStartup(!Shell.InStartup()); }) { Checked = Shell.InStartup() });
        items.Add(new ToolStripMenuItem("Check for updates automatically", null,
            delegate { Ini.SetBool("arca", "updates", !Ini.GetBool("arca", "updates")); }) { Checked = Ini.GetBool("arca", "updates") });
        items.Add(Shell.Item("Check for updates now", delegate { Updater.Check(true); }));
        items.Add(Shell.Item("About arca v" + Updater.CurrentText, delegate { Process.Start("https://github.com/" + Updater.Repo); }));
        items.Add(new ToolStripSeparator());
        items.Add(Shell.Item("Exit", delegate { Exit(); }));
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
        Shell.SetIcon(tray, Shell.GlyphIcon("", false));
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
