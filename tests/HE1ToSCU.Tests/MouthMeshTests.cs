using System.Numerics;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class MouthMeshTests
{
    private static ModelAsset Fixture(bool secondMouth = true)
    {
        List<Bone> bones = [new("Head", -1, Matrix4x4.Identity), new("MouthReference", 0, Matrix4x4.Identity),
            new("Jaw", 1, Matrix4x4.Identity), new("Teeth", 2, Matrix4x4.Identity),
            new("MouthReference_L", 0, Matrix4x4.Identity), new("Lip_L_L", 4, Matrix4x4.Identity),
            new("Teeth_L", 5, Matrix4x4.Identity), new("Sonic_Mouth", 0, Matrix4x4.Identity),
            new("Flipper", 0, Matrix4x4.Identity), new("InnerSurface", 2, Matrix4x4.Identity)];
        var vertices = new List<Vertex>();
        void Triangle(int bone, float weight = 1, int other = 0)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                float id = vertices.Count;
                vertices.Add(new Vertex(new(id, id * .2f, 0), Vector3.UnitZ, new(1, 0, 0, -1),
                    new(id / 100, .3f), new(.2f, .8f), new(.5f, .6f, .7f, 1),
                    [bone, other, 7, 0], new(weight, 1 - weight, 0, 0)));
            }
        }
        Triangle(0); Triangle(1, .7f, 2); Triangle(9); // Teeth/internal geometry can share a body material.
        if (secondMouth) { Triangle(4, .6f, 5); Triangle(6); }
        Triangle(1, .1f); // Slight mouth influence on the head stays with the body.
        Triangle(8); // "Flipper" must not match "lip".
        return new ModelAsset("character", bones, [new("skin", "opaque", vertices, Enumerable.Range(0, vertices.Count).ToList())]);
    }

    [Fact]
    public void IndependentMouthBranchesSeparateDespiteSharedMaterialsAndPreserveEveryFace()
    {
        var source = Fixture(); var split = MouthMeshSplitter.Split(source);
        Assert.Equal(new[] { "Mesh", "Mouth", "Mouth_L" }, split.MeshParts.Select(p => p.Name));
        Assert.Equal(new[] { "MouthReference", "MouthReference_L" }, split.MouthMeshes!.Select(p => p.MouthRootBone));
        Assert.Equal(9, Assert.Single(split.Surfaces).Indices.Count);
        Assert.All(split.MouthMeshes!, p => Assert.Equal(6, Assert.Single(p.Surfaces).Indices.Count));
        Assert.Same(source.Bones, split.Bones); Assert.Same(split, MouthMeshSplitter.Split(split));
        Assert.All(split.AllSurfaces, s => { Assert.Equal("skin", s.Material); Assert.Equal("opaque", s.Slot); });
        var original = source.Surfaces.Single();
        var faces = split.AllSurfaces.SelectMany(s => Enumerable.Range(0, s.Indices.Count / 3)
            .Select(f => s.Indices.Skip(f * 3).Take(3).Select(i => s.Vertices[i]).ToArray())).ToArray();
        Assert.Equal(original.Indices.Count / 3, faces.Length);
        for (int i = 0; i < original.Indices.Count; i += 3)
        {
            int first = i;
            Assert.Single(faces.Where(face => Enumerable.Range(0, 3).All(c => ReferenceEquals(face[c], original.Vertices[original.Indices[first + c]]))));
        }
    }

    [Theory]
    [InlineData("MouthReference")]
    [InlineData("mouthreference")]
    [InlineData("MOUTHREFERENCE")]
    public void UnweightedMouthLocatorsDoNotCreateAnExtraMesh(string rootName)
    {
        var source = Fixture(false);
        source.Bones[1] = source.Bones[1] with { Name = rootName };
        var split = MouthMeshSplitter.Split(source);
        Assert.Equal("Mouth", Assert.Single(split.MouthMeshes!).Name);
        Assert.Equal(2, split.MeshParts.Count());
        Assert.DoesNotContain(split.MouthMeshes!, p => p.MouthRootBone == "Sonic_Mouth");
    }

    [Fact]
    public void ModelsWithoutMouthWeightsKeepTheirExistingGeometry()
    {
        var original = Fixture();
        original = original with { Surfaces = [original.Surfaces[0] with { Indices = [0, 1, 2] }] };
        var split = MouthMeshSplitter.Split(original);
        Assert.Empty(split.MouthMeshes!); Assert.Same(original.Surfaces, split.Surfaces);
        Assert.Single(split.MeshParts);
    }

    [Fact]
    public void SeparateMeshesShareOneSkeletonAndAnimationTracks()
    {
        var source = Fixture(); string directory = Path.Combine(Path.GetTempPath(), "HE1SCU-mouth-" + Guid.NewGuid().ToString("N"));
        var clip = new AnimationClip("talk", 1, [2, 5], [[new(Vector3.Zero, Quaternion.Identity, Vector3.One), new(Vector3.Zero, Quaternion.Identity, Vector3.One)]]);
        try
        {
            GodotWriter.Write(source, [clip], directory, CancellationToken.None);
            string scene = File.ReadAllText(Path.Combine(directory, "character.tscn"));
            Assert.Equal(1, scene.Split("type=\"Skeleton\"").Length - 1);
            Assert.Equal(3, scene.Split("type=\"MeshInstance\" parent=\"Skeleton\"").Length - 1);
            Assert.Equal(3, scene.Split("skeleton = NodePath(\"..\")").Length - 1);
            foreach (string name in new[] { "character.mesh.tres", "character_Mouth.mesh.tres", "character_Mouth_L.mesh.tres" })
                Assert.True(File.Exists(Path.Combine(directory, name)), name);
            Assert.Contains("anims/talk = ExtResource( 4 )", scene);
            string animation = File.ReadAllText(Path.Combine(directory, "talk.tres"));
            Assert.Contains("Skeleton:Jaw", animation); Assert.Contains("Skeleton:Lip_L_L", animation);
            Assert.Contains("type=\"SpatialMaterial\"", File.ReadAllText(Path.Combine(directory, "materials/skin.tres")));
            var split = MouthMeshSplitter.Split(source);
            Assert.Throws<InvalidDataException>(() => ExportNames.Validate(split, [clip with { Name = "character_Mouth.mesh" }]));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
