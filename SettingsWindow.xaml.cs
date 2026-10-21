using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NoctaliaWin.Services;

namespace NoctaliaWin;

/// <summary>Окно настроек: пишет в тот же config.json, изменения применяются на лету.
/// Набор полей соответствует реальным ключам бара в оригинале Noctalia.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        BuildPaletteList();
        Loaded += (_, _) =>
        {
            CenterOnPrimary();
            LoadFrom(ConfigService.Instance.Config);
        };
    }

    /// <summary>Ставим окно по центру основного экрана вручную: CenterScreen
    /// в WPF иногда считает центр по всей виртуальной области и уезжает за край.</summary>
    private void CenterOnPrimary()
    {
        var wa = SystemParameters.WorkArea;
        double left = wa.Left + (wa.Width - ActualWidth) / 2;
        double top = wa.Top + (wa.Height - ActualHeight) / 2;
        Left = System.Math.Max(wa.Left, left);
        Top = System.Math.Max(wa.Top, top);
    }

    private void BuildPaletteList()
    {
        foreach (var name in Palettes.Names) PaletteCombo.Items.Add(name);
    }

    private void LoadFrom(PanelConfig cfg)
    {
        var b = cfg.Bar;

        SelectRadio(PosTop, "top", b.Position, "top");
        SelectRadio(PosBottom, "bottom", b.Position, "top");

        ThicknessSlider.Value = System.Math.Clamp(b.Thickness, 24, 96);
        MarginEndsSlider.Value = System.Math.Clamp(b.MarginEnds, 0, 400);
        MarginEdgeSlider.Value = System.Math.Clamp(b.MarginEdge, 0, 60);
        RadiusSlider.Value = System.Math.Clamp(b.CornerRadius, 0, 40);
        PaddingSlider.Value = System.Math.Clamp(b.Padding, 0, 40);
        SpacingSlider.Value = System.Math.Clamp(b.WidgetSpacing, 0, 24);
        ScaleSlider.Value = System.Math.Clamp(b.Scale, 0.6, 2);
        FontScaleSlider.Value = System.Math.Clamp(b.FontScale, 0.6, 2);
        WeightSlider.Value = System.Math.Clamp(b.FontWeight, 100, 900);
        OpacitySlider.Value = System.Math.Clamp(b.BackgroundOpacity, 0.2, 1.0);
        DelaySlider.Value = System.Math.Clamp(b.AutoHideDelayMs, 0, 3000);

        BlurBox.IsChecked = b.Blur;
        ShadowBox.IsChecked = b.Shadow;
        ReserveBox.IsChecked = b.ReserveSpace;
        AutoHideBox.IsChecked = b.AutoHide;
        SmartHideBox.IsChecked = b.SmartAutoHide;
        PeekBox.IsChecked = b.ShowOnWorkspaceSwitch;
        HoverBox.IsChecked = b.HoverHighlight;
        TipsBox.IsChecked = b.ShowTooltips;
        AnimBox.IsChecked = b.Animations;
        ConcaveBox.IsChecked = b.ConcaveEdgeCorners;
        TitleBox.IsChecked = b.ShowWindowTitle;

        CapsuleBox.IsChecked = b.Capsule;
        CapsuleBorderBox.IsChecked = b.Capsule && !string.IsNullOrWhiteSpace(b.CapsuleBorder);
        CapsuleThickSlider.Value = System.Math.Clamp(b.CapsuleThickness, 0.4, 1.0);
        CapsulePadSlider.Value = System.Math.Clamp(b.CapsulePadding, 0, 24);
        CapsuleFillBox.Text = b.CapsuleFill ?? "";

        var t = cfg.Theme;
        SelectRadio(ThemeDark, "dark", t.Mode, "dark");
        SelectRadio(ThemeLight, "light", t.Mode, "dark");
        SelectRadio(ThemeAuto, "auto", t.Mode, "dark");
        PaletteCombo.SelectedItem = Palettes.Names
            .FirstOrDefault(n => string.Equals(n, t.Palette, System.StringComparison.OrdinalIgnoreCase))
            ?? "Noctalia";
        PureBlackBox.IsChecked = t.PureBlackDark;

        BgBox.Text = t.Background ?? "";
        SurfaceBox.Text = t.Surface ?? "";
        OnSurfaceBox.Text = t.OnSurface ?? "";
        DimBox.Text = t.Dim ?? "";
        PrimaryBox.Text = t.Primary ?? "";
        OutlineBox.Text = t.Outline ?? "";

        TimeFmtBox.Text = cfg.Clock.TimeFormat;
        DateFmtBox.Text = cfg.Clock.DateFormat;

        StartBox.Text = Join(b.Start);
        CenterBox.Text = Join(b.Center);
        EndBox.Text = Join(b.End);

        Track(ThicknessSlider, ThicknessVal, v => v.ToString("0"));
        Track(MarginEndsSlider, MarginEndsVal, v => v.ToString("0"));
        Track(MarginEdgeSlider, MarginEdgeVal, v => v.ToString("0"));
        Track(RadiusSlider, RadiusVal, v => v.ToString("0"));
        Track(PaddingSlider, PaddingVal, v => v.ToString("0"));
        Track(SpacingSlider, SpacingVal, v => v.ToString("0"));
        Track(ScaleSlider, ScaleVal, v => v.ToString("0.##"));
        Track(FontScaleSlider, FontScaleVal, v => v.ToString("0.##"));
        Track(WeightSlider, WeightVal, v => v.ToString("0"));
        Track(OpacitySlider, OpacityVal, v => v.ToString("0.##"));
        Track(DelaySlider, DelayVal, v => v.ToString("0"));
        Track(CapsuleThickSlider, CapsuleThickVal, v => v.ToString("0.##"));
        Track(CapsulePadSlider, CapsulePadVal, v => v.ToString("0"));
    }

    private static string Join(List<string> list) =>
        list == null ? "" : string.Join(", ", list);

    private static void SelectRadio(RadioButton rb, string tag, string value, string def) =>
        rb.IsChecked = string.Equals(tag, value ?? def, System.StringComparison.OrdinalIgnoreCase);

    private static void Track(Slider slider, TextBlock label, System.Func<double, string> fmt)
    {
        label.Text = fmt(slider.Value);
        slider.ValueChanged += (_, _) => label.Text = fmt(slider.Value);
    }

    private static List<string> Split(string text) =>
        (text ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries |
                               System.StringSplitOptions.TrimEntries).ToList();

    private static string EmptyToNull(string text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cfg = ConfigService.Instance.Config;
            var b = cfg.Bar;

            b.Position = PosBottom.IsChecked == true ? "bottom" : "top";
            b.Thickness = (int)ThicknessSlider.Value;
            b.MarginEnds = (int)MarginEndsSlider.Value;
            b.MarginEdge = (int)MarginEdgeSlider.Value;
            b.CornerRadius = (int)RadiusSlider.Value;
            b.Padding = (int)PaddingSlider.Value;
            b.WidgetSpacing = (int)SpacingSlider.Value;
            b.Scale = ScaleSlider.Value;
            b.FontScale = FontScaleSlider.Value;
            b.FontWeight = (int)WeightSlider.Value;
            b.BackgroundOpacity = OpacitySlider.Value;
            b.AutoHideDelayMs = (int)DelaySlider.Value;

            b.Blur = BlurBox.IsChecked == true;
            b.Shadow = ShadowBox.IsChecked == true;
            b.ReserveSpace = ReserveBox.IsChecked == true;
            b.AutoHide = AutoHideBox.IsChecked == true;
            b.SmartAutoHide = SmartHideBox.IsChecked == true;
            b.ShowOnWorkspaceSwitch = PeekBox.IsChecked == true;
            b.HoverHighlight = HoverBox.IsChecked == true;
            b.ShowTooltips = TipsBox.IsChecked == true;
            b.Animations = AnimBox.IsChecked == true;
            b.ConcaveEdgeCorners = ConcaveBox.IsChecked == true;
            b.ShowWindowTitle = TitleBox.IsChecked == true;

            b.Capsule = CapsuleBox.IsChecked == true;
            b.CapsuleThickness = CapsuleThickSlider.Value;
            b.CapsulePadding = CapsulePadSlider.Value;
            b.CapsuleFill = string.IsNullOrWhiteSpace(CapsuleFillBox.Text)
                ? "surface_variant" : CapsuleFillBox.Text.Trim();
            b.CapsuleBorder = CapsuleBorderBox.IsChecked == true ? "outline" : null;

            var t = cfg.Theme;
            t.Mode = ThemeLight.IsChecked == true ? "light"
                : ThemeAuto.IsChecked == true ? "auto" : "dark";
            t.Palette = PaletteCombo.SelectedItem as string ?? "Noctalia";
            t.PureBlackDark = PureBlackBox.IsChecked == true;

            t.Background = EmptyToNull(BgBox.Text);
            t.Surface = EmptyToNull(SurfaceBox.Text);
            t.OnSurface = EmptyToNull(OnSurfaceBox.Text);
            t.Dim = EmptyToNull(DimBox.Text);
            t.Primary = EmptyToNull(PrimaryBox.Text);
            t.Outline = EmptyToNull(OutlineBox.Text);

            cfg.Clock.TimeFormat = string.IsNullOrWhiteSpace(TimeFmtBox.Text) ? "HH:mm" : TimeFmtBox.Text.Trim();
            cfg.Clock.DateFormat = string.IsNullOrWhiteSpace(DateFmtBox.Text) ? "ddd, d MMM" : DateFmtBox.Text.Trim();

            b.Start = Split(StartBox.Text);
            b.Center = Split(CenterBox.Text);
            b.End = Split(EndBox.Text);

            ConfigService.Instance.Save();
            ConfigService.Instance.Reload(); // применить сразу, не дожидаясь watcher'а

            MessageBox.Show(this, "Настройки сохранены и применены.", "NoctaliaWin",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(this, "Не удалось сохранить: " + ex.Message, "NoctaliaWin",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenCfg_Click(object sender, RoutedEventArgs e)
    {
        ConfigService.Instance.OpenConfigFolder();
    }

    /// <summary>Сброс внешнего вида к значениям оригинала, персональные настройки
    /// поведения (позиция, автоскрытие) остаются как есть.</summary>
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var cfg = ConfigService.Instance.Config;
        var b = cfg.Bar;

        b.Thickness = 34;
        b.MarginEnds = 180;
        b.MarginEdge = 10;
        b.CornerRadius = 12;
        b.Padding = 14;
        b.WidgetSpacing = 6;
        b.Scale = 1.0;
        b.FontScale = 1.0;
        b.FontWeight = 500;
        b.BackgroundOpacity = 1.0;
        b.AutoHideDelayMs = 0;
        b.Blur = true;
        b.Shadow = true;
        b.HoverHighlight = true;
        b.ShowTooltips = true;
        b.Animations = true;
        b.ConcaveEdgeCorners = true;
        b.Capsule = false;
        b.CapsuleFill = "surface_variant";
        b.CapsuleThickness = 0.76;
        b.CapsulePadding = 6;
        b.CapsuleBorder = null;
        b.Border = "outline";
        b.BorderWidth = 0.0;

        b.Start = new List<string> { "launcher", "workspaces" };
        b.Center = new List<string> { "clock" };
        b.End = new List<string>
        {
            "media", "tray", "notifications", "network", "volume", "battery", "control-center", "session",
        };

        var t = cfg.Theme;
        t.Palette = "Noctalia";
        t.PureBlackDark = false;
        t.Background = null;
        t.Surface = null;
        t.OnSurface = null;
        t.Dim = null;
        t.Primary = null;
        t.Outline = null;

        cfg.Clock.TimeFormat = "HH:mm";
        cfg.Clock.DateFormat = "ddd, d MMM";

        ConfigService.Instance.Save();
        ConfigService.Instance.Reload();
        LoadFrom(cfg);
    }
}
