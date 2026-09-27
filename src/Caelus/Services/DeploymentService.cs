using System.Net.Http;
using System.IO.Compression;
using System.Text.Json;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public sealed class ClientVersionInfo
{
    public string VersionGuid { get; init; } = "";
    public string? PlayerUrl { get; init; }
    public string? StudioUrl { get; init; }
    public string? ManifestUrl { get; init; }
    public string? Sha256 { get; init; }
}

public static class DeploymentService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    static DeploymentService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Caelus/1.0 (+https://octane.wtf)");
    }

    public static async Task<ClientVersionInfo?> QueryAsync(Settings settings, Action<string>? status, CancellationToken token)
    {
        var candidates = new[]
        {
            settings.ManifestUrl,
            // Do NOT use https://octane.wtf/version.txt — it returns the login page HTML.
            // Optional launcher channel only; client install is detected via state\INSTALLED-*.
            $"{settings.SetupBaseUrl.TrimEnd('/')}/version.txt",
            $"{settings.WebsiteUrl.TrimEnd('/')}/caelus-manifest.json",
            $"{settings.SetupBaseUrl.TrimEnd('/')}/caelus.json",
            $"{settings.SetupBaseUrl.TrimEnd('/')}/version"
        }.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct();

        foreach (var url in candidates)
        {
            token.ThrowIfCancellationRequested();
            status?.Invoke($"Checking {new Uri(url).Host}...");

            try
            {
                using var response = await Http.GetAsync(url, token);
                if (!response.IsSuccessStatusCode)
                    continue;

                var media = response.Content.Headers.ContentType?.MediaType ?? "";
                var body = (await response.Content.ReadAsStringAsync(token)).Trim();
                if (string.IsNullOrWhiteSpace(body) || body.StartsWith("<") || body.Contains("<html", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (media.Contains("json", StringComparison.OrdinalIgnoreCase) || body.StartsWith('{'))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var guid = Read(root, "clientVersionUpload", "version", "versionGuid", "guid");
                    if (string.IsNullOrWhiteSpace(guid))
                        continue;

                    return new ClientVersionInfo
                    {
                        VersionGuid = guid,
                        PlayerUrl = Read(root, "playerUrl", "windowsPlayerUrl", "url"),
                        StudioUrl = Read(root, "studioUrl", "windowsStudioUrl"),
                        ManifestUrl = Read(root, "manifestUrl", "packageManifest"),
                        Sha256 = Read(root, "sha256", "hash")
                    };
                }

                if (body.StartsWith("version-", StringComparison.OrdinalIgnoreCase) || body.All(c => char.IsLetterOrDigit(c) || c is '-' or '.'))
                {
                    // This is the shape Octane's version.txt actually returns (a bare GUID).
                    // The rbxPkgManifest.txt guess below is carried over from the Roblox/prior revival
                    // deployment layout and is UNVERIFIED for Octane - if Octane's CDN doesn't
                    // expose per-package manifests this way, InstallFromManifestAsync will just
                    // fail its GET and InstallAsync() falls through to "no update available",
                    // leaving any existing/official Octane install alone rather than corrupting it.
                    var guid = body.Split('\n', '\r')[0].Trim();
                    return new ClientVersionInfo
                    {
                        VersionGuid = guid.StartsWith("version-", StringComparison.OrdinalIgnoreCase) ? guid : $"version-{guid}",
                        ManifestUrl = $"{settings.SetupBaseUrl.TrimEnd('/')}/{guid}-rbxPkgManifest.txt"
                    };
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Write("Deployment", $"Lookup failed for {url}: {ex.Message}");
            }
        }

        return null;
    }

    public static async Task<ClientInstall?> InstallAsync(
        Settings settings,
        ClientVersionInfo version,
        IProgress<double>? progress,
        Action<string>? status,
        CancellationToken token)
    {
        var destination = Path.Combine(Paths.Versions, version.VersionGuid);
        if (Directory.Exists(destination) && ClientLocator.FindInRoot(Paths.Base) is { } existing &&
            existing.VersionGuid.Equals(version.VersionGuid, StringComparison.OrdinalIgnoreCase))
        {
            status?.Invoke("Latest client is already installed.");
            return existing;
        }

        if (!string.IsNullOrWhiteSpace(version.PlayerUrl))
        {
            var zip = Path.Combine(Paths.Downloads, $"{version.VersionGuid}.zip");
            await DownloadAsync(version.PlayerUrl, zip, progress, status, token);
            status?.Invoke("Extracting Octane...");
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
            ZipFile.ExtractToDirectory(zip, destination);
            return ClientLocator.FindInRoot(Paths.Base);
        }

        if (!string.IsNullOrWhiteSpace(version.ManifestUrl))
        {
            var installed = await InstallFromManifestAsync(settings, version, destination, progress, status, token);
            if (installed)
                return ClientLocator.FindInRoot(Paths.Base);
        }

        return null;
    }

    private static async Task<bool> InstallFromManifestAsync(
        Settings settings,
        ClientVersionInfo version,
        string destination,
        IProgress<double>? progress,
        Action<string>? status,
        CancellationToken token)
    {
        try
        {
            status?.Invoke("Reading package manifest...");
            var manifest = await Http.GetStringAsync(version.ManifestUrl, token);
            if (manifest.StartsWith('<'))
                return false;

            var packages = ParseManifest(manifest);
            if (packages.Count == 0)
                return false;

            Directory.CreateDirectory(destination);
            var completed = 0;

            foreach (var package in packages)
            {
                token.ThrowIfCancellationRequested();
                status?.Invoke($"Downloading {package}...");
                var url = $"{settings.SetupBaseUrl.TrimEnd('/')}/{version.VersionGuid}-{package}";
                var zip = Path.Combine(Paths.Downloads, $"{version.VersionGuid}-{package}");
                try
                {
                    await DownloadAsync(url, zip, null, null, token);
                    ZipFile.ExtractToDirectory(zip, destination, overwriteFiles: true);
                }
                catch (Exception ex)
                {
                    Logger.Write("Deployment", $"Package {package} failed: {ex.Message}");
                }

                completed++;
                progress?.Report(completed / (double)packages.Count);
            }

            return ClientLocator.FindInRoot(Paths.Base) is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error("Deployment", ex);
            return false;
        }
    }

    private static List<string> ParseManifest(string manifest)
    {
        var packages = new List<string>();
        var lines = manifest.Replace("\r\n", "\n").Split('\n');
        for (var i = 1; i + 3 < lines.Length; i += 4)
        {
            var name = lines[i].Trim();
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                packages.Add(name);
        }

        return packages;
    }

    private static async Task DownloadAsync(string url, string destination, IProgress<double>? progress, Action<string>? status, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = File.Create(destination);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            readTotal += read;
            if (total > 0)
                progress?.Report(readTotal / (double)total);
            if (total > 0)
                status?.Invoke($"Downloading {readTotal / 1024 / 1024} / {total / 1024 / 1024} MB");
        }
    }

    private static string? Read(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }
}
