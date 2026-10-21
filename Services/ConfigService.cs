using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoctaliaWin.Services;

/// <summary>Корень конфигурации — структура как секции в конфиге оригинального Noctalia.</summary>
public sealed class PanelConfig
{
    public BarConfig Bar { get; set; } = new();
    public ThemeConfig Theme { get; set; } = new();
    public ClockConfig Clock { get; set; } = new();
    public bool AutoStart { get; set; }
}

/// <summary>Секция bar: геометрия, поведение и порядок виджетов.
/// Дефолты совпадают со значениями из example.toml оригинала.</summary>
public sealed class BarConfig
{
    /// <summary>Позиция: "top" или "bottom" (как position у Noctalia).</summary>
    public string Position { get; set; } = "top";

    /// <summary>Толщина полосы в DIP (thickness у Noctalia). Дефолт оригинала — 34.</summary>
    public int Thickness { get; set; } = 34;

    /// <summary>Отступ от каждого края по длине полосы (margin_ends у Noctalia).</summary>
    public int MarginEnds { get; set; } = 180;

    /// <summary>Отступ от экрана по нормали (margin_edge): 0 = прижата к краю.</summary>
    public int MarginEdge { get; set; } = 10;

    /// <summary>Глобальное скругление углов (radius у Noctalia). Дефолт — 12.</summary>
    public int CornerRadius { get; set; } = 12;

    /// <summary>Отдельные радиусы по углам; null = взять общий CornerRadius.</summary>
    public int? RadiusTopLeft { get; set; }
    public int? RadiusTopRight { get; set; }
    public int? RadiusBottomLeft { get; set; }
    public int? RadiusBottomRight { get; set; }

    /// <summary>Вырезать углы у края экрана внутрь (concave_edge_corners).
    /// Требует MarginEdge = 0, как в оригинале.</summary>
    public bool ConcaveEdgeCorners { get; set; } = true;

    /// <summary>Прозрачность подложки 0..1 (background_opacity).</summary>
    public double BackgroundOpacity { get; set; } = 1.0;

    /// <summary>Acrylic-размытие под панелью (compositor_blur).</summary>
    public bool Blur { get; set; } = true;

    /// <summary>Обводка полосы: роль палитры или #RRGGBB (border у Noctalia).</summary>
    public string Border { get; set; } = "outline";

    /// <summary>Толщина обводки в px; 0 выключает (border_width).</summary>
    public double BorderWidth { get; set; } = 0.0;

    public int Padding { get; set; } = 14;
    public int WidgetSpacing { get; set; } = 6;

    /// <summary>Масштаб содержимого (scale): иконки, зазоры, базовый текст.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Дополнительный масштаб только текста (font_scale).</summary>
    public double FontScale { get; set; } = 1.0;

    /// <summary>Насыщенность шрифта (font_weight): CSS 100–1000.</summary>
    public int FontWeight { get; set; } = 500;

    /// <summary>Капсулы под виджетами (capsule). В оригинале по умолчанию выключены.</summary>
    public bool Capsule { get; set; }

    /// <summary>Заливка капсулы: роль палитры или #hex (capsule_fill).</summary>
    public string CapsuleFill { get; set; } = "surface_variant";

    /// <summary>Радиус капсулы; null = авто-«пилюля» (capsule_radius).</summary>
    public double? CapsuleRadius { get; set; }

    /// <summary>Высота капсулы как доля толщины полосы (capsule_thickness).</summary>
    public double CapsuleThickness { get; set; } = 0.76;

    /// <summary>Прозрачность капсулы (capsule_opacity).</summary>
    public double CapsuleOpacity { get; set; } = 1.0;

    /// <summary>Внутренний отступ капсулы (capsule_padding).</summary>
    public double CapsulePadding { get; set; } = 6;

    /// <summary>Обводка капсулы: роль или #hex; пусто/не задано = без обводки.</summary>
    public string CapsuleBorder { get; set; }

    /// <summary>Толщина обводки капсулы (capsule_border_width).</summary>
    public double CapsuleBorderWidth { get; set; } = 1.0;

