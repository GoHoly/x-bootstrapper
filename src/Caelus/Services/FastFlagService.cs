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
        "FFlagDebugGraphicsDisableDirect3D11",
        "FFlagDebugGraphicsDisableD3D11",
        "FFlagDebugGraphicsPreferVulkan",
        "FFlagDebugGraphicsPreferOpenGL",
        "FFlagDisablePostFx",
        "DFFlagTextureQualityOverrideEnabled",
        "DFIntTextureQualityOverride"
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
                flags["FFlagDebugGraphicsPreferD3D11"] = "True";
                flags["FFlagDebugGraphicsPreferD3D11FL10"] = "True";
                flags["FFlagDebugGraphicsDisableDirect3D11"] = "False";
                flags["FFlagDebugGraphicsDisableD3D11"] = "False";
                flags["FFlagDebugGraphicsPreferVulkan"] = "False";
                flags["FFlagDebugGraphicsPreferOpenGL"] = "False";
                break;
            case RenderingMode.Vulkan:
                flags["FFlagDebugGraphicsPreferVulkan"] = "True";
                flags["FFlagDebugGraphicsPreferD3D11"] = "False";
                flags["FFlagDebugGraphicsPreferD3D11FL10"] = "False";
                flags["FFlagDebugGraphicsDisableDirect3D11"] = "True";
                flags["FFlagDebugGraphicsDisableD3D11"] = "True";
                flags["FFlagDebugGraphicsPreferOpenGL"] = "False";
                break;
            case RenderingMode.OpenGL:
                flags["FFlagDebugGraphicsPreferOpenGL"] = "True";
                flags["FFlagDebugGraphicsPreferD3D11"] = "False";
                flags["FFlagDebugGraphicsPreferD3D11FL10"] = "False";
                flags["FFlagDebugGraphicsDisableDirect3D11"] = "True";
                flags["FFlagDebugGraphicsDisableD3D11"] = "True";
                flags["FFlagDebugGraphicsPreferVulkan"] = "False";
                break;
        }

        if (settings.DisablePostFx)
            flags["FFlagDisablePostFx"] = "True";

        if (settings.TextureQuality >= 0)
        {
            flags["DFFlagTextureQualityOverrideEnabled"] = "True";
            flags["DFIntTextureQualityOverride"] = settings.TextureQuality.ToString();
        }

        return flags;
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

    private static IEnumerable<string> SettingFiles(ClientInstall install)
    {
        yield return Path.Combine(install.VersionDirectory, "ClientSettings", "ClientAppSettings.json");

        if (!string.IsNullOrWhiteSpace(install.Root) &&
            !string.Equals(install.Root, install.VersionDirectory, StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine(install.Root, "ClientSettings", "ClientAppSettings.json");

        var exeDir = Path.GetDirectoryName(install.PlayerExecutable);
        if (!string.IsNullOrWhiteSpace(exeDir) &&
            !string.Equals(exeDir, install.VersionDirectory, StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine(exeDir, "ClientSettings", "ClientAppSettings.json");
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

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ToFlagString(string value)
    {
        if (bool.TryParse(value, out var boolean))
            return boolean ? "True" : "False";
        return value;
    }
}
