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
    Aisaka,
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
    public string WebsiteUrl { get; set; } = "https://www.aisaka.me";
    public string SetupBaseUrl { get; set; } = "https://setup.aisaka.me";
    public string ManifestUrl { get; set; } = "https://www.aisaka.me/caelus-manifest.json";
    public string ClientDirectory { get; set; } = "";
    public string Channel { get; set; } = "live";

    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.Fluent;
    public bool UiSounds { get; set; } = true;

    public bool CheckForClientUpdates { get; set; } = true;
    public bool CheckForAppUpdates { get; set; } = true;
    public string GitHubRepository { get; set; } = "";
    public bool ConfirmLaunches { get; set; }
    public bool StayOpenAfterLaunch { get; set; }
    public bool RegisterWebsiteProtocol { get; set; } = true;
    public bool RegisterRobloxProtocol { get; set; }

    public bool DiscordRichPresence { get; set; } = true;
    public string DiscordClientId { get; set; } = "";
    public bool ActivityTracking { get; set; } = true;

    public int FramerateLimit { get; set; } = 0;
    public RenderingMode RenderingMode { get; set; } = RenderingMode.Automatic;
    public bool DisablePostFx { get; set; }
    public int TextureQuality { get; set; } = -1;
    public bool ShowFpsCounter { get; set; } = true;
    public bool PerformanceMode { get; set; } = true;
    public int FlagPresetRevision { get; set; }

    public Dictionary<string, string> FastFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool HasCustomClient => !string.IsNullOrWhiteSpace(ClientDirectory);
}

public sealed class AppState
{
    public string? PlayerVersionGuid { get; set; }
    public string? StudioVersionGuid { get; set; }
    public string? PlayerExecutable { get; set; }
    public string? StudioExecutable { get; set; }
    public DateTime? LastLaunched { get; set; }
    public string? LastNotifiedAppVersion { get; set; }
    public string? SkippedAppVersion { get; set; }
    public string? PendingUpdateNotice { get; set; }
}
