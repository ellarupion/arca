// A tool inside arca. Each one can be switched on and off from the arca menu; its settings live in its own ini section.
using System.Windows.Forms;

abstract class Module
{
    public abstract string Id { get; }
    public abstract string Title { get; }
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

    // Every few seconds, after arca.ini has been re-read.
    public virtual void Tick() { }

    // The taskbar switched light/dark.
    public virtual void ThemeChanged() { }

    // Extra items for the main arca menu while the module is running.
    public virtual void AddMenu(ToolStripItemCollection items) { }

    protected ToolStripMenuItem TurnOffItem()
    {
        return Shell.Item(L.T("Turn off"),delegate { Arca.SetEnabled(this, false); });
    }
}