    /// <summary>Не пускать максимизированные окна под панель (reserve_space).</summary>
    public bool ReserveSpace { get; set; } = true;

    /// <summary>Прятать панель, когда курсор не у её края (auto_hide).</summary>
    public bool AutoHide { get; set; }

    /// <summary>Задержка скрытия после ухода курсора, мс (auto_hide_delay_ms).</summary>
    public int AutoHideDelayMs { get; set; }

    /// <summary>Смарт-автоскрытие: показывать, когда рабочий стол пуст (smart_auto_hide).</summary>
    public bool SmartAutoHide { get; set; }

    /// <summary>Кратковременно показывать панель при смене рабочего стола.</summary>
    public bool ShowOnWorkspaceSwitch { get; set; } = true;

    /// <summary>Мягкая подсветка виджета под курсором (hover_highlight).</summary>
    public bool HoverHighlight { get; set; } = true;

    /// <summary>Всплывающие подсказки (show_tooltip).</summary>
    public bool ShowTooltips { get; set; } = true;

    /// <summary>Плавные анимации popup'ов и авто-скрытия ([shell.animation]).</summary>
    public bool Animations { get; set; } = true;

    /// <summary>Длительность анимаций, множитель (speed у Noctalia: больше = быстрее).</summary>
    public double AnimationSpeed { get; set; } = 1.0;

    /// <summary>Тень под панелью (shadow).</summary>
    public bool Shadow { get; set; } = true;

    /// <summary>Действия «мёртвой зоны» — отступов вне секций виджетов.</summary>
    public string DeadZoneLeft { get; set; } = "none";
    public string DeadZoneRight { get; set; } = "control-center";

    /// <summary>Виджеты слева. Ключи: launcher, workspaces, title, stats.</summary>
    public List<string> Start { get; set; } = new() { "launcher", "workspaces" };

    /// <summary>Виджеты по центру. Ключи: clock.</summary>
    public List<string> Center { get; set; } = new() { "clock" };

    /// <summary>Виджеты справа: как в оригинале — media, tray, notifications, network,
    /// volume, battery, control-center, session.</summary>
    public List<string> End { get; set; } = new()
    {
        "media", "tray", "notifications", "network", "volume", "battery", "control-center", "session",
    };

    /// <summary>Заголовок активного окна (в оригинале его отдельным виджетом бара нет).</summary>
    public bool ShowWindowTitle { get; set; }
}

/// <summary>Секция theme: источник палитры, режим и переопределения ролей.</summary>
public sealed class ThemeConfig
{
    /// <summary>"dark", "light" или "auto" (auto = следовать режиму Windows).</summary>
    public string Mode { get; set; } = "dark";

    /// <summary>Пресет палитры (builtin у Noctalia): Noctalia, Catppuccin, Nord, ...</summary>
    public string Palette { get; set; } = "Noctalia";

    /// <summary>Очистить тёмную палитру до истинно чёрного (pure_black_dark).</summary>
    public bool PureBlackDark { get; set; }

    // Переопределения ролей палитры. null/пусто = значение пресета.
    public string Primary { get; set; }
    public string OnPrimary { get; set; }
    public string Secondary { get; set; }
    public string OnSecondary { get; set; }
    public string Tertiary { get; set; }
    public string OnTertiary { get; set; }
    public string Error { get; set; }
    public string OnError { get; set; }
    public string Surface { get; set; }
    public string OnSurface { get; set; }
    public string SurfaceVariant { get; set; }
    public string OnSurfaceVariant { get; set; }
    public string Outline { get; set; }
    public string Shadow { get; set; }
    public string Hover { get; set; }
    public string OnHover { get; set; }

    /// <summary>Заливка панели; null = Surface (в оригинале полоса — это surface).</summary>
    public string Background { get; set; }

    /// <summary>Приглушённый текст; null = OnSurfaceVariant.</summary>
    public string Dim { get; set; }
}

