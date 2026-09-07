using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

internal static class AppUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    static AppUpdateService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name.Replace(' ', '-')}/{AppInfo.Version}");
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static async Task<bool> TryApplyAsync(LaunchArgs args, CancellationToken token)
    {
        var settings = App.Settings.Prop;
        if (!settings.CheckForAppUpdates || args.SkipUpdate)
            return false;

        try
        {
            var latest = await QueryLatestAsync(settings, token);
            if (latest is null || !IsNewer(latest.Version, AppInfo.Version))
                return false;

            if (string.IsNullOrWhiteSpace(latest.SetupUrl))
            {
                Logger.Write("Update", $"Release {latest.Version} has no Setup.exe asset.");
                return false;
            }

            NotifyService.Show(AppInfo.Name, $"Updating to {latest.Version}…");
            Logger.Write("Update", $"Downloading {latest.SetupUrl}");

            var setup = Path.Combine(Path.GetTempPath(), "X Bootstrapper Setup.exe");
            await using (var remote = await Http.GetStreamAsync(latest.SetupUrl, token))
            await using (var file = File.Create(setup))
                await remote.CopyToAsync(file, token);

            RestartThroughInstaller(setup, args);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error("Update", ex);
            return false;
        }
    }

    public static async Task CheckInBackgroundAsync(LaunchArgs args)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await TryApplyAsync(args, cts.Token);
        }
        catch (Exception ex)
        {
            Logger.Error("Update", ex);
        }
    }

    private static async Task<ReleaseInfo?> QueryLatestAsync(Settings settings, CancellationToken token)
    {
        var repo = string.IsNullOrWhiteSpace(settings.GitHubRepository)
            ? AppInfo.GitHubRepository
            : settings.GitHubRepository.Trim();
        if (string.IsNullOrWhiteSpace(repo) || !repo.Contains('/'))
            return null;

        var url = $"https://api.github.com/repos/{repo}/releases/latest";
        using var response = await Http.GetAsync(url, token);
        if (!response.IsSuccessStatusCode)
        {
            Logger.Write("Update", $"GitHub returned {(int)response.StatusCode} for {url}");
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = tag.TrimStart('v', 'V');
        string? setupUrl = null;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                setupUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        return new ReleaseInfo(version, setupUrl);
    }

    private static bool IsNewer(string remote, string local)
    {
        if (!Version.TryParse(remote, out var remoteVersion) ||
            !Version.TryParse(local, out var localVersion))
            return false;
        return remoteVersion > localVersion;
    }

    private static void RestartThroughInstaller(string setupPath, LaunchArgs args)
    {
        var exe = File.Exists(Paths.Executable) ? Paths.Executable : Environment.ProcessPath!;
        var restartArgs = BuildRestartArgs(args);
        var command =
            $"/c start \"\" /wait \"{setupPath}\" /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /FORCECLOSEAPPLICATIONS " +
            $"& ping 127.0.0.1 -n 4 > nul " +
            $"& start \"\" \"{exe}\" {restartArgs}";

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = command,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        System.Windows.Application.Current.Shutdown();
    }

    private static string BuildRestartArgs(LaunchArgs args)
    {
        var parts = new List<string> { "-updated" };
        if (!string.IsNullOrWhiteSpace(args.ProtocolUri))
            parts.Add($"\"{args.ProtocolUri}\"");
        else if (args.Mode == LaunchMode.Player)
            parts.Add("-player");
        else if (args.Mode == LaunchMode.Studio)
            parts.Add("-studio");
        else
            parts.Add("-menu");
        return string.Join(' ', parts);
    }

    private sealed record ReleaseInfo(string Version, string? SetupUrl);
}
