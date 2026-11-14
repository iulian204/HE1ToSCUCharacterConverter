using System.Text.Json;
using System.Text.RegularExpressions;

namespace HE1ToSCU.Core;

public sealed record PrrUniform(string Type, string Name, string Declaration)
{
    public string? Hint => Regex.Match(Declaration, @":\s*(\w+(?:\s*\([^)]*\))?)") is { Success: true } m ? m.Groups[1].Value : null;
    public string? DefaultValue => Declaration.IndexOf('=') is int i && i >= 0 ? Declaration[(i + 1)..].Trim() : null;
    public string? ArrayDeclaration => Regex.Match(Declaration, @"\[[^\]]*\]") is { Success: true } m ? m.Value : null;
    public bool IsColor => Hint == "hint_color";
}
public sealed record PrrShader(string Path, string Code, IReadOnlyList<PrrUniform> Uniforms);
public sealed record PrrShaderMatch(PrrShader? Shader, string? Reason);

/// <summary>The two current user-supplied PRR collections, embedded for portable publishing.</summary>
public sealed class PrrShaderLibrary
{
    private static readonly Lazy<PrrShaderLibrary> Shared = new(() => new PrrShaderLibrary());
    public static PrrShaderLibrary Default => Shared.Value;
    private readonly Dictionary<string, string> resources;
    private readonly Dictionary<string, string[]> aliases;
    private readonly Dictionary<string, PrrShader> parsed = new(StringComparer.Ordinal);
    public IReadOnlyList<string> Paths { get; }
    public IReadOnlyList<string> PresetPaths { get; }

    public PrrShaderLibrary()
    {
        var assembly = typeof(PrrShaderLibrary).Assembly;
        resources = assembly.GetManifestResourceNames().Where(n => n.StartsWith("PRR/", StringComparison.Ordinal))
            .ToDictionary(n => n[4..].Replace('\\', '/'), n => n, StringComparer.Ordinal);
        string Read(string path)
        {
            using var stream = assembly.GetManifestResourceStream(resources[path])!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        aliases = JsonSerializer.Deserialize<Dictionary<string, string[]>>(Read("bindings.json"))!;
        PresetPaths = resources.Keys.Where(p => p.EndsWith(".shader", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        Paths = PresetPaths.Where(p => p.StartsWith("Generations/", StringComparison.Ordinal) || p.StartsWith("Unleashed/", StringComparison.Ordinal)).ToArray();
    }

    public PrrShader SelectPreset(PrrShader canonical, string slot, bool additive, bool doubleSided)
    {
        if (!Paths.Contains(canonical.Path)) throw new ArgumentException("Expected a canonical PRR shader.", nameof(canonical));
        string coverage = slot switch
        {
            "opaque" => "Opaque", "punch" => "Punch", "transparent" => "Trans",
            _ => throw new InvalidDataException("Unknown HE1 material coverage slot: " + slot)
        };
        if (coverage == "Opaque" && !additive && !doubleSided) return canonical;
        return Get($"Render States/{coverage}/{(additive ? "Additive" : "Normal")}/{(doubleSided ? "DoubleSided" : "SingleSided")}/{canonical.Path}");
    }

    public PrrShader Get(string path)
    {
        lock (parsed)
        {
            if (parsed.TryGetValue(path, out var shader)) return shader;
            using var stream = typeof(PrrShaderLibrary).Assembly.GetManifestResourceStream(resources[path])!;
            using var reader = new StreamReader(stream);
            string code = reader.ReadToEnd();
            shader = new(path, code, ParseUniforms(code));
            parsed.Add(path, shader);
            return shader;
        }
    }

    public static IReadOnlyList<PrrUniform> ParseUniforms(string code)
    {
        string clean = Regex.Replace(code, @"/\*[\s\S]*?\*/|//[^\r\n]*", "");
        if (!Regex.IsMatch(clean, @"\bshader_type\s+spatial\s*;"))
            throw new InvalidDataException("PRR shader must declare shader_type spatial.");
        if (Regex.IsMatch(clean, @"#\s*include"))
            throw new NotSupportedException("Godot 3.1 does not support shader #include. Supply a standalone PRR shader.");
        var result = new List<PrrUniform>();
        foreach (Match statement in Regex.Matches(clean, @"\buniform\b([^;]*);"))
        {
            var m = Regex.Match(statement.Groups[1].Value, @"^\s*(?:(?:lowp|mediump|highp)\s+)?(\w+)\s+(\w+)\s*([\s\S]*)$");
            if (!m.Success) throw new InvalidDataException("Unsupported uniform declaration: " + statement.Value);
            string tail = m.Groups[3].Value.Trim();
            if (tail.Length > 0 && tail[0] is not ('[' or ':' or '='))
                throw new InvalidDataException("Unsupported uniform declaration form: " + statement.Value);
            var uniform = new PrrUniform(m.Groups[1].Value, m.Groups[2].Value, tail);
            if (result.Any(u => u.Name == uniform.Name)) throw new InvalidDataException("Duplicate shader uniform: " + uniform.Name);
            result.Add(uniform);
        }
        if (Regex.Matches(clean, @"\buniform\b").Count != result.Count)
            throw new InvalidDataException("Unterminated shader uniform declaration.");
        return result;
    }

    public PrrShaderMatch Match(SourceGame game, string identifier)
    {
        string folder = game == SourceGame.SonicUnleashed ? "Unleashed/" : "Generations/";
        string name = identifier;
        foreach (string extension in new[] { ".shader-list", ".wpu.hlsl", ".shader", ".hlsl" })
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) { name = name[..^extension.Length]; break; }
        // Preserve permutation and bracket distinctions. The alias table comes from the current source-list manifest.
        var exact = Paths.Where(p => p == folder + name + ".shader").ToArray();
        if (exact.Length == 1) return new(Get(exact[0]), null);
        var candidates = Paths.Where(p => p.Equals(folder + name + ".shader", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length == 0)
            candidates = aliases.Where(a => a.Key.Equals(folder + name, StringComparison.OrdinalIgnoreCase))
                .SelectMany(a => a.Value).Where(resources.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0 && !name.Contains('@') && !name.Contains('['))
            candidates = Paths.Where(p => p.Equals(folder + name + "@@.shader", StringComparison.OrdinalIgnoreCase)).ToArray();
        return candidates.Length switch
        {
            1 => new(Get(candidates[0]), null),
            0 => new(null, "Matching PRR shader unavailable"),
            _ => new(null, "Ambiguous source-list mapping: " + string.Join(", ", candidates))
        };
    }
}
