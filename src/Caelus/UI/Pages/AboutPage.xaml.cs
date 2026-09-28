using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI.Pages;

public partial class AboutPage : System.Windows.Controls.UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"{AppInfo.Name} {AppInfo.Version}";
        WhatsNewButton.Content = $"What's new in {AppInfo.Version}";
    }

    private string? _bundle;

    private async void CopyLogs_Click(object sender, RoutedEventArgs e) => await CopyLogsAsync();

    /// <summary>What the Copy logs button does; returns the bundle or null on failure.</summary>
    internal async Task<SupportBundleService.Result?> CopyLogsAsync()
    {
        CopyLogsButton.IsEnabled = false;
        CopyLogsStatus.Visibility = Visibility.Visible;
        CopyLogsStatus.Text = "Collecting logs…";
        try
        {
            var result = await Task.Run(() => SupportBundleService.Create());
            _bundle = result.Path;
            var shown = result.Path.StartsWith(Paths.Desktop, StringComparison.OrdinalIgnoreCase)
                ? "Desktop\\" + Path.GetFileName(result.Path)
                : result.Path;
            CopyLogsStatus.Text = $"Saved to {shown} ({result.LogCount} log(s) plus a summary, tokens removed).\nFull path: {result.Path}";
            ShowBundleButton.Visibility = Visibility.Visible;
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error("Support", ex);
            CopyLogsStatus.Text = $"Could not save the logs: {ex.Message}";
            return null;
        }
        finally
        {
            CopyLogsButton.IsEnabled = true;
        }
    }

    private void ShowBundle_Click(object sender, RoutedEventArgs e)
    {
        if (_bundle is null || !File.Exists(_bundle))
            return;
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{_bundle}\"", UseShellExecute = true });
    }

    private void WhatsNew_Click(object sender, RoutedEventArgs e) => WhatsNewWindow.ShowFor(Window.GetWindow(this), markShown: false);

    private void Setup_Click(object sender, RoutedEventArgs e) => SetupWindow.ShowFor(Window.GetWindow(this));

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
