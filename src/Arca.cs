// arca — a box of tiny Windows tools in one exe.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("arca")]
[assembly: AssemblyProduct("arca")]
[assembly: AssemblyDescription("A box of tiny Windows tools")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class Arca
{
    static readonly TimeSpan UpdateEvery = TimeSpan.FromHours(12);
    const string BoxGlyph = "";
    const string ShowEventName = "arca-show-window";

    static System.Windows.Application app;
    static NotifyIcon tray;
    static List<Module> modules;
    static MainWindow window;
    static EventWaitHandle showEvent;
    static Action balloonClick;
    static DateTime nextUpdateCheck = DateTime.Now.AddSeconds(15);
    static bool stopped;

    public static IList<Module> Modules { get { return modules; } }

    // Something the window shows has changed: a tool was switched, the language, an update, the ini file.
    public static event Action Changed;

    [STAThread]
    static void Main(string[] args)
    {
        Native.SetDpiAware();
        bool updated = Array.IndexOf(args, "--updated") >= 0;
        bool quiet = updated || Array.IndexOf(args, "--tray") >= 0;

        using (var mutex = new Mutex(false, "arca-tray-suite"))
        {
            // After an update the old process may still be shutting down.
            bool owned;
            try { owned = mutex.WaitOne(updated ? 15000 : 0); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                // Already running: bring its window up instead.
                EventWaitHandle other;
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out other))
                {
                    Native.AllowSetForegroundWindow(-1); // ASFW_ANY: let the running arca come to the front
                    other.Set();
                    other.Dispose();
                }
                return;
            }

            System.Windows.Forms.Application.EnableVisualStyles();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { Log(e.ExceptionObject as Exception); StopAll(); };
            Updater.Cleanup();
            try { Shell.UpgradeStartup(); } catch (Exception e) { Log(e); }

            Ini.Default("arca", "language", "auto");
            Ini.Default("arca", "updates", "1");
            modules = new List<Module> { new ThemeModule(), new BinModule(), new WindowsModule() };
            foreach (var m in modules) Ini.Default("arca", m.Id, "1");
            Ini.Reload();
            L.Load();

            app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (s, e) => { Log(e.Exception); e.Handled = true; };
            Ui.Init(app);

            tray = new NotifyIcon { ContextMenuStrip = Shell.Menu(BuildMenu), Text = "arca", Icon = Shell.GlyphIcon(BoxGlyph, false), Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.BalloonTipClicked += delegate { var a = balloonClick; balloonClick = null; if (a != null) a(); };
            tray.BalloonTipClosed += delegate { balloonClick = null; };

            SystemEvents.UserPreferenceChanged += OnThemeChanged;
            Updater.Changed += RaiseChanged;

            Apply();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += delegate { Tick(); };
            timer.Start();

            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            ThreadPool.RegisterWaitForSingleObject(showEvent, delegate { app.Dispatcher.BeginInvoke(new Action(ShowWindow)); }, null, -1, false);

            if (updated) Notify(L.F("Updated to v{0}.", Updater.CurrentText));
            if (!quiet) ShowWindow();
            app.Run();

            SystemEvents.UserPreferenceChanged -= OnThemeChanged;
            mutex.ReleaseMutex();
        }
    }

    // A problem in the window must not take the tools down with it.
    public static void ShowWindow()
    {
        try
        {
            if (window == null) window = new MainWindow();
            window.Bring();
        }
        catch (Exception e)
        {
            Log(e);
            Notify(L.T("Couldn't open the arca window. Details are in arca.log."));
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
        RaiseChanged();
    }

    static void RaiseChanged()
    {
        var c = Changed;
        if (c != null) c();
    }

    public static void SetEnabled(Module m, bool on)
    {
        Ini.SetBool("arca", m.Id, on);
        Apply();
    }

    // A setting was changed in the window: apply it now rather than on the next tick.
    public static void SettingChanged(Module m)
    {
        if (m.Running) Safe(m.Tick);
    }

    public static void SuspendShortcuts(bool suspend)
    {
        foreach (var m in modules)
        {
            var mod = m;
            if (m.Running) Safe(delegate { mod.SuspendShortcuts(suspend); });
        }
    }

    public static void SetLanguage(string value)
    {
        Ini.Set("arca", "language", value);
        L.Load();
        foreach (var m in modules)
            if (m.Running) Safe(m.Tick);
        RaiseChanged();
    }

    public static string Language
    {
        get
        {
            string v = Ini.Get("arca", "language").Trim().ToLowerInvariant();
            return v == "en" || v == "ru" ? v : "auto";
        }
    }

    static void BuildMenu(ToolStripItemCollection items)
    {
        items.Add(Shell.Bold(Shell.Item(L.T("Open arca"), delegate { ShowWindow(); })));
        if (Updater.Available != null)
            items.Add(Shell.Item(L.F("Update to v{0}", Updater.Available), delegate { Updater.Install(); }));
        items.Add(new ToolStripSeparator());

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
        items.Add(Shell.Item(L.T("Exit"), delegate { Exit(); }));
    }

    static void OnThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (tray == null) return;
        Shell.SetIcon(tray, Shell.GlyphIcon(BoxGlyph, false));
        foreach (var m in modules)
            if (m.Running) Safe(m.ThemeChanged);
        Ui.ApplyTheme();
        if (window != null) window.ApplyChrome();
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
        if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        if (window != null) window.Quit();
        app.Shutdown();
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

    public static void Log(Exception e)
    {
        if (e == null) return;
        try { File.AppendAllText(Path.ChangeExtension(System.Windows.Forms.Application.ExecutablePath, ".log"), DateTime.Now.ToString("s") + " " + e + "\r\n\r\n"); }
        catch (Exception) { }
    }
}
