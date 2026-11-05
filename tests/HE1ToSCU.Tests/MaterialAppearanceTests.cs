using System.Globalization;
using System.Buffers.Binary;
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

    [Theory]
    [InlineData("EnmMetal_dsnfe", SourceGame.SonicGenerations, 1, true, true)]
    [InlineData("EnmMetal_dsne", SourceGame.SonicGenerations, 0, true, true)]
    [InlineData("EnmMetal_dsne", SourceGame.SonicGenerations, 1, false, true)]
    [InlineData("EnmMetal_dsne", SourceGame.SonicUnleashed, 1, true, false)]
    [InlineData("EnmGlass_dsne1", SourceGame.SonicGenerations, 1, true, false)]
    [InlineData("SonicMetal_dpe", SourceGame.SonicUnleashed, 0, true, false)]
    public void NormalUvRoutingAndMetallicFinishAreIndependent(string shader, SourceGame game, byte uvIndex, bool reflection, bool metallic)
    {
        string directory = Path.Combine(Path.GetTempPath(), "HE1SCU-slots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "fixture.dds");
            File.WriteAllBytes(input, TwoPixelDds([193, 128, 32, 0, 64, 128, 255, 200]));
            string specInput = Path.Combine(directory, "spec.dds");
            byte[] bytes = TwoPixelDds([0, 0, 0, 255, 255, 255, 255, 0]);
            File.WriteAllBytes(specInput, bytes);
            var material = Material(shader, Vector4.One, reflection);
            material = material with { Textures = [.. material.Textures,
                new("base", "diffuse", 0, 0, 0, input),
                new("normal", "normal", uvIndex, 0, 0, input),
                new("spec", "specular", 0, 0, 0, specInput)] };
            var model = new ModelAsset("model", [], [new("surface", "opaque", [], [])],
                new Dictionary<string, He1Material> { ["surface"] = material });
            var logs = new List<string>();
            new SpatialMaterialWriter().Write(model, directory, logs.Add, CancellationToken.None, game);
            string text = File.ReadAllText(Path.Combine(directory, "materials/surface.tres"));
            Assert.Contains("type=\"SpatialMaterial\"", text);
            Assert.Contains("albedo_texture = ExtResource", text);
            Assert.DoesNotContain(logs, log => log.Contains("/normal:"));
            if (uvIndex == 1)
            {
                Assert.Contains("detail_normal = ExtResource", text);
                Assert.Contains("detail_uv_layer = 1", text);
                Assert.Contains("detail_blend_mode = 3", text);
                Assert.DoesNotContain("normal_texture =", text);
            }
            else
            {
                Assert.Contains("normal_texture = ExtResource", text);
                Assert.DoesNotContain("detail_enabled", text);
            }
            float metalness = float.Parse(Regex.Match(text, @"\bmetallic = ([^\r\n]+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.Equal(metallic ? 1 : 0, metalness);
            if (!metallic)
            {
                Assert.DoesNotContain("metallic_texture", text);
                return;
            }
            Assert.Contains("metallic_texture = ExtResource", text);
            Assert.DoesNotContain("clearcoat_enabled", text);
            string mapPath = Path.Combine(directory, "textures/surface_metalness.texture.tres");
            string basePath = Path.Combine(directory, "textures/surface_metal_base.texture.tres");
            string map = File.ReadAllText(mapPath), baseColor = File.ReadAllText(basePath);
            Assert.Contains("flags = 7", map);
            byte[] metalPixels = Pixels(map);
            Assert.Equal(0, metalPixels[0]);
            Assert.InRange(metalPixels[4], (byte)1, byte.MaxValue);
            Assert.Equal(0, Pixels(baseColor)[3]);
            Assert.Equal(200, Pixels(baseColor)[7]);
            // Changing only specular alpha cannot affect either generated map.
            bytes[131] = 0;
            bytes[135] = 255;
            File.WriteAllBytes(specInput, bytes);
            new SpatialMaterialWriter().Write(model, directory, null, CancellationToken.None, game);
            Assert.Equal(text, File.ReadAllText(Path.Combine(directory, "materials/surface.tres")));
            Assert.Equal(map, File.ReadAllText(mapPath));
            Assert.Equal(baseColor, File.ReadAllText(basePath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void GenerationsEnemyGlassUsesAuthoredEmissionAndMatcapCoverage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "HE1SCU-glass-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string diffuse = Path.Combine(directory, "glass.dds"), matcap = Path.Combine(directory, "reflection.dds");
            File.WriteAllBytes(diffuse, TwoPixelDds([0, 0, 0, 0, 0, 0, 0, 64]));
            File.WriteAllBytes(matcap, TwoPixelDds([200, 200, 200, 128, 200, 200, 200, 128]));
            var material = new He1Material("lens", "EnmGlass_dsne1", .5f, false, true,
                new Dictionary<string, Vector4[]> { ["mrgChrEmissionParam"] = [new(.5f, 1, .25f, .5f)], ["ambient"] = [new(.5f, 1, 1, 1)] },
                [new("glass", "diffuse", 0, 0, 0, diffuse), new("reflection", "reflection", 0, 0, 0, matcap)]);
            var model = new ModelAsset("unrelated_automaton", [], [new("lens", "transparent", [], [])],
                new Dictionary<string, He1Material> { ["lens"] = material });
            new SpatialMaterialWriter().Write(model, directory, null, CancellationToken.None);
            string text = File.ReadAllText(Path.Combine(directory, "materials/lens.tres"));
            Assert.Contains("params_blend_mode = 1", text);
            Assert.Contains("emission_enabled = true", text);
            Assert.Contains("emission = Color( 0.125, 0.5, 0.125, 1 )", text);
            byte[] rgba = Pixels(File.ReadAllText(Path.Combine(directory, "textures/lens_glass.texture.tres")));
            Assert.Equal(128, rgba[3]);
            Assert.Equal(192, rgba[7]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] TwoPixelDds(byte[] rgba)
        => RgbaDds(2, 1, rgba);

    private static byte[] RgbaDds(uint width, uint height, byte[] rgba)
    {
        byte[] bytes = new byte[128 + rgba.Length];
        "DDS "u8.CopyTo(bytes);
        void U(int p, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(p), value);
        U(4, 124); U(12, height); U(16, width); U(76, 32); U(80, 0x41); U(88, 32);
        U(92, 0xff); U(96, 0xff00); U(100, 0xff0000); U(104, 0xff000000);
        rgba.CopyTo(bytes, 128);
        return bytes;
    }

    [Theory]
    [InlineData("EnmGlass_dsne1", "reflection")]
    [InlineData("EnmEmission_dl", "displacement")]
    [InlineData("Parallax_dpnh", "height")]
    public void UnsupportedOptionalMapsPreserveConversionWithAnExplicitDiagnostic(string shader, string slot)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-optional-dds-").FullName;
        try
        {
            string path = Path.Combine(directory, "cube.dds");
            var bytes = TwoPixelDds([255, 255, 255, 255, 0, 0, 0, 255]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(112), 0x200);
            File.WriteAllBytes(path, bytes);
            var material = Material(shader, Vector4.One) with { Textures = [new("cube", slot, 0, 0, 0, path)] };
            var model = new ModelAsset("fixture", [], [new("surface", "opaque", [], [])],
                new Dictionary<string, He1Material> { ["surface"] = material });
            var logs = new List<string>();
            new SpatialMaterialWriter().Write(model, directory, logs.Add, CancellationToken.None);
            Assert.Contains(logs, line => line.Contains("skipped optional texture cube") && line.Contains("Cubemap"));

            material = material with { Textures = [new("cube", "diffuse", 0, 0, 0, path)] };
            Assert.Throws<NotSupportedException>(() => WriteMaterial(material, directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MetalBakeResamplesWideAndTallTexturesOnAValidGrid()
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-wide-metal-").FullName;
        try
        {
            string albedo = Path.Combine(directory, "wide.dds"), specular = Path.Combine(directory, "tall.dds");
            byte[] wide = new byte[65536 * 4];
            wide[^1] = 173;
            File.WriteAllBytes(albedo, RgbaDds(65536, 1, wide));
            File.WriteAllBytes(specular, RgbaDds(1, 512, new byte[512 * 4]));
            var material = Material("EnmMetal_dsne", Vector4.One) with { Textures =
                [new("wide", "diffuse", 0, 0, 0, albedo), new("tall", "specular", 0, 0, 0, specular)] };
            WriteMaterial(material, directory);
            string baked = File.ReadAllText(Path.Combine(directory, "textures/surface_metal_base.texture.tres"));
            Assert.Contains("\"width\": 65536", baked);
            Assert.Contains("\"height\": 1", baked);
            Assert.Equal(173, Pixels(baked)[65536 * 4 - 1]);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static byte[] Pixels(string texture) =>
        Regex.Match(texture, @"PoolByteArray\( ([^)]*)").Groups[1].Value
            .Split(',').Select(value => byte.Parse(value, CultureInfo.InvariantCulture)).ToArray();

    [Theory]
    [InlineData("ChrMetal_dpne", SourceGame.SonicGenerations, true)]
    [InlineData("PamChrMetal_dpe1", SourceGame.SonicGenerations, true)]
    [InlineData("Common_dp", SourceGame.SonicGenerations, false)]
    [InlineData("SonicMetal_dpe[b]", SourceGame.SonicUnleashed, false)]
    public void GlossUsesTheChannelReadByTheSourceShader(string shader, SourceGame game, bool alpha)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-gloss-").FullName;
        try
        {
            string map = Path.Combine(directory, "finish.dds");
            File.WriteAllBytes(map, TwoPixelDds([255, 255, 255, 0, 0, 0, 0, 255]));
            var material = Material(shader, Vector4.One) with { Textures = [new("finish", "power", 0, 0, 0, map)] };
            WriteMaterial(material, directory, game);
            byte[] rgba = Pixels(File.ReadAllText(Path.Combine(directory, "textures/surface_roughness.texture.tres")));
            Assert.Equal(230, rgba[alpha ? 0 : 4]);
            Assert.True(alpha ? rgba[0] > rgba[4] : rgba[0] < rgba[4]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("EnmEmission_dsnl", true)]
    [InlineData("EnmIgnore_dle1", true)]
    [InlineData("EnmGlass_dsnle1", true)]
    [InlineData("EnmEmission_d", false)]
    public void EnemyEmissionUsesTheAuthoredOffsetAndVariantTexture(string shader, bool sampled)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-emission-").FullName;
        try
        {
            string map = Path.Combine(directory, "effect.dds");
            File.WriteAllBytes(map, TwoPixelDds([255, 255, 255, 255, 0, 0, 0, 255]));
            string albedo = Path.Combine(directory, "base.dds");
            File.WriteAllBytes(albedo, TwoPixelDds([0, 255, 220, 255, 0, 0, 0, 0]));
            var material = Material(shader, Vector4.One) with
            {
                Parameters = new Dictionary<string, Vector4[]>
                {
                    ["mrgChrEmissionParam"] = [new(.25f, .5f, 0, 2)],
                    ["ambient"] = [new(.5f, .25f, 1, 1)]
                },
                Textures = [new("effect", "displacement", 1, 0, 0, map),
                    new("base", "diffuse", 0, 0, 0, albedo), new("base", "opacity", 0, 0, 0, albedo)]
            };
            string text = WriteMaterial(material, directory);
            Assert.Equal(255, Pixels(File.ReadAllText(Path.Combine(directory, "textures/base.texture.tres")))[3]);
            if (sampled)
            {
                Assert.Contains("emission_on_uv2 = true", text);
                Assert.Contains("emission_energy = 2", text);
                Assert.Contains("emission = Color( 0, 0, 0, 1 )", text);
                byte[] rgba = Pixels(File.ReadAllText(Path.Combine(directory, "textures/surface_emission.texture.tres")));
                Assert.Equal(new byte[] {207, 165, 255, 255, 99, 99, 0, 255}, rgba[..8]);
            }
            else
            {
                Assert.Contains("emission = Color( 0.25, 0.25, 0, 1 )", text);
                Assert.DoesNotContain("emission_texture", text);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void EnemyEmissionRespectsTheSourceProgramOutputCeiling()
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-emission-cap-").FullName;
        try
        {
            var material = Material("EnmEmission_d", Vector4.One) with
            {
                Parameters = new Dictionary<string, Vector4[]> { ["mrgChrEmissionParam"] = [new(3, .25f, 0, 5)] }
            };
            Assert.Contains("emission = Color( 4, 1.25, 0, 1 )", WriteMaterial(material, directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("Luminescence_dE@v@", SourceGame.SonicGenerations, true)]
    [InlineData("Luminescence_dE@@", SourceGame.SonicUnleashed, true)]
    [InlineData("Luminescence_d@E@", SourceGame.SonicUnleashed, false)]
    public void LuminescenceOnlyAliasesDisplacementWhenThePixelVariantSamplesEmission(string shader, SourceGame game, bool sampled)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-luminescence-").FullName;
        try
        {
            string map = Path.Combine(directory, "effect.dds");
            File.WriteAllBytes(map, TwoPixelDds([255, 255, 255, 255, 0, 0, 0, 255]));
            var material = Material(shader, Vector4.One) with { Textures = [new("effect", "displacement", 0, 0, 0, map)] };
            Assert.Equal(sampled, WriteMaterial(material, directory, game).Contains("emission_texture = ExtResource"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("IgnoreLight_da@v@", SourceGame.SonicGenerations, true, false)]
    [InlineData("IgnoreLight_d@v@", SourceGame.SonicUnleashed, true, false)]
    [InlineData("IgnoreLightTwice_d@v@", SourceGame.SonicGenerations, true, true)]
    [InlineData("IgnoreLightV_d@s@", SourceGame.SonicGenerations, false, false)]
    public void OnlyPureIgnoreLightVariantsAreUnshaded(string shader, SourceGame game, bool unshaded, bool doubled)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-unshaded-").FullName;
        try
        {
            var material = Material(shader, new Vector4(.25f, .5f, .75f, .5f));
            var parameters = material.Parameters.ToDictionary();
            parameters["emissive"] = [Vector4.One];
            string text = WriteMaterial(material with { Parameters = parameters }, directory, game);
            Assert.Equal(unshaded, text.Contains("flags_unshaded = true"));
            Assert.Equal(!unshaded, text.Contains("emission_enabled = true"));
            Assert.Contains(doubled ? "Color( 0.5, 1, 1.5, 0.5 )" : "Color( 0.25, 0.5, 0.75, 0.5 )", text);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ParallaxDepthUsesOnlyPrimaryUvHeightMaps(byte uv, bool supported)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-depth-").FullName;
        try
        {
            string map = Path.Combine(directory, "height.dds");
            File.WriteAllBytes(map, TwoPixelDds([0, 128, 255, 255, 255, 128, 0, 255]));
            var material = Material("Parallax_dpnh", Vector4.One) with { Textures = [new("height", "height", uv, 0, 0, map)] };
            string text = WriteMaterial(material, directory);
            Assert.Equal(supported, text.Contains("depth_texture = ExtResource"));
            Assert.Equal(supported, text.Contains("depth_scale = -0.03"));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string WriteMaterial(He1Material material, string directory, SourceGame game = SourceGame.SonicGenerations)
    {
        var model = new ModelAsset("test", [], [new("surface", "opaque", [], [])],
            new Dictionary<string, He1Material> { ["surface"] = material });
        new SpatialMaterialWriter().Write(model, directory, null, CancellationToken.None, game);
        return File.ReadAllText(Path.Combine(directory, "materials/surface.tres"));
    }

    [Theory]
    [InlineData("IgnoreLight_d", true, true, "1, 1, 1")]
    [InlineData("IgnoreLight_d[v]", true, false, "1, 1, 1")]
    [InlineData("IgnoreLight_d", false, true, "0, 0, 0")]
    [InlineData("Common_d", true, true, "0, 0, 0")]
    public void ZeroTintEffectUsesItsStoredColorOnlyWithAnUnshadedTexture(string shader, bool textured, bool additive, string rgb)
    {
        string directory = Directory.CreateTempSubdirectory("HE1SCU-effect-tint-").FullName;
        try
        {
            string map = Path.Combine(directory, "effect.dds");
            File.WriteAllBytes(map, TwoPixelDds([0, 200, 255, 255, 0, 0, 128, 64]));
            var material = Material(shader, new Vector4(0, 0, 0, .5f)) with
            {
                Additive = additive,
                Parameters = new Dictionary<string, Vector4[]>
                {
                    ["diffuse"] = [new(0, 0, 0, .5f)],
                    ["emissive"] = [new(1, 1, 1, 0)]
                },
                Textures = textured ? [new("effect", "diffuse", 0, 0, 0, map)] : []
            };
            string text = WriteMaterial(material, directory);
            Assert.Contains($"albedo_color = Color( {rgb}, 0.5 )", text);
            Assert.Equal(additive, text.Contains("params_blend_mode = 1"));
            if (shader.StartsWith("IgnoreLight", StringComparison.Ordinal))
                Assert.DoesNotContain("emission_enabled", text);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MetallicHighlightFitAccountsForMixedBaseReflectance()
    {
        var map = new DdsImage(2, 1, [0, 0, 0, 255, 255, 255, 255, 255]);
        float equivalent = MaterialAppearance.EquivalentMetalSpecular(.5f, .6f, Vector4.One, map);
        Assert.Equal(.4f * .16f * .5f * .5f + .6f * .5f, .16f * equivalent * equivalent, 6);
    }
}
