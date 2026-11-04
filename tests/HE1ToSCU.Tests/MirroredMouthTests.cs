using System.Numerics;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class MirroredMouthTests
{
    private static readonly Matrix4x4 Reflection = Matrix4x4.CreateScale(-1, 1, 1);
    private static ModelAsset Fixture()
    {
        List<Bone> bones = [new("Root", -1, Matrix4x4.CreateTranslation(0, .1f, 0)),
            new("Head", 0, Matrix4x4.CreateFromYawPitchRoll(.2f, .3f, .5f) * Matrix4x4.CreateTranslation(.05f, .9f, .03f)),
            new("MouthReference", 1, Matrix4x4.CreateFromYawPitchRoll(.1f, .25f, -.3f) * Matrix4x4.CreateTranslation(.08f, .03f, .07f)),
            new("Jaw", 2, Matrix4x4.CreateRotationZ(.12f) * Matrix4x4.CreateTranslation(.02f, -.04f, .05f)),
            new("Cheek", 1, Matrix4x4.Identity)];
        Vertex V(Vector3 p, bool mouth) => new(p, Vector3.UnitZ, new(1, 0, 0, 1), new(.3f, .7f), new(.2f, .1f),
            new(.4f, .5f, .6f, 1), mouth ? [2, 3, 0, 0] : [1, 0, 0, 0], mouth ? new(.4f, .6f, 0, 0) : new(1, 0, 0, 0));
        return new ModelAsset("sonic", bones, [new("skin", "opaque",
            [V(new(0, 0, 0), false), V(new(.1f, 0, 0), false), V(new(0, .1f, 0), false),
             V(new(.1f, 1, .1f), true), V(new(.2f, 1, .1f), true), V(new(.1f, 1.1f, .1f), true)], [0, 1, 2, 3, 4, 5])]);
    }
    private static (Matrix4x4[] Global, Matrix4x4[] Rigid) Globals(IReadOnlyList<Bone> bones, Pose[] locals)
    {
        var rigid = new Matrix4x4?[bones.Count]; var scales = new Vector3?[bones.Count];
        Matrix4x4 R(int i) => rigid[i] ?? (rigid[i] = (locals[i] with { Scale = Vector3.One }).Matrix * (bones[i].Parent < 0 ? Matrix4x4.Identity : R(bones[i].Parent))).Value;
        Vector3 S(int i) => scales[i] ?? (scales[i] = locals[i].Scale * (bones[i].Parent < 0 ? Vector3.One : S(bones[i].Parent))).Value;
        return (Enumerable.Range(0, bones.Count).Select(i => Matrix4x4.CreateScale(S(i)) * R(i)).ToArray(), Enumerable.Range(0, bones.Count).Select(R).ToArray());
    }
    private static Vector3 Skin(Vertex vertex, IReadOnlyList<Bone> bones, Matrix4x4[] globals)
    {
        Matrix4x4 Bind(int i) => bones[i].Rest * (bones[i].Parent < 0 ? Matrix4x4.Identity : Bind(bones[i].Parent));
        var result = Vector3.Zero;
        for (int i = 0; i < 4; i++) if (vertex.Weights[i] > 0)
            result += vertex.Weights[i] * Vector3.Transform(vertex.Position, Spatial.Inverse(Bind(vertex.Bones[i])) * globals[vertex.Bones[i]]);
        return result;
    }

    [Fact]
    public void MirroredGeometryKeepsFacingTangentsMaterialsAndRemapsEveryWeight()
    {
        var original = MouthMeshSplitter.Split(Fixture());
        var result = MirroredMouth.Create(original, []);
        Assert.Equal(2, result.GeneratedBones); Assert.Equal(7, result.Model.Bones.Count); Assert.Equal(5, original.Bones.Count);
        var source = Assert.Single(original.MouthMeshes!); var copy = result.Model.MouthMeshes!.Single(p => p.Name == "Mouth_L");
        Assert.True(copy.Mirrored); Assert.Equal("MouthReference_L", copy.MouthRootBone);
        Assert.Equal(1, result.Model.Bones[5].Parent); Assert.Equal(5, result.Model.Bones[6].Parent);
        Assert.Equal("Jaw_L", result.Model.Bones[6].Name);
        var a = Assert.Single(source.Surfaces); var b = Assert.Single(copy.Surfaces);
        Assert.Equal(a.Material, b.Material); Assert.Equal(a.Slot, b.Slot); Assert.Equal(new[] { 0, 2, 1 }, b.Indices);
        for (int i = 0; i < a.Vertices.Count; i++)
        {
            var v = a.Vertices[i]; var mirrored = b.Vertices[i];
            Assert.Equal(Vector3.Transform(v.Position, Reflection), mirrored.Position);
            Assert.Equal(Vector3.TransformNormal(v.Normal, Reflection), mirrored.Normal);
            Assert.Equal(new Vector4(-v.Tangent.X, v.Tangent.Y, v.Tangent.Z, -v.Tangent.W), mirrored.Tangent);
            Assert.Equal(v.Weights, mirrored.Weights); Assert.Equal(new[] { 5, 6, 0, 0 }, mirrored.Bones);
            Assert.Equal(v.UV, mirrored.UV); Assert.Equal(v.UV2, mirrored.UV2); Assert.Equal(v.Color, mirrored.Color);
        }
        Vector3 Face(Surface s) => Vector3.Cross(s.Vertices[s.Indices[1]].Position - s.Vertices[s.Indices[0]].Position, s.Vertices[s.Indices[2]].Position - s.Vertices[s.Indices[0]].Position);
        Assert.True(Vector3.Dot(Vector3.Normalize(Face(b)), Vector3.Normalize(Vector3.TransformNormal(Face(a), Reflection))) > .99999f);
        Assert.Same(original.Surfaces, result.Model.Surfaces);
        string directory = Path.Combine(Path.GetTempPath(), "HE1SCU-mirror-" + Guid.NewGuid().ToString("N"));
        try
        {
            GodotWriter.Write(result.Model, [], directory, CancellationToken.None);
            var nodes = File.ReadAllText(Path.Combine(directory, "sonic.tscn")).Split("[node");
            Assert.Contains("visible = false", nodes.Single(n => n.StartsWith(" name=\"Mouth_L\"")));
            Assert.DoesNotContain("visible = false", nodes.Single(n => n.StartsWith(" name=\"Mouth\"")));
            Assert.DoesNotContain("scale =", nodes.Single(n => n.StartsWith(" name=\"Mouth_L\"")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void MirroredAnimationFollowsHeadAndMatchesReflectedSkinThroughSignedZeroScales()
    {
        var source = MouthMeshSplitter.Split(Fixture()); var rest = source.Bones.Select(b => Pose.FromMatrix(b.Rest)).ToArray();
        Pose[] Sample(float time)
        {
            var local = rest.ToArray();
            local[0] = local[0] with { Translation = local[0].Translation + new Vector3(.3f * time, 0, -.2f * time), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, time * .6f), Scale = new(1 + .1f * time, 1, 1 - .1f * time) };
            local[1] = local[1] with { Rotation = Quaternion.Normalize(Quaternion.Concatenate(local[1].Rotation, Quaternion.CreateFromAxisAngle(Vector3.UnitY, .8f * time))) };
            local[2] = local[2] with { Rotation = Quaternion.CreateFromYawPitchRoll(.2f * time, -.3f * time, .4f * time), Translation = local[2].Translation + new Vector3(.04f * time, .01f, 0), Scale = new(1 - 2 * time, .8f + .3f * time, 1) };
            local[3] = local[3] with { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .9f * time), Scale = new(1, 1.1f, .9f) };
            return local;
        }
        var clip = new AnimationClip("talk", 1, Enumerable.Range(0, rest.Length).ToArray(), [Sample(0), Sample(1)], SourceSampler: Sample);
        var result = MirroredMouth.Create(source, [clip]);
        var mirroredClip = Assert.Single(result.Clips);
        var sourceVertices = source.MouthMeshes![0].Surfaces[0].Vertices;
        var copiedVertices = result.Model.MouthMeshes!.Single(p => p.Mirrored).Surfaces[0].Vertices;
        Matrix4x4 Bind(int i) => source.Bones[i].Rest * (source.Bones[i].Parent < 0 ? Matrix4x4.Identity : Bind(source.Bones[i].Parent));
        var headReflection = Bind(1) * Reflection * Spatial.Inverse(Bind(1));
        for (int step = 0; step <= 50; step++)
        {
            float time = step / 50f;
            var original = Sample(time); var mirrored = AnimationSampling.Sample(mirroredClip, time);
            var sourceWorld = Globals(source.Bones, original); var mirroredWorld = Globals(result.Model.Bones, mirrored);
            for (int i = 0; i < original.Length; i++) Assert.Equal(original[i], mirrored[i]);
            var frameReflection = Spatial.Inverse(sourceWorld.Rigid[1]) * headReflection * sourceWorld.Rigid[1];
            for (int i = 0; i < sourceVertices.Count; i++)
            {
                var expected = Vector3.Transform(Skin(sourceVertices[i], source.Bones, sourceWorld.Global), frameReflection);
                var actual = Skin(copiedVertices[i], result.Model.Bones, mirroredWorld.Global);
                Assert.True(Vector3.Distance(expected, actual) < .00001f, $"Mirrored skin differs at {time}: {expected} vs {actual}");
            }
            Assert.Equal(original[2].Scale.X, mirrored[5].Scale.X, 5);
            Assert.True(float.IsFinite(mirrored[5].Rotation.LengthSquared()));
        }
        // Also exercise the same adaptive/scale-control stage used by real exports.
        var prepared = AnimationRig.Prepare(result.Model, result.Clips, CancellationToken.None);
        Assert.True(prepared.Helpers > 0); Assert.True(prepared.Clips[0].Frames.Length > 2);
    }

    [Fact]
    public void ExistingLeftMouthIsRetainedWithoutDuplicatingBones()
    {
        var first = MirroredMouth.Create(Fixture(), []);
        var again = MirroredMouth.Create(first.Model, first.Clips);
        Assert.Same(first.Model, again.Model); Assert.Same(first.Clips, again.Clips); Assert.Equal(0, again.GeneratedBones);
    }

    [Fact]
    public void BoneNameCollisionIsRejectedWithoutChangingTheOriginalModel()
    {
        var model = Fixture(); model.Bones.Add(new("MouthReference_L", 1, Matrix4x4.Identity));
        Assert.Throws<InvalidDataException>(() => MirroredMouth.Create(model, []));
        Assert.Equal(6, model.Bones.Count);
    }

    [Fact]
    public void NoMouthGeometryDoesNotGenerateEmptyMeshesOrBones()
    {
        var model = new ModelAsset("effect", [new("root", -1, Matrix4x4.Identity)], []);
        var result = MirroredMouth.Create(model, []);
        Assert.Equal(0, result.GeneratedBones); Assert.Single(result.Model.MeshParts); Assert.Single(result.Model.Bones);
    }

    [Fact]
    public async Task OptionDefaultsOffAndCoreRejectsItForGenerations()
    {
        var request = new ConversionRequest("missing.model", null, [], "new-output");
        Assert.False(request.GenerateMouthL);
        await Assert.ThrowsAsync<ArgumentException>(() => new ConversionService().ConvertAsync(request with { GenerateMouthL = true }));
    }
}
