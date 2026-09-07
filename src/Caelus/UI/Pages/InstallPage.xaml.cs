using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using Caelus.Core;
using Caelus.Services;
using Caelus.UI;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;

namespace Caelus.UI.Pages;

public partial class InstallPage : UserControl
{
    private AppRelease? _pending;
    private bool _busy;

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
        AppUpdatesBox.IsChecked = s.CheckForAppUpdates;
        CurrentVersionText.Text = $"This PC is on {AppInfo.Version}.";
        AppUpdatesBox.Checked += (_, _) =>
        {
            App.Settings.Prop.CheckForAppUpdates = true;
            App.Save();
        };
        AppUpdatesBox.Unchecked += (_, _) =>
        {
            App.Settings.Prop.CheckForAppUpdates = false;
            App.Save();
        };
        Loaded += async (_, _) => await LoadVersionsAsync();
    }

    private async void RefreshVersions_Click(object sender, RoutedEventArgs e) => await LoadVersionsAsync();

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_pending is not null)
            await InstallReleaseAsync(_pending);
    }

    private void SkipUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_pending is null)
            return;

        App.State.Prop.SkippedAppVersion = _pending.Version;
        App.Save();
        UpdateBanner.Visibility = Visibility.Collapsed;
        NotifyService.Show(AppInfo.Name, $"Staying on {AppInfo.Version}. You can install {_pending.Version} later from this list.");
    }

    private async Task LoadVersionsAsync()
    {
        VersionsStatus.Text = "Loading releases…";
        VersionList.Children.Clear();
        UpdateBanner.Visibility = Visibility.Collapsed;
        _pending = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var releases = await AppUpdateService.QueryReleasesAsync(cts.Token);
            VersionList.Children.Clear();

            if (releases.Count == 0)
            {
                VersionsStatus.Text = "No GitHub releases were found.";
                return;
            }

            VersionsStatus.Text = $"{releases.Count} release(s) from GitHub.";
            _pending = releases.FirstOrDefault(release =>
                !release.Prerelease && AppUpdateService.IsNewer(release.Version, AppInfo.Version));

            if (_pending is not null &&
                !string.Equals(App.State.Prop.SkippedAppVersion, _pending.Version, StringComparison.OrdinalIgnoreCase))
            {
                UpdateBanner.Visibility = Visibility.Visible;
                UpdateBannerTitle.Text = $"Version {_pending.Version} is out";
                var summary = AppUpdateService.Summarize(_pending.Notes);
                UpdateBannerBody.Text = string.IsNullOrWhiteSpace(summary)
                    ? "A newer X Bootstrapper is on GitHub. Update now, or pick Not now to stay on this build."
                    : summary;
                UpdateNowButton.IsEnabled = !string.IsNullOrWhiteSpace(_pending.SetupUrl);
            }

            foreach (var release in releases)
                VersionList.Children.Add(CreateVersionRow(release));
        }
        catch (Exception ex)
        {
            Logger.Error("Update", ex);
            VersionsStatus.Text = "Could not load releases. Check your internet connection.";
        }
    }

    private UIElement CreateVersionRow(AppRelease release)
    {
        var current = AppUpdateService.SameVersion(release.Version, AppInfo.Version);
        var newer = AppUpdateService.IsNewer(release.Version, AppInfo.Version);
        var date = release.Published?.ToLocalTime().ToString("d MMM yyyy") ?? "";
        var summary = AppUpdateService.Summarize(release.Notes);

        var row = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(4),
            Background = (Brush)FindResource("InputBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(current ? 1.5 : 1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        var title = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Text = string.IsNullOrWhiteSpace(release.Name) || release.Name == release.Version
                ? release.Version
                : $"{release.Version}  ·  {release.Name}",
            TextWrapping = TextWrapping.Wrap
        };
        var meta = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Text = string.Join("  ·  ", new[]
            {
                current ? "This PC" : newer ? "Newer" : "Previous",
                date,
                release.Prerelease ? "Pre-release" : null,
                string.IsNullOrWhiteSpace(summary) ? null : summary
            }.Where(part => !string.IsNullOrWhiteSpace(part)))
        };
        text.Children.Add(title);
        text.Children.Add(meta);
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var action = new Button
        {
            Content = current ? "This PC" : newer ? "Update" : "Install",
            Style = (Style)FindResource(current || string.IsNullOrWhiteSpace(release.SetupUrl) ? "GhostButton" : "AccentButton"),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = !current && !string.IsNullOrWhiteSpace(release.SetupUrl) && !_busy
        };
        action.Click += async (_, _) => await InstallReleaseAsync(release);
        Grid.SetColumn(action, 1);
        grid.Children.Add(action);

        row.Child = grid;
        return row;
    }

    private async Task InstallReleaseAsync(AppRelease release)
    {
        if (_busy)
            return;

        _busy = true;
        VersionsStatus.Text = $"Downloading {release.Version}…";
        SetVersionButtonsEnabled(false);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var started = await AppUpdateService.InstallAsync(release, App.Args, cts.Token);
            if (!started)
                VersionsStatus.Text = $"{release.Version} has no Setup.exe, or the download failed.";
        }
        catch (Exception ex)
        {
            Logger.Error("Update", ex);
            VersionsStatus.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            SetVersionButtonsEnabled(true);
        }
    }

    private void SetVersionButtonsEnabled(bool enabled)
    {
        foreach (var child in VersionList.Children.OfType<Border>())
        {
            if (child.Child is Grid grid)
            {
                foreach (var button in grid.Children.OfType<Button>())
                    button.IsEnabled = enabled && button.Content as string != "This PC";
            }
        }

        UpdateNowButton.IsEnabled = enabled && _pending is not null && !string.IsNullOrWhiteSpace(_pending.SetupUrl);
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

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        new UninstallWindow(removeAllContents: WipeBox.IsChecked == true).Show();
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
        s.CheckForAppUpdates = AppUpdatesBox.IsChecked == true;
    }
}
