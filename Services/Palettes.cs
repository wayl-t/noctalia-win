using System.Collections.Generic;

namespace NoctaliaWin.Services;

/// <summary>
/// Палитры-пресеты Noctalia: значения один в один из исходников оригинала
/// (src/theme/builtin_palettes.cpp). Роли совпадают с mPrimary/mOnSurface/... ,
/// чтобы цвета панели были теми же, что в Noctalia на Linux.
/// </summary>
public static class Palettes
{
    /// <summary>Полный набор цветовых ролей Noctalia.</summary>
    public sealed record Palette(
        string Primary, string OnPrimary,
        string Secondary, string OnSecondary,
        string Tertiary, string OnTertiary,
        string Error, string OnError,
        string Surface, string OnSurface,
        string SurfaceVariant, string OnSurfaceVariant,
        string Outline, string Shadow,
        string Hover, string OnHover);

    /// <summary>Тёмный и светлый варианты пресета.</summary>
    public sealed record PalettePair(Palette Dark, Palette Light);

    /// <summary>Имена пресетов в порядке оригинала.</summary>
    public static readonly string[] Names =
    {
        "Noctalia", "Ayu", "Catppuccin", "Dracula", "Eldritch",
        "Gruvbox", "Kanagawa", "Nord", "Rosé Pine", "Tokyo-Night",
    };

    private static readonly Dictionary<string, PalettePair> All = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["Noctalia"] = new(
            new Palette(
                "#fff59b", "#0e0e43", "#a9aefe", "#0e0e43", "#9BFECE", "#0e0e43",
                "#FD4663", "#0e0e43", "#070722", "#f3edf7", "#11112d", "#7c80b4",
                "#21215F", "#070722", "#9BFECE", "#0e0e43"),
            new Palette(
                "#5d65f5", "#dadcff", "#8E93D8", "#dadcff", "#0e0e43", "#fef29a",
                "#FD4663", "#0e0e43", "#e6e8fa", "#0e0e43", "#eff0ff", "#4b55c8",
                "#8288fc", "#f3edf7", "#0e0e43", "#fef29a")),

        ["Ayu"] = new(
            new Palette(
                "#E6B450", "#0B0E14", "#AAD94C", "#0B0E14", "#39BAE6", "#0B0E14",
                "#D95757", "#0B0E14", "#0B0E14", "#D1D1C7", "#1e222a", "#8E959E",
                "#565B66", "#000000", "#39BAE6", "#0B0E14"),
            new Palette(
                "#FF8F40", "#F8F9FA", "#86B300", "#F8F9FA", "#55B4D4", "#F8F9FA",
                "#E65050", "#F8F9FA", "#F8F9FA", "#42474C", "#E4E6E9", "#6E757C",
                "#8A9199", "#F8F9FA", "#55B4D4", "#F8F9FA")),

        ["Catppuccin"] = new(
            new Palette(
                "#cba6f7", "#11111b", "#fab387", "#11111b", "#94e2d5", "#11111b",
                "#f38ba8", "#11111b", "#1e1e2e", "#cdd6f4", "#313244", "#a3b4eb",
                "#4c4f69", "#11111b", "#94e2d5", "#11111b"),
            new Palette(
                "#8839ef", "#eff1f5", "#fe640b", "#eff1f5", "#40a02b", "#eff1f5",
                "#d20f39", "#dce0e8", "#eff1f5", "#4c4f69", "#ccd0da", "#6c6f85",
                "#a5adcb", "#dce0e8", "#40a02b", "#eff1f5")),

        ["Dracula"] = new(
            new Palette(
                "#bd93f9", "#282A36", "#ff79c6", "#4e1d32", "#8be9fd", "#003543",
                "#FF5555", "#282A36", "#282A36", "#F8F8F2", "#44475A", "#d6d8e0",
                "#5a5e77", "#282A36", "#8be9fd", "#003543"),
            new Palette(
                "#8332f4", "#ffffff", "#ff1399", "#ffffff", "#0398b9", "#ffffff",
                "#FF5555", "#282A36", "#f8f8f2", "#282a36", "#e6e6ea", "#44475a",
                "#cacad3", "#d6d8e0", "#0398b9", "#ffffff")),

        ["Eldritch"] = new(
            new Palette(
                "#37f499", "#171928", "#04d1f9", "#171928", "#a48cf2", "#171928",
                "#f16c75", "#171928", "#212337", "#ebfafa", "#292e42", "#ABB4DA",
                "#3b4261", "#414868", "#a48cf2", "#171928"),
            new Palette(
                "#fb5bb6", "#1e2029", "#0ad6ff", "#1e2029", "#8a69f7", "#1e2029",
                "#fb5b66", "#1e2029", "#f0f3f4", "#1e2029", "#d5d9db", "#1e2029",
                "#c9cbcd", "#e2e6e8", "#8a69f7", "#1e2029")),

