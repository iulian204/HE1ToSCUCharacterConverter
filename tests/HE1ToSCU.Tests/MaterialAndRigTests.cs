using System.Buffers.Binary;
using System.Numerics;
using System.Text.RegularExpressions;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class MaterialAndRigTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "HE1SCU-material-tests-" + Guid.NewGuid().ToString("N"));
    public MaterialAndRigTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public void ScaleCompensationMatchesQuaternionScaleCompositionAndPreservesBinds()
    {
        List<Bone> bones = [new("root", -1, Matrix4x4.Identity), new("calf", 0, Matrix4x4.CreateTranslation(0, 2, 0)), new("foot", 1, Matrix4x4.CreateTranslation(1, 0, 0))];
        var poses = new[] { new Pose(Vector3.Zero, Quaternion.Identity, new Vector3(.1f, 1, 1)), new Pose(new(0, 2, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .8f), new Vector3(10, 1, 1)), new Pose(new(1, 0, 0), Quaternion.Identity, Vector3.One) };
        var model = new ModelAsset("character", bones, []);
        var clip = new AnimationClip("boost", 1, [0, 1, 2], [poses]);
        var prepared = AnimationRig.Prepare(model, [clip], CancellationToken.None);
        Assert.Equal(2, prepared.Helpers);
        for (int i = 0; i < bones.Count; i++) Assert.Equal(bones[i].Name, prepared.Model.Bones[i].Name);
        var world = AnimationRig.ModelPose(bones, poses);
        var exported = prepared.Clips.Single().Frames[0];
        Matrix4x4 Global(int i) => prepared.Model.Bones[i].Parent < 0 ? exported[i].Matrix : exported[i].Matrix * Global(prepared.Model.Bones[i].Parent);
        Matrix4x4 Rest(int i) => prepared.Model.Bones[i].Parent < 0 ? prepared.Model.Bones[i].Rest : prepared.Model.Bones[i].Rest * Rest(prepared.Model.Bones[i].Parent);
        Matrix4x4 OriginalRest(int i) => bones[i].Parent < 0 ? bones[i].Rest : bones[i].Rest * OriginalRest(bones[i].Parent);
        for (int i = 0; i < bones.Count; i++)
        {
            Assert.True(Spatial.Near(Global(i), world[i].Matrix, .0001f));
            Assert.True(Spatial.Near(Rest(i), OriginalRest(i), .0001f));
        }
        var uncorrected = poses[2].Matrix * poses[1].Matrix * poses[0].Matrix;
        Assert.False(Spatial.Near(uncorrected, world[2].Matrix, .1f));
        Assert.Equal(0, AnimationRig.Prepare(model, [clip with { Frames = [poses.Select(p => p with { Scale = Vector3.One }).ToArray()] }], CancellationToken.None).Helpers);
    }

    [Fact]
    public void GodotFrontFacesAgreeWithSourceNormalsWithoutFlippingNormals()
    {
        // A clockwise source triangle seen from +Z has a -Z cross product and +Z authored normals.
        Vector3[] vertices = [new(0, 0, 0), new(0, 1, 0), new(1, 0, 0)];
        var indices = He1ModelReader.DecodeStrip([0, 1, 2], 3);
        var cross = Vector3.Cross(vertices[indices[1]] - vertices[indices[0]], vertices[indices[2]] - vertices[indices[0]]);
        Assert.True(Vector3.Dot(cross, Vector3.UnitZ) < 0);
    }

    [Fact]
    public void DdsReadsBc1AndBc3AndUncompressedAlphaMasks()
    {
        byte[] bc1 = Dds(4, 4, "DXT1", 8); BinaryPrimitives.WriteUInt16LittleEndian(bc1.AsSpan(128), 0xF800);
        var red = DdsImage.Decode(bc1); Assert.Equal(new byte[] { 255, 0, 0, 255 }, red.Pixels[..4]);
        byte[] bc3 = Dds(4, 4, "DXT5", 16); bc3[128] = 80; bc3[129] = 20; BinaryPrimitives.WriteUInt16LittleEndian(bc3.AsSpan(136), 0x07E0);
        Assert.Equal(new byte[] { 0, 255, 0, 80 }, DdsImage.Decode(bc3).Pixels[..4]);
        var rgba = Rgba(64, 128, 192, 32); Assert.Equal(new byte[] { 64, 128, 192, 32 }, DdsImage.Decode(rgba).Pixels[..4]);
        Assert.Throws<InvalidDataException>(() => DdsImage.Decode(bc1[..130]));
    }

    [Fact]
    public void MirageV3MaterialResolvesParametersFlagsAndTextureEntry()
    {
        var bytes = new List<byte>(new byte[60]);
        int Reserve(int count) { int p = bytes.Count; bytes.AddRange(new byte[count]); return p; }
        void U(int p, uint v) { byte[] a = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(a, v); for (int i = 0; i < 4; i++) bytes[p + i] = a[i]; }
        int Str(string s) { int p = bytes.Count; bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(s)); bytes.Add(0); return p; }
        void Ptr(int p, int target) => U(p, (uint)(target - 24));
        Ptr(24, Str("ChrSkin_dsn")); bytes[40] = 128; bytes[41] = 1; bytes[42] = 1; bytes[44] = 2; bytes[47] = 1;
        int table = Reserve(8); Ptr(48, table); int parameter = Reserve(12); Ptr(table, parameter); bytes[parameter + 2] = 1;
        Ptr(parameter + 4, Str("power_gloss_level\r")); int values = Reserve(16); Ptr(parameter + 8, values);
        float[] pgl = [50, .3f, .19f, 0]; for (int i = 0; i < 4; i++) U(values + i * 4, BitConverter.SingleToUInt32Bits(pgl[i]));
        int textures = Reserve(4); Ptr(36, textures); int entry = Reserve(12); Ptr(textures, entry);
        Ptr(entry, Str("body_dif")); Ptr(entry + 8, Str("diffuse")); bytes[entry + 5] = bytes[entry + 6] = 2;
        U(0, (uint)bytes.Count); U(4, 3); U(12, 24);
        string path = Path.Combine(directory, "body.material"); File.WriteAllBytes(path, bytes.ToArray()); File.WriteAllBytes(Path.Combine(directory, "body_dif.dds"), Rgba(255, 0, 0, 255));
        var material = new He1MaterialReader().Read(path);
        Assert.Equal("ChrSkin_dsn", material.Shader); Assert.True(material.DoubleSided); Assert.True(material.Additive);
        Assert.Equal(.3f, material.Parameter("power_gloss_level", Vector4.Zero).Y);
        var texture = Assert.Single(material.Textures); Assert.Equal("diffuse", texture.Type); Assert.NotNull(texture.SourcePath); Assert.Equal(2, texture.WrapU);
    }

    [Fact]
    public void SpatialMaterialUsesTextureColorNormalPackingAndChrSkinGlossChannels()
    {
        string diffuse = Path.Combine(directory, "body_dif.dds"), normal = Path.Combine(directory, "body_nrm.dds");
        File.WriteAllBytes(diffuse, Rgba(255, 0, 0, 255)); File.WriteAllBytes(normal, Rgba(255, 64, 255, 128));
        Dictionary<string, Vector4[]> parameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["diffuse"] = [new Vector4(1, .5f, .25f, 0)], ["opacity_reflection_refraction_spectype"] = [new Vector4(.5f, 0, 1, 3)],
            ["power_gloss_level"] = [new Vector4(999, .2f, .2f, 0)], ["specular"] = [Vector4.One]
        };
        var material = new He1Material("body", "ChrSkin_dsn", .5f, true, false, parameters,
            [new("body_dif", "diffuse", 0, 0, 0, diffuse), new("body_nrm", "normal", 0, 0, 0, normal)]);
        var model = new ModelAsset("character", [], [new("body", "opaque", [], [])], new Dictionary<string, He1Material> { ["body"] = material });
        new SpatialMaterialWriter().Write(model, directory, null, CancellationToken.None);
        string text = File.ReadAllText(Path.Combine(directory, "materials/body.tres"));
        Assert.Contains("albedo_color = Color( 1, 0.5, 0.25, 0.5 )", text);
        Assert.Contains("normal_enabled = true", text); Assert.Contains("params_cull_mode = 2", text);
        Assert.Contains("metallic_specular = 1", text); Assert.Contains("SpatialMaterial", text); Assert.DoesNotContain("ShaderMaterial", text);
        Assert.Equal(MathF.Pow(2f / 102, .25f), SpatialMaterialWriter.Roughness(100), 5);
        string normalText = File.ReadAllText(Path.Combine(directory, "textures/body_nrm.texture.tres"));
        byte[] first = Regex.Match(normalText, @"PoolByteArray\( ([^)]*)").Groups[1].Value.Split(',').Take(4).Select(s => byte.Parse(s.Trim())).ToArray();
        Assert.Equal(128, first[0]); Assert.Equal(191, first[1]); Assert.Equal(255, first[3]);
        Assert.Contains("flags = 7", normalText);
        Assert.Contains("flags = 23", File.ReadAllText(Path.Combine(directory, "textures/body_dif.texture.tres")));
    }

    [Theory]
    [InlineData("ChrEye_dpn",SourceGame.SonicGenerations)]
    [InlineData("SonicEye_dpne[bcu]",SourceGame.SonicUnleashed)]
    public void BothEyeShaderFamiliesDecodeSurfaceNormalsFromBlueAndAlpha(string shader,SourceGame game)
    {
        string normal=Path.Combine(directory,"eye_nrm.dds");File.WriteAllBytes(normal,Rgba(255,64,192,160));
        var material=new He1Material("eye",shader,.5f,false,false,new Dictionary<string,Vector4[]>(),[new("eye_nrm","normal",0,0,0,normal)]);
        var model=new ModelAsset("rig",[],[new("eye","opaque",[],[])],new Dictionary<string,He1Material>{["eye"]=material});
        new SpatialMaterialWriter().Write(model,directory,null,CancellationToken.None,game);
        var match=Regex.Match(File.ReadAllText(Path.Combine(directory,"textures/eye_nrm.texture.tres")),@"PoolByteArray\( ([^)]*)");
        var rgba=match.Groups[1].Value.Split(',').Take(4).Select(v=>byte.Parse(v.Trim())).ToArray();
        Assert.Equal(192,rgba[0]);Assert.Equal(95,rgba[1]);Assert.Equal(255,rgba[3]);
    }

    private static byte[] Dds(int width, int height, string format, int count)
    {
        var bytes = new byte[128 + count]; "DDS "u8.CopyTo(bytes); void U(int p, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(p), v);
        U(4, 124); U(12, (uint)height); U(16, (uint)width); U(76, 32); U(80, 4); System.Text.Encoding.ASCII.GetBytes(format).CopyTo(bytes, 84); return bytes;
    }
    private static byte[] Rgba(byte r, byte g, byte b, byte a)
    {
        var bytes = Dds(1, 1, "\0\0\0\0", 4); void U(int p, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(p), v);
        U(80, 0x41); U(88, 32); U(92, 0xFF); U(96, 0xFF00); U(100, 0xFF0000); U(104, 0xFF000000); bytes[128] = r; bytes[129] = g; bytes[130] = b; bytes[131] = a; return bytes;
    }
}
