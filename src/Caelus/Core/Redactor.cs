using System.Text.RegularExpressions;

namespace Caelus.Core;

/// <summary>
/// Scrubs anything that could be a secret from text that leaves the PC (the Copy logs bundle): website
/// join links (they carry a login ticket), name=value pairs with secret-sounding names, bearer tokens, JWTs,
/// command-line ticket arguments, and long opaque strings. Also hides the Windows user folder.
/// </summary>
public static class Redactor
{
    public const string Mark = "[redacted]";

    private static readonly Regex ProtocolLink = new(
        @"\b(octane-(?:player|studio)|roblox-(?:player|studio)|roblox|caelus-(?:player|studio|launcher))(:[^\s""'<>]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NamedSecret = new(
        @"(?<name>\b[\w.-]*(?:token|ticket|auth[\w]*|secret|password|passwd|pwd|cookie|apikey|api_key|signature|sig|session[\w]*|key|jwt|code)\b[""']?\s*[:=]\s*[""']?)(?<value>[^\s""'&,;}\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Bearer = new(@"(?<name>\b(?:bearer|basic)\s+)(?<value>[A-Za-z0-9._~+/=-]{8,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArgSecret = new(
        @"(?<name>(?:^|\s)--?(?:t|j|a|token|ticket|authticket|authenticationticket|joinscripturl|auth\w*)\s+)(?<value>[""']?[^\s""']+[""']?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Jwt = new(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*", RegexOptions.Compiled);

    // Runs of 32+ token characters. Replaced when they look random: hex, or mixed upper/lower case with digits.
    private static readonly Regex Opaque = new(@"(?<![A-Za-z0-9_+=/-])[A-Za-z0-9_+=/-]{32,}(?![A-Za-z0-9_+=/-])", RegexOptions.Compiled);

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        var result = ProtocolLink.Replace(text, match => match.Groups[1].Value + ":" + Mark);
        result = Jwt.Replace(result, Mark);
        result = Bearer.Replace(result, match => match.Groups["name"].Value + Mark);
        result = NamedSecret.Replace(result, match => match.Groups["name"].Value + Mark);
        result = ArgSecret.Replace(result, match => match.Groups["name"].Value + Mark);
        result = Opaque.Replace(result, match => LooksRandom(match.Value) ? Mark : match.Value);

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile) && profile.Length > 3)
            result = result.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static bool LooksRandom(string value)
    {
        foreach (var part in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 24)
                continue;
            var hex = part.All(Uri.IsHexDigit);
            var upper = part.Any(char.IsUpper);
            var lower = part.Any(char.IsLower);
            var digit = part.Any(char.IsDigit);
            if (hex && part.Length >= 32)
                return true;
            if (upper && lower && digit)
                return true;
        }

        return false;
    }
}
