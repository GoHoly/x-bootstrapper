using System.Buffers.Binary;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caelus.Core;

namespace Caelus.Services;

public sealed record SkyPreset(string Id, string Name, string Blurb);

/// <summary>
/// Sky picker. Octane's 2021 client draws its default sky (and any Sky object left on the default images)
/// from six files: content\textures\sky\sky512_{bk,dn,ft,lf,rt,up}.tex. Each is a DDS texture:
/// DXT1 (BC1), 1024x1024, full mip chain (11 levels), written by NVIDIA Texture Tools (699,192 bytes).
/// Skies are written in exactly that format into the Modifications folder and copied onto the client by the
/// normal mods mechanism (<see cref="ModService"/> + <see cref="ModBackupStore"/>), which backs up the
/// original files first. "Default" deletes the six mod files, so the originals are put back byte for byte.
/// Built-in skies are generated here, procedurally (no third-party images; see docs/skyboxes.md).
/// </summary>
public static class SkyboxService
{
    public const string DefaultId = "default";
    public const string CustomId = "custom";
    public const int FaceSize = 1024;
    public const int MipCount = 11;
    public const int FileLength = 699_192;

    /// <summary>File suffixes, in the order the client names them.</summary>
    public static readonly string[] Faces = { "bk", "dn", "ft", "lf", "rt", "up" };

    public static readonly SkyPreset[] BuiltIn =
    {
        new("sunset", "Sunset", "Low sun, warm horizon, pink clouds."),
        new("night", "Starry night", "Deep blue, a moon and a sky full of stars."),
        new("nebula", "Purple nebula", "Octane-purple space clouds and stars."),
        new("clearday", "Clear day", "Bright blue with a high sun and soft clouds.")
    };

    public static string RelativePath(string face) => Path.Combine("content", "textures", "sky", $"sky512_{face}.tex");

    private static string MarkerPath => Path.Combine(Paths.Modifications, "xb-sky.txt");

    public static bool IsSkyRelative(string relative) =>
        relative.Replace('/', '\\').StartsWith(@"content\textures\sky\", StringComparison.OrdinalIgnoreCase);

    /// <summary>The selected sky id: default, custom, or a built-in id.</summary>
    public static string CurrentId()
    {
        var present = Faces.Count(face => File.Exists(Path.Combine(Paths.Modifications, RelativePath(face))));
        if (present == 0)
            return DefaultId;

        try
        {
            var id = File.Exists(MarkerPath) ? File.ReadAllText(MarkerPath).Trim().ToLowerInvariant() : "";
            if (BuiltIn.Any(preset => preset.Id == id))
                return id;
        }
        catch
        {
            /* unreadable marker: treat as custom */
        }

        return CustomId;
    }

    public static string CurrentName()
    {
        var id = CurrentId();
        return id switch
        {
            DefaultId => "Default",
            CustomId => "Custom",
            _ => BuiltIn.First(preset => preset.Id == id).Name
        };
    }

    /// <summary>Generates a built-in sky and applies it to every known client.</summary>
    public static void ApplyBuiltIn(string id)
    {
        var preset = BuiltIn.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"Unknown sky \"{id}\".");
        var faces = new Dictionary<string, byte[]>();
        foreach (var face in Faces)
            faces[face] = Render(preset.Id, face, FaceSize);
        Write(faces, preset.Id);
        Logger.Write("Sky", $"Applied the built-in sky \"{preset.Name}\"");
    }

