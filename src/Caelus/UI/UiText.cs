using Caelus.Models;

namespace Caelus.UI;

/// <summary>Copy shared by the settings pages and the first-run setup.</summary>
internal static class UiText
{
    public static readonly string[] DiscordChoices =
    {
        "X Bootstrapper status",
        "Octane's own status",
        "None"
    };

    public static string DiscordHint(DiscordStatusMode mode) => mode switch
    {
        DiscordStatusMode.Octane =>
            "X Bootstrapper stays off Discord, so the Octane client's own \"Playing Octane\" status shows while you play.",
        DiscordStatusMode.None =>
            "X Bootstrapper stays off Discord. Honest note: the Octane client still sets its own \"Playing Octane\" status and X Bootstrapper can't turn that off. To hide it too, turn off activity sharing in Discord (User Settings > Activity Privacy).",
        _ =>
            "Shows \"Playing on Octane\" with the place name (looked up from Octane) and time played. Discord shows one game status from this PC at a time, so X Bootstrapper connects as the launch starts and its status is the one shown instead of Octane's. Cleared when the game closes. Discord must be running."
    };

    public const string BackgroundHint =
        "Off (default): closing the window exits X Bootstrapper completely, nothing stays in Task Manager. While a game you started through X Bootstrapper runs, a small helper stays only as long as it's needed: until the game closes if X Bootstrapper sets your Discord status (or closes programs with the game), otherwise about 15 seconds to take the website links back. " +
        "On: closing hides X Bootstrapper to the notification area, where it keeps the website links pointed at X Bootstrapper. Exit it from the tray icon.";

    public const string LinksHealthy = "Play on octane.wtf opens X Bootstrapper.";
}
