using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
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
using FontStyle = System.Windows.FontStyle;
using Path = System.Windows.Shapes.Path;

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
    private Brush _barBorderBrush = Brushes.Transparent;
    private Thickness _barBorderThickness = new(0);
    private Thickness _startPadding = new(14, 0, 0, 0);
    private Thickness _endPadding = new(0, 0, 14, 0);
    private uint _acrylicArgb = 0xCC070722;

    // С‚РµРєСѓС‰Р°СЏ СЂР°Р·СЂРµС€С‘РЅРЅР°СЏ РїР°Р»РёС‚СЂР° (СЂРѕР»Рё Noctalia)
    private Palettes.Palette _palette = Palettes.Resolve("Noctalia", "dark");

    // РїРѕСЃС‚СЂРѕРµРЅРЅС‹Рµ РІРёРґР¶РµС‚С‹ РїРѕ РєР»СЋС‡Р°Рј start/center/end
    private readonly Dictionary<string, FrameworkElement> _widgets =
        new(System.StringComparer.OrdinalIgnoreCase);

    // РёСЃС…РѕРґРЅС‹Рµ С‚СѓР»С‚РёРїС‹, С‡С‚РѕР±С‹ РІРєР»СЋС‡Р°С‚СЊ/РІС‹РєР»СЋС‡Р°С‚СЊ РїРѕ РєРѕРЅС„РёРіСѓ
    private readonly Dictionary<FrameworkElement, object> _toolTips = new();

    // Р°РІС‚Рѕ-СЃРєСЂС‹С‚РёРµ
    private bool _barHidden;
    private DateTime _pointerLeftAtUtc = DateTime.UtcNow;
    private double _normalTop;
    private DateTime _peekUntilUtc = DateTime.MinValue;

    private SettingsWindow _settingsWindow;

    // РІРёРґР¶РµС‚С‹, РєРѕС‚РѕСЂС‹Рµ РЅСѓР¶РЅРѕ РѕР±РЅРѕРІР»СЏС‚СЊ РїРѕ С‚Р°Р№РјРµСЂСѓ
    private TextBlock _clockTime, _clockDate, _cpuText, _ramText, _netText, _batteryText, _volumeText;
    private TextBlock _batteryIcon, _volumeIcon, _netIcon, _mediaIcon, _notifIcon;
    private TextBlock _mediaText;
    private Border _batteryWidget, _netWidget, _mediaWidget, _notifWidget;
    private StackPanel _workspacesPanel;
    private Border _workspacesWidget, _titleWidget, _statsWidget;
    private TextBlock _titleText;
    private Button _clockButton, _volumeButton, _launcherButton, _controlCenterButton, _sessionButton;
    private Button _trayButton;

    public event PropertyChangedEventHandler PropertyChanged;

    public Brush BackgroundBrush
    {
        get => _backgroundBrush;
        private set { _backgroundBrush = value; Raise(nameof(BackgroundBrush)); }
    }

    public Brush BarBorderBrush
    {
        get => _barBorderBrush;
        private set { _barBorderBrush = value; Raise(nameof(BarBorderBrush)); }
    }

    public Thickness BarBorderThickness
    {
        get => _barBorderThickness;
        private set { _barBorderThickness = value; Raise(nameof(BarBorderThickness)); }
    }

    public Thickness StartPadding
    {
        get => _startPadding;
        private set { _startPadding = value; Raise(nameof(StartPadding)); }
    }

    public Thickness EndPadding
    {
        get => _endPadding;
        private set { _endPadding = value; Raise(nameof(EndPadding)); }
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>РџР°РЅРµР»СЊ СЃРЅРёР·Сѓ? (bottom вЂ” РєР°Рє Сѓ РїР°РЅРµР»Рё Р·Р°РґР°С‡)</summary>
    private bool IsBottom =>
        string.Equals(ConfigService.Instance.Config.Bar.Position, "bottom",
            System.StringComparison.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => { UpdateStats(); UpdateMedia(); };

        _wsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wsTimer.Tick += (_, _) => RefreshWorkspaces();

        BuildWidgets();

        ConfigService.Instance.ConfigChanged += ApplyConfig;
    }

    // ================= Р’РёРґР¶РµС‚С‹ =================

    /// <summary>РЎРѕР±РёСЂР°РµС‚ РІРёРґР¶РµС‚С‹ РїР°РЅРµР»Рё РІ РєРѕРґРµ: С‚Р°Рє РѕРґРёРЅ РЅР°Р±РѕСЂ СЌР»РµРјРµРЅС‚РѕРІ
    /// РїРµСЂРµРёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ Р»СЋР±С‹Рј РїРѕСЂСЏРґРєРѕРј start/center/end РёР· РєРѕРЅС„РёРіР°.</summary>
    private void BuildWidgets()
    {
        // --- start ---
        _launcherButton = MakeIconButton("Р›Р°СѓРЅС‡РµСЂ", "\uE71D", Launcher_Click);

        _workspacesPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _workspacesWidget = new Border { Style = (Style)FindResource("WidgetBorder"), Child = _workspacesPanel };

        _titleText = new TextBlock { Style = (Style)FindResource("DimText"), MaxWidth = 300, FontSize = 12 };
        _titleWidget = new Border
        {
            Style = (Style)FindResource("WidgetBorder"), Child = _titleText, ToolTip = "РђРєС‚РёРІРЅРѕРµ РѕРєРЅРѕ",
        };

        // --- center ---
        _clockTime = new TextBlock { Style = (Style)FindResource("BarText"), FontWeight = FontWeights.SemiBold };
        _clockDate = new TextBlock { Style = (Style)FindResource("DimText"), Margin = new Thickness(7, 0, 0, 0) };
        _clockButton = new Button { Style = (Style)FindResource("BarButton") };
        _clockButton.ToolTip = "РљР°Р»РµРЅРґР°СЂСЊ";
        _clockButton.Click += Clock_Click;
        var clockRow = new StackPanel { Orientation = Orientation.Horizontal };
        clockRow.Children.Add(_clockTime);
        clockRow.Children.Add(_clockDate);
        _clockButton.Content = clockRow;

        // --- end ---
        _mediaIcon = MakeGlyph("");
        _mediaText = new TextBlock { Style = (Style)FindResource("DimText"), Margin = new Thickness(6, 0, 0, 0), FontSize = 12, MaxWidth = 190 };
        var mediaRow = new StackPanel { Orientation = Orientation.Horizontal };
        mediaRow.Children.Add(_mediaIcon);
        mediaRow.Children.Add(_mediaText);
        _mediaWidget = new Border { Style = (Style)FindResource("WidgetBorder"), Child = mediaRow, ToolTip = "РњРµРґРёР°", Visibility = Visibility.Collapsed };

        _notifIcon = MakeGlyph("\uE7ED");
        _notifWidget = new Border
        {
            Style = (Style)FindResource("WidgetBorder"),
            Child = _notifIcon, ToolTip = "РЈРІРµРґРѕРјР»РµРЅРёСЏ", Cursor = Cursors.Hand,
        };
        _notifWidget.MouseLeftButtonDown += (_, _) => Notifications_Click();

        _trayButton = MakeIconButton("РЎРёСЃС‚РµРјРЅС‹Р№ С‚СЂРµР№", "\uE71D", (_, _) => OpenTrayFallback());
        _trayButton.Visibility = Visibility.Collapsed; // С‚СЂРµР№ РІ Windows вЂ” С‡РµСЂРµР· СЃРІРѕСЋ РёРєРѕРЅРєСѓ

        _netIcon = MakeGlyph("\uE774");
        _netText = new TextBlock { Style = (Style)FindResource("DimText"), Margin = new Thickness(5, 0, 0, 0), FontSize = 12 };
        var netRow = new StackPanel { Orientation = Orientation.Horizontal };
        netRow.Children.Add(_netIcon);
        netRow.Children.Add(_netText);
        _netWidget = new Border { Style = (Style)FindResource("WidgetBorder"), Child = netRow, ToolTip = "РЎРµС‚СЊ" };
        _volumeIcon = MakeGlyph("\uE767");
        _volumeText = new TextBlock { Style = (Style)FindResource("BarText"), Margin = new Thickness(5, 0, 0, 0), FontSize = 12 };
        _volumeButton = new Button { Style = (Style)FindResource("BarButton"), ToolTip = "Р“СЂРѕРјРєРѕСЃС‚СЊ вЂ” РєР»РёРє, РїСЂРѕРєСЂСѓС‚РєР° вЂ” СЂРµРіСѓР»РёСЂРѕРІРєР°" };
        var volRow = new StackPanel { Orientation = Orientation.Horizontal };
        volRow.Children.Add(_volumeIcon);
        volRow.Children.Add(_volumeText);
        _volumeButton.Content = volRow;
        _volumeButton.Click += Volume_Click;
        _volumeButton.MouseWheel += Volume_Scroll;

        _batteryIcon = MakeGlyph("\uE850");
        _batteryText = new TextBlock { Style = (Style)FindResource("BarText"), Margin = new Thickness(4, 0, 0, 0), FontSize = 12 };
        var batRow = new StackPanel { Orientation = Orientation.Horizontal };
        batRow.Children.Add(_batteryIcon);
        batRow.Children.Add(_batteryText);
        _batteryWidget = new Border { Style = (Style)FindResource("WidgetBorder"), Child = batRow, ToolTip = "Р‘Р°С‚Р°СЂРµСЏ", Visibility = Visibility.Collapsed };

        _controlCenterButton = MakeIconButton("Р‘С‹СЃС‚СЂС‹Рµ РЅР°СЃС‚СЂРѕР№РєРё", "\uE713", QuickSettings_Click);
        _sessionButton = MakeIconButton("РџРёС‚Р°РЅРёРµ", "\uE7E8", Session_Click);

        // --- РїРѕРєР°Р·Р°С‚РµР»Рё СЂРµСЃСѓСЂСЃРѕРІ (РЅРµ РІ РґРµС„РѕР»С‚РЅРѕРј РЅР°Р±РѕСЂРµ РѕСЂРёРіРёРЅР°Р»Р°, РѕСЃС‚Р°РІР»РµРЅС‹ РѕРїС†РёРµР№) ---
        _cpuText = new TextBlock { Style = (Style)FindResource("DimText"), FontSize = 12 };
        _ramText = new TextBlock { Style = (Style)FindResource("DimText"), Margin = new Thickness(7, 0, 0, 0), FontSize = 12 };
        var statRow = new StackPanel { Orientation = Orientation.Horizontal };
        statRow.Children.Add(_cpuText);
        statRow.Children.Add(_ramText);
        _statsWidget = new Border { Style = (Style)FindResource("WidgetBorder"), Child = statRow, ToolTip = "Р РµСЃСѓСЂСЃС‹ СЃРёСЃС‚РµРјС‹" };

        void Reg(string key, FrameworkElement el) => _widgets[key] = el;
        Reg("launcher", _launcherButton);
        Reg("workspaces", _workspacesWidget);
        Reg("title", _titleWidget);
        Reg("clock", _clockButton);
        Reg("media", _mediaWidget);
        Reg("tray", _trayButton);
        Reg("notifications", _notifWidget);
        Reg("network", _netWidget);
        Reg("volume", _volumeButton);
        Reg("battery", _batteryWidget);
        Reg("control-center", _controlCenterButton);
        Reg("session", _sessionButton);
        Reg("stats", _statsWidget);
    }

    private TextBlock MakeGlyph(string glyph)
    {
        var tb = new TextBlock
        {
            Style = (Style)FindResource("BarIcon"),
            Text = glyph,
        };
        ApplyIconFont(tb);
        return tb;
    }

    private Button MakeIconButton(string tip, string glyph, RoutedEventHandler onClick)
    {
        var tb = new TextBlock { Style = (Style)FindResource("BarIcon"), Text = glyph };
        ApplyIconFont(tb);
        var btn = new Button
        {
            Style = (Style)FindResource("BarButton"),
            Content = tb,
            ToolTip = tip,
        };
        btn.Click += onClick;
        return btn;
    }

    /// <summary>РЁСЂРёС„С‚ РёРєРѕРЅРѕРє Tabler, РµСЃР»Рё РѕРЅ РµСЃС‚СЊ; РёРЅР°С‡Рµ СЃРёСЃС‚РµРјРЅС‹Рµ РіР»РёС„С‹ Segoe MDL2.</summary>
    private void ApplyIconFont(TextBlock tb)
    {
        // Tabler РёРґС‘С‚ РІ РєРѕРјРїР»РµРєС‚Рµ РѕСЂРёРіРёРЅР°Р»Р°; РІ РЅР°С€РµР№ СЃР±РѕСЂРєРµ Р±РµСЂС‘Рј Tabler, РµСЃР»Рё
        // РѕРЅ СѓСЃС‚Р°РЅРѕРІР»РµРЅ, Р° РёРЅР°С‡Рµ РѕСЃС‚Р°РІР»СЏРµРј Segoe MDL2 Assets (РіР»РёС„С‹ СЃРѕРІРїР°РґР°СЋС‚).
        var tabler = new FontFamily("Tabler Icons");
        tb.FontFamily = _hasTabler ? tabler : new FontFamily("Segoe MDL2 Assets");
    }

    private static readonly bool _hasTabler = HasFont("Tabler Icons");

    private static bool HasFont(string family)
    {
        try
        {
            foreach (var f in Fonts.SystemFontFamilies)
                if (string.Equals(f.Source, family, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }
        return false;
    }

    // ================= Lifecycle =================

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE,
            ex | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        ApplyConfig();
        PositionBar();
        EnableAcrylic();
        ApplyBarRegion();

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
        UpdateMedia();
        RefreshWorkspaces(true);

        _clockTimer.Start();
        _statsTimer.Start();
        _wsTimer.Start();

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
                    _workAreaSaved = false;
                    PositionBar();
                    EnableAcrylic();
                    ApplyBarRegion();
                    if (ConfigService.Instance.Config.Bar.ReserveSpace)
                        ReserveWorkArea();
                });
                break;

            case NativeMethods.WM_SETTINGCHANGE:
                Dispatcher.BeginInvoke((Action)(() => ReapplyWorkArea()));
                break;

            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)NativeMethods.MA_NOACTIVATE;
        }
        return IntPtr.Zero;
    }

    // ================= РљРѕРЅС„РёРі Рё РІРЅРµС€РЅРёР№ РІРёРґ =================

    /// <summary>РџРµСЂРµРѕРїСЂРµРґРµР»РµРЅРёРµ СЂРѕР»Рё: РїРѕР»СЊР·РѕРІР°С‚РµР»СЊСЃРєРѕРµ Р·РЅР°С‡РµРЅРёРµ РёР»Рё СЂРѕР»СЊ РїСЂРµСЃРµС‚Р°.</summary>
    private static string Pick(string user, string preset) =>
        !string.IsNullOrWhiteSpace(user) ? user : preset;

    /// <summary>Р Р°Р·СЂРµС€Р°РµС‚ РїР°Р»РёС‚СЂСѓ: РїСЂРµСЃРµС‚ Noctalia + РїРµСЂРµРѕРїСЂРµРґРµР»РµРЅРёСЏ СЂРѕР»РµР№ РёР· РєРѕРЅС„РёРіР°.
    /// РЎРїРµС†Р·РЅР°С‡РµРЅРёРµ "auto" СЃР»РµРґСѓРµС‚ СЃРІРµС‚Р»РѕРјСѓ СЂРµР¶РёРјСѓ Windows.</summary>
    private Palettes.Palette ResolvePalette(ThemeConfig t)
    {
        string mode = t.Mode;
        if (string.Equals(mode, "auto", System.StringComparison.OrdinalIgnoreCase))
            mode = IsSystemLight() ? "light" : "dark";

        var p = Palettes.Resolve(t.Palette, mode);

        if (t.PureBlackDark && !string.Equals(mode, "light", System.StringComparison.OrdinalIgnoreCase))
        {
            // pure_black_dark: С‚С‘РјРЅС‹Рµ РїРѕРІРµСЂС…РЅРѕСЃС‚Рё РїСЂРёР¶РёРјР°СЋС‚СЃСЏ Рє РёСЃС‚РёРЅРЅРѕ С‡С‘СЂРЅРѕРјСѓ
            p = p with { Surface = "#000000", SurfaceVariant = "#0a0a14" };
        }

        return p with
        {
            Primary = Pick(t.Primary, p.Primary),
            OnPrimary = Pick(t.OnPrimary, p.OnPrimary),
            Secondary = Pick(t.Secondary, p.Secondary),
            OnSecondary = Pick(t.OnSecondary, p.OnSecondary),
            Tertiary = Pick(t.Tertiary, p.Tertiary),
            OnTertiary = Pick(t.OnTertiary, p.OnTertiary),
            Error = Pick(t.Error, p.Error),
            OnError = Pick(t.OnError, p.OnError),
            Surface = Pick(t.Surface, p.Surface),
            OnSurface = Pick(t.OnSurface, p.OnSurface),
            SurfaceVariant = Pick(t.SurfaceVariant, p.SurfaceVariant),
            OnSurfaceVariant = Pick(t.OnSurfaceVariant, p.OnSurfaceVariant),
            Outline = Pick(t.Outline, p.Outline),
            Shadow = Pick(t.Shadow, p.Shadow),
            Hover = Pick(t.Hover, p.Hover),
            OnHover = Pick(t.OnHover, p.OnHover),
        };
    }

    private static bool IsSystemLight()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v) return v != 0;
        }
        catch { }
        return false;
    }

    /// <summary>Р¦РІРµС‚ СЂРѕР»Рё РёР»Рё hex (#RGB/#RGBA/#RRGGBB/#RRGGBBAA).</summary>
    private Color RoleColor(string spec, Palettes.Palette p, string fallback)
    {
        string v = string.IsNullOrWhiteSpace(spec) ? null : spec.Trim();
        if (v == null) return ParseColor(fallback);

        // РёРјСЏ СЂРѕР»Рё (snake_case) вЂ” РјР°РїРїРёРј РІ РїР°Р»РёС‚СЂСѓ
        string role = v.Replace("-", "_").ToLowerInvariant();
        string resolved = role switch
        {
            "primary" => p.Primary,
            "on_primary" => p.OnPrimary,
            "secondary" => p.Secondary,
            "on_secondary" => p.OnSecondary,
            "tertiary" => p.Tertiary,
            "on_tertiary" => p.OnTertiary,
            "error" => p.Error,
            "on_error" => p.OnError,
            "surface" => p.Surface,
            "on_surface" => p.OnSurface,
            "surface_variant" => p.SurfaceVariant,
            "on_surface_variant" => p.OnSurfaceVariant,
            "outline" => p.Outline,
            "shadow" => p.Shadow,
            "hover" => p.Hover,
            "on_hover" => p.OnHover,
            _ => null,
        };
        if (resolved != null) return ParseColor(resolved);

        return v.StartsWith("#") ? ParseColorHex(v, fallback) : ParseColor(fallback);
    }

    /// <summary>Р Р°Р·Р±РѕСЂ #RGB/#RGBA/#RRGGBB/#RRGGBBAA (РєР°Рє РІ Noctalia).</summary>
    private static Color ParseColorHex(string hex, string fallback)
    {
        try
        {
            string h = hex.TrimStart('#');
            byte a = 0xFF, r, g, b;
            switch (h.Length)
            {
                case 3:
                    r = (byte)(System.Convert.ToInt32($"{h[0]}{h[0]}", 16));
                    g = (byte)(System.Convert.ToInt32($"{h[1]}{h[1]}", 16));
                    b = (byte)(System.Convert.ToInt32($"{h[2]}{h[2]}", 16));
                    break;
                case 4:
                    r = (byte)(System.Convert.ToInt32($"{h[0]}{h[0]}", 16));
                    g = (byte)(System.Convert.ToInt32($"{h[1]}{h[1]}", 16));
                    b = (byte)(System.Convert.ToInt32($"{h[2]}{h[2]}", 16));
                    a = (byte)(System.Convert.ToInt32($"{h[3]}{h[3]}", 16));
                    break;
                case 6:
                    r = System.Convert.ToByte(h.Substring(0, 2), 16);
                    g = System.Convert.ToByte(h.Substring(2, 2), 16);
                    b = System.Convert.ToByte(h.Substring(4, 2), 16);
                    break;
                case 8:
                    r = System.Convert.ToByte(h.Substring(0, 2), 16);
                    g = System.Convert.ToByte(h.Substring(2, 2), 16);
                    b = System.Convert.ToByte(h.Substring(4, 2), 16);
                    a = System.Convert.ToByte(h.Substring(6, 2), 16);
                    break;
                default:
                    return ParseColor(fallback);
            }
            return Color.FromArgb(a, r, g, b);
        }
        catch { return ParseColor(fallback); }
    }

    private void ApplyConfig()
    {
        var cfg = ConfigService.Instance.Config;
        var bar = cfg.Bar;

        // --- РїР°Р»РёС‚СЂР° ---
        _palette = ResolvePalette(cfg.Theme);

        var surface = ParseColor(_palette.Surface);
        var onSurface = ParseColor(_palette.OnSurface);
        var onSurfaceVariant = ParseColor(_palette.OnSurfaceVariant);
        var outline = ParseColor(_palette.Outline);

        Resources["TextColor"] = Freeze(onSurface);
        Resources["DimColor"] = Freeze(ParseColor(Pick(cfg.Theme.Dim, _palette.OnSurfaceVariant)));
        Resources["AccentBrush"] = Freeze(ParseColor(_palette.Primary));
        Resources["OnAccentBrush"] = Freeze(ParseColor(_palette.OnPrimary));
        Resources["SurfaceBrush"] = Freeze(ApplyOpacity(surface, 0.97));
        Resources["SurfaceVariantBrush"] = Freeze(ApplyOpacity(ParseColor(_palette.SurfaceVariant), 0.97));
        Resources["OutlineBrush"] = Freeze(ApplyAlpha(outline, 0.75));
        Resources["CapsuleBorderBrush"] = Brushes.Transparent;

        // hover: РєР°РїСЃСѓР»Р° РїСЂРё РЅР°РІРµРґРµРЅРёРё. hover_highlight=false вЂ” РїРѕРґСЃРІРµС‚РєРё РЅРµС‚.
        var hoverColor = ParseColor(_palette.Hover);
        Resources["HoverBrush"] = bar.HoverHighlight
            ? Freeze(ApplyOpacity(Mix(surface, hoverColor, 0.55), 0.9))
            : Brushes.Transparent;

        // --- РєР°РїСЃСѓР»С‹ ---
        var capsuleFillColor = RoleColor(bar.CapsuleFill, _palette, _palette.SurfaceVariant);
        Resources["CapsuleBrush"] = bar.Capsule
            ? Freeze(ApplyOpacity(capsuleFillColor, System.Math.Clamp(bar.CapsuleOpacity, 0, 1)))
            : Brushes.Transparent;

        double capsuleHeight = System.Math.Clamp(bar.CapsuleThickness, 0.3, 1.0) * bar.Thickness * bar.Scale;
        double capsuleRadius = bar.CapsuleRadius ?? capsuleHeight / 2.0;
        Resources["WidgetRadius"] = new CornerRadius(System.Math.Max(0, capsuleRadius));

        double capPad = System.Math.Clamp(bar.CapsulePadding, 0, 48) * bar.Scale;
        Resources["CapsulePadding"] = new Thickness(capPad, 0, capPad, 0);

        if (bar.Capsule && !string.IsNullOrWhiteSpace(bar.CapsuleBorder))
        {
            var cb = RoleColor(bar.CapsuleBorder, _palette, _palette.Outline);
            Resources["CapsuleBorderBrush"] = Freeze(ApplyAlpha(cb, 0.7));
        }

        // --- РіРµРѕРјРµС‚СЂРёСЏ РїРѕР»РѕСЃС‹ ---
        double opacity = System.Math.Clamp(bar.BackgroundOpacity, 0.0, 1.0);
        var bgBase = ParseColor(Pick(cfg.Theme.Background, _palette.Surface));
        // margin_ends > 0 (РїРѕР»РѕСЃР° В«РІСЂРµР·РєРѕР№В») вЂ” С„РѕРЅ С‡СѓС‚СЊ РїСЂРѕР·СЂР°С‡РЅРµРµ, РєР°Рє РІ РѕСЂРёРіРёРЅР°Р»Рµ
        var bgColor = ApplyOpacity(bgBase, opacity);
        BackgroundBrush = Freeze(bgColor);
        _acrylicArgb = (uint)((bgColor.A << 24) | (bgColor.R << 16) | (bgColor.G << 8) | bgColor.B);

        double borderWidth = System.Math.Max(0, bar.BorderWidth);
        BarBorderThickness = new Thickness(borderWidth);
        BarBorderBrush = borderWidth > 0
            ? Freeze(ApplyAlpha(RoleColor(bar.Border, _palette, _palette.Outline), 0.8))
            : Brushes.Transparent;

        int sidePad = (int)System.Math.Round(System.Math.Max(0, bar.Padding) * bar.Scale);
        StartPadding = new Thickness(sidePad, 0, 0, 0);
        EndPadding = new Thickness(0, 0, sidePad, 0);

        Height = System.Math.Max(24, System.Math.Round(bar.Thickness * bar.Scale));

        // С‚РµРЅСЊ РїРѕРґ РїРѕР»РѕСЃРѕР№ ([shell.shadow])
        RootBorder.Effect = bar.Shadow && !bar.AutoHide
            ? new DropShadowEffect
            {
                Color = ParseColor(_palette.Shadow),
                BlurRadius = 18,
                ShadowDepth = IsBottom ? -3 : 3,
                Opacity = 0.55,
                Direction = IsBottom ? 270 : 90,
            }
            : null;

        // --- Р¶РёСЂРЅРѕСЃС‚СЊ/СЂР°Р·РјРµСЂ С€СЂРёС„С‚Р° (font_weight, font_scale) ---
        ApplyFont(bar);

        // --- РєРѕРјРїРѕРЅРѕРІРєР° РІРёРґР¶РµС‚РѕРІ start/center/end ---
        ApplyLayout(bar);

        ApplyTooltips(bar.ShowTooltips);
        UpdateAutoHide(bar);

        Dispatcher.BeginInvoke(() =>
        {
            if (_hwnd == IntPtr.Zero) return;
            PositionBar();
            EnableAcrylic();
            ApplyBarRegion();
            if (bar.ReserveSpace) ReserveWorkArea();
            else ReleaseWorkArea();
        });
    }

    /// <summary>Р–РёСЂРЅРѕСЃС‚СЊ Рё РјР°СЃС€С‚Р°Р± С‚РµРєСЃС‚Р°: font_weight (CSS 100вЂ“1000) Рё font_scale.</summary>
    private void ApplyFont(BarConfig bar)
    {
        var weight = CssToFontWeight(bar.FontWeight);
        double scale = System.Math.Clamp(bar.FontScale, 0.2, 2.5) * System.Math.Clamp(bar.Scale, 0.2, 2.5);

        foreach (var el in new[]
                 {
                     _clockTime, _clockDate, _cpuText, _ramText, _netText, _batteryText,
                     _volumeText, _mediaText, _titleText,
                 })
        {
            if (el == null) continue;
            el.FontWeight = weight;
            double baseSize = el == _clockTime ? 13.5 : 12.5;
            el.FontSize = System.Math.Round(baseSize * scale, 1);
        }

        foreach (var icon in new[] { _batteryIcon, _volumeIcon, _netIcon, _mediaIcon, _notifIcon })
        {
            if (icon == null) continue;
            icon.FontSize = System.Math.Round(15 * System.Math.Clamp(bar.Scale, 0.2, 2.5), 1);
        }

        foreach (var btn in new[] { _launcherButton, _controlCenterButton, _sessionButton, _trayButton })
        {
            if (btn?.Content is TextBlock tb)
                tb.FontSize = System.Math.Round(15 * System.Math.Clamp(bar.Scale, 0.2, 2.5), 1);
        }
    }

    private static FontWeight CssToFontWeight(int w) => w switch
    {
        <= 150 => FontWeights.Thin,
        <= 250 => FontWeights.ExtraLight,
        <= 350 => FontWeights.Light,
        <= 450 => FontWeights.Normal,
        <= 550 => FontWeights.Medium,
        <= 650 => FontWeights.DemiBold,
        <= 750 => FontWeights.Bold,
        <= 850 => FontWeights.ExtraBold,
        _ => FontWeights.Black,
    };

    /// <summary>Р Р°СЃСЃС‚Р°РІР»СЏРµС‚ РІРёРґР¶РµС‚С‹ РїРѕ С‚СЂС‘Рј Р·РѕРЅР°Рј СЃРѕРіР»Р°СЃРЅРѕ СЃРїРёСЃРєР°Рј start/center/end.</summary>
    private void ApplyLayout(BarConfig bar)
    {
        static List<string> Clean(List<string> src) =>
            src == null ? new List<string>()
                : new List<string>(src.Where(s => !string.IsNullOrWhiteSpace(s)));

        var start = Clean(bar.Start);
        var center = Clean(bar.Center);
        var end = Clean(bar.End);

        // Р·Р°РіРѕР»РѕРІРѕРє РѕРєРЅР° вЂ” РѕРїС†РёРѕРЅР°Р»СЊРЅС‹Р№, РґРѕР±Р°РІР»СЏРµС‚СЃСЏ РІ РєРѕРЅРµС† start, РµСЃР»Рё РІРєР»СЋС‡С‘РЅ
        if (bar.ShowWindowTitle && !start.Contains("title", System.StringComparer.OrdinalIgnoreCase))
            start.Add("title");

        StartPanel.Children.Clear();
        CenterPanel.Children.Clear();
        EndPanel.Children.Clear();

        // widget_spacing = 3 DIP СЃ РєР°Р¶РґРѕР№ СЃС‚РѕСЂРѕРЅС‹, С‡С‚РѕР±С‹ СЃСѓРјРјР°СЂРЅС‹Р№ Р·Р°Р·РѕСЂ СЃРѕРІРїР°Р»
        int gap = System.Math.Max(0, (int)System.Math.Round(bar.WidgetSpacing / 2.0));
        var margin = new Thickness(gap, 0, gap, 0);

        void Place(string key, StackPanel panel)
        {
            if (!_widgets.TryGetValue(key.Trim(), out var el)) return;
            if (el.Parent is Panel old && old != panel) old.Children.Remove(el);
            el.Margin = margin;
            panel.Children.Add(el);
        }

        foreach (var k in start) Place(k, StartPanel);
        foreach (var k in center) Place(k, CenterPanel);
        foreach (var k in end) Place(k, EndPanel);
    }

    /// <summary>Р’РєР»СЋС‡Р°РµС‚/РІС‹РєР»СЋС‡Р°РµС‚ С‚СѓР»С‚РёРїС‹, СЃРѕС…СЂР°РЅСЏСЏ РёСЃС…РѕРґРЅС‹Рµ С‚РµРєСЃС‚С‹.</summary>
    private void ApplyTooltips(bool show)
    {
        foreach (var el in _widgets.Values)
        {
            if (!_toolTips.ContainsKey(el)) _toolTips[el] = el.ToolTip;
            el.ToolTip = show ? _toolTips[el] : null;
        }
    }

    private static Color ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Colors.Transparent;
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
        Color.FromArgb((byte)System.Math.Round(c.A * System.Math.Clamp(opacity, 0, 1)), c.R, c.G, c.B);

    private static Color ApplyAlpha(Color c, double alpha) =>
        Color.FromArgb((byte)System.Math.Round(255 * System.Math.Clamp(alpha, 0, 1)), c.R, c.G, c.B);

    /// <summary>РЎРјРµС€РµРЅРёРµ РґРІСѓС… С†РІРµС‚РѕРІ; t=0 в†’ a, t=1 в†’ b.</summary>
    private static Color Mix(Color a, Color b, double t)
    {
        t = System.Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)System.Math.Round(a.A * (1 - t) + b.A * t),
            (byte)System.Math.Round(a.R * (1 - t) + b.R * t),
            (byte)System.Math.Round(a.G * (1 - t) + b.G * t),
            (byte)System.Math.Round(a.B * (1 - t) + b.B * t));
    }

    private Brush SurfaceBrush => Resources["SurfaceBrush"] as Brush ?? ParseBrush("#F21A1B26");
    private Brush OutlineBrush => Resources["OutlineBrush"] as Brush ?? ParseBrush("#33FFFFFF");
    private Brush TextBrush => Resources["TextColor"] as Brush ?? Brushes.White;
    private Brush DimBrush => Resources["DimColor"] as Brush ?? Brushes.Gray;
    private Brush AccentBrush => Resources["AccentBrush"] as Brush ?? Brushes.White;
    private Brush OnAccentBrush => Resources["OnAccentBrush"] as Brush ?? Brushes.Black;

    private void PositionBar()
    {
        var bar = ConfigService.Instance.Config.Bar;

        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);

        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        int marginEnds = System.Math.Max(0, bar.MarginEnds);
        double width = System.Math.Max(200, screenW - marginEnds * 2);
        Left = marginEnds;
        Width = width;
        Height = System.Math.Max(24, System.Math.Round(bar.Thickness * bar.Scale));

        double edge = System.Math.Max(0, bar.MarginEdge);
        _normalTop = IsBottom ? screenH - Height - edge : edge;
        Top = _barHidden ? HiddenTop() : _normalTop;
    }

    private double HiddenTop() =>
        IsBottom ? SystemParameters.PrimaryScreenHeight + 1 : -(Height + 1);

    private void EnableAcrylic()
    {
        if (_hwnd == IntPtr.Zero) return;
        var bar = ConfigService.Instance.Config.Bar;
        NativeMethods.EnableAcrylic(_hwnd, _acrylicArgb, bar.Blur);
    }

    /// <summary>Р¤РѕСЂРјР° РѕРєРЅР°: СЃРєСЂСѓРіР»РµРЅРёСЏ РїРѕ СѓРіР»Р°Рј Рё, РїСЂРё concave_edge_corners,
    /// РІРѕРіРЅСѓС‚С‹Рµ СѓРіР»С‹ СЃРѕ СЃС‚РѕСЂРѕРЅС‹ РєСЂР°СЏ СЌРєСЂР°РЅР° (РєР°Рє carve РІ Noctalia).</summary>
    private void ApplyBarRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        var bar = ConfigService.Instance.Config.Bar;

        double px = PixelsPerDip();
        int w = (int)System.Math.Round(Width * px);
        int h = (int)System.Math.Round(Height * px);

        // СѓРіР»С‹ РІ С„РёР·РёС‡РµСЃРєРёС… РїРёРєСЃРµР»СЏС…
        int TopLeft = Radius(bar.RadiusTopLeft, bar);
        int TopRight = Radius(bar.RadiusTopRight, bar);
        int BottomLeft = Radius(bar.RadiusBottomLeft, bar);
        int BottomRight = Radius(bar.RadiusBottomRight, bar);

        // РІРѕРіРЅСѓС‚РѕСЃС‚СЊ РІРѕР·РјРѕР¶РЅР° С‚РѕР»СЊРєРѕ РїСЂРё margin_edge = 0, РєР°Рє РІ РѕСЂРёРіРёРЅР°Р»Рµ
        bool concave = bar.ConcaveEdgeCorners && System.Math.Max(0, bar.MarginEdge) == 0;

        NativeMethods.SetBarRegion(
            _hwnd, w, h,
            (int)System.Math.Round(TopLeft * px),
            (int)System.Math.Round(TopRight * px),
            (int)System.Math.Round(BottomRight * px),
            (int)System.Math.Round(BottomLeft * px),
            concave,
            concaveTop: !IsBottom);

        int Radius(int? perCorner, BarConfig b) =>
            System.Math.Max(0, perCorner ?? b.CornerRadius);
    }

    private double PixelsPerDip()
    {
        try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
        catch { return 1.0; }
    }

    // ================= Р Р°Р±РѕС‡Р°СЏ РѕР±Р»Р°СЃС‚СЊ =================

    private void ReserveWorkArea()
    {
        var bar = ConfigService.Instance.Config.Bar;
        _savedWorkArea = ScreenWorkArea();
        _workAreaSaved = true;
        ApplyWorkArea(ComputeReservedArea(bar));
    }

    /// <summary>РџРѕР»РЅР°СЏ СЂР°Р±РѕС‡Р°СЏ РѕР±Р»Р°СЃС‚СЊ РѕСЃРЅРѕРІРЅРѕРіРѕ СЌРєСЂР°РЅР° РІ РїРёРєСЃРµР»СЏС… (Р±РµР· РЅР°С€РµРіРѕ СЂРµР·РµСЂРІР°).</summary>
    private NativeMethods.RECT ScreenWorkArea()
    {
        double scale = PixelsPerDip();
        int w = (int)System.Math.Round(SystemParameters.PrimaryScreenWidth * scale);
        int h = (int)System.Math.Round(SystemParameters.PrimaryScreenHeight * scale);

        var area = new NativeMethods.RECT { Left = 0, Top = 0, Right = w, Bottom = h };

        if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref _taskbarData) != IntPtr.Zero)
        {
            var tb = _taskbarData.rc;
            int tbEdge = (int)_taskbarData.uEdge;
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
        // СЂРµР·РµСЂРІ = margin_edge + thickness, РІ С„РёР·РёС‡РµСЃРєРёС… РїРёРєСЃРµР»СЏС…
        int reserve = (int)System.Math.Ceiling(
            (System.Math.Max(0, bar.MarginEdge) + Height) * PixelsPerDip());

        var area = _savedWorkArea;
        if (IsBottom)
            area.Bottom = System.Math.Max(area.Top + 1, area.Bottom - reserve);
        else
            area.Top = System.Math.Min(area.Bottom - 1, area.Top + reserve);
        return area;
    }

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

    // ================= РђРІС‚Рѕ-СЃРєСЂС‹С‚РёРµ =================

    private void UpdateAutoHide(BarConfig bar)
    {
        bool autoHide = bar.AutoHide || bar.SmartAutoHide;
        if (autoHide)
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
        try { AutoHideTickCore(); }
        catch { /* С‚РёРє Р°РІС‚Рѕ-СЃРєСЂС‹С‚РёСЏ РЅРµ РґРѕР»Р¶РµРЅ СЂРѕРЅСЏС‚СЊ РїР°РЅРµР»СЊ */ }
    }

    private void AutoHideTickCore()
    {
        var bar = ConfigService.Instance.Config.Bar;
        if (!(bar.AutoHide || bar.SmartAutoHide) || Visibility != Visibility.Visible) return;
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
            double wouldTop = IsBottom
                ? SystemParameters.PrimaryScreenHeight - Height - System.Math.Max(0, bar.MarginEdge)
                : System.Math.Max(0, bar.MarginEdge);
            bool inZone = y >= wouldTop - 3 && y <= wouldTop + Height + 3
                       && x >= Left - 3 && x <= Left + Width + 3;
            if (inZone) ShowBar(animate: true);
            return;
        }

        // СЃРјР°СЂС‚-Р°РІС‚РѕСЃРєСЂС‹С‚РёРµ: РїР°РЅРµР»СЊ РІРёРґРЅР°, С‚РѕР»СЊРєРѕ РєРѕРіРґР° РЅРµС‚ РѕРєРѕРЅ РЅР° СЃС‚РѕР»Рµ
        if (bar.SmartAutoHide && !bar.AutoHide)
        {
            bool hasWindow = HasWindowsOnCurrentDesktop();
            if (!hasWindow && _peekUntilUtc != DateTime.MinValue && DateTime.UtcNow < _peekUntilUtc)
                return;
            if (hasWindow) { HideBar(); return; }
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

    private bool HasWindowsOnCurrentDesktop()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == _hwnd) return false;
            if (!NativeMethods.IsWindowVisible(hwnd)) return false;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            return proc.ProcessName is not ("explorer" or "SearchHost" or "ShellExperienceHost" or "NoctaliaWin");
        }
        catch { return false; }
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

    private void Peek() // РєСЂР°С‚РєРѕ РїРѕРєР°Р·Р°С‚СЊ РїСЂРё СЃРјРµРЅРµ СЂР°Р±РѕС‡РµРіРѕ СЃС‚РѕР»Р°
    {
        var bar = ConfigService.Instance.Config.Bar;
        if (!(bar.AutoHide || bar.SmartAutoHide) || !bar.ShowOnWorkspaceSwitch) return;
        _peekUntilUtc = DateTime.UtcNow.AddSeconds(2);
        ShowBar(animate: true);
    }

    private void MoveTop(double top, bool animate)
    {
        var bar = ConfigService.Instance.Config.Bar;
        double speed = System.Math.Clamp(bar.AnimationSpeed, 0.2, 5.0);
        if (animate && bar.Animations && IsLoaded)
        {
            var anim = new DoubleAnimation(top, TimeSpan.FromMilliseconds(220 / speed))
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

    // ================= Popup'С‹ =================

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
            PopupAnimation = cfg.Bar.Animations ? PopupAnimation.Slide : PopupAnimation.None,
        };
        popup.IsOpen = true;
        return popup;
    }

    /// <summary>РћР±С‰Р°СЏ В«РєР°СЂС‚РѕС‡РєР°В» popup'Р°: surface + outline + СЂР°РґРёСѓСЃ, РєР°Рє РїР°РЅРµР»Рё Noctalia.</summary>
    private Border MakeCard(FrameworkElement content, double padding = 14, double width = double.NaN)
    {
        var card = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(padding),
            Child = content,
        };
        if (!double.IsNaN(width)) card.Width = width;
        return card;
    }

    // ================= РўСЂРёРіРіРµСЂС‹ Р·РѕРЅ =================

    private void StartPanel_RightClick(object sender, MouseButtonEventArgs e)
    {
        // РїСЂР°РІС‹Р№ РєР»РёРє РїРѕ РѕС‚СЃС‚СѓРїСѓ: РєР°Рє dead_zone РІ Noctalia
        OpenDeadZone("left");
        e.Handled = true;
    }

    private void EndPanel_RightClick(object sender, MouseButtonEventArgs e)
    {
        OpenDeadZone("right");
        e.Handled = true;
    }

    private void OpenDeadZone(string side)
    {
        var bar = ConfigService.Instance.Config.Bar;
        string action = side == "left" ? bar.DeadZoneLeft : bar.DeadZoneRight;
        RunAction(action, _endPanelTarget());
    }

    private FrameworkElement _endPanelTarget() =>
        EndPanel.Children.Count > 0 ? (FrameworkElement)EndPanel.Children[^1] : this;

    private void RunAction(string action, FrameworkElement target)
    {
        switch ((action ?? "none").Trim().ToLowerInvariant())
        {
            case "control-center": QuickSettings_Click(null, null); break;
            case "launcher": Launcher_Click(null, null); break;
            case "session": Session_Click(null, null); break;
            case "settings": OpenSettings(); break;
            case "none":
            default: break;
        }
    }

    // ================= Р§Р°СЃС‹ =================

    private void UpdateClock()
    {
        var cfg = ConfigService.Instance.Config;
        _clockTime.Text = DateTime.Now.ToString(cfg.Clock.TimeFormat);
        _clockDate.Text = DateTime.Now.ToString(cfg.Clock.DateFormat);
    }

    private void Clock_Click(object sender, RoutedEventArgs e)
    {
        if (_calendarPopup != null && _calendarPopup.IsOpen)
        {
            _calendarPopup.IsOpen = false;
            return;
        }

        // РєР°Р»РµРЅРґР°СЂСЊ РІ СЃС‚РёР»Рµ Noctalia: С€Р°РїРєР° + Р±РѕР»СЊС€Р°СЏ РґР°С‚Р° + СЃРµС‚РєР°
        var stack = new StackPanel { Width = 268 };

        var header = new TextBlock
        {
            Text = DateTime.Now.ToString("dddd, d MMMM"),
            Foreground = TextBrush,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(2, 0, 0, 8),
        };
        stack.Children.Add(header);

        var calendar = new Calendar
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = TextBrush,
        };
        stack.Children.Add(calendar);

        var card = MakeCard(stack, 14);
        card.CornerRadius = new CornerRadius(12);
        _calendarPopup = OpenPopup(card, _clockButton);
    }

    // ================= Р Р°Р±РѕС‡РёРµ СЃС‚РѕР»С‹ =================

    private void RefreshWorkspaces(bool force = false)
    {
        if (!_vds.IsAvailable)
        {
            if (_workspacesPanel.Children.Count > 0) _workspacesPanel.Children.Clear();
            return;
        }

        int count = _vds.GetCount();
        int current = _vds.GetCurrentIndex();

        if (!force && count == _lastDesktopCount && current == _lastDesktopIndex) return;

        bool changed = _lastDesktopIndex >= 0 && current != _lastDesktopIndex;
        _lastDesktopCount = count;
        _lastDesktopIndex = current;

        _workspacesPanel.Children.Clear();

        bool tips = ConfigService.Instance.Config.Bar.ShowTooltips;
        var accent = AccentBrush;
        var onAccent = OnAccentBrush;
        var dim = DimBrush;
        var idle = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

        for (int i = 0; i < count; i++)
        {
            int index = i;
            bool active = i == current;

            // РїРёР»СЋР»СЏ СЂР°Р±РѕС‡РµРіРѕ СЃС‚РѕР»Р°: Р°РєС‚РёРІРЅР°СЏ вЂ” Р·Р°Р»РёРІРєР° primary, РѕСЃС‚Р°Р»СЊРЅС‹Рµ вЂ” РїСЂРѕР·СЂР°С‡РЅС‹Рµ
            var pill = new Border
            {
                Style = (Style)FindResource("WsDot"),
                Background = active ? accent : Brushes.Transparent,
                MinWidth = active ? 26 : 22,
                ToolTip = tips ? $"Р Р°Р±РѕС‡РёР№ СЃС‚РѕР» {i + 1}" : null,
            };
            pill.Child = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 11.5,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium,
                Foreground = active ? onAccent : dim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            pill.MouseLeftButtonUp += (_, _) => _vds.SwitchTo(index);
            _workspacesPanel.Children.Add(pill);
        }

        if (count < 10)
        {
            var addBtn = new Border
            {
                Style = (Style)FindResource("WsDot"),
                Background = Brushes.Transparent,
                ToolTip = tips ? "РЎРѕР·РґР°С‚СЊ СЂР°Р±РѕС‡РёР№ СЃС‚РѕР»" : null,
            };
            addBtn.Child = new TextBlock
            {
                Text = "+",
                FontSize = 13,
                Foreground = dim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            addBtn.MouseLeftButtonUp += (_, _) => _vds.Create();
            _workspacesPanel.Children.Add(addBtn);
        }

        if (changed) Peek();
    }

    // ================= РђРєС‚РёРІРЅРѕРµ РѕРєРЅРѕ =================

    private void RefreshWindowTitle()
    {
        if (_titleWidget == null) return;
        if (_titleWidget.Parent == null) { _titleText.Text = ""; return; }

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == _hwnd || !NativeMethods.IsWindowVisible(hwnd))
        {
            _titleText.Text = "";
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

        _titleText.Text = title;
    }

    // ================= РЎС‚Р°С‚РёСЃС‚РёРєР° =================

    private void UpdateStats()
    {
        _stats.Refresh();
        RefreshWindowTitle();

        _cpuText.Text = $"{(int)_stats.CpuPercent}%";
        _ramText.Text = $"{_stats.RamUsedGb}/{_stats.RamTotalGb} GB";

        // СЃРµС‚СЊ: СЃРєРѕСЂРѕСЃС‚СЊ РїСЂРёС‘РјР°/РїРµСЂРµРґР°С‡Рё
        if (_netText != null && _netWidget != null)
        {
            _netText.Text = _stats.NetText ?? "";
            _netWidget.Visibility = string.IsNullOrEmpty(_netText.Text)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_stats.HasBattery)
        {
            _batteryWidget.Visibility = Visibility.Visible;
            _batteryIcon.Text = _stats.Charging ? "\uE898" : BatteryGlyph(_stats.BatteryPercent);
            _batteryText.Text = $"{_stats.BatteryPercent}%";
            _batteryIcon.Foreground = _stats.BatteryPercent <= 20 && !_stats.Charging
                ? Freeze(ParseColor(_palette.Error))
                : TextBrush;
        }
        else if (_batteryWidget != null)
        {
            _batteryWidget.Visibility = Visibility.Collapsed;
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

    // ================= РњРµРґРёР° (Р»СѓС‡С€РµРµ РґРѕСЃС‚СѓРїРЅРѕРµ РѕРєРЅРѕ) =================

    private void UpdateMedia()
    {
        if (_mediaWidget == null) return;

        // РІ Windows РЅРµС‚ MPRIS: РїРѕРєР°Р·С‹РІР°РµРј Р°РєС‚РёРІРЅРѕРµ РјРµРґРёР°-РѕРєРЅРѕ, РµСЃР»Рё РѕРЅРѕ РµСЃС‚СЊ
        string title = "";
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(hwnd))
        {
            var sb = new System.Text.StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                string name = proc.ProcessName;
                if (name is "Spotify" or "SpotifyAB.SpotifyMusic" or "MusicBee" or "foobar2000"
                    or "vlc" or "wmplayer" or "AIMP" or "Dopamine" or "YouTube Music")
                {
                    title = sb.ToString();
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            _mediaWidget.Visibility = Visibility.Collapsed;
            return;
        }

        _mediaWidget.Visibility = Visibility.Visible;
        _mediaIcon.Text = "\uE768";
        _mediaText.Text = title;
    }

    // ================= Р“СЂРѕРјРєРѕСЃС‚СЊ =================

    private void UpdateVolumeUI()
    {
        if (_volumeButton == null) return;

        if (!_audio.IsAvailable)
        {
            _volumeText.Text = "";
            _volumeIcon.Text = "\uE74F";
            return;
        }

        bool muted = _audio.GetMute();
        float vol = _audio.GetVolume();

        _volumeIcon.Text = muted || vol <= 0f ? "\uE74F" : (vol < 0.34f ? "\uE992" : vol < 0.67f ? "\uE993" : "\uE767");
        _volumeText.Text = muted ? "вЂ”" : $"{(int)(vol * 100)}%";

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

        var stack = new StackPanel { Width = 230 };

        stack.Children.Add(new TextBlock
        {
            Text = "Р“СЂРѕРјРєРѕСЃС‚СЊ",
            Foreground = TextBrush,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 6),
        });

        _volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = _audio.GetVolume() * 100,
            Margin = new Thickness(0, 4, 0, 8),
        };
        _volumeSlider.ValueChanged += (_, _) => _audio.SetVolume((float)(_volumeSlider.Value / 100.0));
        stack.Children.Add(_volumeSlider);

        var muteBtn = MakeCardButton("Р’С‹РєР»СЋС‡РёС‚СЊ Р·РІСѓРє", () => { _audio.ToggleMute(); UpdateVolumeUI(); });
        stack.Children.Add(muteBtn);

        _volumePopup = OpenPopup(MakeCard(stack), _volumeButton);
    }

    private Button MakeCardButton(string label, Action action)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 2, 0, 2),
            Background = Resources["SurfaceVariantBrush"] as Brush ?? Brushes.Transparent,
            Foreground = TextBrush,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Template = MakeRoundTemplate(),
        };
        btn.Click += (_, _) => action();
        return btn;
    }

    private ControlTemplate MakeRoundTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };

        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82));
        template.Triggers.Add(hover);

        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.65));
        template.Triggers.Add(pressed);

        return template;
    }

    // ================= Р›Р°СѓРЅС‡РµСЂ =================

    private void Launcher_Click(object sender, RoutedEventArgs e)
    {
        if (_quickPopup != null && _quickPopup.IsOpen)
        {
            _quickPopup.IsOpen = false;
            return;
        }

        // Р›Р°СѓРЅС‡РµСЂ РІ СЃС‚РёР»Рµ Noctalia: РїРѕР»Рµ РїРѕРёСЃРєР° + СЃРїРёСЃРѕРє РїСЂРёР»РѕР¶РµРЅРёР№ РёР· РјРµРЅСЋ В«РџСѓСЃРєВ»
        var apps = LoadStartMenuApps();

        var stack = new StackPanel { Width = 420 };

        var search = new TextBox
        {
            Background = Resources["SurfaceVariantBrush"] as Brush ?? Brushes.Transparent,
            Foreground = TextBrush,
            CaretBrush = TextBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 8, 10, 8),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 10),
        };
        stack.Children.Add(search);

        var list = new StackPanel();
        void Fill(string q)
        {
            list.Children.Clear();
            var shown = apps
                .Where(a => string.IsNullOrWhiteSpace(q) ||
                            a.Name.Contains(q, System.StringComparison.OrdinalIgnoreCase))
                .Take(40);
            foreach (var app in shown)
            {
                var b = MakeCardButton(app.Name, () =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = app.Path, UseShellExecute = true,
                        });
                    }
                    catch { }
                    if (_quickPopup != null) _quickPopup.IsOpen = false;
                });
                list.Children.Add(b);
            }
        }
        Fill("");
        search.TextChanged += (_, _) => Fill(search.Text);

        stack.Children.Add(new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
            Background = Brushes.Transparent,
        });

        if (apps.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "РџСЂРёР»РѕР¶РµРЅРёСЏ РЅРµ РЅР°Р№РґРµРЅС‹ РІ РјРµРЅСЋ В«РџСѓСЃРєВ».",
                Foreground = DimBrush,
                Margin = new Thickness(2, 4, 0, 0),
            });
        }

        _quickPopup = OpenPopup(MakeCard(stack, 14), _launcherButton);
        search.Focus();
    }

    private readonly record struct AppEntry(string Name, string Path);

    private List<AppEntry> LoadStartMenuApps()
    {
        var result = new List<AppEntry>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        var dirs = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
        };

        foreach (var dir in dirs)
        {
            try
            {
                if (!System.IO.Directory.Exists(dir)) continue;
                foreach (var lnk in System.IO.Directory.EnumerateFiles(dir, "*.lnk", System.IO.SearchOption.AllDirectories))
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(lnk);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (name.StartsWith("Uninstall", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(name)) result.Add(new AppEntry(name, lnk));
                }
            }
            catch { }
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    // ================= Р‘С‹СЃС‚СЂС‹Рµ РЅР°СЃС‚СЂРѕР№РєРё (control center) =================

    // ================= Control center (РїР°РЅРµР»СЊ РІРєР»Р°РґРѕРє РєР°Рє РІ РѕСЂРёРіРёРЅР°Р»Рµ) =================

    private ControlCenterWindow _controlCenter;

    private void QuickSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_controlCenter != null)
        {
            _controlCenter.Close();
            _controlCenter = null;
            return;
        }

        var cc = new ControlCenterWindow(_stats, _audio);
        cc.ApplyPalette(
            Freeze(ApplyOpacity(ParseColor(Pick(ConfigService.Instance.Config.Theme.Background, _palette.Surface)), 0.98)),
            Freeze(ApplyOpacity(ParseColor(_palette.Surface), 0.98)),
            Freeze(ApplyOpacity(ParseColor(_palette.SurfaceVariant), 0.98)),
            Freeze(ParseColor(_palette.OnSurface)),
            Freeze(ParseColor(_palette.OnSurfaceVariant)),
            Freeze(ParseColor(_palette.Primary)),
            Freeze(ParseColor(_palette.OnPrimary)),
            Freeze(ParseColor(_palette.Secondary)),
            Freeze(ParseColor(_palette.Outline)),
            Freeze(ApplyOpacity(Mix(ParseColor(_palette.Surface), ParseColor(_palette.Hover), 0.55), 0.9)));
        cc.OpenSettingsRequested += OpenSettings;
        cc.Closed += (_, _) => _controlCenter = null;
        _controlCenter = cc;
        cc.Show();
        cc.Activate();
    }

    private CheckBox MakeToggle(string label, bool isChecked, Action<bool> onChange)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = isChecked,
            Foreground = TextBrush,
            Margin = new Thickness(2, 4, 0, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        box.Checked += (_, _) => onChange(true);
        box.Unchecked += (_, _) => onChange(false);
        return box;
    }

    private TextBlock MakeHeader(string text) => new()
    {
        Text = text,
        Foreground = DimBrush,
        FontSize = 11.5,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(2, 6, 0, 2),
    };

    private void Notifications_Click()
    {
        if (_quickPopup != null && _quickPopup.IsOpen)
        {
            _quickPopup.IsOpen = false;
            return;
        }

        var stack = new StackPanel { Width = 300 };
        stack.Children.Add(MakeHeader("РЈРІРµРґРѕРјР»РµРЅРёСЏ"));

        // РІ Windows РЅРµС‚ daemon-Р° СѓРІРµРґРѕРјР»РµРЅРёР№: РїРѕРєР°Р·С‹РІР°РµРј С‚РѕР»СЊРєРѕ РєРЅРѕРїРєСѓ С†РµРЅС‚СЂР°
        stack.Children.Add(new TextBlock
        {
            Text = "Р¦РµРЅС‚СЂ СѓРІРµРґРѕРјР»РµРЅРёР№ Windows",
            Foreground = DimBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 2, 0, 8),
        });
        stack.Children.Add(MakeCardButton("РћС‚РєСЂС‹С‚СЊ С†РµРЅС‚СЂ СѓРІРµРґРѕРјР»РµРЅРёР№", () =>
        {
            if (_quickPopup != null) _quickPopup.IsOpen = false;
            NativeMethods.OpenActionCenter();
        }));

        _quickPopup = OpenPopup(MakeCard(stack), _notifWidget);
    }

    private void Session_Click(object sender, RoutedEventArgs e)
    {
        QuickSettings_Click(sender, e);
    }

    private void OpenTrayFallback()
    {
        // Р·Р°РїР°СЃРЅРѕР№ РїСѓС‚СЊ: РµСЃР»Рё РёРєРѕРЅРєР° С‚СЂРµСЏ СЃРєСЂС‹С‚Р°, РїРѕРєР°Р·Р°С‚СЊ РјРµРЅСЋ Сѓ РµС‘ РјРµСЃС‚Р°
        OpenSettings();
    }

    // ================= РќР°СЃС‚СЂРѕР№РєРё =================

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
