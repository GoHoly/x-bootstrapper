using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;

namespace Caelus.UI;

/// <summary>
/// First-run setup: shown once for a new profile (never for people who already had settings), skippable,
/// and reopenable from About. Theme and style, Discord status, website links, background behaviour.
/// </summary>
public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        var s = App.Settings.Prop;

        SetupClassicBox.IsChecked = s.UiStyle == UiStyle.Classic;
        SetupClassicBox.Checked += (_, _) => SetStyle(UiStyle.Classic);
        SetupClassicBox.Unchecked += (_, _) => SetStyle(UiStyle.Modern);

        foreach (var choice in UiText.DiscordChoices)
            DiscordStatusBox.Items.Add(new ComboBoxItem { Content = choice });
        DiscordStatusBox.SelectedIndex = (int)s.EffectiveDiscordStatus;
        DiscordStatusHint.Text = UiText.DiscordHint(s.EffectiveDiscordStatus);
        DiscordStatusBox.SelectionChanged += (_, _) =>
        {
            var mode = DiscordStatusBox.SelectedIndex is >= 0 and <= 2 ? (DiscordStatusMode)DiscordStatusBox.SelectedIndex : DiscordStatusMode.XBootstrapper;
            App.Settings.Prop.SetDiscordStatus(mode);
            DiscordStatusHint.Text = UiText.DiscordHint(mode);
            App.Save();
        };

        SetupBackgroundBox.IsChecked = s.KeepRunningInBackground;
        SetupBackgroundBox.Checked += (_, _) => { App.Settings.Prop.KeepRunningInBackground = true; App.Save(); };
        SetupBackgroundBox.Unchecked += (_, _) => { App.Settings.Prop.KeepRunningInBackground = false; App.Save(); };

        RebuildThemes();
        RefreshLinks();
        ThemeService.Changed += RebuildThemes;
        Closed += (_, _) =>
        {
            ThemeService.Changed -= RebuildThemes;
            App.RequestExitIfIdle();
        };
    }

    public static SetupWindow ShowFor(Window? owner)
    {
        var existing = Application.Current.Windows.OfType<SetupWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return existing;
        }

        var window = new SetupWindow();
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Show();
        Logger.Write("App", "Showed the first-run setup");
        return window;
    }

    internal bool LinksHealthy { get; private set; }

    internal void RefreshLinks()
    {
        var watch = App.Settings.Prop.RegisterWebsiteProtocol && (InstallerService.IsInstalled(App.Settings.Prop) || UiShots.CheckLinksAnyway);
        var broken = ProtocolService.CheckLinks().Where(link => !link.Healthy).ToList();
        LinksHealthy = broken.Count == 0;
        if (!watch)
        {
            LinkIcon.Text = "\uE946";
            LinkIcon.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            LinkStatusText.Text = App.Settings.Prop.RegisterWebsiteProtocol
                ? "X Bootstrapper takes over the octane-player:// and octane-studio:// links once it's installed."
                : "Website links are turned off (Install settings), so Play on octane.wtf uses Octane's own launcher.";
            SetupFixLinksButton.Visibility = Visibility.Collapsed;
            return;
        }

        if (LinksHealthy)
        {
            LinkIcon.Text = "\uE73E";
            LinkIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            LinkStatusText.Text = UiText.LinksHealthy + " Both octane-player:// and octane-studio:// point here.";
            SetupFixLinksButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            LinkIcon.Text = "\uE7BA";
            LinkIcon.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
            var owners = string.Join(", ", broken.Select(link => $"{link.Scheme}:// opens {link.OwnerName ?? "nothing"}"));
            LinkStatusText.Text = $"Play on octane.wtf is skipping X Bootstrapper ({owners}). Fix links points them back here.";
            SetupFixLinksButton.Visibility = Visibility.Visible;
        }
    }

    private void FixLinks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ProtocolService.Register(App.Settings.Prop, App.State.Prop);
            Native.NotifyShell();
            App.Save();
            Logger.Write("Protocol", "Links fixed from the first-run setup");
        }
        catch (Exception ex)
        {
            Logger.Error("Protocol", ex);
        }

        RefreshLinks();
    }

    private void RebuildThemes()
    {
        ThemePanel.Children.Clear();
        foreach (var palette in ThemeService.All)
            ThemePanel.Children.Add(Tile(palette));
    }

    private UIElement Tile(ThemePalette palette)
    {
        var selected = palette.Id == App.Settings.Prop.Theme;
        var modern = ThemeService.IsModern;
        var tile = new Border
        {
            Width = 120,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(modern ? 8 : 3),
            BorderThickness = new Thickness(selected ? 2 : 1),
            BorderBrush = new SolidColorBrush(selected ? palette.Accent : palette.Border),
            Background = new SolidColorBrush(palette.Card),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = palette.Id
        };
        var preview = new Grid { Height = 30 };
        preview.Children.Add(new Border { CornerRadius = new CornerRadius(modern ? 5 : 2), Background = new SolidColorBrush(palette.Background) });
        preview.Children.Add(new Border
        {
            Width = 16,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(modern ? 5 : 2, 0, 0, modern ? 5 : 2),
            Background = new SolidColorBrush(palette.Sidebar)
        });
        preview.Children.Add(new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(modern ? 4 : 1),
            Background = new SolidColorBrush(palette.Accent),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6)
        });
        var name = palette.Id == AppTheme.XyxyDark ? palette.Name + " (dark)" : palette.Name;
        var stack = new StackPanel();
        stack.Children.Add(preview);
        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = new SolidColorBrush(palette.Text),
            Margin = new Thickness(2, 6, 2, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        tile.Child = stack;
        tile.MouseLeftButtonUp += (_, _) => SelectTheme(palette.Id);
        return tile;
    }

    /// <summary>What a theme tile click does (also used by the developer tests).</summary>
    internal void SelectTheme(AppTheme theme)
    {
        App.Settings.Prop.Theme = theme;
        App.Save();
        ThemeService.Apply(theme);
        UiSound.PlayTheme();
    }

    private static void SetStyle(UiStyle style)
    {
        if (App.Settings.Prop.UiStyle == style && ThemeService.Style == style)
            return;
        App.Settings.Prop.UiStyle = style;
        App.Save();
        ThemeService.Apply(App.Settings.Prop.Theme, style);
        UiSound.PlayTheme();
    }

    private void Finish(bool skipped)
    {
        App.Settings.Prop.SetupPending = false;
        App.Save();
        Logger.Write("App", skipped ? "First-run setup skipped" : "First-run setup done");
        Close();
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Finish(skipped: false);

    private void Skip_Click(object sender, RoutedEventArgs e) => Finish(skipped: true);
}
