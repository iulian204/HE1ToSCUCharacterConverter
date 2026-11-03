using System.Numerics;

namespace HE1ToSCU.Core;

public sealed record Bone(string Name, int Parent, Matrix4x4 Rest);
public sealed record Vertex(Vector3 Position, Vector3 Normal, Vector4 Tangent, Vector2 UV, Vector2 UV2,
    Vector4 Color, int[] Bones, Vector4 Weights);
public sealed record Surface(string Material, string Slot, List<Vertex> Vertices, List<int> Indices);
public sealed record ModelAsset(string Name, List<Bone> Bones, List<Surface> Surfaces, IReadOnlyDictionary<string, He1Material>? Materials = null,
    IReadOnlyDictionary<int,int>? RigControls = null);
public sealed record Pose(Vector3 Translation, Quaternion Rotation, Vector3 Scale)
{
    public Matrix4x4 Matrix => Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Translation);
    public static Pose FromMatrix(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
            throw new InvalidDataException("Transform cannot be decomposed into translation, quaternion and scale.");
        var result = new Pose(translation, Quaternion.Normalize(rotation), scale);
        if (!Spatial.Near(matrix, result.Matrix, 0.002f))
            throw new InvalidDataException("Transform contains shear; it cannot be represented by a Godot bone track.");
        return result;
    }
    public static Pose Relative(Pose local, Matrix4x4 rest)
    {
        // Extract rotation before applying authored scales. Matrix decomposition
        // loses rotation at zero scale and can swap negative axes between keys.
        var rigid=local with {Scale=Vector3.One};
        var basis=FromMatrix(rigid.Matrix*Spatial.Inverse(rest));
        return basis with {Scale=basis.Scale*local.Scale};
    }
}
public enum AnimationBlend { Normal, AdditiveDeprecated, Additive }
public sealed record AnimationClip(string Name, float Duration, int[] TrackToBone, Pose[][] Frames, RootMotion? RootMotion = null,
    float[]? Times = null, Func<float, Pose[]>? SourceSampler = null, float[]? Discontinuities = null,
    AnimationBlend Blend = AnimationBlend.Normal, string? BakedReference = null);
public sealed record HavokAsset(List<Bone> Bones, List<AnimationClip> Animations);

public static class Spatial
{
    // Generations/Unleashed metre-space -> SCU character decimetre-space, both Y up.
    // Source chr_Sonic_HD Hips: 0.501910; SCU chr_Sonic_SD Hips: 5.01910.
    // Keep the basis; do not introduce an arbitrary handedness flip.
    public const float Scale = 10f;
    public static Matrix4x4 Convert(Matrix4x4 m)
    {
        m.M41 *= Scale; m.M42 *= Scale; m.M43 *= Scale;
        return m;
    }
    public static Matrix4x4 Inverse(Matrix4x4 matrix)
        => Matrix4x4.Invert(matrix, out var inverse) ? inverse : throw new InvalidDataException("Singular bind/rest matrix.");
    public static bool Near(Matrix4x4 a, Matrix4x4 b, float epsilon)
    {
        float[] x = [a.M11,a.M12,a.M13,a.M14,a.M21,a.M22,a.M23,a.M24,a.M31,a.M32,a.M33,a.M34,a.M41,a.M42,a.M43,a.M44];
        float[] y = [b.M11,b.M12,b.M13,b.M14,b.M21,b.M22,b.M23,b.M24,b.M31,b.M32,b.M33,b.M34,b.M41,b.M42,b.M43,b.M44];
        return x.Zip(y).All(p => float.IsFinite(p.First) && float.IsFinite(p.Second) && MathF.Abs(p.First-p.Second) <= epsilon);
    }
    public static void ValidateSkeleton(IReadOnlyList<Bone> bones,bool exportNames=true)
    {
        if (bones.Select(b => b.Name).Distinct(StringComparer.Ordinal).Count() != bones.Count)
            throw new InvalidDataException("Duplicate bone names are ambiguous.");
        for (int i = 0; i < bones.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(bones[i].Name) || (exportNames && bones[i].Name.IndexOfAny([':', '/', '@', '"']) >= 0))
                throw new InvalidDataException($"Bone name '{bones[i].Name}' is not a valid Godot 3 bone name.");
            var seen = new HashSet<int> { i };
            for (int p = bones[i].Parent; p != -1; p = bones[p].Parent)
                if (p < 0 || p >= bones.Count || !seen.Add(p)) throw new InvalidDataException("Invalid or cyclic bone hierarchy.");
            _ = Inverse(bones[i].Rest);
        }
    }
}
