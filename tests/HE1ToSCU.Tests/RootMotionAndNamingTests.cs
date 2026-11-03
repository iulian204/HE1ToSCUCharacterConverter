using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class RootMotionAndNamingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "HE1SCU-motion-tests-" + Guid.NewGuid().ToString("N"));
    public RootMotionAndNamingTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public void ReferenceMotionInterpolatesTranslationAndHeadingAboutItsUpAxis()
    {
        var motion = new RootMotion(Vector3.UnitZ, Vector3.UnitY, 2,
            [Vector4.Zero, new Vector4(2, 4, 6, MathF.PI)]);
        var midpoint = motion.Sample(1);
        Assert.Equal(new Vector3(1, 2, 3), midpoint.Translation);
        Assert.True(Vector3.Distance(Vector3.Transform(Vector3.UnitX, midpoint.Rotation), Vector3.UnitY) < 1e-5f);
        Assert.Equal(Vector3.Zero, motion.Sample(-1).Translation);
        Assert.Equal(new Vector3(2, 4, 6), motion.Sample(9).Translation);
    }

    [Fact]
    public void SparseRootMotionPreservesCompleteTurns()
    {
        var motion = new RootMotion(Vector3.UnitY, Vector3.UnitZ, 2,
            [Vector4.Zero, new Vector4(0, 0, 0, 4 * MathF.PI)]);
        var keys = motion.Keys().ToArray();
        Assert.True(keys.Length >= 9);
        Assert.Equal(0, keys[0].Time); Assert.Equal(2, keys[^1].Time);
        float travelled = keys.Zip(keys.Skip(1), (a, b) => 2 * MathF.Acos(Math.Clamp(MathF.Abs(Quaternion.Dot(a.Pose.Rotation, b.Pose.Rotation)), 0, 1))).Sum();
        Assert.InRange(travelled, 4 * MathF.PI - .001f, 4 * MathF.PI + .001f);
        Assert.Throws<InvalidDataException>(() => new RootMotion(Vector3.Zero, Vector3.UnitZ, 1, [Vector4.Zero]));
    }

    [Fact]
    public void OriginalFileNamesAndSceneReferencesArePreserved()
    {
        Assert.Equal("sn_dead", ExportNames.AnimationStem("sn_dead.anm.hkx"));
        Assert.Equal("sn.hkx_reference", ExportNames.AnimationStem("sn.hkx_reference.anm.hkx"));
        Assert.Equal("sn.anm_variant", ExportNames.AnimationStem("sn.anm_variant.hkx"));
        var model = new ModelAsset("chr_Sonic_HD", [new Bone("root", -1, Matrix4x4.Identity)], []);
        var clip = new AnimationClip("sn_dead", 1, [0], [[new Pose(Vector3.Zero, Quaternion.Identity, Vector3.One)]]);
        GodotWriter.Write(model, [clip], directory, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(directory, "chr_Sonic_HD.tscn")));
        Assert.True(File.Exists(Path.Combine(directory, "chr_Sonic_HD.mesh.tres")));
        Assert.True(File.Exists(Path.Combine(directory, "sn_dead.tres")));
        string scene = File.ReadAllText(Path.Combine(directory, "chr_Sonic_HD.tscn"));
        Assert.Contains("path=\"chr_Sonic_HD.mesh.tres\"", scene);
        Assert.Contains("path=\"sn_dead.tres\"", scene);
        Assert.Equal("chr_Sonic_HD.scn", ExportNames.SceneBinary(model));
        Assert.Equal("chr_Sonic_HD.mesh", ExportNames.MeshBinary(model));
        Assert.Equal("sn_dead.anim", ExportNames.AnimationBinary(clip));
        Assert.DoesNotContain(Directory.GetFiles(directory), p => Path.GetFileName(p).StartsWith("animation_", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => ExportNames.Validate(model, [clip, clip with { Name = "SN_DEAD" }]));
    }

    [Fact]
    public void XmlExtractedMotionIsWrittenAsIndependentScaledRootTrack()
    {
        string path = Path.Combine(directory, "sn_move.anm.hkx");
        File.WriteAllText(path, """
        <hkpackfile><hksection>
        <hkobject class="hkaInterleavedUncompressedAnimation" name="#1"><hkparam name="duration">1</hkparam><hkparam name="numberOfTransformTracks">1</hkparam><hkparam name="numberOfFloatTracks">0</hkparam><hkparam name="extractedMotion">#3</hkparam><hkparam name="transforms">(0 0 0 0)(0 0 0 1)(1 1 1 0)</hkparam></hkobject>
        <hkobject class="hkaAnimationBinding" name="#2"><hkparam name="animation">#1</hkparam><hkparam name="blendHint">NORMAL</hkparam><hkparam name="transformTrackToBoneIndices">0</hkparam></hkobject>
        <hkobject class="hkaDefaultAnimatedReferenceFrame" name="#3"><hkparam name="up">(0 1 0 0)</hkparam><hkparam name="forward">(0 0 1 0)</hkparam><hkparam name="duration">1</hkparam><hkparam name="referenceFrameSamples">(0 0 0 0)(1 2 3 1.5707963)</hkparam></hkobject>
        </hksection></hkpackfile>
        """);
        var clip = Assert.Single(new HavokReader().Read(path).Animations);
        Assert.Equal("sn_move", clip.Name); Assert.NotNull(clip.RootMotion);
        Assert.Equal(new Vector3(.5f, 1, 1.5f), clip.RootMotion.Sample(.5f).Translation);
        GodotWriter.Write(new ModelAsset("character", [new Bone("root", -1, Matrix4x4.Identity)], []), [clip], directory, CancellationToken.None);
        string text = File.ReadAllText(Path.Combine(directory, "sn_move.tres"));
        Assert.Contains("tracks/1/path = NodePath(\".\")", text);
        string[] entries = Regex.Match(text, @"tracks/1/keys = PoolRealArray\( (.*?) \)").Groups[1].Value.Split(',');
        float[] values = entries.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(new[] { 10f, 20f, 30f }, values[^10..^7]);
        Assert.Equal(Vector3.Zero, clip.Frames[0][0].Translation);
    }

    [Fact]
    public void InPlaceClipsExplicitlyResetPreviousRootMotion()
    {
        var clip = new AnimationClip("idle", 1, [], [[]]);
        GodotWriter.Write(new ModelAsset("character", [], []), [clip], directory, CancellationToken.None);
        string text = File.ReadAllText(Path.Combine(directory, "idle.tres"));
        Assert.Contains("tracks/0/path = NodePath(\".\")", text);
        Assert.Contains("keys = PoolRealArray( 0, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 )", text);
    }
}
