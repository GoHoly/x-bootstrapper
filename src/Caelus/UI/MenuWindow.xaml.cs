using System.Windows;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;
using Caelus.UI.Pages;

namespace Caelus.UI;

public partial class MenuWindow : Window
{
    public MenuWindow()
    {
        InitializeComponent();
        Closed += (_, _) => App.RequestExitIfIdle();
        PlayfulMotion.Attach(this, SparkleLayer);
        _style = ThemeService.Style;
        ThemeService.Changed += OnThemeChanged;
        ThemeService.StyleChanging += OnStyleChanging;
        Closed += (_, _) =>
        {
            ThemeService.Changed -= OnThemeChanged;
            ThemeService.StyleChanging -= OnStyleChanging;
        };
        RefreshPlayfulCopy();
        PageHost.Content = new ModsPage();

        // Link health: checked when the menu opens, whenever it's focused again, and every few seconds while
        // it's open (Octane's launcher can take the links back while the menu sits in the background).
        _linkTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _linkTimer.Tick += (_, _) => { if (IsVisible) RefreshLinkStatus(); };
        Loaded += (_, _) =>
        {
            RefreshLinkStatus();
            _linkTimer.Start();
            if (ShowIntroWindows)
                Dispatcher.BeginInvoke(ShowIntro, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };
        Activated += (_, _) => RefreshLinkStatus();
        Closed += (_, _) => _linkTimer.Stop();
        ApplyBannerMargin();
    }

    private readonly System.Windows.Threading.DispatcherTimer _linkTimer;

    /// <summary>Off for screenshot runs that open many menus; the intro tests open these windows themselves.</summary>
    internal static bool ShowIntroWindows { get; set; } = true;

    /// <summary>First-run setup for a new profile, otherwise What's new once after an update.</summary>
    private void ShowIntro()
    {
        if (!IsVisible)
            return;
        if (App.Settings.Prop.SetupPending)
            SetupWindow.ShowFor(this);
        else if (App.WhatsNewDue)
            WhatsNewWindow.ShowFor(this, markShown: true);
    }

    internal bool LinkWarningVisible => LinkBannerHost.Visibility == Visibility.Visible;

    /// <summary>Shows or hides the "links taken over" warning. Returns the statuses it saw.</summary>
    internal IReadOnlyList<ProtocolService.LinkStatus> RefreshLinkStatus()
    {
        IReadOnlyList<ProtocolService.LinkStatus> links;
        try
        {
            links = ProtocolService.CheckLinks();
        }
        catch (Exception ex)
        {
            Logger.Write("Protocol", $"Could not read the website links: {ex.Message}");
            return Array.Empty<ProtocolService.LinkStatus>();
        }

        var watch = App.Settings.Prop.RegisterWebsiteProtocol && (InstallerService.IsInstalled(App.Settings.Prop) || UiShots.CheckLinksAnyway);
        var broken = watch ? links.Where(link => !link.Healthy).ToList() : new List<ProtocolService.LinkStatus>();
        if (broken.Count == 0)
        {
            LinkBannerHost.Visibility = Visibility.Collapsed;
            return links;
        }

        var names = string.Join(" and ", broken.Select(link => link.Scheme + "://"));
        var owners = broken.Select(link => link.OwnerName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var now = owners.All(owner => owner is null)
            ? (broken.Count == 1 ? "is not registered" : "are not registered")
            : $"now open{(broken.Count == 1 ? "s" : "")} {string.Join(", ", owners.Select(owner => owner ?? "nothing"))}";
        var who = owners.Any(owner => owner is not null && owner.Contains("OctanePlayerLauncher", StringComparison.OrdinalIgnoreCase))
            ? " (Octane's launcher takes them back whenever it runs)"
            : "";
        LinkBannerText.Text = $"{names} {now}{who}, so Play on the website skips your mods, FastFlags and Discord status choice.";
        if (LinkBannerHost.Visibility != Visibility.Visible)
            Logger.Write("Protocol", $"Link warning shown: {names} {now}");
        LinkBannerHost.Visibility = Visibility.Visible;
        return links;
    }

    private void FixLinks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ProtocolService.Register(App.Settings.Prop, App.State.Prop);
            Native.NotifyShell();
            App.Save();
            Logger.Write("Protocol", "Links fixed from the menu");
        }
        catch (Exception ex)
        {
            Logger.Error("Protocol", ex);
            System.Windows.MessageBox.Show($"Could not fix the links.\n\n{ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshLinkStatus();
    }

    private void ApplyBannerMargin() =>
        LinkBannerHost.Margin = ThemeService.IsModern ? new Thickness(40, 18, 36, 0) : new Thickness(32, 16, 28, 0);

    private UiStyle _style;

    /// <summary>The control that switched the style (by name), where it sat in the page viewport, and the scroll offset.</summary>
    private (string? Name, double Y, double Offset)? _anchor;

    private void OnStyleChanging(UiStyle next)
    {
        // Recorded before the swap, while the page is still laid out in the old style.
        _anchor = null;
        if (PageHost?.Content is not FrameworkElement page || !page.IsLoaded)
            return;

        _anchor = (null, 0, PageScroll.VerticalOffset);
        if (System.Windows.Input.Keyboard.FocusedElement is FrameworkElement { Name.Length: > 0 } focused &&
            focused.IsVisible && page.IsAncestorOf(focused))
        {
            var y = focused.TranslatePoint(new System.Windows.Point(0, focused.ActualHeight / 2), PageScroll).Y;
            _anchor = (focused.Name, y, PageScroll.VerticalOffset);
        }
    }

    private void OnThemeChanged()
    {
        RefreshPlayfulCopy();
        ApplyBannerMargin();
        if (ThemeService.Style == _style)
            return;

        // Code-built rows pick their styles when created, so rebuild the open page for the new style.
        _style = ThemeService.Style;
        Dispatcher.BeginInvoke(ReloadPage);
    }

    private void ReloadPage()
    {
        var nav = new[] { NavMods, NavFlags, NavAppearance, NavBehaviour, NavIntegrations, NavInstall, NavAbout }
            .FirstOrDefault(item => item.IsChecked == true);
        if (nav is null)
            return;

        PageHost.Content = CreatePage(nav);
        if (_anchor is not { } anchor)
            return;

        // Keep the rebuilt page where it was: the control that was just clicked (the Classic style toggle)
        // stays under the pointer and keeps focus, so it can be clicked again or flipped with Space.
        _anchor = null;
        PageScroll.UpdateLayout();
        if (anchor.Name is not null && PageHost.Content is FrameworkElement page &&
            page.FindName(anchor.Name) is FrameworkElement target && target.IsVisible)
        {
            var y = target.TranslatePoint(new System.Windows.Point(0, target.ActualHeight / 2), PageScroll).Y;
            PageScroll.ScrollToVerticalOffset(Math.Max(0, PageScroll.VerticalOffset + y - anchor.Y));
            PageScroll.UpdateLayout();
            target.Focus();
        }
        else
        {
            PageScroll.ScrollToVerticalOffset(anchor.Offset);
        }
    }

    /// <summary>Opens a page by its nav name (NavMods, NavAbout, ...) and returns it.</summary>
    internal object? Navigate(string navName)
    {
        if (FindName(navName) is System.Windows.Controls.RadioButton nav)
        {
            if (nav.IsChecked == true)
                PageHost.Content = CreatePage(nav);
            else
                nav.IsChecked = true;
        }

        return PageHost.Content;
    }

    private void RefreshPlayfulCopy()
    {
        var themePlayful = PlayfulMotion.IsPlayful;
        BrandTitle.Text = themePlayful ? $"{AppInfo.Name} ✨" : AppInfo.Name;
        BrandSub.Text = themePlayful ? "for Octane 🎀" : "for Octane";
        // Modern nav items have icons, so the emoji suffixes are Classic-only.
        var playful = themePlayful && !ThemeService.IsModern;
        NavMods.Content = playful ? "Mods 🧁" : "Mods";
        NavFlags.Content = playful ? "Fast Flags ⭐" : "Fast Flags";
        NavAppearance.Content = playful ? "Appearance 💗" : "Appearance";
        NavBehaviour.Content = playful ? "Behaviour 🌸" : "Behaviour";
        NavIntegrations.Content = playful ? "Integrations 💎" : "Integrations";
        NavInstall.Content = playful ? "Install 🌷" : "Install";
        NavAbout.Content = playful ? "About 🍓" : "About";
        LaunchPlayerButton.Content = playful ? "Launch Octane 💕" : "Launch Octane";
        LaunchStudioButton.Content = playful ? "Studio 🎀" : "Studio";
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageHost is null)
            return;

        PageHost.Content = CreatePage(sender);

        if (IsLoaded)
            PlayfulMotion.PopIn(PageHost);
    }

    private object? CreatePage(object sender)
    {
        if (sender == NavInstall) return new InstallPage();
        if (sender == NavBehaviour) return new BehaviourPage();
        if (sender == NavAppearance) return new AppearancePage();
        if (sender == NavIntegrations) return new IntegrationsPage();
        if (sender == NavFlags) return new FastFlagsPage();
        if (sender == NavMods) return new ModsPage();
        if (sender == NavAbout) return new AboutPage();
        return PageHost.Content;
    }

    private void LaunchPlayer_Click(object sender, RoutedEventArgs e) => Launch(LaunchMode.Player);

    private void LaunchStudio_Click(object sender, RoutedEventArgs e) => Launch(LaunchMode.Studio);

    private void Launch(LaunchMode mode)
    {
        if (PageHost.Content is FastFlagsPage flags)
            flags.Flush();
        App.Save();
        if (App.Settings.Prop.ConfirmLaunches &&
            System.Windows.MessageBox.Show($"Launch Octane {(mode == LaunchMode.Studio ? "Studio" : "Player")}?", AppInfo.Name, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        App.LaunchOctane(mode);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (PageHost.Content is FastFlagsPage flags)
            flags.Flush();
        App.Save();
        Close();
    }
}
