namespace Caelus.Core;

public static class AppInfo
{
    public const string Name = "X Bootstrapper";
    public const string Version = "1.0.4";
    public const string Website = "https://www.aisaka.me";
    public const string Discord = "https://discord.gg/caelus";
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
