using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private DispatcherTimer _autoHideTimer;

    private IntPtr _hwnd;
    private int _lastDesktopCount = -1;
    private int _lastDesktopIndex = -1;

    private Popup _volumePopup;
    private Slider _volumeSlider;
    private Popup _quickPopup;
    private Popup _calendarPopup;

    private NativeMethods.RECT _savedWorkArea;
    private bool _workAreaSaved;
    private bool _applyingWorkArea;
    private NativeMethods.APPBARDATA _taskbarData = new()
    {
        cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
    };

    private Brush _backgroundBrush = Brushes.Transparent;
    private int _cornerRadius = 14;
    private Thickness _barPadding = new(14, 0, 14, 0);
    private uint _acrylicArgb = 0xCC1A1B26;

    // виджеты по ключам из конфига (start/center/end)
    private readonly Dictionary<string, FrameworkElement> _widgets =
        new(System.StringComparer.OrdinalIgnoreCase);

    // исходные тултипы, чтобы включать/выключать по конфигу
    private readonly Dictionary<FrameworkElement, object> _toolTips = new();

    // авто-скрытие
    private bool _barHidden;
    private DateTime _pointerLeftAtUtc = DateTime.UtcNow;
    private double _normalTop;

    private SettingsWindow _settingsWindow;

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

    public Thickness BarPadding
    {
        get => _barPadding;
        private set { _barPadding = value; Raise(nameof(BarPadding)); }
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Панель снизу? (bottom — как у панели задач)</summary>
    private bool IsBottom =>
        string.Equals(ConfigService.Instance.Config.Bar.Position, "bottom",
            System.StringComparison.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => UpdateStats();

        _wsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wsTimer.Tick += (_, _) => RefreshWorkspaces();

        _widgets["workspaces"] = WorkspacesWidget;
        _widgets["title"] = TitleWidget;
        _widgets["clock"] = ClockButton;
        _widgets["stats"] = StatsWidget;
        _widgets["battery"] = BatteryWidget;
        _widgets["volume"] = VolumeButton;
        _widgets["settings"] = QuickSettingsButton;

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
        NativeMethods.RegisterHotKey(_hwnd, 2,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_S);

        _tray.Attach(_hwnd, "NoctaliaWin");
        _tray.SettingsRequested += OpenSettings;

        HwndSource source = HwndSource.FromHwnd(_hwnd);
        source.AddHook(WndProc);

        UpdateClock();
        UpdateStats();
        RefreshWorkspaces(true);

        _clockTimer.Start();
        _statsTimer.Start();
        _wsTimer.Start();

        // тестовый хук: открыть окно настроек сразу
        if (Environment.GetEnvironmentVariable("NOCTALIA_WIN_SETTINGS") == "1")
            OpenSettings();
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        RestoreWorkArea();
        _clockTimer.Stop();
        _statsTimer.Stop();
        _wsTimer.Stop();
        _autoHideTimer?.Stop();
        _tray.Dispose();
        _audio.Dispose();
        _stats.Dispose();
        NativeMethods.UnregisterHotKey(_hwnd, 1);
        NativeMethods.UnregisterHotKey(_hwnd, 2);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // сначала отдаём шанс трей-иконке
        var trayResult = _tray.WndProc(hwnd, msg, wParam, lParam, ref handled);
        if (handled) return trayResult;

        switch (msg)
        {
            case NativeMethods.WM_HOTKEY:
                if (wParam.ToInt32() == 1)
                {
                    Visibility = Visibility == Visibility.Visible
                        ? Visibility.Hidden : Visibility.Visible;
                }
                else if (wParam.ToInt32() == 2)
                {
                    OpenSettings();
                }
                handled = true;
                break;

            case NativeMethods.WM_DISPLAYCHANGE:
                Dispatcher.BeginInvoke(() =>
                {
                    // геометрия экрана поменялась — рабочую область берём заново
                    _workAreaSaved = false;
                    PositionBar();
                    EnableAcrylic();
                    ApplyRoundedRegion();
                    if (ConfigService.Instance.Config.Bar.ReserveSpace)
                        ReserveWorkArea();
                });
                break;

            case NativeMethods.WM_SETTINGCHANGE:
                // система/explorer мог переписать рабочую область — возвращаем резерв
                Dispatcher.BeginInvoke((Action)(() => ReapplyWorkArea()));
                break;

            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)NativeMethods.MA_NOACTIVATE;
        }
        return IntPtr.Zero;
    }

    // ================= Конфиг и внешний вид =================

    /// <summary>Цветовые роли темы: user-значение или дефолт режима (как palette в Noctalia).</summary>
    private static string Pick(string user, string dark, string light, bool isLight) =>
        !string.IsNullOrWhiteSpace(user) ? user : (isLight ? light : dark);

    private readonly record struct ThemeColors(
        string Background, string Surface, string OnSurface,
        string Dim, string Primary, string Outline, string Hover);

    private static ThemeColors ResolveColors(ThemeConfig t)
    {
        bool light = string.Equals(t.Mode, "light", System.StringComparison.OrdinalIgnoreCase);
        return new ThemeColors(
            Pick(t.Background, "#CC1A1B26", "#E6F2F3F8", light),
            Pick(t.Surface,     "#F21A1B26", "#FAFFFFFF", light),
            Pick(t.OnSurface,   "#FFF2F2F7", "#FF1A1B26", light),
            Pick(t.Dim,         "#FF9A9AA8", "#FF5A5B6E", light),
            Pick(t.Primary,     "#FFB4BEFE", "#FF7C7FD5", light),
            Pick(t.Outline,     "#33FFFFFF", "#1A000000", light),
            light ? "#14000000" : "#28FFFFFF");
    }

    private void ApplyConfig()
    {
        var cfg = ConfigService.Instance.Config;
        var bar = cfg.Bar;
        var colors = ResolveColors(cfg.Theme);

        // --- палитра ---
        Resources["TextColor"] = ParseBrush(colors.OnSurface);
        Resources["DimColor"] = ParseBrush(colors.Dim);
        Resources["AccentBrush"] = ParseBrush(colors.Primary);
        Resources["SurfaceBrush"] = ParseBrush(colors.Surface);
        Resources["OutlineBrush"] = ParseBrush(colors.Outline);
        Resources["HoverBrush"] = bar.HoverHighlight ? ParseBrush(colors.Hover) : Brushes.Transparent;

        // --- геометрия ---
        double opacity = System.Math.Clamp(bar.BackgroundOpacity, 0.0, 1.0);
        var bgColor = ApplyOpacity(ParseColor(colors.Background), opacity);
        BackgroundBrush = Freeze(bgColor);
        _acrylicArgb = (uint)((bgColor.A << 24) | (bgColor.R << 16) | (bgColor.G << 8) | bgColor.B);

        CornerRadius = System.Math.Max(0, bar.CornerRadius);
        Height = System.Math.Max(24, bar.Thickness);
        BarPadding = new Thickness(System.Math.Max(0, bar.Padding), 0, System.Math.Max(0, bar.Padding), 0);

        // --- компоновка виджетов start/center/end ---
        ApplyLayout(bar);

        // --- подсказки ---
        ApplyTooltips(bar.ShowTooltips);

        // --- авто-скрытие ---
        UpdateAutoHide(bar);

        Dispatcher.BeginInvoke(() =>
        {
            if (_hwnd == IntPtr.Zero) return;
            PositionBar();
            EnableAcrylic();
            ApplyRoundedRegion();
            if (bar.ReserveSpace) ReserveWorkArea();
            else ReleaseWorkArea();
        });
    }

    /// <summary>Расставляет виджеты по трём зонам согласно спискам start/center/end.</summary>
    private void ApplyLayout(BarConfig bar)
    {
        static List<string> Clean(List<string> src) =>
            src == null ? new List<string>()
                : new List<string>(src.Where(s => !string.IsNullOrWhiteSpace(s)));

        var start = Clean(bar.Start);
        var center = Clean(bar.Center);
        var end = Clean(bar.End);

        StartPanel.Children.Clear();
        CenterPanel.Children.Clear();
        EndPanel.Children.Clear();

        int gap = System.Math.Max(0, bar.WidgetSpacing / 2);
        var margin = new Thickness(gap, 0, gap, 0);

        void Place(string key, StackPanel panel)
        {
            if (!_widgets.TryGetValue(key.Trim(), out var el)) return;
            el.Margin = margin;
            panel.Children.Add(el); // повторное добавление само переносит из старого родителя
        }

        foreach (var k in start) Place(k, StartPanel);
        foreach (var k in center) Place(k, CenterPanel);
        foreach (var k in end) Place(k, EndPanel);
    }

    /// <summary>Включает/выключает тултипы, сохраняя исходные тексты.</summary>
    private void ApplyTooltips(bool show)
    {
        foreach (var el in new FrameworkElement[]
                 {
                     WorkspacesWidget, TitleWidget, ClockButton,
                     StatsWidget, BatteryWidget, VolumeButton, QuickSettingsButton,
                 })
        {
            if (!_toolTips.ContainsKey(el)) _toolTips[el] = el.ToolTip;
            el.ToolTip = show ? _toolTips[el] : null;
        }
    }

    private static Color ParseColor(string value)
    {
        try { return (Color)ColorConverter.ConvertFromString(value); }
        catch { return Colors.Transparent; }
    }

    private static Brush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Brush ParseBrush(string value) => Freeze(ParseColor(value));

    private static Color ApplyOpacity(Color c, double opacity) =>
        Color.FromArgb((byte)System.Math.Round(c.A * opacity), c.R, c.G, c.B);

    private Brush SurfaceBrush => Resources["SurfaceBrush"] as Brush ?? ParseBrush("#F21A1B26");
    private Brush OutlineBrush => Resources["OutlineBrush"] as Brush ?? ParseBrush("#33FFFFFF");
    private Brush TextBrush => Resources["TextColor"] as Brush ?? Brushes.White;

    private void PositionBar()
    {
        var bar = ConfigService.Instance.Config.Bar;

        // снять анимации авто-скрытия, иначе прямое изменение игнорируется
        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);

        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        int marginEnds = System.Math.Max(0, bar.MarginEnds);
        double width = System.Math.Max(200, screenW - marginEnds * 2);
        Left = marginEnds;
        Width = width;
        Height = System.Math.Max(24, bar.Thickness);

        double edge = System.Math.Max(0, bar.MarginEdge);
        _normalTop = IsBottom ? screenH - bar.Thickness - edge : edge;
        Top = _barHidden ? HiddenTop() : _normalTop;
    }

    /// <summary>Координата Top, при которой окно полностью за экраном.</summary>
    private double HiddenTop() =>
        IsBottom ? SystemParameters.PrimaryScreenHeight + 1 : -(Height + 1);

    private void EnableAcrylic()
    {
        if (_hwnd == IntPtr.Zero) return;
        var bar = ConfigService.Instance.Config.Bar;
        NativeMethods.EnableAcrylic(_hwnd, _acrylicArgb, bar.Blur);
    }

    private void ApplyRoundedRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        var cfg = ConfigService.Instance.Config;
        double pxPerDip = PixelsPerDip();
        int w = (int)(Width * pxPerDip);
        int h = (int)(Height * pxPerDip);
        int r = (int)(cfg.Bar.CornerRadius * pxPerDip);
        NativeMethods.SetRoundedRegion(_hwnd, w, h, System.Math.Max(0, r));
    }

    private double PixelsPerDip()
    {
        try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
        catch { return 1.0; }
    }

    // ================= Рабочая область =================

    /// <summary>Отрезает под панель полосу рабочей области (сверху или снизу).</summary>
    private void ReserveWorkArea()
    {
        var bar = ConfigService.Instance.Config.Bar;

        // Базу пересчитываем каждый раз от рамок экрана и позиции панели задач,
        // а не от текущей рабочей области: та может быть уже урезана нами же.
        _savedWorkArea = ScreenWorkArea();
        _workAreaSaved = true;

        var area = ComputeReservedArea(bar);
        ApplyWorkArea(area);
    }

    /// <summary>Полная рабочая область основного экрана в пикселях (без нашего резерва).</summary>
    private NativeMethods.RECT ScreenWorkArea()
    {
        double scale = PixelsPerDip();
        int w = (int)System.Math.Round(SystemParameters.PrimaryScreenWidth * scale);
        int h = (int)System.Math.Round(SystemParameters.PrimaryScreenHeight * scale);

        // Панель задач Windows не трогаем — вычитаем её полосу из рамок экрана.
        // Позиция системной панели задач берётся у оболочки, а не из рабочей
        // области: та могла быть испорчена прошлым запуском (резерв накапливался
        // 48 → 96 → 144, потому что база читалась уже урезанной).
        var area = new NativeMethods.RECT { Left = 0, Top = 0, Right = w, Bottom = h };

        if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref _taskbarData) != IntPtr.Zero)
        {
            var tb = _taskbarData.rc;
            int tbEdge = (int)_taskbarData.uEdge; // 0=left,1=top,2=right,3=bottom
            switch (tbEdge)
            {
                case 1: area.Top = System.Math.Min(h, tb.Bottom); break;
                case 3: area.Bottom = System.Math.Max(0, tb.Top); break;
                case 0: area.Left = System.Math.Min(w, tb.Right); break;
                case 2: area.Right = System.Math.Max(0, tb.Left); break;
            }
        }

        if (area.Bottom <= area.Top) area.Bottom = h;
        if (area.Right <= area.Left) area.Right = w;
        return area;
    }

    /// <summary>
    /// Единственная точка записи рабочей области. Пишем БЕЗ SPIF_UPDATEINIFILE:
    /// резерв не должен попадать в реестр, иначе следующий запуск прочитает уже
    /// урезанную область и отрежет ещё раз (резерв накапливался: 48 → 96 → ...).
    /// Без SPIF_SENDCHANGE — он синхронно рассылает WM_SETTINGCHANGE всем окнам,
    /// включая наше, чей обработчик снова звал SPI_SETWORKAREA (deadlock UI-потока).
    /// </summary>
    private void ApplyWorkArea(NativeMethods.RECT area)
    {
        if (_applyingWorkArea) return;
        _applyingWorkArea = true;
        try
        {
            NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETWORKAREA, 0, ref area, 0);
        }
        finally
        {
            _applyingWorkArea = false;
        }
    }

    private NativeMethods.RECT ComputeReservedArea(BarConfig bar)
    {
        // ВАЖНО: резерв считаем в физических пикселях — PixelsPerDip уже отношение px/dip.
        // Старая формула "*dpi/96" с PixelsPerDip давала 0 и окна залезали под панель.
        int reserve = (int)System.Math.Ceiling(
            (System.Math.Max(0, bar.MarginEdge) + System.Math.Max(24, bar.Thickness)) * PixelsPerDip());

        var area = _savedWorkArea;
        if (IsBottom)
            area.Bottom = System.Math.Max(area.Top + 1, area.Bottom - reserve);
        else
            area.Top = System.Math.Min(area.Bottom - 1, area.Top + reserve);
        return area;
    }

    /// <summary>Отпускает резерв рабочей области (вызывается при выходе приложения).</summary>
    public void ReleaseWorkArea()
    {
        if (_workAreaSaved) RestoreWorkArea();
    }

    private void RestoreWorkArea()
    {
        if (!_workAreaSaved) return;
        var wa = _savedWorkArea;
        ApplyWorkArea(wa);
        _workAreaSaved = false;
    }

    /// <summary>Возвращает резерв, если его снёс explorer/система (проверка идемпотентна).</summary>
    private void ReapplyWorkArea()
    {
        if (_applyingWorkArea) return;
        if (!_workAreaSaved || !ConfigService.Instance.Config.Bar.ReserveSpace) return;

        var expected = ComputeReservedArea(ConfigService.Instance.Config.Bar);
        var current = new NativeMethods.RECT();
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETWORKAREA, 0, ref current, 0))
            return;
        if (current.Top == expected.Top && current.Bottom == expected.Bottom) return;

        ApplyWorkArea(expected);
    }

    // ================= Авто-скрытие =================

    private void UpdateAutoHide(BarConfig bar)
    {
        if (bar.AutoHide)
        {
            if (_autoHideTimer == null)
            {
                _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
                _autoHideTimer.Tick += AutoHideTick;
            }
            _pointerLeftAtUtc = DateTime.UtcNow;
            _autoHideTimer.Start();
        }
        else
        {
            _autoHideTimer?.Stop();
            if (_barHidden) ShowBar(animate: false);
        }
    }

    private void AutoHideTick(object sender, EventArgs e)
    {
        try
        {
            AutoHideTickCore();
        }
        catch (System.Exception)
        {
            // тик авто-скрытия не должен ронять панель
        }
    }

    private void AutoHideTickCore()
    {
        var bar = ConfigService.Instance.Config.Bar;

        if (!bar.AutoHide || Visibility != Visibility.Visible) return;
        if (!NativeMethods.GetCursorPos(out var pt)) return;

        double scale = PixelsPerDip();
        double x = pt.X / scale;
        double y = pt.Y / scale;

        bool overBar = x >= Left && x <= Left + Width && y >= Top && y <= Top + Height;
        bool overPopup = (_volumePopup?.IsOpen ?? false)
                      || (_quickPopup?.IsOpen ?? false)
                      || (_calendarPopup?.IsOpen ?? false);

        if (_barHidden)
        {
            // показываем, когда курсор коснулся зоны, где должна быть панель
            double wouldTop = IsBottom
                ? SystemParameters.PrimaryScreenHeight - bar.Thickness - System.Math.Max(0, bar.MarginEdge)
                : System.Math.Max(0, bar.MarginEdge);
            bool inZone = y >= wouldTop - 3 && y <= wouldTop + bar.Thickness + 3
                       && x >= Left - 3 && x <= Left + Width + 3;
            if (inZone) ShowBar(animate: true);
            return;
        }

        if (overBar || overPopup)
        {
            _pointerLeftAtUtc = DateTime.UtcNow;
            return;
        }

        if ((DateTime.UtcNow - _pointerLeftAtUtc).TotalMilliseconds >=
            System.Math.Max(100, bar.AutoHideDelayMs))
        {
            HideBar();
        }
    }

    private void HideBar()
    {
        if (_barHidden) return;
        _barHidden = true;
        MoveTop(HiddenTop(), animate: true);
    }

    private void ShowBar(bool animate)
    {
        if (!_barHidden) return;
        _barHidden = false;
        MoveTop(_normalTop, animate);
    }

    private void MoveTop(double top, bool animate)
    {
        if (animate && ConfigService.Instance.Config.Bar.Animations && IsLoaded)
        {
            var anim = new DoubleAnimation(top, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            BeginAnimation(TopProperty, anim);
        }
        else
        {
            BeginAnimation(TopProperty, null);
            Top = top;
        }
    }

    // ================= Popup'ы =================

    /// <summary>Создаёт и открывает popup; при включённых анимациях — fade-in со сдвигом.</summary>
    private Popup OpenPopup(FrameworkElement child, FrameworkElement target)
    {
        var cfg = ConfigService.Instance.Config;
        var popup = new Popup
        {
            Child = child,
            StaysOpen = false,
            Placement = IsBottom ? PlacementMode.Top : PlacementMode.Bottom,
            PlacementTarget = target,
            AllowsTransparency = true,
        };

        if (cfg.Bar.Animations)
        {
            child.Opacity = 0;
            var transform = new TranslateTransform(0, 10);
            child.RenderTransform = transform;
            popup.Opened += (_, _) =>
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dur = TimeSpan.FromMilliseconds(160);
                child.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
                transform.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(10, 0, dur) { EasingFunction = ease });
            };
        }

        popup.IsOpen = true;
        return popup;
    }

    // ================= Часы =================

    private void UpdateClock()
    {
        var cfg = ConfigService.Instance.Config;
        TimeText.Text = DateTime.Now.ToString(cfg.Clock.TimeFormat);
        DateText.Text = DateTime.Now.ToString(cfg.Clock.DateFormat);
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
            Background = SurfaceBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            Child = calendar,
        };

        _calendarPopup = OpenPopup(border, ClockButton);
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

        bool tips = ConfigService.Instance.Config.Bar.ShowTooltips;
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
                ToolTip = tips ? $"Рабочий стол {i + 1}" : null,
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
                ToolTip = tips ? "Создать рабочий стол" : null,
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

        CpuText.Text = $"CPU {(int)_stats.CpuPercent}%";
        RamText.Text = $"RAM {_stats.RamUsedGb}/{_stats.RamTotalGb}G";

        if (_stats.HasBattery)
        {
            BatteryWidget.Visibility = Visibility.Visible;
            BatteryIcon.Text = _stats.Charging ? "\uE898" : BatteryGlyph(_stats.BatteryPercent);
            BatteryText.Text = $"{_stats.BatteryPercent}%";
            BatteryIcon.Foreground = _stats.BatteryPercent <= 20 && !_stats.Charging
                ? new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8))
                : Resources["TextColor"] as Brush ?? Brushes.White;
        }
        else
        {
            BatteryWidget.Visibility = Visibility.Collapsed;
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
            Foreground = TextBrush,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
        };
        muteBtn.Click += (_, _) => { _audio.ToggleMute(); UpdateVolumeUI(); };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Громкость",
            Foreground = TextBrush,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        });
        stack.Children.Add(_volumeSlider);
        stack.Children.Add(muteBtn);

        var border = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 12),
            Child = stack,
        };

        _volumePopup = OpenPopup(border, VolumeButton);
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
            Foreground = TextBrush,
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
            IsChecked = cfg.Bar.ReserveSpace,
            Foreground = TextBrush,
            Margin = new Thickness(0, 0, 0, 8),
        };
        reserveBox.Checked += (_, _) =>
        {
            cfg.Bar.ReserveSpace = true;
            ConfigService.Instance.Save();
            ApplyConfig();
        };
        reserveBox.Unchecked += (_, _) =>
        {
            cfg.Bar.ReserveSpace = false;
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
        stack.Children.Add(MakePowerButton("\uE713", "Настройки...", () =>
        {
            if (_quickPopup != null) _quickPopup.IsOpen = false;
            OpenSettings();
        }));
        stack.Children.Add(MakePowerButton("\uE8A5", "Папка конфига", () =>
            ConfigService.Instance.OpenConfigFolder()));
        stack.Children.Add(MakePowerButton("\uE8BB", "Выход", () =>
            Application.Current?.Shutdown()));

        var border = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = OutlineBrush,
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

        _quickPopup = OpenPopup(border, QuickSettingsButton);
    }

    private TextBlock MakeHeader(string text) => new()
    {
        Text = text,
        Foreground = TextBrush,
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
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Grid.SetColumn(icon, 0);

        var text = new TextBlock
        {
            Text = label,
            Foreground = TextBrush,
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

    // ================= Настройки =================

    private void OpenSettings()
    {
        Dispatcher.BeginInvoke((Action)(() =>
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }));
    }
}
