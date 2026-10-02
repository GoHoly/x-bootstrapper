using System.IO.Compression;
using Caelus.Core;

namespace Caelus.Services;

public sealed class ModSlot
{
    public string Id { get; init; } = "";
    public string Group { get; init; } = "Other";
    public string Title { get; init; } = "";
    public string Hint { get; init; } = "";
    public string RelativePath { get; init; } = "";
    public string[] ExtraPaths { get; init; } = Array.Empty<string>();
    public string Filter { get; init; } = "";

    public IEnumerable<string> AllPaths
    {
        get
        {
            yield return RelativePath;
            foreach (var path in ExtraPaths)
                yield return path;
        }
    }
}

public static class ModService
{
    private const string AudioFilter = "Audio|*.ogg;*.mp3;*.wav|All files|*.*";
    private const string ImageFilter = "Images|*.png;*.jpg;*.jpeg|All files|*.*";
    private const string FontFilter = "Fonts|*.ttf;*.otf|All files|*.*";
    private const string TextureFilter = "Textures|*.dds;*.png;*.jpg;*.jpeg;*.tex|All files|*.*";

    public const string WaterRelativeFolder = @"content\textures\water";
    public const string ParticlesRelativeFolder = @"content\textures\particles";
    public const string ClientSkyFolder = @"content\sky";

