using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>Bake an X reflection into a second mouth and a separate animated bone branch.
/// Mouth-root transforms are mirrored in the shared parent's bind frame; character root motion stays shared.</summary>
public static class MirroredMouth
{
    private static readonly Matrix4x4 Reflection = Matrix4x4.CreateScale(-1, 1, 1);

    public static (ModelAsset Model, List<AnimationClip> Clips, int GeneratedBones) Create(
        ModelAsset model, List<AnimationClip> clips, CancellationToken token = default, Action<string>? log = null)
    {
        model = MouthMeshSplitter.Split(model, token, log);
        var mouths = model.MouthMeshes!;
        if (mouths.Any(p => p.Name.Equals("Mouth_L", StringComparison.OrdinalIgnoreCase)))
        {
            log?.Invoke("Mouth_L already exists; retaining the authored mouth and bones.");
            return (model, clips, 0);
        }
        if (mouths.Count == 0)
        {
            log?.Invoke("No weighted mouth geometry found; Mouth_L generation skipped.");
            return (model, clips, 0);
        }
        var mouth = mouths.FirstOrDefault(p => p.Name == "Mouth") ??
            (mouths.Count == 1 ? mouths[0] : throw new InvalidDataException("Cannot identify the primary mouth to mirror."));
        int root = model.Bones.FindIndex(b => b.Name == mouth.MouthRootBone);
        if (root < 0) throw new InvalidDataException("The mouth mesh has no matching root bone.");
        int originalCount = model.Bones.Count;
        bool InBranch(int bone)
        {
            for (int parent = bone; parent >= 0; parent = model.Bones[parent].Parent) if (parent == root) return true;
            return false;
        }
        int[] branch = Enumerable.Range(0, originalCount).Where(InBranch).ToArray();
        var mapping = branch.Select((source, i) => (source, target: originalCount + i)).ToDictionary(p => p.source, p => p.target);
        var names = model.Bones.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (int source in branch)
            if (!names.Add(model.Bones[source].Name + "_L")) throw new InvalidDataException($"Cannot generate Mouth_L: bone '{model.Bones[source].Name}_L' already exists.");

        var globals = new Matrix4x4?[originalCount];
        Matrix4x4 Bind(int bone) => globals[bone] ?? (globals[bone] = model.Bones[bone].Parent < 0 ?
            model.Bones[bone].Rest : model.Bones[bone].Rest * Bind(model.Bones[bone].Parent)).Value;
        int rootParent = model.Bones[root].Parent;
        var parentBind = rootParent < 0 ? Matrix4x4.Identity : Bind(rootParent);
        // The parent (normally Head) keeps its authored axes and animation. A local-X
        // reflection would be wrong when its local axes differ from model-space X.
        var parentReflection = parentBind * Reflection * Spatial.Inverse(parentBind);
        var bones = model.Bones.ToList();
        foreach (int source in branch)
        {
            token.ThrowIfCancellationRequested();
            var bone = model.Bones[source];
            var right = source == root ? parentReflection : Reflection;
            bones.Add(new Bone(bone.Name + "_L", mapping.GetValueOrDefault(bone.Parent, bone.Parent), Reflection * bone.Rest * right));
        }
        Spatial.ValidateSkeleton(bones);

        Vector3 Reflect(Vector3 value) => new(-value.X, value.Y, value.Z);
        var surfaces = new List<Surface>();
        foreach (var surface in mouth.Surfaces)
        {
            token.ThrowIfCancellationRequested();
            var vertices = surface.Vertices.Select(vertex => vertex with
            {
                Position = Reflect(vertex.Position), Normal = Reflect(vertex.Normal),
                Tangent = new Vector4(-vertex.Tangent.X, vertex.Tangent.Y, vertex.Tangent.Z, -vertex.Tangent.W),
                Bones = vertex.Bones.Select(b => mapping.GetValueOrDefault(b, b)).ToArray()
            }).ToList();
            var indices = surface.Indices.ToList();
            // A reflection reverses face orientation. Swap winding as well as tangent
            // handedness; do not hide inside-out faces by disabling backface culling.
            for (int i = 0; i < indices.Count; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            surfaces.Add(new Surface(surface.Material, surface.Slot, vertices, indices));
        }

        var reference = model.Bones.Select(b => Pose.FromMatrix(b.Rest)).ToArray();
        var converted = new List<AnimationClip>();
        foreach (var clip in clips)
        {
            Pose[] Expand(Pose[] tracks)
            {
                token.ThrowIfCancellationRequested();
                var local = new Pose[bones.Count]; Array.Copy(reference, local, originalCount);
                for (int track = 0; track < clip.TrackToBone.Length; track++) local[clip.TrackToBone[track]] = tracks[track];
                foreach (int source in branch)
                {
                    var pose = local[source];
                    var rigid = pose with { Scale = Vector3.One };
                    var reflected = Pose.FromMatrix(Reflection * rigid.Matrix * (source == root ? parentReflection : Reflection));
                    // Preserve authored signs and zero crossings without decomposing
                    // a singular scaled matrix (notably sonic_mouth_reverse).
                    local[mapping[source]] = reflected with { Scale = reflected.Scale * pose.Scale };
                }
                return local;
            }
            converted.Add(clip with
            {
                Frames = clip.Frames.Select(Expand).ToArray(), TrackToBone = Enumerable.Range(0, bones.Count).ToArray(),
                SourceSampler = time => Expand(AnimationSampling.Sample(clip, time))
            });
        }
        var mirrored = new MeshPart("Mouth_L", surfaces, bones[mapping[root]].Name, Mirrored: true);
        log?.Invoke($"Generated Mouth_L with {branch.Length} mirrored _L bones and mirrored tracks in {clips.Count} animations.");
        return (model with { Bones = bones, MouthMeshes = mouths.Append(mirrored).ToArray() }, converted, branch.Length);
    }
}
