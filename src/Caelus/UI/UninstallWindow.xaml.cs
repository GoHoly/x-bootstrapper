using System.Windows;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI;

public partial class UninstallWindow : Window
{
    public UninstallWindow(bool removeAllContents = false)
    {
        InitializeComponent();
        Closed += (_, _) => App.RequestExitIfIdle();
        PlayfulMotion.Attach(this, SparkleLayer);
        DataBox.IsChecked = removeAllContents;
        if (removeAllContents)
            ClientBox.IsChecked = true;
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Removing X Bootstrapper...";
            var removeData = DataBox.IsChecked == true;
            InstallerService.Uninstall(
                App.Settings.Prop,
                removeClient: ClientBox.IsChecked == true || removeData,
                removeData: removeData);
            if (removeData)
                App.SuppressSave = true;
            else
                App.Save();
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("Uninstall", ex);
            StatusText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current.Windows.OfType<MenuWindow>().Any())
        {
            Close();
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }
}
