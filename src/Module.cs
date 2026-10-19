// A tool inside arca. Each one can be switched on and off; its settings live in its own ini section
// and are described here so the arca window can show them.
using System.Collections.Generic;
using System.Windows.Forms;

enum SettingKind { Bool, Time, Number, Hotkey, Choice }

class Setting
{
    public readonly string Key, Label, Hint;
    public readonly SettingKind Kind;
    public readonly string[] Values, Labels; // Choice only: what goes into the ini, and what the window shows

    // Label and hint are English; the window translates them.
    public Setting(string key, SettingKind kind, string label, string hint)
        : this(key, kind, label, hint, null, null) { }

    public Setting(string key, SettingKind kind, string label, string hint, string[] values, string[] labels)
    {
        Key = key;
        Kind = kind;
        Label = label;
        Hint = hint;
        Values = values;
        Labels = labels;
    }
}

abstract class Module
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    // A character from Segoe Fluent Icons.
    public abstract string Glyph { get; }

    public virtual IList<Setting> Settings { get { return new Setting[0]; } }

    // Whether a fresh arca.ini turns this tool on.
    public virtual bool DefaultOn { get { return true; } }

    // Short "how to use" lines, already translated.
    public virtual IList<string> Usage { get { return new string[0]; } }

    public bool Running { get; private set; }

    public void Start()
    {
        if (Running) return;
        Running = true;
        OnStart();
    }

    public void Stop()
    {
        if (!Running) return;
        Running = false;
        OnStop();
    }

    protected abstract void OnStart();
    protected abstract void OnStop();

    // Every few seconds, after arca.ini has been re-read, and right after a setting changes in the window.
    public virtual void Tick() { }

    // The taskbar switched light/dark.
    public virtual void ThemeChanged() { }

    // Extra items for the tray menu while the module is running.
    public virtual void AddMenu(ToolStripItemCollection items) { }

    // The window is recording a shortcut: let go of global shortcuts so the keys reach it.
    public virtual void SuspendShortcuts(bool suspend) { }

    protected ToolStripMenuItem TurnOffItem()
    {
        return Shell.Item(L.T("Turn off"), delegate { Arca.SetEnabled(this, false); });
    }
}
