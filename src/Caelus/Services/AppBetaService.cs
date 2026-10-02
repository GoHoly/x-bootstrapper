using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// Roblox Windows App Beta (the in-client home UI). Stock 2021 clients launch it with
/// <c>--app --rloc … --gloc …</c>, which is what the DevForum
/// <c>roblox-player:+launchmode:app+…+LaunchExp:InApp</c> URI becomes.
/// Octane's current OctanePlayer.exe no longer contains that launch path (verified by scanning
/// for <c>--app</c>); launching it the DevForum way opens a white screen. This helper does the
/// correct launch when a client still supports it, and fails clearly when it does not.
/// </summary>
public static class AppBetaService
{
    public const string LaunchArguments = "--app --rloc en_us --gloc en_us";

    public const string ProtocolUri =
        "octane-player:1+launchmode:app+robloxLocale:en_us+gameLocale:en_us+LaunchExp:InApp";

    private static readonly byte[] AppFlagAscii = "--app"u8.ToArray();

    private static readonly object CacheLock = new();
    private static string? _cachedPath;
    private static long _cachedLength;
    private static DateTime _cachedWriteUtc;
    private static bool _cachedSupports;

    public static bool IsAppModeProtocol(string? uri)
    {
        var payload = ProtocolPayload.TryParse(uri);
        return payload is not null &&
               payload.LaunchMode.Equals("app", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ClientSupportsAppBeta(string? playerExecutable)
    {
        if (string.IsNullOrWhiteSpace(playerExecutable) || !File.Exists(playerExecutable))
            return false;

        try
        {
            var info = new FileInfo(playerExecutable);
            lock (CacheLock)
            {
                if (_cachedPath is not null &&
                    string.Equals(_cachedPath, info.FullName, StringComparison.OrdinalIgnoreCase) &&
                    _cachedLength == info.Length &&
                    _cachedWriteUtc == info.LastWriteTimeUtc)
                    return _cachedSupports;
            }

            var supports = FileContains(playerExecutable, AppFlagAscii);
            lock (CacheLock)
            {
                _cachedPath = info.FullName;
                _cachedLength = info.Length;
                _cachedWriteUtc = info.LastWriteTimeUtc;
                _cachedSupports = supports;
            }

            Logger.Write("AppBeta", supports
                ? $"{Path.GetFileName(playerExecutable)} still has the --app App Beta launch path."
                : $"{Path.GetFileName(playerExecutable)} has no --app flag (Octane stripped App Beta from this build).");
            return supports;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Write("AppBeta", $"Could not scan player for App Beta support: {ex.Message}");
            return false;
        }
    }

    public static string MissingSupportMessage(string? playerExecutable)
    {
        var name = string.IsNullOrWhiteSpace(playerExecutable)
            ? "OctanePlayer.exe"
            : Path.GetFileName(playerExecutable);

        return
            "This Octane client cannot open the Windows App Beta.\n\n" +
            $"{name} no longer includes the --app launch path that the old Roblox / Aisaka App Beta used. " +
            "That is why the DevForum method opens a white screen on Octane — the launch executable Octane ships is missing App Beta, not X Bootstrapper.\n\n" +
            "If Octane restores App Beta, or you set Install → client folder to a 2021 build that still has it, turn this on again.";
    }

    private static bool FileContains(string path, byte[] needle)
    {
        const int chunk = 1024 * 1024;
        var buffer = new byte[chunk + needle.Length];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var carry = 0;
        while (true)
        {
            var read = stream.Read(buffer, carry, chunk);
            if (read <= 0)
                break;

            var length = carry + read;
            if (IndexOf(buffer, length, needle) >= 0)
                return true;

            if (read < chunk)
                break;

            // Keep the tail so a match that spans chunks is not missed.
            carry = needle.Length - 1;
            Buffer.BlockCopy(buffer, length - carry, buffer, 0, carry);
        }

        return false;
    }

    private static int IndexOf(byte[] haystack, int length, byte[] needle)
    {
        var limit = length - needle.Length;
        for (var i = 0; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }
}
