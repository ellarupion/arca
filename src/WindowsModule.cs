// Window tools: keep on top, hide to tray, transparency, move with Alt + drag.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class WindowsModule : Module
{
    public override string Id { get { return "windows"; } }
    public override string Title { get { return L.T("Window tools"); } }
    public override string Description { get { return L.T("Keep any window on top, hide it to the tray, make it see-through, or move it by grabbing anywhere."); } }
    public override string Glyph { get { return ""; } }

    public override IList<Setting> Settings
    {
        get
        {
            return new[]
            {
                new Setting("on_top", SettingKind.Hotkey, "Keep on top", "Pins the active window above all others. Press again to release"),
                new Setting("hide", SettingKind.Hotkey, "Hide to tray", "Click the window's tray icon to bring it back"),
                new Setting("fade", SettingKind.Hotkey, "More transparent", "Down to 20%, in 10% steps"),
                new Setting("unfade", SettingKind.Hotkey, "Less transparent", "Back up to fully opaque"),
                new Setting("alt_drag", SettingKind.Bool, "Move windows with Alt + drag", "Grab a window anywhere, not just the title bar"),
            };
        }
    }

    public override IList<string> Usage
    {
        get
        {
            return new[]
            {
                L.T("Shortcuts work on the active window."),
                L.T("When arca exits, every window goes back to normal."),
                L.T("Windows of apps running as administrator need arca to run as administrator too."),
            };
        }
    }

    // While the window records a new shortcut, ours must not fire.
    public override void SuspendShortcuts(bool suspend)
    {
        suspended = suspend;
        if (keys == null) return;
        keys.Clear();
        keySig = null;
        if (!suspend) Tick();
    }

    // Windows hidden to the tray are listed here, so they come back even if arca is killed.
    static readonly string HiddenFile = Path.ChangeExtension(Application.ExecutablePath, ".hidden");
    static readonly string[] ShellClasses = {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow" };

    readonly Dictionary<IntPtr, NotifyIcon> hidden = new Dictionary<IntPtr, NotifyIcon>();
    readonly List<IntPtr> pinned = new List<IntPtr>();
    readonly Dictionary<IntPtr, bool> faded = new Dictionary<IntPtr, bool>(); // value: arca added WS_EX_LAYERED

    Hotkeys keys;
    string keySig;
    bool suspended;

    IntPtr hook;
    Native.LowLevelMouseProc hookProc;
    IntPtr dragWnd;
    int dragX, dragY;

    public WindowsModule()
    {
        Ini.Default(Id, "on_top", "Ctrl+Win+T");
        Ini.Default(Id, "hide", "Ctrl+Win+H");
        Ini.Default(Id, "fade", "Ctrl+Win+PageDown");
        Ini.Default(Id, "unfade", "Ctrl+Win+PageUp");
        Ini.Default(Id, "alt_drag", "0");
    }

    protected override void OnStart()
    {
        RestoreOrphans();
        keys = new Hotkeys();
        keySig = null;
        Tick();
    }

    protected override void OnStop()
    {
        SetHook(false);
        keys.Dispose();
        keys = null;
        foreach (var h in hidden.Keys.ToList()) Restore(h, false);
        foreach (var h in pinned.ToList()) SetTop(h, false);
        foreach (var h in faded.Keys.ToList()) SetAlpha(h, 255);
    }

    public override void Tick()
    {
        string sig = string.Join("|", new[] { "on_top", "hide", "fade", "unfade" }.Select(k => Ini.Get(Id, k)));
        if (sig != keySig && !suspended)
        {
            keySig = sig;
            keys.Clear();
            var failed = new List<string>();
            Register("on_top", delegate { Target(h => SetTop(h, !IsTop(h))); }, failed);
            Register("hide", delegate { Target(Hide); }, failed);
            Register("fade", delegate { Target(h => Fade(h, -1)); }, failed);
            Register("unfade", delegate { Target(h => Fade(h, +1)); }, failed);
            if (failed.Count > 0)
                Arca.Notify(L.F("Shortcut taken or not recognised: {0}", string.Join(", ", failed)));
        }

        SetHook(Ini.GetBool(Id, "alt_drag"));

        // Forget windows that were closed, or shown again by their own app.
        foreach (var h in hidden.Keys.ToList())
            if (!Native.IsWindow(h) || Native.IsWindowVisible(h)) Forget(h);
        pinned.RemoveAll(h => !Native.IsWindow(h) || !IsTop(h));
        foreach (var h in faded.Keys.ToList())
            if (!Native.IsWindow(h)) faded.Remove(h);
    }

    void Register(string key, Action action, List<string> failed)
    {
        string spec = Ini.Get(Id, key);
        if (!keys.Add(spec, action)) failed.Add(spec);
    }

    public override void AddMenu(ToolStripItemCollection items)
    {
        var sub = new ToolStripMenuItem(Title);
        sub.DropDownItems.Add(Hint("Keep on top", "on_top"));
        sub.DropDownItems.Add(Hint("Hide to tray", "hide"));
        sub.DropDownItems.Add(Hint("More transparent", "fade"));
        sub.DropDownItems.Add(Hint("Less transparent", "unfade"));
        sub.DropDownItems.Add(new ToolStripSeparator());
        sub.DropDownItems.Add(new ToolStripMenuItem(L.T("Move windows with Alt + drag"), null,
            delegate { Ini.SetBool(Id, "alt_drag", !Ini.GetBool(Id, "alt_drag")); Tick(); }) { Checked = Ini.GetBool(Id, "alt_drag") });
        sub.DropDownItems.Add(Shell.Item(L.T("Edit shortcuts…"), delegate { Shell.OpenSettings(); }));

        if (hidden.Count > 0)
        {
            sub.DropDownItems.Add(new ToolStripSeparator());
            sub.DropDownItems.Add(Shell.Label(L.T("Hidden — click to show")));
            foreach (var h in hidden.Keys.ToList())
            {
                var hw = h;
                sub.DropDownItems.Add(new ToolStripMenuItem(WindowTitle(hw), hidden[hw].Icon.ToBitmap(), delegate { Restore(hw, true); }));
            }
        }
        if (pinned.Count > 0)
        {
            sub.DropDownItems.Add(new ToolStripSeparator());
            sub.DropDownItems.Add(Shell.Label(L.T("On top — click to release")));
            foreach (var h in pinned.ToList())
            {
                var hw = h;
                sub.DropDownItems.Add(new ToolStripMenuItem(WindowTitle(hw), null, delegate { SetTop(hw, false); }) { Checked = true });
            }
        }
        if (hidden.Count + pinned.Count + faded.Count > 0)
            sub.DropDownItems.Add(Shell.Item(L.T("Reset all windows"), delegate { OnStop(); OnStart(); }));
        items.Add(sub);
    }

    ToolStripMenuItem Hint(string text, string key)
    {
        string spec = Ini.Get(Id, key);
        return new ToolStripMenuItem(L.T(text)) { ShortcutKeyDisplayString = spec.Length > 0 ? spec : L.T("off"), Enabled = false };
    }

    static string WindowTitle(IntPtr h)
    {
        string t = Native.WindowText(h);
        if (t.Length == 0) t = L.T("(untitled window)");
        return t.Length <= 60 ? t : t.Substring(0, 59) + "…";
    }

    // Runs the action on the active window, unless it's the desktop, the taskbar or arca itself.
    static void Target(Action<IntPtr> action)
    {
        IntPtr h = Native.GetForegroundWindow();
        if (Usable(h)) action(h);
    }

    static bool Usable(IntPtr h)
    {
        if (h == IntPtr.Zero || !Native.IsWindow(h)) return false;
        uint pid;
        Native.GetWindowThreadProcessId(h, out pid);
        if (pid == Process.GetCurrentProcess().Id) return false;
        return Array.IndexOf(ShellClasses, Native.ClassName(h)) < 0;
    }

    // ---- keep on top

    static bool IsTop(IntPtr h)
    {
        return (Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0;
    }

    void SetTop(IntPtr h, bool top)
    {
        Native.SetWindowPos(h, top ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        pinned.Remove(h);
        if (top && IsTop(h)) pinned.Add(h);
        else if (top) Arca.Notify(L.T("Can't change this window. Apps running as administrator need arca to run as administrator too."));
    }

    // ---- hide to tray

    void Hide(IntPtr h)
    {
        if (hidden.ContainsKey(h)) return;
        var menu = new ContextMenuStrip();
        menu.Items.Add(Shell.Bold(Shell.Item(L.T("Show"), delegate { Restore(h, true); })));
        menu.Items.Add(Shell.Item(L.T("Close window"), delegate { Restore(h, false); Native.PostMessage(h, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }));
        var tray = new NotifyIcon { Icon = WindowIcon(h), Text = Shell.Tip(WindowTitle(h)), ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Restore(h, true); };
        hidden[h] = tray;
        Native.ShowWindow(h, Native.SW_HIDE);
        SaveHidden();
    }

    void Restore(IntPtr h, bool activate)
    {
        if (Native.IsWindow(h))
        {
            Native.ShowWindow(h, Native.SW_SHOW);
            if (activate) Native.SetForegroundWindow(h);
        }
        Forget(h);
    }

    void Forget(IntPtr h)
    {
        NotifyIcon tray;
        if (!hidden.TryGetValue(h, out tray)) return;
        hidden.Remove(h);
        tray.Visible = false;
        if (tray.Icon != null) tray.Icon.Dispose();
        tray.Dispose();
        SaveHidden();
    }

    void SaveHidden()
    {
        try
        {
            if (hidden.Count == 0) File.Delete(HiddenFile);
            else File.WriteAllLines(HiddenFile, hidden.Keys.Select(h => h.ToInt64().ToString()).ToArray());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // Brings back windows a previous run hid and never got to restore.
    static void RestoreOrphans()
    {
        if (!File.Exists(HiddenFile)) return;
        try
        {
            foreach (var line in File.ReadAllLines(HiddenFile))
            {
                long v;
                if (!long.TryParse(line.Trim(), out v)) continue;
                var h = new IntPtr(v);
                if (Native.IsWindow(h) && !Native.IsWindowVisible(h)) Native.ShowWindow(h, Native.SW_SHOW);
            }
            File.Delete(HiddenFile);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static Icon WindowIcon(IntPtr h)
    {
        IntPtr icon, r;
        foreach (int type in new[] { 2, 0, 1 }) // ICON_SMALL2, ICON_SMALL, ICON_BIG
            if (Native.SendMessageTimeout(h, Native.WM_GETICON, (IntPtr)type, IntPtr.Zero, 2, 100, out r) != IntPtr.Zero && r != IntPtr.Zero)
                return (Icon)Icon.FromHandle(r).Clone();
        foreach (int index in new[] { -34, -14 }) // GCLP_HICONSM, GCLP_HICON
            if ((icon = Native.GetClassLongPtr(h, index)) != IntPtr.Zero)
                return (Icon)Icon.FromHandle(icon).Clone();
        try
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            using (var p = Process.GetProcessById((int)pid))
                return Icon.ExtractAssociatedIcon(p.MainModule.FileName);
        }
        catch (Exception) { return (Icon)SystemIcons.Application.Clone(); }
    }

    // ---- transparency

    void Fade(IntPtr h, int direction)
    {
        uint key, flags;
        byte alpha;
        bool layered = (Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_LAYERED) != 0;
        if (!faded.ContainsKey(h))
        {
            if (direction > 0) return;
            if (layered && !Native.GetLayeredWindowAttributes(h, out key, out alpha, out flags)) return; // draws its own transparency
            faded[h] = !layered;
        }
        if (!Native.GetLayeredWindowAttributes(h, out key, out alpha, out flags) || (flags & Native.LWA_ALPHA) == 0) alpha = 255;
        SetAlpha(h, Math.Max(51, Math.Min(255, alpha + direction * 26)));
    }

    void SetAlpha(IntPtr h, int alpha)
    {
        bool added;
        if (!faded.TryGetValue(h, out added) || !Native.IsWindow(h)) { faded.Remove(h); return; }
        int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
        if (added) Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_LAYERED);

        uint key, flags;
        byte old;
        if (!Native.GetLayeredWindowAttributes(h, out key, out old, out flags)) { key = 0; flags = 0; }
        Native.SetLayeredWindowAttributes(h, key, (byte)alpha, flags | Native.LWA_ALPHA);

        if (alpha >= 255)
        {
            if (added) Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex & ~Native.WS_EX_LAYERED);
            faded.Remove(h);
        }
    }

    // ---- Alt + drag

    void SetHook(bool on)
    {
        if (on && hook == IntPtr.Zero)
        {
            hookProc = MouseHook;
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
        }
        else if (!on && hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            dragWnd = IntPtr.Zero;
        }
    }

    IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = (int)wParam;
            var info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
            if (msg == Native.WM_LBUTTONDOWN && dragWnd == IntPtr.Zero && (Native.GetAsyncKeyState(Native.VK_MENU) & 0x8000) != 0)
            {
                IntPtr w = Native.GetAncestor(Native.WindowFromPoint(info.pt), 2); // GA_ROOT
                Native.RECT r;
                if (Usable(w) && !Native.IsZoomed(w) && Native.GetWindowRect(w, out r))
                {
                    dragWnd = w;
                    dragX = info.pt.X - r.Left;
                    dragY = info.pt.Y - r.Top;
                    // A key press between Alt down and up stops Alt from opening the app's menu bar.
                    Native.keybd_event(0x07, 0, 0, UIntPtr.Zero);
                    Native.keybd_event(0x07, 0, 2, UIntPtr.Zero);
                    return (IntPtr)1;
                }
            }
            else if (dragWnd != IntPtr.Zero && msg == Native.WM_MOUSEMOVE)
            {
                Native.SetWindowPos(dragWnd, IntPtr.Zero, info.pt.X - dragX, info.pt.Y - dragY, 0, 0,
                    Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_ASYNCWINDOWPOS);
            }
            else if (dragWnd != IntPtr.Zero && msg == Native.WM_LBUTTONUP)
            {
                dragWnd = IntPtr.Zero;
                return (IntPtr)1;
            }
        }
        return Native.CallNextHookEx(hook, code, wParam, lParam);
    }
}
