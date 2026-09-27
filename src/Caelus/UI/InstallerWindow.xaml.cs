using System.Windows;
using System.Windows.Forms;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI;

public partial class InstallerWindow : Window
{
    public InstallerWindow()
    {
        InitializeComponent();
        Closed += (_, _) => App.RequestExitIfIdle();
        PlayfulMotion.Attach(this, SparkleLayer);
        LocationBox.Text = InstallerService.NormalizeInstallLocation(Paths.Base);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose an X Bootstrapper install folder",
            SelectedPath = LocationBox.Text,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            LocationBox.Text = InstallerService.NormalizeInstallLocation(dialog.SelectedPath);
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Settings.Prop.RegisterRobloxProtocol = RobloxProtocolBox.IsChecked == true;
            InstallerService.Install(
                App.Settings.Prop,
                LocationBox.Text,
                ShortcutsBox.IsChecked == true,
                ProtocolBox.IsChecked == true);
            App.Save();
            StatusText.Text = "Installed. Opening X Bootstrapper...";
            new MenuWindow().Show();
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
            StatusText.Text = ex.Message;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
