using System.Security.Cryptography;
using System.Text.Json;
using Caelus.Core;

namespace Caelus.Services;

public sealed class AppliedModFile
{
    public string Sha256 { get; set; } = "";
    public bool HadOriginal { get; set; }
    // True when the file was already modded before backups existed (2.0.x), so no clean original is on disk.
    public bool NoBackup { get; set; }
    public DateTime AppliedUtc { get; set; }
}

public sealed class ModManifest
{
    public string VersionDirectory { get; set; } = "";
    public Dictionary<string, AppliedModFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Remembers which client files X Bootstrapper overwrote with mods, keeps a copy of each original,
/// and puts the original back when the mod is removed. One store per client version folder,
/// kept under &lt;install&gt;\ModBackups\&lt;key&gt;.
/// </summary>
public sealed class ModBackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _storeDir;
    private readonly ModManifest _manifest;

    public string VersionDirectory { get; }
    public IReadOnlyDictionary<string, AppliedModFile> Files => _manifest.Files;

    private ModBackupStore(string versionDirectory, string storeDir, ModManifest manifest)
    {
        VersionDirectory = versionDirectory;
        _storeDir = storeDir;
        _manifest = manifest;
    }

    private string ManifestPath => Path.Combine(_storeDir, "manifest.json");
    private string OriginalsDir => Path.Combine(_storeDir, "originals");

    public static ModBackupStore Open(string versionDirectory)
    {
        var full = Path.GetFullPath(versionDirectory);
        var storeDir = Path.Combine(Paths.ModBackups, Key(full));
        var manifest = new ModManifest { VersionDirectory = full };
        var path = Path.Combine(storeDir, "manifest.json");
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    manifest.Files = new Dictionary<string, AppliedModFile>(loaded.Files ?? new(), StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Mods", $"Could not read mod manifest {path}: {ex.Message}");
            }
        }

        return new ModBackupStore(full, storeDir, manifest);
    }

    public static IEnumerable<ModBackupStore> OpenAll()
    {
        if (!Directory.Exists(Paths.ModBackups))
            yield break;

        foreach (var dir in Directory.GetDirectories(Paths.ModBackups))
        {
            var path = Path.Combine(dir, "manifest.json");
            if (!File.Exists(path))
                continue;

            string? versionDir = null;
            try
            {
                versionDir = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(path))?.VersionDirectory;
            }
            catch (Exception ex)
            {
                Logger.Write("Mods", $"Could not read mod manifest {path}: {ex.Message}");
            }

            if (!string.IsNullOrWhiteSpace(versionDir))
                yield return Open(versionDir);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(_storeDir);
        var temp = ManifestPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_manifest, JsonOptions));
        File.Move(temp, ManifestPath, overwrite: true);
    }

    /// <summary>Copies <paramref name="source"/> over the client file, backing up the original first.</summary>
    public bool Apply(string relative, string source)
    {
        var target = MatchExistingName(Path.Combine(VersionDirectory, relative));
        var sourceHash = Hash(source);
        _manifest.Files.TryGetValue(relative, out var entry);

        if (File.Exists(target))
        {
            var currentHash = Hash(target);
            var ours = entry is not null && string.Equals(entry.Sha256, currentHash, StringComparison.OrdinalIgnoreCase);
            if (!ours)
            {
                if (entry is null && string.Equals(currentHash, sourceHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Written by 2.0.x before backups existed: the stock file is already gone.
                    entry = new AppliedModFile { HadOriginal = true, NoBackup = true };
                }
                else
                {
                    // Stock file (or the client updated it since we last wrote it): keep it as the original.
                    var backup = BackupPath(relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: true);
                    entry = new AppliedModFile { HadOriginal = true };
                }
            }

            if (string.Equals(currentHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                entry!.Sha256 = sourceHash;
                _manifest.Files[relative] = entry;
                return false;
            }
        }
        else
        {
            entry ??= new AppliedModFile { HadOriginal = false };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        CopyReplace(source, target);
        entry!.Sha256 = sourceHash;
        entry.AppliedUtc = DateTime.UtcNow;
        _manifest.Files[relative] = entry;
        return true;
    }

    /// <summary>Puts the original back (or deletes a file the client never had) and forgets the entry.</summary>
    public void Restore(string relative)
    {
        if (!_manifest.Files.TryGetValue(relative, out var entry))
            return;

        var target = MatchExistingName(Path.Combine(VersionDirectory, relative));
        var backup = BackupPath(relative);

        if (File.Exists(target) && !string.Equals(Hash(target), entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            // The client replaced the file after we modded it; that file is the new original.
            Logger.Write("Mods", $"{relative} was changed by the client; leaving it.");
        }
        else if (entry.HadOriginal && File.Exists(backup))
        {
            CopyReplace(backup, target);
        }
        else if (!entry.HadOriginal)
        {
            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
            }
        }
        else
        {
            Logger.Write("Mods", $"No clean original for {relative} (applied before 2.1). Repair Octane to get the stock file back.");
        }

        TryDelete(backup);
        _manifest.Files.Remove(relative);
    }

    public int RestoreAll()
    {
        var count = 0;
        foreach (var relative in _manifest.Files.Keys.ToList())
        {
            try
            {
                Restore(relative);
                count++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Write("Mods", $"Could not restore {relative}: {ex.Message}");
            }
        }

        return count;
    }

    private string BackupPath(string relative) => Path.Combine(OriginalsDir, relative);

    /// <summary>
    /// The client's own copy of a file: the backed-up original while a mod replaces it, otherwise the file in
    /// the client folder. Null when neither exists.
    /// </summary>
    public string? OriginalFile(string relative)
    {
        if (_manifest.Files.TryGetValue(relative, out var entry))
        {
            if (!entry.HadOriginal)
                return null;
            var backup = BackupPath(relative);
            return File.Exists(backup) ? backup : null;
        }

        var target = MatchExistingName(Path.Combine(VersionDirectory, relative));
        return File.Exists(target) ? target : null;
    }

    private static string Key(string versionDirectory)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(versionDirectory.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }

    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal static string MatchExistingName(string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        var name = Path.GetFileName(destination);
        if (directory is null || !Directory.Exists(directory))
            return destination;

        var existing = Directory.GetFiles(directory)
            .FirstOrDefault(file => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));
        return existing ?? destination;
    }

    internal static void CopyReplace(string source, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (File.Exists(destination))
                    File.SetAttributes(destination, FileAttributes.Normal);
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(60);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Write("Mods", $"Could not delete backup {path}: {ex.Message}");
        }
    }
}
