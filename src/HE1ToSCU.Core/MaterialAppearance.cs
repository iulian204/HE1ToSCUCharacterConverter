using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>Game/shader adaptation for the built-in SpatialMaterial response.</summary>
public static class MaterialAppearance
{
    public static bool EyeShader(string shader) => shader.StartsWith("ChrEye",StringComparison.OrdinalIgnoreCase) || shader.StartsWith("SonicEye",StringComparison.OrdinalIgnoreCase);

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
