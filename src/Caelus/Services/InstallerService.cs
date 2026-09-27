using System.Diagnostics;
using System.Text.Json;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public static class InstallerService
{
    public static bool IsInstalled(Settings settings)
    {
        if (File.Exists(Paths.Executable))
            return true;

        return settings.Installed && File.Exists(Paths.LegacyExecutable);
    }

    public static string PrepareInstallDirectory()
    {
        var saved = ReadSavedInstallLocation(Paths.DefaultBase)
            ?? ReadSavedInstallLocation(Paths.PreviousBase)
            ?? ReadSavedInstallLocation(Paths.LegacyBase);

        if (!string.IsNullOrWhiteSpace(saved) &&
            !Paths.IsLegacyDefault(saved) &&
            !string.Equals(Path.GetFullPath(saved), Path.GetFullPath(Paths.DefaultBase), StringComparison.OrdinalIgnoreCase))
        {
            return saved;
        }

        MigrateLegacyFolder(Paths.PreviousBase, Paths.DefaultBase);
        MigrateLegacyFolder(Paths.LegacyBase, Paths.DefaultBase);
        return Paths.DefaultBase;
    }

    /// <summary>
    /// Always install into a dedicated "X Bootstrapper" folder, so picking e.g. C:\Games never
    /// makes C:\Games itself the install folder (uninstall only ever deletes files it owns).
    /// </summary>
    public static string NormalizeInstallLocation(string chosen)
    {
        var raw = Environment.ExpandEnvironmentVariables((chosen ?? "").Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(raw))
            return Paths.DefaultBase;

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
        if (!Path.GetFileName(full).Equals(AppInfo.Name, StringComparison.OrdinalIgnoreCase))
            full = Path.Combine(full, AppInfo.Name);
        return full;
    }

    public static void Install(Settings settings, string location, bool createShortcuts, bool registerProtocols)
    {
        location = NormalizeInstallLocation(location);
        Paths.Initialize(location);
        Directory.CreateDirectory(Paths.Base);
        CopyPayload(Environment.ProcessPath!, Paths.Executable);
        EnsureInstallCasing();
        StripLegacyBinaries(Paths.Base);

        settings.Installed = true;
        settings.InstallLocation = Paths.Base;

        if (createShortcuts)
            ShortcutService.Publish(createMissing: true);

        WindowsAppRegistration.Register();

        if (registerProtocols)
            ProtocolService.Register(settings);

        Logger.Write("Installer", $"Installed to {Paths.Base}");
        Native.NotifyShell();
    }

    public static void Uninstall(Settings settings, bool removeClient, bool removeData = false)
    {
        try
        {
            ModService.RestoreAllOriginals();
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }

        ProtocolService.Unregister();
        ShortcutService.RemoveAll();
        WindowsAppRegistration.Unregister();

        if (removeClient || removeData)
        {
            TryDeleteDirectory(Path.Combine(Paths.Base, "Versions"));
            TryDeleteDirectory(Path.Combine(Paths.Base, "Downloads"));
        }

        settings.Installed = false;
        Logger.Write("Installer", removeData
            ? $"Uninstalled {AppInfo.Name} and deleted its data"
            : $"Uninstalled {AppInfo.Name}");
        Native.NotifyShell();

        if (removeData)
        {
            Logger.Close();
            foreach (var folder in new[] { Paths.Base, Paths.LegacyBase, Paths.PreviousBase })
                DeleteKnownData(folder);
        }

        var setupUninstaller = TryLaunchSetupUninstaller();
        if (!setupUninstaller)
        {
            DeletePayload(Paths.Base);
            StripLegacyBinaries(Paths.Base);
        }

        foreach (var legacy in new[] { Paths.LegacyBase, Paths.PreviousBase })
        {
            StripLegacyBinaries(legacy);
            TryDeleteEmptyLegacy(legacy);
        }

        ScheduleCleanup(deleteRunningExe: !setupUninstaller, waitSeconds: setupUninstaller ? 10 : 3);
    }

    /// <summary>Deletes only folders and files X Bootstrapper creates, never the folder's other contents.</summary>
    private static void DeleteKnownData(string folder)
    {
        if (!Directory.Exists(folder))
            return;

        foreach (var name in new[] { "Logs", "Modifications", "ModBackups", "ModProfiles", "Versions", "Downloads" })
            TryDeleteDirectory(Path.Combine(folder, name));

        foreach (var name in new[] { "Settings.json", "Settings.json.bak", "Settings.json.tmp", "State.json", "State.json.bak", "State.json.tmp", "desktop.ini" })
            TryDelete(Path.Combine(folder, name));
    }

    private static void DeletePayload(string folder)
    {
        var manifest = Path.Combine(folder, Path.GetFileName(Paths.PayloadManifest));
        if (File.Exists(manifest))
        {
            foreach (var line in File.ReadAllLines(manifest))
            {
                var relative = line.Trim();
                if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(".."))
                    continue;
                var path = Path.Combine(folder, relative);
                if (!IsRunningExecutable(path))
                    TryDelete(path);
            }

            TryDelete(manifest);
        }

        var stem = Path.GetFileNameWithoutExtension(AppInfo.ExeFileName);
        foreach (var suffix in new[] { ".dll", ".pdb", ".runtimeconfig.json", ".deps.json" })
            TryDelete(Path.Combine(folder, stem + suffix));
        if (!IsRunningExecutable(Path.Combine(folder, AppInfo.ExeFileName)))
            TryDelete(Path.Combine(folder, AppInfo.ExeFileName));
    }

    private static bool IsRunningExecutable(string path)
    {
        var running = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(running) &&
               string.Equals(Path.GetFullPath(path), Path.GetFullPath(running), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// After this process exits: delete the running exe (if it lives in the install folder) and remove
    /// the install folder only if it is empty. rmdir without /s never deletes anything else.
    /// </summary>
    private static void ScheduleCleanup(bool deleteRunningExe, int waitSeconds)
    {
        try
        {
            var delay = Math.Max(2, waitSeconds);
            var command = $"/c ping 127.0.0.1 -n {delay} > nul";
            var running = Environment.ProcessPath;
            if (deleteRunningExe && !string.IsNullOrWhiteSpace(running) &&
                string.Equals(Path.GetDirectoryName(Path.GetFullPath(running)), Path.GetFullPath(Paths.Base), StringComparison.OrdinalIgnoreCase))
                command += $" & del /f /q \"{running}\"";
            command += $" & rmdir \"{Paths.Base}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }

    private static bool TryLaunchSetupUninstaller()
    {
        try
        {
            if (!Directory.Exists(Paths.Base))
                return false;

            var setup = Directory.EnumerateFiles(Paths.Base, "unins*.exe")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (setup is null)
                return false;

            Process.Start(new ProcessStartInfo
            {
                FileName = setup,
                Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /FORCECLOSEAPPLICATIONS /fromapp=1",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
            return false;
        }
    }

    /// <summary>True when the running exe is a newer build than the installed one, or nothing is installed.</summary>
    public static bool ShouldRefreshPayload(string runningExe, string installedExe)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(runningExe), Path.GetFullPath(installedExe), StringComparison.OrdinalIgnoreCase))
                return false;
            if (!File.Exists(installedExe))
                return true;

            var running = ParseFileVersion(System.Diagnostics.FileVersionInfo.GetVersionInfo(runningExe).FileVersion);
            var installed = ParseFileVersion(System.Diagnostics.FileVersionInfo.GetVersionInfo(installedExe).FileVersion);
            return running > installed;
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
            return false;
        }
    }

    private static Version ParseFileVersion(string? value) =>
        Version.TryParse(value?.Split(' ')[0], out var version) ? version : new Version(0, 0);

    public static void CopyPayload(string sourceExe, string destinationExe)
    {
        try
        {
            var sourceDir = Path.GetDirectoryName(sourceExe)!;
            var destinationDir = Path.GetDirectoryName(destinationExe)!;
            Directory.CreateDirectory(destinationDir);

            if (string.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(destinationDir), StringComparison.OrdinalIgnoreCase))
            {
                EnsureCasing(destinationExe);
                return;
            }

            var selfContained = File.Exists(Path.Combine(sourceDir, "coreclr.dll")) ||
                                File.Exists(Path.Combine(sourceDir, "hostfxr.dll"));
            var copied = new List<string>();
            if (selfContained)
            {
                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(sourceDir, file);
                    if (IsInstallData(relative))
                        continue;

                    var dest = Path.Combine(destinationDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, overwrite: true);
                    copied.Add(relative);
                }
            }
            else
            {
                File.Copy(sourceExe, destinationExe, overwrite: true);
                copied.Add(Path.GetFileName(destinationExe));
                var stem = Path.GetFileNameWithoutExtension(sourceExe);
                foreach (var suffix in new[] { ".dll", ".runtimeconfig.json", ".deps.json" })
                {
                    var from = Path.Combine(sourceDir, stem + suffix);
                    if (File.Exists(from))
                    {
                        File.Copy(from, Path.Combine(destinationDir, Path.GetFileName(from)), overwrite: true);
                        copied.Add(Path.GetFileName(from));
                    }
                }
            }

            RecordPayload(destinationDir, copied);

            EnsureCasing(destinationExe);
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }

    private static void RecordPayload(string destinationDir, IEnumerable<string> relativeFiles)
    {
        try
        {
            var manifest = Path.Combine(destinationDir, Path.GetFileName(Paths.PayloadManifest));
            var existing = File.Exists(manifest) ? File.ReadAllLines(manifest) : Array.Empty<string>();
            var all = existing.Concat(relativeFiles)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                .ToList();
            File.WriteAllLines(manifest, all);
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }

    public static void EnsureInstallCasing()
    {
        EnsureCasing(Paths.Base);
        EnsureCasing(Paths.StartMenu);
        EnsureCasing(Paths.Executable);
        var dir = Path.GetDirectoryName(Paths.Executable)!;
        var stem = Path.GetFileNameWithoutExtension(AppInfo.ExeFileName);
        EnsureCasing(Path.Combine(dir, stem + ".dll"));
        EnsureCasing(Path.Combine(dir, stem + ".runtimeconfig.json"));
        EnsureCasing(Path.Combine(dir, stem + ".deps.json"));
        EnsureCasing(Path.Combine(Paths.StartMenu, AppInfo.Name + ".lnk"));
        EnsureCasing(Path.Combine(Paths.Desktop, AppInfo.Name + ".lnk"));
    }

    public static void StripLegacyBinaries(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var name in new[]
        {
            "Caelus.exe",
            "Caelus.dll",
            "Caelus.pdb",
            "Caelus.runtimeconfig.json",
            "Caelus.deps.json",
            "X boostrapper.exe",
            "X boostrapper.dll",
            "X boostrapper.pdb",
            "X boostrapper.runtimeconfig.json",
            "X boostrapper.deps.json"
        })
            TryDelete(Path.Combine(directory, name));
    }

    private static void MigrateLegacyFolder(string legacy, string current)
    {
        if (!Directory.Exists(legacy))
            return;

        if (string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase))
            return;

        if (!Directory.Exists(current))
        {
            try
            {
                Directory.Move(legacy, current);
                Logger.Write("Installer", $"Moved install folder to {current}");
                return;
            }
            catch (Exception ex)
            {
                Logger.Error("Installer", ex);
            }
        }

        CopyDirectory(legacy, current);
        StripLegacyBinaries(legacy);
        TryDeleteEmptyLegacy(legacy);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("Caelus.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("X boostrapper.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
                continue;

            var dest = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (!File.Exists(dest))
                File.Copy(file, dest, overwrite: false);
        }
    }

    private static void TryDeleteEmptyLegacy(string legacy)
    {
        try
        {
            if (!Directory.Exists(legacy))
                return;

            StripLegacyBinaries(legacy);
            if (!Directory.EnumerateFileSystemEntries(legacy).Any())
                Directory.Delete(legacy, false);
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }

    private static string? ReadSavedInstallLocation(string folder)
    {
        var path = Path.Combine(folder, "Settings.json");
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("InstallLocation", out var location))
                return location.GetString();
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }

        return null;
    }

    private static bool IsInstallData(string relative)
    {
        return relative.StartsWith("Logs", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("Modifications", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("Versions", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("Downloads", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("ModBackups", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("ModProfiles", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith(".xb-payload", StringComparison.OrdinalIgnoreCase) ||
               relative.Equals("Settings.json", StringComparison.OrdinalIgnoreCase) ||
               relative.Equals("State.json", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("unins", StringComparison.OrdinalIgnoreCase) ||
               relative.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureCasing(string desiredPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(desiredPath);
            var desiredName = Path.GetFileName(desiredPath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || string.IsNullOrWhiteSpace(desiredName))
                return;

            var match = Directory.EnumerateFileSystemEntries(dir, desiredName).FirstOrDefault();
            if (match is null || string.Equals(Path.GetFileName(match), desiredName, StringComparison.Ordinal))
                return;

            var temp = Path.Combine(dir, desiredName + ".casing-tmp");
            if (File.Exists(match))
            {
                File.Move(match, temp);
                File.Move(temp, Path.Combine(dir, desiredName));
            }
            else if (Directory.Exists(match))
            {
                Directory.Move(match, temp);
                Directory.Move(temp, Path.Combine(dir, desiredName));
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Logger.Error("Installer", ex);
        }
    }
}
