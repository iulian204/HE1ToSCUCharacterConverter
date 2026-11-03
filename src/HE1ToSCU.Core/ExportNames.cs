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
    public static string SceneText(ModelAsset model) => File(model.Name, ".tscn");
    public static string SceneBinary(ModelAsset model) => File(model.Name, ".scn");
    public static string MeshText(ModelAsset model) => File(model.Name, ".mesh.tres");
    public static string MeshBinary(ModelAsset model) => File(model.Name, ".mesh");
    public static string AnimationText(AnimationClip clip) => File(clip.Name, ".tres");
    public static string AnimationBinary(AnimationClip clip) => File(clip.Name, ".anim");
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
        foreach (var name in new[] { SceneText(model), SceneBinary(model), MeshText(model), MeshBinary(model), "conversion.json", "acorn-validation.log", "acorn-binary-validation.log" }) Add(name);
        foreach (var clip in clips) { Add(AnimationText(clip)); Add(AnimationBinary(clip)); }
    }
}
