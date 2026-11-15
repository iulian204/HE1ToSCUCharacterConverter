using System.Text.Json;

namespace HE1ToSCU.Core;

/// <summary>One naming contract shared by text writing, native compilation and verification.</summary>
public static class ExportNames
{
    public static string AnimationStem(string sourcePath)
    {
        string stem = Path.GetFileNameWithoutExtension(sourcePath);
        return stem.EndsWith(".anm", StringComparison.OrdinalIgnoreCase) ? stem[..^4] : stem;
    }
    public static string SceneText(ModelAsset model) => File(model.ExportIdentity?.SceneStem??model.Name, ".tscn");
    public static string SceneBinary(ModelAsset model) => model.ExportIdentity?.NativeSceneFile is{} file?File(file,""):File(model.ExportIdentity?.SceneStem??model.Name, ".scn");
    public static string MeshStem(ModelAsset model,string nodeName="Mesh")=>model.ExportIdentity is{} names?
        nodeName=="Mesh"||nodeName==names.MeshNodeName?names.MeshResourceName:names.SceneStem+"_"+nodeName:model.Name+(nodeName=="Mesh"?"":"_"+nodeName);
    public static string MeshText(ModelAsset model, string nodeName = "Mesh") => File(MeshStem(model,nodeName), ".mesh.tres");
    public static string MeshBinary(ModelAsset model, string nodeName = "Mesh") => File(MeshStem(model,nodeName), ".mesh");
    public static string AnimationName(AnimationClip clip)=>clip.ExportIdentity?.PlayerKey??clip.Name;
    public static bool AnimationLoops(AnimationClip clip)=>clip.Name.Contains("loop",StringComparison.OrdinalIgnoreCase);
    public static string SkeletonNode(ModelAsset model)=>model.ExportIdentity?.SkeletonNodeName??"Skeleton";
    public static string AnimationText(AnimationClip clip) => (clip.ExportIdentity==null?"":"animations/")+File(clip.ExportIdentity?.FileStem??clip.Name, ".tres");
    public static string AnimationBinary(AnimationClip clip) => (clip.ExportIdentity==null?"":"animations/")+File(clip.ExportIdentity?.FileStem??clip.Name, ".anim");
    public static void ValidateStem(string stem)=>_ = File(stem,"");
    internal static string Literal(string text) => JsonSerializer.Serialize(text);
    internal static string AnimationPaths(IReadOnlyList<AnimationClip> clips) =>
        "[" + string.Join(", ", clips.Select(c => Literal("res://" + AnimationBinary(c)))) + "]";

    private static string File(string stem, string extension)
    {
        if (string.IsNullOrWhiteSpace(stem) || stem is "." or ".." || stem.EndsWith('.') || stem.EndsWith(' ') ||
            stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || stem.IndexOfAny(['/', '\\']) >= 0)
            throw new InvalidDataException($"Cannot preserve invalid output filename '{stem}'.");
        return stem + extension;
    }

    public static void Validate(ModelAsset model, IReadOnlyList<AnimationClip> clips)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name)
        {
            if (!files.Add(name)) throw new InvalidDataException($"Output filename collision: {name}. Original names are preserved; select uniquely named assets.");
        }
        foreach (var name in new[] { SceneText(model), SceneBinary(model), "conversion.json", "acorn-validation.log", "acorn-binary-validation.log", "scu-animation-mapping.txt", "scu-animation-mapping.json" }) Add(name);
        foreach (var part in model.MeshParts) { Add(MeshText(model, part.Name)); Add(MeshBinary(model, part.Name)); }
        foreach (var clip in clips) { Add(AnimationText(clip)); Add(AnimationBinary(clip)); }
    }
}
