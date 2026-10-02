using System.Windows;
using System.Windows.Media;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;
using Color = System.Windows.Media.Color;

namespace Caelus.UI;

public sealed class ThemePalette
{
    public AppTheme Id { get; init; }
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public Color Accent { get; init; }
    public Color AccentHover { get; init; }
    public Color AccentForeground { get; init; }
    public Color Background { get; init; }
    public Color Surface { get; init; }
    public Color Card { get; init; }
    public Color Sidebar { get; init; }
    public Color Border { get; init; }
    public Color Text { get; init; }
    public Color Muted { get; init; }
    public Color Danger { get; init; }
    public Color Input { get; init; }
    public Color Hover { get; init; }
    public Color Popup { get; init; }
    public Color Track { get; init; }
    public bool Playful { get; init; }
}

/// <summary>
/// Themes are color palettes; the style (Modern or Classic) is the set of control templates and
/// layout metrics. Each is its own ResourceDictionary in Application.Resources.MergedDictionaries:
/// [0] = UI/Themes/Modern.xaml or UI/Themes/Classic.xaml, [1] = the palette brushes for the theme in
/// that style. Both are swapped at runtime. Classic palettes and Classic.xaml are the pre-2.2 look, unchanged.
/// </summary>
public static class ThemeService
{
    public static event Action? Changed;

    /// <summary>Raised just before the style dictionary is swapped (the old visuals are still laid out).</summary>
    public static event Action<UiStyle>? StyleChanging;

