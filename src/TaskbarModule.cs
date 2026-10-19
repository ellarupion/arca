// Taskbar tweaks: middle click on a taskbar button closes that app, scrolling over the taskbar changes the volume.
// Works from outside explorer: a low-level mouse hook on its own thread, UI Automation to find the button under
// the cursor, and the shell's own app ID resolver to find the button's windows.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

class TaskbarModule : Module
{
    public override string Id { get { return "taskbar"; } }
    public override string Title { get { return L.T("Taskbar"); } }
    public override string Description { get { return L.T("Close apps with a middle click on the taskbar, and change the volume by scrolling over it."); } }
    public override string Glyph { get { return ""; } }

    // It changes how the taskbar reacts, so it waits to be switched on.
    public override bool DefaultOn { get { return false; } }

    public override IList<Setting> Settings
    {
        get
        {
            return new[]
            {
                new Setting("middle_close", SettingKind.Bool, "Middle click closes the app", "Click a taskbar button with the mouse wheel to close that app"),
                new Setting("close_all", SettingKind.Bool, "Close all of its windows", "When off, apps with several windows keep the usual middle click"),
                new Setting("volume", SettingKind.Bool, "Scroll over the taskbar to change volume", "Shows the usual Windows volume indicator"),
                new Setting("volume_anywhere", SettingKind.Choice, "Scroll anywhere while holding", "Change the volume over any window while this key is held",
                    new[] { "off", "ctrl", "alt", "shift", "win" }, new[] { "Off", "Ctrl", "Alt", "Shift", "Win" }),
                new Setting("volume_step", SettingKind.Number, "Volume step", "Each wheel notch changes the volume by 2% times this"),
            };
        }
    }

    public override IList<string> Usage
    {
        get
        {
            return new[]
            {
                L.T("Middle-click a taskbar button to close the app, the same as clicking its × button."),
                L.T("Scroll the mouse wheel anywhere over the taskbar to turn the volume up or down."),
                L.T("If you use Windhawk mods that do the same, turn them off to avoid surprises."),
            };
        }
    }

    const uint WM_QUIT = 0x12, WM_APP_VOLUME = 0x8001, WM_APP_MIDDLE = 0x8002, WM_APP_UNSTICK = 0x8003;
    const int WM_MOUSEWHEEL = 0x20A, WM_MBUTTONDOWN = 0x207, WM_MBUTTONUP = 0x208;
    static readonly IntPtr Marker = (IntPtr)0x41524341; // tags input arca injects itself

    // Copied from arca.ini on the UI thread, read by the hook thread.
    volatile bool middleOn, closeAll, volumeOn;
    volatile int modifierKey, step;

    Thread thread;
    uint threadId;
    IntPtr hook;
    Native.LowLevelMouseProc hookProc;
    int wheel;
    bool middlePending;

    public TaskbarModule()
    {
        Ini.Default(Id, "middle_close", "1");
        Ini.Default(Id, "close_all", "1");
        Ini.Default(Id, "volume", "1");
        Ini.Default(Id, "volume_anywhere", "off");
        Ini.Default(Id, "volume_step", "1");
    }

    protected override void OnStart()
    {
        Tick();
        var ready = new ManualResetEvent(false);
        thread = new Thread(delegate () { Run(ready); }) { IsBackground = true, Name = "arca taskbar hook" };
        thread.Start();
        ready.WaitOne(2000);
    }

    protected override void OnStop()
    {
        Native.PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        thread.Join(2000);
        thread = null;
    }

    public override void Tick()
    {
        middleOn = Ini.GetBool(Id, "middle_close");
        closeAll = Ini.GetBool(Id, "close_all");
        volumeOn = Ini.GetBool(Id, "volume");
        step = (int)Math.Max(1, Math.Min(10, Math.Round(Ini.GetDouble(Id, "volume_step", 1))));
        switch (Ini.Get(Id, "volume_anywhere").Trim().ToLowerInvariant())
        {
            case "ctrl": modifierKey = 0x11; break;
            case "alt": modifierKey = 0x12; break;
            case "shift": modifierKey = 0x10; break;
            case "win": modifierKey = 0x5B; break;
            default: modifierKey = 0; break;
        }
    }