/// <summary>Секция clock: форматы времени и даты.</summary>
public sealed class ClockConfig
{
    public string TimeFormat { get; set; } = "HH:mm";
    public string DateFormat { get; set; } = "ddd, d MMM";
}

/// <summary>Загрузка/сохранение конфига в %APPDATA%\NoctaliaWin\config.json с горячей перезагрузкой.</summary>
public sealed class ConfigService
{
    public static ConfigService Instance { get; } = new();

    public PanelConfig Config { get; private set; } = new();

    public string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoctaliaWin");
    public string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private FileSystemWatcher _watcher;
    private bool _reloadGuard;

    public event Action ConfigChanged;

    public void Load()
    {
        Directory.CreateDirectory(ConfigDir);
        if (!File.Exists(ConfigPath))
        {
            Config = new PanelConfig();
            Save();
        }
        else
        {
            string raw = null;
            try
            {
                raw = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<PanelConfig>(raw, JsonOptions) ?? new PanelConfig();
            }
            catch
            {
                Config = new PanelConfig();
            }

            // миграция со схемы 0.1 (плоские ключи) на секции bar/theme/clock
            if (raw != null && raw.Contains("\"BackgroundColor\""))
            {
                ApplyLegacy(raw);
                Save();
            }
            // миграция 0.2 → 0.3: старые геометрия и роли палитры заменены
            // на доковые дефолты Noctalia; старый CreateDefault не переносим.
            else if (raw != null && raw.Contains("\"BackgroundOpacity\"")
                     && !raw.Contains("\"Palette\""))
            {
                MigrateTo03(raw);
                Save();
            }
        }
        StartWatcher();
        ApplyAutoStart();
    }

