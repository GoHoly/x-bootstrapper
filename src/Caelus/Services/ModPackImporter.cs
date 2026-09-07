using Caelus.Core;

namespace Caelus.Services;

public static class ModPackImporter
{
    public static string FindRoot(string selected)
    {
        var current = new DirectoryInfo(Path.GetFullPath(selected));
        for (var dir = current; dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "content")))
                return dir.FullName;
            if (dir.Name.Equals("content", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
                return dir.Parent.FullName;
        }

        var nested = Directory.EnumerateDirectories(selected, "content", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (nested is not null)
            return Directory.GetParent(nested)!.FullName;

        return Path.GetFullPath(selected);
    }

    public static IEnumerable<string> DirectDestinations(string relative)
    {
        relative = Canonicalize(NormalizePrefix(StripToContent(relative)));
        if (!relative.StartsWith("content" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            yield break;

        yield return relative;
        foreach (var scaled in MissingScales(relative))
            yield return scaled;
    }

    public static IEnumerable<string> AliasDestinations(string relative)
    {
        relative = NormalizePrefix(StripToContent(relative));
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in Aliases(relative))
        {
            var canonical = Canonicalize(alias);
            if (!canonical.StartsWith("content" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;
            if (canonical.Equals(Canonicalize(relative), StringComparison.OrdinalIgnoreCase))
                continue;

            paths.Add(canonical);
            foreach (var scaled in MissingScales(canonical))
                paths.Add(scaled);
        }

        return paths;
    }

    public static bool IsMenuBarHudAlias(string relative)
    {
        var name = Path.GetFileNameWithoutExtension(relative).Replace("_", "").ToLowerInvariant();
        return name is "iconchat" or "iconemote" or "iconleaderboard"
            or "iconbackpack" or "iconmore" or "iconmenu" or "iconhome";
    }

    public static IEnumerable<string> Destinations(string relative) =>
        DirectDestinations(relative).Concat(AliasDestinations(relative));

    public static string StripToContent(string relative)
    {
        relative = relative.Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        var parts = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(parts, part => part.Equals("content", StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            return string.Join(Path.DirectorySeparatorChar, parts[index..]);

        return relative;
    }

    private static string NormalizePrefix(string relative)
    {
        if (relative.StartsWith("content" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return relative;

        if (relative.StartsWith("textures" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            relative.Equals("textures", StringComparison.OrdinalIgnoreCase))
            return Path.Combine("content", relative);

        if (StartsWithAny(relative, "ui", "Cursors", "MenuBar", "loading", "particles", "DevConsole", "StudioToolbox"))
            return Path.Combine("content", "textures", relative);

        return relative;
    }

    private static IEnumerable<string> Aliases(string relative)
    {
        var file = Path.GetFileName(relative);
        var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

        if (relative.Contains(Path.Combine("Cursors", "KeyboardMouse"), StringComparison.OrdinalIgnoreCase) ||
            name is "arrowcursor" or "arrowfarcursor" or "ibeamcursor" or "mouselockedcursor")
        {
            if (name == "arrowcursor")
            {
                yield return P("content", "textures", "ArrowCursor.png");
                yield return P("content", "textures", "ArrowCursorDecalDrag.png");
                yield return P("content", "textures", "Cursors", "KeyboardMouse", "ArrowCursor.png");
            }
            else if (name == "arrowfarcursor")
            {
                yield return P("content", "textures", "ArrowFarCursor.png");
                yield return P("content", "textures", "Cursors", "KeyboardMouse", "ArrowFarCursor.png");
            }
            else if (name == "ibeamcursor")
            {
                yield return P("content", "textures", "IBeamCursor.png");
                yield return P("content", "textures", "Cursors", "KeyboardMouse", "IBeamCursor.png");
            }
            else if (name == "mouselockedcursor")
            {
                yield return P("content", "textures", "MouseLockedCursor.png");
                yield return P("content", "textures", "Cursors", "KeyboardMouse", "MouseLockedCursor.png");
            }
        }

        if (name.Contains("mouselock") && relative.Contains(Path.Combine("ui"), StringComparison.OrdinalIgnoreCase))
        {
            yield return P("content", "textures", "ui", "mouseLock_on.png");
            yield return P("content", "textures", "ui", "mouseLock_off.png");
        }

        foreach (var icon in MapMenuBarIcon(name))
            yield return icon;

        if (name is "segmentedcircle" or "wheel")
        {
            foreach (var size in new[] { "Large", "Small", "TenFoot" })
                yield return P("content", "textures", "ui", "Emotes", size, "SegmentedCircle.png");
            yield return P("content", "textures", "ui", "Emotes", "Editor", "Large", "Wheel.png");
            yield return P("content", "textures", "ui", "Emotes", "Editor", "Small", "Wheel.png");
        }

        if (name == "selectedgradient")
        {
            foreach (var size in new[] { "Large", "Small", "TenFoot" })
                yield return P("content", "textures", "ui", "Emotes", size, "SelectedGradient.png");
        }

        if (name == "selectedline")
        {
            foreach (var size in new[] { "Large", "Small", "TenFoot" })
                yield return P("content", "textures", "ui", "Emotes", size, "SelectedLine.png");
        }

        if (name == "circlebackground")
        {
            foreach (var size in new[] { "Large", "Small", "TenFoot" })
                yield return P("content", "textures", "ui", "Emotes", size, "CircleBackground.png");
        }

        if (name is "emotesicon" or "emotesradialicon")
        {
            yield return P("content", "textures", "ui", "Emotes", "EmotesIcon.png");
            yield return P("content", "textures", "ui", "TopBar", "emotesOn.png");
            yield return P("content", "textures", "ui", "TopBar", "emotesOff.png");
        }

        if (name is "newavatarbackground" or "avatarbackground")
        {
            yield return P("content", "textures", "ui", "PlayerList", "NewAvatarBackground.png");
            yield return P("content", "textures", "ui", "PlayerList", "AvatarBackground.png");
        }

        if (relative.Contains(Path.Combine("loading"), StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine("content", "textures", "ui", "LoadingScreen", file);

        if (relative.Contains(Path.Combine("MenuBar"), StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine("content", "textures", "ui", "MenuBar", file);
    }

    private static IEnumerable<string> MapMenuBarIcon(string name)
    {
        var key = name.Replace("_", "");
        if (key is "iconchat")
            return TopBar("chatOn", "chatOff");
        if (key is "iconemote")
            return TopBar("emotesOn", "emotesOff");
        if (key is "iconleaderboard")
            return TopBar("leaderboardOn", "leaderboardOff");
        if (key is "iconbackpack" or "icon_backpack")
            return TopBar("inventoryOn", "inventoryOff");
        if (key is "iconmore" or "iconmenu")
            return TopBar("moreOn", "moreOff");
        if (key is "iconhome")
        {
            return new[]
            {
                P("content", "textures", "ui", "Settings", "MenuBarIcons", "HomeTab.png"),
                P("content", "textures", "ui", "TopBar", "moreOff.png")
            };
        }

        return Array.Empty<string>();
    }

    private static IEnumerable<string> TopBar(params string[] names) =>
        names.Select(name => P("content", "textures", "ui", "TopBar", name + ".png"));

    private static string Canonicalize(string relative)
    {
        var directory = Path.GetDirectoryName(relative) ?? "";
        var file = Path.GetFileName(relative);
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["segmentedcircle.png"] = "SegmentedCircle.png",
            ["selectedgradient.png"] = "SelectedGradient.png",
            ["selectedline.png"] = "SelectedLine.png",
            ["circlebackground.png"] = "CircleBackground.png",
            ["arrowcursor.png"] = "ArrowCursor.png",
            ["arrowfarcursor.png"] = "ArrowFarCursor.png",
            ["mouselockedcursor.png"] = "MouseLockedCursor.png",
            ["newavatarbackground.png"] = "NewAvatarBackground.png"
        };

        if (known.TryGetValue(file, out var canonical))
            file = canonical;

        return string.IsNullOrEmpty(directory) ? file : Path.Combine(directory, file);
    }

    private static IEnumerable<string> MissingScales(string relative)
    {
        if (!relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            yield break;
        if (relative.Contains("@2x", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("@3x", StringComparison.OrdinalIgnoreCase))
            yield break;

        yield return WithScale(relative, "@2x");
        yield return WithScale(relative, "@3x");
    }

    private static string WithScale(string relative, string scale)
    {
        var directory = Path.GetDirectoryName(relative) ?? "";
        var stem = Path.GetFileNameWithoutExtension(relative);
        var extension = Path.GetExtension(relative);
        return Path.Combine(directory, stem + scale + extension);
    }

    private static bool StartsWithAny(string relative, params string[] names) =>
        names.Any(name =>
            relative.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith(name + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    private static string P(params string[] parts) => Path.Combine(parts);
}
