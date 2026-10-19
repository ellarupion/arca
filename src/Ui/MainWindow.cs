// The arca window: a sidebar, the tool catalog, a page per tool with its settings, app settings and about.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

class MainWindow : Window
{
    enum Page { Tools, Tool, Settings, About }

    const string RepoUrl = "https://github.com/" + Updater.Repo;

    readonly Grid root = new Grid();
    readonly ScrollViewer scroller;
    readonly StackPanel content = new StackPanel();
    UniformGrid cards;
    Page page = Page.Tools;
    Module tool;
    bool quitting;

    public MainWindow()
    {
        Title = "arca";
        Width = 1040;
        Height = 720;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = (FontFamily)FindResource("Ui");
        FontSize = 14;
        UseLayoutRounding = true;
        Icon = Ui.AppIcon(32);
        SetResourceReference(BackgroundProperty, "Bg");
        SetResourceReference(ForegroundProperty, "Text");

        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(256) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        content.Margin = new Thickness(40, 32, 40, 40);
        scroller = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FocusVisualStyle = null,
        };
        scroller.SizeChanged += (s, e) => { if (cards != null) cards.Columns = Columns(); };
        Content = root;

        SourceInitialized += delegate { ApplyChrome(); };
        Closing += (s, e) => { if (!quitting) { e.Cancel = true; Hide(); } };
        Arca.Changed += OnChanged;
        PreviewMouseDown += (s, e) =>
        {
            // Clicking outside a field ends editing, so it saves and shortcut recording stops.
            if (Keyboard.FocusedElement is TextBox && !InsideTextBox(e.OriginalSource as DependencyObject)) Keyboard.ClearFocus();
        };
    }

    static bool InsideTextBox(DependencyObject d)
    {
        while (d != null)
        {
            if (d is TextBox) return true;
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    public void Bring()
    {
        Render();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void ApplyChrome()
    {
        Ui.Chrome(this);
    }

    public void Quit()
    {
        quitting = true;
        Arca.Changed -= OnChanged;
        Close();
    }

    // Redraw after changes made outside the window (tray menu, ini file, update check), unless the user is typing.
    void OnChanged()
    {
        Dispatcher.BeginInvoke(new Action(delegate
        {
            if (!IsVisible || Keyboard.FocusedElement is TextBox) return;
            double offset = scroller.VerticalOffset;
            Render();
            scroller.ScrollToVerticalOffset(offset);
        }));
    }

    void Go(Page p, Module m)
    {
        page = p;
        tool = m;
        Render();
        scroller.ScrollToTop();
    }

    void Render()
    {
        root.Children.Clear();
        root.Children.Add(Sidebar());

        content.Children.Clear();
        cards = null;
        if (page == Page.Tool && tool != null) ToolPage(tool);
        else if (page == Page.Settings) SettingsPage();
        else if (page == Page.About) AboutPage();
        else ToolsPage();

        var host = scroller.Parent as Border;
        if (host != null) host.Child = null;
        var layer = new Border { CornerRadius = new CornerRadius(8, 0, 0, 0), BorderThickness = new Thickness(1, 1, 0, 0), Child = scroller };
        layer.SetResourceReference(Border.BackgroundProperty, "Layer");
        layer.SetResourceReference(Border.BorderBrushProperty, "LayerStroke");
        Grid.SetColumn(layer, 1);
        root.Children.Add(layer);
    }

    // ---- sidebar

    UIElement Sidebar()
    {
        var dock = new DockPanel { Margin = new Thickness(12, 14, 12, 14), LastChildFill = true };

        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 2, 0, 20) };
        brand.Children.Add(Tile("", 34, 17));
        var name = Text("arca", 20, "Text", FontWeights.SemiBold);
        name.FontFamily = (FontFamily)FindResource("Display");
        name.Margin = new Thickness(12, 0, 0, 2);
        name.VerticalAlignment = VerticalAlignment.Center;
        brand.Children.Add(name);
        DockPanel.SetDock(brand, Dock.Top);
        dock.Children.Add(brand);

        var footer = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
        if (Updater.Available != null)
        {
            var box = new StackPanel();
            box.Children.Add(Text(L.T("Update available"), 13, "Text", FontWeights.SemiBold));
            var v = Text("v" + Updater.Available, 12, "Text2");
            v.Margin = new Thickness(0, 2, 0, 10);
            box.Children.Add(v);
            box.Children.Add(Button(L.T("Update now"), true, delegate { Updater.Install(); }));
            footer.Children.Add(CardBorder(box, new Thickness(14)));
        }
        else
        {
            var v = Text("arca v" + Updater.CurrentText, 12, "Text3");
            v.Margin = new Thickness(10, 0, 0, 4);
            footer.Children.Add(v);
        }
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(footer);

        var nav = new StackPanel();
        nav.Children.Add(Nav("", L.T("Tools"), page == Page.Tools || page == Page.Tool, delegate { Go(Page.Tools, null); }));
        nav.Children.Add(Nav("", L.T("Settings"), page == Page.Settings, delegate { Go(Page.Settings, null); }));
        nav.Children.Add(Nav("", L.T("About"), page == Page.About, delegate { Go(Page.About, null); }));
        dock.Children.Add(nav);
        return dock;
    }

    RadioButton Nav(string glyph, string text, bool selected, Action click)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Glyph(glyph, 16, "Text");
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);
        var label = Text(text, 14, "Text");
        label.Margin = new Thickness(14, 0, 0, 1);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        var r = new RadioButton { Style = S("Nav"), Content = row, GroupName = "nav", IsChecked = selected };
        AutomationProperties.SetName(r, text);
        // Checked covers mouse, keyboard and accessibility tools; Click covers clicking the page you're already in.
        bool handled = false;
        r.Checked += delegate { handled = true; click(); };
        r.Click += delegate { if (!handled) click(); handled = false; };
        return r;
    }

    // ---- pages

    void ToolsPage()
    {
        Header(L.T("Tools"), L.T("Turn on what you need. Each tool lives in the tray next to the clock."));
        cards = new UniformGrid { Columns = Columns(), Margin = new Thickness(-6, 0, -6, 0) };
        foreach (var m in Arca.Modules) cards.Children.Add(ToolCard(m));
        content.Children.Add(cards);

        var note = new DockPanel();
        var i = Glyph("", 16, "Text2");
        i.Margin = new Thickness(0, 1, 12, 0);
        i.VerticalAlignment = VerticalAlignment.Top;
        note.Children.Add(i);
        note.Children.Add(Wrap(L.T("Closing this window keeps arca running in the tray. To quit, use Exit in the tray menu or Quit in Settings."), 13, "Text2"));
        var card = CardBorder(note, new Thickness(16, 14, 16, 14));
        card.Margin = new Thickness(0, 18, 0, 0);
        content.Children.Add(card);
    }

    int Columns()
    {
        double w = scroller.ActualWidth - 80;
        return w >= 860 ? 3 : w >= 500 ? 2 : 1;
    }

    UIElement ToolCard(Module m)
    {
        var g = new Grid();
        for (int r = 0; r < 4; r++)
            g.RowDefinitions.Add(new RowDefinition { Height = r == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

        var top = new DockPanel();
        var toggle = Toggle(m.Running, on => Arca.SetEnabled(m, on));
        AutomationProperties.SetName(toggle, m.Title);
        DockPanel.SetDock(toggle, Dock.Right);
        toggle.VerticalAlignment = VerticalAlignment.Top;
        top.Children.Add(toggle);
        var tile = Tile(m.Glyph, 48, 22);
        tile.HorizontalAlignment = HorizontalAlignment.Left;
        top.Children.Add(tile);
        g.Children.Add(top);

        var title = Text(m.Title, 16, "Text", FontWeights.SemiBold);
        title.Margin = new Thickness(0, 16, 0, 0);
        Grid.SetRow(title, 1);
        g.Children.Add(title);

        var desc = Wrap(m.Description, 13, "Text2");
        desc.Margin = new Thickness(0, 6, 0, 0);
        desc.MinHeight = 54;
        Grid.SetRow(desc, 2);
        g.Children.Add(desc);

        var foot = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var chevron = Glyph("", 12, "Text3");
        DockPanel.SetDock(chevron, Dock.Right);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        foot.Children.Add(chevron);
        var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 1, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, m.Running ? "Accent" : "Text3");
        foot.Children.Add(dot);
        foot.Children.Add(Text(m.Running ? L.T("On — in the tray") : L.T("Off"), 13, "Text2"));
        Grid.SetRow(foot, 3);
        g.Children.Add(foot);

        var card = new Button { Style = S("CardButton"), Content = g, Margin = new Thickness(6, 0, 6, 12) };
        AutomationProperties.SetName(card, m.Title);
        card.Click += delegate { Go(Page.Tool, m); };
        return card;
    }

    void ToolPage(Module m)
    {
        var crumb = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        var back = new Button { Style = S("Ghost"), Content = Display(L.T("Tools"), null) };
        back.Click += delegate { Go(Page.Tools, null); };
        crumb.Children.Add(back);
        var sep = Glyph("", 14, "Text3");
        sep.Margin = new Thickness(14, 6, 14, 0);
        sep.VerticalAlignment = VerticalAlignment.Center;
        crumb.Children.Add(sep);
        crumb.Children.Add(Display(m.Title, "Text"));
        content.Children.Add(crumb);

        var hero = new DockPanel();
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
        var state = Text(m.Running ? L.T("On") : L.T("Off"), 14, "Text2");
        state.Margin = new Thickness(0, 0, 12, 1);
        state.VerticalAlignment = VerticalAlignment.Center;
        right.Children.Add(state);
        right.Children.Add(Toggle(m.Running, on => Arca.SetEnabled(m, on)));
        DockPanel.SetDock(right, Dock.Right);
        hero.Children.Add(right);
        var tile = Tile(m.Glyph, 56, 26);
        tile.Margin = new Thickness(0, 0, 20, 0);
        hero.Children.Add(tile);
        var desc = Wrap(m.Description, 14, "Text2");
        desc.VerticalAlignment = VerticalAlignment.Center;
        hero.Children.Add(desc);
        content.Children.Add(CardBorder(hero, new Thickness(20)));

        if (m.Settings.Count > 0)
        {
            Section(L.T("Settings"));
            foreach (var s in m.Settings) content.Children.Add(Row(L.T(s.Label), L.T(s.Hint), Control(m, s)));
        }

        if (m.Usage.Count > 0)
        {
            Section(L.T("How to use"));
            var list = new StackPanel();
            foreach (var line in m.Usage)
            {
                var item = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                var bullet = Glyph("", 12, "Accent");
                bullet.Margin = new Thickness(0, 4, 12, 0);
                bullet.VerticalAlignment = VerticalAlignment.Top;
                item.Children.Add(bullet);
                item.Children.Add(Wrap(line, 14, "Text"));
                list.Children.Add(item);
            }
            content.Children.Add(CardBorder(list, new Thickness(20, 14, 20, 14)));
        }
    }

    void SettingsPage()
    {
        Header(L.T("Settings"), null);

        Section(L.T("General"));
        var lang = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var o in new[] { new[] { "auto", L.T("Same as Windows") }, new[] { "en", "English" }, new[] { "ru", "Русский" } })
        {
            string value = o[0];
            var r = new RadioButton { Style = S("Segment"), Content = o[1], GroupName = "lang", IsChecked = Arca.Language == value };
            r.Checked += delegate { Arca.SetLanguage(value); };
            lang.Children.Add(r);
        }
        var langBox = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Padding = new Thickness(1), Child = lang };
        langBox.SetResourceReference(Border.BackgroundProperty, "Ctrl");
        langBox.SetResourceReference(Border.BorderBrushProperty, "CtrlStroke");
        content.Children.Add(Row(L.T("Language"), L.T("arca follows the Windows language unless you pick one"), langBox));
        content.Children.Add(Row(L.T("Start with Windows"), L.T("arca starts quietly in the tray"),
            Toggle(Shell.InStartup(), on => { Shell.SetStartup(on); Render(); })));

        Section(L.T("Updates"));
        content.Children.Add(Row(L.T("Check for updates automatically"), L.T("At startup and every 12 hours"),
            Toggle(Ini.GetBool("arca", "updates"), on => Ini.SetBool("arca", "updates", on))));
        string status;
        switch (Updater.Status)
        {
            case Updater.State.Checking: status = L.T("Checking for updates…"); break;
            case Updater.State.UpToDate: status = L.T("You're up to date"); break;
            case Updater.State.Available: status = L.F("Version {0} is available", Updater.Available); break;
            case Updater.State.Failed: status = L.F("Couldn't check: {0}", Updater.Error); break;
            default: status = L.T("Not checked yet"); break;
        }
        UIElement action;
        if (Updater.Available != null) action = Button(L.T("Update now"), true, delegate { Updater.Install(); });
        else
        {
            var b = Button(L.T("Check now"), false, delegate { Updater.Check(false); });
            b.IsEnabled = Updater.Status != Updater.State.Checking;
            action = b;
        }
        content.Children.Add(Row("arca v" + Updater.CurrentText, status, action));

        Section(L.T("Advanced"));
        content.Children.Add(Row(L.T("Settings file"), Ini.File, Button(L.T("Open"), false, delegate { Shell.OpenSettings(); })));
        content.Children.Add(Row(L.T("Quit arca"), L.T("Stops every tool and puts all windows back to normal"),
            Button(L.T("Quit"), false, delegate { Arca.Exit(); })));
    }

    void AboutPage()
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 24) };
        head.Children.Add(new Image { Source = Ui.AppIcon(256), Width = 96, Height = 96 });
        var titles = new StackPanel { Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var name = Text("arca", 40, "Text", FontWeights.SemiBold);
        name.FontFamily = (FontFamily)FindResource("Display");
        titles.Children.Add(name);
        titles.Children.Add(Text(L.T("A box of tiny Windows tools"), 16, "Text2"));
        var ver = Text(L.F("Version {0}", Updater.CurrentText), 13, "Text3");
        ver.Margin = new Thickness(0, 6, 0, 0);
        titles.Children.Add(ver);
        head.Children.Add(titles);
        content.Children.Add(head);

        var about = Wrap(L.T("arca keeps a few handy tools in one small app. Turn on the ones you need, and the rest stay out of the way. No installer, no background services, nothing sent anywhere except the update check."), 14, "Text2");
        about.MaxWidth = 640;
        about.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(about);

        var links = new WrapPanel { Margin = new Thickness(0, 24, 0, 0) };
        links.Children.Add(LinkButton("GitHub", RepoUrl));
        links.Children.Add(LinkButton(L.T("Report a problem"), RepoUrl + "/issues/new"));
        links.Children.Add(LinkButton(L.T("Releases"), RepoUrl + "/releases"));
        content.Children.Add(links);

        var legal = Text(L.T("MIT license · © 2026 ellarupion"), 12, "Text3");
        legal.Margin = new Thickness(0, 28, 0, 0);
        content.Children.Add(legal);
    }

    // ---- setting controls

    FrameworkElement Control(Module m, Setting s)
    {
        switch (s.Kind)
        {
            case SettingKind.Bool:
                return Toggle(Ini.GetBool(m.Id, s.Key), on => { Ini.SetBool(m.Id, s.Key, on); Arca.SettingChanged(m); });
            case SettingKind.Time:
                return Field(Ini.Get(m.Id, s.Key), 96, ParseTime, value => { Ini.Set(m.Id, s.Key, value); Arca.SettingChanged(m); });
            case SettingKind.Number:
                return Field(Ini.Get(m.Id, s.Key), 96, text =>
                {
                    double d;
                    if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d) || d < 0) return null;
                    return d.ToString("0.##", CultureInfo.InvariantCulture);
                }, value => { Ini.Set(m.Id, s.Key, value); Arca.SettingChanged(m); });
            default:
                return HotkeyBox(Ini.Get(m.Id, s.Key), value => { Ini.Set(m.Id, s.Key, value); Arca.SettingChanged(m); });
        }
    }

    // "7:30", "7.30", "730", "0730" and "7" all mean 07:30 or 07:00, so the colon (Shift+6 on a Russian keyboard) is optional.
    static string ParseTime(string text)
    {
        int h, m = 0;
        var parts = text.Split(':', '.', ',', '-', ' ');
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) return null;
        }
        else if (parts.Length == 1 && text.Length > 0 && text.Length <= 4 && int.TryParse(text, out h))
        {
            if (text.Length > 2) { m = h % 100; h /= 100; }
        }
        else return null;
        if (h < 0 || h > 23 || m < 0 || m > 59) return null;
        return h.ToString("00") + ":" + m.ToString("00");
    }

    // A text field that saves on Enter or when it loses focus; parse returns null for invalid input.
    TextBox Field(string value, double width, Func<string, string> parse, Action<string> save)
    {
        var box = new TextBox { Style = S("Field"), Text = value, Width = width, Height = 32, TextAlignment = TextAlignment.Center };
        string saved = value;
        Action commit = delegate
        {
            string v = parse(box.Text.Trim());
            if (v == null)
            {
                box.SetResourceReference(TextBox.BorderBrushProperty, "Danger");
                return;
            }
            box.SetResourceReference(TextBox.BorderBrushProperty, "CtrlStroke");
            box.Text = v;
            if (v != saved) { saved = v; save(v); }
        };
        box.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; commit(); Keyboard.ClearFocus(); }
            else if (e.Key == Key.Escape) { e.Handled = true; box.Text = saved; Keyboard.ClearFocus(); }
        };
        // Fires once when focus enters or leaves the field (Got/LostKeyboardFocus also bubble up from its insides).
        box.IsKeyboardFocusWithinChanged += (s, e) =>
        {
            if ((bool)e.NewValue) box.SelectAll(); // type a new value straight over the old one
            else commit();
        };
        return box;
    }

    // Click, then press the shortcut. Backspace clears it, Esc cancels.
    TextBox HotkeyBox(string value, Action<string> save)
    {
        string current = value;
        var box = new TextBox
        {
            Style = S("Field"),
            Width = 220,
            Height = 32,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = false,
            Cursor = Cursors.Hand,
            TextAlignment = TextAlignment.Center,
            Text = current.Length > 0 ? current : L.T("Off"),
        };
        box.IsKeyboardFocusWithinChanged += (s, e) =>
        {
            bool recording = (bool)e.NewValue;
            Arca.SuspendShortcuts(recording);
            box.Text = recording ? L.T("Press a shortcut…") : current.Length > 0 ? current : L.T("Off");
        };
        box.PreviewKeyDown += (s, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = Keyboard.Modifiers;
            if (key == Key.Tab && mods == ModifierKeys.None) return;
            e.Handled = true;
            if (mods == ModifierKeys.None && key == Key.Escape) { Keyboard.ClearFocus(); return; }
            if (mods == ModifierKeys.None && (key == Key.Back || key == Key.Delete))
            {
                current = "";
                save(current);
                Keyboard.ClearFocus();
                return;
            }
            string prefix = Modifiers(mods);
            if (IsModifier(key)) { box.Text = prefix.Length > 0 ? prefix + "…" : L.T("Press a shortcut…"); return; }
            if (mods == ModifierKeys.None) { box.Text = L.T("Add Ctrl, Alt, Shift or Win"); return; }
            current = prefix + KeyName(key);
            save(current);
            Keyboard.ClearFocus();
        };
        return box;
    }

    static bool IsModifier(Key k)
    {
        return k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt
            || k == Key.LeftShift || k == Key.RightShift || k == Key.LWin || k == Key.RWin;
    }

    static string Modifiers(ModifierKeys m)
    {
        string s = "";
        if ((m & ModifierKeys.Control) != 0) s += "Ctrl+";
        if ((m & ModifierKeys.Alt) != 0) s += "Alt+";
        if ((m & ModifierKeys.Shift) != 0) s += "Shift+";
        if ((m & ModifierKeys.Windows) != 0) s += "Win+";
        return s;
    }

    // Names that the shortcut parser understands.
    static string KeyName(Key key)
    {
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A)) return ((char)vk).ToString();
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
        switch (vk)
        {
            case 0x21: return "PageUp";
            case 0x22: return "PageDown";
            case 0x0D: return "Enter";
            case 0x20: return "Space";
            case 0x2C: return "PrintScreen";
        }
        return ((System.Windows.Forms.Keys)vk).ToString();
    }

    // ---- building blocks

    Style S(string key)
    {
        return (Style)FindResource(key);
    }

    void Header(string title, string subtitle)
    {
        var t = Display(title, "Text");
        t.Margin = new Thickness(0, 0, 0, subtitle == null ? 12 : 6);
        content.Children.Add(t);
        if (subtitle == null) return;
        var s = Wrap(subtitle, 14, "Text2");
        s.Margin = new Thickness(0, 0, 0, 24);
        content.Children.Add(s);
    }

    TextBlock Display(string text, string brush)
    {
        var t = new TextBlock { Text = text, FontSize = 28, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)FindResource("Display") };
        if (brush != null) t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return t;
    }

    void Section(string text)
    {
        var t = Text(text, 14, "Text", FontWeights.SemiBold);
        t.Margin = new Thickness(2, 28, 0, 8);
        content.Children.Add(t);
    }

    // A settings row: label and hint on the left, a control on the right.
    UIElement Row(string label, string hint, UIElement control)
    {
        var dock = new DockPanel();
        var fe = control as FrameworkElement;
        if (fe != null) { fe.VerticalAlignment = VerticalAlignment.Center; fe.Margin = new Thickness(24, 0, 0, 0); }
        DockPanel.SetDock(control, Dock.Right);
        dock.Children.Add(control);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Text(label, 14, "Text"));
        if (!string.IsNullOrEmpty(hint))
        {
            var h = Wrap(hint, 12, "Text2");
            h.Margin = new Thickness(0, 2, 0, 0);
            text.Children.Add(h);
        }
        dock.Children.Add(text);
        var card = CardBorder(dock, new Thickness(18, 14, 18, 14));
        card.MinHeight = 66;
        card.Margin = new Thickness(0, 0, 0, 4);
        return card;
    }

    Border CardBorder(UIElement child, Thickness padding)
    {
        var b = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = padding, Child = child };
        b.SetResourceReference(Border.BackgroundProperty, "Card");
        b.SetResourceReference(Border.BorderBrushProperty, "CardStroke");
        return b;
    }

    Border Tile(string glyph, double size, double glyphSize)
    {
        var g = Glyph(glyph, glyphSize, "Accent");
        g.HorizontalAlignment = HorizontalAlignment.Center;
        g.VerticalAlignment = VerticalAlignment.Center;
        var b = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 4.5), Child = g };
        b.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
        return b;
    }

    TextBlock Glyph(string glyph, double size, string brush)
    {
        var t = new TextBlock { Text = glyph, FontSize = size, FontFamily = (FontFamily)FindResource("Icons") };
        t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return t;
    }

    static TextBlock Text(string text, double size, string brush, FontWeight weight)
    {
        var t = new TextBlock { Text = text, FontSize = size, FontWeight = weight };
        t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return t;
    }

    static TextBlock Text(string text, double size, string brush)
    {
        return Text(text, size, brush, FontWeights.Normal);
    }

    static TextBlock Wrap(string text, double size, string brush)
    {
        var t = Text(text, size, brush);
        t.TextWrapping = TextWrapping.Wrap;
        return t;
    }

    CheckBox Toggle(bool on, Action<bool> changed)
    {
        // Checked/Unchecked rather than Click, so keyboard and accessibility tools work too.
        var c = new CheckBox { Style = S("Toggle"), IsChecked = on };
        c.Checked += delegate { changed(true); };
        c.Unchecked += delegate { changed(false); };
        return c;
    }

    Button Button(string text, bool accent, Action click)
    {
        var b = new Button { Style = S(accent ? "AccentBtn" : "Btn"), Content = text };
        b.Click += delegate { click(); };
        return b;
    }

    Button LinkButton(string text, string url)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = text });
        var g = Glyph("", 12, "Text2");
        g.Margin = new Thickness(10, 2, 0, 0);
        g.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(g);
        var b = new Button { Style = S("Btn"), Content = row, Margin = new Thickness(0, 0, 8, 8) };
        b.Click += delegate { Process.Start(url); };
        return b;
    }
}
