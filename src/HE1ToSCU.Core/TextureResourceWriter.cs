using System.Globalization;
using System.Text;

namespace HE1ToSCU.Core;

internal static class TextureResourceWriter
{
    public static void Write(string path, string name, DdsImage image, int flags, CancellationToken token, byte[]? originalMipData = null)
    {
        byte[] mipData = originalMipData ?? Mipmaps(image, token);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.Write($"[gd_resource type=\"ImageTexture\" load_steps=2 format=2]\n\n[sub_resource type=\"Image\" id=1]\ndata = {{\n\"width\": {image.Width},\n\"height\": {image.Height},\n\"format\": \"RGBA8\",\n\"mipmaps\": true,\n\"data\": PoolByteArray( ");
        for (int i = 0; i < mipData.Length; i++) { if (i > 0) writer.Write(", "); writer.Write(mipData[i].ToString(CultureInfo.InvariantCulture)); }
        writer.Write($" )\n}}\n\n[resource]\nresource_name = {ExportNames.Literal(name)}\nflags = {flags}\nimage = SubResource( 1 )\n");
    }
    public static byte[] Mipmaps(DdsImage image, CancellationToken token)
    {
        using var stream = new MemoryStream(); byte[] current = image.Pixels; int width = image.Width, height = image.Height;
        while (true)
        {
            token.ThrowIfCancellationRequested(); stream.Write(current);
            if (width == 1 && height == 1) break;
            int nw = Math.Max(1, width / 2), nh = Math.Max(1, height / 2); var next = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) for (int channel = 0; channel < 4; channel++)
            {
                int sum = 0, count = 0;
                for (int dy = 0; dy < 2 && y * 2 + dy < height; dy++) for (int dx = 0; dx < 2 && x * 2 + dx < width; dx++) { sum += current[((y * 2 + dy) * width + x * 2 + dx) * 4 + channel]; count++; }
                next[(y * nw + x) * 4 + channel] = (byte)(sum / count);
            }
            current = next; width = nw; height = nh;
        }
        return stream.ToArray();
    }
}
