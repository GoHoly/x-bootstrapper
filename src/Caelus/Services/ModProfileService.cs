using Caelus.Core;

namespace Caelus.Services;

public sealed record ModPresetInfo(
    string Name,
    bool BuiltIn,
    string Blurb,
    string? SkyId = null,
    int FileCount = 0);

/// <summary>
/// Named snapshots of the Modifications folder (ModProfiles\&lt;name&gt;), plus built-in starter presets
/// (stock / each sky). Loading a user preset first saves the current mods as "Before last load".
/// </summary>
public static class ModProfileService
{
    public const string AutoSaveName = "Before last load";
    public const string StockPresetName = "Stock (no mods)";

    public static readonly ModPresetInfo[] BuiltIn =
    {
        new(StockPresetName, true, "Clears every mod and puts Octane's original files back."),
        new("Sunset sky", true, "Warm horizon sky only — a clean starting point.", "sunset"),
        new("Starry night sky", true, "Deep night sky with stars and a moon.", "night"),
        new("Purple nebula sky", true, "Octane-purple space clouds.", "nebula"),
        new("Clear day sky", true, "Bright blue daytime sky.", "clearday")
    };

    public static IReadOnlyList<string> List()
    {
        if (!Directory.Exists(Paths.ModProfiles))
            return Array.Empty<string>();

        return Directory.GetDirectories(Paths.ModProfiles)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !name!.StartsWith(".", StringComparison.Ordinal))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ModPresetInfo> ListUserPresets()
    {
        return List().Select(name =>
        {
            var dir = Path.Combine(Paths.ModProfiles, name);
            var count = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Count(file => !Path.GetFileName(file).Equals("README.txt", StringComparison.OrdinalIgnoreCase))
                : 0;
            var sky = ReadSkyMarker(dir);
            var blurb = name.Equals(AutoSaveName, StringComparison.OrdinalIgnoreCase)
                ? "Automatic backup from the last Load / Remove all."
                : count == 0
                    ? "Empty preset."
                    : sky is null
                        ? $"{count} file(s)."
                        : $"{count} file(s) · sky: {sky}.";
            return new ModPresetInfo(name, false, blurb, sky, count);
        }).ToList();
    }

