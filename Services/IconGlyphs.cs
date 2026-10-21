using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace NoctaliaWin.Services;

/// <summary>
/// Иконки как в оригинальном Noctalia: шрифт Tabler (<c>noctalia-tabler.ttf</c>) плюс
/// таблица алиасов <c>kAliases</c> из <c>src/render/text/glyph_registry.cpp</c>.
///
/// Оригинал адресует иконки по имени (<c>"volume-high"</c>, <c>"battery-4"</c>, <c>"wifi"</c>),
/// а не по кодпоинту. Здесь то же самое: <see cref="Glyph"/> переводит имя в символ,
/// подставляя алиас, если у имени нет прямого кодпоинта, и «череп» (U+F292), если иконки нет —
/// ровно как <c>GlyphRegistry::lookup</c>.
/// </summary>
public static class IconGlyphs
{
    /// <summary>Кодпоинт отсутствующей иконки — «череп», как kMissingGlyph в оригинале.</summary>
    public const int Missing = 0xF292;

    private const string FontFileName = "noctalia-tabler.ttf";

    private static readonly Dictionary<string, int> _map = TablerCodepoints.Map;

    /// <summary>Алиас -> настоящее имя Tabler. Один в один из glyph_registry.cpp.</summary>
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal)
    {
        // General
        ["close"] = "x",
        ["add"] = "plus",
        ["more-vertical"] = "dots-vertical",
        ["person"] = "user",
        ["info"] = "file-description",
        ["unpin"] = "pinned-off",
        ["image"] = "photo",
        ["capslock"] = "keyboard",
        ["numlock"] = "keyboard",
        ["scrolllock"] = "keyboard",
        ["plugin"] = "plug-connected",
        ["official-plugin"] = "shield-filled",
        ["package"] = "box",

        // Toast / warnings
        ["toast-notice"] = "circle-check",
        ["toast-warning"] = "alert-circle",
        ["toast-error"] = "circle-x",
        ["warning"] = "exclamation-circle",

        // Media
        ["media-pause"] = "player-pause-filled",
        ["media-play"] = "player-play-filled",
        ["media-prev"] = "player-skip-back-filled",
        ["media-next"] = "player-skip-forward-filled",
        ["shuffle"] = "arrows-shuffle",
        ["stop"] = "player-stop-filled",
        ["microphone-mute"] = "microphone-off",

        // Volume
        ["volume-high"] = "volume",
        ["volume-low"] = "volume-2",
        ["volume-mute"] = "volume-off",
        ["volume-x"] = "volume-3",
        ["volume-zero"] = "volume-3",

        // Network speed
        ["download-speed"] = "download",
        ["upload-speed"] = "upload",

        // System monitor
        ["cpu-intensive"] = "alert-octagon",
        ["cpu-usage"] = "brand-speedtest",
        ["cpu-temperature"] = "flame",
        ["gpu-usage"] = "device-desktop",
        ["memory"] = "cpu",
        ["storage"] = "database",
        ["busy"] = "hourglass-empty",

        // Power
        ["performance"] = "gauge",
        ["balanced"] = "scale",
        ["powersaver"] = "leaf",
        ["shutdown"] = "power",
        ["reboot"] = "refresh",
        ["reboot-uefi"] = "device-desktop-cog",
        ["suspend"] = "player-pause",
        ["hibernate"] = "zzz",

        // Night light / dark mode
        ["nightlight-on"] = "moon",
        ["nightlight-off"] = "moon-off",
        ["nightlight-forced"] = "moon-stars",
        ["theme-mode"] = "contrast-filled",

        // Caffeine (idle inhibitor)
        ["caffeine-on"] = "mug-filled",
        ["caffeine-off"] = "mug",

        // Brightness / Display
        ["brightness-low"] = "brightness-down-filled",
        ["brightness-high"] = "brightness-up-filled",

        // Wallpaper / color
        ["wallpaper-selector"] = "library-photo",

        // Battery
        ["battery-0"] = "battery",
        ["battery-plugged"] = "battery-charging-2",

        // Bluetooth devices
        ["bluetooth-device-generic"] = "bluetooth",
        ["bluetooth-device-gamepad"] = "device-gamepad-2",
        ["bluetooth-device-microphone"] = "microphone",
        ["bluetooth-device-headset"] = "headset",
        ["bluetooth-device-earbuds"] = "device-airpods",
        ["bluetooth-device-headphones"] = "headphones",
        ["bluetooth-device-mouse"] = "mouse-2",
        ["bluetooth-device-keyboard"] = "bluetooth",
        ["bluetooth-device-phone"] = "device-mobile",
        ["bluetooth-device-watch"] = "device-watch",
        ["bluetooth-device-speaker"] = "device-speaker",
        ["bluetooth-device-tv"] = "device-tv",

        // Weather
        ["weather-sun"] = "sun",
        ["weather-moon"] = "moon",
        ["weather-moon-stars"] = "moon-stars",
        ["weather-cloud"] = "cloud",
        ["weather-cloud-off"] = "cloud-off",
        ["weather-cloud-haze"] = "cloud-fog",
        ["weather-cloud-lightning"] = "cloud-bolt",
        ["weather-cloud-rain"] = "cloud-rain",
        ["weather-cloud-snow"] = "cloud-snow",
        ["weather-cloud-sun"] = "cloud-sun",
        ["weather-sunrise"] = "sunrise",
        ["weather-sunset"] = "sunset",
    };

    private static FontFamily _family;
    private static bool _familyLoaded;

    /// <summary>Имя семейства, под которым зарегистрирован встроенный шрифт Tabler.</summary>
    public const string EmbeddedFamilyName = "noctalia-tabler-icons";

    /// <summary>
    /// Семейство шрифта иконок. Сначала встроенный Tabler (тот же файл, что в оригинале),
    /// затем системный «Tabler Icons», и лишь при полном отсутствии — Segoe MDL2 Assets.
    /// </summary>
    public static FontFamily Family
    {
        get
        {
            if (_familyLoaded) return _family;
            _familyLoaded = true;

            // 1) встроенный шрифт: распаковываем ресурс на диск и читаем семейства из файла.
            //    (WPF не умеет читать семейства шрифтов прямо из pack://-ресурса.)
            try
            {
                string path = ExtractFont();
                if (path != null)
                {
                    var fams = Fonts.GetFontFamilies(new Uri(path));
                    foreach (var f in fams)
                    {
                        if (IsUsable(f)) { _family = f; return _family; }
                    }
                }
            }
            catch { /* пробуем дальше */ }

            // 2) системный Tabler Icons
            if (HasFont("Tabler Icons")) { _family = new FontFamily("Tabler Icons"); return _family; }

            // 3) запасной вариант
            _family = new FontFamily("Segoe MDL2 Assets");
            return _family;
        }
    }

    private static string _fontPath;

    /// <summary>
    /// Достаёт встроенный TTF из ресурсов сборки в файл (%LOCALAPPDATA%\NoctaliaWin)
    /// и возвращает путь. При повторном запуске файл переиспользуется.
    /// </summary>
    private static string ExtractFont()
    {
        if (_fontPath != null) return _fontPath;

        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoctaliaWin");
        System.IO.Directory.CreateDirectory(dir);
        var target = System.IO.Path.Combine(dir, FontFileName);

        var uri = new Uri($"pack://application:,,,/Assets/{FontFileName}");
        var res = Application.GetResourceStream(uri);
        if (res == null) return null;

        using (var stream = res.Stream)
        {
            // перезаписываем, если размер отличается (обновление шрифта в новой версии)
            bool needWrite = true;
            if (System.IO.File.Exists(target) && new System.IO.FileInfo(target).Length == stream.Length)
                needWrite = false;

            if (needWrite)
            {
                var tmp = target + ".tmp";
                using (var fs = System.IO.File.Create(tmp)) stream.CopyTo(fs);
                System.IO.File.Delete(target);
                System.IO.File.Move(tmp, target);
            }
        }

        _fontPath = target;
        return _fontPath;
    }

    /// <summary>true, если используется именно Tabler (встроенный или системный).</summary>
    public static bool IsTabler => !string.Equals(Family.Source, "Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase);

    /// <summary>Символ иконки по имени. Неизвестное имя -> «череп» U+F292, как в оригинале.</summary>
    public static string Glyph(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        // прямое имя
        if (_map.TryGetValue(name, out int cp)) return char.ConvertFromUtf32(cp);
        // алиас
        if (_aliases.TryGetValue(name, out string native) && _map.TryGetValue(native, out int cp2))
            return char.ConvertFromUtf32(cp2);
        // литерал кодпоинта "U+XXXX" или "0xXXXX"
        if (TryParseLiteral(name, out int lit)) return char.ConvertFromUtf32(lit);
        return char.ConvertFromUtf32(Missing);
    }

    /// <summary>Кодпоинт иконки по имени (с учётом алиасов) либо <see cref="Missing"/>.</summary>
    public static int Codepoint(string name)
    {
        if (string.IsNullOrEmpty(name)) return Missing;
        if (_map.TryGetValue(name, out int cp)) return cp;
        if (_aliases.TryGetValue(name, out string native) && _map.TryGetValue(native, out int cp2)) return cp2;
        if (TryParseLiteral(name, out int lit)) return lit;
        return Missing;
    }

    /// <summary>Есть ли такая иконка (по имени или алиасу).</summary>
    public static bool Contains(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (_map.ContainsKey(name)) return true;
        if (_aliases.TryGetValue(name, out string native)) return _map.ContainsKey(native);
        return TryParseLiteral(name, out _);
    }

    private static bool TryParseLiteral(string value, out int cp)
    {
        cp = 0;
        if (value.Length < 3) return false;
        ReadOnlySpan<char> hex;
        if ((value[0] == 'U' || value[0] == 'u') && value[1] == '+') hex = value.AsSpan(2);
        else if (value.Length > 2 && value[0] == '0' && (value[1] == 'x' || value[1] == 'X')) hex = value.AsSpan(2);
        else return false;
        return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out cp) && cp > 0 && cp <= 0x10FFFF;
    }

    /// <summary>Проверяет, что семейство шрифта реально разворачивается (не пустышка).</summary>
    private static bool IsUsable(FontFamily ff)
    {
        try
        {
            var typefaces = ff.GetTypefaces();
            foreach (var t in typefaces)
            {
                if (t.TryGetGlyphTypeface(out _)) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static bool HasFont(string family)
    {
        try
        {
            var ff = new FontFamily(family);
            return ff.FamilyNames.Count > 0;
        }
        catch { return false; }
    }
}
