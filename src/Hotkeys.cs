// Global shortcuts written as text, like "Ctrl+Win+T".
using System;
using System.Collections.Generic;
using System.Windows.Forms;

class Hotkeys : NativeWindow, IDisposable
{
    readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
    int nextId = 1;

    public Hotkeys()
    {
        CreateHandle(new CreateParams());
    }

    // False when the text can't be parsed or another app already owns the shortcut. An empty string just means "off".
    public bool Add(string spec, Action action)
    {
        if (spec.Trim().Length == 0) return true;
        uint mods, vk;
        if (!Parse(spec, out mods, out vk)) return false;
        int id = nextId++;
        if (!Native.RegisterHotKey(Handle, id, mods | 0x4000, vk)) return false;
        actions[id] = action;
        return true;
    }

    public void Clear()
    {
        foreach (int id in actions.Keys) Native.UnregisterHotKey(Handle, id);
        actions.Clear();
    }

    public void Dispose()
    {
        Clear();
        DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        Action a;
        if (m.Msg == Native.WM_HOTKEY && actions.TryGetValue((int)m.WParam, out a)) a();
        base.WndProc(ref m);
    }

    static bool Parse(string spec, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        foreach (var raw in spec.Split('+'))
        {
            string p = raw.Trim().ToLowerInvariant();
            if (p == "ctrl" || p == "control") mods |= 2;
            else if (p == "alt") mods |= 1;
            else if (p == "shift") mods |= 4;
            else if (p == "win") mods |= 8;
            else
            {
                if (p == "pgup") p = "pageup";
                else if (p == "pgdn") p = "pagedown";
                else if (p.Length == 1 && char.IsDigit(p[0])) p = "d" + p;
                Keys k;
                if (vk != 0 || !Enum.TryParse(p, true, out k)) return false;
                vk = (uint)k;
            }
        }
        return vk != 0;
    }
}
