using System.Windows;
using System.Windows.Media;
using Caelus.Models;
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

public static class ThemeService
{
    public static event Action? Changed;

    public static readonly ThemePalette[] All =
    {
        new()
        {
            Id = AppTheme.Dark,
            Name = "Midnight",
            Blurb = "Dark wood and signal red. Default.",
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

    public static ThemePalette Current { get; private set; } = All[0];

    public static ThemePalette Get(AppTheme theme) =>
        All.FirstOrDefault(item => item.Id == theme) ?? All[0];

    public static bool IsXyxy(AppTheme theme) => theme is AppTheme.Xyxy or AppTheme.XyxyDark;

    public static void Apply(AppTheme theme)
    {
        if (Application.Current is null)
            return;

        var palette = Get(theme);
        Current = palette;
        var resources = Application.Current.Resources;

        resources["AccentColor"] = palette.Accent;
        Set(resources, "AccentBrush", palette.Accent);
        Set(resources, "AccentHoverBrush", palette.AccentHover);
        Set(resources, "AccentForegroundBrush", palette.AccentForeground);
        Set(resources, "AccentSoftBrush", Color.FromArgb(0x36, palette.Accent.R, palette.Accent.G, palette.Accent.B));
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

        Changed?.Invoke();
    }

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = new SolidColorBrush(color);
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
