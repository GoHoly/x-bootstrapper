using Microsoft.Win32;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public static class ProtocolService
{
    private static readonly string[] OwnProtocols =
    {
        "caelus-launcher",
        // Confirmed from OctanePlayerLauncher.exe strings: these are the schemes octane.wtf
        // uses for its own Play/Studio links, and what the official launcher registers itself.
        "octane-player",
        "octane-studio",
        "caelus-player",
        "caelus-studio"
    };
    private static readonly string[] RobloxProtocols = { "roblox-player", "roblox-studio", "roblox" };
    private static readonly string[] OctaneProtocols = { "octane-player", "octane-studio" };

    public static void Register(Settings settings, AppState? state = null)
    {
        // OctanePlayerLauncher.exe (Octane's official launcher) registers octane-player:// and
        // octane-studio:// itself; whichever ran last owns the HKCU key. Before taking a scheme over
        // we remember the official command so joins can be forwarded to it and uninstall can put it back.
        state ??= CurrentState();
        var exe = File.Exists(Paths.Executable) ? Paths.Executable : Environment.ProcessPath!;
        foreach (var protocol in OwnProtocols)
            RegisterProtocol(protocol, exe, state);

        if (settings.RegisterRobloxProtocol)
        {
            foreach (var protocol in RobloxProtocols)
                RegisterProtocol(protocol, exe, state);
        }

        Logger.Write("Protocol", $"Registered handlers for {exe}");
    }

    /// <summary>
    /// Removes our handlers. For octane-player / octane-studio the official launcher's handler is put
    /// back (the saved command, or OctanePlayerLauncher.exe) so Play on octane.wtf keeps working.
    /// </summary>
    public static void Unregister(AppState? state = null)
    {
        state ??= CurrentState();
        foreach (var protocol in OwnProtocols.Concat(RobloxProtocols).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var command = ReadCommand(protocol);
                if (!IsOwnHandler(command))
                    continue;

                if (TryRestoreOfficial(protocol, state))
                    continue;

                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
                key?.DeleteSubKeyTree(protocol, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                Logger.Error("Protocol", ex);
            }
        }
    }

    /// <summary>The official (non-X Bootstrapper) program that should receive a join link.</summary>
    public static (string Exe, string Arguments)? OfficialHandler(string scheme, string uri, ClientInstall? install, AppState state)
    {
        var candidates = new List<string>();
        var current = ReadCommand(scheme);
        if (!string.IsNullOrWhiteSpace(current) && !IsOwnHandler(current))
            candidates.Add(current);
        if (state.OfficialProtocolHandlers.TryGetValue(scheme, out var saved) && !string.IsNullOrWhiteSpace(saved))
            candidates.Add(saved);

        foreach (var command in candidates)
        {
            if (TryParseCommand(command, uri, out var exe, out var arguments) && File.Exists(exe) && !IsOwnExecutable(exe))
                return (exe, arguments);
        }

        var launcher = install?.LauncherExecutable;
        if (string.IsNullOrWhiteSpace(launcher) || !File.Exists(launcher))
            launcher = ClientLocator.FindExistingLauncher();
        if (!string.IsNullOrWhiteSpace(launcher) && File.Exists(launcher))
            return (launcher, Quote(uri));

        return null;
    }

    internal static bool IsOwnHandler(string? command)
    {
        return TryGetExecutable(command, out var exe) && IsOwnExecutable(exe);
    }

    private static bool IsOwnExecutable(string exe)
    {
        var name = Path.GetFileName(exe);
        if (name.Equals(AppInfo.ExeFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        string? directory;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(exe));
        }
        catch
        {
            return false;
        }

        if (directory is null)
            return false;

        var ownFolders = new[] { Paths.Base, Paths.DefaultBase, Paths.LegacyBase, Paths.PreviousBase };
        var inOwnFolder = ownFolders.Any(folder =>
            string.Equals(Path.TrimEndingDirectorySeparator(folder), directory, StringComparison.OrdinalIgnoreCase));
        return inOwnFolder &&
               (name.Equals(AppInfo.LegacyExeFileName, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(AppInfo.PreviousFolderName + ".exe", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryRestoreOfficial(string protocol, AppState? state)
    {
        if (!OctaneProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
            return false;

        string? command = null;
        if (state is not null && state.OfficialProtocolHandlers.TryGetValue(protocol, out var saved) &&
            TryGetExecutable(saved, out var savedExe) && File.Exists(savedExe) && !IsOwnExecutable(savedExe))
            command = saved;

        if (command is null)
        {
            var launcher = ClientLocator.FindExistingLauncher();
            if (!string.IsNullOrWhiteSpace(launcher))
                command = $"\"{launcher}\" \"%1\"";
        }

        if (command is null || !TryGetExecutable(command, out var exe))
            return false;

        WriteProtocol(protocol, exe, command);
        Logger.Write("Protocol", $"Gave {protocol}:// back to {exe}");
        return true;
    }

    private static void RegisterProtocol(string scheme, string exe, AppState? state)
    {
        try
        {
            var existing = ReadCommand(scheme);
            if (state is not null && !string.IsNullOrWhiteSpace(existing) && !IsOwnHandler(existing))
                state.OfficialProtocolHandlers[scheme] = existing;

            WriteProtocol(scheme, exe, $"\"{exe}\" \"%1\"");
        }
        catch (Exception ex)
        {
            Logger.Write("Protocol", $"Could not register {scheme}: {ex.Message}");
        }
    }

    private static void WriteProtocol(string scheme, string exe, string command)
    {
        var root = $@"HKEY_CURRENT_USER\Software\Classes\{scheme}";
        Registry.SetValue(root, "", $"URL:{scheme}");
        Registry.SetValue(root, "URL Protocol", "");
        Registry.SetValue($@"{root}\DefaultIcon", "", $"\"{exe}\",0");
        Registry.SetValue($@"{root}\shell\open\command", "", command);
    }

    private static string? ReadCommand(string scheme)
    {
        try
        {
            return Registry.GetValue($@"HKEY_CURRENT_USER\Software\Classes\{scheme}\shell\open\command", "", null) as string;
        }
        catch
        {
            return null;
        }
    }

    internal static bool TryGetExecutable(string? command, out string exe)
    {
        exe = "";
        if (string.IsNullOrWhiteSpace(command))
            return false;

        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end <= 1)
                return false;
            exe = command[1..end];
        }
        else
        {
            var lower = command.ToLowerInvariant();
            var exeEnd = lower.IndexOf(".exe", StringComparison.Ordinal);
            exe = exeEnd > 0 ? command[..(exeEnd + 4)] : command.Split(' ')[0];
        }

        return exe.Length > 0;
    }

    private static bool TryParseCommand(string command, string uri, out string exe, out string arguments)
    {
        arguments = "";
        if (!TryGetExecutable(command, out exe))
            return false;

        command = command.Trim();
        var rest = command.StartsWith('"')
            ? command[(exe.Length + 2)..].Trim()
            : command[exe.Length..].Trim();

        arguments = rest.Contains("%1")
            ? rest.Replace("%1", uri)
            : (rest + " " + Quote(uri)).Trim();
        return true;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "") + "\"";

    private static AppState? CurrentState()
    {
        try
        {
            return App.State?.Prop;
        }
        catch
        {
            return null;
        }
    }
}
