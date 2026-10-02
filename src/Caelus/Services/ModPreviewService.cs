using System.Buffers.Binary;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caelus.Core;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Size = System.Windows.Size;

namespace Caelus.Services;

/// <summary>Thumbnails and helpers for Mods slot previews (images, DDS, fonts).</summary>
public static class ModPreviewService
{
    public static bool IsAudio(ModSlot slot) =>
        slot.Filter.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
        slot.Group.Equals("Sounds", StringComparison.OrdinalIgnoreCase);

    public static bool IsFont(ModSlot slot) =>
        slot.Filter.Contains("Fonts", StringComparison.OrdinalIgnoreCase) ||
        slot.Group.Equals("Fonts", StringComparison.OrdinalIgnoreCase);

    public static bool IsParticle(ModSlot slot) =>
        slot.Id.Equals("particleSquare", StringComparison.OrdinalIgnoreCase) ||
        slot.RelativePath.Contains("particles", StringComparison.OrdinalIgnoreCase);

    public static bool IsVisual(ModSlot slot) => !IsAudio(slot);

    /// <summary>Active mod file, otherwise the stock client file for this slot.</summary>
    public static string? ResolvePath(ModSlot slot)
    {
        if (ModService.HasSlot(slot))
            return ModService.SlotPath(slot);

        var install = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
        if (install is null || string.IsNullOrWhiteSpace(install.VersionDirectory))
            return null;

        foreach (var relative in slot.AllPaths)
        {
            var found = FindUnder(install.VersionDirectory, relative);
            if (found is not null)
                return found;
        }

        return null;
    }

    public static ImageSource? LoadThumb(ModSlot slot, int size = 48)
    {
        try
        {
            if (IsFont(slot))
                return RenderFontThumb(ResolvePath(slot), size);

            var path = ResolvePath(slot);
            return path is null ? null : LoadImage(path, size);
        }
        catch (Exception ex)
        {
            Logger.Write("ModPreview", $"Thumb for {slot.Id}: {ex.Message}");
            return null;
        }
    }

    public static ImageSource? LoadImage(string path, int size)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
            return LoadBitmapFile(path, size);

        if (ext is ".dds" or ".tex")
        {
            var rgba = DecodeDdsRgba(path, size);
            if (rgba is null)
                return Placeholder(size, "DDS");
            return RgbaToBitmap(rgba, size, size);
        }

