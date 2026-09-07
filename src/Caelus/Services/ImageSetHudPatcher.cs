using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// 2021 TopBar chat / Tab / emote icons are UIBlox ImageSet sprites, not the loose
/// TopBar PNGs. Paint custom icons into the atlas sheets the HUD actually loads.
/// </summary>
internal static class ImageSetHudPatcher
{
    private static readonly Regex SpritePattern = new(
        @"\['(?<key>[^']+)'\] = \{ ImageRectOffset = Vector2\.new\((?<x>\d+), (?<y>\d+)\), ImageRectSize = Vector2\.new\((?<w>\d+), (?<h>\d+)\), ImageSet = '(?<set>img_set_[^']+)' \}",
        RegexOptions.Compiled);

    private static readonly string[] ChatOn =
    {
        P("content", "textures", "ui", "MenuBar", "icon_chat.png"),
        P("content", "textures", "ui", "TopBar", "chatOn.png"),
        P("content", "textures", "ui", "Chat", "Chat.png"),
        P("content", "textures", "ui", "Settings", "Radial", "Chat.png")
    };

    private static readonly string[] ChatOff =
    {
        P("content", "textures", "ui", "MenuBar", "icon_chat.png"),
        P("content", "textures", "ui", "TopBar", "chatOff.png"),
        P("content", "textures", "ui", "TopBar", "chatOn.png")
    };

    private static readonly string[] TabOn =
    {
        P("content", "textures", "ui", "MenuBar", "icon_leaderboard.png"),
        P("content", "textures", "ui", "TopBar", "leaderboardOn.png"),
        P("content", "textures", "ui", "Settings", "Radial", "PlayerList.png"),
        P("content", "textures", "ui", "Settings", "MenuBarIcons", "PlayersTabIcon.png")
    };

    private static readonly string[] TabOff =
    {
        P("content", "textures", "ui", "MenuBar", "icon_leaderboard.png"),
        P("content", "textures", "ui", "TopBar", "leaderboardOff.png"),
        P("content", "textures", "ui", "TopBar", "leaderboardOn.png")
    };

    private static readonly string[] EmoteOn =
    {
        P("content", "textures", "ui", "MenuBar", "icon_emote.png"),
        P("content", "textures", "ui", "TopBar", "emotesOn.png"),
        P("content", "textures", "ui", "Emotes", "EmotesIcon.png")
    };

    private static readonly string[] EmoteOff =
    {
        P("content", "textures", "ui", "MenuBar", "icon_emote.png"),
        P("content", "textures", "ui", "TopBar", "emotesOff.png"),
        P("content", "textures", "ui", "TopBar", "emotesOn.png")
    };

    private static readonly string[] BackpackOn =
    {
        P("content", "textures", "ui", "MenuBar", "icon__backpack.png"),
        P("content", "textures", "ui", "TopBar", "inventoryOn.png")
    };

    private static readonly string[] BackpackOff =
    {
        P("content", "textures", "ui", "MenuBar", "icon__backpack.png"),
        P("content", "textures", "ui", "TopBar", "inventoryOff.png"),
        P("content", "textures", "ui", "TopBar", "inventoryOn.png")
    };

    private static readonly string[] MoreOn =
    {
        P("content", "textures", "ui", "MenuBar", "icon_more.png"),
        P("content", "textures", "ui", "TopBar", "moreOn.png")
    };

    private static readonly string[] MoreOff =
    {
        P("content", "textures", "ui", "MenuBar", "icon_more.png"),
        P("content", "textures", "ui", "TopBar", "moreOff.png"),
        P("content", "textures", "ui", "TopBar", "moreOn.png")
    };

    private static readonly (string Key, string[] Sources)[] Maps =
    {
        ("icons/menu/chat_on", ChatOn),
        ("icons/menu/chat_off", ChatOff),
        ("icons/controls/leaderboardOn", TabOn),
        ("icons/controls/leaderboardOff", TabOff),
        ("icons/controls/players", TabOn),
        ("icons/controls/emoteOn", EmoteOn),
        ("icons/controls/emoteOff", EmoteOff),
        ("icons/menu/inventoryOn", BackpackOn),
        ("icons/menu/inventoryOff", BackpackOff),
        ("icons/menu/inventory", BackpackOn),
        ("icons/menu/more_on", MoreOn),
        ("icons/menu/more_off", MoreOff)
    };

