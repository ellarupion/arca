// English / Russian. Strings are written in English in the code; this maps them to Russian.
using System.Collections.Generic;
using System.Globalization;

static class L
{
    public static bool Ru { get; private set; }

    static readonly Dictionary<string, string> ru = new Dictionary<string, string>
    {
        // arca
        { "Tools", "Инструменты" },
        { "Settings…", "Настройки…" },
        { "Start with Windows", "Запускать вместе с Windows" },
        { "Check for updates automatically", "Проверять обновления автоматически" },
        { "Check for updates now", "Проверить обновления" },
        { "About arca v{0}", "О программе arca v{0}" },
        { "Exit", "Выход" },
        { "Language", "Язык" },
        { "Same as Windows", "Как в Windows" },
        { "Turn off", "Выключить" },
        { "Update to v{0}", "Обновить до v{0}" },
        { "Updated to v{0}.", "arca обновлена до v{0}." },
        { "arca {0} is available. Click here to update.", "Вышла arca {0}. Нажмите, чтобы обновить." },
        { "arca is up to date (v{0}).", "У вас последняя версия arca (v{0})." },
        { "Couldn't check for updates: {0}", "Не удалось проверить обновления: {0}" },
        { "Update failed: {0}", "Не удалось обновиться: {0}" },

        // theme
        { "Light/dark theme", "Светлая/тёмная тема" },
        { "Toggle theme", "Переключить тему" },
        { "Auto ({0:hh\\:mm} light, {1:hh\\:mm} dark)", "По расписанию (светлая с {0:hh\\:mm}, тёмная с {1:hh\\:mm})" },
        { "Edit schedule…", "Изменить расписание…" },
        { "Theme — light", "Тема — светлая" },
        { "Theme — dark", "Тема — тёмная" },

        // recycle bin
        { "Recycle Bin", "Корзина" },
        { "Empty Recycle Bin", "Очистить корзину" },
        { "Open Recycle Bin", "Открыть корзину" },
        { "Open on double-click", "Открывать двойным щелчком" },
        { "Ask before emptying", "Спрашивать перед очисткой" },
        { "Auto-empty over {0:0.##} GB", "Очищать автоматически, если больше {0:0.##} ГБ" },
        { "Edit settings…", "Изменить настройки…" },
        { "Recycle Bin — empty", "Корзина пуста" },
        { "Recycle Bin — {0}, {1}", "Корзина — {0}, {1}" },

        // windows
        { "Window tools", "Работа с окнами" },
        { "Keep on top", "Поверх всех окон" },
        { "Hide to tray", "Спрятать в трей" },
        { "More transparent", "Прозрачнее" },
        { "Less transparent", "Менее прозрачно" },
        { "off", "выкл." },
        { "Move windows with Alt + drag", "Перетаскивать окна с зажатым Alt" },
        { "Edit shortcuts…", "Изменить сочетания клавиш…" },
        { "Hidden — click to show", "Скрытые — нажмите, чтобы показать" },
        { "On top — click to release", "Поверх всех — нажмите, чтобы отпустить" },
        { "Reset all windows", "Вернуть все окна как было" },
        { "(untitled window)", "(окно без названия)" },
        { "Show", "Показать" },
        { "Close window", "Закрыть окно" },
        { "Shortcut taken or not recognised: {0}", "Сочетание занято другой программой или не распознано: {0}" },
        { "Can't change this window. Apps running as administrator need arca to run as administrator too.",
          "Не удалось изменить окно. Для программ, запущенных от администратора, arca тоже нужно запустить от администратора." },
    };

    // language=auto follows the Windows display language.
    public static void Load()
    {
        string v = Ini.Get("arca", "language").Trim().ToLowerInvariant();
        Ru = v == "ru" || (v != "en" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru");
    }

    public static string T(string en)
    {
        string s;
        return Ru && ru.TryGetValue(en, out s) ? s : en;
    }

    public static string F(string en, params object[] args)
    {
        return string.Format(T(en), args);
    }

    // "1 item", "3 объекта", "5 объектов".
    public static string Items(long n)
    {
        if (!Ru) return n + (n == 1 ? " item" : " items");
        long d = n % 10, h = n % 100;
        string w = d == 1 && h != 11 ? "объект" : d >= 2 && d <= 4 && (h < 12 || h > 14) ? "объекта" : "объектов";
        return n + " " + w;
    }

    public static string[] SizeUnits()
    {
        return Ru ? new[] { "Б", "КБ", "МБ", "ГБ", "ТБ" } : new[] { "B", "KB", "MB", "GB", "TB" };
    }
}
