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
    // Octane player is OctanePlayer.exe under clients\<year>\. RobloxPlayerBeta is not used for player.
    private static readonly string[] PlayerNames =
    {
        "OctanePlayer.exe",
        "OctanePlayerBeta.exe",
        "CaelusPlayerBeta.exe",
        "RobloxPlayer.exe"
    };

    private static readonly string[] StudioNames =
    {
        // Confirmed: Studio\2021\RobloxStudioBeta.exe
        "RobloxStudioBeta.exe",
        "OctaneStudioBeta.exe",
        "CaelusStudioBeta.exe",
        "RobloxStudio.exe"
    };

    private static readonly string[] LauncherNames =
    {
        "OctanePlayerLauncher.exe",
        "CaelusPlayerLauncher.exe",
        "RobloxPlayerLauncher.exe"
    };

    public static IEnumerable<string> CandidateRoots(Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ClientDirectory) && FastFlagService.IsSafeClientFolder(settings.ClientDirectory))
            yield return settings.ClientDirectory;

        yield return Paths.Base;
        yield return Path.Combine(Paths.LocalAppData, "Octane");
        yield return Path.Combine(Paths.LocalAppData, "Caelus");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Octane");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Caelus");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Octane");
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
        foreach (var name in new[] { "OctanePlayer", "OctanePlayerBeta", "RobloxPlayer", "CaelusPlayerBeta" })
        {
            var process = Process.GetProcessesByName(name).FirstOrDefault();
            if (process is not null)
                return process;
        }

        return null;
    }

    private static IEnumerable<ClientInstall> FindPlayersInRoot(string root)
    {
        if (!Directory.Exists(root) || !FastFlagService.IsSafeClientFolder(root))
            yield break;

        // Octane layout: %LOCALAPPDATA%\Octane\clients\<year>\OctanePlayer.exe
        // and Studio\<year>\RobloxStudioBeta.exe. Year folders (e.g. 2021), not version hashes.
        // state\INSTALLED-<year> marks which years are present.
        foreach (var yearDir in EnumerateOctaneYearDirs(root))
        {
            var player = FindFile(yearDir, PlayerNames);
            if (player is null)
                continue;

            var year = Path.GetFileName(yearDir);
            var studio = FindStudioForYear(root, year);
            yield return new ClientInstall
            {
                Root = root,
                VersionDirectory = yearDir,
                PlayerExecutable = player,
                StudioExecutable = studio,
                LauncherExecutable = FindFile(root, LauncherNames) ?? FindExistingLauncher(),
                VersionGuid = year
            };
        }

        // Legacy Roblox/prior revival Versions\<hash> layout (kept as fallback).
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

    private static IEnumerable<string> EnumerateOctaneYearDirs(string root)
    {
        var clients = Path.Combine(root, "clients");
        if (!Directory.Exists(clients))
            yield break;

        var preferred = new List<string>();
        var stateDir = Path.Combine(root, "state");
        if (Directory.Exists(stateDir))
        {
            foreach (var marker in Directory.GetFiles(stateDir, "INSTALLED-*"))
            {
                var name = Path.GetFileName(marker);
                if (!name.StartsWith("INSTALLED-", StringComparison.OrdinalIgnoreCase))
                    continue;
                var year = name["INSTALLED-".Length..];
                if (string.IsNullOrWhiteSpace(year))
                    continue;
                var yearDir = Path.Combine(clients, year);
                if (Directory.Exists(yearDir))
                    preferred.Add(yearDir);
            }
        }

        var all = Directory.GetDirectories(clients)
            .Where(d => Directory.GetFiles(d, "OctanePlayer.exe").Length > 0
                        || Directory.GetFiles(d, "OctanePlayerBeta.exe").Length > 0
                        || Directory.GetFiles(d, "*.exe").Any(f =>
                            PlayerNames.Any(n => n.Equals(Path.GetFileName(f), StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .ToList();

        foreach (var dir in preferred.Concat(all).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return dir;
    }

    private static string? FindStudioForYear(string root, string year)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(root, "Studio", year),
                     Path.Combine(root, "clients", "Studio", year),
                     Path.Combine(root, "studio", year)
                 })
        {
            var studio = FindFile(candidate, StudioNames);
            if (studio is not null)
                return studio;
        }

        return FindFile(root, StudioNames);
    }

    public static ClientInstall? FromPlayerProcess(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var versionDir = Path.GetDirectoryName(path)!;
            if (!FastFlagService.IsSafeClientFolder(versionDir))
                return null;

            // clients\2021 -> root is Octane (two levels up from year dir when under clients)
            var parent = Directory.GetParent(versionDir);
            var root = parent?.Name.Equals("clients", StringComparison.OrdinalIgnoreCase) == true
                ? parent.Parent?.FullName ?? parent.FullName
                : parent?.FullName ?? versionDir;

            return new ClientInstall
            {
                Root = root,
                VersionDirectory = versionDir,
                PlayerExecutable = path,
                StudioExecutable = FindStudioForYear(root, Path.GetFileName(versionDir)),
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
        if (!Directory.Exists(root) || !FastFlagService.IsSafeClientFolder(root))
            return null;

        foreach (var install in FindPlayersInRoot(root))
            return install;

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
        if (!Directory.Exists(directory))
            return null;

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
                     Path.Combine(Paths.LocalAppData, "Octane"),
                     Path.Combine(Paths.LocalAppData, "Octane", "clients"),
                     Path.Combine(Paths.LocalAppData, "Caelus"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Octane")
                 })
        {
            var launcher = FindFile(root, LauncherNames);
            if (launcher is not null)
                return launcher;
        }

        return null;
    }
}
