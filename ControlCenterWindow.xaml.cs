using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using NoctaliaWin.Interop;
using NoctaliaWin.Services;

namespace NoctaliaWin;

/// <summary>
/// Control center — копия панели оригинала Noctalia
/// (src/shell/control_center/control_center_panel.cpp).
///
/// Структура повторяет оригинал:
///   • панель 700×520, скругление radiusXl (12), обводка outline шириной 1;
///   • слева сайдбар (surface_variant, radiusXl) со списком вкладок
///     (компактный режим — только иконки, как sidebar_mode = compact);
///   • справа колонка: шапка (заголовок вкладки primary fontSizeTitle + действия)
///     и тело вкладки;
///   • вкладка home: карточка пользователя (аватар + имя/хост/версия),
///     карточка медиа, карточка даты/времени и сетка быстрых ярлыков в 2 колонки.
///
/// Размеры и зазоры — точные значения из src/ui/style.h:
/// radiusXl = 12, radiusLg = 9, spaceXs/Sm/Md/Lg = 4/8/12/16,
/// cardPadding = panelPadding = 14, controlHeightSm = 32, controlHeightLg = 44,
/// fontSizeMini/Caption/Body/Title = 11/13/14/16.
/// Дефолты control center — width 700, preferredHeight = scaled(520),
/// sidebar_mode = compact, shortcuts = wifi/bluetooth/caffeine/nightlight/notification/power_profile.
/// </summary>
public sealed class ControlCenterWindow : Window
{
    // ── Style из оригинала ───────────────────────────────────────────────────
    private const double RadiusLg = 9, RadiusXl = 12;
    private const double BorderWidth = 1;
    private const double SpaceXs = 4, SpaceSm = 8, SpaceMd = 12, SpaceLg = 16;
    private const double CardPadding = 14, PanelPadding = 14;
    private const double FontSizeMini = 11, FontSizeCaption = 13, FontSizeBody = 14;
    private const double FontSizeTitle = 16;
    private const double ControlHeightSm = 32, ControlHeightLg = 44;

    // Вкладки оригинала (kTabs в control_center_panel.h, порядок как в TabId).
    private static readonly (string Key, string Title, string Glyph)[] Tabs =
    {
        ("home", "Главная", "home"),
        ("media", "Медиа", "disc"),
        ("audio", "Звук", "volume-high"),
        ("monitor", "Экран", "brightness-high"),
        ("system", "Система", "cpu"),
        ("power", "Питание", "shutdown"),
        ("network", "Сеть", "wifi"),
        ("bluetooth", "Bluetooth", "bluetooth"),
        ("weather", "Погода", "cloud"),
        ("calendar", "Календарь", "calendar"),
        ("notifications", "Уведомления", "bell"),
        ("screen-time", "Экранное время", "hourglass"),
    };

    private readonly SystemStatsService _stats;
    private readonly AudioService _audio;
    private readonly MediaService _media;
    private readonly PanelConfig _cfg;

    private string _activeTab = "home";
    private readonly Dictionary<string, Button> _tabButtons = new();
    private TextBlock _title;
    private Grid _tabBodyHost;
    private StackPanel _headerActions;

    // Роли палитры — приходят из MainWindow (тот же пресет и режим).
    private Brush _surface, _surfaceVariant, _onSurface, _onSurfaceVariant;
    private Brush _primary, _onPrimary, _secondary, _outline, _hover;
    private Brush _background;

    private bool _caffeine;

    public event Action OpenSettingsRequested;

    public ControlCenterWindow(SystemStatsService stats, AudioService audio, MediaService media)
    {
        _stats = stats;
        _audio = audio;
        _media = media;
        _cfg = ConfigService.Instance.Config;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var cc = _cfg.ControlCenter;
        Width = cc.Width;
        Height = cc.Height;

        Build();
        Loaded += (_, _) => PositionNear();
    }

    /// <summary>Обновляет данные карточек (медиа/система) при открытии.</summary>
    public async void RefreshDataAsync()
    {
        _stats.Refresh();
        if (_media != null) await _media.RefreshAsync();
        RebuildTabBodies();
    }

