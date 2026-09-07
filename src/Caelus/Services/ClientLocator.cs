using System.Diagnostics;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public sealed class ClientInstall
{
    public string Root { get; init; } = "";
    public string VersionDirectory { get; init; } = "";
    public string PlayerExecutable { get; init; } = "";
    public string? StudioExecutable { get; init; }
    public string? LauncherExecutable { get; init; }
    public string VersionGuid { get; init; } = "";
}

public static class ClientLocator
{
    private static readonly string[] PlayerNames =
    {
        "RobloxPlayerBeta.exe",
        "AisakaPlayerBeta.exe",
        "CaelusPlayerBeta.exe",
        "RobloxPlayer.exe",
        "AisakaPlayer.exe"
    };

    private static readonly string[] StudioNames =
    {
        "RobloxStudioBeta.exe",
        "AisakaStudioBeta.exe",
        "CaelusStudioBeta.exe",
        "RobloxStudio.exe"
    };

    private static readonly string[] LauncherNames =
    {
        "AisakaLauncher.exe",
        "CaelusPlayerLauncher.exe",
        "AisakaPlayerLauncher.exe",
        "RobloxPlayerLauncher.exe"
    };

    public static IEnumerable<string> CandidateRoots(Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ClientDirectory))
            yield return settings.ClientDirectory;

        yield return Paths.Base;
        yield return Path.Combine(Paths.LocalAppData, "Aisaka");
        yield return Path.Combine(Paths.LocalAppData, "Caelus");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Aisaka");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Caelus");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Aisaka");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Caelus");
    }

    public static ClientInstall? Find(Settings settings, AppState state)
    {
        ClientInstall? best = null;
        var bestWrite = DateTime.MinValue;

        foreach (var root in CandidateRoots(settings).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var install in FindPlayersInRoot(root))
            {
                DateTime write;
                try
                {
                    write = Directory.GetLastWriteTimeUtc(install.VersionDirectory);
                }
                catch
                {
                    write = DateTime.MinValue;
                }

                if (best is null || write > bestWrite)
                {
                    best = install;
                    bestWrite = write;
                }
            }
        }

        if (best is not null)
            return best;

        foreach (var root in CandidateRoots(settings).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var found = FindInRoot(root);
            if (found is not null && IsPlayerExe(found.PlayerExecutable))
                return found;
        }

        if (!string.IsNullOrWhiteSpace(state.PlayerExecutable) && File.Exists(state.PlayerExecutable))
        {
            var versionDir = Path.GetDirectoryName(state.PlayerExecutable)!;
            var root = Directory.GetParent(versionDir)?.FullName ?? versionDir;
            return new ClientInstall
            {
                Root = root,
                VersionDirectory = versionDir,
                PlayerExecutable = state.PlayerExecutable,
                StudioExecutable = Existing(state.StudioExecutable),
                LauncherExecutable = FindFile(root, LauncherNames) ?? FindExistingLauncher(),
                VersionGuid = Path.GetFileName(versionDir)
            };
        }

        foreach (var root in CandidateRoots(settings).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var found = FindInRoot(root);
            if (found is not null)
                return found;
        }

        return null;
    }

    public static IEnumerable<ClientInstall> FindAll(Settings settings, AppState state, ClientInstall? primary = null)
    {
        if (primary is not null)
            yield return primary;

        foreach (var root in CandidateRoots(settings).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var install in FindPlayersInRoot(root))
                yield return install;
        }
    }

    public static bool IsPlayerExe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var name = Path.GetFileName(path);
        return PlayerNames.Any(player => player.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static Process? FindRunningPlayer()
    {
        foreach (var name in new[] { "AisakaPlayer", "AisakaPlayerBeta", "RobloxPlayerBeta", "RobloxPlayer", "CaelusPlayerBeta" })
        {
            var process = Process.GetProcessesByName(name).FirstOrDefault();
            if (process is not null)
                return process;
        }

        return null;
    }

    private static IEnumerable<ClientInstall> FindPlayersInRoot(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        var versions = Path.Combine(root, "Versions");
        if (!Directory.Exists(versions))
            yield break;

        foreach (var versionDir in Directory.GetDirectories(versions).OrderByDescending(Directory.GetLastWriteTimeUtc))
        {
            var player = FindFile(versionDir, PlayerNames);
            if (player is null)
                continue;

            yield return new ClientInstall
            {
                Root = root,
                VersionDirectory = versionDir,
                PlayerExecutable = player,
                StudioExecutable = FindFile(versionDir, StudioNames) ?? FindFile(root, StudioNames),
                LauncherExecutable = FindFile(root, LauncherNames),
                VersionGuid = Path.GetFileName(versionDir)
            };
        }
    }

    public static ClientInstall? FromPlayerProcess(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var versionDir = Path.GetDirectoryName(path)!;
            var root = Directory.GetParent(versionDir)?.FullName ?? versionDir;
            return new ClientInstall
            {
                Root = root,
                VersionDirectory = versionDir,
                PlayerExecutable = path,
                LauncherExecutable = FindFile(root, LauncherNames) ?? FindExistingLauncher(),
                VersionGuid = Path.GetFileName(versionDir)
            };
        }
        catch (Exception ex)
        {
            Logger.Write("ClientLocator", $"Could not read player path: {ex.Message}");
            return null;
        }
    }

    public static ClientInstall? FindInRoot(string root)
    {
        if (!Directory.Exists(root))
            return null;

        var versions = Path.Combine(root, "Versions");
        if (Directory.Exists(versions))
        {
            foreach (var versionDir in Directory.GetDirectories(versions).OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                var player = FindFile(versionDir, PlayerNames);
                if (player is null)
                    continue;

                return new ClientInstall
                {
                    Root = root,
                    VersionDirectory = versionDir,
                    PlayerExecutable = player,
                    StudioExecutable = FindFile(versionDir, StudioNames) ?? FindFile(root, StudioNames),
                    LauncherExecutable = FindFile(root, LauncherNames),
                    VersionGuid = Path.GetFileName(versionDir)
                };
            }
        }

        var rootPlayer = FindFile(root, PlayerNames);
        if (rootPlayer is not null)
        {
            return new ClientInstall
            {
                Root = root,
                VersionDirectory = root,
                PlayerExecutable = rootPlayer,
                StudioExecutable = FindFile(root, StudioNames),
                LauncherExecutable = FindFile(root, LauncherNames),
                VersionGuid = Path.GetFileName(root)
            };
        }

        var launcher = FindFile(root, LauncherNames);
        if (launcher is not null)
        {
            return new ClientInstall
            {
                Root = root,
                VersionDirectory = root,
                PlayerExecutable = launcher,
                LauncherExecutable = launcher
            };
        }

        return null;
    }

    private static string? FindFile(string directory, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? Existing(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;

    private static string? FindExistingLauncher()
    {
        foreach (var root in new[]
                 {
                     Path.Combine(Paths.LocalAppData, "Aisaka"),
                     Path.Combine(Paths.LocalAppData, "Caelus"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Aisaka")
                 })
        {
            var launcher = FindFile(root, LauncherNames);
            if (launcher is not null)
                return launcher;
        }

        return null;
    }
}
