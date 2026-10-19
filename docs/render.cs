// Renders the README banner and shortcut sheet, using arca's own icon drawing and translations (loaded by reflection).
// Window screenshots are taken from the running app.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class R
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly A;
    static string Out;
    static bool ru;

    static readonly Color Bg1 = Color.FromArgb(24, 24, 32), Bg2 = Color.FromArgb(44, 40, 62);
    static readonly Color Accent = Color.FromArgb(150, 130, 255);

    [STAThread]
    static void Main(string[] args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        A = Assembly.LoadFrom(args[0]);
        Out = args[1];
        Directory.CreateDirectory(Out);

        foreach (var lang in new[] { "en", "ru" })
        {
            ru = lang == "ru";
            A.GetType("L").GetProperty("Ru").GetSetMethod(true).Invoke(null, new object[] { ru });
            Save(Banner(), "banner-" + lang);
            Save(Shortcuts(), "shortcuts-" + lang);
        }
    }

    static void Save(Bitmap b, string name)
    {
        b.Save(Path.Combine(Out, name + ".png"), ImageFormat.Png);
        b.Dispose();
    }

    static string T(string en) { return (string)A.GetType("L").GetMethod("T").Invoke(null, new object[] { en }); }

    // ---- drawing helpers

    static void Hq(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    static Bitmap Canvas(int w, int h)
    {
        var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            Hq(g);
            using (var path = Round(new RectangleF(0, 0, w, h), 28))
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, h), Bg1, Bg2, 35f))
                g.FillPath(b, path);
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(w * 0.55f, -h * 0.6f, w * 0.9f, h * 1.2f);
                using (var pb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(60, Accent), SurroundColors = new[] { Color.FromArgb(0, Accent) } })
                using (var clip = Round(new RectangleF(0, 0, w, h), 28))
                {
                    g.SetClip(clip);
                    g.FillPath(pb, glow);
                    g.ResetClip();
                }
            }
        }
        return bmp;
    }

    static void Card(Graphics g, Bitmap img, int x, int y)
    {
        for (int i = 14; i >= 1; i--)
            using (var b = new SolidBrush(Color.FromArgb(9, 0, 0, 0)))
            using (var p = Round(new RectangleF(x - i + 2, y - i + 8, img.Width + 2 * i - 4, img.Height + 2 * i - 4), 8 + i))
                g.FillPath(b, p);
        using (var p = Round(new RectangleF(x, y, img.Width, img.Height), 8))
        {
            g.SetClip(p);
            g.DrawImage(img, x, y, img.Width, img.Height);
            g.ResetClip();
        }
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Bitmap Glyph(int s, string glyph, bool dot, Color ink)
    {
        return (Bitmap)A.GetType("Shell").GetMethod("Glyph", F).Invoke(null, new object[] { s, glyph, dot, ink });
    }

    static Bitmap HalfMoon(int s, Color c)
    {
        var bmp = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pad = s / 8f, d = s - 2 * pad - 1;
            using (var pen = new Pen(c, Math.Max(1f, s / 12f))) g.DrawEllipse(pen, pad, pad, d, d);
            using (var brush = new SolidBrush(c)) g.FillPie(brush, pad, pad, d, d, 90, 180);
        }
        return bmp;
    }

    static int TrayWidth() { return 12 + 3 * 52 + 12 + 92; }

    // A taskbar-like strip: arca, theme and bin icons plus a clock. One icon can be highlighted.
    static void Tray(Graphics g, int x, int y, int active)
    {
        int w = TrayWidth(), h = 64;
        using (var p = Round(new RectangleF(x, y, w, h), 14))
        using (var b = new SolidBrush(Color.FromArgb(32, 32, 36)))
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255)))
        {
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }
        var icons = new[] { Glyph(32, "", false, Color.White), HalfMoon(32, Color.White), Glyph(32, "", true, Color.White) };
        for (int i = 0; i < icons.Length; i++)
        {
            int ix = x + 12 + i * 52;
            if (i == active)
                using (var hp = Round(new RectangleF(ix, y + 8, 48, 48), 8))
                using (var hb = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillPath(hb, hp);
            g.DrawImage(icons[i], ix + 8, y + 16, 32, 32);
            icons[i].Dispose();
        }
        using (var f = new Font("Segoe UI", 15f, GraphicsUnit.Pixel))
        using (var fmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            g.DrawString("19:04\n" + (ru ? "19.10.2026" : "10/19/2026"), f, Brushes.White, new RectangleF(x, y, w - 14, h), fmt);
    }

    // ---- banner

    static Bitmap Banner()
    {
        int w = 1600, h = 520;
        var bmp = Canvas(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            Hq(g);
            int s = 220, ix = 130, iy = (h - s) / 2;
            for (int i = 24; i >= 1; i--)
                using (var b = new SolidBrush(Color.FromArgb(6, Accent)))
                using (var p = Round(new RectangleF(ix - i, iy - i + 10, s + 2 * i, s + 2 * i), 48 + i))
                    g.FillPath(b, p);
            using (var p = Round(new RectangleF(ix, iy, s, s), 48))
            using (var b = new LinearGradientBrush(new Rectangle(ix, iy, s, s), Color.FromArgb(52, 52, 60), Color.FromArgb(30, 30, 36), 90f))
            using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255), 2))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            using (var box = Glyph(140, "", false, Color.White)) g.DrawImage(box, ix + 40, iy + 40, 140, 140);

            int tx = ix + s + 80;
            using (var f = new Font("Segoe UI Semibold", 132f, GraphicsUnit.Pixel))
                g.DrawString("arca", f, Brushes.White, tx - 10, 92);
            using (var f = new Font("Segoe UI", 38f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(200, 200, 215)))
                g.DrawString(ru ? "Ларец маленьких утилит для Windows" : "A box of tiny Windows tools", f, b, tx, 268);

            string[] tags = ru
                ? new[] { "тема", "корзина", "окна", "≈130 КБ", "без установки" }
                : new[] { "theme", "recycle bin", "windows", "≈130 KB", "no installer" };
            float cx = tx;
            using (var f = new Font("Segoe UI Semibold", 24f, GraphicsUnit.Pixel))
                foreach (var t in tags)
                {
                    var sz = g.MeasureString(t, f);
                    var r = new RectangleF(cx, 352, sz.Width + 28, 48);
                    using (var p = Round(r, 24))
                    using (var b = new SolidBrush(Color.FromArgb(36, Accent)))
                    using (var pen = new Pen(Color.FromArgb(110, Accent), 1.5f))
                    {
                        g.FillPath(b, p);
                        g.DrawPath(pen, p);
                    }
                    using (var tb = new SolidBrush(Color.FromArgb(225, 220, 255)))
                        g.DrawString(t, f, tb, cx + 14, 352 + (48 - sz.Height) / 2);
                    cx += r.Width + 14;
                }
            Tray(g, w - 110 - TrayWidth(), 344, -1);
        }
        return bmp;
    }

    // ---- shortcut sheet

    static Bitmap Shortcuts()
    {
        var rows = new[]
        {
            new[] { "Ctrl", "Win", "T", T("Keep on top") },
            new[] { "Ctrl", "Win", "H", T("Hide to tray") },
            new[] { "Ctrl", "Win", "PgDn", T("More transparent") },
            new[] { "Ctrl", "Win", "PgUp", T("Less transparent") },
            new[] { "Alt", "🖱", null, T("Move windows with Alt + drag") },
        };
        int w = 1100, rowH = 84, pad = 56;
        int h = pad * 2 + rows.Length * rowH - 20;
        var bmp = Canvas(w, h);
        using (var g = Graphics.FromImage(bmp))
        using (var keyFont = new Font("Segoe UI Semibold", 24f, GraphicsUnit.Pixel))
        using (var plusFont = new Font("Segoe UI", 26f, GraphicsUnit.Pixel))
        using (var textFont = new Font("Segoe UI", 28f, GraphicsUnit.Pixel))
        using (var plus = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
        using (var text = new SolidBrush(Color.FromArgb(235, 235, 245)))
        {
            Hq(g);
            for (int i = 0; i < rows.Length; i++)
            {
                int y = pad + i * rowH;
                float x = pad;
                for (int k = 0; k < 3; k++)
                {
                    string key = rows[i][k];
                    if (key == null) continue;
                    if (k > 0) { g.DrawString("+", plusFont, plus, x + 4, y + 10); x += 32; }
                    x += Key(g, key == "🖱" ? (ru ? "перетащить" : "drag") : key, keyFont, x, y) + 6;
                }
                g.DrawString(rows[i][3], textFont, text, 470, y + 10);
            }
        }
        return bmp;
    }

    static float Key(Graphics g, string label, Font f, float x, float y)
    {
        var sz = g.MeasureString(label, f);
        float kw = Math.Max(64, sz.Width + 30), kh = 60;
        using (var p = Round(new RectangleF(x, y + 4, kw, kh), 12))
        using (var b = new SolidBrush(Color.FromArgb(150, 150, 165)))
            g.FillPath(b, p);
        using (var p = Round(new RectangleF(x, y, kw, kh - 2), 12))
        using (var b = new LinearGradientBrush(new RectangleF(x, y, kw, kh), Color.FromArgb(252, 252, 255), Color.FromArgb(226, 226, 236), 90f))
            g.FillPath(b, p);
        using (var tb = new SolidBrush(Color.FromArgb(40, 40, 52)))
            g.DrawString(label, f, tb, x + (kw - sz.Width) / 2, y + (kh - 2 - sz.Height) / 2);
        return kw;
    }
}
