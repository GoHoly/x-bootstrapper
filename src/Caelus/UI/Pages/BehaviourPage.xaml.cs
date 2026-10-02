using System.Windows.Controls;

namespace Caelus.UI.Pages;

public partial class BehaviourPage : System.Windows.Controls.UserControl
{
    public BehaviourPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
        AppUpdatesBox.IsChecked = s.CheckForAppUpdates;
        ConfirmBox.IsChecked = s.ConfirmLaunches;
        StayBox.IsChecked = s.StayOpenAfterLaunch;
        TrayBox.IsChecked = s.ShowTrayIcon;
        BackgroundBox.IsChecked = s.KeepRunningInBackground;
        AppBetaBox.IsChecked = s.LaunchAppBeta;
        BackgroundHint.Text = UiText.BackgroundHint;
        BackgroundBox.Checked += (_, _) => Write();
        BackgroundBox.Unchecked += (_, _) => Write();
        TrayBox.Checked += (_, _) => Write();
        TrayBox.Unchecked += (_, _) => Write();
        AppUpdatesBox.Checked += (_, _) => Write();
        AppUpdatesBox.Unchecked += (_, _) => Write();
        ConfirmBox.Checked += (_, _) => Write();
        ConfirmBox.Unchecked += (_, _) => Write();
        StayBox.Checked += (_, _) => Write();
        StayBox.Unchecked += (_, _) => Write();
        AppBetaBox.Checked += (_, _) => Write();
        AppBetaBox.Unchecked += (_, _) => Write();
    }

    private void Write()
    {
        var s = App.Settings.Prop;
        s.CheckForAppUpdates = AppUpdatesBox.IsChecked == true;
        s.ConfirmLaunches = ConfirmBox.IsChecked == true;
        s.StayOpenAfterLaunch = StayBox.IsChecked == true;
        s.ShowTrayIcon = TrayBox.IsChecked == true;
        s.KeepRunningInBackground = BackgroundBox.IsChecked == true;
        s.LaunchAppBeta = AppBetaBox.IsChecked == true;
        App.Save();
    }
}