    // The hook lives on its own thread, so slow work elsewhere in arca never makes the mouse stutter.
    void Run(ManualResetEvent ready)
    {
        threadId = Native.GetCurrentThreadId();
        hookProc = MouseHook;
        hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
        ready.Set();
        Native.MSG msg;
        while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_APP_VOLUME) Volume((int)msg.wParam);
            else if (msg.message == WM_APP_UNSTICK) Unstick();
            else if (msg.message == WM_APP_MIDDLE) MiddleClick((int)msg.wParam, (int)msg.lParam);
            else { Native.TranslateMessage(ref msg); Native.DispatchMessage(ref msg); }
        }
        Native.UnhookWindowsHookEx(hook);
    }

    IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = (int)wParam;
            var info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
            if (info.extra != Marker)
            {
                if (msg == WM_MOUSEWHEEL && volumeOn)
                {
                    bool held = modifierKey != 0 && Held(modifierKey);
                    if (held || OverTaskbar(info.pt))
                    {
                        wheel += (short)((info.mouseData >> 16) & 0xFFFF);
                        int notches = wheel / 120;
                        wheel %= 120;
                        if (notches != 0) Native.PostThreadMessage(threadId, WM_APP_VOLUME, (IntPtr)(notches * step), IntPtr.Zero);
                        if (held && (modifierKey == 0x12 || modifierKey == 0x5B)) Native.PostThreadMessage(threadId, WM_APP_UNSTICK, IntPtr.Zero, IntPtr.Zero);
                        return (IntPtr)1;
                    }
                }
                else if (msg == WM_MBUTTONDOWN && middleOn && OverTaskbar(info.pt))
                {
                    middlePending = true;
                    Native.PostThreadMessage(threadId, WM_APP_MIDDLE, (IntPtr)info.pt.X, (IntPtr)info.pt.Y);
                    return (IntPtr)1;
                }
                else if (msg == WM_MBUTTONUP && middlePending)
                {
                    middlePending = false;
                    return (IntPtr)1;
                }
            }
        }
        return Native.CallNextHookEx(hook, code, wParam, lParam);
    }

    static bool Held(int vk)
    {
        if (vk == 0x5B) return (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
        return (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    // Taskbars on every monitor. Rectangles only: no messages to explorer from inside the hook.
    static bool OverTaskbar(Native.POINT p)
    {
        IntPtr h = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
        if (Inside(h, p)) return true;
        h = IntPtr.Zero;
        while ((h = Native.FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            if (Inside(h, p)) return true;
        return false;
    }

    static bool Inside(IntPtr h, Native.POINT p)
    {
        Native.RECT r;
        return h != IntPtr.Zero && Native.IsWindowVisible(h) && Native.GetWindowRect(h, out r)
            && p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
    }

    // Volume keys: Windows changes the volume by 2% per press and shows its own indicator.
    static void Volume(int presses)
    {
        byte vk = (byte)(presses > 0 ? 0xAF : 0xAE);
        for (int i = 0; i < Math.Abs(presses); i++)
        {
            Native.keybd_event(vk, 0, 1, UIntPtr.Zero); // KEYEVENTF_EXTENDEDKEY
            Native.keybd_event(vk, 0, 3, UIntPtr.Zero); // ...and KEYUP
        }
    }

    // Win or Alt released after scrolling would open Start or the app's menu; a key press in between stops that.
    static void Unstick()
    {
        Native.keybd_event(0x07, 0, 0, UIntPtr.Zero);
        Native.keybd_event(0x07, 0, 2, UIntPtr.Zero);
    }

    // UI Automation and the app resolver are cross-process calls, so they run on a separate thread.
    void MiddleClick(int x, int y)
    {
        bool all = closeAll;
        var worker = new Thread(delegate ()
        {
            bool done = false;
            try
            {
                string appId = ButtonAppId(x, y);
                if (appId != null)
                {
                    var windows = WindowsOf(appId);
                    if (windows.Count > 0 && (all || windows.Count == 1))
                    {
                        foreach (var w in windows) Native.PostMessage(w, 0x112, (IntPtr)0xF060, IntPtr.Zero); // WM_SYSCOMMAND, SC_CLOSE
                        done = true;
                    }
                }
            }
            catch (Exception e) { Arca.Log(e); }
            if (!done)
            {
                // Not an app button (or nothing to close): give the taskbar its middle click back.
                Native.mouse_event(0x20, 0, 0, 0, Marker); // MIDDLEDOWN
                Native.mouse_event(0x40, 0, 0, 0, Marker); // MIDDLEUP
            }
        });
        worker.IsBackground = true;
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    // The app ID of the taskbar button under the point ("Appid: ..." in UI Automation).
    // FromPoint stops at the taskbar window, so look through that taskbar's buttons instead.
    static string ButtonAppId(int x, int y)
    {
        var p = new Native.POINT { X = x, Y = y };
        IntPtr bar = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
        if (!Inside(bar, p))
        {
            bar = IntPtr.Zero;
            while ((bar = Native.FindWindowEx(IntPtr.Zero, bar, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero && !Inside(bar, p)) { }
            if (bar == IntPtr.Zero) return null;
        }
        var buttons = AutomationElement.FromHandle(bar).FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer"));
        foreach (AutomationElement b in buttons)
        {
            var r = b.Current.BoundingRectangle;
            if (x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom)
            {
                string id = b.Current.AutomationId;
                return id.StartsWith("Appid: ") ? id.Substring(7) : null;
            }
        }
        return null;
    }

    // Windows that the taskbar groups under this app ID, asked from the shell's own resolver.
    static List<IntPtr> WindowsOf(string appId)
    {
        var found = new List<IntPtr>();
        var resolver = (IApplicationResolver8)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("660B90C8-73A9-4B58-8CAE-355B7F55341B")));
        try
        {
            Native.EnumWindows((h, l) =>
            {
                if (OnTaskbar(h))
                {
                    IntPtr id;
                    int a, b, c;
                    if (resolver.GetAppIDForWindow(h, out id, out a, out b, out c) == 0 && id != IntPtr.Zero)
                    {
                        if (string.Equals(Marshal.PtrToStringUni(id), appId, StringComparison.OrdinalIgnoreCase)) found.Add(h);
                        Marshal.FreeCoTaskMem(id);
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        finally { Marshal.ReleaseComObject(resolver); }
        return found;
    }

    // The windows that get a taskbar button: visible, not cloaked, not tool windows, unowned unless marked as app windows.
    static bool OnTaskbar(IntPtr h)
    {
        if (!Native.IsWindowVisible(h)) return false;
        int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
        bool appWindow = (ex & 0x40000) != 0, toolWindow = (ex & 0x80) != 0;
        if (!appWindow && (toolWindow || Native.GetWindow(h, 4) != IntPtr.Zero)) return false; // GW_OWNER
        int cloaked;
        return Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) != 0 || cloaked == 0; // DWMWA_CLOAKED
    }

    // Undocumented but long-standing shell interface (Windows 8 and later) that maps a window to its taskbar app ID.
    [ComImport, Guid("DE25675A-72DE-44b4-9373-05170450C140"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IApplicationResolver8
    {
        [PreserveSig] int GetAppIDForShortcut(IntPtr item, out IntPtr appId);
        [PreserveSig] int GetAppIDForShortcutObject(IntPtr link, IntPtr item, out IntPtr appId);
        [PreserveSig] int GetAppIDForWindow(IntPtr hwnd, out IntPtr appId, out int pinningPrevented, out int explicitAppId, out int embeddedShortcutValid);
    }
}