    public static readonly ModSlot[] Slots =
    {
        new()
        {
            Id = "clouds",
            Group = "Atmosphere",
            Title = "Clouds",
            Hint = "DDS — default cloud layer (content\\sky\\clouds.dds)",
            RelativePath = P("content", "sky", "clouds.dds"),
            ExtraPaths = new[] { P("content", "sky", "clouds-bc4.dds") },
            Filter = TextureFilter
        },
        new()
        {
            Id = "cloudDetail",
            Group = "Atmosphere",
            Title = "Cloud detail",
            Hint = "DDS — cloud noise / detail",
            RelativePath = P("content", "sky", "cloudDetail.dds"),
            ExtraPaths = new[]
            {
                P("content", "sky", "cloudDetail3D.dds"),
                P("content", "sky", "cloudDetail3D-bc4.dds")
            },
            Filter = TextureFilter
        },
        new()
        {
            Id = "cloudAdvection",
            Group = "Atmosphere",
            Title = "Cloud advection",
            Hint = "DDS — cloud motion map",
            RelativePath = P("content", "sky", "cloudAdvection.dds"),
            Filter = TextureFilter
        },
        new()
        {
            Id = "sun",
            Group = "Atmosphere",
            Title = "Sun",
            Hint = "JPG — default sun disc",
            RelativePath = P("content", "sky", "sun.jpg"),
            ExtraPaths = new[] { P("content", "sky", "sun-rays.jpg") },
            Filter = TextureFilter
        },
        new()
        {
            Id = "moon",
            Group = "Atmosphere",
            Title = "Moon",
            Hint = "JPG — default moon",
            RelativePath = P("content", "sky", "moon.jpg"),
            ExtraPaths = new[] { P("content", "sky", "moon-alpha.jpg") },
            Filter = TextureFilter
        },
        new()
        {
            Id = "particleSquare",
            Group = "Atmosphere",
            Title = "Particle square",
            Hint = "PNG — common particle sprite",
            RelativePath = P("content", "textures", "particles", "SquareParticle.png"),
            Filter = TextureFilter
        },
        new()
        {
            Id = "death",
            Group = "Sounds",
            Title = "Death sound",
            Hint = "OGG or MP3",
            RelativePath = P("content", "sounds", "uuhhh.mp3"),
            ExtraPaths = new[] { P("content", "sounds", "ouch.ogg") },
            Filter = AudioFilter
        },
        new()
        {
            Id = "jump",
            Group = "Sounds",
            Title = "Jump sound",
            Hint = "OGG or MP3",
            RelativePath = P("content", "sounds", "action_jump.mp3"),
            Filter = AudioFilter
        },
        new()
        {
            Id = "land",
            Group = "Sounds",
            Title = "Land sound",
            Hint = "OGG or MP3",
            RelativePath = P("content", "sounds", "action_jump_land.mp3"),
            Filter = AudioFilter
        },
        new()
        {
            Id = "footsteps",
            Group = "Sounds",
            Title = "Footsteps",
            Hint = "OGG or MP3",
            RelativePath = P("content", "sounds", "action_footsteps_plastic.mp3"),
            Filter = AudioFilter
        },
        new()
        {
            Id = "cursor",
            Group = "Cursors",
            Title = "Mouse cursor",
            Hint = "PNG — in-game pointer",
            RelativePath = P("content", "textures", "ArrowCursor.png"),
            ExtraPaths = new[]
            {
                P("content", "textures", "ArrowCursorDecalDrag.png"),
                P("content", "textures", "Cursors", "KeyboardMouse", "ArrowCursor.png"),
                P("content", "textures", "ArrowFarCursor.png"),
                P("content", "textures", "Cursors", "KeyboardMouse", "ArrowFarCursor.png")
            },
            Filter = ImageFilter
        },
        new()
        {
            Id = "cursorFar",
            Group = "Cursors",
            Title = "Far cursor",
            Hint = "Optional hover pointer. PNG",
            RelativePath = P("content", "textures", "ArrowFarCursor.png"),
            ExtraPaths = new[] { P("content", "textures", "Cursors", "KeyboardMouse", "ArrowFarCursor.png") },
            Filter = ImageFilter
        },
        new()
        {
            Id = "shiftlock",
            Group = "Cursors",
            Title = "Shiftlock cursor",
            Hint = "PNG — shown while mouse is locked",
            RelativePath = P("content", "textures", "MouseLockedCursor.png"),
            ExtraPaths = new[] { P("content", "textures", "Cursors", "KeyboardMouse", "MouseLockedCursor.png") },
            Filter = ImageFilter
        },
        new()
        {
            Id = "emoteWheel",
            Group = "HUD",
            Title = "Emote wheel",
            Hint = "PNG — replaces the radial wheel",
            RelativePath = P("content", "textures", "ui", "Emotes", "Large", "SegmentedCircle.png"),
            ExtraPaths = ExtraScales(
                P("content", "textures", "ui", "Emotes", "Large", "SegmentedCircle.png"),
                P("content", "textures", "ui", "Emotes", "Small", "SegmentedCircle.png"),
                P("content", "textures", "ui", "Emotes", "TenFoot", "SegmentedCircle.png"),
                P("content", "textures", "ui", "Emotes", "Editor", "Large", "Wheel.png"),
                P("content", "textures", "ui", "Emotes", "Editor", "Small", "Wheel.png")),
            Filter = ImageFilter
        },
        new()
        {
            Id = "emoteButton",
            Group = "HUD",
            Title = "Emote button",
            Hint = "PNG — topbar emote icon",
            RelativePath = P("content", "textures", "ui", "TopBar", "emotesOn.png"),
            ExtraPaths = ExtraScales(
                P("content", "textures", "ui", "TopBar", "emotesOn.png"),
                P("content", "textures", "ui", "TopBar", "emotesOff.png"),
                P("content", "textures", "ui", "MenuBar", "icon_emote.png"),
                P("content", "textures", "ui", "Emotes", "EmotesIcon.png"),
                P("content", "textures", "ui", "Emotes", "EmotesRadialIcon.png")),
            Filter = ImageFilter
        },
        new()
        {
            Id = "tabIcon",
            Group = "HUD",
            Title = "Tab / player list icon",
            Hint = "PNG — the Tab leaderboard button",
            RelativePath = P("content", "textures", "ui", "TopBar", "leaderboardOn.png"),
            ExtraPaths = ExtraScales(
                P("content", "textures", "ui", "TopBar", "leaderboardOn.png"),
                P("content", "textures", "ui", "TopBar", "leaderboardOff.png"),
                P("content", "textures", "ui", "MenuBar", "icon_leaderboard.png"),
                P("content", "textures", "ui", "Settings", "Radial", "PlayerList.png"),
                P("content", "textures", "ui", "Settings", "MenuBarIcons", "PlayersTabIcon.png")),
            Filter = ImageFilter
        },
        new()
        {
            Id = "tabBackground",
            Group = "HUD",
            Title = "Tab / player list background",
            Hint = "PNG — panel behind the player list",
            RelativePath = P("content", "textures", "ui", "PlayerList", "NewAvatarBackground.png"),
            ExtraPaths = ExtraScales(
                P("content", "textures", "ui", "PlayerList", "NewAvatarBackground.png"),
                P("content", "textures", "ui", "PlayerList", "AvatarBackground.png")),
            Filter = ImageFilter
        },
        new()
        {
            Id = "chatIcon",
            Group = "HUD",
            Title = "Chat icon",
            Hint = "PNG — topbar chat button",
            RelativePath = P("content", "textures", "ui", "TopBar", "chatOn.png"),
            ExtraPaths = ExtraScales(
                P("content", "textures", "ui", "TopBar", "chatOn.png"),
                P("content", "textures", "ui", "TopBar", "chatOff.png"),
                P("content", "textures", "ui", "MenuBar", "icon_chat.png"),
                P("content", "textures", "ui", "Chat", "Chat.png"),
                P("content", "textures", "ui", "Chat", "ToggleChat.png"),
                P("content", "textures", "ui", "Settings", "Radial", "Chat.png")),
            Filter = ImageFilter
        },
        new()
        {
            Id = "healthBar",
            Group = "HUD",
            Title = "Health bar",
            Hint = "PNG — topbar health fill",
            RelativePath = P("content", "textures", "ui", "TopBar", "HealthBar.png"),
            ExtraPaths = new[]
            {
                P("content", "textures", "ui", "TopBar", "HealthBarTV.png"),
                P("content", "textures", "ui", "LegacyRbxGui", "health_greenBar.png")
            },
            Filter = ImageFilter
        },
        new()
        {
            Id = "font",
            Group = "Fonts",
            Title = "Custom font",
            Hint = "TTF or OTF — replaces the UI typefaces",
            RelativePath = P("content", "fonts", "SourceSansPro-Regular.ttf"),
            ExtraPaths = new[]
            {
                P("content", "fonts", "SourceSansPro-Bold.ttf"),
                P("content", "fonts", "SourceSansPro-Semibold.ttf"),
                P("content", "fonts", "SourceSansPro-Light.ttf"),
                P("content", "fonts", "SourceSansPro-It.ttf"),
                P("content", "fonts", "GothamSSm-Book.otf"),
                P("content", "fonts", "GothamSSm-Medium.otf"),
                P("content", "fonts", "GothamSSm-Bold.otf"),
                P("content", "fonts", "GothamSSm-Black.otf")
            },
            Filter = FontFilter
        }
    };