    /// <summary>The pre-2.2 palettes, used with the Classic style.</summary>
    public static readonly ThemePalette[] ClassicPalettes =
    {
        new()
        {
            Id = AppTheme.Halloween,
            Name = "Halloween",
            Blurb = "Pumpkin neon on charcoal. Default for now.",
            Accent = Rgb(0xFF, 0x7A, 0x18),
            AccentHover = Rgb(0xFF, 0x92, 0x2E),
            AccentForeground = Rgb(0x1A, 0x0C, 0x04),
            Background = Rgb(0x12, 0x10, 0x0E),
            Surface = Rgb(0x18, 0x15, 0x12),
            Card = Rgb(0x1E, 0x1A, 0x16),
            Sidebar = Rgb(0x0C, 0x0B, 0x09),
            Border = Rgb(0x3A, 0x2E, 0x22),
            Text = Rgb(0xF5, 0xEB, 0xDE),
            Muted = Rgb(0xA8, 0x90, 0x78),
            Danger = Rgb(0xE0, 0x4E, 0x3A),
            Input = Rgb(0x26, 0x20, 0x1A),
            Hover = Rgb(0x2E, 0x26, 0x1E),
            Popup = Rgb(0x16, 0x13, 0x10),
            Track = Rgb(0x34, 0x2A, 0x20),
            Playful = true
        },
        new()
        {
            Id = AppTheme.Dark,
            Name = "Midnight",
            Blurb = "Dark wood and signal red.",
            Accent = Rgb(0xC4, 0x32, 0x2A),
            AccentHover = Rgb(0xD8, 0x44, 0x3A),
            AccentForeground = Rgb(0xFF, 0xF4, 0xF2),
            Background = Rgb(0x12, 0x11, 0x0F),
            Surface = Rgb(0x19, 0x18, 0x15),
            Card = Rgb(0x1D, 0x1C, 0x18),
            Sidebar = Rgb(0x0F, 0x0E, 0x0C),
            Border = Rgb(0x32, 0x2F, 0x28),
            Text = Rgb(0xEE, 0xEA, 0xE2),
            Muted = Rgb(0x8E, 0x88, 0x7A),
            Danger = Rgb(0xC9, 0x5A, 0x4C),
            Input = Rgb(0x24, 0x22, 0x1C),
            Hover = Rgb(0x2A, 0x28, 0x22),
            Popup = Rgb(0x1A, 0x19, 0x16),
            Track = Rgb(0x2C, 0x2A, 0x24)
        },
        new()
        {
            Id = AppTheme.Octane,
            Name = "Octane",
            Blurb = "Ink navy and a hard red, after the revival.",
            Accent = Rgb(0xD4, 0x36, 0x2C),
            AccentHover = Rgb(0xE4, 0x4A, 0x3E),
            AccentForeground = Rgb(0xFF, 0xF4, 0xF2),
            Background = Rgb(0x0E, 0x15, 0x1E),
            Surface = Rgb(0x14, 0x1D, 0x28),
            Card = Rgb(0x17, 0x22, 0x30),
            Sidebar = Rgb(0x0A, 0x11, 0x18),
            Border = Rgb(0x2A, 0x3A, 0x4C),
            Text = Rgb(0xE6, 0xEE, 0xF6),
            Muted = Rgb(0x86, 0x97, 0xAB),
            Danger = Rgb(0xD0, 0x64, 0x58),
            Input = Rgb(0x1A, 0x27, 0x36),
            Hover = Rgb(0x22, 0x32, 0x44),
            Popup = Rgb(0x12, 0x1B, 0x26),
            Track = Rgb(0x24, 0x33, 0x44)
        },
        new()
        {
            Id = AppTheme.Dusk,
            Name = "Dusk",
            Blurb = "Clay red on shadow. Quiet, a bit dusty.",
            Accent = Rgb(0xC4, 0x3A, 0x32),
            AccentHover = Rgb(0xD4, 0x4C, 0x42),
            AccentForeground = Rgb(0xFF, 0xF4, 0xF2),
            Background = Rgb(0x18, 0x16, 0x1B),
            Surface = Rgb(0x20, 0x1D, 0x24),
            Card = Rgb(0x24, 0x21, 0x29),
            Sidebar = Rgb(0x13, 0x11, 0x16),
            Border = Rgb(0x3A, 0x35, 0x40),
            Text = Rgb(0xED, 0xE6, 0xDC),
            Muted = Rgb(0x98, 0x8E, 0x84),
            Danger = Rgb(0xC4, 0x5C, 0x58),
            Input = Rgb(0x2A, 0x26, 0x2F),
            Hover = Rgb(0x32, 0x2D, 0x38),
            Popup = Rgb(0x1C, 0x19, 0x21),
            Track = Rgb(0x34, 0x2F, 0x3A)
        },
        new()
        {
            Id = AppTheme.Light,
            Name = "Paper",
            Blurb = "Cream stock and sealing-wax red.",
            Accent = Rgb(0xB4, 0x2C, 0x24),
            AccentHover = Rgb(0xC6, 0x3A, 0x30),
            AccentForeground = Rgb(0xFF, 0xF6, 0xF4),
            Background = Rgb(0xF1, 0xE9, 0xDB),
            Surface = Rgb(0xF8, 0xF3, 0xE8),
            Card = Rgb(0xFC, 0xF8, 0xF0),
            Sidebar = Rgb(0xE5, 0xDA, 0xC6),
            Border = Rgb(0xD2, 0xC4, 0xAA),
            Text = Rgb(0x2A, 0x24, 0x1A),
            Muted = Rgb(0x76, 0x6B, 0x58),
            Danger = Rgb(0xB0, 0x45, 0x3A),
            Input = Rgb(0xFF, 0xFC, 0xF6),
            Hover = Rgb(0xE8, 0xDC, 0xC6),
            Popup = Rgb(0xFF, 0xFC, 0xF6),
            Track = Rgb(0xD8, 0xCC, 0xB4)
        },
        new()
        {
            Id = AppTheme.Classic,
            Name = "Classic",
            Blurb = "Old launcher gray. 2008 energy.",
            Accent = Rgb(0x0B, 0x5F, 0xA4),
            AccentHover = Rgb(0x0D, 0x6F, 0xBC),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0xE8, 0xE8, 0xE8),
            Surface = Rgb(0xF2, 0xF2, 0xF2),
            Card = Rgb(0xFF, 0xFF, 0xFF),
            Sidebar = Rgb(0xD8, 0xD8, 0xD8),
            Border = Rgb(0xB8, 0xB8, 0xB8),
            Text = Rgb(0x1A, 0x1A, 0x1A),
            Muted = Rgb(0x5A, 0x5A, 0x5A),
            Danger = Rgb(0xB0, 0x33, 0x2B),
            Input = Rgb(0xFF, 0xFF, 0xFF),
            Hover = Rgb(0xD0, 0xD0, 0xD0),
            Popup = Rgb(0xFF, 0xFF, 0xFF),
            Track = Rgb(0xC8, 0xC8, 0xC8)
        },
        new()
        {
            Id = AppTheme.Xyxy,
            Name = "xyxy's theme",
            Blurb = "Blush, bows, and sparkles. Light and a little extra.",
            Accent = Rgb(0xE8, 0x5A, 0x9B),
            AccentHover = Rgb(0xF4, 0x72, 0xB6),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0xFF, 0xF4, 0xF7),
            Surface = Rgb(0xFF, 0xFB, 0xFC),
            Card = Rgb(0xFF, 0xFF, 0xFF),
            Sidebar = Rgb(0xFF, 0xE4, 0xEE),
            Border = Rgb(0xF5, 0xC0, 0xD4),
            Text = Rgb(0x4A, 0x24, 0x38),
            Muted = Rgb(0xB0, 0x7A, 0x92),
            Danger = Rgb(0xE1, 0x1D, 0x48),
            Input = Rgb(0xFF, 0xF9, 0xFB),
            Hover = Rgb(0xFF, 0xD0, 0xE0),
            Popup = Rgb(0xFF, 0xFF, 0xFF),
            Track = Rgb(0xF5, 0xC0, 0xD4),
            Playful = true
        },
        new()
        {
            Id = AppTheme.XyxyDark,
            Name = "xyxy's theme",
            Blurb = "Same bows and sparkles, lights out.",
            Accent = Rgb(0xF4, 0x72, 0xB6),
            AccentHover = Rgb(0xFB, 0x8C, 0xC4),
            AccentForeground = Rgb(0x2A, 0x10, 0x1C),
            Background = Rgb(0x14, 0x0E, 0x12),
            Surface = Rgb(0x1B, 0x13, 0x18),
            Card = Rgb(0x22, 0x18, 0x1E),
            Sidebar = Rgb(0x10, 0x0B, 0x0E),
            Border = Rgb(0x4A, 0x2A, 0x3A),
            Text = Rgb(0xFC, 0xE8, 0xF0),
            Muted = Rgb(0xC4, 0x8B, 0xA0),
            Danger = Rgb(0xFB, 0x71, 0x85),
            Input = Rgb(0x2A, 0x1C, 0x24),
            Hover = Rgb(0x3A, 0x24, 0x30),
            Popup = Rgb(0x1B, 0x13, 0x18),
            Track = Rgb(0x4A, 0x2A, 0x3A),
            Playful = true
        }
    };

    /// <summary>The redesigned palettes, used with the Modern style (default).</summary>
    public static readonly ThemePalette[] ModernPalettes =
    {
        new()
        {
            Id = AppTheme.Halloween,
            Name = "Halloween",
            Blurb = "Jack-o'-lantern orange on charcoal — Octane's spooky season. Default for now.",
            Accent = Rgb(0xFF, 0x7A, 0x18),
            AccentHover = Rgb(0xFF, 0x94, 0x32),
            AccentForeground = Rgb(0x1A, 0x0C, 0x04),
            Background = Rgb(0x10, 0x0E, 0x0C),
            Surface = Rgb(0x16, 0x13, 0x11),
            Card = Rgb(0x1B, 0x17, 0x14),
            Sidebar = Rgb(0x0A, 0x09, 0x08),
            Border = Rgb(0x36, 0x2A, 0x1E),
            Text = Rgb(0xF6, 0xEC, 0xDF),
            Muted = Rgb(0xB0, 0x94, 0x78),
            Danger = Rgb(0xE8, 0x52, 0x3C),
            Input = Rgb(0x24, 0x1E, 0x18),
            Hover = Rgb(0x2C, 0x24, 0x1C),
            Popup = Rgb(0x14, 0x11, 0x0F),
            Track = Rgb(0x32, 0x28, 0x1E),
            Playful = true
        },
        new()
        {
            Id = AppTheme.Dark,
            Name = "Midnight",
            Blurb = "Near-black with a violet accent.",
            Accent = Rgb(0x8B, 0x7C, 0xF6),
            AccentHover = Rgb(0x9E, 0x91, 0xFA),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0x0E, 0x0E, 0x12),
            Surface = Rgb(0x13, 0x13, 0x18),
            Card = Rgb(0x16, 0x16, 0x1C),
            Sidebar = Rgb(0x0A, 0x0A, 0x0D),
            Border = Rgb(0x25, 0x25, 0x2E),
            Text = Rgb(0xEC, 0xEC, 0xF1),
            Muted = Rgb(0x8B, 0x8B, 0x99),
            Danger = Rgb(0xE5, 0x48, 0x4D),
            Input = Rgb(0x1B, 0x1B, 0x22),
            Hover = Rgb(0x20, 0x20, 0x28),
            Popup = Rgb(0x18, 0x18, 0x1F),
            Track = Rgb(0x26, 0x26, 0x30)
        },
        new()
        {
            Id = AppTheme.Octane,
            Name = "Octane",
            Blurb = "Octane's own black and a vivid purple.",
            // Brand purple: brighter and more saturated than Midnight's soft violet, on purple-tinted blacks
            // (Midnight's surfaces are neutral). White on the accent and the accent on the window both pass 4.4:1.
            Accent = Rgb(0x9F, 0x45, 0xF2),
            AccentHover = Rgb(0xB0, 0x62, 0xF7),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0x08, 0x06, 0x0C),
            Surface = Rgb(0x0C, 0x09, 0x12),
            Card = Rgb(0x11, 0x0C, 0x18),
            Sidebar = Rgb(0x05, 0x03, 0x08),
            Border = Rgb(0x2A, 0x1F, 0x3A),
            Text = Rgb(0xEE, 0xEA, 0xF5),
            Muted = Rgb(0xA1, 0x95, 0xB8),
            Danger = Rgb(0xF2, 0x55, 0x5A),
            Input = Rgb(0x16, 0x0F, 0x20),
            Hover = Rgb(0x1D, 0x14, 0x2A),
            Popup = Rgb(0x12, 0x0D, 0x1A),
            Track = Rgb(0x2C, 0x20, 0x3E)
        },
        new()
        {
            Id = AppTheme.Dusk,
            Name = "Dusk",
            Blurb = "Soft plum shadow and warm clay.",
            Accent = Rgb(0xD9, 0x73, 0x5B),
            AccentHover = Rgb(0xE5, 0x87, 0x6F),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0x14, 0x12, 0x17),
            Surface = Rgb(0x19, 0x16, 0x1D),
            Card = Rgb(0x1C, 0x19, 0x21),
            Sidebar = Rgb(0x10, 0x0E, 0x13),
            Border = Rgb(0x2C, 0x28, 0x33),
            Text = Rgb(0xEE, 0xE8, 0xE4),
            Muted = Rgb(0x9A, 0x90, 0x99),
            Danger = Rgb(0xE5, 0x53, 0x4B),
            Input = Rgb(0x22, 0x1E, 0x27),
            Hover = Rgb(0x27, 0x23, 0x2D),
            Popup = Rgb(0x1B, 0x18, 0x20),
            Track = Rgb(0x2C, 0x28, 0x33)
        },
        new()
        {
            Id = AppTheme.Light,
            Name = "Paper",
            Blurb = "Warm white and a deep red.",
            Accent = Rgb(0xB9, 0x3A, 0x32),
            AccentHover = Rgb(0xA3, 0x31, 0x2A),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0xF6, 0xF4, 0xF0),
            Surface = Rgb(0xFB, 0xFA, 0xF7),
            Card = Rgb(0xFF, 0xFF, 0xFF),
            Sidebar = Rgb(0xEF, 0xEC, 0xE6),
            Border = Rgb(0xE3, 0xDE, 0xD5),
            Text = Rgb(0x1F, 0x1B, 0x16),
            Muted = Rgb(0x6F, 0x67, 0x5C),
            Danger = Rgb(0xC0, 0x36, 0x2C),
            Input = Rgb(0xFF, 0xFF, 0xFF),
            Hover = Rgb(0xE9, 0xE5, 0xDE),
            Popup = Rgb(0xFF, 0xFF, 0xFF),
            Track = Rgb(0xE3, 0xDE, 0xD5)
        },
        new()
        {
            Id = AppTheme.Classic,
            Name = "Slate",
            Blurb = "Cool gray and a crisp blue. The old launcher, tidied up.",
            Accent = Rgb(0x25, 0x63, 0xEB),
            AccentHover = Rgb(0x1D, 0x4E, 0xD8),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0xF3, 0xF4, 0xF6),
            Surface = Rgb(0xF9, 0xFA, 0xFB),
            Card = Rgb(0xFF, 0xFF, 0xFF),
            Sidebar = Rgb(0xE9, 0xEB, 0xEF),
            Border = Rgb(0xDC, 0xE0, 0xE6),
            Text = Rgb(0x16, 0x18, 0x1D),
            Muted = Rgb(0x5F, 0x66, 0x73),
            Danger = Rgb(0xDC, 0x26, 0x26),
            Input = Rgb(0xFF, 0xFF, 0xFF),
            Hover = Rgb(0xE3, 0xE6, 0xEB),
            Popup = Rgb(0xFF, 0xFF, 0xFF),
            Track = Rgb(0xDC, 0xE0, 0xE6)
        },
        new()
        {
            Id = AppTheme.Xyxy,
            Name = "xyxy's theme",
            Blurb = "Blush, bows, and sparkles. Light and a little extra.",
            Accent = Rgb(0xE0, 0x55, 0x9A),
            AccentHover = Rgb(0xD1, 0x43, 0x89),
            AccentForeground = Rgb(0xFF, 0xFF, 0xFF),
            Background = Rgb(0xFF, 0xF6, 0xF9),
            Surface = Rgb(0xFF, 0xFB, 0xFC),
            Card = Rgb(0xFF, 0xFF, 0xFF),
            Sidebar = Rgb(0xFF, 0xEE, 0xF4),
            Border = Rgb(0xF6, 0xD5, 0xE2),
            Text = Rgb(0x3F, 0x22, 0x33),
            Muted = Rgb(0xA0, 0x74, 0x89),
            Danger = Rgb(0xE1, 0x1D, 0x48),
            Input = Rgb(0xFF, 0xFF, 0xFF),
            Hover = Rgb(0xFF, 0xE4, 0xEE),
            Popup = Rgb(0xFF, 0xFF, 0xFF),
            Track = Rgb(0xF6, 0xD5, 0xE2),
            Playful = true
        },
        new()
        {
            Id = AppTheme.XyxyDark,
            Name = "xyxy's theme",
            Blurb = "Same bows and sparkles, lights out.",
            Accent = Rgb(0xF4, 0x72, 0xB6),
            AccentHover = Rgb(0xF9, 0x8B, 0xC6),
            AccentForeground = Rgb(0x2A, 0x0F, 0x1C),
            Background = Rgb(0x12, 0x0D, 0x11),
            Surface = Rgb(0x17, 0x11, 0x16),
            Card = Rgb(0x1B, 0x14, 0x1A),
            Sidebar = Rgb(0x0E, 0x0A, 0x0D),
            Border = Rgb(0x33, 0x23, 0x2D),
            Text = Rgb(0xFB, 0xEA, 0xF2),
            Muted = Rgb(0xBC, 0x8C, 0xA3),
            Danger = Rgb(0xFB, 0x71, 0x85),
            Input = Rgb(0x22, 0x18, 0x20),
            Hover = Rgb(0x2A, 0x1D, 0x27),
            Popup = Rgb(0x1A, 0x13, 0x19),
            Track = Rgb(0x33, 0x23, 0x2D),
            Playful = true
        }
    };

    public static UiStyle Style { get; private set; } = UiStyle.Modern;

    public static bool IsModern => Style == UiStyle.Modern;

    /// <summary>The palettes for the active style.</summary>
    public static ThemePalette[] All => PalettesFor(Style);

    public static ThemePalette Current { get; private set; } = ModernPalettes[0];

    public static ThemePalette[] PalettesFor(UiStyle style) =>
        style == UiStyle.Classic ? ClassicPalettes : ModernPalettes;

    public static ThemePalette Get(AppTheme theme) => Get(theme, Style);

    public static ThemePalette Get(AppTheme theme, UiStyle style)
    {
        var palettes = PalettesFor(style);
        return palettes.FirstOrDefault(item => item.Id == theme) ?? palettes[0];
    }

    public static bool IsXyxy(AppTheme theme) => theme is AppTheme.Xyxy or AppTheme.XyxyDark;

    private static readonly Dictionary<UiStyle, ResourceDictionary> StyleDictionaries = new();

    /// <summary>
    /// What picking a theme or style in the UI does: repaint first, then remember it. The save used to come first, so a
    /// save that threw (Settings.json briefly locked by another X Bootstrapper process, antivirus or a sync tool) aborted
    /// the pick before anything repainted, and the choice only landed on disk with the next save.
    /// </summary>
    public static void Pick(AppTheme theme, UiStyle style, string source)
    {
        Logger.Write("Theme", $"{source}: {theme} ({style})");
        App.Settings.Prop.Theme = theme;
        App.Settings.Prop.UiStyle = style;
        Apply(theme, style);
        try
        {
            App.Save();
        }
        catch (Exception ex)
        {
            Logger.Error("Theme", ex);
        }

        UiSound.PlayTheme();
    }

    /// <summary>
    /// Runs <paramref name="pick"/> when the element is pressed. Tiles react on press, not release: the press bounce shrinks
    /// the tile and the page repaints, so a release could land outside it (or on a rebuilt tile) and the pick was lost.
    /// </summary>
    public static void OnPress(UIElement element, Action pick) =>
        element.MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled)
                return;
            e.Handled = true;
            pick();
        };

    /// <summary>Applies a theme in the current style.</summary>
    public static void Apply(AppTheme theme) => Apply(theme, Style);

    public static void Apply(AppTheme theme, UiStyle style)
    {
        if (Application.Current is null)
            return;

        var palette = Get(theme, style);
        if (style != Style)
            StyleChanging?.Invoke(style);
        Style = style;
        Current = palette;

        var merged = Application.Current.Resources.MergedDictionaries;
        var styles = StyleDictionary(style);
        var colors = PaletteDictionary(palette);
        if (merged.Count == 2)
        {
            if (!ReferenceEquals(merged[0], styles))
                merged[0] = styles;
            merged[1] = colors;
        }
        else
        {
            merged.Clear();
            merged.Add(styles);
            merged.Add(colors);
        }

        Changed?.Invoke();
        SyncChromeIcons();
    }

    private static bool _chromeHooked;

    /// <summary>
    /// Taskbar / title-bar / tray: swap to the theme's logo so Halloween gets the dripping X, Octane purple, etc.
    /// </summary>
    public static void SyncChromeIcons()
    {
        EnsureChromeHook();
        if (Application.Current is null)
            return;

        var icon = Logo(Current.Id);
        foreach (Window window in Application.Current.Windows)
            window.Icon = icon;

        TrayService.ApplyThemeIcon();
    }

    private static void EnsureChromeHook()
    {
        if (_chromeHooked)
            return;
        _chromeHooked = true;
        // New windows (bootstrapper, setup, what's new…) pick up the current theme logo on load.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(static (sender, _) =>
            {
                if (sender is Window window)
                    window.Icon = Logo(Current.Id);
            }));
    }

    private static ResourceDictionary StyleDictionary(UiStyle style)
    {
        if (!StyleDictionaries.TryGetValue(style, out var dictionary))
        {
            // Reuse the copy App.xaml already loaded instead of parsing it twice.
            var merged = Application.Current.Resources.MergedDictionaries;
            var existing = merged.FirstOrDefault(item =>
                item.Source?.OriginalString.EndsWith($"{style}.xaml", StringComparison.OrdinalIgnoreCase) == true);
            if (existing is not null)
            {
                StyleDictionaries[style] = existing;
                return existing;
            }


            dictionary = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/UI/Themes/{style}.xaml", UriKind.Absolute)
            };
            StyleDictionaries[style] = dictionary;
        }

        return dictionary;
    }

    /// <summary>The per-theme color dictionary (same keys in both styles).</summary>
    public static ResourceDictionary PaletteDictionary(ThemePalette palette)
    {
        var resources = new ResourceDictionary
        {
            ["AccentColor"] = palette.Accent
        };
        Set(resources, "AccentBrush", palette.Accent);
        Set(resources, "AccentHoverBrush", palette.AccentHover);
        Set(resources, "AccentForegroundBrush", palette.AccentForeground);
        Set(resources, "AccentSoftBrush", Color.FromArgb(0x36, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        Set(resources, "AccentSubtleBrush", Color.FromArgb(0x1F, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        Set(resources, "BackgroundBrush", palette.Background);
        Set(resources, "SurfaceBrush", palette.Surface);
        Set(resources, "CardBrush", palette.Card);
        Set(resources, "SidebarBrush", palette.Sidebar);
        Set(resources, "BorderBrush", palette.Border);
        Set(resources, "TextBrush", palette.Text);
        Set(resources, "MutedBrush", palette.Muted);
        Set(resources, "DangerBrush", palette.Danger);
        Set(resources, "InputBrush", palette.Input);
        Set(resources, "HoverBrush", palette.Hover);
        Set(resources, "PopupBrush", palette.Popup);
        Set(resources, "TrackBrush", palette.Track);
        resources["LogoImage"] = Logo(palette.Id);
        // The neon logos glow on dark themes straight over the window; light themes (and the original mark) keep the dark tile.
        var dark = (palette.Background.R * 0.299 + palette.Background.G * 0.587 + palette.Background.B * 0.114) < 96;
        Set(resources, "LogoTileBrush", LogoFile(palette.Id) is not null && dark ? Colors.Transparent : Rgb(0x0B, 0x0B, 0x0D));
        return resources;
    }

    /// <summary>
    /// Per-theme logo: the neon "X in a broken circle" whose color matches the theme's accent.
    /// Halloween uses the dripping pumpkin-neon mark; themes without a match keep the original.
    /// </summary>
    public static string? LogoFile(AppTheme theme) => theme switch
    {
        AppTheme.Halloween => "logo-halloween.png",
        AppTheme.Octane => "logo-purple.png",
        AppTheme.Classic => "logo-blue.png",
        AppTheme.Light or AppTheme.Dusk => "logo-red.png",
        AppTheme.Xyxy or AppTheme.XyxyDark => "logo-pink.png",
        _ => null
    };

    private static readonly Dictionary<string, System.Windows.Media.Imaging.BitmapImage> Logos = new();

    public static System.Windows.Media.Imaging.BitmapImage Logo(AppTheme theme)
    {
        var file = LogoFile(theme);
        var uri = file is null ? "pack://application:,,,/Assets/x-mark.png" : $"pack://application:,,,/Assets/logos/{file}";
        if (!Logos.TryGetValue(uri, out var image))
        {
            image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(uri, UriKind.Absolute);
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 256;
            image.EndInit();
            image.Freeze();
            Logos[uri] = image;
        }

        return image;
    }

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