    /// <summary>Применяет роли палитры, вычисленные в MainWindow.</summary>
    public void ApplyPalette(Brush background, Brush surface, Brush surfaceVariant, Brush onSurface,
        Brush onSurfaceVariant, Brush primary, Brush onPrimary, Brush secondary, Brush outline, Brush hover)
    {
        _background = background; _surface = surface; _surfaceVariant = surfaceVariant;
        _onSurface = onSurface; _onSurfaceVariant = onSurfaceVariant;
        _primary = primary; _onPrimary = onPrimary; _secondary = secondary;
        _outline = outline; _hover = hover;
        Build();
    }

    private static Brush B(Brush b, string fallback) =>
        b ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));

    // ── Построение ───────────────────────────────────────────────────────────

    private void Build()
    {
        var cc = _cfg.ControlCenter;

        var panel = new Border
        {
            Background = B(_background ?? _surface, "#F9070722"),
            BorderBrush = B(_outline, "#BF21215F"),
            BorderThickness = new Thickness(BorderWidth),
            CornerRadius = new CornerRadius(RadiusXl),
            Padding = new Thickness(PanelPadding),
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelPadding) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (cc.ShowSidebar)
        {
            var sidebar = BuildSidebar();
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);
        }

        root.Children.Add(Place(new Border(), 1));

        var content = BuildContent();
        Grid.SetColumn(content, 2);
        root.Children.Add(content);

        panel.Child = root;
        Content = panel;

        _tabBodyHost.Children.Clear();
        _tabBodyHost.Children.Add(BuildTab(_activeTab));
        SelectTab(_activeTab);
    }

    private static T Place<T>(T el, int col) where T : UIElement
    {
        Grid.SetColumn(el, col);
        return el;
    }

    /// <summary>Сайдбар: surface_variant, radiusXl, кнопки-вкладки (controlHeightSm = 32,
    /// radiusLg = 9). Компактный режим — только глиф 21px, как sidebar_mode = compact.</summary>
    private FrameworkElement BuildSidebar()
    {
        var cc = _cfg.ControlCenter;
        bool compact = cc.CompactSidebar;

        var stack = new StackPanel { Margin = new Thickness(SpaceMd) };
        _tabButtons.Clear();

        foreach (var (key, title, glyph) in Tabs)
        {
            if (IsTabHidden(key)) continue;

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(Glyph(glyph, 21, _onSurface));
            if (!compact)
            {
                row.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = FontSizeBody,
                    FontWeight = FontWeights.Bold,
                    Foreground = B(_onSurface, "#FFf3edf7"),
                    Margin = new Thickness(SpaceSm, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            var btn = new Button
            {
                Content = row,
                Height = ControlHeightSm,
                MinWidth = compact ? ControlHeightSm : 0,
                Padding = new Thickness(compact ? SpaceXs : SpaceSm, SpaceXs, compact ? SpaceXs : SpaceSm, SpaceXs),
                Margin = new Thickness(0, 0, 0, SpaceXs),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = B(_onSurface, "#FFf3edf7"),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ToolTip = compact ? title : null,
                Template = RoundHoverTemplate(RadiusLg),
            };
            string k = key;
            btn.Click += (_, _) => SelectTab(k);
            _tabButtons[key] = btn;
            stack.Children.Add(btn);
        }

        return new Border
        {
            Background = B(_surfaceVariant, "#F911112d"),
            CornerRadius = new CornerRadius(RadiusXl),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
            },
        };
    }

    /// <summary>Правый столбец: шапка + тело вкладки.</summary>
    private FrameworkElement BuildContent()
    {
        var cc = _cfg.ControlCenter;
        var col = new Grid();
        col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SpaceMd) });
        col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _title = new TextBlock
        {
            Text = TitleFor(_activeTab),
            FontSize = FontSizeTitle,
            FontWeight = FontWeights.Bold,
            Foreground = B(_primary, "#FFfff59b"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(Place(_title, 0));

        _headerActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (cc.ShowSessionButton)
            _headerActions.Children.Add(HeaderIconButton("shutdown", "Питание", ShowSessionActions));
        _headerActions.Children.Add(HeaderIconButton("settings", "Настройки", () => { Close(); OpenSettingsRequested?.Invoke(); }));
        _headerActions.Children.Add(HeaderIconButton("close", "Закрыть", Close));
        header.Children.Add(Place(_headerActions, 1));

        col.Children.Add(Place(header, 0, 0));

        _tabBodyHost = new Grid();
        _tabBodyHost.Children.Add(BuildTab(_activeTab));
        col.Children.Add(Place(_tabBodyHost, 0, 2));

        return col;
    }

    private static T Place<T>(T el, int row, int col) where T : UIElement
    {
        Grid.SetRow(el, row);
        Grid.SetColumn(el, col);
        return el;
    }

    private Button HeaderIconButton(string glyph, string tip, Action onClick)
    {
        var btn = new Button
        {
            Content = Glyph(glyph, 18, _onSurface),
            Width = ControlHeightSm,
            Height = ControlHeightSm,
            Margin = new Thickness(SpaceSm, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = B(_onSurface, "#FFf3edf7"),
            Cursor = Cursors.Hand,
            ToolTip = tip,
            Template = RoundHoverTemplate(RadiusLg),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private ControlTemplate RoundHoverTemplate(double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var t = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, B(_hover, "#33a9aefe")));
        t.Triggers.Add(hover);
        return t;
    }

    /// <summary>Иконка по имени Tabler (как в оригинале), с размером и цветом.</summary>
    private TextBlock Glyph(string iconName, double size, Brush color)
    {
        var tb = new TextBlock
        {
            Text = IconGlyphs.Glyph(iconName),
            FontSize = size,
            Foreground = B(color, "#FFf3edf7"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        IconFont.Apply(tb);
        return tb;
    }

    // ── Вкладки ──────────────────────────────────────────────────────────────

    private static string TitleFor(string key) =>
        Tabs.FirstOrDefault(t => t.Key == key).Title ?? "Главная";

    private bool IsTabHidden(string key) =>
        _cfg.ControlCenter.HiddenTabs?.Contains(key, StringComparer.OrdinalIgnoreCase) == true;

    private void SelectTab(string key)
    {
        if (IsTabHidden(key) || !Tabs.Any(t => t.Key == key)) key = "home";
        _activeTab = key;

        // chrome: активная вкладка = ButtonVariant::TabActive (заливка primary)
        foreach (var kv in _tabButtons)
        {
            bool active = kv.Key == key;
            kv.Value.Background = active ? B(_primary, "#FFfff59b") : Brushes.Transparent;
            kv.Value.Foreground = active ? B(_onPrimary, "#FF0e0e43") : B(_onSurface, "#FFf3edf7");
            if (kv.Value.Content is StackPanel sp && !_cfg.ControlCenter.CompactSidebar && sp.Children.Count > 1
                && sp.Children[1] is TextBlock lbl)
                lbl.Foreground = active ? B(_onPrimary, "#FF0e0e43") : B(_onSurface, "#FFf3edf7");
        }

        if (_title != null) _title.Text = TitleFor(key);
        RebuildTabBodies();
    }

    private void RebuildTabBodies()
    {
        if (_tabBodyHost == null) return;
        _tabBodyHost.Children.Clear();
        _tabBodyHost.Children.Add(BuildTab(_activeTab));
    }

    private FrameworkElement BuildTab(string key) => key switch
    {
        "home" => BuildHome(),
        "audio" => BuildAudio(),
        "network" => BuildNetwork(),
        "bluetooth" => BuildBluetooth(),
        "notifications" => BuildNotifications(),
        "calendar" => BuildCalendar(),
        "media" => BuildMedia(),
        "monitor" => BuildMonitor(),
        "system" => BuildSystem(),
        "power" => BuildPower(),
        "weather" => BuildWeather(),
        "screen-time" => BuildScreenTime(),
        _ => BuildHome(),
    };

    // ── Home tab (по home_tab.cpp) ───────────────────────────────────────────

    /// <summary>Home: userCard сверху, bottomRow = leftColumn(media над dateTime)
    /// + shortcutsGrid. Flex-гроу оригинала: main 1.66, shortcuts 1.0, media 1.4, dateTime 1.0.</summary>
    private FrameworkElement BuildHome()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2.6, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SpaceMd) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3.2, GridUnitType.Star) });

        root.Children.Add(Place(BuildUserCard(), 0, 0));

        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.66, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SpaceMd) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.4, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SpaceMd) });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
        left.Children.Add(Place(BuildMediaCard(), 0, 0));
        left.Children.Add(Place(BuildDateTimeCard(), 2, 0));
        bottom.Children.Add(Place(left, 0));

        bottom.Children.Add(Place(BuildShortcutGrid(), 2));

        root.Children.Add(Place(bottom, 0, 2));
        return root;
    }


    /// <summary>Карточка пользователя: аватар-круг (обводка primary×3), имя (bold, fontSizeTitle×1.12),
    /// хост/uptime/версия (fontSizeCaption, onSurfaceVariant).</summary>
    private FrameworkElement BuildUserCard()
    {
        var card = SectionCard();

        var dock = new Grid();
        dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        dock.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SpaceMd) });
        dock.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        double avatarSize = ControlHeightLg * 2.6; // kHomeAvatarScale = 2.6

        var avatar = new Border
        {
            Width = avatarSize, Height = avatarSize,
            CornerRadius = new CornerRadius(avatarSize / 2),
            Background = B(_surfaceVariant, "#F911112d"),
            BorderBrush = B(_primary, "#FFfff59b"),
            BorderThickness = new Thickness(BorderWidth * 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Initials(),
                FontSize = avatarSize * 0.34,
                FontWeight = FontWeights.Bold,
                Foreground = B(_primary, "#FFfff59b"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        dock.Children.Add(Place(avatar, 0));

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Line(Environment.UserName, FontSizeTitle * 1.12, _onSurface, FontWeights.Bold));
        info.Children.Add(Line($"{Environment.UserName}@{Environment.MachineName}", FontSizeCaption, _onSurfaceVariant));
        info.Children.Add(Line($"В работе {Uptime()}", FontSizeCaption, _onSurfaceVariant));
        info.Children.Add(Line("NoctaliaWin 0.4.2", FontSizeCaption, _onSurfaceVariant));
        dock.Children.Add(Place(info, 2));

        card.Child = dock;
        return card;
    }

    private static TextBlock Line(string text, double size, Brush color, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = B(color, "#FFf3edf7"),
        Margin = new Thickness(0, SpaceXs * 0.5, 0, 0),
    };

    private static string Initials()
    {
        var n = Environment.UserName;
        if (string.IsNullOrEmpty(n)) return "?";
        return n.Substring(0, 1).ToUpperInvariant();
    }

    private static string Uptime()
    {
        var t = TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays} д {t.Hours} ч";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ч {t.Minutes} мин";
        return $"{t.Minutes} мин";
    }

    /// <summary>Карточка медиа: обложка (или disc-заглушка), трек/артист/статус.
    /// Клик — вкладка media (addCardOverlay в оригинале).</summary>
    private FrameworkElement BuildMediaCard()
    {
        var card = SectionCard();
        card.Cursor = Cursors.Hand;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SpaceSm) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        double art = ControlHeightLg * 1.22;
        var artBox = new Border
        {
            Width = art, Height = art,
            CornerRadius = new CornerRadius(RadiusLg),
            Background = B(_surfaceVariant, "#F911112d"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var artImage = ArtworkImage(art);
        artBox.Child = artImage ?? (UIElement)Glyph("disc", art * 0.55, _onSurfaceVariant);
        row.Children.Add(Place(artBox, 0));

        bool available = _media?.Available == true;
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = available ? _media.Title : "Нет активного плеера",
            FontSize = FontSizeBody * 0.95,
            Foreground = B(_onSurface, "#FFf3edf7"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(new TextBlock
        {
            Text = available ? _media.Artist : "",
            FontSize = FontSizeCaption,
            Foreground = B(_onSurfaceVariant, "#FF7c80b4"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(new TextBlock
        {
            Text = available ? (_media.IsPlaying ? "играет" : "пауза") : "простой",
            FontSize = FontSizeCaption,
            Foreground = B(_onSurfaceVariant, "#FF7c80b4"),
        });
        row.Children.Add(Place(text, 2));
        card.Child = row;

        card.MouseLeftButtonUp += (_, _) => SelectTab("media");
        return card;
    }

    /// <summary>Обложка трека как Image, если <see cref="MediaService.Artwork"/> непустой.</summary>
    private System.Windows.Controls.Image ArtworkImage(double size)
    {
        var bytes = _media?.Artwork;
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.StreamSource = new System.IO.MemoryStream(bytes);
            bmp.DecodePixelWidth = (int)(size * 2);
            bmp.EndInit();
            bmp.Freeze();
            return new System.Windows.Controls.Image
            {
                Source = bmp,
                Stretch = Stretch.UniformToFill,
                Width = size, Height = size,
            };
        }
        catch { return null; }
    }

    /// <summary>Карточка даты/времени: время (bold, primary, fontSizeTitle×1.7) + дата.</summary>
    private FrameworkElement BuildDateTimeCard()
    {
        var card = SectionCard();

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var time = new TextBlock
        {
            Text = DateTime.Now.ToString(DotNetFormat(_cfg.Clock.TimeFormat, "HH:mm")),
            FontSize = FontSizeTitle * 1.7,
            FontWeight = FontWeights.Bold,
            Foreground = B(_primary, "#FFfff59b"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => time.Text = DateTime.Now.ToString(DotNetFormat(_cfg.Clock.TimeFormat, "HH:mm"));
        timer.Start();
        Closed += (_, _) => timer.Stop();
        row.Children.Add(time);

        var right = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(SpaceLg, 0, 0, 0),
        };
        right.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString(DotNetFormat(_cfg.Clock.DateFormat, "dddd, d MMMM")),
            FontSize = FontSizeBody * 0.9,
            Foreground = B(_onSurface, "#FFf3edf7"),
        });
        var weatherRow = new StackPanel { Orientation = Orientation.Horizontal };
        weatherRow.Children.Add(Glyph("cloud", FontSizeCaption * 1.12, _primary));
        weatherRow.Children.Add(new TextBlock
        {
            Text = "погода недоступна",
            FontSize = FontSizeCaption,
            Foreground = B(_onSurfaceVariant, "#FF7c80b4"),
            Margin = new Thickness(SpaceXs, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        right.Children.Add(weatherRow);
        row.Children.Add(right);

        card.Child = row;
        return card;
    }

    /// <summary>Сетка ярлыков: 2 колонки (kHomeShortcutGridColumns = 2), до 6 ярлыков,
    /// при ≤2 — одна колонка (kHomeStackedShortcutMax = 2).</summary>
    private FrameworkElement BuildShortcutGrid()
    {
        var shortcuts = (_cfg.ControlCenter.Shortcuts ?? new List<string>()).Take(6).ToList();
        var grid = new UniformGrid
        {
            Columns = shortcuts.Count <= 2 ? 1 : 2,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        foreach (var type in shortcuts)
        {
            var (label, glyph, action) = ShortcutFor(type);
            var btn = new Button
            {
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(SpaceSm),
                Background = B(_surfaceVariant, "#F911112d"),
                BorderBrush = B(_outline, "#BF21215F"),
                BorderThickness = new Thickness(BorderWidth),
                Foreground = B(_onSurface, "#FFf3edf7"),
                Cursor = Cursors.Hand,
                Template = ShortcutTemplate(),
                Content = ShortcutContent(label, glyph),
                ToolTip = label,
            };
            btn.Click += (_, _) => action();
            grid.Children.Add(btn);
        }

        return grid;
    }

    private FrameworkElement ShortcutContent(string label, string glyph)
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(Glyph(glyph, FontSizeTitle * 1.35, _onSurface));
        if (_cfg.ControlCenter.ShowShortcutLabels)
        {
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = FontSizeMini,
                Foreground = B(_onSurface, "#FFf3edf7"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, SpaceXs, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        return stack;
    }

    private ControlTemplate ShortcutTemplate() => RoundHoverTemplate(RadiusXl);

    /// <summary>Ярлыки оригинала: wifi, bluetooth, caffeine, nightlight, notification,
    /// power_profile (+ wallpaper, screen_recorder, session).</summary>
    private (string Label, string Glyph, Action Action) ShortcutFor(string type) => type switch
    {
        "wifi" => ("Wi-Fi", "wifi", () => { Close(); NativeMethods.OpenNetworkFlyout(); }),
        "bluetooth" => ("Bluetooth", "bluetooth", () => { Close(); NativeMethods.OpenBluetoothSettings(); }),
        "caffeine" => ("Не гасить", "caffeine-off", ToggleCaffeine),
        "nightlight" => ("Ночной свет", "nightlight-off", () => { Close(); NativeMethods.OpenDisplaySettings(); }),
        "notification" => ("Уведомления", "bell", () => SelectTab("notifications")),
        "power_profile" => ("Питание", "shutdown", () => SelectTab("power")),
        "wallpaper" => ("Обои", "wallpaper-selector", () => { Close(); NativeMethods.OpenPersonalizationSettings(); }),
        "screen_recorder" => ("Запись экрана", "camera", () => { Close(); NativeMethods.OpenSnippingTool(); }),
        "session" => ("Сеанс", "shutdown", ShowSessionActions),
        _ => (type, "circle-dot", () => { }),
    };

    private void ToggleCaffeine()
    {
        _caffeine = !_caffeine;
        NativeMethods.SetScreenSaverActive(_caffeine);
        RebuildTabBodies();
    }

    // ── Остальные вкладки ────────────────────────────────────────────────────

    private FrameworkElement BuildAudio()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Громкость"));

        float vol = _audio.GetVolume();
        bool mute = _audio.GetMute();
        body.Children.Add(Slider(0, 100, mute ? 0 : vol * 100, v =>
        {
            _audio.SetVolume((float)(v / 100.0));
            if (mute && v > 0) _audio.SetMute(false);
        }));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var muteBtn = CardButton(mute ? "Включить звук" : "Выключить звук", () =>
        {
            _audio.ToggleMute();
            RebuildTabBodies();
        });
        muteBtn.Width = 200;
        row.Children.Add(muteBtn);
        body.Children.Add(row);
        return body;
    }

    private FrameworkElement BuildNetwork()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Соединение"));
        body.Children.Add(CardLabel(_stats.NetText.Length > 0
            ? _stats.NetText
            : "Сетевые счётчики недоступны"));
        body.Children.Add(new Border { Height = SpaceSm });
        body.Children.Add(CardButton("Параметры сети", () => { Close(); NativeMethods.OpenNetworkFlyout(); }));
        return body;
    }

    private FrameworkElement BuildBluetooth()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Bluetooth"));
        body.Children.Add(CardLabel("Bluetooth в Windows управляется системными параметрами."));
        body.Children.Add(new Border { Height = SpaceSm });
        body.Children.Add(CardButton("Открыть Bluetooth", () => { Close(); NativeMethods.OpenBluetoothSettings(); }));
        return body;
    }

    private FrameworkElement BuildNotifications()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Уведомления"));
        body.Children.Add(CardLabel("В Windows нет демона уведомлений, как в Noctalia."));
        body.Children.Add(CardLabel("Центр уведомлений открывается системно."));
        body.Children.Add(new Border { Height = SpaceSm });
        body.Children.Add(CardButton("Открыть центр уведомлений", () => { Close(); NativeMethods.OpenActionCenter(); }));
        return body;
    }

    private FrameworkElement BuildCalendar()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader(DateTime.Now.ToString("MMMM yyyy", CultureInfo.CurrentCulture)));
        body.Children.Add(new System.Windows.Controls.Calendar
        {
            Background = B(_surfaceVariant, "#F911112d"),
            Foreground = B(_onSurface, "#FFf3edf7"),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        return body;
    }

    private FrameworkElement BuildMedia()
    {
        var body = new StackPanel();
        bool available = _media?.Available == true;

        // шапка карточки: обложка + трек/артист/альбом (как media_tab оригинала)
        var head = SectionCard();
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SpaceMd) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        double art = ControlHeightLg * 2.2;
        var artBox = new Border
        {
            Width = art, Height = art,
            CornerRadius = new CornerRadius(RadiusLg),
            Background = B(_surfaceVariant, "#F911112d"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        artBox.Child = ArtworkImage(art) ?? (UIElement)Glyph("disc", art * 0.5, _onSurfaceVariant);
        row.Children.Add(Place(artBox, 0));

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Line(available ? _media.Title : "Нет активного плеера",
            FontSizeTitle, _onSurface, FontWeights.Bold));
        if (available)
        {
            if (!string.IsNullOrWhiteSpace(_media.Artist))
                info.Children.Add(Line(_media.Artist, FontSizeBody, _onSurfaceVariant));
            if (!string.IsNullOrWhiteSpace(_media.Album))
                info.Children.Add(Line(_media.Album, FontSizeCaption, _onSurfaceVariant));
            info.Children.Add(Line(_media.IsPlaying ? "играет" : "пауза", FontSizeCaption, _primary));
        }
        row.Children.Add(Place(info, 2));
        head.Child = row;
        body.Children.Add(head);

        body.Children.Add(new Border { Height = SpaceMd });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(CardButton("Предыдущий", async () => { await _media.PreviousAsync(); await _media.RefreshAsync(); RebuildTabBodies(); }));
        buttons.Children.Add(CardButton("Play/Pause", async () => { await _media.TogglePlayPauseAsync(); await _media.RefreshAsync(); RebuildTabBodies(); }));
        buttons.Children.Add(CardButton("Следующий", async () => { await _media.NextAsync(); await _media.RefreshAsync(); RebuildTabBodies(); }));
        body.Children.Add(buttons);
        return body;
    }

    private FrameworkElement BuildMonitor()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Яркость"));
        int br = _stats.GetBrightness();
        if (br < 0) body.Children.Add(CardLabel("Яркость недоступна на этом дисплее."));
        else body.Children.Add(Slider(5, 100, br, v => _stats.SetBrightness((int)v)));
        return body;
    }

    private FrameworkElement BuildSystem()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Система"));
        body.Children.Add(CardLabel($"Процессор: {_stats.CpuPercent:0}%"));
        body.Children.Add(CardLabel($"Память: {_stats.RamUsedGb} / {_stats.RamTotalGb} ГБ ({_stats.RamPercent:0}%)"));
        body.Children.Add(CardLabel($"Сеть: {_stats.NetText}"));
        return body;
    }

    private FrameworkElement BuildPower()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Питание"));
        body.Children.Add(CardLabel(_stats.HasBattery
            ? $"Батарея: {_stats.BatteryPercent}%{(_stats.Charging ? " (заряжается)" : "")}{(_stats.OnAc ? " · сеть" : "")}"
            : "Батарея не обнаружена"));
        body.Children.Add(new Border { Height = SpaceSm });
        body.Children.Add(CardButton("Схема электропитания", () => { Close(); NativeMethods.OpenPowerSettings(); }));
        return body;
    }

    private FrameworkElement BuildWeather()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Погода"));
        body.Children.Add(CardLabel("Погода в Windows-порте недоступна."));
        return body;
    }

    private FrameworkElement BuildScreenTime()
    {
        var body = new StackPanel();
        body.Children.Add(SectionHeader("Экранное время"));
        body.Children.Add(CardLabel($"Система работает: {Uptime()}"));
        return body;
    }

    // ── Действия шапки ───────────────────────────────────────────────────────

    private void ShowSessionActions()
    {
        var menu = new ContextMenu();
        void Add(string text, Action run)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += (_, _) => { Close(); run(); };
            menu.Items.Add(mi);
        }
        Add("Заблокировать", () => NativeMethods.LockWorkStation());
        Add("Сон", () => NativeMethods.SetSuspendState(false, false, false));
        Add("Перезагрузка", () => System.Diagnostics.Process.Start("shutdown.exe", "/r /t 0"));
        Add("Выключение", () => System.Diagnostics.Process.Start("shutdown.exe", "/s /t 0"));
        menu.IsOpen = true;
        menu.PlacementTarget = _headerActions;
    }

    // ── Помощники разметки ───────────────────────────────────────────────────

    private Border SectionCard() => new()
    {
        Background = B(_surfaceVariant, "#F911112d"),
        BorderBrush = B(_outline, "#BF21215F"),
        BorderThickness = new Thickness(BorderWidth),
        CornerRadius = new CornerRadius(RadiusXl),
        Padding = new Thickness(CardPadding),
    };

    private TextBlock SectionHeader(string text) => new()
    {
        Text = text,
        FontSize = FontSizeCaption,
        FontWeight = FontWeights.SemiBold,
        Foreground = B(_onSurfaceVariant, "#FF7c80b4"),
        Margin = new Thickness(0, 0, 0, SpaceSm),
    };

    private Border CardLabel(string text) => new()
    {
        Background = B(_surfaceVariant, "#F911112d"),
        CornerRadius = new CornerRadius(RadiusLg),
        Padding = new Thickness(SpaceMd, SpaceSm, SpaceMd, SpaceSm),
        Margin = new Thickness(0, 0, 0, SpaceXs),
        Child = new TextBlock
        {
            Text = text,
            FontSize = FontSizeBody,
            Foreground = B(_onSurface, "#FFf3edf7"),
            TextWrapping = TextWrapping.Wrap,
        },
    };

    private Button CardButton(string label, Action action)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(SpaceMd, SpaceSm, SpaceMd, SpaceSm),
            Margin = new Thickness(0, 0, SpaceSm, SpaceXs),
            Background = B(_surfaceVariant, "#F911112d"),
            Foreground = B(_onSurface, "#FFf3edf7"),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Template = RoundHoverTemplate(RadiusLg),
        };
        btn.Click += (_, _) => action();
        return btn;
    }

    private Slider Slider(double min, double max, double value, Action<double> onChange)
    {
        var slider = new Slider
        {
            Minimum = min, Maximum = max, Value = value,
            Margin = new Thickness(0, SpaceXs, 0, SpaceMd),
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        slider.ValueChanged += (_, e) => onChange(e.NewValue);
        return slider;
    }

    private static string DotNetFormat(string fmt, string fallback)
    {
        if (string.IsNullOrWhiteSpace(fmt)) return fallback;
        // формат Noctalia ({:%H:%M}) -> .NET
        return fmt.Replace("{:", "").Replace("}", "")
                  .Replace("%H", "HH").Replace("%M", "mm").Replace("%S", "ss")
                  .Replace("%A", "dddd").Replace("%a", "ddd").Replace("%x", "d")
                  .Replace("%d", "dd").Replace("%B", "MMMM").Replace("%b", "MMM");
    }

    // ── Позиционирование ─────────────────────────────────────────────────────

    /// <summary>Панель по центру рабочей области под баром (attached-панель в оригинале).</summary>
    private void PositionNear()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - ActualWidth) / 2;
        Top = wa.Top + SpaceMd;
        if (Top + ActualHeight > wa.Bottom)
            Top = Math.Max(wa.Top, wa.Bottom - ActualHeight - SpaceMd);
    }
}

/// <summary>
/// Шрифт иконок control center. Использует встроенный Tabler — тот же
/// <see cref="IconGlyphs.Family"/>, что и панель, чтобы вид совпадал с оригиналом.
/// </summary>
internal static class IconFont
{
    public static void Apply(TextBlock tb) => tb.FontFamily = IconGlyphs.Family;
}