    public static string SlotPath(ModSlot slot) => Path.Combine(Paths.Modifications, slot.RelativePath);

    public static bool HasSlot(ModSlot slot) => File.Exists(SlotPath(slot));

    public static void SetSlot(ModSlot slot, string sourceFile)
    {
        foreach (var relative in slot.AllPaths)
            WriteModFile(sourceFile, relative);

        Logger.Write("Mods", $"Set {slot.Title} -> {slot.RelativePath}");
        ApplyToInstalledClients();
    }

    public static void ClearSlot(ModSlot slot)
    {
        foreach (var relative in slot.AllPaths)
        {
            var path = Path.Combine(Paths.Modifications, relative);
            if (File.Exists(path))
                File.Delete(path);
        }

        ApplyToInstalledClients();
    }

    public static void MigrateLegacyCursor()
    {
        var cursor = Slots.First(s => s.Id == "cursor");
        if (HasSlot(cursor))
            return;

        var legacy = Path.Combine(Paths.Modifications, "content", "textures", "Cursors", "KeyboardMouse", "ArrowCursor.png");
        if (!File.Exists(legacy))
            return;

        foreach (var relative in cursor.AllPaths)
            WriteModFile(legacy, relative);

        Logger.Write("Mods", "Moved the cursor onto the paths Octane actually loads.");
    }

