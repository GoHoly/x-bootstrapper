using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI.Pages;

public partial class InstallPage : System.Windows.Controls.UserControl
{
    public InstallPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
        ClientBox.Text = s.ClientDirectory;
        WebsiteBox.Text = s.WebsiteUrl;
        SetupBox.Text = s.SetupBaseUrl;
        ManifestBox.Text = s.ManifestUrl;
        ChannelBox.Text = s.Channel;
        WebsiteProtocolBox.IsChecked = s.RegisterWebsiteProtocol;
        RobloxProtocolBox.IsChecked = s.RegisterRobloxProtocol;
    }

    private void BrowseClient_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Aisaka client folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            ClientBox.Text = dialog.SelectedPath;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        WriteBack();
        App.Save();
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        WriteBack();
        ProtocolService.Register(App.Settings.Prop);
        App.Save();
        System.Windows.MessageBox.Show("Protocol handlers were written to your user account.", AppInfo.Name);
    }

    private void WriteBack()
    {
        var s = App.Settings.Prop;
        s.ClientDirectory = ClientBox.Text.Trim();
        s.WebsiteUrl = WebsiteBox.Text.Trim();
        s.SetupBaseUrl = SetupBox.Text.Trim();
        s.ManifestUrl = ManifestBox.Text.Trim();
        s.Channel = ChannelBox.Text.Trim();
        s.RegisterWebsiteProtocol = WebsiteProtocolBox.IsChecked == true;
        s.RegisterRobloxProtocol = RobloxProtocolBox.IsChecked == true;
    }
}
