using Caelus.Core;
using System.Text.Json.Serialization;

namespace Caelus.Models;

public enum BootstrapperStyle
{
    Fluent,
    Classic
}

public enum AppTheme
{
    Dark,
    Light,
    Octane,
    Classic,
    Dusk,
    Xyxy,
    XyxyDark
}

/// <summary>Visual style applied on top of the theme colors. Classic is the pre-2.2 look.</summary>
public enum UiStyle
{
    Modern,
    Classic
}

/// <summary>Whose Discord status shows while Octane runs (Discord displays one local Rich Presence at a time).</summary>
public enum DiscordStatusMode
{
    // X Bootstrapper connects as the launch starts, so its status is the one Discord shows (2.2.1 behavior).
    XBootstrapper,
    // X Bootstrapper stays off Discord, so the Octane client's own "Playing Octane" shows.
    Octane,
    // X Bootstrapper stays off Discord. It cannot turn off the status the Octane client sets itself.
    None
}

public enum RenderingMode
{
    Automatic,
    Direct3D11,
    Vulkan,
    OpenGL
}

public sealed class Settings
{
    public bool Installed { get; set; }
    public string InstallLocation { get; set; } = "";
    public string ClientDirectory { get; set; } = "";

    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.Fluent;
    // Missing in older settings files, so existing users get the new Modern style too.
    public UiStyle UiStyle { get; set; } = UiStyle.Modern;
    public bool UiSounds { get; set; } = true;

    public bool CheckForAppUpdates { get; set; } = true;
    public string GitHubRepository { get; set; } = "";
    public bool ConfirmLaunches { get; set; }
    public bool StayOpenAfterLaunch { get; set; }
    public bool RegisterWebsiteProtocol { get; set; } = true;
    // When on, Launch Octane / -player starts the Windows App Beta home UI (--app) instead of a blank player.
    // Octane's current client often lacks --app; X Bootstrapper detects that and explains instead of a white screen.
    public bool LaunchAppBeta { get; set; }

    // Kept in step with DiscordStatus (true only for X Bootstrapper status) so older versions read it right.
    public bool DiscordRichPresence { get; set; } = true;
    // Missing in settings from 2.2.1 and older: derived from DiscordRichPresence (on = X Bootstrapper status,
    // off = Octane's own, which is what "off" did), so upgrading changes nothing.
    public DiscordStatusMode? DiscordStatus { get; set; }

    [JsonIgnore]
    public DiscordStatusMode EffectiveDiscordStatus =>
        DiscordStatus ?? (DiscordRichPresence ? DiscordStatusMode.XBootstrapper : DiscordStatusMode.Octane);

    public void SetDiscordStatus(DiscordStatusMode mode)
    {
        DiscordStatus = mode;
        DiscordRichPresence = mode == DiscordStatusMode.XBootstrapper;
    }
    // Optional override. Empty or whitespace means the built-in X Bootstrapper application (AppInfo.DiscordClientId).
    public string DiscordClientId { get; set; } = "";

    [JsonIgnore]
    public string EffectiveDiscordClientId =>
        string.IsNullOrWhiteSpace(DiscordClientId) ? AppInfo.DiscordClientId : DiscordClientId.Trim();
    public bool ActivityTracking { get; set; } = true;

    public int FramerateLimit { get; set; } = 0;
    public RenderingMode RenderingMode { get; set; } = RenderingMode.Automatic;
    public bool DisablePostFx { get; set; }
    public int TextureQuality { get; set; } = -1;
    public bool ShowFpsCounter { get; set; } = true;
    public bool PerformanceMode { get; set; } = true;
    public int FlagPresetRevision { get; set; }

    public Dictionary<string, string> FastFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Saved sets of custom flags (name -> flags), managed on the Fast Flags page.
    public Dictionary<string, Dictionary<string, string>> FastFlagProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Programs started with the game and optionally closed when it exits.
    public List<Integration> Integrations { get; set; } = new();
    public bool ShowTrayIcon { get; set; } = true;
    // Off: closing the menu ends the process (a launch helper stays only while a game needs it).
    // On: closing hides X Bootstrapper to the tray, where it keeps the website links pointed at itself.
    public bool KeepRunningInBackground { get; set; }

    // Set only when this profile was created by this run (no settings file yet): shows the first-run setup
    // once. Missing (false) for everyone who already had settings.
    public bool SetupPending { get; set; }
    // "Don't show again" on the What's new popup.
    public bool ShowWhatsNew { get; set; } = true;

    [JsonIgnore]
    public bool HasCustomClient => !string.IsNullOrWhiteSpace(ClientDirectory);
}

public sealed class Integration
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool AutoClose { get; set; } = true;
}

public sealed class AppState
{
    public string? PlayerVersionGuid { get; set; }
    public string? PlayerExecutable { get; set; }
    public string? StudioExecutable { get; set; }
    public DateTime? LastLaunched { get; set; }
    public string? LastNotifiedAppVersion { get; set; }
    public string? SkippedAppVersion { get; set; }
    public string? PendingUpdateNotice { get; set; }
    // Official handler commands (e.g. OctanePlayerLauncher.exe) seen before X Bootstrapper took a scheme over.
    public Dictionary<string, string> OfficialProtocolHandlers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // FastFlag keys X Bootstrapper wrote last time, so flags you delete are removed from the client too.
    public List<string> WrittenFlagKeys { get; set; } = new();
    // Version that last started (missing in 2.2.1 and older). A newer build than this one = an update happened.
    public string? LastRunVersion { get; set; }
    // What's new is due for this version (set when an update is detected) and was shown for that one.
    public string? WhatsNewPendingVersion { get; set; }
    public string? WhatsNewShownVersion { get; set; }
}