    public static ModSlot? GuessSlot(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        if (name.Contains("ouch") || name.Contains("death") || name.Contains("uuhhh"))
            return Slots.First(s => s.Id == "death");
        if (name.Contains("land"))
            return Slots.First(s => s.Id == "land");
        if (name.Contains("foot") || name.Contains("walk"))
            return Slots.First(s => s.Id == "footsteps");
        if (name.Contains("jump"))
            return Slots.First(s => s.Id == "jump");
        if (name.Contains("lock") || name.Contains("shift"))
            return Slots.First(s => s.Id == "shiftlock");
        if (name.Contains("wheel") || (name.Contains("emote") && name.Contains("circle")))
            return Slots.First(s => s.Id == "emoteWheel");
        if (name.Contains("emote"))
            return Slots.First(s => s.Id == "emoteButton");
        if (name.Contains("leaderboard") || name.Contains("playerlist") || name.Contains("tab"))
            return Slots.First(s => s.Id == "tabIcon");
        if (name.Contains("chat"))
            return Slots.First(s => s.Id == "chatIcon");
        if (name.Contains("health"))
            return Slots.First(s => s.Id == "healthBar");
        if (name.Contains("far") && (name.EndsWith(".png") || name.EndsWith(".jpg") || name.EndsWith(".jpeg")))
            return Slots.First(s => s.Id == "cursorFar");
        if (name.Contains("cursor") || name.Contains("arrow"))
            return Slots.First(s => s.Id == "cursor");
        if (name.EndsWith(".ttf") || name.EndsWith(".otf"))
            return Slots.First(s => s.Id == "font");
        if (name.Contains("cloudadvect"))
            return Slots.First(s => s.Id == "cloudAdvection");
        if (name.Contains("clouddetail") || name.Contains("cloud_detail"))
            return Slots.First(s => s.Id == "cloudDetail");
        if (name.Contains("cloud"))
            return Slots.First(s => s.Id == "clouds");
        if (name.StartsWith("sun"))
            return Slots.First(s => s.Id == "sun");
        if (name.StartsWith("moon"))
            return Slots.First(s => s.Id == "moon");
        if (name.Contains("squareparticle") || name.Contains("particle"))
            return Slots.First(s => s.Id == "particleSquare");
        return null;
    }

    public static IEnumerable<ModSlot> AtmosphereSlots() =>
        Slots.Where(slot => slot.Group.Equals("Atmosphere", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ModSlot> NonAtmosphereSlots() =>
        Slots.Where(slot => !slot.Group.Equals("Atmosphere", StringComparison.OrdinalIgnoreCase));

    /// <summary>Copies every file from a folder into Modifications under <paramref name="relativeFolder"/> (flat).</summary>
    public static int ImportFlatFolder(string sourceDirectory, string relativeFolder)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException(sourceDirectory);

        relativeFolder = relativeFolder.Trim().TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (!IsClientRelative(relativeFolder + Path.DirectorySeparatorChar + "x"))
            throw new InvalidOperationException("That folder is not under content, ExtraContent, or PlatformContent.");

        var written = 0;
        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("README.txt", StringComparison.OrdinalIgnoreCase))
                continue;
            WriteModFile(file, Path.Combine(relativeFolder, name));
            written++;
        }

        if (written == 0)
            throw new InvalidOperationException("That folder has no files to import.");

