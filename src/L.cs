// English / Russian. Strings are written in English in the code; this maps them to Russian.
using System.Collections.Generic;
using System.Globalization;

static class L
{
    public static bool Ru { get; private set; }

    static readonly Dictionary<string, string> ru = new Dictionary<string, string>
    {
        // arca
        { "Open arca", "Открыть arca" },
        { "Tools", "Инструменты" },
        { "Start with Windows", "Запускать вместе с Windows" },
        { "Check for updates automatically", "Проверять обновления автоматически" },
        { "Exit", "Выход" },
        { "Couldn't open the arca window. Details are in arca.log.", "Не удалось открыть окно arca. Подробности в arca.log." },

        // window
        { "Settings", "Настройки" },
        { "About", "О программе" },
        { "Update available", "Доступно обновление" },
        { "Update now", "Обновить" },
        { "Turn on what you need. Each tool lives in the tray next to the clock.", "Включите то, что нужно. Каждый инструмент живёт в трее рядом с часами." },
        { "Closing this window keeps arca running in the tray. To quit, use Exit in the tray menu or Quit in Settings.",
          "Если закрыть это окно, arca продолжит работать в трее. Чтобы выйти совсем, нажмите «Выход» в меню трея или «Выйти» в настройках." },
        { "On — in the tray", "Включено — в трее" },
        { "On", "Вкл." },
        { "Off", "Выкл." },
        { "How to use", "Как пользоваться" },
        { "General", "Общие" },
        { "arca follows the Windows language unless you pick one", "По умолчанию arca говорит на языке Windows" },
        { "arca starts quietly in the tray", "arca запустится в трее, без окна" },
        { "Updates", "Обновления" },
        { "At startup and every 12 hours", "При запуске и раз в 12 часов" },
        { "Checking for updates…", "Проверяю обновления…" },
        { "You're up to date", "У вас последняя версия" },
        { "Version {0} is available", "Доступна версия {0}" },
        { "Couldn't check: {0}", "Не удалось проверить: {0}" },
        { "Not checked yet", "Ещё не проверялось" },
        { "Check now", "Проверить" },
        { "Advanced", "Дополнительно" },
        { "Settings file", "Файл настроек" },
        { "Open", "Открыть" },
        { "Quit arca", "Выйти из arca" },
        { "Stops every tool and puts all windows back to normal", "Останавливает все инструменты и возвращает окна как было" },
        { "Quit", "Выйти" },
        { "A box of tiny Windows tools", "Ларец маленьких утилит для Windows" },
        { "Version {0}", "Версия {0}" },
        { "arca keeps a few handy tools in one small app. Turn on the ones you need, and the rest stay out of the way. No installer, no background services, nothing sent anywhere except the update check.",
          "arca собирает несколько полезных утилит в одном маленьком приложении. Включите нужные, остальные не будут мешать. Без установки, без фоновых служб, в сеть уходит только проверка обновлений." },
        { "Report a problem", "Сообщить о проблеме" },
        { "Releases", "Все версии" },
        { "MIT license · © 2026 ellarupion", "Лицензия MIT · © 2026 ellarupion" },
        { "Press a shortcut…", "Нажмите сочетание…" },
        { "Add Ctrl, Alt, Shift or Win", "Добавьте Ctrl, Alt, Shift или Win" },
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
        { "Switch Windows between light and dark with one click, or let it follow a daily schedule.",
          "Переключает Windows между светлой и тёмной темой в один клик или по расписанию." },
        { "Switch on a schedule", "Переключать по расписанию" },
        { "A manual switch stays until the next scheduled one", "Ручное переключение действует до следующего по расписанию" },
        { "Light from", "Светлая с" },
        { "Time of day, like 07:00", "Время, например 07:00" },
        { "Dark from", "Тёмная с" },
        { "Time of day, like 19:00", "Время, например 19:00" },
        { "Click the ◐ icon next to the clock to switch the theme.", "Клик по значку ◐ у часов переключает тему." },
        { "Right-click it to turn the schedule on or off.", "Правый клик по нему включает и выключает расписание." },
        { "Toggle theme", "Переключить тему" },
        { "Auto ({0:hh\\:mm} light, {1:hh\\:mm} dark)", "По расписанию (светлая с {0:hh\\:mm}, тёмная с {1:hh\\:mm})" },
        { "Edit schedule…", "Изменить расписание…" },
        { "Theme — light", "Тема — светлая" },
        { "Theme — dark", "Тема — тёмная" },

        // recycle bin
        { "Recycle Bin", "Корзина" },
        { "The Recycle Bin next to the clock. Open it, empty it, or let it empty itself when it gets too big.",
          "Корзина рядом с часами. Открыть, очистить или очищать автоматически, когда она становится слишком большой." },
        { "Turn off to open with a single click", "Если выключить, корзина будет открываться одним щелчком" },
        { "Show the usual Windows confirmation", "Показывать обычное подтверждение Windows" },
        { "Empty automatically", "Очищать автоматически" },
        { "Without asking, once the bin is bigger than the limit below", "Без вопросов, когда корзина больше лимита ниже" },
        { "Size limit, GB", "Лимит, ГБ" },
        { "Fractions work too, like 2.5", "Можно дробное, например 2.5" },
        { "Double-click the bin icon to open the Recycle Bin.", "Двойной щелчок по значку корзины открывает её." },
        { "Right-click it: Empty Recycle Bin is the first item.", "Правый клик: первым пунктом идёт «Очистить корзину»." },
        { "The icon gets a dot when there's something inside. Hover over it to see how much.",
          "Когда в корзине что-то есть, на значке появляется точка. Наведите на значок, чтобы увидеть, сколько там." },
        { "Empty Recycle Bin", "Очистить корзину" },
        { "Open Recycle Bin", "Открыть корзину" },
        { "Open on double-click", "Открывать двойным щелчком" },
        { "Ask before emptying", "Спрашивать перед очисткой" },
        { "Auto-empty over {0:0.##} GB", "Очищать автоматически, если больше {0:0.##} ГБ" },
        { "Edit settings…", "Изменить настройки…" },
        { "Recycle Bin — empty", "Корзина пуста" },
        { "Recycle Bin — {0}, {1}", "Корзина — {0}, {1}" },

        // windows
        // taskbar
        { "Taskbar", "Панель задач" },
        { "Close apps with a middle click on the taskbar, and change the volume by scrolling over it.",
          "Закрывайте программы щелчком колёсика по панели задач и меняйте громкость, прокручивая колесо над ней." },
        { "Middle click closes the app", "Щелчок колёсиком закрывает программу" },
        { "Click a taskbar button with the mouse wheel to close that app", "Нажмите колёсиком на кнопку программы на панели задач, чтобы закрыть её" },
        { "Close all of its windows", "Закрывать все её окна" },
        { "When off, apps with several windows keep the usual middle click", "Если выключить, у программ с несколькими окнами щелчок колёсиком работает как обычно" },
        { "Scroll over the taskbar to change volume", "Громкость колесом над панелью задач" },
        { "Shows the usual Windows volume indicator", "Показывается обычный индикатор громкости Windows" },
        { "Scroll anywhere while holding", "Громкость колесом везде, если зажата" },
        { "Change the volume over any window while this key is held", "Колесо меняет громкость над любым окном, пока зажата эта клавиша" },
        { "Volume step", "Шаг громкости" },
        { "Each wheel notch changes the volume by 2% times this", "Один щелчок колеса меняет громкость на 2%, умноженные на это число" },
        { "Middle-click a taskbar button to close the app, the same as clicking its × button.",
          "Щелчок колёсиком по кнопке на панели задач закрывает программу, как кнопка ×." },
        { "Scroll the mouse wheel anywhere over the taskbar to turn the volume up or down.",
          "Прокрутите колесо над любым местом панели задач, чтобы сделать громче или тише." },
        { "If you use Windhawk mods that do the same, turn them off to avoid surprises.",
          "Если у вас стоят моды Windhawk с тем же действием, выключите их, чтобы они не мешали." },

        { "Window tools", "Работа с окнами" },
        { "Keep any window on top, hide it to the tray, make it see-through, or move it by grabbing anywhere.",
          "Закрепить любое окно поверх остальных, спрятать его в трей, сделать прозрачным или двигать, взявшись за любое место." },
        { "Pins the active window above all others. Press again to release", "Закрепляет активное окно над остальными. Повторное нажатие отпускает" },
        { "Click the window's tray icon to bring it back", "Клик по значку окна в трее возвращает его" },
        { "Down to 20%, in 10% steps", "До 20%, шагами по 10%" },
        { "Back up to fully opaque", "Обратно до полной непрозрачности" },
        { "Grab a window anywhere, not just the title bar", "Можно взяться за любое место окна, а не только за заголовок" },
        { "Shortcuts work on the active window.", "Сочетания клавиш действуют на активное окно." },
        { "When arca exits, every window goes back to normal.", "При выходе из arca все окна возвращаются как было." },
        { "Windows of apps running as administrator need arca to run as administrator too.",
          "Чтобы менять окна программ, запущенных от администратора, arca тоже нужно запустить от администратора." },
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
