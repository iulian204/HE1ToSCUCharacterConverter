using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>Havok composes quaternion, scale and translation independently. A plain
/// matrix hierarchy adds shear when a scaled parent has a rotated child. Compensation
/// bones remove parent scale before applying each child's model-space scale.</summary>
public static class AnimationRig
{
    public static Pose Compose(Pose local, Pose parent) => new(
        parent.Translation + Vector3.Transform(local.Translation * parent.Scale, parent.Rotation),
        Quaternion.Normalize(Quaternion.Concatenate(local.Rotation, parent.Rotation)),
        local.Scale * parent.Scale);

    public static Pose[] ModelPose(IReadOnlyList<Bone> bones, IReadOnlyList<Pose> locals)
    {
        var result = new Pose?[bones.Count];
        Pose Get(int i) => result[i] ?? (result[i] = bones[i].Parent < 0 ? locals[i] : Compose(locals[i], Get(bones[i].Parent)));
        return Enumerable.Range(0, bones.Count).Select(Get).ToArray();
    }

    private static Vector3 Reciprocal(Vector3 scale)
    {
        if (MathF.Abs(scale.X) < 1e-7f || MathF.Abs(scale.Y) < 1e-7f || MathF.Abs(scale.Z) < 1e-7f)
            throw new InvalidDataException("A zero bone scale cannot be compensated.");
        return Vector3.One / scale;
    }

    public static (ModelAsset Model, List<AnimationClip> Clips, int Helpers) Prepare(
        ModelAsset model, List<AnimationClip> clips, CancellationToken token)
    {
        bool Uniform(Vector3 s) => MathF.Abs(s.X - s.Y) < 1e-5f && MathF.Abs(s.Y - s.Z) < 1e-5f;
        if (!clips.Any(c => c.Frames.Any(frame => frame.Any(p => !Uniform(p.Scale)))))
            return (model, clips, 0);

        int count = model.Bones.Count;
        var rests = model.Bones.Select(b => Pose.FromMatrix(b.Rest)).ToArray();
        var bindMatrices = new Matrix4x4?[count];
        Matrix4x4 Bind(int i) => bindMatrices[i] ?? (bindMatrices[i] = model.Bones[i].Parent < 0 ? model.Bones[i].Rest : model.Bones[i].Rest * Bind(model.Bones[i].Parent)).Value;
        var bindPoses = Enumerable.Range(0, count).Select(i => Pose.FromMatrix(Bind(i))).ToArray();
        var bones = model.Bones.ToList();
        var helpers = new Dictionary<int, int>();
        var usedNames = model.Bones.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            int parent = model.Bones[i].Parent;
            if (parent < 0) continue;
            string name = model.Bones[i].Name + "_ScaleCompensation";
            while (!usedNames.Add(name)) name += "_";
            int helper = bones.Count;
            helpers.Add(i, helper);
            var helperRest = Matrix4x4.CreateScale(Reciprocal(bindPoses[parent].Scale));
            bones.Add(new Bone(name, parent, helperRest));
            bones[i] = bones[i] with { Parent = helper, Rest = Bind(i) * Spatial.Inverse(helperRest * Bind(parent)) };
        }
        Spatial.ValidateSkeleton(bones);
        var converted = new List<AnimationClip>();
        foreach (var clip in clips)
        {
            var frames = new Pose[clip.Frames.Length][];
            for (int f = 0; f < frames.Length; f++)
            {
                token.ThrowIfCancellationRequested();
                var local = rests.ToArray();
                for (int t = 0; t < clip.TrackToBone.Length; t++) local[clip.TrackToBone[t]] = clip.Frames[f][t];
                var world = ModelPose(model.Bones, local);
                frames[f] = new Pose[bones.Count];
                for (int i = 0; i < count; i++)
                {
                    int parent = model.Bones[i].Parent;
                    if (parent < 0) { frames[f][i] = world[i]; continue; }
                    var compensation = new Pose(Vector3.Zero, Quaternion.Identity, Reciprocal(world[parent].Scale));
                    frames[f][helpers[i]] = compensation;
                    frames[f][i] = Pose.FromMatrix(world[i].Matrix * Spatial.Inverse(compensation.Matrix * world[parent].Matrix));
                }
            }
            converted.Add(clip with { Frames = frames, TrackToBone = Enumerable.Range(0, bones.Count).ToArray() });
        }
        return (model with { Bones = bones }, converted, helpers.Count);
    }
}
