using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class PrrMaterialTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("HE1SCU-PRR-").FullName;
    public void Dispose() => Directory.Delete(directory, true);
    private string Output(string name = "output") => Path.Combine(directory, name);
    private static He1Material Material(string name = "body", string shader = "ChrSkin_dsn") =>
        new(name, shader, .25f, false, false, new Dictionary<string, Vector4[]>(), []);
    private static ModelAsset Model(params He1Material[] materials) => new("Character", [new("Root", -1, Matrix4x4.Identity)],
        materials.Select(m => new Surface(m.Name, "opaque", [], [])).ToList(), materials.ToDictionary(m => m.Name));
    private PrrExportResult Write(ModelAsset model, string? path = null, SourceGame game = SourceGame.SonicGenerations) =>
        new PrrMaterialWriter().Write(model, Output(), game, new(path, Output(), false), null, default);

    public static IEnumerable<object[]> ShaderPaths => PrrShaderLibrary.Default.Paths.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(ShaderPaths))]
    public void EveryEmbeddedShaderHasTheRecordedSourceHashAndValidUniqueUniforms(string path)
    {
        var assembly = typeof(PrrShaderLibrary).Assembly;
        using var stream = assembly.GetManifestResourceStream("PRR/inventory.json")!;
        var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        var shader = PrrShaderLibrary.Default.Get(path);
        Assert.Equal(hashes[path], Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shader.Code))).ToLowerInvariant());
        Assert.Equal(shader.Uniforms.Count, shader.Uniforms.Select(u => u.Name).Distinct().Count());
        Assert.DoesNotContain("#include", shader.Code);
        Assert.DoesNotContain("res://", shader.Code);
    }

    [Fact]
    public void PortableLibraryContainsBothCompleteGameCollectionsAndEveryAliasResolves()
    {
        var library = PrrShaderLibrary.Default;
        Assert.Equal(99, library.Paths.Count(p => p.StartsWith("Generations/")));
        Assert.Equal(53, library.Paths.Count(p => p.StartsWith("Unleashed/")));
        using var stream = typeof(PrrShaderLibrary).Assembly.GetManifestResourceStream("PRR/bindings.json")!;
        var aliases = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
        foreach (var (source, targets) in aliases)
        {
            var match = library.Match(source.StartsWith("Generations/") ? SourceGame.SonicGenerations : SourceGame.SonicUnleashed, source[(source.IndexOf('/') + 1)..]);
            if (targets.Length == 1) Assert.Equal(targets[0], match.Shader?.Path);
            else Assert.Contains("Ambiguous", match.Reason);
        }
    }

    [Theory]
    [InlineData(SourceGame.SonicGenerations, "chrskin_dsn.SHADER-LIST", "Generations/ChrSkin_dsn@@.shader")]
    [InlineData(SourceGame.SonicGenerations, "EnmMetal_dsne", "Generations/EnmMetal_dsne1@@.shader")]
    [InlineData(SourceGame.SonicUnleashed, "SonicEye_dpne[bcu]", "Unleashed/SonicEye_dpne@c@.shader")]
    [InlineData(SourceGame.SonicUnleashed, "SonicEye_dpne[bu]", "Unleashed/SonicEye_dpne@@.shader")]
    public void MatchingPreservesGameAliasesAndVariants(SourceGame game, string source, string expected) =>
        Assert.Equal(expected, PrrShaderLibrary.Default.Match(game, source).Shader?.Path);

    [Fact]
    public void UnrelatedFamiliesAreNeverChosenAndUnsupportedIncludesAreExplicitErrors()
    {
        Assert.Null(PrrShaderLibrary.Default.Match(SourceGame.SonicUnleashed, "ChrSkin_dsn").Shader);
        Assert.Null(PrrShaderLibrary.Default.Match(SourceGame.SonicGenerations, "NotActuallyChrSkin_dsn").Shader);
        Assert.Throws<NotSupportedException>(() => PrrShaderLibrary.ParseUniforms("shader_type spatial;\n#include \"x\""));
        Assert.Throws<InvalidDataException>(() => PrrShaderLibrary.ParseUniforms("shader_type canvas_item;"));
    }

    [Fact]
    public void SourceValuesUseDeclaredColorTypesAndRawDataPlanesWithVerifiedComponents()
    {
        var shader = PrrShaderLibrary.Default.Get("Unleashed/SonicEye_dpne@@.shader");
        var source = Material() with { Parameters = new Dictionary<string, Vector4[]>
        {
            ["diffuse"] = [new(.2f, .4f, .6f, 0)], ["power_gloss_level"] = [new(7, 3, 4, 8)],
            ["mrgFresnelParam"] = [new(.3f, 2, 8, 9)], ["g_SonicEyeHighLightPosition"] = [new(1, 2, 3, 0)],
            ["opacity_reflection_refraction_spectype"] = [new(.75f, 8, 9, 2)], ["ambient"] = [Vector4.One]
        }};
        var mapped = PrrParameterMapper.Map(source, shader);
        Assert.Equal("Plane( 7, 3, 4, 8 )", mapped.Values["power_gloss_level"]);
        Assert.StartsWith("Plane( ", mapped.Values["diffuse_color"]);
        Assert.EndsWith(", 0 )", mapped.Values["diffuse_color"]);
        Assert.Equal("Vector3( 1, 2, 3 )", mapped.Values["eye_highlight_position"]);
        Assert.DoesNotContain("material_opacity", mapped.Values.Keys); // Opaque specialization has no coverage controls.
        Assert.DoesNotContain("alpha_cutoff", mapped.Values.Keys);
        var punch=PrrParameterMapper.Map(source,PrrShaderLibrary.Default.SelectPreset(shader,"punch",false,false));
        Assert.Equal("0.75",punch.Values["material_opacity"]);
        Assert.Equal("0.25",punch.Values["alpha_cutoff"]);
        Assert.Equal("sonic_fresnel_param.xy", mapped.Mappings["mrgFresnelParam"]);
        Assert.Equal(new[]{"opacity_reflection_refraction_spectype"},mapped.Unmapped);
        Assert.Equal("Color( 1, 1, 1, 1 )", mapped.Values["ambient_light_color"]);
        Assert.Contains(mapped.Assignments,a=>a.Name=="sky_color"&&a.Origin=="ConverterDefault");
    }

    [Fact]
    public void UnsupportedArraysAreReportedAndNonFiniteParametersAreRejected()
    {
        var shader = new PrrShader("test", "shader_type spatial;", [new("vec4", "data", "")]);
        var source = Material() with { Parameters = new Dictionary<string, Vector4[]> { ["data"] = [Vector4.One, Vector4.Zero] } };
        Assert.Equal(new[] { "data" }, PrrParameterMapper.Map(source, shader).Unmapped);
        source = source with { Parameters = new Dictionary<string, Vector4[]> { ["data"] = [new(float.NaN, 0, 0, 0)] } };
        Assert.Equal("failed_conversion",Assert.Single(PrrParameterMapper.Map(source, shader).Parameters).Classification);
    }

    [Fact]
    public void UsedShadersAreDeduplicatedMixedMaterialsKeepMouthAssignmentsAndRelocate()
    {
        var model = Model(Material(), Material("eyes", "ChrEye_dpne"), Material("other", "MissingShader"));
        model = model with { MouthMeshes = [new("Mouth", [new("body", "opaque", [], [])])] };
        var logs = new List<string>();
        var result = new PrrMaterialWriter().Write(model, Output(), SourceGame.SonicGenerations, new(null, Output(), false), logs.Add, default);
        Assert.Equal(2, result.ShaderMaterials);
        Assert.Equal(1, result.SpatialFallbacks);
        Assert.Equal(2, result.ShaderFiles.Count);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(Output(), "shaders"), "*.shader", SearchOption.AllDirectories).Length);
        Assert.Contains(logs, s => s.Contains("[PRR FALLBACK]") && s.Contains("MissingShader"));
        GodotWriter.Write(model, [], Output(), default, materialFiles: result.MaterialFiles);
        string mouth = File.ReadAllText(Path.Combine(Output(), "Character_Mouth.mesh.tres"));
        Assert.Contains(result.MaterialFiles[("body", "opaque")], mouth);
        Directory.Move(Output(), Output("moved"));
        foreach (var file in Directory.GetFiles(Output("moved"), "*.tres", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("D:/HE1TOPRRShaders", text);
            foreach (System.Text.RegularExpressions.Match reference in System.Text.RegularExpressions.Regex.Matches(text, "\\[ext_resource path=\"([^\"]+)\""))
                Assert.True(File.Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, reference.Groups[1].Value))));
        }
    }

    [Fact]
    public void ExternalProjectReferencesValidateAndStageWithoutPermanentCopies()
    {
        File.WriteAllText(Path.Combine(directory, "project.godot"), "config_version=4");
        string library = Path.Combine(directory, "PRR"); Directory.CreateDirectory(library);
        var shader = PrrShaderLibrary.Default.Get("Generations/ChrSkin_dsn@@.shader");
        File.WriteAllText(Path.Combine(library, Path.GetFileName(shader.Path)), shader.Code);
        Assert.Equal("res://PRR", PrrShaderDestination.ResourcePathFromFolder(library));
        var result = Write(Model(Material()), library);
        Assert.Empty(Directory.GetDirectories(Output(), "shaders"));
        string text = File.ReadAllText(Path.Combine(Output(), result.MaterialFiles.Single().Value));
        Assert.Contains("res://PRR/ChrSkin_dsn@@.shader", text);
        Assert.DoesNotContain(library, text);
        result.StageExternalShaders(Output());
        Assert.True(File.Exists(Path.Combine(Output(), "PRR", "ChrSkin_dsn@@.shader")));
        result.RemoveStagedShaders(Output());
        Assert.False(Directory.Exists(Path.Combine(Output(), "PRR")));
        Assert.True(File.Exists(Path.Combine(library, "ChrSkin_dsn@@.shader")));
        File.WriteAllText(Path.Combine(directory, "ChrSkin_dsn@@.shader"), shader.Code);
        var root = new PrrShaderDestination("res://", Output(), false);
        Assert.Equal("res://ChrSkin_dsn@@.shader", root.Resolve(shader, []).Reference);
    }

    [Fact]
    public void InvalidOrIncompatibleExternalReferencesAreConfigurationErrors()
    {
        Assert.Throws<ArgumentException>(() => new PrrShaderDestination("../shaders", Output(), false));
        Assert.Throws<ArgumentException>(() => new PrrShaderDestination("res://../bad", Output(), false));
        Assert.Throws<ArgumentException>(() => new PrrShaderDestination("res://Shaders", Output(), true));
        Assert.Throws<ArgumentException>(() => new PrrShaderDestination(directory, Output(), false));
        File.WriteAllText(Path.Combine(directory, "project.godot"), "config_version=4");
        var destination = new PrrShaderDestination(directory, Output(), true);
        var shader = PrrShaderLibrary.Default.Get("Generations/ChrSkin_dsn@@.shader");
        Assert.Throws<FileNotFoundException>(() => destination.Resolve(shader, []));
        File.WriteAllText(Path.Combine(directory, Path.GetFileName(shader.Path)), "shader_type spatial;");
        Assert.Throws<InvalidDataException>(() => destination.Resolve(shader, []));
    }

    [Fact]
    public void UnavailableLocalProjectIsReportedForTextButRejectedForBinary()
    {
        var destination = new PrrShaderDestination("res://PRR", Output(), false);
        var warnings = new List<string>();
        destination.Resolve(PrrShaderLibrary.Default.Get("Generations/ChrSkin_dsn@@.shader"), warnings);
        Assert.Contains("could not be checked", Assert.Single(warnings));
    }

    [Fact]
    public async Task ShaderPathWithoutPrrFlagIsRejectedBeforeAnyOutputIsWritten()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => new ConversionService().ConvertAsync(
            new("missing.model", null, [], Output(), PrrShaderPath: "res://PRR")));
        Assert.Contains("--export-prr-materials", error.Message);
        Assert.False(Directory.Exists(Output()));
    }

    [Fact]
    public void DdsChannelsMipsAndShaderUvSelectorsArePreservedWithoutRoughnessBaking()
    {
        string path = Path.Combine(directory, "original.dds");
        var bytes = Dds(2, 2, 2, false);
        new byte[] { 16, 32, 64, 128 }.CopyTo(bytes, 128);
        new byte[] { 100, 110, 120, 130 }.CopyTo(bytes, 144);
        File.WriteAllBytes(path, bytes);
        var source = Material(shader: "EnmMetal_dsne") with
        { Textures = [new("original", "normal", 1, 0, 0, path), new("original", "specular", 0, 0, 0, path)] };
        var result = Write(Model(source));
        var record = Assert.Single(result.Materials);
        Assert.Equal(record.Samplers["normal_texture"], record.Samplers["specular_texture"]);
        string texture = File.ReadAllText(Path.Combine(Output(), record.Samplers["normal_texture"]));
        Assert.Contains("16, 32, 64, 128", texture);
        Assert.Contains("100, 110, 120, 130", texture);
        Assert.Contains("flags = 7", texture);
        string material = File.ReadAllText(Path.Combine(Output(), result.MaterialFiles.Single().Value));
        Assert.Contains("shader_param/normal_uv_set = 1", material);
        Assert.Contains("shader_param/specular_uv_set = 0", material);
        Assert.DoesNotContain("roughness", material);
        Assert.Single(Directory.GetFiles(Path.Combine(Output(), "textures"), "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("gloss")]
    [InlineData("power")]
    public void OriginalGlossRolesBindRenamedSamplerAndIndependentUvSelector(string role)
    {
        string path=Path.Combine(directory,"eye.dds");
        var bytes=Dds(2,2,1,false);new byte[]{16,32,64,128}.CopyTo(bytes,128);File.WriteAllBytes(path,bytes);
        var source=Material(shader:"ChrEye_dpne1") with
        {Textures=[new("eye",role,1,0,0,path),new("eye","normal",0,0,0,path)]};
        var result=Write(Model(source));var record=Assert.Single(result.Materials);
        Assert.Equal(record.Samplers["normal_texture"],record.Samplers["gloss_texture"]);
        string text=File.ReadAllText(Path.Combine(Output(),result.MaterialFiles.Single().Value));
        Assert.Contains("shader_param/gloss_uv_set = 1",text);
        Assert.Contains("shader_param/normal_uv_set = 0",text);
        Assert.DoesNotContain("shader_param/power_texture",text);
        Assert.Contains("16, 32, 64, 128",File.ReadAllText(Path.Combine(Output(),record.Samplers["gloss_texture"])));
    }

    [Fact]
    public void CubemapFacesAndOriginalMipsUseTheVerifiedAtlasLayout()
    {
        byte[] bytes = Dds(2, 2, 2, true);
        for (int face = 0; face < 6; face++)
        {
            for (int pixel = 0; pixel < 4; pixel++) bytes[128 + face * 20 + pixel * 4] = (byte)(face + 1);
            bytes[128 + face * 20 + 16] = (byte)(face + 11);
        }
        var atlas = PrrTextureReader.Decode(bytes, true);
        Assert.Equal((8, 6, 1), (atlas.Image.Width, atlas.Image.Height, atlas.SourceMaxLod));
        (int X, int Y)[] tiles = [(2, 1), (0, 1), (1, 0), (1, 2), (1, 1), (3, 1)];
        for (int face = 0; face < 6; face++)
        {
            Assert.Equal(face + 1, atlas.Image.Pixels[(tiles[face].Y * 2 * 8 + tiles[face].X * 2) * 4]);
            Assert.Equal(face + 11, atlas.MipData[8 * 6 * 4 + (tiles[face].Y * 4 + tiles[face].X) * 4]);
        }
        Assert.Equal((8 * 6 + 4 * 3 + 2 * 1 + 1) * 4, atlas.MipData.Length);
        Assert.Throws<NotSupportedException>(() => PrrTextureReader.Decode(bytes, false));
        Assert.Throws<InvalidDataException>(() => PrrTextureReader.Decode(bytes[..^1], true));
    }

    [Fact]
    public void MissingTexturesAreVisibleDiagnosticsAndCancellationStopsShaderExport()
    {
        var source = Material() with { Textures = [new("missing", "normal", 0, 0, 0, null)] };
        Assert.Contains("Missing source texture", Assert.Single(Assert.Single(Write(Model(source)).Materials).Warnings));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => new PrrMaterialWriter().Write(Model(Material()), Output("cancelled"),
            SourceGame.SonicGenerations, new(null, Output(), false), null, cancel.Token));
    }

    [Fact]
    public void DedicatedEnemyEmissionWinsOverTheDifferentGenericEmissionRegister()
    {
        var shader = PrrShaderLibrary.Default.Match(SourceGame.SonicGenerations, "EnmGlass_dsne1").Shader!;
        var source = Material() with { Parameters = new Dictionary<string, Vector4[]>
        {
            ["emissive"] = [Vector4.Zero], ["mrgChrEmissionParam"] = [Vector4.One], ["ambient"] = [new(.2f, .3f, .4f, 0)]
        }};
        var result = PrrParameterMapper.Map(source, shader);
        Assert.Equal("Plane( 1, 1, 1, 1 )", result.Values["material_emission"]);
        Assert.Contains("emissive", result.Unmapped);
        Assert.Contains("ambient", result.Mappings.Keys);
    }

    [Fact]
    public void UnsupportedSourceTextureRolesAreExplicitManifestDiagnostics()
    {
        var source = Material() with { Textures = [new("source_map", "unknown_role", 0, 0, 0, null)] };
        Assert.Equal("Unmapped source texture role: unknown_role (source_map).",
            Assert.Single(Assert.Single(Write(Model(source)).Materials).Warnings));
    }

    [Fact]
    public void MixedFallbackTextureProcessingCannotOverwritePrrRawTexturePixels()
    {
        string path = Path.Combine(directory, "packed.dds");
        var bytes = Dds(2, 2, 1, false); new byte[] { 16, 32, 64, 128 }.CopyTo(bytes, 128); File.WriteAllBytes(path, bytes);
        var textures = new[] { new He1Texture("packed", "normal", 0, 0, 0, path) };
        var fallback = Material("fallback", "Unavailable") with { Textures = textures };
        string baseline = Output("baseline");
        new SpatialMaterialWriter().Write(Model(fallback), baseline, null, default);
        Write(Model(fallback, Material() with { Textures = textures }));
        foreach (string old in Directory.GetFiles(baseline, "*", SearchOption.AllDirectories))
            Assert.Equal(File.ReadAllBytes(old), File.ReadAllBytes(Path.Combine(Output(), Path.GetRelativePath(baseline, old))));
        string raw = Assert.Single(Directory.GetFiles(Path.Combine(Output(), "textures", "prr")));
        Assert.Contains("16, 32, 64, 128", File.ReadAllText(raw));
    }

    [Fact]
    public void TwoSidedAndAdditiveModesAreIsolatedVariantsAndExternalVariantsAreValidated()
    {
        var source = Material() with { DoubleSided = true, Additive = true };
        var result = Write(Model(source));
        var shader = Assert.Single(result.ShaderFiles);
        Assert.Equal("shaders/Render States/Opaque/Additive/DoubleSided/Generations/ChrSkin_dsn@@.shader", shader.Key);
        Assert.Contains("cull_disabled", shader.Value);
        Assert.Contains("blend_add", shader.Value);
        Assert.Equal(PrrShaderLibrary.Default.Get(shader.Key[8..]).Code,shader.Value);
        Assert.DoesNotContain("cull_disabled", PrrShaderLibrary.Default.Get("Generations/ChrSkin_dsn@@.shader").Code);
        File.WriteAllText(Path.Combine(directory, "project.godot"), "config_version=4");
        File.WriteAllText(Path.Combine(directory, Path.GetFileName(shader.Key)), shader.Value);
        var external = new PrrMaterialWriter().Write(Model(source), Output("external"), SourceGame.SonicGenerations,
            new(directory, Output("external"), true), null, default);
        Assert.Equal("res://" + Path.GetFileName(shader.Key), Assert.Single(external.ShaderFiles).Key);
    }

    private static byte[] Dds(int width, int height, int levels, bool cube)
    {
        int size = Enumerable.Range(0, levels).Sum(l => Math.Max(1, width >> l) * Math.Max(1, height >> l) * 4);
        byte[] bytes = new byte[128 + size * (cube ? 6 : 1)];
        "DDS "u8.CopyTo(bytes);
        void U(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        U(4, 124); U(12, (uint)height); U(16, (uint)width); U(28, (uint)levels);
        U(80, 0x40); U(88, 32); U(92, 255); U(96, 65280); U(100, 16711680); U(104, 4278190080);
        U(112, cube ? 0xFE00u : 0);
        return bytes;
    }
}