    public static string SafeName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        foreach (var bad in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(bad, '_');
        trimmed = trimmed.Trim('.', ' ');
        if (trimmed.Length > 60)
            trimmed = trimmed[..60].Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Enter a preset name.");
        if (BuiltIn.Any(preset => preset.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("That name is reserved for a built-in preset. Pick another.");
        return trimmed;
    }

    public static string Save(string name)
    {
        var safe = SafeName(name);
        Directory.CreateDirectory(Paths.ModProfiles);
        Directory.CreateDirectory(Paths.Modifications);
        var dest = Path.Combine(Paths.ModProfiles, safe);
        var temp = Path.Combine(Paths.ModProfiles, $".tmp-{Guid.NewGuid():N}");
        try
        {
            CopyTree(Paths.Modifications, temp);
            if (Directory.Exists(dest))
                Directory.Delete(dest, recursive: true);
            Directory.Move(temp, dest);
        }
        finally
        {
            TryDelete(temp);
        }

        Logger.Write("Mods", $"Saved mod preset \"{safe}\"");
        return safe;
    }

    public static void Load(string name)
    {
        var safe = SafeNameAllowingAuto(name);
        var source = Path.Combine(Paths.ModProfiles, safe);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"The preset \"{safe}\" no longer exists.");

        // Stage the profile first: it might be the auto-save we are about to overwrite.
        var staged = Path.Combine(Path.GetTempPath(), $"xb-modprofile-{Guid.NewGuid():N}");
        try
        {
            CopyTree(source, staged);
            if (Directory.Exists(Paths.Modifications) && Directory.EnumerateFileSystemEntries(Paths.Modifications).Any())
                Save(AutoSaveName);

            ReplaceModifications(staged);
        }
        finally
        {
            TryDelete(staged);
        }

        Logger.Write("Mods", $"Loaded mod preset \"{safe}\"");
        ModService.ApplyToInstalledClients();
    }

    /// <summary>Applies a built-in starter (stock or sky-only). Autosaves the current mods first.</summary>
    public static void ApplyBuiltIn(ModPresetInfo preset)
    {
        if (!preset.BuiltIn)
            throw new ArgumentException("Not a built-in preset.");

        if (Directory.Exists(Paths.Modifications) && Directory.EnumerateFileSystemEntries(Paths.Modifications).Any())
            Save(AutoSaveName);

        if (preset.Name.Equals(StockPresetName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(preset.SkyId))
        {
            var empty = Path.Combine(Path.GetTempPath(), $"xb-modprofile-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(empty);
                ReplaceModifications(empty);
            }
            finally
            {
                TryDelete(empty);
            }

            ModService.RestoreAllOriginals();
            Logger.Write("Mods", "Applied built-in preset Stock");
            return;
        }

        // Sky-only starter: wipe mods, then generate the built-in sky into Modifications.
        var wipe = Path.Combine(Path.GetTempPath(), $"xb-modprofile-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(wipe);
            ReplaceModifications(wipe);
            ModService.RestoreAllOriginals();
            SkyboxService.ApplyBuiltIn(preset.SkyId!);
        }
        finally
        {
            TryDelete(wipe);
        }

        Logger.Write("Mods", $"Applied built-in sky preset \"{preset.Name}\"");
    }

    /// <summary>Removes every mod (after saving them as "Before last load") and puts the client's files back.</summary>
    public static void RemoveAllMods()
    {
        if (Directory.Exists(Paths.Modifications) && Directory.EnumerateFileSystemEntries(Paths.Modifications).Any())
            Save(AutoSaveName);

        var empty = Path.Combine(Path.GetTempPath(), $"xb-modprofile-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(empty);
            ReplaceModifications(empty);
        }
        finally
        {
            TryDelete(empty);
        }

        ModService.RestoreAllOriginals();
        Logger.Write("Mods", "Removed all mods");
    }

    public static void Delete(string name)
    {
        var safe = SafeNameAllowingAuto(name);
        if (safe.Equals(AutoSaveName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The automatic \"Before last load\" backup can't be deleted.");

        var dir = Path.Combine(Paths.ModProfiles, safe);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public static string CurrentSummary()
    {
        var count = ModService.Count();
        var sky = SkyboxService.CurrentName();
        if (count == 0)
            return "No mods active (stock client files).";
        return $"{count} mod file(s) active · sky: {sky}.";
    }

    private static string SafeNameAllowingAuto(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Equals(AutoSaveName, StringComparison.OrdinalIgnoreCase))
            return AutoSaveName;
        return SafeName(trimmed);
    }

    private static string? ReadSkyMarker(string profileDir)
    {
        try
        {
            var marker = Path.Combine(profileDir, "xb-sky.txt");
            if (!File.Exists(marker))
                return null;
            var id = File.ReadAllText(marker).Trim().ToLowerInvariant();
            if (id == SkyboxService.CustomId)
                return "Custom";
            var preset = SkyboxService.BuiltIn.FirstOrDefault(p => p.Id == id);
            return preset?.Name ?? id;
        }
        catch
        {
            return null;
        }
    }

    private static void ReplaceModifications(string source)
    {
        Directory.CreateDirectory(Paths.Modifications);
        foreach (var file in Directory.GetFiles(Paths.Modifications))
            File.Delete(file);
        foreach (var dir in Directory.GetDirectories(Paths.Modifications))
            Directory.Delete(dir, recursive: true);
        CopyTree(source, Paths.Modifications);
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        if (!Directory.Exists(source))
            return;

        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            /* ignore */
        }
    }
}
