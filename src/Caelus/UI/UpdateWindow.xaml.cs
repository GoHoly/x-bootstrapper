using System.Windows;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI;

/// <summary>Download progress for an X Bootstrapper update, with Cancel.</summary>
public partial class UpdateWindow : Window
{
    private readonly CancellationTokenSource _cancel = new();
    private bool _finished;

    private UpdateWindow(string version)
    {
        InitializeComponent();
        SubtitleText.Text = $"{AppInfo.Version} → {version}";
        Closed += (_, _) =>
        {
            if (!_finished)
                _cancel.Cancel();
            App.RequestExitIfIdle();
        };
    }

    /// <summary>Static preview for the screenshot mode (no download).</summary>
    internal static UpdateWindow CreatePreview(string version)
    {
        var window = new UpdateWindow(version) { _finished = true };
        window.Report(new DownloadProgress(31_000_000, 73_000_000, "Downloading… 29.6 of 69.6 MB"));
        return window;
    }

    /// <summary>
    /// Downloads and verifies the release, then hands over to the installer (which closes this app).
    /// Returns false when cancelled or when it failed (the error stays visible in the window).
    /// </summary>
    internal static async Task<bool> RunAsync(AppRelease release, LaunchArgs args)
    {
        var window = new UpdateWindow(release.Version);
        window.Show();
        var progress = new Progress<DownloadProgress>(window.Report);
        try
        {
            var started = await AppUpdateService.InstallAsync(release, args, window._cancel.Token, progress);
            window._finished = true;
            if (!started)
                window.Fail($"{release.Version} has no Setup.exe to download.");
            return started;
        }
        catch (OperationCanceledException) when (window._cancel.IsCancellationRequested)
        {
            window._finished = true;
            Logger.Write("Update", "Update cancelled.");
            if (window.IsVisible)
                window.Close();
            return false;
        }
        catch (Exception ex)
        {
            window._finished = true;
            Logger.Error("Update", ex);
            window.Fail(ex.Message);
            return false;
        }
    }

    private void Report(DownloadProgress value)
    {
        StatusText.Text = value.Status;
        if (value.Total is long total && total > 0)
        {
            Progress.IsIndeterminate = false;
            Progress.Value = Math.Clamp(value.Read * 100.0 / total, 0, 100);
        }
        else
        {
            Progress.IsIndeterminate = true;
        }
    }

    private void Fail(string message)
    {
        if (!IsVisible)
            return;

        TitleText.Text = "Update failed";
        StatusText.Text = message;
        Progress.IsIndeterminate = false;
        Progress.Value = 0;
        CancelButton.Content = "Close";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_finished)
        {
            _cancel.Cancel();
            StatusText.Text = "Cancelling…";
            CancelButton.IsEnabled = false;
            return;
        }

        Close();
    }
}