    public void Reload()
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<PanelConfig>(json, JsonOptions);
            if (cfg != null) Config = cfg;
        }
        catch { /* noop */ }
        ConfigChanged?.Invoke();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
        }
        catch { /* noop */ }
    }

    /// <summary>Перенос данных из схемы 0.1. Геометрия НЕ переносится:
    /// старые дефолты (плавающая полоса, margin 12/10) — это как раз то, что
    /// заменено на док-полосу; берутся новые значения по умолчанию.</summary>
    private void ApplyLegacy(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            string Str(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            bool? Bool(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

            var th = Config.Theme;
            th.Background ??= Str("BackgroundColor");
            th.Primary ??= Str("AccentColor");
            th.OnSurface ??= Str("TextColor");
            th.Dim ??= Str("DimTextColor");

            var b = Config.Bar;
            if (Bool("ShowWorkspaces") == false) b.Start.RemoveAll(k => k == "workspaces");
            if (Bool("ShowWindowTitle") == false) b.Start.RemoveAll(k => k == "title");
            if (Bool("ShowStats") == false) b.End.RemoveAll(k => k == "stats");
            if (Bool("ShowBattery") == false) b.End.RemoveAll(k => k == "battery");
            if (Bool("ShowVolume") == false) b.End.RemoveAll(k => k == "volume");

            if (Str("ClockFormat") is string tf) Config.Clock.TimeFormat = tf;
            if (Str("DateFormat") is string df) Config.Clock.DateFormat = df;
            if (Bool("AutoStart") is bool autos) Config.AutoStart = autos;
        }
        catch { /* noop */ }
    }

    /// <summary>Миграция 0.2 → 0.3. Ключи bar/theme переименованы под Noctalia,
    /// поэтому переносим только персональные значения и берём новые дефолты
    /// для всего, что изменилось по смыслу (палитра, капсулы, отступы).</summary>
    private void MigrateTo03(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Bar", out var barEl)) return;

            string Str(JsonElement el, string name) =>
                el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            bool? Bool(JsonElement el, string name) =>
                el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? v.GetBoolean() : null;
            int? Int(JsonElement el, string name) =>
                el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
            double? Dbl(JsonElement el, string name) =>
                el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

            var b = Config.Bar;
            if (Str(barEl, "Position") is string pos) b.Position = pos;
            if (Bool(barEl, "ReserveSpace") is bool rs) b.ReserveSpace = rs;
            if (Bool(barEl, "AutoHide") is bool ah) b.AutoHide = ah;
            if (Bool(barEl, "HoverHighlight") is bool hh) b.HoverHighlight = hh;
            if (Bool(barEl, "ShowTooltips") is bool st) b.ShowTooltips = st;
            if (Bool(barEl, "Animations") is bool an) b.Animations = an;
            if (Bool(barEl, "Blur") is bool bl) b.Blur = bl;
            if (Int(barEl, "AutoHideDelayMs") is int dl) b.AutoHideDelayMs = dl;
            if (Dbl(barEl, "BackgroundOpacity") is double bo) b.BackgroundOpacity = bo;

            // толщина: старый дефолт 48 заменён на 34 оригинала. Переносим
            // значение только если оно отличается от старого дефолта — то есть
            // пользователь его сам менял.
            if (Int(barEl, "Thickness") is int th && th != 48) b.Thickness = th;
            if (Int(barEl, "MarginEnds") is int me && me != 100) b.MarginEnds = me;
            if (Int(barEl, "Padding") is int pd && pd != 14) b.Padding = pd;
            if (Dbl(barEl, "WidgetSpacing") is double ws) b.WidgetSpacing = (int)ws;
            if (Int(barEl, "CornerRadius") is int cr && cr != 14) b.CornerRadius = cr;
            if (Int(barEl, "MarginEdge") is int mge) b.MarginEdge = mge;

            // порядок виджетов: старые ключи маппим в новые имена
            var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["workspaces"] = "workspaces",
                ["title"] = "title",
                ["clock"] = "clock",
                ["stats"] = "stats",
                ["battery"] = "battery",
                ["volume"] = "volume",
                ["settings"] = "control-center",
            };

            List<string> Lanes(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    return null;
                var list = new List<string>();
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    string key = item.GetString();
                    if (key != null && map.TryGetValue(key, out var mapped)) list.Add(mapped);
                }
                return list;
            }

            if (Lanes(barEl, "Start") is { Count: > 0 } s) b.Start = s;
            if (Lanes(barEl, "Center") is { Count: > 0 } c) b.Center = c;
            if (Lanes(barEl, "End") is { Count: > 0 } e) b.End = e;

            if (root.TryGetProperty("Theme", out var thEl))
            {
                if (Str(thEl, "Mode") is string mode) Config.Theme.Mode = mode;
                // старые роли переносим как переопределения поверх палитры Noctalia
                Config.Theme.Background = Str(thEl, "Background");
                Config.Theme.Surface = Str(thEl, "Surface");
                Config.Theme.OnSurface = Str(thEl, "OnSurface");
                Config.Theme.Dim = Str(thEl, "Dim");
                Config.Theme.Primary = Str(thEl, "Primary");
                Config.Theme.Outline = Str(thEl, "Outline");
            }

            if (root.TryGetProperty("Clock", out var ckEl))
            {
                if (Str(ckEl, "TimeFormat") is string tf) Config.Clock.TimeFormat = tf;
                if (Str(ckEl, "DateFormat") is string df) Config.Clock.DateFormat = df;
            }
            if (Bool(root, "AutoStart") is bool asv) Config.AutoStart = asv;
        }
        catch { /* noop */ }
    }

    private void StartWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(ConfigDir, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, _) =>
            {
                if (_reloadGuard) return;
                _reloadGuard = true;
                try
                {
                    // debounce: файл мог дописываться
                    Thread.Sleep(120);
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => Reload());
                }
                finally { _reloadGuard = false; }
            };
        }
        catch { /* noop */ }
    }

    public void OpenConfigFolder()
    {
        Directory.CreateDirectory(ConfigDir);
        NativeOpen(ConfigDir);
    }

    private static void NativeOpen(string path)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    /// <summary>Включение/выключение автозагрузки через реестр HKCU Run.</summary>
    public void ApplyAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            const string name = "NoctaliaWin";
            if (Config.AutoStart)
            {
                string exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(name, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
        }
        catch { /* noop */ }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
