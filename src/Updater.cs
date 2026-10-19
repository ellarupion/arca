// Updates from GitHub Releases: checks the latest tag, swaps arca.exe in place and restarts.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

static class Updater
{
    public const string Repo = "ellarupion/arca";
    static readonly string Exe = Application.ExecutablePath;

    public static string Available { get; private set; }
    static string downloadUrl;

    public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

    public static string CurrentText
    {
        get { var v = Current; return v.Major + "." + v.Minor + "." + v.Build; }
    }

    // Leftover from the previous update: the old exe can only be deleted once it has exited.
    public static void Cleanup()
    {
        try { File.Delete(Exe + ".old"); } catch (Exception) { }
        try { File.Delete(Exe + ".new"); } catch (Exception) { }
    }

    public static void Check(bool manual)
    {
        var ui = SynchronizationContext.Current;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string tag = null, url = null, error = null;
            try
            {
                string json;
                using (var wc = Client()) json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
                var t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
                var u = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/arca\\.exe)\"");
                if (t.Success) tag = t.Groups[1].Value;
                if (u.Success) url = u.Groups[1].Value;
            }
            catch (Exception e) { error = e.Message; }
            ui.Post(delegate { Done(tag, url, error, manual); }, null);
        });
    }

    static void Done(string tag, string url, string error, bool manual)
    {
        if (error != null)
        {
            if (manual) Arca.Notify(L.F("Couldn't check for updates: {0}", error));
            return;
        }
        Version v;
        if (tag != null && url != null && Version.TryParse(tag.Split('-')[0], out v) && Pad(v) > Current)
        {
            bool fresh = Available != tag;
            Available = tag;
            downloadUrl = url;
            if (fresh || manual) Arca.Notify(L.F("arca {0} is available. Click here to update.", tag), Install);
        }
        else if (manual) Arca.Notify(L.F("arca is up to date (v{0}).", CurrentText));
    }

    static Version Pad(Version v)
    {
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
    }

    public static void Install()
    {
        if (downloadUrl == null) return;
        string fresh = Exe + ".new", old = Exe + ".old";
        try
        {
            using (var wc = Client()) wc.DownloadFile(downloadUrl, fresh);
            if (File.Exists(old)) File.Delete(old);
            File.Move(Exe, old); // a running exe can be renamed, just not overwritten
            File.Move(fresh, Exe);
        }
        catch (Exception e)
        {
            try { if (!File.Exists(Exe) && File.Exists(old)) File.Move(old, Exe); } catch (Exception) { }
            Arca.Notify(L.F("Update failed: {0}", e.Message));
            return;
        }
        Process.Start(Exe, "--updated");
        Arca.Exit();
    }

    static WebClient Client()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        var wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = "arca/" + CurrentText;
        return wc;
    }
}
