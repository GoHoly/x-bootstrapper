using System.Windows.Controls;

namespace Caelus.UI.Pages;

public partial class BehaviourPage : System.Windows.Controls.UserControl
{
    public BehaviourPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
        UpdatesBox.IsChecked = s.CheckForClientUpdates;
        AppUpdatesBox.IsChecked = s.CheckForAppUpdates;
        MultiBox.IsChecked = s.MultiInstance;
        ConfirmBox.IsChecked = s.ConfirmLaunches;
        StayBox.IsChecked = s.StayOpenAfterLaunch;
        UpdatesBox.Checked += (_, _) => Write();
        UpdatesBox.Unchecked += (_, _) => Write();
        AppUpdatesBox.Checked += (_, _) => Write();
        AppUpdatesBox.Unchecked += (_, _) => Write();
        MultiBox.Checked += (_, _) => Write();
        MultiBox.Unchecked += (_, _) => Write();
        ConfirmBox.Checked += (_, _) => Write();
        ConfirmBox.Unchecked += (_, _) => Write();
        StayBox.Checked += (_, _) => Write();
        StayBox.Unchecked += (_, _) => Write();
    }

    private void Write()
    {
        var s = App.Settings.Prop;
        s.CheckForClientUpdates = UpdatesBox.IsChecked == true;
        s.CheckForAppUpdates = AppUpdatesBox.IsChecked == true;
        s.MultiInstance = MultiBox.IsChecked == true;
        s.ConfirmLaunches = ConfirmBox.IsChecked == true;
        s.StayOpenAfterLaunch = StayBox.IsChecked == true;
        App.Save();
    }
}
