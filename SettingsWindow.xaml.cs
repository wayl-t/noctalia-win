using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NoctaliaWin.Services;

namespace NoctaliaWin;

/// <summary>Окно настроек: пишет в тот же config.json, изменения применяются на лету.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadFrom(ConfigService.Instance.Config);
    }

    private void LoadFrom(PanelConfig cfg)
    {
        var b = cfg.Bar;

        SelectRadio(PosTop, "top", b.Position, "top");
        SelectRadio(PosBottom, "bottom", b.Position, "top");
        ThicknessSlider.Value = b.Thickness;
        MarginEndsSlider.Value = b.MarginEnds;
        MarginEdgeSlider.Value = b.MarginEdge;
        RadiusSlider.Value = b.CornerRadius;
        PaddingSlider.Value = b.Padding;
        SpacingSlider.Value = b.WidgetSpacing;
        OpacitySlider.Value = System.Math.Clamp(b.BackgroundOpacity, 0.2, 1.0);
        BlurBox.IsChecked = b.Blur;
        ReserveBox.IsChecked = b.ReserveSpace;
        AutoHideBox.IsChecked = b.AutoHide;
        DelaySlider.Value = System.Math.Clamp(b.AutoHideDelayMs, 100, 2000);
        HoverBox.IsChecked = b.HoverHighlight;
        TipsBox.IsChecked = b.ShowTooltips;
        AnimBox.IsChecked = b.Animations;

        var t = cfg.Theme;
        SelectRadio(ThemeDark, "dark", t.Mode, "dark");
        SelectRadio(ThemeLight, "light", t.Mode, "dark");
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
        Track(OpacitySlider, OpacityVal, v => v.ToString("0.##"));
        Track(DelaySlider, DelayVal, v => v.ToString("0"));
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
            b.BackgroundOpacity = OpacitySlider.Value;
            b.Blur = BlurBox.IsChecked == true;
            b.ReserveSpace = ReserveBox.IsChecked == true;
            b.AutoHide = AutoHideBox.IsChecked == true;
            b.AutoHideDelayMs = (int)DelaySlider.Value;
            b.HoverHighlight = HoverBox.IsChecked == true;
            b.ShowTooltips = TipsBox.IsChecked == true;
            b.Animations = AnimBox.IsChecked == true;

            var t = cfg.Theme;
            t.Mode = ThemeLight.IsChecked == true ? "light" : "dark";
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
}
