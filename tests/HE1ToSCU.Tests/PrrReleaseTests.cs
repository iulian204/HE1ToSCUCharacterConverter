using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class PrrReleaseTests
{
    [LocalPrrReleaseFact]
    public void BundledShadersMatchTheCurrentlySuppliedReleaseNotOnlyTheirOwnInventory()
    {
        string root=LocalPrrReleaseFactAttribute.Directory;
        var library=PrrShaderLibrary.Default;
        var files=System.IO.Directory.GetFiles(root,"*.shader",SearchOption.AllDirectories);
        Assert.Equal(library.PresetPaths.Count,files.Length);
        foreach(string file in files)
        {
            string path=Path.GetRelativePath(root,file).Replace('\\','/');
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(file)),SHA256.HashData(Encoding.UTF8.GetBytes(library.Get(path).Code)));
        }
    }
    [Fact]
    public void CompleteReleaseHasAllNativePresetsWithExactHashesAndSupportedInterfaces()
    {
        var library=PrrShaderLibrary.Default;
        using var stream=typeof(PrrShaderLibrary).Assembly.GetManifestResourceStream("PRR/inventory.json")!;
        var hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
        Assert.Equal(3648,library.PresetPaths.Count);
        Assert.Equal(library.PresetPaths.Count,hashes.Count);
        foreach(var path in library.PresetPaths)
        {
            var shader=library.Get(path);
            Assert.Equal(hashes[path],Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shader.Code))).ToLowerInvariant());
            Assert.DoesNotContain(shader.Uniforms,u=>u.Name is "power_texture" or "material_uv_set" or "extra_uv_table" or "eye_light_range");
            Assert.DoesNotContain(shader.Uniforms,u=>u.ArrayDeclaration!=null);
            Assert.DoesNotContain("#include",shader.Code);
            Assert.DoesNotContain("res://",shader.Code);
            foreach(string color in new[]{"diffuse_color","specular_color","eye_highlight_color"})
                Assert.DoesNotContain(shader.Uniforms,u=>u.Name==color&&u.Hint!=null);
        }
        foreach(string canonical in library.Paths)
            foreach(string coverage in new[]{"Opaque","Punch","Trans"})
                foreach(string blend in new[]{"Normal","Additive","Multiply","Subtract"})
                    foreach(string sides in new[]{"SingleSided","DoubleSided"})
                    {
                        string path=coverage=="Opaque"&&blend=="Normal"&&sides=="SingleSided"?canonical:$"Render States/{coverage}/{blend}/{sides}/{canonical}";
                        Assert.Contains(path,library.PresetPaths);
                        Assert.Equal(coverage=="Punch",library.Get(path).Uniforms.Any(u=>u.Name=="alpha_cutoff"));
                    }
    }

    [Theory]
    [InlineData("opaque",false,false,"Generations/ChrEye_dpne1@@.shader")]
    [InlineData("punch",false,false,"Render States/Punch/Normal/SingleSided/Generations/ChrEye_dpne1@@.shader")]
    [InlineData("transparent",true,true,"Render States/Trans/Additive/DoubleSided/Generations/ChrEye_dpne1@@.shader")]
    public void CoverageAndFlagsSelectSuppliedByteExactShader(string slot,bool add,bool two,string expected)
    {
        var library=PrrShaderLibrary.Default;
        Assert.Equal(expected,library.SelectPreset(library.Get("Generations/ChrEye_dpne1@@.shader"),slot,add,two).Path);
        Assert.Throws<InvalidDataException>(()=>library.SelectPreset(library.Get("Generations/ChrEye_dpne1@@.shader"),"unknown",false,false));
    }

    [Fact]
    public void SourceSpecificExtraParametersUseVerifiedLanesAndRawNumbers()
    {
        var source=new He1Material("night","NightEnemyCommon_drE@@",.5f,false,false,new Dictionary<string,Vector4[]>
        {
            ["g_FalloffColor0"]=[new(.2f,.3f,.4f,0)],["g_FalloffColor1"]=[new(2,3,4,0)],
            ["g_FalloffParam"]=[new(1,2,3,4)],["g_RefractionParam"]=[new(4,5,6,7)],
            ["g_NoiseScrollScale"]=[new(-1,0,2,3)],["g_NoisePower"]=[new(2,3,4,5)],
            ["mrgEmissionPower"]=[new(10,20,30,40)]
        },[]);
        var result=PrrParameterMapper.Map(source,PrrShaderLibrary.Default.Get("Unleashed/NightEnemyCommon_drE@@.shader"));
        Assert.Empty(result.Unmapped);
        Assert.Equal("Vector2( 30, 20 )",result.Values["emission_power"]);
        Assert.Equal("Vector2( 2, 3 )",result.Values["noise_power"]);
        Assert.Equal("Vector3( 4, 5, 6 )",result.Values["refraction_param"]);
        Assert.EndsWith(", 0 )",result.Values["falloff_color_near"]);
        Assert.StartsWith("Plane(",result.Values["falloff_color_near"]);
        Assert.Equal("Plane( -1, 0, 2, 3 )",result.Values["noise_scroll_scale"]);
    }

    [Fact]
    public void DifferentCoverageSlotsHaveIndependentExpectedValuesAndExternalReleasePaths()
    {
        string root=Directory.CreateTempSubdirectory("PRR-release-").FullName;
        try
        {
            var source=new He1Material("eye","ChrEye_dpne1@@",.4f,false,false,new Dictionary<string,Vector4[]>{["diffuse"]=[new(.6f,.6f,.6f,0)]},[]);
            var model=new ModelAsset("test",[new("Root",-1,Matrix4x4.Identity)],new[]{"opaque","punch","transparent"}.Select(s=>new Surface("eye",s,[],[])).ToList(),new Dictionary<string,He1Material>{{"eye",source}});
            string output=Path.Combine(root,"output");
            var result=new PrrMaterialWriter().Write(model,output,SourceGame.SonicGenerations,new(null,output,false),null,default);
            result.WriteExpected(output);
            using var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"__prr-expected.json")));
            Assert.Equal(3,expected.RootElement.EnumerateObject().Count());
            Assert.Equal(3,result.Audits.Select(a=>a.ResourceName).Distinct().Count());
            Assert.DoesNotContain(result.Audits.Single(a=>a.Slot=="opaque").Mapping.Assignments,a=>a.Name=="alpha_cutoff");
            Assert.Contains(result.Audits.Single(a=>a.Slot=="punch").Mapping.Assignments,a=>a.Name=="alpha_cutoff");
            File.WriteAllText(Path.Combine(root,"project.godot"),"config_version=4");
            string release=Path.Combine(root,"PRR");
            foreach(var (reference,code) in result.ShaderFiles)
            {
                string path=Path.Combine(release,reference[8..]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,code);
            }
            string externalOutput=Path.Combine(root,"external");
            var external=new PrrMaterialWriter().Write(model,externalOutput,SourceGame.SonicGenerations,new(release,externalOutput,true),null,default);
            Assert.Contains("res://PRR/Generations/ChrEye_dpne1@@.shader",external.ShaderFiles.Keys);
            Assert.Contains("res://PRR/Render States/Punch/Normal/SingleSided/Generations/ChrEye_dpne1@@.shader",external.ShaderFiles.Keys);
            Assert.False(Directory.Exists(Path.Combine(externalOutput,"shaders")));
            external.StageExternalShaders(externalOutput);external.RemoveStagedShaders(externalOutput);
            Assert.False(Directory.Exists(Path.Combine(externalOutput,"PRR")));
        }
        finally {Directory.Delete(root,true);}
    }
}

public sealed class LocalPrrReleaseFactAttribute : FactAttribute
{
    public static string Directory=>Environment.GetEnvironmentVariable("HE1SCU_PRR_RELEASE")??@"D:\HE1TOPRRShaders\Release";
    public LocalPrrReleaseFactAttribute(){if(!System.IO.Directory.Exists(Directory))Skip="Set HE1SCU_PRR_RELEASE to verify against an independently supplied current shader library.";}
}
