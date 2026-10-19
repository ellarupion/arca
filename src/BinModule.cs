// The Recycle Bin in the tray: open, empty, auto-empty over a size limit (formerly vacua).
using System;
using System.Diagnostics;
using System.Windows.Forms;

class BinModule : Module
{
    public override string Id { get { return "bin"; } }
    public override string Title { get { return "Recycle Bin"; } }

    const uint NoConfirmation = 1, NoProgressUI = 2, NoSound = 4;

    NotifyIcon tray;
    ToolStripMenuItem emptyItem, doubleItem, confirmItem, autoItem;
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
        var menu = new ContextMenuStrip();
        emptyItem = Shell.Bold(Shell.Item("Empty Recycle Bin", delegate { Empty(!Ini.GetBool(Id, "confirm"), 0); }));
        doubleItem = Shell.Item("Open on double-click", delegate { Ini.SetBool(Id, "doubleclick", !Ini.GetBool(Id, "doubleclick")); Refresh(); });
        confirmItem = Shell.Item("Ask before emptying", delegate { Ini.SetBool(Id, "confirm", !Ini.GetBool(Id, "confirm")); Refresh(); });
        autoItem = Shell.Item("", delegate { Ini.SetBool(Id, "auto", !Ini.GetBool(Id, "auto")); Tick(); });
        menu.Items.Add(emptyItem);
        menu.Items.Add(Shell.Item("Open Recycle Bin", delegate { Open(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(doubleItem);
        menu.Items.Add(confirmItem);
        menu.Items.Add(autoItem);
        menu.Items.Add(Shell.Item("Edit settings…", delegate { Shell.OpenSettings(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(TurnOffItem());
        menu.Opening += delegate { Query(); Refresh(); };

        tray = new NotifyIcon { ContextMenuStrip = menu };
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left && !Ini.GetBool(Id, "doubleclick")) Open(); };
        tray.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left && Ini.GetBool(Id, "doubleclick")) Open(); };
        shownFull = null;
        Tick();
        tray.Visible = true;
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
        tray.Text = full ? string.Format("Recycle Bin — {0} item{1}, {2}", items, items == 1 ? "" : "s", FormatSize(size)) : "Recycle Bin — empty";
        emptyItem.Enabled = full;
        doubleItem.Checked = Ini.GetBool(Id, "doubleclick");
        confirmItem.Checked = Ini.GetBool(Id, "confirm");
        autoItem.Text = string.Format("Auto-empty over {0:0.##} GB", Ini.GetDouble(Id, "limit_gb", 10));
        autoItem.Checked = Ini.GetBool(Id, "auto");
    }

    static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return string.Format(i == 0 ? "{0:0} {1}" : "{0:0.#} {1}", v, units[i]);
    }
}
