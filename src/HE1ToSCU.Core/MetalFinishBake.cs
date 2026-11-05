using System.Numerics;

namespace HE1ToSCU.Core;

internal static class MetalFinishBake
{
    private const float DielectricF0 = .04f;
    public const float DielectricSpecular = .5f;
    private static readonly Vector3 Luminance = new(.2126f, .7152f, .0722f);

    public static (DdsImage Albedo, DdsImage Metalness) Bake(DdsImage albedo, DdsImage specular, DdsImage? gloss,
        Vector4 diffuseTint, Vector4 specularTint, Vector4 powerGloss, CancellationToken token)
    {
        int width = Math.Max(albedo.Width, Math.Max(specular.Width, gloss?.Width ?? 1));
        int height = Math.Max(albedo.Height, Math.Max(specular.Height, gloss?.Height ?? 1));
        if ((long)width * height > 16_777_216)
        {
            // Orthogonal wide/tall source maps can exceed the limit only
            // when combined. Bake on the largest valid source grid instead.
            var grid = albedo.Pixels.Length >= specular.Pixels.Length ? albedo : specular;
            if (gloss != null && gloss.Pixels.Length > grid.Pixels.Length) grid = gloss;
            (width, height) = (grid.Width, grid.Height);
        }
        byte[] basePixels = new byte[width * height * 4], metalPixels = new byte[basePixels.Length];
        var diffuse = new Vector3(diffuseTint.X, diffuseTint.Y, diffuseTint.Z);
        // The source applies this tint to direct light, while the matcap uses
        // its own color. One PBR reflection must serve both, so retain the
        // atlas hue and use the material tint for overall highlight intensity.
        float specularGain = Math.Max(0, Vector3.Dot(new Vector3(specularTint.X, specularTint.Y, specularTint.Z), Luminance));
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int a = albedo.ResampleOffset(x, y, width, height);
                int s = specular.ResampleOffset(x, y, width, height), dst = (y * width + x) * 4;
                Vector3 d = diffuse * new Vector3(MaterialAppearance.LinearSrgb[albedo.Pixels[a]],
                    MaterialAppearance.LinearSrgb[albedo.Pixels[a + 1]], MaterialAppearance.LinearSrgb[albedo.Pixels[a + 2]]);
                float g = gloss == null ? 1 : gloss.Pixels[gloss.ResampleOffset(x, y, width, height)] / 255f;
                float power = SpecularApproximation.Power(powerGloss.Y, g);
                // At normal incidence the Godot GGX peak is F0 / (4*pi*r^4).
                // With the power-matched roughness, r^4 = 2 / (power + 2).
                float peakToF0 = 8 * MathF.PI * SpecularApproximation.Brightness(powerGloss.Z, g) / (power + 2);
                Vector3 reflectance = new Vector3(MaterialAppearance.LinearSrgb[specular.Pixels[s]],
                    MaterialAppearance.LinearSrgb[specular.Pixels[s + 1]], MaterialAppearance.LinearSrgb[specular.Pixels[s + 2]])
                    * (peakToF0 * specularGain);
                float maxChannel = Math.Max(reflectance.X, Math.Max(reflectance.Y, reflectance.Z));
                if (maxChannel > 1) reflectance /= maxChannel;
                var (color, metallic) = Solve(d, reflectance);
                basePixels[dst] = MaterialAppearance.EncodeSrgb(color.X);
                basePixels[dst + 1] = MaterialAppearance.EncodeSrgb(color.Y);
                basePixels[dst + 2] = MaterialAppearance.EncodeSrgb(color.Z);
                basePixels[dst + 3] = albedo.Pixels[a + 3];
                metalPixels[dst] = metalPixels[dst + 1] = metalPixels[dst + 2] = (byte)MathF.Round(metallic * 255);
                metalPixels[dst + 3] = 255;
            }
        }
        return (new DdsImage(width, height, basePixels), new DdsImage(width, height, metalPixels));
    }

    internal static (Vector3 Color, float Metallic) Solve(Vector3 diffuse, Vector3 reflectance)
    {
        diffuse = Vector3.Clamp(diffuse, Vector3.Zero, Vector3.One);
        reflectance = Vector3.Clamp(reflectance, Vector3.Zero, Vector3.One);
        float d = Vector3.Dot(diffuse, Luminance), s = Vector3.Dot(reflectance, Luminance);
        if (s <= DielectricF0) return (diffuse, 0);

        // Solve luminance for D = q*C and F0 = (1-q)*C + q*0.04,
        // where q is the dielectric fraction. Fit the individual RGB channels
        // jointly with the resulting q because they need not agree on one metalness.
        float sum = d + s;
        float q = Math.Clamp(2 * d / (sum + MathF.Sqrt(Math.Max(0, sum * sum - 4 * DielectricF0 * d))), 0, 1);
        float metallic = 1 - q;
        Vector3 color = (q * diffuse + metallic * (reflectance - new Vector3(q * DielectricF0))) /
            (q * q + metallic * metallic);
        return (Vector3.Clamp(color, Vector3.Zero, Vector3.One), metallic);
    }
}
