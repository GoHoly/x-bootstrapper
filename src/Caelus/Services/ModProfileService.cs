using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// Named snapshots of the Modifications folder (ModProfiles\&lt;name&gt;). Loading a profile first
/// saves the current mods as "Before last load", so switching is always reversible.
/// </summary>
public static class ModProfileService
{
    public const string AutoSaveName = "Before last load";

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

    public static string SafeName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        foreach (var bad in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(bad, '_');
        trimmed = trimmed.Trim('.', ' ');
        if (trimmed.Length > 60)
            trimmed = trimmed[..60].Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Enter a profile name.");
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

        Logger.Write("Mods", $"Saved mod profile \"{safe}\"");
        return safe;
    }

    public static void Load(string name)
    {
        var safe = SafeName(name);
        var source = Path.Combine(Paths.ModProfiles, safe);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"The profile \"{safe}\" no longer exists.");

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

        Logger.Write("Mods", $"Loaded mod profile \"{safe}\"");
        ModService.ApplyToInstalledClients();
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
        var dir = Path.Combine(Paths.ModProfiles, SafeName(name));
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
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
