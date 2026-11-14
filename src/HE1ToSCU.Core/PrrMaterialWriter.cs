using System.Text;

namespace HE1ToSCU.Core;

public sealed record PrrMaterialRecord(string Name, string Slot, string OutputType, string? SourceShader,
    string? MatchedShader, string? ShaderResource, string? FallbackReason,
    IReadOnlyDictionary<string, string> MappedParameters, IReadOnlyDictionary<string, string> Samplers,
    IReadOnlyList<string> UnmappedParameters, IReadOnlyList<string> Warnings);

public sealed class PrrExportResult
{
    public SourceGame Game { get; init; }
    public List<PrrMaterialAudit> Audits { get; } = [];
    public List<string> ValidationErrors { get; } = [];
    public void WriteAudit(string directory) => PrrParameterAuditReport.Write(this,directory);
    public void WriteExpected(string directory) => PrrParameterAuditReport.WriteExpected(this,directory);
    public void ReadBack(string directory) => PrrParameterAuditReport.ReadBack(this,directory);
    public required Dictionary<(string Material, string Slot), string> MaterialFiles { get; init; }
    public required IReadOnlyList<PrrMaterialRecord> Materials { get; init; }
    public required PrrShaderDestination Destination { get; init; }
    public Dictionary<string, string> ShaderFiles { get; } = new(StringComparer.Ordinal);
    public int ShaderMaterials => Materials.Count(m => m.OutputType == "ShaderMaterial");
    public int SpatialFallbacks => Materials.Count - ShaderMaterials;

    public void StageExternalShaders(string directory)
    {
        if (!Destination.External) return;
        foreach (var (resource, code) in ShaderFiles)
        {
            string path = Path.Combine(directory, resource[6..].Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                throw new IOException("External shader collides with an export or compiler file: " + resource);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, code, new UTF8Encoding(false));
        }
    }

    public void RemoveStagedShaders(string directory)
    {
        if (!Destination.External) return;
        foreach (string resource in ShaderFiles.Keys)
        {
            string path = Path.Combine(directory, resource[6..].Replace('/', Path.DirectorySeparatorChar));
            File.Delete(path);
            for (var parent = Directory.GetParent(path); parent != null && parent.FullName != Path.GetFullPath(directory); parent = parent.Parent)
            {
                if (Directory.EnumerateFileSystemEntries(parent.FullName).Any()) break;
                parent.Delete();
            }
        }
    }
}

public sealed class PrrMaterialWriter
{
    private static readonly Dictionary<string, string[]> SamplerAliases = new(StringComparer.Ordinal)
    {
        ["diffuse_texture"] = ["diffuse"],
        ["specular_texture"] = ["specular"],
        ["normal_texture"] = ["normal"],
        ["falloff_texture"] = ["displacement", "falloff"],
        ["environment_texture"] = ["reflection", "environment"],
        ["power_texture"] = ["gloss", "power"],
        ["gloss_texture"] = ["gloss", "power"],
        ["emission_texture"] = ["emission", "emissive"],
        ["alpha_texture"] = ["opacity", "alpha"],
        ["mask_texture"] = ["mask"],
        ["noise_texture"] = ["noise"],
        ["detail_diffuse_texture"] = ["detail_diffuse"],
        ["detail_normal_texture"] = ["detail_normal"],
        ["detail_power_texture"] = ["detail_gloss", "detail_power"],
        ["detail_gloss_texture"] = ["detail_gloss", "detail_power"],
        ["emission_mask_texture"] = ["emission_mask"],
        ["refraction_texture"] = ["refraction"]
    };

