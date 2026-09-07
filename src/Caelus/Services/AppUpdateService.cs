using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

internal static class AppUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IReadOnlyList<AppRelease>? _cache;
    private static DateTimeOffset _cacheAt;

    static AppUpdateService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name.Replace(' ', '-')}/{AppInfo.Version}");
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static async Task CheckInBackgroundAsync(LaunchArgs args)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await TryApplyAsync(args, cts.Token, installIfAllowed: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Update", ex);
        }
    }

    public static async Task<bool> TryApplyAsync(LaunchArgs args, CancellationToken token, bool installIfAllowed)
    {
        if (args.SkipUpdate)
            return false;

        try
        {
            var latest = (await QueryReleasesAsync(token)).FirstOrDefault(release => !release.Prerelease);
            if (latest is null || !IsNewer(latest.Version, AppInfo.Version))
                return false;

            NotifyIfNew(latest);

            var settings = App.Settings.Prop;
            if (!installIfAllowed || !settings.CheckForAppUpdates)
                return false;
            if (string.Equals(App.State.Prop.SkippedAppVersion, latest.Version, StringComparison.OrdinalIgnoreCase))
                return false;

            return await InstallAsync(latest, args, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error("Update", ex);
            return false;
        }
    }

    public static async Task<IReadOnlyList<AppRelease>> QueryReleasesAsync(CancellationToken token)
    {
        if (_cache is not null && DateTimeOffset.UtcNow - _cacheAt < TimeSpan.FromMinutes(2))
            return _cache;

        var settings = App.Settings.Prop;
        var repo = string.IsNullOrWhiteSpace(settings.GitHubRepository)
            ? AppInfo.GitHubRepository
            : settings.GitHubRepository.Trim();
        if (string.IsNullOrWhiteSpace(repo) || !repo.Contains('/'))
            return _cache ?? Array.Empty<AppRelease>();

        var url = $"https://api.github.com/repos/{repo}/releases?per_page=20";
        using var response = await Http.GetAsync(url, token);
        if (!response.IsSuccessStatusCode)
        {
            Logger.Write("Update", $"GitHub returned {(int)response.StatusCode} for {url}");
            return _cache ?? Array.Empty<AppRelease>();
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var list = new List<AppRelease>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                continue;

            var tag = item.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            var version = tag.TrimStart('v', 'V');
            if (string.IsNullOrWhiteSpace(version))
                continue;

            DateTimeOffset? published = null;
            if (item.TryGetProperty("published_at", out var publishedEl) &&
                publishedEl.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(publishedEl.GetString(), out var parsed))
                published = parsed;

            list.Add(new AppRelease(
                version,
                tag,
                item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? version : version,
                item.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "",
                FindSetupUrl(item),
                published,
                item.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True));
        }

        _cache = list;
        _cacheAt = DateTimeOffset.UtcNow;
        return list;
    }

    public static async Task<bool> InstallAsync(AppRelease release, LaunchArgs args, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(release.SetupUrl))
        {
            Logger.Write("Update", $"Release {release.Version} has no Setup.exe asset.");
            return false;
        }

        await Gate.WaitAsync(token);
        try
        {
            NotifyService.Show(AppInfo.Name, $"Updating to {release.Version}… {Summarize(release.Notes)}".Trim());
            Logger.Write("Update", $"Downloading {release.SetupUrl}");

            var setup = Path.Combine(Path.GetTempPath(), "X Bootstrapper Setup.exe");
            await using (var remote = await Http.GetStreamAsync(release.SetupUrl, token))
            await using (var file = File.Create(setup))
                await remote.CopyToAsync(file, token);

            var notice = string.IsNullOrWhiteSpace(Summarize(release.Notes))
                ? $"Updated to {release.Version}."
                : $"Updated to {release.Version}. {Summarize(release.Notes)}";
            App.State.Prop.PendingUpdateNotice = notice;
            App.State.Prop.SkippedAppVersion = null;
            App.Save();

            RestartThroughInstaller(setup, args);
            return true;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static bool IsNewer(string remote, string local)
    {
        if (!Version.TryParse(Normalize(remote), out var remoteVersion) ||
            !Version.TryParse(Normalize(local), out var localVersion))
            return false;
        return remoteVersion > localVersion;
    }

    public static bool SameVersion(string left, string right)
    {
        if (Version.TryParse(Normalize(left), out var a) && Version.TryParse(Normalize(right), out var b))
            return a == b;
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string Summarize(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "";

        foreach (var raw in notes.Replace("\r", "").Split('\n'))
        {
            var line = Regex.Replace(raw.Trim(), @"^#+\s*", "");
            line = Regex.Replace(line, @"^\*\*\s*|\s*\*\*$", "");
            line = line.TrimStart('-', '*', ' ').Trim();
            if (line.Length < 8)
                continue;
            if (line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                continue;
            return line.Length > 140 ? line[..137] + "..." : line;
        }

        return "";
    }

    private static void NotifyIfNew(AppRelease latest)
    {
        var state = App.State.Prop;
        if (string.Equals(state.LastNotifiedAppVersion, latest.Version, StringComparison.OrdinalIgnoreCase))
            return;

        var summary = Summarize(latest.Notes);
        var body = string.IsNullOrWhiteSpace(summary)
            ? $"{latest.Version} is out. Open Install to update or stay on {AppInfo.Version}."
            : $"{latest.Version} is out. {summary}";
        NotifyService.Show(AppInfo.Name, body);
        state.LastNotifiedAppVersion = latest.Version;
        App.Save();
    }

    private static string? FindSetupUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            return asset.GetProperty("browser_download_url").GetString();
        }

        return null;
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

    private static string Normalize(string version) => version.Trim().TrimStart('v', 'V');
}

internal sealed record AppRelease(
    string Version,
    string Tag,
    string Name,
    string Notes,
    string? SetupUrl,
    DateTimeOffset? Published,
    bool Prerelease);
