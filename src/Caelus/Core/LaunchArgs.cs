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
                parsed.Mode = arg.Contains("studio", StringComparison.OrdinalIgnoreCase)
                    ? LaunchMode.Studio
                    : LaunchMode.Player;
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

    public static bool IsProtocol(string arg)
    {
        return arg.StartsWith("caelus-launcher:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("octane-player:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("caelus-player:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("octane-studio:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("caelus-studio:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("roblox-studio:", StringComparison.OrdinalIgnoreCase);
    }

    public string ToPlayerArgument()
    {
        if (string.IsNullOrWhiteSpace(ProtocolUri))
            return "";

        var uri = ProtocolUri;
        foreach (var scheme in new[] { "caelus-launcher:", "caelus-player:" })
        {
            if (uri.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return "octane-player:" + uri[scheme.Length..];
        }

        return uri;
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
