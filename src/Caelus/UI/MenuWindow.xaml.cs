using System.Windows;
using Caelus.Core;
using Caelus.UI.Pages;

namespace Caelus.UI;

public partial class MenuWindow : Window
{
    public MenuWindow()
    {
        InitializeComponent();
        Closed += (_, _) => App.RequestExitIfIdle();
        PlayfulMotion.Attach(this, SparkleLayer);
        ThemeService.Changed += RefreshPlayfulCopy;
        Closed += (_, _) => ThemeService.Changed -= RefreshPlayfulCopy;
        RefreshPlayfulCopy();
        PageHost.Content = new ModsPage();
    }

    private void RefreshPlayfulCopy()
    {
        var playful = PlayfulMotion.IsPlayful;
        BrandTitle.Text = playful ? $"{AppInfo.Name} ✨" : AppInfo.Name;
        BrandSub.Text = playful ? "for Octane 🎀" : "for Octane";
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

        if (sender == NavInstall) PageHost.Content = new InstallPage();
        else if (sender == NavBehaviour) PageHost.Content = new BehaviourPage();
        else if (sender == NavAppearance) PageHost.Content = new AppearancePage();
        else if (sender == NavIntegrations) PageHost.Content = new IntegrationsPage();
        else if (sender == NavFlags) PageHost.Content = new FastFlagsPage();
        else if (sender == NavMods) PageHost.Content = new ModsPage();
        else if (sender == NavAbout) PageHost.Content = new AboutPage();

        if (IsLoaded)
            PlayfulMotion.PopIn(PageHost);
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