        Logger.Write("Mods", $"Imported {written} file(s) into {relativeFolder}");
        ApplyToInstalledClients();
        return written;
    }

    public static string IndoorSkyRelativePath(string face) =>
        Path.Combine("content", "textures", "sky", $"indoor512_{face}.tex");

    public static bool HasIndoorSkyMods() =>
        SkyboxService.Faces.Any(face => File.Exists(Path.Combine(Paths.Modifications, IndoorSkyRelativePath(face))));

    public static void ClearIndoorSky()
    {
        foreach (var face in SkyboxService.Faces)
        {
            var path = Path.Combine(Paths.Modifications, IndoorSkyRelativePath(face));
            if (File.Exists(path))
                File.Delete(path);
        }

        ApplyToInstalledClients();
        Logger.Write("Mods", "Cleared indoor sky mods.");
    }

    /// <summary>
    /// Imports six indoor cubemap faces from a folder. Accepts indoor512_*.tex or bk/dn/ft/lf/rt/up named files.
    /// </summary>
    public static int ImportIndoorSkyFolder(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException(sourceDirectory);

        var files = Directory.GetFiles(sourceDirectory);
        var matched = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in SkyboxService.Faces)
        {
            var hit = files.FirstOrDefault(file =>
            {
                var name = Path.GetFileNameWithoutExtension(file);
                return name.Equals($"indoor512_{face}", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals(face, StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith($"_{face}", StringComparison.OrdinalIgnoreCase);
            });
            if (hit is not null)
                matched[face] = hit;
        }

        if (matched.Count == 0)
            throw new InvalidOperationException(
                "No indoor sky faces found. Name files indoor512_bk.tex … indoor512_up.tex (or bk, dn, ft, lf, rt, up).");

        foreach (var (face, file) in matched)
            WriteModFile(file, IndoorSkyRelativePath(face));

        Logger.Write("Mods", $"Imported indoor sky ({matched.Count} face(s))");
        ApplyToInstalledClients();
        return matched.Count;
    }

    /// <summary>Resolves an absolute path under the client install to a content-relative mod path.</summary>
    public static string ClientRelativePath(string absoluteClientFile, ClientInstall install)
    {
        if (string.IsNullOrWhiteSpace(install.VersionDirectory))
            throw new InvalidOperationException("No Octane client folder is available.");
        if (!File.Exists(absoluteClientFile))
            throw new FileNotFoundException("That file is missing.", absoluteClientFile);

        var root = Path.GetFullPath(install.VersionDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(absoluteClientFile);
        if (!IsInside(root, full))
            throw new InvalidOperationException(
                "Pick a file inside the Octane client folder (content, ExtraContent, or PlatformContent).");

        var relative = Path.GetRelativePath(root, full);
        if (!IsClientRelative(relative))
            throw new InvalidOperationException(
                "Only files under content, ExtraContent, or PlatformContent can be replaced.");

        return relative;
    }

    /// <summary>
    /// Stages a byte-identical copy of a client file into Modifications (same relative path).
    /// Prefer <see cref="SetClientRelativeFromFile"/> when you already have a replacement.
    /// </summary>
    public static string ReplaceClientFile(string absoluteClientFile, ClientInstall install)
    {
        var relative = ClientRelativePath(absoluteClientFile, install);
        WriteModFile(absoluteClientFile, relative);
        Logger.Write("Mods", $"Staged client file for replace: {relative}");
        ApplyToInstalledClients();
        return relative;
    }

    /// <summary>Copies a replacement into Modifications at a client-relative path and applies it.</summary>
    public static string SetClientRelativeFromFile(string sourceFile, string relativePath)
    {
        relativePath = relativePath.Trim().TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!IsClientRelative(relativePath))
            throw new InvalidOperationException(
                "Only files under content, ExtraContent, or PlatformContent can be replaced.");

        WriteModFile(sourceFile, relativePath);
        Logger.Write("Mods", $"Set client-relative mod: {relativePath}");
        ApplyToInstalledClients();
        return relativePath;
    }

    private static string P(params string[] parts) => Path.Combine(parts);

    private static string[] ExtraScales(string primary, params string[] siblings)
    {
        var paths = new List<string>();
        paths.AddRange(ScaleSuffixes(primary));
        foreach (var sibling in siblings)
        {
            paths.Add(sibling);
            paths.AddRange(ScaleSuffixes(sibling));
        }

        return paths.ToArray();
    }

    private static IEnumerable<string> ScaleSuffixes(string relative)
    {
        var directory = Path.GetDirectoryName(relative) ?? "";
        var stem = Path.GetFileNameWithoutExtension(relative);
        var extension = Path.GetExtension(relative);
        yield return Path.Combine(directory, stem + "@2x" + extension);
        yield return Path.Combine(directory, stem + "@3x" + extension);
    }

    public static int ImportFolder(string sourceDirectory)
    {
        Directory.CreateDirectory(Paths.Modifications);
        // Scan only what was selected. If the selected folder is "content" itself, paths are made
        // relative to its parent so they keep their "content\..." prefix, but the parent is never scanned.
        var root = ModPackImporter.FindRoot(sourceDirectory);
        var relativeBase = new DirectoryInfo(root).Name.Equals("content", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(root)?.FullName ?? root
            : root;
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).Equals("README.txt", StringComparison.OrdinalIgnoreCase))
            .Select(file => (File: file, Relative: Path.GetRelativePath(relativeBase, file)))
            .ToList();

        var written = 0;
        var writtenThisImport = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, relative) in files)
        {
            foreach (var destination in ModPackImporter.DirectDestinations(relative))
            {
                WriteModFile(file, destination);
                writtenThisImport.Add(destination);
                written++;
            }
        }

        foreach (var (file, relative) in files)
        {
            foreach (var destination in ModPackImporter.AliasDestinations(relative))
            {
                if (writtenThisImport.Contains(destination) && !ModPackImporter.IsMenuBarHudAlias(relative))
                    continue;

                WriteModFile(file, destination);
                writtenThisImport.Add(destination);
                written++;
            }
        }

        Logger.Write("Mods", $"Imported folder {root} ({written} file(s))");
        ApplyToInstalledClients();
        return written;
    }

    public static int ImportZip(string zipPath)
    {
        Directory.CreateDirectory(Paths.Modifications);
        var temp = Path.Combine(Path.GetTempPath(), "xb-modpack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, temp);
            var written = ImportFolder(temp);
            Logger.Write("Mods", $"Imported zip {zipPath}");
            return written;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* ignore */ }
        }
    }

    public static int Count()
    {
        return Enumerate().Count(item => IsClientRelative(item.Relative));
    }

    public static void ApplyToInstalledClients()
    {
        // Every client X Bootstrapper knows about, including a custom client folder.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var install in ClientLocator.FindAll(App.Settings.Prop, App.State.Prop, null))
        {
            if (string.IsNullOrWhiteSpace(install.VersionDirectory) || !seen.Add(install.VersionDirectory))
                continue;
            Apply(install, log: seen.Count == 1);
        }
    }

    /// <summary>Developer tests only: leave the HUD atlas alone (it keeps its own originals in the client folder).</summary>
    internal static bool SkipHudPatch { get; set; }

    public static int Apply(ClientInstall install, bool log = true)
    {
        if (string.IsNullOrWhiteSpace(install.VersionDirectory) || !Directory.Exists(install.VersionDirectory))
            return 0;

        MigrateLegacyCursor();

        var desired = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, relative) in Enumerate())
        {
            if (!IsClientRelative(relative))
                continue;

            foreach (var destination in ClientDestinations(relative))
                desired[destination] = file;
        }

        var store = ModBackupStore.Open(install.VersionDirectory);
        var restored = 0;
        foreach (var relative in store.Files.Keys.Where(key => !desired.ContainsKey(key)).ToList())
        {
            try
            {
                store.Restore(relative);
                restored++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Write("Mods", $"Could not restore {relative}: {ex.Message}");
            }
        }

        var copied = 0;
        foreach (var (relative, source) in desired)
        {
            try
            {
                if (store.Apply(relative, source))
                    copied++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Write("Mods", $"Could not copy {relative}: {ex.Message}");
            }
        }

        try
        {
            store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Write("Mods", $"Could not save the mod manifest: {ex.Message}");
        }

        try
        {
            if (!SkipHudPatch)
                ImageSetHudPatcher.Apply(install.VersionDirectory);
        }
        catch (Exception ex)
        {
            Logger.Write("Mods", $"Could not patch HUD ImageSet icons: {ex}");
        }

        if (log)
            Logger.Write("Mods", $"Applied {copied} changed file(s), restored {restored} original(s), {desired.Count} mod file(s) active in {install.VersionDirectory}");

        return copied;
    }

    /// <summary>Puts every original client file back (used by uninstall and "Restore originals").</summary>
    public static int RestoreAllOriginals()
    {
        var restored = 0;
        var versionDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var store in ModBackupStore.OpenAll().ToList())
        {
            versionDirs.Add(store.VersionDirectory);
            restored += store.RestoreAll();
            try
            {
                store.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Write("Mods", $"Could not save the mod manifest: {ex.Message}");
            }
        }

        foreach (var versionDir in versionDirs)
        {
            try
            {
                ImageSetHudPatcher.Restore(versionDir);
            }
            catch (Exception ex)
            {
                Logger.Write("Mods", $"Could not restore HUD atlas in {versionDir}: {ex.Message}");
            }
        }

        Logger.Write("Mods", $"Restored {restored} original client file(s).");
        return restored;
    }

    public static IReadOnlyList<string> AppliedFiles(ClientInstall? install)
    {
        if (install is null || string.IsNullOrWhiteSpace(install.VersionDirectory))
            return Array.Empty<string>();

        return ModBackupStore.Open(install.VersionDirectory).Files.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void OpenFolder()
    {
        Directory.CreateDirectory(Paths.Modifications);
        var readme = Path.Combine(Paths.Modifications, "README.txt");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                "Drop 2021-era client files here using the same paths as the Octane version folder.\r\n" +
                "Or use the Mods page pickers for atmosphere, cursors, HUD, and sounds.\r\n" +
                "Examples:\r\n" +
                "  content\\sky\\clouds.dds\r\n" +
                "  content\\sky\\sun.jpg\r\n" +
                "  content\\textures\\sky\\indoor512_ft.tex\r\n" +
                "  content\\textures\\particles\\SquareParticle.png\r\n" +
                "  content\\textures\\water\\normal_01.dds\r\n" +
                "  content\\sounds\\uuhhh.mp3\r\n" +
                "  content\\textures\\ArrowCursor.png\r\n" +
                "  content\\textures\\MouseLockedCursor.png\r\n" +
                "  content\\textures\\ui\\Emotes\\Large\\SegmentedCircle.png\r\n" +
                "  content\\textures\\ui\\TopBar\\leaderboardOn.png\r\n" +
                "Games that set their own Sky / fog keep theirs. CDN item skins (e.g. Jailbreak guns) are not client files.\r\n" +
                "X Bootstrapper copies these over the installed client every launch.\r\n");
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Paths.Modifications,
            UseShellExecute = true
        });
    }

    private static void WriteModFile(string sourceFile, string relative)
    {
        var destination = Path.Combine(Paths.Modifications, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(destination))
        {
            File.SetAttributes(destination, FileAttributes.Normal);
            File.Delete(destination);
        }

        File.Copy(sourceFile, destination, overwrite: true);
    }

    private static bool IsClientRelative(string relative) =>
        relative.StartsWith("content" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("ExtraContent" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("PlatformContent" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ClientDestinations(string relative)
    {
        yield return relative;

        // The sky only lives in content\textures\sky; mirroring it into ExtraContent would add files the client never had.
        if (SkyboxService.IsSkyRelative(relative))
            yield break;

        const string prefix = "content\\textures\\";
        if (relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("content/textures/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = relative["content".Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            yield return Path.Combine("ExtraContent", rest);
        }
    }

    private static string ResolveRelative(string relative)
    {
        relative = relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relative.Contains(Path.DirectorySeparatorChar) || relative.Contains(Path.AltDirectorySeparatorChar))
            return relative;

        return GuessSlot(relative)?.RelativePath ?? relative;
    }

    private static bool IsInside(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string File, string Relative)> Enumerate()
    {
        if (!Directory.Exists(Paths.Modifications))
            yield break;

        foreach (var file in Directory.GetFiles(Paths.Modifications, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Paths.Modifications, file);
            if (relative.Equals("README.txt", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals("xb-sky.txt", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return (file, relative);
        }
    }
}
