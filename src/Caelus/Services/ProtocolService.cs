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

    public static void Register(Settings settings)
    {
        // Note: OctanePlayerLauncher.exe (Octane's own official launcher) registers
        // octane-player:// / octane-studio:// itself. Whichever of the two ran most recently
        // "owns" the HKCU registration - this is the same interception pattern Bloxstrap uses
        // on roblox-player://, so it's expected, but call it out since it means installing/
        // running the official Octane launcher again will silently take the handlers back.
        var exe = File.Exists(Paths.Executable) ? Paths.Executable : Environment.ProcessPath!;
        foreach (var protocol in OwnProtocols)
            RegisterProtocol(protocol, $"URL:{protocol}", exe);

        if (settings.RegisterRobloxProtocol)
        {
            foreach (var protocol in RobloxProtocols)
                RegisterProtocol(protocol, $"URL:{protocol}", exe);
        }

        Logger.Write("Protocol", $"Registered handlers for {exe}");
    }

    public static void Unregister()
    {
        foreach (var protocol in OwnProtocols.Concat(RobloxProtocols))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
                if (key?.GetSubKeyNames().Contains(protocol, StringComparer.OrdinalIgnoreCase) == true)
                {
                    var command = Registry.GetValue($@"HKEY_CURRENT_USER\Software\Classes\{protocol}\shell\open\command", "", null) as string;
                    if (IsOwnHandler(command))
                        key.DeleteSubKeyTree(protocol, throwOnMissingSubKey: false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Protocol", ex);
            }
        }
    }

    private static bool IsOwnHandler(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        return command.Contains("Caelus", StringComparison.OrdinalIgnoreCase) ||
               command.Contains(AppInfo.Name, StringComparison.OrdinalIgnoreCase) ||
               command.Contains(AppInfo.ExeFileName, StringComparison.OrdinalIgnoreCase) ||
               command.Contains(Paths.Base, StringComparison.OrdinalIgnoreCase);
    }

    private static void RegisterProtocol(string scheme, string name, string exe)
    {
        var root = $@"HKEY_CURRENT_USER\Software\Classes\{scheme}";
        Registry.SetValue(root, "", name);
        Registry.SetValue(root, "URL Protocol", "");
        Registry.SetValue($@"{root}\DefaultIcon", "", $"\"{exe}\",0");
        Registry.SetValue($@"{root}\shell\open\command", "", $"\"{exe}\" \"%1\"");
    }
}
