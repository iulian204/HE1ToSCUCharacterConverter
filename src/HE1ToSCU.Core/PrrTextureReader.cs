using System.Buffers.Binary;
using System.Text;

namespace HE1ToSCU.Core;

/// <summary>Raw HE1 DDS channels/mips; cube faces follow the supplied PRR 4x3 environment-atlas contract.</summary>
public sealed record PrrTextureData(DdsImage Image, byte[] MipData, int SourceMaxLod);

public static class PrrTextureReader
{
    public static PrrTextureData Read(string path, bool cubeAtlas, CancellationToken token) =>
        Decode(File.ReadAllBytes(path), cubeAtlas, token);

    public static PrrTextureData Decode(byte[] data, bool cubeAtlas, CancellationToken token = default)
    {
        if (data.Length < 128 || !data.AsSpan(0, 4).SequenceEqual("DDS "u8))
            throw new InvalidDataException("Invalid DDS header.");
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
        int width = checked((int)U(16)), height = checked((int)U(12));
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216 || U(24) > 1)
            throw new InvalidDataException("Invalid DDS dimensions or volume texture.");
        string format = Encoding.ASCII.GetString(data, 84, 4);
        int header = format == "DX10" ? 148 : 128;
        if (data.Length < header) throw new InvalidDataException("Truncated DDS header.");
        bool cube = (U(112) & 0x200) != 0 || (header == 148 && (U(136) & 4) != 0);
        if ((long)width * height * (cube ? 12 : 1) > 16_777_216)
            throw new InvalidDataException("PRR DDS image/atlas exceeds the managed decoder's 16-million-pixel limit.");
        if (header == 148 && (U(132) != 3 || U(140) != 1))
            throw new NotSupportedException("PRR supports single DDS textures, not arrays or volumes.");
        if (cube && (!cubeAtlas || width != height || (width & (width - 1)) != 0 || (header == 128 && (U(112) & 0xFE00) != 0xFE00)))
            throw new NotSupportedException("DDS cube requires six square power-of-two faces and a PRR cube-atlas shader.");
        if (!cube && cubeAtlas) throw new InvalidDataException("PRR cube-atlas shader expects a six-face DDS cubemap.");
        int levels = checked((int)Math.Max(1, U(28)));
        int maximum = 1 + (int)Math.Log2(Math.Max(width, height));
        if (levels > maximum) throw new InvalidDataException("Invalid DDS mip count.");
        int block = header == 148 ? U(128) switch { 71 or 72 or 80 => 8, 74 or 75 or 77 or 78 or 83 => 16, _ => 0 } :
            format switch { "DXT1" or "ATI1" or "BC4U" => 8, "DXT3" or "DXT5" or "ATI2" or "BC5U" => 16, _ => 0 };
        var sizes = new int[levels];
        for (int level = 0; level < levels; level++)
        {
            int w = Math.Max(1, width >> level), h = Math.Max(1, height >> level);
            sizes[level] = block != 0 ? checked(((w + 3) / 4) * ((h + 3) / 4) * block) :
                checked((level == 0 && (U(8) & 8) != 0 ? (int)U(20) : w * checked((int)U(88)) / 8) * h);
        }
        int faceBytes = checked(sizes.Sum());
        if ((long)header + (long)faceBytes * (cube ? 6 : 1) > data.Length)
            throw new InvalidDataException("Truncated DDS mip chain.");
        using var pixels = new MemoryStream();
        DdsImage? first = null, last = null;
        (int X, int Y)[] tiles = [(2, 1), (0, 1), (1, 0), (1, 2), (1, 1), (3, 1)];
        int mipOffset = 0;
        for (int level = 0; level < levels; level++)
        {
            token.ThrowIfCancellationRequested();
            int w = Math.Max(1, width >> level), h = Math.Max(1, height >> level);
            var image = new DdsImage(w * (cube ? 4 : 1), h * (cube ? 3 : 1), new byte[checked(w * h * (cube ? 12 : 1) * 4)]);
            for (int face = 0; face < (cube ? 6 : 1); face++)
            {
                byte[] single = new byte[checked(header + sizes[level])];
                data.AsSpan(0, header).CopyTo(single);
                void Set(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(single.AsSpan(offset, 4), value);
                Set(12, (uint)h); Set(16, (uint)w); Set(28, 1); Set(112, 0);
                if (block == 0) Set(20, (uint)(sizes[level] / h));
                if (header == 148) Set(136, 0);
                data.AsSpan(header + face * faceBytes + mipOffset, sizes[level]).CopyTo(single.AsSpan(header));
                var decoded = DdsImage.Decode(single);
                int x = cube ? tiles[face].X * w : 0, y = cube ? tiles[face].Y * h : 0;
                for (int row = 0; row < h; row++)
                    decoded.Pixels.AsSpan(row * w * 4, w * 4).CopyTo(image.Pixels.AsSpan(((y + row) * image.Width + x) * 4));
            }
            first ??= image; last = image; pixels.Write(image.Pixels); mipOffset += sizes[level];
        }
        // Complete Godot's required chain. Cube shaders clamp before these mixed-face tail levels.
        byte[] tail = TextureResourceWriter.Mipmaps(last!, token);
        pixels.Write(tail.AsSpan(last!.Pixels.Length));
        return new(first!, pixels.ToArray(), levels - 1);
    }
}
