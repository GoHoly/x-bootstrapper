using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

/// <summary>
/// "Copy logs": one zip on the Desktop with the newest logs, the version, and a settings summary, so it
/// can be attached to a bug report. Every text file goes through <see cref="Redactor"/> first.
/// </summary>
public static class SupportBundleService
{
    public const int MaxLogs = 10;

    public sealed record Result(string Path, int LogCount);

    /// <summary>Writes the bundle into <paramref name="folder"/> (the Desktop by default) and returns its path.</summary>
    public static Result Create(string? folder = null)
    {
        folder ??= Paths.Desktop;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"X Bootstrapper logs {DateTime.Now:yyyy-MM-dd HH-mm-ss}.zip");
        var logs = NewestLogs();

        var temp = path + ".tmp";
        using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
        {
            Add(zip, "summary.txt", Summary(logs.Count));
            foreach (var log in logs)
            {
                string text;
                try
                {
                    // The running process keeps its own log open for writing; share it.
                    using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    text = reader.ReadToEnd();
                }
                catch (Exception ex)
                {
                    text = $"(could not read this log: {ex.Message})";
                }

                Add(zip, "logs/" + Path.GetFileName(log), text);
            }
        }

        File.Move(temp, path, overwrite: true);
        Logger.Write("Support", $"Saved a log bundle ({logs.Count} log(s)) to {path}");
        return new Result(path, logs.Count);
    }

    private static List<string> NewestLogs()
    {
        var folders = new[] { Paths.Logs, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "X Bootstrapper Logs") };
        return folders.Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.log"))
            .Select(file => new FileInfo(file))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Take(MaxLogs)
            .Select(info => info.FullName)
            .ToList();
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(Redactor.Redact(text));
    }

    internal static string Summary(int logCount)
    {
        var s = App.Settings.Prop;
        var b = new StringBuilder();
        b.AppendLine($"{AppInfo.Name} {AppInfo.Version}");
        b.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (UTC{DateTimeOffset.Now:zzz})");
        b.AppendLine($"Windows: {Environment.OSVersion.VersionString} ({RuntimeInformation.OSArchitecture}), .NET {Environment.Version}");
        b.AppendLine($"Install folder: {Paths.Base}");
        b.AppendLine($"Installed: {InstallerService.IsInstalled(s)}");
        b.AppendLine($"Logs included: {logCount} newest");
        b.AppendLine("Anything that looked like a token, ticket, key or join link was replaced with " + Redactor.Mark + ".");
        b.AppendLine();

        b.AppendLine("[Octane client]");
        try
        {
            var install = ClientLocator.Find(s, App.State.Prop);
            b.AppendLine(install is null ? "Not found" : $"Folder: {install.VersionDirectory}");
            b.AppendLine($"Custom client folder: {(s.HasCustomClient ? "yes" : "no")}");
            b.AppendLine($"Octane running: {(ClientLocator.RunningIds(ClientLocator.PlayerProcessNames).Count > 0 ? "yes" : "no")}");
        }
        catch (Exception ex)
        {
            b.AppendLine($"Could not look for the client: {ex.Message}");
        }

        b.AppendLine();
        b.AppendLine("[Website links]");
        b.AppendLine($"Register links: {s.RegisterWebsiteProtocol}");
        try
        {
            foreach (var link in ProtocolService.CheckLinks())
                b.AppendLine($"{link.Scheme} link -> {(link.OwnerName ?? "(not registered)")}{(link.Healthy ? " (X Bootstrapper, OK)" : " (not X Bootstrapper)")}");
        }
        catch (Exception ex)
        {
            b.AppendLine($"Could not read the links: {ex.Message}");
        }

        b.AppendLine();
        b.AppendLine("[Settings]");
        b.AppendLine($"Theme: {s.Theme}, style: {s.UiStyle}, launch window: {s.BootstrapperStyle}, UI sounds: {s.UiSounds}");
        b.AppendLine($"Discord status: {s.EffectiveDiscordStatus}{(string.IsNullOrWhiteSpace(s.DiscordClientId) ? "" : " (custom application ID)")}, activity tracking: {s.ActivityTracking}");
        b.AppendLine($"Auto-update: {s.CheckForAppUpdates}, ask before launching: {s.ConfirmLaunches}, stay open: {s.StayOpenAfterLaunch}, tray icon: {s.ShowTrayIcon}");
        b.AppendLine($"Framerate limit: {s.FramerateLimit}, rendering: {s.RenderingMode}, texture quality: {s.TextureQuality}, post FX off: {s.DisablePostFx}, FPS counter: {s.ShowFpsCounter}, performance mode: {s.PerformanceMode}");
        b.AppendLine($"Custom FastFlags: {s.FastFlags.Count}{(s.FastFlags.Count == 0 ? "" : " (" + string.Join(", ", s.FastFlags.Keys.OrderBy(k => k).Take(40)) + ")")}");
        b.AppendLine($"Integrations: {s.Integrations.Count}{(s.Integrations.Count == 0 ? "" : " (" + string.Join(", ", s.Integrations.Select(i => i.Name)) + ")")}");
        try
        {
            b.AppendLine($"Mod files: {ModService.Count()}, sky: {SkyboxService.CurrentName()}");
        }
        catch (Exception ex)
        {
            b.AppendLine($"Mods: could not count ({ex.Message})");
        }

        return b.ToString();
    }
}
