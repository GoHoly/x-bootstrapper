namespace Caelus.Core;

public sealed class ProtocolPayload
{
    public string Raw { get; init; } = "";
    public string LaunchMode { get; init; } = "play";
    public string? GameInfo { get; init; }
    public string? PlaceLauncherUrl { get; init; }
    public string? LaunchTime { get; init; }
    public string? BrowserTrackerId { get; init; }

    public bool CanStartPlayer =>
        !string.IsNullOrWhiteSpace(GameInfo) && !string.IsNullOrWhiteSpace(PlaceLauncherUrl);

    public static ProtocolPayload? TryParse(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return null;

        var colon = uri.IndexOf(':');
        if (colon < 0)
            return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in uri[(colon + 1)..].Split('+'))
        {
            var split = part.IndexOf(':');
            if (split <= 0)
                continue;

            fields[part[..split]] = part[(split + 1)..];
        }

        fields.TryGetValue("launchmode", out var mode);
        fields.TryGetValue("gameinfo", out var info);
        fields.TryGetValue("placelauncherurl", out var url);
        fields.TryGetValue("launchtime", out var time);
        fields.TryGetValue("browsertrackerid", out var tracker);

        if (!string.IsNullOrWhiteSpace(url))
        {
            try { url = Uri.UnescapeDataString(url); }
            catch (UriFormatException) { /* keep raw */ }
        }

        return new ProtocolPayload
        {
            Raw = uri,
            LaunchMode = string.IsNullOrWhiteSpace(mode) ? "play" : mode,
            GameInfo = info,
            PlaceLauncherUrl = url,
            LaunchTime = time,
            BrowserTrackerId = tracker
        };
    }
}