        ["Gruvbox"] = new(
            new Palette(
                "#b8bb26", "#282828", "#fabd2F", "#282828", "#83a598", "#282828",
                "#fb4934", "#282828", "#282828", "#fbf1c7", "#3c3836", "#ebdbb2",
                "#57514e", "#282828", "#83a598", "#282828"),
            new Palette(
                "#98971a", "#fbf1c7", "#d79921", "#fbf1c7", "#458588", "#fbf1c7",
                "#cc241d", "#fbf1c7", "#fbf1c7", "#3c3836", "#ebdbb2", "#7c6f64",
                "#bdae93", "#d5c4a1", "#458588", "#fbf1c7")),

        ["Kanagawa"] = new(
            new Palette(
                "#76946a", "#1f1f28", "#c0a36e", "#1f1f28", "#7e9cd8", "#1f1f28",
                "#c34043", "#1f1f28", "#1f1f28", "#c8c093", "#2a2a37", "#717c7c",
                "#363646", "#1f1f28", "#7e9cd8", "#1f1f28"),
            new Palette(
                "#6f894e", "#f2ecbc", "#77713F", "#f2ecbc", "#4d699b", "#f2ecbc",
                "#c84053", "#f2ecbc", "#f2ecbc", "#545464", "#e5ddb0", "#8a8980",
                "#cfc49c", "#f2ecbc", "#4d699b", "#f2ecbc")),

        ["Nord"] = new(
            new Palette(
                "#8fbcbb", "#2e3440", "#88c0d0", "#2e3440", "#5e81ac", "#2e3440",
                "#bf616a", "#2e3440", "#2e3440", "#eceff4", "#3b4252", "#d8dee9",
                "#505a70", "#2e3440", "#5e81ac", "#2e3440"),
            new Palette(
                "#5e81ac", "#eceff4", "#64adc2", "#eceff4", "#6fa9a8", "#eceff4",
                "#bf616a", "#eceff4", "#eceff4", "#2e3440", "#e5e9f0", "#4c566a",
                "#c5cedd", "#d8dee9", "#6fa9a8", "#eceff4")),

        // Rosé Pine dark: base #191724, поверхность #1f1d2e, акцент #ebbcba / золото #f6c177.
        ["Rosé Pine"] = new(
            new Palette(
                "#ebbcba", "#191724", "#f6c177", "#191724", "#9ccfd8", "#191724",
                "#eb6f92", "#191724", "#191724", "#e0def4", "#1f1d2e", "#a5a3c0",
                "#403d52", "#191724", "#9ccfd8", "#191724"),
            new Palette(
                "#907aa9", "#fffaf3", "#d7827e", "#fffaf3", "#56949f", "#fffaf3",
                "#b4637a", "#fffaf3", "#faf4ed", "#575279", "#f2e9e1", "#797593",
                "#cecacd", "#f2e9e1", "#56949f", "#fffaf3")),

        // Tokyo-Night dark: bg #1a1b26, поверхность #24283b, акцент #7aa2f7.
        ["Tokyo-Night"] = new(
            new Palette(
                "#7aa2f7", "#1a1b26", "#bb9af7", "#1a1b26", "#7dcfff", "#1a1b26",
                "#f7768e", "#1a1b26", "#1a1b26", "#c0caf5", "#24283b", "#a9b1d6",
                "#414868", "#1a1b26", "#7dcfff", "#1a1b26"),
            new Palette(
                "#2e7de9", "#1a1b26", "#9854f1", "#ffffff", "#007197", "#ffffff",
                "#f52a65", "#ffffff", "#e1e2e7", "#3760bf", "#d0d5e3", "#6172b0",
                "#a8aecb", "#e1e2e7", "#007197", "#ffffff")),
    };

    /// <summary>Пара палитр пресета; неизвестное имя — Noctalia.</summary>
    public static PalettePair Get(string name) =>
        name != null && All.TryGetValue(name, out var p) ? p : All["Noctalia"];

    /// <summary>Палитра пресета для режима dark/light.</summary>
    public static Palette Resolve(string preset, string mode) =>
        string.Equals(mode, "light", System.StringComparison.OrdinalIgnoreCase)
            ? Get(preset).Light
            : Get(preset).Dark;
}
