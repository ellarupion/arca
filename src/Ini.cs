// arca.ini: one [section] per module. Keeps key order, re-read when the file changes on disk.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

static class Ini
{
    public static readonly string File = Path.ChangeExtension(Application.ExecutablePath, ".ini");

    static readonly List<string> sections = new List<string>();
    static readonly Dictionary<string, List<string>> keys = new Dictionary<string, List<string>>();
    static readonly Dictionary<string, string> values = new Dictionary<string, string>();
    static DateTime stamp;

    // Registers a setting with its default value; the file overrides it.
    public static void Default(string section, string key, string value)
    {
        if (!keys.ContainsKey(section)) { sections.Add(section); keys[section] = new List<string>(); }
        if (keys[section].Contains(key)) return;
        keys[section].Add(key);
        values[section + "." + key] = value;
    }

    public static string Get(string section, string key)
    {
        string v;
        return values.TryGetValue(section + "." + key, out v) ? v : "";
    }

    public static bool GetBool(string section, string key)
    {
        string v = Get(section, key).ToLowerInvariant();
        return v == "1" || v == "true" || v == "on" || v == "yes";
    }

    public static double GetDouble(string section, string key, double fallback)
    {
        double d;
        return double.TryParse(Get(section, key).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : fallback;
    }

    public static TimeSpan GetTime(string section, string key, TimeSpan fallback)
    {
        TimeSpan t;
        return TimeSpan.TryParse(Get(section, key), out t) ? t : fallback;
    }

    public static void Set(string section, string key, string value)
    {
        Default(section, key, value);
        values[section + "." + key] = value;
        Save();
    }

    public static void SetBool(string section, string key, bool value)
    {
        Set(section, key, value ? "1" : "0");
    }

    // True when the file was (re)read. Missing keys are written back so the file always lists every setting.
    public static bool Reload()
    {
        if (!System.IO.File.Exists(File)) { Save(); return true; }
        var t = System.IO.File.GetLastWriteTimeUtc(File);
        if (t == stamp) return false;
        stamp = t;

        var seen = new HashSet<string>();
        string section = "";
        try
        {
            foreach (var raw in System.IO.File.ReadAllLines(File))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[' && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant(); continue; }
                var kv = line.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                string id = section + "." + kv[0].Trim().ToLowerInvariant();
                string value = kv[1];
                int comment = value.IndexOf(" ;");
                if (comment < 0) comment = value.IndexOf("\t;");
                if (comment >= 0) value = value.Substring(0, comment); // "auto=1   ; note"
                values[id] = value.Trim();
                seen.Add(id);
            }
        }
        catch (IOException) { stamp = default(DateTime); return false; }

        foreach (var s in sections)
            foreach (var k in keys[s])
                if (!seen.Contains(s + "." + k)) { Save(); return true; }
        return true;
    }

    public static void Save()
    {
        var sb = new StringBuilder();
        foreach (var s in sections)
        {
            if (sb.Length > 0) sb.Append("\r\n");
            sb.Append('[').Append(s).Append("]\r\n");
            foreach (var k in keys[s]) sb.Append(k).Append('=').Append(Get(s, k)).Append("\r\n");
        }
        try
        {
            System.IO.File.WriteAllText(File, sb.ToString());
            stamp = System.IO.File.GetLastWriteTimeUtc(File);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