    public PrrExportResult Write(ModelAsset model, string directory, SourceGame game, PrrShaderDestination destination,
        Action<string>? log, CancellationToken token)
    {
        var keys = model.AllSurfaces.Select(s => (s.Material, s.Slot)).Distinct().ToArray();
        var records = new List<PrrMaterialRecord>();
        var matches = new Dictionary<(string, string), PrrShaderMatch>();
        var fallbacks = new List<Surface>();
        foreach (var key in keys)
        {
            var source = model.Materials?.GetValueOrDefault(key.Material);
            var match = source == null ? new PrrShaderMatch(null, "Original material file missing") :
                PrrShaderLibrary.Default.Match(game, source.Shader);
            matches[key] = match;
            if (match.Shader == null)
            {
                string reason = match.Reason!;
                log?.Invoke($"[PRR FALLBACK] Material: {key.Material} | Game: {game} | Shader: {source?.Shader ?? "(missing)"} | Reason: {reason} | Exported as SpatialMaterial");
                fallbacks.Add(model.AllSurfaces.First(s => (s.Material, s.Slot) == key));
                records.Add(new(key.Material, key.Slot, "SpatialMaterial", source?.Shader, null, null, reason,
                    new Dictionary<string, string>(), new Dictionary<string, string>(), source?.Parameters.Keys.ToArray() ?? [], []));
            }
        }
        var materialFiles = fallbacks.Count == 0 ? new Dictionary<(string, string), string>() :
            new SpatialMaterialWriter().Write(model with { Surfaces = fallbacks, MouthMeshes = [] }, directory, log, token, game);
        var result = new PrrExportResult { MaterialFiles = materialFiles, Materials = records, Destination = destination, Game=game };
        foreach(var key in keys.Where(k=>matches[k].Shader==null))
        {
            var source=model.Materials?.GetValueOrDefault(key.Material);
            var mapping=new PrrMappingResult();
            foreach(var parameter in source?.AllParameters??[])
                mapping.Parameters.Add(new(parameter.Name,parameter.Storage,parameter.Values,"intentionally_unused",
                    "PRR mapping not applicable: "+matches[key].Reason,[],"Per-material SpatialMaterial fallback","confirmed"));
            result.Audits.Add(new(){Material=key.Material,Slot=key.Slot,SourceShader=source?.Shader??"(missing)",OutputType="SpatialMaterial",
                MaterialVersion=source?.MaterialVersion,SourceFile=source?.SourcePath,SourceMaterialName=source?.Name,
                SourceHeader=source==null?null:Header(source),
                RawParameters=source?.AllParameters.ToArray()??[],Mapping=mapping,FallbackReason=matches[key].Reason,
                Textures=source?.Textures.Select(t=>new PrrTextureAudit{Source=t,Status="spatial_fallback",
                    Reason="Handled by existing SpatialMaterial fallback; not a PRR sampler binding."}).ToList()??[]});
        }
        try
        {
        Directory.CreateDirectory(Path.Combine(directory, "materials"));
        Directory.CreateDirectory(Path.Combine(directory, "textures"));
        var textures = new Dictionary<string, (string File, int MaxLod, DdsImage Image, byte[] Pixels)>(StringComparer.OrdinalIgnoreCase);
        var textureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var materialNames = new HashSet<string>(materialFiles.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys.Where(k => matches[k].Shader != null))
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetFileName(key.Material) != key.Material || key.Material.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("Invalid source material filename.");
            var source = model.Materials![key.Material];
            var shader = PrrShaderLibrary.Default.SelectPreset(matches[key].Shader!, key.Slot, source.Additive, source.DoubleSided);
            var warnings = new List<string>();
            var (reference, code) = destination.Resolve(shader, warnings);
            var mapped = PrrParameterMapper.Map(source, shader);
            var audit=new PrrMaterialAudit {Material=key.Material,Slot=key.Slot,SourceShader=source.Shader,PrrShader=shader.Path,
                RuntimeName=key.Material+(keys.Count(k=>k.Material==key.Material)>1?"_"+key.Slot:""),
                OutputType="ShaderMaterial",MaterialVersion=source.MaterialVersion,SourceFile=source.SourcePath,SourceMaterialName=source.Name,
                SourceHeader=Header(source),
                RawParameters=source.AllParameters.ToArray(),Mapping=mapped,
                Textures=source.Textures.Select(t=>new PrrTextureAudit{Source=t}).ToList()};
            result.Audits.Add(audit);
            if(mapped.Parameters.Any(p=>p.Classification is "ambiguous" or "failed_conversion"))
                throw new InvalidDataException($"{source.Name}: PRR parameter mapping failed. See prr-parameter-audit.json for classified source values and reasons.");
            var values = mapped.Values;
            var bindings = new Dictionary<string, string>();
            foreach (var uniform in shader.Uniforms.Where(u => u.Type == "sampler2D"))
            {
                if (!SamplerAliases.TryGetValue(uniform.Name, out var types)) types = []; // scene-owned unless explicitly supplied
                var candidates = source.Textures.Where(t => types.Contains(t.Type, StringComparer.OrdinalIgnoreCase) ||
                    t.Type.Equals(uniform.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length > 1)
                {
                    foreach(var candidate in candidates)
                    {
                        var ambiguous=audit.Textures.First(t=>t.Source==candidate);
                        ambiguous.Status="ambiguous";ambiguous.Sampler=uniform.Name;ambiguous.Reason="Multiple source textures target this sampler.";
                    }
                    throw new InvalidDataException($"{source.Name}: ambiguous textures for PRR sampler {uniform.Name}.");
                }
                if (candidates.Length == 0) continue;
                var texture = candidates[0];
                var textureAudit=audit.Textures.First(t=>t.Source==texture);
                textureAudit.Sampler=uniform.Name;
                if(texture.WrapU is <0 or >2 || texture.WrapV is <0 or >2 || texture.WrapU!=texture.WrapV)
                {
                    textureAudit.Status="unsupported";
                    textureAudit.Reason=$"Godot 3.1 ImageTexture flags cannot preserve mixed-axis/mirror-once/border/unknown wrap ({texture.WrapU}/{texture.WrapV}); sampler left at its shader default.";
                    warnings.Add(textureAudit.Reason);
                    continue;
                }
                string uvUniform = uniform.Name.Replace("_texture", "_uv_set", StringComparison.Ordinal);
                if (texture.UvIndex is < 0 or > 1)
                {
                    warnings.Add($"{texture.Type}: UV{texture.UvIndex} requires additional mesh UV data; sampler left at its shader default.");
                    textureAudit.Status="unsupported";textureAudit.Reason=warnings[^1];
                    continue;
                }
                if (shader.Uniforms.Any(u => u.Name == uvUniform)) Metadata(uvUniform,texture.UvIndex,"Source texture UV index.");
                else if (uniform.Name != "environment_texture" && shader.Uniforms.Any(u => u.Name == "material_uv_set"))
                {
                    string uv = texture.UvIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (values.TryGetValue("material_uv_set", out var previousUv) && previousUv != uv)
                    {
                        textureAudit.Status="ambiguous";textureAudit.Reason="Shader has one UV selector but source samplers use different UV sets.";
                        throw new NotSupportedException($"{source.Name}: {shader.Path} has one shared UV selector, but source samplers use different UV sets.");
                    }
                    Metadata("material_uv_set",texture.UvIndex,"Source textures share this shader's single UV selector.");
                }
                else if (texture.UvIndex != 0 && uniform.Name != "environment_texture")
                {
                    warnings.Add($"{texture.Type}: {shader.Path} has no independent {uvUniform}; UV2 sampler left unassigned.");
                    textureAudit.Status="unsupported";textureAudit.Reason=warnings[^1];
                    continue;
                }
                if (texture.SourcePath == null || !File.Exists(texture.SourcePath))
                {
                    warnings.Add("Missing source texture: " + texture.Name);
                    textureAudit.Status="missing";textureAudit.Reason=warnings[^1];
                    continue;
                }
                int flags = 1 | 4 | (texture.WrapU == 2 && texture.WrapV == 2 ? 0 : 2) |
                    (texture.WrapU == 1 && texture.WrapV == 1 ? 32 : 0);
                // PRR performs its own transfer and packed normal decoding. Every sampler receives raw DDS RGB(A).
                bool cube = uniform.Name == "environment_texture" && shader.Uniforms.Any(u => u.Name == "environment_max_lod");
                string identity = Path.GetFullPath(texture.SourcePath) + "|" + flags + "|" + cube;
                if (!textures.TryGetValue(identity, out var exported))
                {
                    var image = PrrTextureReader.Read(texture.SourcePath, cube, token);
                    string stem = Path.GetFileNameWithoutExtension(texture.Name.Replace('\\', '/'));
                    if (stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || stem.Length == 0)
                        throw new InvalidDataException("Invalid PRR texture filename: " + texture.Name);
                    // A mixed SpatialMaterial can contain a processed version of the same DDS.
                    // Keep raw PRR textures separate so neither writer can overwrite the other's data.
                    string output = $"textures/prr/{stem}.texture.tres";
                    if (!textureNames.Add(output))
                    {
                        string suffix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.MipData))[..12] + "_" + flags;
                        output = $"textures/prr/{stem}_prr_{suffix}.texture.tres";
                        textureNames.Add(output);
                    }
                    Directory.CreateDirectory(Path.Combine(directory, "textures", "prr"));
                    TextureResourceWriter.Write(Path.Combine(directory, output), stem, image.Image, flags, token, image.MipData);
                    exported = (output, image.SourceMaxLod, image.Image, image.MipData);
                    textures.Add(identity, exported);
                }
                bindings[uniform.Name] = exported.File;
                textureAudit.Status="mapped";textureAudit.Reason="Explicit texture role / declared sampler binding.";
                textureAudit.Resource=exported.File;textureAudit.Width=exported.Image.Width;textureAudit.Height=exported.Image.Height;
                textureAudit.Flags=flags;textureAudit.Pixels=exported.Pixels;
                textureAudit.PixelSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(exported.Pixels)).ToLowerInvariant();
                if (cube) Metadata("environment_max_lod",exported.MaxLod,"Last original cube face mip level; mixed atlas tails must not be sampled.");
            }
            foreach (var texture in source.Textures.Where(t =>
                !SamplerAliases.Values.SelectMany(types => types).Contains(t.Type, StringComparer.OrdinalIgnoreCase) &&
                !shader.Uniforms.Any(u => u.Type == "sampler2D" && u.Name.Equals(t.Type, StringComparison.OrdinalIgnoreCase))))
            {
                warnings.Add($"Unmapped source texture role: {texture.Type} ({texture.Name}).");
                var unbound=audit.Textures.First(t=>t.Source==texture);
                unbound.Status="missing_mapping";unbound.Reason=warnings[^1];
            }
            if (!result.ShaderFiles.TryAdd(reference, code) && result.ShaderFiles[reference] != code)
                throw new InvalidDataException("PRR shader reference collision: " + reference);
            if (!destination.External)
            {
                string path = Path.Combine(directory, reference);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, code, new UTF8Encoding(false));
            }
            string file = "materials/" + key.Material + (keys.Count(k => k.Material == key.Material) > 1 ? "_" + key.Slot : "") + ".tres";
            if (Path.GetFileName(file) != file[10..] || Path.GetFileName(file).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !materialNames.Add(file))
                throw new InvalidDataException("Invalid or colliding PRR material filename: " + file);
            var text = new StringBuilder($"[gd_resource type=\"ShaderMaterial\" load_steps={bindings.Count + 2} format=2]\n\n");
            string shaderPath = destination.External ? reference : "../" + reference;
            text.AppendLine($"[ext_resource path={ExportNames.Literal(shaderPath)} type=\"Shader\" id=1]");
            int id = 2;
            foreach (string texture in bindings.Values)
                text.AppendLine($"[ext_resource path={ExportNames.Literal("../" + texture)} type=\"Texture\" id={id++}]");
            text.AppendLine($"\n[resource]\nresource_name = {ExportNames.Literal(audit.ResourceName)}\nshader = ExtResource( 1 )");
            foreach (var (name, value) in values) text.AppendLine($"shader_param/{name} = {value}");
            id = 2;
            foreach (string sampler in bindings.Keys) text.AppendLine($"shader_param/{sampler} = ExtResource( {id++} )");
            File.WriteAllText(Path.Combine(directory, file), text.ToString(), new UTF8Encoding(false));
            materialFiles.Add(key, file);
            records.Add(new(key.Material, key.Slot, "ShaderMaterial", source.Shader, shader.Path, shaderPath, null,
                mapped.Mappings, bindings, mapped.Unmapped, warnings));
            foreach (string warning in warnings) log?.Invoke($"[PRR] {key.Material}: {warning}");
            void Metadata(string name,double value,string evidence)
            {
                var uniform=shader.Uniforms.Single(u=>u.Name==name);
                var converted=PrrUniformValue.Convert(uniform,[value,0,0,0]);
                if(mapped.Values.ContainsKey(name))
                {
                    if(!mapped.Assignments.Single(a=>a.Name==name).Expected.Components.SequenceEqual(converted.Components))
                        throw new InvalidDataException(source.Name+": authored parameter conflicts with texture metadata: "+name);
                    return;
                }
                mapped.Assign(uniform,converted,"TextureMetadata",null,evidence,"source_metadata");
            }
        }
        log?.Invoke($"PRR: {result.ShaderMaterials} ShaderMaterials, {result.SpatialFallbacks} SpatialMaterial fallbacks, " +
            $"{result.ShaderFiles.Count} unique shaders ({(destination.External ? "external" : "bundled")}), " +
            $"{result.Audits.Sum(a=>a.Mapping.Parameters.Count(p=>p.Classification=="mapped"))} mapped source parameters, " +
            $"{result.Audits.Sum(a=>a.Mapping.Parameters.Count(p=>p.Classification=="intentionally_unused"))} intentionally unused, " +
            $"{result.Audits.Sum(a=>a.Mapping.Parameters.Count(p=>p.Classification is "missing_mapping" or "unsupported"))} missing/unsupported mappings (details: prr-parameter-audit.json), " +
            $"{records.Sum(r => r.Warnings.Count(w => w.StartsWith("Missing source texture:", StringComparison.Ordinal)))} missing textures, " +
            $"{records.Count(r => r.FallbackReason == "Matching PRR shader unavailable")} missing shaders, {records.Sum(r => r.Warnings.Count)} warnings.");
        return result;
        }
        catch(Exception e) when(e is not OperationCanceledException)
        {
            result.ValidationErrors.Add(e.Message);
            throw;
        }
        finally { result.WriteAudit(directory); }
    }

    private static object Header(He1Material source)=>new {source.AlphaThresholdByte,source.AlphaThreshold,source.DoubleSided,source.Additive,source.SubShader,source.UnknownFlag};
}