    public static int Apply(string versionDirectory)
    {
        var imageSet = Path.Combine(versionDirectory, "ExtraContent", "LuaPackages", "Packages",
            "_Index", "UIBlox", "UIBlox", "App", "ImageSet");
        var dataLua = Path.Combine(imageSet, "GetImageSetData.lua");
        var atlasDir = Path.Combine(imageSet, "ImageAtlas");
        if (!File.Exists(dataLua) || !Directory.Exists(atlasDir))
            return 0;

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, candidates) in Maps)
        {
            var file = FirstMod(candidates);
            if (file is not null)
                sources[key] = file;
        }

        var origDir = Path.Combine(atlasDir, ".caelus-orig");
        EnsureOriginals(atlasDir, origDir);

        if (sources.Count == 0)
        {
            RestoreOriginals(atlasDir, origDir);
            return 0;
        }

        var stamp = Stamp(sources);
        var stampPath = Path.Combine(atlasDir, ".caelus-hud-stamp");
        var sample = Path.Combine(atlasDir, "img_set_1x_2.png");
        var sampleOrig = Path.Combine(origDir, "img_set_1x_2.png");
        var atlasIsStock = File.Exists(sample) && File.Exists(sampleOrig) &&
                           new FileInfo(sample).Length == new FileInfo(sampleOrig).Length &&
                           FilesEqual(sample, sampleOrig);

        if (!atlasIsStock && File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp)
            return 0;

        RestoreOriginals(atlasDir, origDir);

        var lua = File.ReadAllText(dataLua);
        var jobs = new Dictionary<string, List<Sprite>>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SpritePattern.Matches(lua))
        {
            var key = match.Groups["key"].Value;
            if (!sources.TryGetValue(key, out var source))
                continue;

            var set = match.Groups["set"].Value + ".png";
            if (!jobs.TryGetValue(set, out var list))
            {
                list = new List<Sprite>();
                jobs[set] = list;
            }

            list.Add(new Sprite(
                source,
                int.Parse(match.Groups["x"].Value),
                int.Parse(match.Groups["y"].Value),
                int.Parse(match.Groups["w"].Value),
                int.Parse(match.Groups["h"].Value)));
        }

        var painted = 0;
        foreach (var (fileName, sprites) in jobs)
        {
            var dest = Path.Combine(atlasDir, fileName);
            var orig = Path.Combine(origDir, fileName);
            if (!File.Exists(orig))
                continue;

            if (Paint(orig, dest, sprites))
                painted += sprites.Count;
        }

        try
        {
            File.WriteAllText(stampPath, stamp);
        }
        catch (IOException)
        {
            /* ignore */
        }

        if (painted > 0)
            Logger.Write("Mods", $"Painted {painted} HUD icon(s) into the ImageSet atlas.");

        return painted;
    }

    private static bool Paint(string original, string destination, List<Sprite> sprites)
    {
        var temp = destination + ".caelus-tmp";
        try
        {
            using var stock = new Bitmap(original);
            using var canvas = new Bitmap(stock.Width, stock.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImage(stock, 0, 0, stock.Width, stock.Height);
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                foreach (var sprite in sprites)
                {
                    using var raw = new Bitmap(sprite.Source);
                    using var icon = WithTransparentBackdrop(raw);
                    var slot = new Rectangle(sprite.X, sprite.Y, sprite.W, sprite.H);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    using (var clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                        graphics.FillRectangle(clear, slot);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    graphics.DrawImage(icon, slot);
                }
            }

            canvas.Save(temp, ImageFormat.Png);
            Replace(temp, destination);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Logger.Write("Mods", $"Could not paint {Path.GetFileName(destination)}: {ex.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* ignore */ }
        }
    }

    private static Bitmap WithTransparentBackdrop(Bitmap source)
    {
        var copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (pixel.A > 0 && pixel.R < 22 && pixel.G < 22 && pixel.B < 22)
                    copy.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                else
                    copy.SetPixel(x, y, pixel);
            }
        }

        return copy;
    }

    private static void EnsureOriginals(string atlasDir, string origDir)
    {
        Directory.CreateDirectory(origDir);
        foreach (var file in Directory.GetFiles(atlasDir, "img_set_*.png"))
        {
            var backup = Path.Combine(origDir, Path.GetFileName(file));
            if (!File.Exists(backup))
                File.Copy(file, backup);
        }
    }

    private static void RestoreOriginals(string atlasDir, string origDir)
    {
        if (!Directory.Exists(origDir))
            return;

        foreach (var backup in Directory.GetFiles(origDir, "img_set_*.png"))
        {
            var dest = Path.Combine(atlasDir, Path.GetFileName(backup));
            try
            {
                Replace(backup, dest);
            }
            catch (IOException)
            {
                /* game may have the sheet open */
            }
        }
    }

    private static string? FirstMod(string[] relatives)
    {
        if (relatives is null || relatives.Length == 0)
            return null;

        foreach (var relative in relatives)
        {
            var path = Path.Combine(Paths.Modifications, relative);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string Stamp(Dictionary<string, string> sources)
    {
        var parts = sources
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key + "=" + new FileInfo(kv.Value).Length + ":" + new FileInfo(kv.Value).LastWriteTimeUtc.Ticks);
        return "v2|" + string.Join("|", parts);
    }

    private static bool FilesEqual(string a, string b)
    {
        using var left = File.OpenRead(a);
        using var right = File.OpenRead(b);
        if (left.Length != right.Length)
            return false;

        var bufferA = new byte[8192];
        var bufferB = new byte[8192];
        while (true)
        {
            var readA = left.Read(bufferA, 0, bufferA.Length);
            var readB = right.Read(bufferB, 0, bufferB.Length);
            if (readA != readB)
                return false;
            if (readA == 0)
                return true;
            if (!bufferA.AsSpan(0, readA).SequenceEqual(bufferB.AsSpan(0, readB)))
                return false;
        }
    }

    private static void Replace(string source, string destination)
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

    private static string P(params string[] parts) => Path.Combine(parts);

    private readonly record struct Sprite(string Source, int X, int Y, int W, int H);
}
