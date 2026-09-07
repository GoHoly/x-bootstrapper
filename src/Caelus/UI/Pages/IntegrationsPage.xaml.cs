using System.Windows.Controls;

namespace Caelus.UI.Pages;

public partial class IntegrationsPage : System.Windows.Controls.UserControl
{
    public IntegrationsPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
        DiscordBox.IsChecked = s.DiscordRichPresence;
        DiscordIdBox.Text = s.DiscordClientId;
        ActivityBox.IsChecked = s.ActivityTracking;
        DiscordBox.Checked += (_, _) => Write();
        DiscordBox.Unchecked += (_, _) => Write();
        ActivityBox.Checked += (_, _) => Write();
        ActivityBox.Unchecked += (_, _) => Write();
        DiscordIdBox.LostFocus += (_, _) => Write();
    }

    private void Write()
    {
        App.Settings.Prop.DiscordRichPresence = DiscordBox.IsChecked == true;
        App.Settings.Prop.DiscordClientId = DiscordIdBox.Text.Trim();
        App.Settings.Prop.ActivityTracking = ActivityBox.IsChecked == true;
        App.Save();
    }
}
