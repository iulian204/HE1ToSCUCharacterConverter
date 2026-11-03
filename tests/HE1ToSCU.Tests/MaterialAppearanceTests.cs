using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class MaterialAppearanceTests
{
    private static He1Material Material(string shader,Vector4 diffuse,bool reflection=false) => new("surface",shader,.5f,false,false,
        new Dictionary<string,Vector4[]> { ["diffuse"]=[diffuse], ["power_gloss_level"]=[new(50,.65f,1,0)] },
        reflection?[new("finish_ref","reflection",0,0,0,null)]:[]);

    [Theory]
    [InlineData("SonicEye_dpne[bcu]")]
    [InlineData("SonicCloth_dsn[b]")]
    [InlineData("SonicEnamel_dspe[bc]")]
    public void LegacyNeutralDiffuseIsAdaptedOnlyForItsShaderProfile(string shader)
    {
        var material=Material(shader,new(.6f,.6f,.6f,.4f));
        var adapted=MaterialAppearance.Diffuse(material,SourceGame.SonicUnleashed);
        Assert.Equal(.8f,adapted.X,6);Assert.Equal(adapted.X,adapted.Y);Assert.Equal(adapted.Y,adapted.Z);Assert.Equal(.4f,adapted.W);
        Assert.Equal(new Vector4(.6f,.6f,.6f,.4f),MaterialAppearance.Diffuse(material,SourceGame.SonicGenerations));
        var colored=Material(shader,new(.2f,.4f,.6f,1));
        Assert.Equal(new Vector4(.2f,.4f,.6f,1),MaterialAppearance.Diffuse(colored,SourceGame.SonicUnleashed));
        Assert.Equal(Vector4.One,MaterialAppearance.Diffuse(Material(shader,Vector4.One),SourceGame.SonicUnleashed));
    }

    [Fact]
    public void ReflectiveBrightnessIncreasesWithoutChangingRoughnessOrUnrelatedMaterials()
    {
        SpecularSample[] samples=[new(255,128,1)];
        var bounded=SpecularApproximation.Fit(.65f,0,1,samples,SourceGame.SonicUnleashed);
        var reflective=SpecularApproximation.Fit(.65f,0,1,samples,SourceGame.SonicUnleashed,2.5f);
        Assert.True(reflective.Specular>bounded.Specular);
        Assert.Equal(bounded.Roughness.OrderBy(p=>p.Key),reflective.Roughness.OrderBy(p=>p.Key));
        Assert.InRange(.16f*reflective.Specular*reflective.Specular,0,1);
        Assert.Equal(1,MaterialAppearance.MaximumSpecular(Material("SonicEye_dpne",Vector4.One,true)));
        Assert.Equal(1,MaterialAppearance.IntensityScale(Material("SonicCloth_dsn",Vector4.One),SourceGame.SonicGenerations));
    }

    [Theory]
    [InlineData(SourceGame.SonicGenerations,"ChrSkin_dse1",1.55f)]
    [InlineData(SourceGame.SonicUnleashed,"SonicEnamel_dspe[bc]",1f)]
    public void FreshConverterOutputContainsTheProfileAdjustment(SourceGame game,string shader,float gain)
    {
        var material=Material(shader,new(.6f,.6f,.6f,0),true);
        var model=new ModelAsset("model",[],[new("surface","opaque",[],[])],new Dictionary<string,He1Material>{["surface"]=material});
        string directory=Path.Combine(Path.GetTempPath(),"HE1SCU-appearance-"+Guid.NewGuid().ToString("N"));
        try {
            new SpatialMaterialWriter().Write(model,directory,null,CancellationToken.None,game);
            string text=File.ReadAllText(Path.Combine(directory,"materials/surface.tres"));
            Assert.Contains("type=\"SpatialMaterial\"",text);Assert.DoesNotContain("ShaderMaterial",text);
            if(game==SourceGame.SonicUnleashed) Assert.Contains("albedo_color = Color( 0.800000",text);
            else Assert.Contains("albedo_color = Color( 0.600000024, 0.600000024, 0.600000024, 1 )",text);
            Assert.Equal(gain,MaterialAppearance.IntensityScale(material,game));
            float specular=float.Parse(Regex.Match(text,@"metallic_specular = ([^\r\n]+)").Groups[1].Value,CultureInfo.InvariantCulture);
            Assert.InRange(specular,0,1);
            Assert.Contains("clearcoat_enabled = true",text);
            Assert.Contains("roughness = "+(MathF.Round(SpecularApproximation.PeakRoughness(SpecularApproximation.Power(.65f,1,game))*255)/255).ToString("G9",CultureInfo.InvariantCulture),text);
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }

    [Fact]
    public void DirectHighlightPreservesApprovedPeakWhileBaseReflectanceStaysFixed()
    {
        SpecularSample[] samples=[new(255,128,1)];
        var baseline=SpecularApproximation.Fit(.65f,0,1,samples,SourceGame.SonicUnleashed);
        var target=SpecularApproximation.Fit(.65f,0,1,samples,SourceGame.SonicUnleashed,2.5f);
        var highlight=HighlightApproximation.Fit(baseline,target,samples);
        double roughness=baseline.Roughness[(255<<8)|128]/255.0;
        double direct=SpecularApproximation.GodotLobe(roughness,baseline.Specular,1)+highlight.Strength*HighlightApproximation.Lobe(highlight.Gloss,1);
        double wanted=SpecularApproximation.GodotLobe(roughness,target.Specular,1);
        Assert.Equal(wanted,direct,4);Assert.True(highlight.Strength>0);Assert.InRange(highlight.Gloss,0,1);
        Assert.True(double.IsFinite(highlight.Error));Assert.InRange(baseline.Specular,0,1);
    }

    [Fact]
    public void SuppliedSkinFresnelReducesWhiteSheenWithoutChangingAlbedoOrPower()
    {
        var skin=Material("SonicSkin_dspf[b]",Vector4.One);
        var parameters=skin.Parameters.ToDictionary(p=>p.Key,p=>p.Value);parameters["mrgFresnelParam"]=[new(.3f,1,0,0)];skin=skin with {Parameters=parameters};
        Assert.Equal(.3f,MaterialAppearance.SkinFresnelScale(skin));
        Assert.Equal(Vector4.One,MaterialAppearance.Diffuse(skin,SourceGame.SonicUnleashed));
        Assert.Equal(1,MaterialAppearance.SkinFresnelScale(skin with {Shader="SonicCloth_dsn[b]"}));
        SpecularSample[] samples=[new(255,8,1)];
        var before=SpecularApproximation.Fit(.1f,0,1,samples,SourceGame.SonicUnleashed);
        var after=SpecularApproximation.Fit(.1f,0,MaterialAppearance.SkinFresnelScale(skin),samples,SourceGame.SonicUnleashed);
        Assert.True(after.Specular<before.Specular);
        Assert.Equal(before.Roughness.OrderBy(p=>p.Key),after.Roughness.OrderBy(p=>p.Key));
    }
}
