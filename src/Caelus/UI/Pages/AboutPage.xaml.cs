using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Caelus.Core;

namespace Caelus.UI.Pages;

public partial class AboutPage : System.Windows.Controls.UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"{AppInfo.Name} {AppInfo.Version}";
    }

    private void Website_Click(object sender, RoutedEventArgs e) => Open(AppInfo.Website);
    private void GitHub_Click(object sender, RoutedEventArgs e) => Open(AppInfo.GitHubUrl);
    private void Discord_Click(object sender, RoutedEventArgs e) => Open(AppInfo.Discord);

    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Paths.Logs);
        Open(Paths.Logs);
    }

    private static void Open(string target)
    {
        Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
    }
}
