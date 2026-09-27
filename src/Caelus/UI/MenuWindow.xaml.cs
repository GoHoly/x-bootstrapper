using System.Windows;
using Caelus.Core;
using Caelus.Models;
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
        Closed += (_, _) => ThemeService.Changed -= OnThemeChanged;
        RefreshPlayfulCopy();
        PageHost.Content = new ModsPage();
    }

    private UiStyle _style;

    private void OnThemeChanged()
    {
        RefreshPlayfulCopy();
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
        if (nav is not null)
            PageHost.Content = CreatePage(nav);
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
