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

    public static void Install(Settings settings, string location, bool createShortcuts, bool registerProtocols)
    {
        Paths.Initialize(location);
        Directory.CreateDirectory(Paths.Base);
        CopyPayload(Environment.ProcessPath!, Paths.Executable);
        EnsureInstallCasing();
        StripLegacyBinaries(Paths.Base);

        settings.Installed = true;
        settings.InstallLocation = Paths.Base;

        if (createShortcuts)
            ShortcutService.Publish();

        WindowsAppRegistration.Register();

        if (registerProtocols)
            ProtocolService.Register(settings);

        Logger.Write("Installer", $"Installed to {Paths.Base}");
        Native.NotifyShell();
    }

    public static void Uninstall(Settings settings, bool removeClient, bool removeData = false)
    {
        ProtocolService.Unregister();
        ShortcutService.RemoveAll();
        WindowsAppRegistration.Unregister();

        if (removeClient || removeData)
        {
            TryDeleteDirectory(Paths.Versions);
            TryDeleteDirectory(Paths.Downloads);
        }

        settings.Installed = false;
        Logger.Write("Installer", removeData
            ? $"Uninstalled {AppInfo.Name} and deleted all contents"
            : $"Uninstalled {AppInfo.Name}");
        Native.NotifyShell();

        if (removeData)
        {
            Logger.Close();
            TryDeleteDirectory(Paths.Modifications);
            TryDeleteDirectory(Paths.Logs);
            TryDelete(Paths.Settings);
            TryDelete(Paths.State);
            TryDelete(Path.Combine(Paths.Base, "desktop.ini"));
            TryDeleteDirectory(Paths.LegacyBase);
            TryDeleteDirectory(Paths.PreviousBase);
        }

        var setupUninstaller = TryLaunchSetupUninstaller();
        if (!setupUninstaller)
        {
            TryDelete(Paths.Executable);
            StripLegacyBinaries(Paths.Base);
        }

        if (removeData)
            ScheduleDeleteInstallFolder(waitSeconds: setupUninstaller ? 8 : 3);
    }

    private static void ScheduleDeleteInstallFolder(int waitSeconds)
    {
        try
        {
            var delay = Math.Max(2, waitSeconds);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c ping 127.0.0.1 -n {delay} > nul & rmdir /s /q \"{Paths.Base}\"",
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
                }
            }
            else
            {
                File.Copy(sourceExe, destinationExe, overwrite: true);
                var stem = Path.GetFileNameWithoutExtension(sourceExe);
                foreach (var suffix in new[] { ".dll", ".runtimeconfig.json", ".deps.json" })
                {
                    var from = Path.Combine(sourceDir, stem + suffix);
                    if (File.Exists(from))
                        File.Copy(from, Path.Combine(destinationDir, Path.GetFileName(from)), overwrite: true);
                }
            }

            EnsureCasing(destinationExe);
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
