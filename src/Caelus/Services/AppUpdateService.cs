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
    // GitHub API calls: short timeout. Downloads use their own client with no overall timeout and a
    // stall watchdog instead, so a slow connection can still finish a 70+ MB setup.
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    private const string UserAgent = "XBootstrapper/" + AppInfo.Version;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IReadOnlyList<AppRelease>? _cache;
    private static DateTimeOffset _cacheAt;

    static AppUpdateService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        DownloadClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public static async Task CheckInBackgroundAsync(LaunchArgs args)
    {
        try
        {
            if (args.SkipUpdate)
                return;

            // Only the GitHub query is time-boxed; the download has no overall timeout.
            AppRelease? latest;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                latest = await FindAutoUpdateAsync(cts.Token);

            if (latest is not null)
                await InstallAsync(latest, args, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.Error("Update", ex);
        }
    }

    private static async Task<AppRelease?> FindAutoUpdateAsync(CancellationToken token)
    {
        var latest = LatestStable(await QueryReleasesAsync(token));
        if (latest is null || !IsNewer(latest.Version, AppInfo.Version))
            return null;

        NotifyIfNew(latest);

        if (!App.Settings.Prop.CheckForAppUpdates)
            return null;
        if (string.Equals(App.State.Prop.SkippedAppVersion, latest.Version, StringComparison.OrdinalIgnoreCase))
            return null;

        return latest;
    }

    public static AppRelease? LatestStable(IEnumerable<AppRelease> releases) =>
        releases.Where(release => !release.Prerelease)
            .OrderByDescending(release => ParseOrZero(release.Version))
            .FirstOrDefault();

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

            var setup = Path.Combine(Path.GetTempPath(), $"X Bootstrapper Setup {release.Version}.exe");
            try
            {
                await DownloadAsync(release.SetupUrl, setup, token);
            }
            catch
            {
                TryDelete(setup);
                throw;
            }

            var notice = string.IsNullOrWhiteSpace(Summarize(release.Notes))
                ? $"Updated to {release.Version}."
                : $"Updated to {release.Version}. {Summarize(release.Notes)}";
            App.State.Prop.PendingUpdateNotice = notice;
            // Installing an older build on purpose: skip the newest release so auto-update
            // does not immediately undo the downgrade. Installing the newest clears the skip.
            var latest = _cache is null ? null : LatestStable(_cache);
            App.State.Prop.SkippedAppVersion = latest is not null && IsNewer(latest.Version, release.Version)
                ? latest.Version
                : null;
            App.Save();

            RestartThroughInstaller(setup, args);
            return true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task DownloadAsync(string url, string destination, CancellationToken token)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(token);
        stall.CancelAfter(StallTimeout);
        try
        {
            using var response = await DownloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            await using var input = await response.Content.ReadAsStreamAsync(stall.Token);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long read = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, stall.Token)) > 0)
            {
                stall.CancelAfter(StallTimeout);
                await output.WriteAsync(buffer.AsMemory(0, count), stall.Token);
                read += count;
            }

            if (total is long expected && read != expected)
                throw new IOException($"The download ended early ({read} of {expected} bytes).");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException($"The download stalled for {StallTimeout.TotalSeconds:0} seconds.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }

    private static Version ParseOrZero(string version) =>
        Version.TryParse(Normalize(version), out var parsed) ? parsed : new Version(0, 0);

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

    // "v2.0.1-hotfix" / "2.1.0+build" compare as 2.0.1 / 2.1.0.
    private static string Normalize(string version)
    {
        var trimmed = version.Trim().TrimStart('v', 'V');
        var cut = trimmed.IndexOfAny(new[] { '-', '+', ' ' });
        return cut > 0 ? trimmed[..cut] : trimmed;
    }
}

internal sealed record AppRelease(
    string Version,
    string Tag,
    string Name,
    string Notes,
    string? SetupUrl,
    DateTimeOffset? Published,
    bool Prerelease);
