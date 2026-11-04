using System.Text.RegularExpressions;

namespace HE1ToSCU.Core;

/// <summary>Partition complete triangles by independent mouth bone branches.
/// Bone indexes, weights and all vertex attributes stay unchanged.</summary>
public static class MouthMeshSplitter
{
    private static bool MouthBone(string name)
    {
        string words = Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        return Regex.Split(words, @"[^a-zA-Z]+").Any(word =>
            word.StartsWith("mouth", StringComparison.OrdinalIgnoreCase) || word.StartsWith("jaw", StringComparison.OrdinalIgnoreCase) ||
            word.Equals("lip", StringComparison.OrdinalIgnoreCase) || word.Equals("lips", StringComparison.OrdinalIgnoreCase) ||
            word.Equals("teeth", StringComparison.OrdinalIgnoreCase) || word.Equals("tooth", StringComparison.OrdinalIgnoreCase) ||
            word.Equals("tongue", StringComparison.OrdinalIgnoreCase));
    }

    public static ModelAsset Split(ModelAsset model, CancellationToken token = default, Action<string>? log = null)
    {
        if (model.MouthMeshes != null) return model;
        Spatial.ValidateSkeleton(model.Bones);
        var related = model.Bones.Select(bone => MouthBone(bone.Name)).ToArray();
        var groups = new int[model.Bones.Count]; Array.Fill(groups, -1);
        var roots = new List<int>();
        // The highest mouth-related ancestor owns its whole branch. In particular,
        // Lip_L/Lip_R within one mouth do not become two independent mouths.
        for (int bone = 0; bone < model.Bones.Count; bone++)
        {
            int root = -1;
            for (int ancestor = bone; ancestor >= 0; ancestor = model.Bones[ancestor].Parent)
                if (related[ancestor]) root = ancestor;
            if (root < 0) continue;
            if (!roots.Contains(root)) roots.Add(root);
            groups[bone] = roots.IndexOf(root);
        }
        if (roots.Count == 0) return model with { MouthMeshes = [] };

        var surfaces = Enumerable.Range(0, roots.Count + 1).Select(_ => new List<Surface>()).ToArray();
        foreach (var surface in model.Surfaces)
        {
            token.ThrowIfCancellationRequested();
            if (surface.Indices.Count % 3 != 0) throw new InvalidDataException("Mouth partition requires complete triangles.");
            var triangles = Enumerable.Range(0, roots.Count + 1).Select(_ => new List<int>()).ToArray();
            for (int triangle = 0; triangle < surface.Indices.Count; triangle += 3)
            {
                var weights = new float[roots.Count]; float total = 0;
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = surface.Indices[triangle + corner];
                    if (index < 0 || index >= surface.Vertices.Count) throw new InvalidDataException("Mouth partition found an invalid vertex index.");
                    var vertex = surface.Vertices[index];
                    for (int influence = 0; influence < 4; influence++)
                    {
                        float weight = vertex.Weights[influence]; if (weight <= 0) continue;
                        int bone = vertex.Bones[influence];
                        if (bone < 0 || bone >= groups.Length) throw new InvalidDataException("Mouth partition found an invalid weighted bone index.");
                        total += weight;
                        if (groups[bone] >= 0) weights[groups[bone]] += weight;
                    }
                }
                int strongest = 0;
                for (int group = 1; group < weights.Length; group++) if (weights[group] > weights[strongest]) strongest = group;
                // Majority influence avoids pulling neighboring head geometry into
                // the mouth because of a tiny blend weight. Assign each face once.
                int target = total > 0 && weights[strongest] > total * .5f ? strongest + 1 : 0;
                triangles[target].AddRange(surface.Indices.GetRange(triangle, 3));
            }
            for (int target = 0; target < triangles.Length; target++)
            {
                var indices = triangles[target]; if (indices.Count == 0) continue;
                if (indices.Count == surface.Indices.Count) { surfaces[target].Add(surface); continue; }
                var vertices = new List<Vertex>(); var remap = new Dictionary<int, int>(); var compact = new List<int>(indices.Count);
                foreach (int index in indices)
                {
                    if (!remap.TryGetValue(index, out int mapped)) { mapped = vertices.Count; remap[index] = mapped; vertices.Add(surface.Vertices[index]); }
                    compact.Add(mapped);
                }
                surfaces[target].Add(new Surface(surface.Material, surface.Slot, vertices, compact));
            }
        }
        var mouths = new List<MeshPart>(); var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mesh" };
        for (int group = 0; group < roots.Count; group++)
        {
            if (surfaces[group + 1].Count == 0) continue; // Unweighted locator bones are not extra mouths.
            string root = model.Bones[roots[group]].Name;
            string name = root.StartsWith("MouthReference", StringComparison.OrdinalIgnoreCase) ? "Mouth" + root[14..] :
                root.StartsWith("Mouth", StringComparison.OrdinalIgnoreCase) ? root : "Mouth_" + root;
            string stem = name; int suffix = 2; while (!used.Add(name)) name = stem + "_" + suffix++;
            mouths.Add(new MeshPart(name, surfaces[group + 1], root));
            log?.Invoke($"Separated {name}: {surfaces[group + 1].Sum(s => s.Indices.Count / 3):N0} triangles, bone branch {root}.");
        }
        return mouths.Count == 0 ? model with { MouthMeshes = [] } : model with { Surfaces = surfaces[0], MouthMeshes = mouths };
    }
}
