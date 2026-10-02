namespace Caelus.Core;

public static class AppInfo
{
    public const string Name = "X Bootstrapper";
    public const string Version = "2.4.1";
    public const string Website = "https://octane.wtf";
    public const string Discord = "https://discord.com/invite/octanee";
    // Public Discord application "X BootStrapper For Octane" used for Rich Presence (not a secret).
    public const string DiscordClientId = "1553825879996628992";
    // Rich Presence image. The application has no uploaded art asset, so Discord silently dropped the old
    // "octane" key; a public image URL is proxied by Discord (mp:external) and always shows.
    public const string DiscordLargeImage = "https://raw.githubusercontent.com/" + GitHubRepository + "/main/src/Caelus/Assets/x-mark.png";
    public const string DiscordLargeText = "X Bootstrapper for Octane";
    public const string ExeFileName = Name + ".exe";
    public const string LegacyFolderName = "Caelus";
    public const string PreviousFolderName = "X boostrapper";
    public const string LegacyExeFileName = "Caelus.exe";
    public const string AppUserModelId = "XBootstrapper.Launcher";
    public const string GitHubRepository = "nicolasishere1282-dotcom/x-bootstrapper";
    public const string SetupAppId = "{E8C4A91B-2D7F-4B3A-9E15-6F0C8D4A2B11}";

    public static string GitHubUrl => $"https://github.com/{GitHubRepository}";
    public static string ReleaseApiUrl => $"https://api.github.com/repos/{GitHubRepository}/releases/latest";
}
