using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>Game/shader adaptation for the built-in SpatialMaterial response.</summary>
public static class MaterialAppearance
{
    public static bool EyeShader(string shader) => shader.StartsWith("ChrEye",StringComparison.OrdinalIgnoreCase) || shader.StartsWith("SonicEye",StringComparison.OrdinalIgnoreCase);

    internal static bool ShaderFamily(string? shader, string family) => shader != null &&
        (shader.Equals(family, StringComparison.OrdinalIgnoreCase) || shader.StartsWith(family + "_", StringComparison.OrdinalIgnoreCase));

    internal static int GlossChannel(string? shader, SourceGame game)
    {
        if (shader != null && EyeShader(shader)) return 1;
        // The Generations character-metal programs sample sampPower.w. The
        // Xbox 360 SonicMetal fetch writes sampled R into a temporary's W.
        return game == SourceGame.SonicGenerations &&
            (ShaderFamily(shader, "ChrMetal") || ShaderFamily(shader, "PamChrMetal")) ? 3 : 0;
    }

    public static Vector4 Diffuse(He1Material? material,SourceGame game)
    {
        var value=material?.Parameter("diffuse",Vector4.One) ?? new Vector4(.72f,.77f,.86f,1);
        if(game==SourceGame.SonicUnleashed && material!=null &&
            (EyeShader(material.Shader) || material.Shader.StartsWith("SonicCloth",StringComparison.OrdinalIgnoreCase) || material.Shader.StartsWith("SonicEnamel",StringComparison.OrdinalIgnoreCase)) &&
            Vector3.Distance(new(value.X,value.Y,value.Z),new Vector3(.6f))<1e-5f)
        {
            // These source shaders multiply direct diffuse by g_Diffuse, then
            // add ambient separately. SpatialMaterial's albedo scales both.
            // Use an equal direct/ambient reference mix for the legacy neutral
            // tint; actual brightness remains dependent on scene lighting.
            // This is lighting compensation, not a gamma conversion.
            var ambient=material.Parameter("ambient",Vector4.One);
            value=new Vector4((value.X+ambient.X)*.5f,(value.Y+ambient.Y)*.5f,(value.Z+ambient.Z)*.5f,value.W);
        }
        return value;
    }

    public static bool ReflectiveFinish(He1Material? material) => material!=null && !EyeShader(material.Shader) &&
        material.Textures.Any(t=>t.Type.Equals("reflection",StringComparison.OrdinalIgnoreCase));

    // EnmMetal combines diffuse paint with a separate reflective response.
    public static float Metalness(He1Material? material, SourceGame game) =>
        game == SourceGame.SonicGenerations &&
        material?.Shader.StartsWith("EnmMetal_", StringComparison.OrdinalIgnoreCase) == true ? .6f : 0;

    private static readonly float[] linearSrgb = Enumerable.Range(0, 256).Select(i =>
    {
        float s = i / 255f;
        return s <= .04045f ? s / 12.92f : MathF.Pow((s + .055f) / 1.055f, 2.4f);
    }).ToArray();
    internal static ReadOnlySpan<float> LinearSrgb => linearSrgb;
    internal static byte EncodeSrgb(float linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        return (byte)MathF.Round(255 *
            (linear <= .0031308f ? 12.92f * linear : 1.055f * MathF.Pow(linear, 1 / 2.4f) - .055f));
    }

    public static float EquivalentMetalSpecular(float dielectricSpecular, float metallic, Vector4 diffuse, DdsImage? albedo)
    {
        var color = new Vector3(diffuse.X, diffuse.Y, diffuse.Z);
        if (albedo != null)
        {
            Vector3 sum = Vector3.Zero;
            for (int i = 0; i < albedo.Pixels.Length; i += 4)
                sum += new Vector3(LinearSrgb[albedo.Pixels[i]], LinearSrgb[albedo.Pixels[i + 1]], LinearSrgb[albedo.Pixels[i + 2]]);
            color *= sum / (albedo.Pixels.Length / 4);
        }
        float albedoLuminance = Math.Clamp(Vector3.Dot(color, new(.2126f, .7152f, .0722f)), 0, 1);
        // Express the mixed GGX F0 in the fitter's dielectric scalar units.
        float f0 = (1 - metallic) * .16f * dielectricSpecular * dielectricSpecular + metallic * albedoLuminance;
        return MathF.Sqrt(f0 / .16f);
    }

    public static float SkinFresnelScale(He1Material? material)
    {
        if(material==null || !(material.Shader.StartsWith("SonicSkin",StringComparison.OrdinalIgnoreCase) || material.Shader.StartsWith("ChrSkin",StringComparison.OrdinalIgnoreCase)) ||
            !material.Parameters.ContainsKey("mrgFresnelParam")) return 1;
        // HE1's vertex shader exports lerp(x,1,(1-N.V)^5)*y in normal.w;
        // the pixel shader multiplies its specular result by that channel.
        // Our fitting view has N.V=1, so the supplied normal-incidence factor is x*y.
        var fresnel=material.Parameter("mrgFresnelParam",Vector4.One);
        return Math.Clamp(fresnel.X,0,1)*Math.Max(0,fresnel.Y);
    }

    // This limit is for the desired direct-highlight fit only. The exported
    // base specular remains <=1 so the sky reflection is not amplified.
    // Extra direct brightness is approximated with built-in clearcoat.
    public static float MaximumSpecular(He1Material? material) => ReflectiveFinish(material)?2.5f:1;
    public static float IntensityScale(He1Material? material,SourceGame game) =>
        ReflectiveFinish(material) && game==SourceGame.SonicGenerations ? 1.55f : 1;
}