    /// <summary>
    /// Loads and scales six images (any size; PNG, JPG, BMP, GIF, TIFF) to 1024x1024. Uses WPF imaging, so
    /// call it on the UI thread; pass the result to <see cref="ApplyCustom"/> (which can run in the background).
    /// </summary>
    public static Dictionary<string, byte[]> LoadCustom(IReadOnlyDictionary<string, string> images)
    {
        var missing = Faces.Where(face => !images.ContainsKey(face)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException("Missing images for: " + string.Join(", ", missing));

        var faces = new Dictionary<string, byte[]>();
        foreach (var face in Faces)
            faces[face] = LoadImage(images[face], FaceSize);
        return faces;
    }

    /// <summary>Encodes the loaded faces to the client's format and applies them.</summary>
    public static void ApplyCustom(Dictionary<string, byte[]> faces, string description)
    {
        Write(faces, CustomId);
        Logger.Write("Sky", "Applied a custom sky from " + description);
    }

    /// <summary>Removes the sky mod files; the mods mechanism then restores the client's own sky.</summary>
    public static void RestoreDefault()
    {
        foreach (var face in Faces)
        {
            var path = Path.Combine(Paths.Modifications, RelativePath(face));
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        if (File.Exists(MarkerPath))
            File.Delete(MarkerPath);
        ModService.ApplyToInstalledClients();
        Logger.Write("Sky", "Restored the default sky");
    }

    private static void Write(Dictionary<string, byte[]> rgbaFaces, string id)
    {
        foreach (var (face, rgba) in rgbaFaces)
        {
            var path = Path.Combine(Paths.Modifications, RelativePath(face));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, EncodeDds(rgba, FaceSize));
            File.Move(temp, path, overwrite: true);
        }

        File.WriteAllText(MarkerPath, id);
        ModService.ApplyToInstalledClients();
    }

    /// <summary>
    /// Matches picked files to faces by name: _bk/_back, _dn/_down/_bottom, _ft/_front, _lf/_left,
    /// _rt/_right, _up/_top (Roblox Sky naming). Returns the faces it could not place.
    /// </summary>
    public static Dictionary<string, string> MatchFaces(IEnumerable<string> files, out List<string> unmatched)
    {
        var aliases = new Dictionary<string, string[]>
        {
            ["bk"] = new[] { "bk", "back", "b" },
            ["dn"] = new[] { "dn", "down", "bottom", "bot" },
            ["ft"] = new[] { "ft", "front", "f" },
            ["lf"] = new[] { "lf", "left", "l" },
            ["rt"] = new[] { "rt", "right", "r" },
            ["up"] = new[] { "up", "top", "u" }
        };

        var result = new Dictionary<string, string>();
        unmatched = new List<string>();
        foreach (var file in files)
        {
            var tokens = Path.GetFileNameWithoutExtension(file).ToLowerInvariant()
                .Split(new[] { '_', '-', ' ', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            var face = tokens.Reverse()
                .Select(token => aliases.FirstOrDefault(pair => pair.Value.Contains(token)).Key)
                .FirstOrDefault(key => key is not null);
            if (face is null || result.ContainsKey(face))
                unmatched.Add(file);
            else
                result[face] = file;
        }

        return result;
    }

    // ---------------------------------------------------------------- previews

    /// <summary>A strip of the four side faces (rt, ft, lf, bk: left to right, seamless) for the picker tiles.</summary>
    public static BitmapSource? Preview(string id, int faceSize)
    {
        var sides = new[] { "rt", "ft", "lf", "bk" };
        var faces = new List<byte[]>();
        foreach (var face in sides)
        {
            byte[]? rgba = id switch
            {
                DefaultId => DecodeFile(OriginalClientFile(face), faceSize),
                CustomId => DecodeFile(Path.Combine(Paths.Modifications, RelativePath(face)), faceSize),
                _ => Render(id, face, faceSize)
            };
            if (rgba is null)
                return null;
            faces.Add(rgba);
        }

        var width = faceSize * sides.Length;
        var strip = new byte[width * faceSize * 4];
        for (var i = 0; i < faces.Count; i++)
        for (var y = 0; y < faceSize; y++)
        for (var x = 0; x < faceSize; x++)
        {
            var src = (y * faceSize + x) * 4;
            var dst = (y * width + i * faceSize + x) * 4;
            // RGBA -> BGRA for WPF.
            strip[dst] = faces[i][src + 2];
            strip[dst + 1] = faces[i][src + 1];
            strip[dst + 2] = faces[i][src];
            strip[dst + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, faceSize, 96, 96, PixelFormats.Bgra32, null, strip, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>The client's own file for a face: the backed-up original if a sky mod replaced it.</summary>
    private static string? OriginalClientFile(string face)
    {
        var install = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
        if (install is null || string.IsNullOrWhiteSpace(install.VersionDirectory))
            return null;

        return ModBackupStore.Open(install.VersionDirectory).OriginalFile(RelativePath(face));
    }

    // ---------------------------------------------------------------- procedural skies

    /// <summary>Direction for pixel (u right, v down, both -1..1) of a face. Derived from the stock sky's seams.</summary>
    public static Vector3 Direction(string face, float u, float v) => face switch
    {
        "ft" => new Vector3(u, -v, 1),
        "lf" => new Vector3(1, -v, -u),
        "bk" => new Vector3(-u, -v, -1),
        "rt" => new Vector3(-1, -v, u),
        "up" => new Vector3(-v, 1, u),
        "dn" => new Vector3(v, -1, u),
        _ => throw new ArgumentException(face)
    };

    /// <summary>Renders one face of a built-in sky as RGBA.</summary>
    public static byte[] Render(string id, string face, int size)
    {
        var rgba = new byte[size * size * 4];
        Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size * 2 - 1;
                var v = (y + 0.5f) / size * 2 - 1;
                var d = Vector3.Normalize(Direction(face, u, v));
                var c = Shade(id, d);
                // A little noise hides 5:6:5 banding in the smooth gradients.
                var dither = (Hash(x, y, face[0] * 31 + face[1]) - 0.5f) * 1.5f / 255f;
                var i = (y * size + x) * 4;
                rgba[i] = ToByte(c.X + dither);
                rgba[i + 1] = ToByte(c.Y + dither);
                rgba[i + 2] = ToByte(c.Z + dither);
                rgba[i + 3] = 255;
            }
        });
        return rgba;
    }

    private static Vector3 Shade(string id, Vector3 d) => id switch
    {
        "sunset" => Sunset(d),
        "night" => Night(d),
        "nebula" => Nebula(d),
        "clearday" => ClearDay(d),
        _ => Vector3.Zero
    };

    private static Vector3 Gradient(float e, Vector3 zenith, Vector3 upper, Vector3 horizon, Vector3 below, Vector3 bottom)
    {
        if (e >= 0)
        {
            var t = MathF.Pow(e, 0.5f);
            return t < 0.45f ? Vector3.Lerp(horizon, upper, t / 0.45f) : Vector3.Lerp(upper, zenith, (t - 0.45f) / 0.55f);
        }

        var b = -e;
        return b < 0.12f ? Vector3.Lerp(horizon, below, Smooth(0, 0.12f, b)) : Vector3.Lerp(below, bottom, Smooth(0.12f, 1f, b));
    }

    private static readonly Vector3 SunsetSun = Vector3.Normalize(new Vector3(0, 0.07f, 1));

    private static Vector3 Sunset(Vector3 d)
    {
        var c = Gradient(d.Y,
            new Vector3(0.10f, 0.13f, 0.33f), new Vector3(0.42f, 0.30f, 0.56f), new Vector3(1.00f, 0.56f, 0.30f),
            new Vector3(0.30f, 0.15f, 0.18f), new Vector3(0.07f, 0.05f, 0.09f));
        var s = MathF.Max(0, Vector3.Dot(d, SunsetSun));
        c += new Vector3(1.0f, 0.50f, 0.22f) * (MathF.Pow(s, 10) * 0.55f) + new Vector3(1, 0.8f, 0.6f) * (MathF.Pow(s, 180) * 0.5f);
        var clouds = Clouds(d, 1.4f, 0.50f, 0.78f, 7);
        if (clouds > 0)
        {
            var lit = Vector3.Lerp(new Vector3(0.40f, 0.27f, 0.47f), new Vector3(1.0f, 0.62f, 0.46f), 0.5f + 0.5f * Vector3.Dot(d, SunsetSun));
            c = Vector3.Lerp(c, lit, clouds * 0.85f);
        }

        c = Vector3.Lerp(c, new Vector3(1.0f, 0.97f, 0.86f), Smooth(0.99950f, 0.99972f, s));
        return c;
    }

    private static readonly Vector3 Moon = Vector3.Normalize(new Vector3(0.35f, 0.5f, 0.8f));
    private static readonly Vector3 GalaxyNormal = Vector3.Normalize(new Vector3(0.6f, 0.3f, -0.74f));

    private static Vector3 Night(Vector3 d)
    {
        var c = Gradient(d.Y,
            new Vector3(0.010f, 0.014f, 0.042f), new Vector3(0.025f, 0.037f, 0.090f), new Vector3(0.060f, 0.085f, 0.170f),
            new Vector3(0.030f, 0.035f, 0.065f), new Vector3(0.012f, 0.014f, 0.025f));
        var above = Smooth(-0.04f, 0.06f, d.Y);
        var band = MathF.Exp(-MathF.Pow(Vector3.Dot(d, GalaxyNormal), 2) * 28);
        c += new Vector3(0.30f, 0.33f, 0.50f) * (band * Fbm(d * 5f, 4, 3) * 0.20f * above);
        c += Stars(d, 190, 0.982f) * above;
        var m = Vector3.Dot(d, Moon);
        c += new Vector3(0.55f, 0.60f, 0.75f) * (MathF.Pow(MathF.Max(0, m), 260) * 0.35f);
        var disc = Smooth(0.99968f, 0.99976f, m);
        if (disc > 0)
            c = Vector3.Lerp(c, new Vector3(0.93f, 0.93f, 0.88f) * (0.85f + 0.15f * Fbm(d * 180, 3, 9)), disc);
        return c;
    }

    private static Vector3 Nebula(Vector3 d)
    {
        var c = new Vector3(0.024f, 0.010f, 0.050f);
        var n1 = Fbm(d * 2.2f, 5, 21);
        var n2 = Fbm(d * 4.6f + new Vector3(3.1f, 1.7f, 8.2f), 4, 5);
        var cloud = Smooth(0.42f, 0.82f, n1);
        c += new Vector3(0.62f, 0.27f, 0.95f) * (cloud * 0.85f);
        c += new Vector3(0.95f, 0.28f, 0.68f) * (Smooth(0.55f, 0.90f, n2) * cloud * 0.55f);
        c += new Vector3(0.18f, 0.30f, 0.85f) * (Smooth(0.50f, 0.85f, 1 - n1) * 0.12f);
        c = c * (0.75f + 0.25f * Fbm(d * 11f, 3, 17));
        c += Stars(d, 210, 0.975f);
        return c;
    }

    private static readonly Vector3 DaySun = Vector3.Normalize(new Vector3(0.35f, 0.75f, 0.55f));

    private static Vector3 ClearDay(Vector3 d)
    {
        var c = Gradient(d.Y,
            new Vector3(0.15f, 0.40f, 0.85f), new Vector3(0.38f, 0.62f, 0.93f), new Vector3(0.74f, 0.87f, 0.98f),
            new Vector3(0.62f, 0.70f, 0.78f), new Vector3(0.45f, 0.52f, 0.60f));
        var s = MathF.Max(0, Vector3.Dot(d, DaySun));
        c += new Vector3(1.0f, 0.95f, 0.80f) * (MathF.Pow(s, 12) * 0.25f + MathF.Pow(s, 400) * 0.6f);
        var clouds = Clouds(d, 1.1f, 0.52f, 0.74f, 13);
        if (clouds > 0)
        {
            var shade = 0.78f + 0.22f * Fbm(d * 9f, 3, 2);
            c = Vector3.Lerp(c, new Vector3(0.97f, 0.98f, 1.0f) * shade, clouds * 0.9f);
        }

        c = Vector3.Lerp(c, new Vector3(1, 1, 0.97f), Smooth(0.99955f, 0.99975f, s));
        return c;
    }

    /// <summary>A cloud layer: noise on a plane above the camera, fading out toward the horizon.</summary>
    private static float Clouds(Vector3 d, float scale, float from, float to, int seed)
    {
        if (d.Y <= 0.01f)
            return 0;

        var k = scale / (d.Y + 0.10f);
        var p = new Vector3(d.X * k, seed * 7.1f, d.Z * k);
        var n = Fbm(p, 5, seed);
        return Smooth(from, to, n) * Smooth(0.02f, 0.28f, d.Y);
    }

    private static Vector3 Stars(Vector3 d, float density, float threshold)
    {
        var p = d * density;
        var cell = new Vector3(MathF.Floor(p.X), MathF.Floor(p.Y), MathF.Floor(p.Z));
        var (cx, cy, cz) = ((int)cell.X, (int)cell.Y, (int)cell.Z);
        var h = Hash(cx, cy, cz);
        if (h < threshold)
            return Vector3.Zero;

        var center = cell + new Vector3(0.2f + 0.6f * Hash(cx, cy, cz + 101), 0.2f + 0.6f * Hash(cx + 57, cy, cz), 0.2f + 0.6f * Hash(cx, cy + 13, cz));
        var dist2 = Vector3.DistanceSquared(p, center);
        var brightness = (h - threshold) / (1 - threshold);
        var glow = MathF.Exp(-dist2 * 14f) * (0.35f + 0.9f * brightness);
        var tint = Hash(cx + 3, cy + 7, cz + 11);
        var color = tint < 0.3f ? new Vector3(0.75f, 0.82f, 1f) : tint > 0.85f ? new Vector3(1f, 0.85f, 0.7f) : Vector3.One;
        return color * glow;
    }

    private static float Fbm(Vector3 p, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (var i = 0; i < octaves; i++)
        {
            sum += amp * ValueNoise(p.X, p.Y, p.Z, seed + i * 17);
            norm += amp;
            p *= 2.03f;
            amp *= 0.5f;
        }

        return sum / norm;
    }

    private static float ValueNoise(float x, float y, float z, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        float xf = x - xi, yf = y - yi, zf = z - zi;
        float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf), w = zf * zf * (3 - 2 * zf);
        float Corner(int a, int b, int c) => Hash(xi + a, yi + b, zi + c + seed * 1013);
        var x00 = Lerp(Corner(0, 0, 0), Corner(1, 0, 0), u);
        var x10 = Lerp(Corner(0, 1, 0), Corner(1, 1, 0), u);
        var x01 = Lerp(Corner(0, 0, 1), Corner(1, 0, 1), u);
        var x11 = Lerp(Corner(0, 1, 1), Corner(1, 1, 1), u);
        return Lerp(Lerp(x00, x10, v), Lerp(x01, x11, v), w);
    }

    private static float Hash(int x, int y, int z)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smooth(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);

    // ---------------------------------------------------------------- image loading

    /// <summary>Loads any image WPF can decode and scales it to size x size (high quality), as RGBA.</summary>
    public static byte[] LoadImage(string path, int size)
    {
        BitmapSource source;
        using (var stream = File.OpenRead(path))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            source = decoder.Frames[0];
        }

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
            dc.DrawImage(source, new System.Windows.Rect(0, 0, size, size));
        var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var bgra = new byte[size * size * 4];
        target.CopyPixels(bgra, size * 4, 0);
        for (var i = 0; i < bgra.Length; i += 4)
        {
            (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
            bgra[i + 3] = 255;
        }

        return bgra;
    }

    // ---------------------------------------------------------------- DDS / DXT1

    /// <summary>The same 128-byte header the stock sky512_*.tex files carry (NVTT 2.1.0, DXT1, mipmapped).</summary>
    public static byte[] Header(int size, int mips)
    {
        var h = new byte[128];
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(offset), value);
        U32(0, 0x20534444);             // "DDS "
        U32(4, 124);
        U32(8, 0x000A1007);             // CAPS | HEIGHT | WIDTH | PIXELFORMAT | MIPMAPCOUNT | LINEARSIZE
        U32(12, (uint)size);
        U32(16, (uint)size);
        U32(20, (uint)(size * size / 2));
        U32(28, (uint)mips);
        U32(60, 0x52455655);            // "UVER"
        U32(68, 0x5454564E);            // "NVTT"
        U32(72, 0x00020100);            // 2.1.0
        U32(76, 32);
        U32(80, 0x4);                   // FOURCC
        U32(84, 0x31545844);            // "DXT1"
        U32(108, 0x00401008);           // COMPLEX | TEXTURE | MIPMAP
        return h;
    }

    public static byte[] EncodeDds(byte[] rgba, int size)
    {
        using var output = new MemoryStream(FileLength);
        output.Write(Header(size, MipCount));
        var level = rgba;
        var dim = size;
        for (var mip = 0; mip < MipCount; mip++)
        {
            output.Write(EncodeLevel(level, dim));
            if (dim > 1)
            {
                level = Downsample(level, dim);
                dim /= 2;
            }
        }

        return output.ToArray();
    }

    private static byte[] Downsample(byte[] rgba, int dim)
    {
        var half = dim / 2;
        var result = new byte[half * half * 4];
        for (var y = 0; y < half; y++)
        for (var x = 0; x < half; x++)
        for (var c = 0; c < 4; c++)
        {
            var sum = rgba[((2 * y) * dim + 2 * x) * 4 + c] + rgba[((2 * y) * dim + 2 * x + 1) * 4 + c] +
                      rgba[((2 * y + 1) * dim + 2 * x) * 4 + c] + rgba[((2 * y + 1) * dim + 2 * x + 1) * 4 + c];
            result[(y * half + x) * 4 + c] = (byte)((sum + 2) / 4);
        }

        return result;
    }

    private static byte[] EncodeLevel(byte[] rgba, int dim)
    {
        var blocks = Math.Max(1, dim / 4);
        var output = new byte[blocks * blocks * 8];
        Parallel.For(0, blocks, by =>
        {
            Span<float> px = stackalloc float[48];
            for (var bx = 0; bx < blocks; bx++)
            {
                for (var i = 0; i < 16; i++)
                {
                    // Levels smaller than a block repeat their pixels.
                    var x = Math.Min(bx * 4 + i % 4, dim - 1);
                    var y = Math.Min(by * 4 + i / 4, dim - 1);
                    var s = (y * dim + x) * 4;
                    px[i * 3] = rgba[s];
                    px[i * 3 + 1] = rgba[s + 1];
                    px[i * 3 + 2] = rgba[s + 2];
                }

                EncodeBlock(px, output.AsSpan((by * blocks + bx) * 8, 8));
            }
        });
        return output;
    }

    private static void EncodeBlock(ReadOnlySpan<float> px, Span<byte> block)
    {
        // Principal axis of the 16 colors (power iteration on the covariance), then the extremes along it.
        float mr = 0, mg = 0, mb = 0;
        for (var i = 0; i < 16; i++)
        {
            mr += px[i * 3];
            mg += px[i * 3 + 1];
            mb += px[i * 3 + 2];
        }

        mr /= 16; mg /= 16; mb /= 16;
        float rr = 0, rg = 0, rb = 0, gg = 0, gb = 0, bb = 0;
        for (var i = 0; i < 16; i++)
        {
            float r = px[i * 3] - mr, g = px[i * 3 + 1] - mg, b = px[i * 3 + 2] - mb;
            rr += r * r; rg += r * g; rb += r * b; gg += g * g; gb += g * b; bb += b * b;
        }

        float ar = 1, ag = 1, ab = 1;
        for (var it = 0; it < 6; it++)
        {
            var nr = rr * ar + rg * ag + rb * ab;
            var ng = rg * ar + gg * ag + gb * ab;
            var nb = rb * ar + gb * ag + bb * ab;
            var len = MathF.Sqrt(nr * nr + ng * ng + nb * nb);
            if (len < 1e-6f)
                break;
            ar = nr / len; ag = ng / len; ab = nb / len;
        }

        float min = float.MaxValue, max = float.MinValue;
        for (var i = 0; i < 16; i++)
        {
            var t = (px[i * 3] - mr) * ar + (px[i * 3 + 1] - mg) * ag + (px[i * 3 + 2] - mb) * ab;
            min = MathF.Min(min, t);
            max = MathF.Max(max, t);
        }

        var c0 = To565(mr + ar * max, mg + ag * max, mb + ab * max);
        var c1 = To565(mr + ar * min, mg + ag * min, mb + ab * min);
        if (c0 < c1)
            (c0, c1) = (c1, c0);

        BinaryPrimitives.WriteUInt16LittleEndian(block, c0);
        BinaryPrimitives.WriteUInt16LittleEndian(block[2..], c1);
        if (c0 == c1)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], 0);
            return;
        }

        Span<float> palette = stackalloc float[12];
        From565(c0, palette[..3]);
        From565(c1, palette.Slice(3, 3));
        for (var c = 0; c < 3; c++)
        {
            palette[6 + c] = (2 * palette[c] + palette[3 + c]) / 3;
            palette[9 + c] = (palette[c] + 2 * palette[3 + c]) / 3;
        }

        uint indices = 0;
        for (var i = 0; i < 16; i++)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var p = 0; p < 4; p++)
            {
                float dr = px[i * 3] - palette[p * 3], dg = px[i * 3 + 1] - palette[p * 3 + 1], db = px[i * 3 + 2] - palette[p * 3 + 2];
                var distance = dr * dr * 0.3f + dg * dg * 0.59f + db * db * 0.11f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = p;
                }
            }

            indices |= (uint)best << (i * 2);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block[4..], indices);
    }

    private static ushort To565(float r, float g, float b)
    {
        var r5 = Math.Clamp((int)MathF.Round(r * 31 / 255f), 0, 31);
        var g6 = Math.Clamp((int)MathF.Round(g * 63 / 255f), 0, 63);
        var b5 = Math.Clamp((int)MathF.Round(b * 31 / 255f), 0, 31);
        return (ushort)((r5 << 11) | (g6 << 5) | b5);
    }

    private static void From565(ushort c, Span<float> rgb)
    {
        var r = (c >> 11) & 31;
        var g = (c >> 5) & 63;
        var b = c & 31;
        rgb[0] = (r << 3) | (r >> 2);
        rgb[1] = (g << 2) | (g >> 4);
        rgb[2] = (b << 3) | (b >> 2);
    }

    /// <summary>
    /// Decodes a DXT1 .tex/.dds at the mip level closest to <paramref name="size"/> and returns size x size RGBA,
    /// or null when the file is missing or not a DXT1 DDS.
    /// </summary>
    public static byte[]? DecodeFile(string? path, int size)
    {
        try
        {
            if (path is null || !File.Exists(path))
                return null;
            var data = File.ReadAllBytes(path);
            if (data.Length < 128 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 0x20534444 ||
                BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(84)) != 0x31545844)
                return null;

            var dim = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16));
            var mips = Math.Max(1, (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(28)));
            var offset = 128;
            for (var level = 0; level < mips - 1 && dim / 2 >= size; level++)
            {
                offset += Math.Max(1, dim / 4) * Math.Max(1, dim / 4) * 8;
                dim /= 2;
            }

            var level0 = DecodeLevel(data.AsSpan(offset), dim);
            if (dim == size)
                return level0;

            // Nearest-neighbour to the requested size (previews only).
            var result = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var s = ((y * dim / size) * dim + x * dim / size) * 4;
                Array.Copy(level0, s, result, (y * size + x) * 4, 4);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Write("Sky", $"Could not read {path}: {ex.Message}");
            return null;
        }
    }

    public static byte[] DecodeLevel(ReadOnlySpan<byte> data, int dim)
    {
        var blocks = Math.Max(1, dim / 4);
        var rgba = new byte[dim * dim * 4];
        Span<float> palette = stackalloc float[12];
        for (var by = 0; by < blocks; by++)
        for (var bx = 0; bx < blocks; bx++)
        {
            var block = data.Slice((by * blocks + bx) * 8, 8);
            var c0 = BinaryPrimitives.ReadUInt16LittleEndian(block);
            var c1 = BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);
            From565(c0, palette[..3]);
            From565(c1, palette.Slice(3, 3));
            for (var c = 0; c < 3; c++)
            {
                if (c0 > c1)
                {
                    palette[6 + c] = (2 * palette[c] + palette[3 + c]) / 3;
                    palette[9 + c] = (palette[c] + 2 * palette[3 + c]) / 3;
                }
                else
                {
                    palette[6 + c] = (palette[c] + palette[3 + c]) / 2;
                    palette[9 + c] = 0;
                }
            }

            var indices = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);
            for (var i = 0; i < 16; i++)
            {
                var x = bx * 4 + i % 4;
                var y = by * 4 + i / 4;
                if (x >= dim || y >= dim)
                    continue;
                var p = (int)((indices >> (i * 2)) & 3);
                var d = (y * dim + x) * 4;
                rgba[d] = (byte)palette[p * 3];
                rgba[d + 1] = (byte)palette[p * 3 + 1];
                rgba[d + 2] = (byte)palette[p * 3 + 2];
                rgba[d + 3] = 255;
            }
        }

        return rgba;
    }
}
