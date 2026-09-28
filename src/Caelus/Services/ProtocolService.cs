using Microsoft.Win32;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public static class ProtocolService
{
    // Confirmed from OctanePlayerLauncher.exe strings: these are the schemes octane.wtf uses for
    // its own Play/Studio links, and what the official launcher registers itself.
    private static readonly string[] OwnProtocols = { "octane-player", "octane-studio" };

    // Registered by Caelus / X Bootstrapper 2.0 and older. Never registered any more; removed at
    // startup when they still point at us, so Roblox and old Caelus links aren't hijacked.
    private static readonly string[] LegacyProtocols =
    {
        "caelus-launcher",
        "caelus-player",
        "caelus-studio",
        "roblox-player",
        "roblox-studio",
        "roblox"
    };

    public static void Register(Settings settings, AppState? state = null)
    {
        // OctanePlayerLauncher.exe (Octane's official launcher) registers octane-player:// and
        // octane-studio:// itself; whichever ran last owns the HKCU key. Before taking a scheme over
        // we remember the official command so joins can be forwarded to it and uninstall can put it back.
        state ??= CurrentState();
        var exe = HandlerExecutable;
        foreach (var link in CheckLinks().Where(link => !link.Healthy))
            Logger.Write("Protocol", link.Executable is null
                ? $"{link.Scheme}:// was not registered"
                : $"{link.Scheme}:// pointed to {link.Executable}; taking it back");
        foreach (var protocol in OwnProtocols)
            RegisterProtocol(protocol, exe, state);

        CleanupLegacy(state);
        Logger.Write("Protocol", $"Registered handlers for {exe}");
    }

    /// <summary>One website link (octane-player / octane-studio) and whether it opens this X Bootstrapper.</summary>
    public sealed record LinkStatus(string Scheme, string? Command, string? Executable, bool Healthy)
    {
        /// <summary>File name of the program the link opens now, or null when the link is missing.</summary>
        public string? OwnerName => string.IsNullOrWhiteSpace(Executable) ? null : Path.GetFileName(Executable);
    }

    /// <summary>The program X Bootstrapper registers for its links (the installed copy when there is one).</summary>
    public static string HandlerExecutable => File.Exists(Paths.Executable) ? Paths.Executable : Environment.ProcessPath!;

    /// <summary>
    /// Reads both website links. Octane's own launcher (OctanePlayerLauncher.exe) registers itself for them
    /// whenever it runs, after which Play on octane.wtf skips X Bootstrapper; this shows when that happened.
    /// A link is healthy only when it opens exactly this X Bootstrapper and that file exists.
    /// </summary>
    public static IReadOnlyList<LinkStatus> CheckLinks()
    {
        var expected = HandlerExecutable;
        var list = new List<LinkStatus>();
        foreach (var scheme in OwnProtocols)
        {
            var command = ReadCommand(scheme);
            var exe = TryGetExecutable(command, out var parsed) ? parsed : null;
            var healthy = exe is not null && File.Exists(exe) && SamePath(exe, expected);
            list.Add(new LinkStatus(scheme, command, exe, healthy));
        }

        return list;
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Removes caelus-* / roblox-* handlers that an older version pointed at us.</summary>
    public static void CleanupLegacy(AppState? state = null)
    {
        state ??= CurrentState();
        foreach (var protocol in LegacyProtocols)
            RemoveIfOurs(protocol, state);
    }

    /// <summary>
    /// Removes our handlers. For octane-player / octane-studio the official launcher's handler is put
    /// back (the saved command, or OctanePlayerLauncher.exe) so Play on octane.wtf keeps working.
    /// </summary>
    public static void Unregister(AppState? state = null)
    {
        state ??= CurrentState();
        foreach (var protocol in OwnProtocols.Concat(LegacyProtocols))
            RemoveIfOurs(protocol, state);
    }

    private static void RemoveIfOurs(string protocol, AppState? state)
    {
        try
        {
            var command = ReadCommand(protocol);
            if (!IsOwnHandler(command))
                return;

            if (TryRestoreOfficial(protocol, state))
                return;

            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            key?.DeleteSubKeyTree(protocol, throwOnMissingSubKey: false);
            Logger.Write("Protocol", $"Removed our {protocol}:// handler");
        }
        catch (Exception ex)
        {
            Logger.Error("Protocol", ex);
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
        string? command = null;
        if (state is not null && state.OfficialProtocolHandlers.TryGetValue(protocol, out var saved) &&
            TryGetExecutable(saved, out var savedExe) && File.Exists(savedExe) && !IsOwnExecutable(savedExe))
            command = saved;

        // Only Octane's own schemes fall back to OctanePlayerLauncher.exe.
        if (command is null && OwnProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
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
