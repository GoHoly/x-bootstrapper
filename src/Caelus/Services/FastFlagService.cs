using System.Text.Json;
using System.Text.Json.Nodes;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public static class FastFlagService
{
    private static readonly string[] ManagedKeys =
    {
        "DFIntTaskSchedulerTargetFps",
        "FFlagDebugGraphicsPreferD3D11",
        "FFlagDebugGraphicsPreferD3D11FL10",
        "FFlagDebugGraphicsPreferD3D11FL11",
        "FFlagDebugGraphicsDisableDirect3D11",
        "FFlagDebugGraphicsDisableD3D11",
        "FFlagDebugGraphicsPreferVulkan",
        "FFlagDebugGraphicsPreferOpenGL",
        "FFlagDebugGraphicsPreferD3D9",
        "FFlagDisablePostFx",
        "DFFlagTextureQualityOverrideEnabled",
        "DFIntTextureQualityOverride",
        "FFlagDebugDisplayFPS",
        "DFFlagDebugDisplayFPS",
        "FFlagTaskSchedulerLimitTargetFpsTo2402",
        "FFlagTaskSchedulerLimitTargetFpsTo240",
        "FIntRenderLocalLightUpdatesMax",
        "FIntRenderLocalLightUpdatesMin",
        "FIntRenderLocalLightFadeInMs",
        "DFIntDebugFRMOptionalMSAALevelOverride",
        "FIntDebugForceMSAASamples",
        "FIntFRMMinGrassDistance",
        "FIntFRMMaxGrassDistance",
        "DFFlagDebugPauseVoxelizer"
    };

    public static Dictionary<string, string> Build(Settings settings)
    {
        settings.FastFlags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new Dictionary<string, string>(settings.FastFlags, StringComparer.OrdinalIgnoreCase);

        if (settings.FramerateLimit > 0)
            flags["DFIntTaskSchedulerTargetFps"] = settings.FramerateLimit.ToString();
        else if (settings.FramerateLimit < 0)
            flags["DFIntTaskSchedulerTargetFps"] = "9999";

        switch (settings.RenderingMode)
        {
            case RenderingMode.Direct3D11:
                SetRendererPreference(flags, d3d11: true);
                break;
            case RenderingMode.Vulkan:
                SetRendererPreference(flags, vulkan: true);
                break;
            case RenderingMode.OpenGL:
                SetRendererPreference(flags, openGl: true);
                break;
        }

        // Never disable D3D11. That is what forced OpenGL/Vulkan on 2021 clients and broke shadows.
        flags.Remove("FFlagDebugGraphicsDisableDirect3D11");
        flags.Remove("FFlagDebugGraphicsDisableD3D11");

        if (settings.DisablePostFx || settings.PerformanceMode)
            flags["FFlagDisablePostFx"] = "True";

        if (settings.TextureQuality >= 0)
        {
            flags["DFFlagTextureQualityOverrideEnabled"] = "True";
            flags["DFIntTextureQualityOverride"] = settings.TextureQuality.ToString();
        }

        if (settings.ShowFpsCounter || settings.PerformanceMode)
        {
            flags["FFlagDebugDisplayFPS"] = "True";
            flags["DFFlagDebugDisplayFPS"] = "True";
        }

        if (settings.PerformanceMode)
        {
            if (settings.FramerateLimit == 0)
                flags["DFIntTaskSchedulerTargetFps"] = "9999";

            flags["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False";
            flags["FFlagTaskSchedulerLimitTargetFpsTo240"] = "False";
            flags["FIntRenderLocalLightUpdatesMax"] = "1";
            flags["FIntRenderLocalLightUpdatesMin"] = "1";
            flags["FIntRenderLocalLightFadeInMs"] = "0";
            flags["DFIntDebugFRMOptionalMSAALevelOverride"] = "0";
            flags["FIntDebugForceMSAASamples"] = "0";
            flags["FIntFRMMinGrassDistance"] = "0";
            flags["FIntFRMMaxGrassDistance"] = "0";
            flags["DFFlagDebugPauseVoxelizer"] = "True";

            if (settings.RenderingMode == RenderingMode.Automatic)
                SetRendererPreference(flags, d3d11: true);
        }

        return flags;
    }

    public const int CurrentPresetRevision = 3;

    public static bool MigratePresets(Settings settings)
    {
        var changed = false;

        if (settings.FlagPresetRevision < 2 &&
            settings.RenderingMode is RenderingMode.OpenGL or RenderingMode.Vulkan)
        {
            settings.RenderingMode = RenderingMode.Direct3D11;
            changed = true;
        }

        if (settings.FlagPresetRevision < 3)
        {
            settings.ShowFpsCounter = true;
            settings.PerformanceMode = true;
            if (settings.FramerateLimit == 0)
                settings.FramerateLimit = -1;
            settings.DisablePostFx = true;
            changed = true;
        }

        if (settings.FlagPresetRevision < CurrentPresetRevision)
        {
            settings.FlagPresetRevision = CurrentPresetRevision;
            changed = true;
        }

        return changed;
    }

    public static void ApplyAll(Settings settings, AppState state, ClientInstall? primary, bool log = true)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var install in ClientLocator.FindAll(settings, state, primary))
        {
            if (string.IsNullOrWhiteSpace(install.VersionDirectory) || !seen.Add(install.VersionDirectory))
                continue;
            Apply(install, settings, log && seen.Count == 1);
        }
    }

    public static void Apply(ClientInstall install, Settings settings, bool log = true)
    {
        var flags = Build(settings);
        var written = 0;
        foreach (var path in SettingFiles(install))
        {
            if (WriteFile(path, flags))
                written++;
        }

        if (log)
            Logger.Write("FastFlags", $"Wrote {flags.Count} flag(s) to {written} ClientAppSettings.json file(s) under {install.VersionDirectory}");
    }

    private static void SetRendererPreference(Dictionary<string, string> flags, bool d3d11 = false, bool vulkan = false, bool openGl = false)
    {
        flags["FFlagDebugGraphicsPreferD3D11"] = d3d11 ? "True" : "False";
        flags["FFlagDebugGraphicsPreferD3D11FL10"] = "False";
        flags["FFlagDebugGraphicsPreferD3D11FL11"] = "False";
        flags["FFlagDebugGraphicsPreferVulkan"] = vulkan ? "True" : "False";
        flags["FFlagDebugGraphicsPreferOpenGL"] = openGl ? "True" : "False";
        flags["FFlagDebugGraphicsPreferD3D9"] = "False";
    }

    private static IEnumerable<string> SettingFiles(ClientInstall install)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in ClientFolders(install))
        {
            if (!IsSafeClientFolder(folder) || !seen.Add(Path.GetFullPath(folder)))
                continue;
            yield return Path.Combine(Path.GetFullPath(folder), "ClientSettings", "ClientAppSettings.json");
        }
    }

    private static IEnumerable<string> ClientFolders(ClientInstall install)
    {
        if (!string.IsNullOrWhiteSpace(install.VersionDirectory))
            yield return install.VersionDirectory;

        if (!string.IsNullOrWhiteSpace(install.Root))
        {
            yield return install.Root;
            yield return Path.Combine(install.Root, "Versions");
        }

        var exeDir = Path.GetDirectoryName(install.PlayerExecutable);
        if (!string.IsNullOrWhiteSpace(exeDir))
            yield return exeDir;

        yield return Path.Combine(Paths.LocalAppData, "Aisaka");
    }

    internal static bool IsSafeClientFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder))
            return false;

        try
        {
            var full = Path.GetFullPath(folder);
            var windows = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            return !full.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool WriteFile(string path, Dictionary<string, string> flags)
    {
        var directory = Path.GetDirectoryName(path);
        if (!IsSafeClientFolder(directory))
        {
            Logger.Write("FastFlags", $"Skipped unsafe settings path {path}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(directory!);

            JsonObject root;
            if (File.Exists(path))
            {
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
                }
                catch
                {
                    root = new JsonObject();
                }
            }
            else
            {
                root = new JsonObject();
            }

            foreach (var key in ManagedKeys)
            {
                if (!flags.ContainsKey(key))
                    root.Remove(key);
            }

            foreach (var (key, value) in flags)
                root[key] = JsonValue.Create(ToFlagString(value));

            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch (Exception ex)
        {
            Logger.Write("FastFlags", $"Could not write {path}: {ex.Message}");
            return false;
        }
    }

    private static string ToFlagString(string value)
    {
        if (bool.TryParse(value, out var boolean))
            return boolean ? "True" : "False";
        return value;
    }
}
