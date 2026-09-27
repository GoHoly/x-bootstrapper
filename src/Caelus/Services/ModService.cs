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

    public static readonly ModSlot[] Slots =
    {
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
        return null;
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
        var root = ModPackImporter.FindRoot(sourceDirectory);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).Equals("README.txt", StringComparison.OrdinalIgnoreCase))
            .Select(file => (File: file, Relative: Path.GetRelativePath(root, file)))
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
        var temp = Path.Combine(Path.GetTempPath(), "caelus-modpack-" + Guid.NewGuid().ToString("N"));
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
        foreach (var root in new[]
                 {
                     Path.Combine(Paths.LocalAppData, "Octane"),
                     Paths.Base
                 })
        {
            var install = ClientLocator.FindInRoot(root);
            if (install is not null)
                Apply(install, log: true);
        }
    }

    public static int Apply(ClientInstall install, bool log = true)
    {
        MigrateLegacyCursor();
        var copied = 0;
        foreach (var (file, relative) in Enumerate())
        {
            if (!IsClientRelative(relative))
                continue;

            foreach (var destination in ClientDestinations(install.VersionDirectory, relative))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    CopyReplace(file, MatchExistingName(destination));
                    copied++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Write("Mods", $"Could not copy {relative}: {ex.Message}");
                }
            }
        }

        try
        {
            ImageSetHudPatcher.Apply(install.VersionDirectory);
        }
        catch (Exception ex)
        {
            Logger.Write("Mods", $"Could not patch HUD ImageSet icons: {ex}");
        }

        if (log)
            Logger.Write("Mods", $"Applied {copied} file(s) to {install.VersionDirectory}");

        return copied;
    }

    public static void OpenFolder()
    {
        Directory.CreateDirectory(Paths.Modifications);
        var readme = Path.Combine(Paths.Modifications, "README.txt");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                "Drop 2021-era client files here using the same paths as the Octane version folder.\r\n" +
                "Or use the Mods page pickers for cursors, shiftlock, emote wheel, Tab list, and sounds.\r\n" +
                "Examples:\r\n" +
                "  content\\sounds\\uuhhh.mp3\r\n" +
                "  content\\textures\\ArrowCursor.png\r\n" +
                "  content\\textures\\MouseLockedCursor.png\r\n" +
                "  content\\textures\\ui\\Emotes\\Large\\SegmentedCircle.png\r\n" +
                "  content\\textures\\ui\\TopBar\\leaderboardOn.png\r\n" +
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

    private static IEnumerable<string> ClientDestinations(string versionDirectory, string relative)
    {
        yield return Path.Combine(versionDirectory, relative);

        const string prefix = "content\\textures\\";
        if (relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("content/textures/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = relative["content".Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            yield return Path.Combine(versionDirectory, "ExtraContent", rest);
        }
    }

    private static string MatchExistingName(string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        var name = Path.GetFileName(destination);
        if (directory is null || !Directory.Exists(directory))
            return destination;

        var existing = Directory.GetFiles(directory)
            .FirstOrDefault(file => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));
        return existing ?? destination;
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
            if (relative.Equals("README.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return (file, relative);
        }
    }

    private static void CopyReplace(string source, string destination)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (File.Exists(destination))
                {
                    File.SetAttributes(destination, FileAttributes.Normal);
                    File.Delete(destination);
                }

                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(40);
            }
        }
    }
}
