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
    public bool UiSounds { get; set; } = true;

    public bool CheckForAppUpdates { get; set; } = true;
    public string GitHubRepository { get; set; } = "";
    public bool ConfirmLaunches { get; set; }
    public bool StayOpenAfterLaunch { get; set; }
    public bool RegisterWebsiteProtocol { get; set; } = true;

    public bool DiscordRichPresence { get; set; } = true;
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
}
