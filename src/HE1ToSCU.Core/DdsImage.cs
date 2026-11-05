using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace HE1ToSCU.Core;

/// <summary>Managed top-level DDS decoder for HE1 BC1/2/3/4/5 and uncompressed RGB(A).</summary>
public sealed record DdsImage(int Width, int Height, byte[] Pixels)
{
    internal int ResampleOffset(int x, int y, int width, int height)
    {
        int column = (int)((long)x * Width / width), row = (int)((long)y * Height / height);
        return (row * Width + column) * 4;
    }

    public static DdsImage Read(string path) => Decode(File.ReadAllBytes(path));
    public static DdsImage Decode(byte[] bytes)
    {
        if (bytes.Length < 128 || !bytes.AsSpan(0, 4).SequenceEqual("DDS "u8)) throw new InvalidDataException("Invalid DDS header.");
        uint U(int p) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p, 4));
        int width = checked((int)U(16)), height = checked((int)U(12));
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216) throw new InvalidDataException("Invalid or oversized DDS dimensions.");
        if ((U(112) & 0xFE00) != 0 || U(24) > 1) throw new NotSupportedException("Cubemap and volume DDS textures need a separate environment conversion.");
        string fourcc = Encoding.ASCII.GetString(bytes, 84, 4);
        int start = 128;
        if (fourcc == "DX10")
        {
            if (bytes.Length < 148) throw new InvalidDataException("Truncated DDS DX10 header.");
            if (U(132) != 3 || U(140) != 1 || (U(136) & 4) != 0) throw new NotSupportedException("Only single 2D DDS textures are supported.");
            fourcc = U(128) switch { 71 or 72 => "DXT1", 74 or 75 => "DXT3", 77 or 78 => "DXT5", 80 => "ATI1", 83 => "ATI2", _ => throw new NotSupportedException($"Unsupported DDS DXGI format {U(128)}.") };
            start = 148;
        }
        var rgba = new byte[checked(width * height * 4)];
        int blockSize = fourcc is "DXT1" or "ATI1" or "BC4U" ? 8 : 16;
        if (fourcc is "DXT1" or "DXT3" or "DXT5" or "ATI1" or "ATI2" or "BC4U" or "BC5U")
        {
            int length = checked(((width + 3) / 4) * ((height + 3) / 4) * blockSize);
            if (bytes.Length - start < length) throw new InvalidDataException("Truncated DDS block data.");
            int p = start;
            for (int by = 0; by < height; by += 4) for (int bx = 0; bx < width; bx += 4)
            {
                var block = new byte[64];
                if (fourcc is "ATI1" or "ATI2" or "BC4U" or "BC5U")
                {
                    var red = Alpha(bytes.AsSpan(p, 8)); var green = fourcc is "ATI2" or "BC5U" ? Alpha(bytes.AsSpan(p + 8, 8)) : red;
                    for (int i = 0; i < 16; i++) { block[i * 4] = red[i]; block[i * 4 + 1] = green[i]; block[i * 4 + 2] = 255; block[i * 4 + 3] = 255; }
                }
                else
                {
                    int colorOffset = fourcc == "DXT1" ? 0 : 8;
                    ushort c0 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(p + colorOffset)), c1 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(p + colorOffset + 2));
                    Vector4 Color(ushort c) => new((c >> 11) * 255f / 31, ((c >> 5) & 63) * 255f / 63, (c & 31) * 255f / 31, 255);
                    Vector4[] colors = [Color(c0), Color(c1), Vector4.Zero, Vector4.Zero];
                    if (c0 > c1 || fourcc != "DXT1") { colors[2] = (2 * colors[0] + colors[1]) / 3; colors[3] = (colors[0] + 2 * colors[1]) / 3; }
                    else { colors[2] = (colors[0] + colors[1]) / 2; colors[3] = Vector4.Zero; }
                    uint indices = U(p + colorOffset + 4); byte[]? alpha = fourcc == "DXT5" ? Alpha(bytes.AsSpan(p, 8)) : null;
                    for (int i = 0; i < 16; i++)
                    {
                        var c = colors[(indices >> (i * 2)) & 3];
                        block[i * 4] = (byte)c.X; block[i * 4 + 1] = (byte)c.Y; block[i * 4 + 2] = (byte)c.Z;
                        block[i * 4 + 3] = alpha != null ? alpha[i] : fourcc == "DXT3" ? (byte)(((bytes[p + i / 2] >> ((i % 2) * 4)) & 15) * 17) : (byte)c.W;
                    }
                }
                for (int y = 0; y < 4 && by + y < height; y++) for (int x = 0; x < 4 && bx + x < width; x++)
                    block.AsSpan((y * 4 + x) * 4, 4).CopyTo(rgba.AsSpan(((by + y) * width + bx + x) * 4, 4));
                p += blockSize;
            }
        }
        else
        {
            int bits = checked((int)U(88));
            if ((U(80) & 0x40) == 0 || bits is not (24 or 32)) throw new NotSupportedException($"Unsupported DDS encoding '{fourcc}', {bits} bits.");
            int pitch = (U(8) & 8) != 0 ? checked((int)U(20)) : width * (bits / 8);
            if (pitch < width * (bits / 8) || bytes.Length - start < (long)pitch * height) throw new InvalidDataException("Truncated DDS pixel data.");
            byte Channel(uint pixel, uint mask, byte fallback)
            {
                if (mask == 0) return fallback;
                int shift = BitOperations.TrailingZeroCount(mask); uint max = mask >> shift;
                return (byte)(((pixel & mask) >> shift) * 255L / max);
            }
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int p = start + y * pitch + x * (bits / 8), dst = (y * width + x) * 4;
                uint pixel = bits == 32 ? U(p) : (uint)(bytes[p] | bytes[p + 1] << 8 | bytes[p + 2] << 16);
                rgba[dst] = Channel(pixel, U(92), 0); rgba[dst + 1] = Channel(pixel, U(96), 0); rgba[dst + 2] = Channel(pixel, U(100), 0); rgba[dst + 3] = Channel(pixel, U(104), 255);
            }
        }
        return new DdsImage(width, height, rgba);
    }
    private static byte[] Alpha(ReadOnlySpan<byte> data)
    {
        int[] values = new int[8]; values[0] = data[0]; values[1] = data[1];
        if (values[0] > values[1]) for (int i = 1; i <= 6; i++) values[i + 1] = ((7 - i) * values[0] + i * values[1]) / 7;
        else { for (int i = 1; i <= 4; i++) values[i + 1] = ((5 - i) * values[0] + i * values[1]) / 5; values[6] = 0; values[7] = 255; }
        ulong indices = 0; for (int i = 0; i < 6; i++) indices |= (ulong)data[i + 2] << (i * 8);
        var result = new byte[16]; for (int i = 0; i < 16; i++) result[i] = (byte)values[(indices >> (i * 3)) & 7];
        return result;
    }
}
