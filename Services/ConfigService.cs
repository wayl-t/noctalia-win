using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoctaliaWin.Services;

public sealed class PanelConfig
{
    // Цвета в формате #AARRGGBB или #RRGGBB
    public string BackgroundColor { get; set; } = "#CC1A1B26";   // полупрозрачный тёмный
    public string AccentColor { get; set; } = "#FFB4BEFE";       // лаванда (catppuccin)
    public string TextColor { get; set; } = "#FFF2F2F7";
    public string DimTextColor { get; set; } = "#FF9A9AA8";

    // Геометрия
    public int Height { get; set; } = 40;
    public int MarginX { get; set; } = 12;
    public int MarginTop { get; set; } = 10;
    public int CornerRadius { get; set; } = 14;

    // Поведение
    public bool ReserveWorkArea { get; set; } = true;   // не пускать максимизированные окна под панель
    public bool ShowWorkspaces { get; set; } = true;
    public bool ShowWindowTitle { get; set; } = true;
    public bool ShowStats { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowVolume { get; set; } = true;
    public string ClockFormat { get; set; } = "HH:mm";
    public string DateFormat { get; set; } = "ddd, d MMM";

    // Автозагрузка
    public bool AutoStart { get; set; } = false;
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
            try
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<PanelConfig>(json, JsonOptions) ?? new PanelConfig();
            }
            catch
            {
                Config = new PanelConfig();
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
