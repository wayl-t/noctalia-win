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

/// <summary>Секция bar: геометрия, поведение и порядок виджетов.</summary>
public sealed class BarConfig
{
    /// <summary>Позиция: "top" или "bottom" (как position у Noctalia).</summary>
    public string Position { get; set; } = "top";

    /// <summary>Толщина полосы в DIP (thickness у Noctalia).</summary>
    public int Thickness { get; set; } = 48;

    /// <summary>Отступ от каждого края по длине полосы (margin_ends у Noctalia).</summary>
    public int MarginEnds { get; set; } = 100;

    /// <summary>Отступ от экрана по нормали (margin_edge): 0 = прижата к краю.</summary>
    public int MarginEdge { get; set; }

    public int CornerRadius { get; set; } = 14;

    /// <summary>Прозрачность подложки 0..1 (background_opacity).</summary>
    public double BackgroundOpacity { get; set; } = 1.0;

    /// <summary>Acrylic-размытие под панелью (compositor_blur).</summary>
    public bool Blur { get; set; } = true;

    public int Padding { get; set; } = 14;
    public int WidgetSpacing { get; set; } = 6;

    /// <summary>Не пускать максимизированные окна под панель (reserve_space).</summary>
    public bool ReserveSpace { get; set; } = true;

    /// <summary>Прятать панель, когда курсор не у её края (auto_hide).</summary>
    public bool AutoHide { get; set; }

    /// <summary>Задержка скрытия после ухода курсора, мс.</summary>
    public int AutoHideDelayMs { get; set; } = 400;

    /// <summary>Мягкая подсветка виджета под курсором (hover_highlight).</summary>
    public bool HoverHighlight { get; set; } = true;

    /// <summary>Всплывающие подсказки (show_tooltip).</summary>
    public bool ShowTooltips { get; set; } = true;

    /// <summary>Плавные анимации popup'ов и авто-скрытия.</summary>
    public bool Animations { get; set; } = true;

    /// <summary>Виджеты слева. Доступные ключи: workspaces, title.</summary>
    public List<string> Start { get; set; } = new() { "workspaces", "title" };

    /// <summary>Виджеты по центру. Доступные ключи: clock.</summary>
    public List<string> Center { get; set; } = new() { "clock" };

    /// <summary>Виджеты справа. Доступные ключи: stats, battery, volume, settings.</summary>
    public List<string> End { get; set; } = new() { "stats", "battery", "volume", "settings" };
}

/// <summary>Секция theme: режим и цветовые роли. null = дефолт выбранного режима.</summary>
public sealed class ThemeConfig
{
    /// <summary>"dark" или "light".</summary>
    public string Mode { get; set; } = "dark";

    public string Background { get; set; }   // фон панели (подложка acrylic)
    public string Surface { get; set; }      // фон popup'ов
    public string OnSurface { get; set; }    // основной текст
    public string Dim { get; set; }          // приглушённый текст
    public string Primary { get; set; }      // акцент
    public string Outline { get; set; }      // обводки и разделители
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