        return Placeholder(size, "?");
    }

    public static ImageSource Placeholder(int size, string label)
    {
        var visual = new System.Windows.Controls.Border
        {
            Width = size,
            Height = size,
            Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
            Child = new System.Windows.Controls.TextBlock
            {
                Text = label,
                Foreground = Brushes.Gray,
                FontSize = Math.Max(8, size / 5.0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        visual.Measure(new Size(size, size));
        visual.Arrange(new Rect(0, 0, size, size));
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static ImageSource? RenderFontThumb(string? path, int size)
    {
        if (path is null || !File.Exists(path))
            return Placeholder(size, "Aa");

        try
        {
            var family = new System.Windows.Media.FontFamily(new Uri(Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar),
                "./" + Path.GetFileName(path));
            var text = new System.Windows.Controls.TextBlock
            {
                Text = "Aa",
                FontFamily = family,
                FontSize = size * 0.55,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border = new System.Windows.Controls.Border
            {
                Width = size,
                Height = size,
                Background = new SolidColorBrush(Color.FromRgb(32, 36, 44)),
                Child = text
            };
            border.Measure(new Size(size, size));
            border.Arrange(new Rect(0, 0, size, size));
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(border);
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return Placeholder(size, "Aa");
        }
    }

    private static BitmapSource LoadBitmapFile(string path, int size)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = size * 2;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource RgbaToBitmap(byte[] rgba, int width, int height)
    {
        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, RgbaToBgra(rgba), width * 4);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] RgbaToBgra(byte[] rgba)
    {
        var bgra = new byte[rgba.Length];
        for (var i = 0; i + 3 < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i];
            bgra[i + 3] = rgba[i + 3];
        }

        return bgra;
    }

    /// <summary>
    /// Decodes a preview-sized RGBA square from DXT1 / DXT5 / uncompressed / BC4 DDS.
    /// Returns null when the format is unsupported (e.g. BC7).
    /// </summary>
    public static byte[]? DecodeDdsRgba(string path, int size)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 128 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 0x20534444)
                return null;

            var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
            var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16));
            var mips = Math.Max(1, (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(28)));
            var pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(80));
            var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(84));

            // DXT1
            if (fourCc == 0x31545844)
            {
                var dim = width;
                var offset = 128;
                for (var level = 0; level < mips - 1 && dim / 2 >= size; level++)
                {
                    offset += Math.Max(1, dim / 4) * Math.Max(1, dim / 4) * 8;
                    dim /= 2;
                }

                var levelRgba = SkyboxService.DecodeLevel(data.AsSpan(offset), dim);
                return ResizeNearest(levelRgba, dim, dim, size, size);
            }

            // DXT5 / BC3
            if (fourCc == 0x35545844)
            {
                var dim = width;
                var offset = 128;
                for (var level = 0; level < mips - 1 && dim / 2 >= size; level++)
                {
                    offset += Math.Max(1, dim / 4) * Math.Max(1, dim / 4) * 16;
                    dim /= 2;
                }

                var levelRgba = DecodeDxt5Level(data.AsSpan(offset), dim);
                return ResizeNearest(levelRgba, dim, dim, size, size);
            }

            // DX10 header
            if (fourCc == 0x30315844)
            {
                if (data.Length < 148)
                    return null;
                var dxgi = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(128));
                var dataOffset = 148;
                // DXGI_FORMAT_BC4_UNORM = 80, BC4_SNORM = 81
                if (dxgi is 80 or 81)
                {
                    var dim = width;
                    var offset = dataOffset;
                    for (var level = 0; level < mips - 1 && dim / 2 >= size; level++)
                    {
                        offset += Math.Max(1, dim / 4) * Math.Max(1, dim / 4) * 8;
                        dim /= 2;
                    }

                    var levelRgba = DecodeBc4Level(data.AsSpan(offset), dim);
                    return ResizeNearest(levelRgba, dim, dim, size, size);
                }

                return null;
            }

            // Uncompressed RGB(A) — DDPF_RGB = 0x40
            if ((pfFlags & 0x40) != 0)
            {
                var bitCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(88));
                var rMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(92));
                var gMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(96));
                var bMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(100));
                var aMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(104));
                var bpp = (int)(bitCount / 8);
                if (bpp is < 2 or > 4 || width <= 0 || height <= 0)
                    return null;

                var pitch = Math.Max(width * bpp, (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(20)));
                var rgba = new byte[width * height * 4];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var src = 128 + y * pitch + x * bpp;
                    if (src + bpp > data.Length)
                        return null;
                    uint pixel = bpp switch
                    {
                        2 => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(src)),
                        3 => (uint)(data[src] | (data[src + 1] << 8) | (data[src + 2] << 16)),
                        _ => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(src))
                    };
                    var d = (y * width + x) * 4;
                    rgba[d] = MaskByte(pixel, rMask);
                    rgba[d + 1] = MaskByte(pixel, gMask);
                    rgba[d + 2] = MaskByte(pixel, bMask);
                    rgba[d + 3] = aMask == 0 ? (byte)255 : MaskByte(pixel, aMask);
                }

                return ResizeNearest(rgba, width, height, size, size);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Write("ModPreview", $"DDS {path}: {ex.Message}");
            return null;
        }
    }

    private static byte MaskByte(uint pixel, uint mask)
    {
        if (mask == 0)
            return 0;
        var shift = 0;
        var m = mask;
        while ((m & 1) == 0)
        {
            shift++;
            m >>= 1;
        }

        var bits = 0;
        while ((m & 1) != 0)
        {
            bits++;
            m >>= 1;
        }

        var value = (pixel & mask) >> shift;
        return bits >= 8 ? (byte)value : (byte)(value * 255 / ((1 << bits) - 1));
    }

    private static byte[] DecodeDxt5Level(ReadOnlySpan<byte> data, int dim)
    {
        var blocks = Math.Max(1, dim / 4);
        var rgba = new byte[dim * dim * 4];
        Span<byte> alpha = stackalloc byte[8];
        Span<float> palette = stackalloc float[12];
        for (var by = 0; by < blocks; by++)
        for (var bx = 0; bx < blocks; bx++)
        {
            var block = data.Slice((by * blocks + bx) * 16, 16);
            alpha[0] = block[0];
            alpha[1] = block[1];
            if (alpha[0] > alpha[1])
            {
                for (var i = 1; i <= 6; i++)
                    alpha[1 + i] = (byte)(((7 - i) * alpha[0] + i * alpha[1]) / 7);
            }
            else
            {
                for (var i = 1; i <= 4; i++)
                    alpha[1 + i] = (byte)(((5 - i) * alpha[0] + i * alpha[1]) / 5);
                alpha[6] = 0;
                alpha[7] = 255;
            }

            ulong alphaBits = block[2] | ((ulong)block[3] << 8) | ((ulong)block[4] << 16) | ((ulong)block[5] << 24)
                              | ((ulong)block[6] << 32) | ((ulong)block[7] << 40);

            var c0 = BinaryPrimitives.ReadUInt16LittleEndian(block[8..]);
            var c1 = BinaryPrimitives.ReadUInt16LittleEndian(block[10..]);
            From565(c0, palette[..3]);
            From565(c1, palette.Slice(3, 3));
            for (var c = 0; c < 3; c++)
            {
                palette[6 + c] = (2 * palette[c] + palette[3 + c]) / 3;
                palette[9 + c] = (palette[c] + 2 * palette[3 + c]) / 3;
            }

            var indices = BinaryPrimitives.ReadUInt32LittleEndian(block[12..]);
            for (var i = 0; i < 16; i++)
            {
                var x = bx * 4 + i % 4;
                var y = by * 4 + i / 4;
                if (x >= dim || y >= dim)
                    continue;
                var p = (int)((indices >> (i * 2)) & 3);
                var a = (int)((alphaBits >> (i * 3)) & 7);
                var d = (y * dim + x) * 4;
                rgba[d] = (byte)palette[p * 3];
                rgba[d + 1] = (byte)palette[p * 3 + 1];
                rgba[d + 2] = (byte)palette[p * 3 + 2];
                rgba[d + 3] = alpha[a];
            }
        }

        return rgba;
    }

    private static byte[] DecodeBc4Level(ReadOnlySpan<byte> data, int dim)
    {
        var blocks = Math.Max(1, dim / 4);
        var rgba = new byte[dim * dim * 4];
        Span<byte> red = stackalloc byte[8];
        for (var by = 0; by < blocks; by++)
        for (var bx = 0; bx < blocks; bx++)
        {
            var block = data.Slice((by * blocks + bx) * 8, 8);
            red[0] = block[0];
            red[1] = block[1];
            if (red[0] > red[1])
            {
                for (var i = 1; i <= 6; i++)
                    red[1 + i] = (byte)(((7 - i) * red[0] + i * red[1]) / 7);
            }
            else
            {
                for (var i = 1; i <= 4; i++)
                    red[1 + i] = (byte)(((5 - i) * red[0] + i * red[1]) / 5);
                red[6] = 0;
                red[7] = 255;
            }

            ulong bits = block[2] | ((ulong)block[3] << 8) | ((ulong)block[4] << 16) | ((ulong)block[5] << 24)
                         | ((ulong)block[6] << 32) | ((ulong)block[7] << 40);
            for (var i = 0; i < 16; i++)
            {
                var x = bx * 4 + i % 4;
                var y = by * 4 + i / 4;
                if (x >= dim || y >= dim)
                    continue;
                var v = red[(int)((bits >> (i * 3)) & 7)];
                var d = (y * dim + x) * 4;
                rgba[d] = v;
                rgba[d + 1] = v;
                rgba[d + 2] = v;
                rgba[d + 3] = 255;
            }
        }

        return rgba;
    }

    private static void From565(ushort color, Span<float> rgb)
    {
        rgb[0] = ((color >> 11) & 31) * (255f / 31f);
        rgb[1] = ((color >> 5) & 63) * (255f / 63f);
        rgb[2] = (color & 31) * (255f / 31f);
    }

    private static byte[] ResizeNearest(byte[] src, int sw, int sh, int dw, int dh)
    {
        if (sw == dw && sh == dh)
            return src;
        var dest = new byte[dw * dh * 4];
        for (var y = 0; y < dh; y++)
        for (var x = 0; x < dw; x++)
        {
            var s = ((y * sh / dh) * sw + x * sw / dw) * 4;
            Array.Copy(src, s, dest, (y * dw + x) * 4, 4);
        }

        return dest;
    }

    public static string? FindUnder(string root, string relative)
    {
        var direct = Path.Combine(root, relative);
        if (File.Exists(direct))
            return direct;

        var parts = relative.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var part in parts)
        {
            if (!Directory.Exists(current))
                return null;
            var match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => Path.GetFileName(entry).Equals(part, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return null;
            current = match;
        }

        return File.Exists(current) ? current : null;
    }
}
