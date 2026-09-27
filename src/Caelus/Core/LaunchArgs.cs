namespace Caelus.Core;

public enum LaunchMode
{
    Menu,
    Player,
    Studio,
    Uninstall,
    ImportMods
}

public sealed class LaunchArgs
{
    public LaunchMode Mode { get; set; } = LaunchMode.Menu;
    public string? ProtocolUri { get; private set; }
    public bool Quiet { get; private set; }
    public bool Menu { get; private set; }
    public bool SkipUpdate { get; set; }
    public string? ImportModsPath { get; set; }

    public static LaunchArgs Parse(string[] args)
    {
        var parsed = new LaunchArgs();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (IsProtocol(arg))
            {
                parsed.ProtocolUri = arg;
                // Decide from the scheme only; the payload itself can contain any text.
                parsed.Mode = IsStudioScheme(SchemeOf(arg)) ? LaunchMode.Studio : LaunchMode.Player;
                continue;
            }

            if (arg.Equals("--import-mods", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-import-mods", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    parsed.ImportModsPath = args[++i].Trim().Trim('"');
                    parsed.Mode = LaunchMode.ImportMods;
                }

                continue;
            }

            switch (arg.ToLowerInvariant())
            {
                case "-player":
                case "--player":
                    parsed.Mode = LaunchMode.Player;
                    break;
                case "-studio":
                case "--studio":
                    parsed.Mode = LaunchMode.Studio;
                    break;
                case "-menu":
                case "--menu":
                case "-settings":
                case "--settings":
                    parsed.Mode = LaunchMode.Menu;
                    parsed.Menu = true;
                    break;
                case "-uninstall":
                case "--uninstall":
                    parsed.Mode = LaunchMode.Uninstall;
                    break;
                case "-updated":
                case "--updated":
                    parsed.SkipUpdate = true;
                    break;
                case "-quiet":
                case "--quiet":
                    parsed.Quiet = true;
                    break;
            }
        }

        return parsed;
    }

    // Schemes the Octane website uses, plus legacy ones older installs registered.
    private static readonly Dictionary<string, string> SchemeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["octane-player"] = "octane-player",
        ["octane-studio"] = "octane-studio",
        ["caelus-launcher"] = "octane-player",
        ["caelus-player"] = "octane-player",
        ["caelus-studio"] = "octane-studio",
        ["roblox-player"] = "octane-player",
        ["roblox"] = "octane-player",
        ["roblox-studio"] = "octane-studio"
    };

    public static string? SchemeOf(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return null;
        var colon = arg.IndexOf(':');
        return colon > 0 ? arg[..colon] : null;
    }

    public static bool IsProtocol(string arg)
    {
        var scheme = SchemeOf(arg);
        return scheme is not null && SchemeMap.ContainsKey(scheme);
    }

    public static bool IsStudioScheme(string? scheme) =>
        scheme is not null && SchemeMap.TryGetValue(scheme, out var target) && target == "octane-studio";

    /// <summary>The Octane scheme this link maps to (octane-player or octane-studio).</summary>
    public string? TargetScheme
    {
        get
        {
            var scheme = SchemeOf(ProtocolUri);
            return scheme is not null && SchemeMap.TryGetValue(scheme, out var target) ? target : null;
        }
    }

    /// <summary>The link rewritten onto the Octane scheme the official launcher understands.</summary>
    public string ToLaunchUri()
    {
        if (string.IsNullOrWhiteSpace(ProtocolUri))
            return "";

        var scheme = SchemeOf(ProtocolUri);
        var target = TargetScheme;
        if (scheme is null || target is null || scheme.Equals(target, StringComparison.OrdinalIgnoreCase))
            return ProtocolUri;

        return target + ProtocolUri[scheme.Length..];
    }

    public string? ExtractPlaceId()
    {
        if (string.IsNullOrWhiteSpace(ProtocolUri))
            return null;

        var decoded = Uri.UnescapeDataString(ProtocolUri);
        foreach (var key in new[] { "placeId=", "placeid=", "PlaceID=" })
        {
            var index = decoded.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            var start = index + key.Length;
            var end = start;
            while (end < decoded.Length && char.IsDigit(decoded[end]))
                end++;

            if (end > start)
                return decoded[start..end];
        }

        return null;
    }
}
