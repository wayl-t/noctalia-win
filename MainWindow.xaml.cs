using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NoctaliaWin.Interop;
using NoctaliaWin.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;

namespace NoctaliaWin;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly VirtualDesktopService _vds = new();
    private readonly AudioService _audio = new();
    private readonly SystemStatsService _stats = new();
    private readonly TrayIconService _tray = new();

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _statsTimer;
    private readonly DispatcherTimer _wsTimer;

    private IntPtr _hwnd;
    private int _lastDesktopCount = -1;
    private int _lastDesktopIndex = -1;

    private Popup _volumePopup;
    private Slider _volumeSlider;
    private Popup _quickPopup;
    private Popup _calendarPopup;

    private NativeMethods.RECT _savedWorkArea;
    private bool _workAreaSaved;

    private Brush _backgroundBrush = Brushes.Transparent;
    private int _cornerRadius = 14;
    private Thickness _panelMargin = new(12, 10, 12, 0);

    public event PropertyChangedEventHandler PropertyChanged;

    public Brush BackgroundBrush
    {
        get => _backgroundBrush;
        private set { _backgroundBrush = value; Raise(nameof(BackgroundBrush)); }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        private set { _cornerRadius = value; Raise(nameof(CornerRadius)); }
    }

    public Thickness PanelMargin
    {
        get => _panelMargin;
        private set { _panelMargin = value; Raise(nameof(PanelMargin)); }
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public MainWindow()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => UpdateStats();

        _wsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wsTimer.Tick += (_, _) => RefreshWorkspaces();

        ConfigService.Instance.ConfigChanged += ApplyConfig;
    }

    // ================= Lifecycle =================

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // Скрыть из Alt-Tab
        int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE,
            ex | NativeMethods.WS_EX_TOOLWINDOW);

        ApplyConfig();
        PositionBar();
        EnableAcrylic();
        ApplyRoundedRegion();

        NativeMethods.RegisterHotKey(_hwnd, 1,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_SPACE);

        _tray.Attach(_hwnd, "NoctaliaWin");

        HwndSource source = HwndSource.FromHwnd(_hwnd);
        source.AddHook(WndProc);

        UpdateClock();
        UpdateStats();
        RefreshWorkspaces(true);

        _clockTimer.Start();
        _statsTimer.Start();
        _wsTimer.Start();
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        RestoreWorkArea();
        _clockTimer.Stop();
        _statsTimer.Stop();
        _wsTimer.Stop();
        _tray.Dispose();
        _audio.Dispose();
        _stats.Dispose();
        NativeMethods.UnregisterHotKey(_hwnd, 1);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // сначала отдаём шанс трей-иконке
        var trayResult = _tray.WndProc(hwnd, msg, wParam, lParam, ref handled);
        if (handled) return trayResult;

        switch (msg)
        {
            case NativeMethods.WM_HOTKEY:
                Visibility = Visibility == Visibility.Visible
                    ? Visibility.Hidden : Visibility.Visible;
                handled = true;
                break;

            case NativeMethods.WM_DISPLAYCHANGE:
                Dispatcher.BeginInvoke(() =>
                {
                    PositionBar();
                    ApplyRoundedRegion();
                });
                break;

            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)NativeMethods.MA_NOACTIVATE;
        }
        return IntPtr.Zero;
    }

    // ================= Конфиг и внешний вид =================

    private void ApplyConfig()
    {
        var cfg = ConfigService.Instance.Config;

        BackgroundBrush = ParseBrush(cfg.BackgroundColor);
        var accent = ParseBrush(cfg.AccentColor);
        var text = ParseBrush(cfg.TextColor);
        var dim = ParseBrush(cfg.DimTextColor);

        Resources["TextColor"] = text;
        Resources["DimColor"] = dim;
        Resources["AccentBrush"] = accent;

        CornerRadius = cfg.CornerRadius;
        Height = cfg.Height + 20;
        PanelMargin = new Thickness(cfg.MarginX, cfg.MarginTop, cfg.MarginX, 0);

        WorkspacesPanel.Visibility = cfg.ShowWorkspaces ? Visibility.Visible : Visibility.Collapsed;
        WindowTitleText.Visibility = cfg.ShowWindowTitle ? Visibility.Visible : Visibility.Collapsed;
        CpuText.Visibility = cfg.ShowStats ? Visibility.Visible : Visibility.Collapsed;
        RamText.Visibility = cfg.ShowStats ? Visibility.Visible : Visibility.Collapsed;
        BatteryBadge.Visibility = cfg.ShowBattery && _stats.HasBattery ? Visibility.Visible : Visibility.Collapsed;
        VolumeButton.Visibility = cfg.ShowVolume ? Visibility.Visible : Visibility.Collapsed;

        Dispatcher.BeginInvoke(() =>
        {
            PositionBar();
            EnableAcrylic();
            ApplyRoundedRegion();
            if (cfg.ReserveWorkArea) ReserveWorkArea(); else RestoreWorkArea();
        });
    }

    private static Brush ParseBrush(string value)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    private void PositionBar()
    {
        var cfg = ConfigService.Instance.Config;
        var area = SystemParameters.WorkArea;

        Left = area.Left + cfg.MarginX;
        Top = area.Top + cfg.MarginTop;
        Width = area.Width - cfg.MarginX * 2;
    }

    private void EnableAcrylic()
    {
        var cfg = ConfigService.Instance.Config;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(cfg.BackgroundColor);
            uint argb = ((uint)color.A << 24)
                | ((uint)color.R << 16)
                | ((uint)color.G << 8)
                | color.B;
            NativeMethods.EnableAcrylic(_hwnd, argb);
        }
        catch { /* noop */ }
    }

    private void ApplyRoundedRegion()
    {
        var cfg = ConfigService.Instance.Config;
        double dpi = 96;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
        int w = (int)(Width * dpi);
        int h = (int)(Height * dpi);
        int r = (int)(cfg.CornerRadius * dpi);
        NativeMethods.SetRoundedRegion(_hwnd, w, h, r);
    }

    // ================= Рабочая область =================

    private void ReserveWorkArea()
    {
        var cfg = ConfigService.Instance.Config;

        if (!_workAreaSaved)
        {
            if (!NativeMethods.SystemParametersInfo(
                    NativeMethods.SPI_GETWORKAREA, 0, ref _savedWorkArea, 0))
                return;
            _workAreaSaved = true;
        }

        double dpi = 96;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
        int reserve = (int)((cfg.MarginTop + cfg.Height + 4) * dpi / 96);

        var newArea = _savedWorkArea;
        if (newArea.Top + reserve >= newArea.Bottom) return;

        newArea.Top += reserve;
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETWORKAREA, 0, ref newArea,
            NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
    }

    private void RestoreWorkArea()
    {
        if (!_workAreaSaved) return;
        var wa = _savedWorkArea;
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETWORKAREA, 0, ref wa,
            NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
        _workAreaSaved = false;
    }

    // ================= Часы =================

    private void UpdateClock()
    {
        var cfg = ConfigService.Instance.Config;
        TimeText.Text = DateTime.Now.ToString(cfg.ClockFormat);
        DateText.Text = DateTime.Now.ToString(cfg.DateFormat);
    }

    private void Clock_Click(object sender, RoutedEventArgs e)
    {
        if (_calendarPopup != null && _calendarPopup.IsOpen)
        {
            _calendarPopup.IsOpen = false;
            return;
        }

        var calendar = new Calendar
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x1B, 0x26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            Child = calendar,
        };

        _calendarPopup = new Popup
        {
            Child = border,
            StaysOpen = false,
            Placement = PlacementMode.Bottom,
            PlacementTarget = ClockButton,
            AllowsTransparency = true,
        };
        _calendarPopup.IsOpen = true;
    }

    // ================= Рабочие столы =================

    private void RefreshWorkspaces(bool force = false)
    {
        if (!_vds.IsAvailable)
        {
            if (WorkspacesPanel.Children.Count > 0)
                WorkspacesPanel.Children.Clear();
            return;
        }

        int count = _vds.GetCount();
        int current = _vds.GetCurrentIndex();

        if (!force && count == _lastDesktopCount && current == _lastDesktopIndex)
            return;

        _lastDesktopCount = count;
        _lastDesktopIndex = current;

        WorkspacesPanel.Children.Clear();

        var accent = Resources["AccentBrush"] as Brush ?? Brushes.White;
        var dim = Resources["DimColor"] as Brush ?? Brushes.Gray;
        var idle = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        for (int i = 0; i < count; i++)
        {
            int index = i;
            var dot = new Border
            {
                Style = (Style)FindResource("WsDot"),
                Background = i == current ? accent : idle,
                ToolTip = $"Рабочий стол {i + 1}",
            };

            var label = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = i == current
                    ? new SolidColorBrush(Color.FromRgb(0x1A, 0x1B, 0x26))
                    : dim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            dot.Child = label;

            dot.MouseLeftButtonDown += (_, _) => _vds.SwitchTo(index);
            WorkspacesPanel.Children.Add(dot);
        }

        if (count < 10)
        {
            var addBtn = new Border
            {
                Style = (Style)FindResource("WsDot"),
                Background = Brushes.Transparent,
                ToolTip = "Создать рабочий стол",
            };
            addBtn.Child = new TextBlock
            {
                Text = "+",
                FontSize = 13,
                Foreground = dim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            addBtn.MouseLeftButtonDown += (_, _) => _vds.Create();
            WorkspacesPanel.Children.Add(addBtn);
        }
    }

    // ================= Активное окно =================

    private void RefreshWindowTitle()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == _hwnd || !NativeMethods.IsWindowVisible(hwnd))
        {
            WindowTitleText.Text = "";
            return;
        }

        var sb = new System.Text.StringBuilder(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        string title = sb.ToString();

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            if (proc.ProcessName is "NoctaliaWin" or "explorer" or "SearchHost" or "ShellExperienceHost")
                title = "";
        }
        catch { }

        WindowTitleText.Text = title;
    }

    // ================= Статистика =================

    private void UpdateStats()
    {
        _stats.Refresh();
        RefreshWindowTitle();

        var cfg = ConfigService.Instance.Config;
        if (cfg.ShowStats)
        {
            CpuText.Text = $"CPU {(int)_stats.CpuPercent}%";
            RamText.Text = $"RAM {_stats.RamUsedGb}/{_stats.RamTotalGb}G";
        }

        if (cfg.ShowBattery && _stats.HasBattery)
        {
            BatteryBadge.Visibility = Visibility.Visible;
            BatteryIcon.Text = _stats.Charging ? "\uE898" : BatteryGlyph(_stats.BatteryPercent);
            BatteryText.Text = $"{_stats.BatteryPercent}%";
            BatteryIcon.Foreground = _stats.BatteryPercent <= 20 && !_stats.Charging
                ? new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8))
                : Resources["TextColor"] as Brush ?? Brushes.White;
        }
        else
        {
            BatteryBadge.Visibility = Visibility.Collapsed;
        }

        UpdateVolumeUI();
    }

    private static string BatteryGlyph(int percent) => percent switch
    {
        >= 90 => "\uE850",
        >= 75 => "\uE851",
        >= 60 => "\uE852",
        >= 45 => "\uE853",
        >= 30 => "\uE854",
        >= 15 => "\uE855",
        _     => "\uE859",
    };

    // ================= Громкость =================

    private void UpdateVolumeUI()
    {
        if (!_audio.IsAvailable)
        {
            VolumeText.Text = "";
            VolumeIcon.Text = "\uE74F";
            return;
        }

        bool muted = _audio.GetMute();
        float vol = _audio.GetVolume();

        VolumeIcon.Text = muted || vol <= 0f ? "\uE74F" : "\uE767";
        VolumeText.Text = muted ? "×" : $"{(int)(vol * 100)}%";

        if (_volumePopup != null && _volumePopup.IsOpen &&
            _volumeSlider != null && !_volumeSlider.IsMouseOver)
        {
            _volumeSlider.Value = vol * 100;
        }
    }

    private void Volume_Scroll(object sender, MouseWheelEventArgs e)
    {
        if (!_audio.IsAvailable) return;
        float vol = _audio.GetVolume();
        vol += e.Delta > 0 ? 0.04f : -0.04f;
        _audio.SetVolume(vol);
        UpdateVolumeUI();
    }

    private void Volume_Click(object sender, RoutedEventArgs e)
    {
        if (_volumePopup != null && _volumePopup.IsOpen)
        {
            _volumePopup.IsOpen = false;
            return;
        }

        _volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = _audio.GetVolume() * 100,
            Width = 160,
            Margin = new Thickness(0, 8, 0, 4),
        };
        _volumeSlider.ValueChanged += (_, _) => _audio.SetVolume((float)(_volumeSlider.Value / 100.0));

        var muteBtn = new Button
        {
            Content = "Mute",
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 4, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
        };
        muteBtn.Click += (_, _) => { _audio.ToggleMute(); UpdateVolumeUI(); };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Громкость",
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        });
        stack.Children.Add(_volumeSlider);
        stack.Children.Add(muteBtn);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x1B, 0x26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 12),
            Child = stack,
        };

        _volumePopup = new Popup
        {
            Child = border,
            StaysOpen = false,
            Placement = PlacementMode.Bottom,
            PlacementTarget = VolumeButton,
            AllowsTransparency = true,
        };
        _volumePopup.IsOpen = true;
    }

    // ================= Быстрые настройки =================

    private void QuickSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_quickPopup != null && _quickPopup.IsOpen)
        {
            _quickPopup.IsOpen = false;
            return;
        }

        var cfg = ConfigService.Instance.Config;
        var stack = new StackPanel { Width = 230 };

        // Яркость
        int brightness = _stats.GetBrightness();
        if (brightness >= 0)
        {
            stack.Children.Add(MakeHeader("Яркость"));
            var slider = new Slider
            {
                Minimum = 5, Maximum = 100, Value = brightness,
                Margin = new Thickness(0, 2, 0, 8),
            };
            slider.ValueChanged += (_, _) => _stats.SetBrightness((int)slider.Value);
            stack.Children.Add(slider);
        }

        // Автозагрузка
        var autoStartBox = new CheckBox
        {
            Content = "Запускать вместе с Windows",
            IsChecked = cfg.AutoStart,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 8),
        };
        autoStartBox.Checked += (_, _) =>
        {
            cfg.AutoStart = true;
            ConfigService.Instance.Save();
            ConfigService.Instance.ApplyAutoStart();
        };
        autoStartBox.Unchecked += (_, _) =>
        {
            cfg.AutoStart = false;
            ConfigService.Instance.Save();
            ConfigService.Instance.ApplyAutoStart();
        };
        stack.Children.Add(autoStartBox);

        // Не пускать окна под панель
        var reserveBox = new CheckBox
        {
            Content = "Не пускать окна под панель",
            IsChecked = cfg.ReserveWorkArea,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
        };
        reserveBox.Checked += (_, _) =>
        {
            cfg.ReserveWorkArea = true;
            ConfigService.Instance.Save();
            ApplyConfig();
        };
        reserveBox.Unchecked += (_, _) =>
        {
            cfg.ReserveWorkArea = false;
            ConfigService.Instance.Save();
            ApplyConfig();
        };
        stack.Children.Add(reserveBox);

        stack.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

        // Питание
        stack.Children.Add(MakePowerButton("\uE7E8", "Заблокировать", () => NativeMethods.LockWorkStation()));
        stack.Children.Add(MakePowerButton("\uE708", "Сон", () => NativeMethods.SetSuspendState(false, false, false)));
        stack.Children.Add(MakePowerButton("\uE777", "Перезагрузка", () =>
            System.Diagnostics.Process.Start("shutdown.exe", "/r /t 0")));
        stack.Children.Add(MakePowerButton("\uE7E8", "Выключение", () =>
            System.Diagnostics.Process.Start("shutdown.exe", "/s /t 0")));

        stack.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
        stack.Children.Add(MakePowerButton("\uE8A5", "Папка конфига", () =>
            ConfigService.Instance.OpenConfigFolder()));
        stack.Children.Add(MakePowerButton("\uE8BB", "Выход", () =>
            Application.Current?.Shutdown()));

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x1B, 0x26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 12),
            Child = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 460,
                Background = Brushes.Transparent,
            },
        };

        _quickPopup = new Popup
        {
            Child = border,
            StaysOpen = false,
            Placement = PlacementMode.Bottom,
            PlacementTarget = QuickSettingsButton,
            AllowsTransparency = true,
        };
        _quickPopup.IsOpen = true;
    }

    private static TextBlock MakeHeader(string text) => new()
    {
        Text = text,
        Foreground = Brushes.White,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 2, 0, 2),
    };

    private Button MakePowerButton(string glyph, string label, Action action)
    {
        var grid = new Grid { Width = 195 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Grid.SetColumn(icon, 0);

        var text = new TextBlock
        {
            Text = label,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 1);

        grid.Children.Add(icon);
        grid.Children.Add(text);

        var btn = new Button
        {
            Content = grid,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
        };
        btn.Click += (_, _) => action();
        return btn;
    }
}
