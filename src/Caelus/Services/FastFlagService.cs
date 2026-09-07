using System.Text.Json;
using System.Text.Json.Nodes;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public static class FastFlagService
{
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
                flags["FFlagDebugGraphicsPreferD3D11"] = "true";
                flags["FFlagDebugGraphicsDisableDirect3D11"] = "false";
                break;
            case RenderingMode.Vulkan:
                flags["FFlagDebugGraphicsPreferVulkan"] = "true";
                break;
            case RenderingMode.OpenGL:
                flags["FFlagDebugGraphicsPreferOpenGL"] = "true";
                break;
        }

        if (settings.DisablePostFx)
            flags["FFlagDisablePostFx"] = "true";

        if (settings.TextureQuality >= 0)
        {
            flags["DFFlagTextureQualityOverrideEnabled"] = "true";
            flags["DFIntTextureQualityOverride"] = settings.TextureQuality.ToString();
        }

        return flags;
    }

    public static void Apply(ClientInstall install, Settings settings, bool log = true)
    {
        var flags = Build(settings);
        var directory = Path.Combine(install.VersionDirectory, "ClientSettings");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "ClientAppSettings.json");
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

        foreach (var (key, value) in flags)
            root[key] = ToJsonValue(value);

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        if (log)
            Logger.Write("FastFlags", $"Wrote {flags.Count} flag(s) to {path}");
    }

    private static JsonNode ToJsonValue(string value)
    {
        if (bool.TryParse(value, out var boolean))
            return JsonValue.Create(boolean)!;
        if (long.TryParse(value, out var number))
            return JsonValue.Create(number)!;
        if (double.TryParse(value, out var real))
            return JsonValue.Create(real)!;
        return JsonValue.Create(value)!;
    }
}
