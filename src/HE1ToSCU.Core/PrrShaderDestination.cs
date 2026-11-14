namespace HE1ToSCU.Core;

/// <summary>Validates a destination resource directory independently of the temporary engine project.</summary>
public sealed class PrrShaderDestination
{
    public string? ResourceDirectory { get; }
    private readonly string? diskDirectory;
    public bool External => ResourceDirectory != null;

    public PrrShaderDestination(string? path, string exportDirectory, bool compileBinary)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = path.Trim().Replace('\\', '/');
        string? root = FindProjectRoot(exportDirectory);
        if (path.StartsWith("res://", StringComparison.Ordinal))
        {
            string suffix = path[6..].TrimEnd('/');
            ValidateSegments(suffix);
            ResourceDirectory = "res://" + suffix;
            if (root != null) diskDirectory = Path.Combine(root, suffix.Replace('/', Path.DirectorySeparatorChar));
        }
        else
        {
            if (!Path.IsPathFullyQualified(path))
                throw new ArgumentException("Choose shader path must be a res:// directory or an absolute folder inside a Godot project.");
            diskDirectory = Path.GetFullPath(path);
            root = FindProjectRoot(diskDirectory) ??
                throw new ArgumentException("The selected shader folder has no parent project.godot. Select a folder inside the destination Godot project.");
            string relative = Path.GetRelativePath(root, diskDirectory).Replace('\\', '/');
            ResourceDirectory = "res://" + (relative == "." ? "" : relative.TrimEnd('/'));
        }
        if (diskDirectory != null && !Directory.Exists(diskDirectory))
            throw new DirectoryNotFoundException("External PRR shader directory does not exist: " + diskDirectory);
        if (compileBinary && diskDirectory == null)
            throw new ArgumentException("Acorn compilation needs access to the external PRR shaders. Export inside that Godot project or choose its shader folder on disk.");
    }

    public (string Reference, string Code) Resolve(PrrShader shader, List<string> warnings)
    {
        // Retain the Release hierarchy, including game identity and native draw-state folders.
        string relative = shader.Path;
        if (!External) return ("shaders/" + relative, shader.Code);
        if (diskDirectory != null)
        {
            // Also accept a directly selected game/preset folder or an older flat installation.
            string direct = Path.GetFileName(shader.Path);
            if (!File.Exists(Path.Combine(diskDirectory, relative.Replace('/', Path.DirectorySeparatorChar))) &&
                File.Exists(Path.Combine(diskDirectory, direct))) relative = direct;
        }
        string resource = ResourceDirectory! + (ResourceDirectory!.EndsWith('/') ? "" : "/") + relative;
        if (diskDirectory == null)
        {
            warnings.Add("External shader existence could not be checked without a local project: " + resource);
            return (resource, shader.Code);
        }
        string file = Path.Combine(diskDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(file)) throw new FileNotFoundException("External PRR shader is missing: " + resource +
            ". Choose the new PRR Release root, or export once with Choose shader path empty and install the used shader hierarchy in the destination directory.", file);
        string code = File.ReadAllText(file);
        var uniforms = PrrShaderLibrary.ParseUniforms(code).ToDictionary(u => u.Name, StringComparer.Ordinal);
        foreach (var uniform in shader.Uniforms)
            if (!uniforms.TryGetValue(uniform.Name, out var found) || found.Type != uniform.Type)
                throw new InvalidDataException($"External shader {resource} has an incompatible uniform {uniform.Name}. Expected {uniform.Type}.");
        // The filename selects a concrete lighting variant. A different program must be reviewed explicitly.
        if (code.Replace("\r\n", "\n").Trim() != shader.Code.Replace("\r\n", "\n").Trim())
            throw new InvalidDataException("External PRR shader differs from the bundled variant: " + resource + ". Use the matching supplied shader or leave Choose shader path empty.");
        return (resource, code);
    }

    public static string ResourcePathFromFolder(string folder) =>
        new PrrShaderDestination(folder, folder, false).ResourceDirectory!;

    private static string? FindProjectRoot(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "project.godot"))) return directory.FullName;
        return null;
    }

    private static void ValidateSegments(string suffix)
    {
        if (suffix.Length == 0) return;
        if (suffix.Split('/').Any(s => s.Length == 0 || s is "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("Invalid Godot shader directory. Use res:// followed by project-relative folder names, without '..'.");
    }
}
