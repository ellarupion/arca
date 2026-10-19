// The Recycle Bin in the tray: open, empty, auto-empty over a size limit (formerly vacua).
using System;
using System.Diagnostics;
using System.Windows.Forms;

class BinModule : Module
{
    public override string Id { get { return "bin"; } }
    public override string Title { get { return L.T("Recycle Bin"); } }

    const uint NoConfirmation = 1, NoProgressUI = 2, NoSound = 4;

    NotifyIcon tray;
    long size, items;
    bool? shownFull;

    public BinModule()
    {
        Ini.Default(Id, "doubleclick", "1");
        Ini.Default(Id, "confirm", "1");
        Ini.Default(Id, "auto", "0");
        Ini.Default(Id, "limit_gb", "10");
    }

    protected override void OnStart()
    {
        tray = new NotifyIcon { ContextMenuStrip = Shell.Menu(BuildMenu) };
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left && !Ini.GetBool(Id, "doubleclick")) Open(); };
        tray.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left && Ini.GetBool(Id, "doubleclick")) Open(); };
        shownFull = null;
        Tick();
        tray.Visible = true;
    }

    void BuildMenu(ToolStripItemCollection menu)
    {
        Query();
        Refresh();
        var empty = Shell.Bold(Shell.Item(L.T("Empty Recycle Bin"), delegate { Empty(!Ini.GetBool(Id, "confirm"), 0); }));
        empty.Enabled = items > 0;
        menu.Add(empty);
        menu.Add(Shell.Item(L.T("Open Recycle Bin"), delegate { Open(); }));
        menu.Add(new ToolStripSeparator());
        menu.Add(Toggle("Open on double-click", "doubleclick"));
        menu.Add(Toggle("Ask before emptying", "confirm"));
        menu.Add(Shell.Check(L.F("Auto-empty over {0:0.##} GB", Ini.GetDouble(Id, "limit_gb", 10)), Ini.GetBool(Id, "auto"),
            delegate { Ini.SetBool(Id, "auto", !Ini.GetBool(Id, "auto")); Tick(); }));
        menu.Add(Shell.Item(L.T("Edit settings…"), delegate { Shell.OpenSettings(); }));
        menu.Add(new ToolStripSeparator());
        menu.Add(TurnOffItem());
    }

    ToolStripMenuItem Toggle(string text, string key)
    {
        return Shell.Check(L.T(text), Ini.GetBool(Id, key), delegate { Ini.SetBool(Id, key, !Ini.GetBool(Id, key)); });
    }

    protected override void OnStop()
    {
        tray.Visible = false;
        tray.Dispose();
        tray = null;
    }

    public override void Tick()
    {
        Query();
        double limit = Ini.GetDouble(Id, "limit_gb", 10);
        if (Ini.GetBool(Id, "auto") && limit > 0 && size > limit * 1073741824)
            Empty(true, NoProgressUI | NoSound);
        Refresh();
    }

    public override void ThemeChanged()
    {
        shownFull = null;
        Refresh();
    }

    void Query()
    {
        var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.SHQUERYRBINFO)) };
        if (Native.SHQueryRecycleBin(null, ref info) == 0) { size = info.i64Size; items = info.i64NumItems; }
    }

    void Empty(bool silent, uint extra)
    {
        Query();
        if (items > 0) Native.SHEmptyRecycleBin(IntPtr.Zero, null, (silent ? NoConfirmation : 0) | extra);
        Query();
        Refresh();
    }

    static void Open()
    {
        Process.Start("explorer.exe", "shell:RecycleBinFolder");
    }

    void Refresh()
    {
        if (tray == null) return;
        bool full = items > 0;
        if (shownFull != full)
        {
            shownFull = full;
            Shell.SetIcon(tray, Shell.GlyphIcon("", full));
        }
        tray.Text = full ? L.F("Recycle Bin — {0}, {1}", L.Items(items), FormatSize(size)) : L.T("Recycle Bin — empty");
    }

    static string FormatSize(long bytes)
    {
        string[] units = L.SizeUnits();
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return string.Format(i == 0 ? "{0:0} {1}" : "{0:0.#} {1}", v, units[i]);
    }
}
