using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Caelus.Models;

namespace Caelus.UI.Pages;

public partial class AppearancePage : System.Windows.Controls.UserControl
{
    public AppearancePage()
    {
        InitializeComponent();
        StyleBox.SelectedIndex = App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.Classic ? 1 : 0;
        StyleBox.SelectionChanged += (_, _) =>
        {
            App.Settings.Prop.BootstrapperStyle = StyleBox.SelectedIndex == 1 ? BootstrapperStyle.Classic : BootstrapperStyle.Fluent;
            App.Save();
        };
        SoundsBox.IsChecked = App.Settings.Prop.UiSounds;
        SoundsBox.Checked += (_, _) => { App.Settings.Prop.UiSounds = true; App.Save(); };
        SoundsBox.Unchecked += (_, _) => { App.Settings.Prop.UiSounds = false; App.Save(); };
        RebuildThemes();
        ThemeService.Changed += RebuildThemes;
        Unloaded += (_, _) => ThemeService.Changed -= RebuildThemes;
    }

    private void RebuildThemes()
    {
        ThemePanel.Children.Clear();
        foreach (var palette in ThemeService.All)
        {
            if (palette.Id == AppTheme.XyxyDark)
                continue;
            ThemePanel.Children.Add(palette.Id == AppTheme.Xyxy ? CreateXyxyTile() : CreateTile(palette));
        }
    }

    private UIElement CreateXyxyTile()
    {
        var dark = App.Settings.Prop.Theme == AppTheme.XyxyDark;
        var selected = ThemeService.IsXyxy(App.Settings.Prop.Theme);
        var palette = ThemeService.Get(selected && dark ? AppTheme.XyxyDark : AppTheme.Xyxy);
        var card = (Border)CreateTile(palette, bindClick: false);
        card.Width = 196;
        card.BorderThickness = new Thickness(selected ? 2 : 1);
        card.BorderBrush = new SolidColorBrush(selected ? palette.Accent : palette.Border);

        var stack = (StackPanel)card.Child;
        if (stack.Children[1] is TextBlock title)
            title.Text = dark && selected ? "xyxy's theme 🌙" : "xyxy's theme 🎀";
        if (stack.Children[2] is TextBlock blurb)
            blurb.Text = dark && selected
                ? "Same bows and sparkles, lights out."
                : "Blush, bows, and sparkles. Light and a little extra.";

        var toggle = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(2, 8, 2, 0)
        };
        toggle.Children.Add(ModeChip("Light ✨", selected && !dark, () => Select(AppTheme.Xyxy), palette));
        toggle.Children.Add(ModeChip("Dark 🌙", selected && dark, () => Select(AppTheme.XyxyDark), palette));
        stack.Children.Add(toggle);

        card.MouseLeftButtonUp += (_, _) => Select(selected && dark ? AppTheme.XyxyDark : AppTheme.Xyxy);
        return card;
    }

    private static Border ModeChip(string label, bool on, Action pick, ThemePalette palette)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(on ? palette.Accent : palette.Input),
            BorderBrush = new SolidColorBrush(on ? palette.Accent : palette.Border),
            BorderThickness = new Thickness(1)
        };
        chip.Child = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = new SolidColorBrush(on ? palette.AccentForeground : palette.Muted)
        };
        chip.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            pick();
        };
        return chip;
    }

    private UIElement CreateTile(ThemePalette palette, bool bindClick = true)
    {
        var selected = palette.Id == App.Settings.Prop.Theme ||
                       (ThemeService.IsXyxy(palette.Id) && ThemeService.IsXyxy(App.Settings.Prop.Theme));
        var card = new Border
        {
            Width = 168,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(selected ? 2 : 1),
            BorderBrush = new SolidColorBrush(selected ? palette.Accent : palette.Border),
            Background = new SolidColorBrush(palette.Card),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = palette.Id
        };

        var preview = new Border
        {
            Height = 52,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(palette.Background),
            ClipToBounds = true
        };

        var host = new Grid();
        host.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = 22,
            Fill = new SolidColorBrush(palette.Sidebar),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        });
        host.Children.Add(new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(palette.Accent),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 8, 8)
        });
        preview.Child = host;

        var stack = new StackPanel();
        stack.Children.Add(preview);
        stack.Children.Add(new TextBlock
        {
            Text = palette.Playful ? palette.Name + " 🎀" : palette.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(palette.Text),
            Margin = new Thickness(2, 8, 2, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = palette.Blurb,
            FontSize = 11,
            Foreground = new SolidColorBrush(palette.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 2, 2, 2)
        });
        card.Child = stack;
        if (bindClick)
            card.MouseLeftButtonUp += (_, _) => Select(palette.Id);
        return card;
    }

    private void Select(AppTheme theme)
    {
        App.Settings.Prop.Theme = theme;
        App.Save();
        ThemeService.Apply(theme);
        UiSound.PlayTheme();
    }
}
