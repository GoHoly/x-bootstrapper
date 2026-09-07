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
            if (!seen.Add(install.VersionDirectory))
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
            WriteFile(path, flags);
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
        yield return Path.Combine(install.VersionDirectory, "ClientSettings", "ClientAppSettings.json");

        if (!string.IsNullOrWhiteSpace(install.Root) &&
            !string.Equals(install.Root, install.VersionDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(install.Root, "ClientSettings", "ClientAppSettings.json");
            yield return Path.Combine(install.Root, "Versions", "ClientSettings", "ClientAppSettings.json");
        }

        var exeDir = Path.GetDirectoryName(install.PlayerExecutable);
        if (!string.IsNullOrWhiteSpace(exeDir) &&
            !string.Equals(exeDir, install.VersionDirectory, StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine(exeDir, "ClientSettings", "ClientAppSettings.json");

        yield return Path.Combine(Paths.LocalAppData, "Aisaka", "ClientSettings", "ClientAppSettings.json");
    }

    private static void WriteFile(string path, Dictionary<string, string> flags)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

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
    }

    private static string ToFlagString(string value)
    {
        if (bool.TryParse(value, out var boolean))
            return boolean ? "True" : "False";
        return value;
    }
}
