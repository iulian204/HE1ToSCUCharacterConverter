using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HE1ToSCU.Core;

public sealed class PrrTextureAudit
{
    public required He1Texture Source { get; init; }
    public string? Sampler { get; set; }
    public string Status { get; set; } = "intentionally_unused";
    public string Reason { get; set; } = "Selected shader has no corresponding sampler.";
    public string? Resource { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Flags { get; set; }
    public string? PixelSha256 { get; set; }
    [JsonIgnore] public string? ExpectedFile { get; set; }
    public string ChannelPolicy => "Original DDS RGBA and mip bytes; no SpatialMaterial normal/gloss/color transformation.";
    public bool WrapModesPreserved => Source.WrapU==Source.WrapV && Source.WrapU is >=0 and <=2;
    public string FilteringPolicy => "HE1 texture entry has no filter field; linear filtering and mipmaps are explicit converter policy.";
    public Dictionary<string,PrrReadback> Actual { get; } = new();
    [JsonIgnore] public byte[]? Pixels { get; set; }
}
public sealed class PrrMaterialAudit
{
    public string? RuntimeName { get; init; }
    public string ResourceName => RuntimeName ?? Material;
    public required string Material { get; init; }
    public required string Slot { get; init; }
    public required string SourceShader { get; init; }
    public string? PrrShader { get; init; }
    public required string OutputType { get; init; }
    public int? MaterialVersion { get; init; }
    public string? SourceFile { get; init; }
    public string? SourceMaterialName { get; init; }
    public object? SourceHeader { get; init; }
    public IReadOnlyList<He1Parameter> RawParameters { get; init; } = [];
    public PrrMappingResult Mapping { get; init; } = new();
    public List<PrrTextureAudit> Textures { get; init; } = [];
    public string? FallbackReason { get; init; }
}

public static class PrrParameterAuditReport
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public static void Write(PrrExportResult result,string directory)
    {
        Directory.CreateDirectory(directory);
        var parameters=result.Audits.SelectMany(m=>m.Mapping.Parameters).ToArray();
        var assignments=result.Audits.SelectMany(m=>m.Mapping.Assignments).ToArray();
        var textures=result.Audits.SelectMany(m=>m.Textures).ToArray();
        var report=new
        {
            schemaVersion=1, game=result.Game.ToString(), referenceMode=result.Destination.External?"external":"bundled",
            summary=new {
                totalMaterialParameters=parameters.Length, successfullyMapped=parameters.Count(p=>p.Classification=="mapped"),
                runtimeVerifiedParameters=result.Audits.Sum(m=>m.Mapping.Parameters.Count(p=>p.Classification=="mapped"&&
                    p.Destinations.All(d=>m.Mapping.Assignments.Single(a=>a.Name==d).Actual.GetValueOrDefault("text")?.Status=="verified"))),
                binaryVerifiedParameters=result.Audits.Sum(m=>m.Mapping.Parameters.Count(p=>p.Classification=="mapped"&&
                    p.Destinations.All(d=>m.Mapping.Assignments.Single(a=>a.Name==d).Actual.GetValueOrDefault("binary")?.Status=="verified"))),
                runtimeVerifiedAssignments=assignments.Count(a=>a.Actual.GetValueOrDefault("text")?.Status=="verified"),
                binaryVerifiedAssignments=assignments.Count(a=>a.Actual.GetValueOrDefault("binary")?.Status=="verified"),
                correctedColorVariantAssignments=assignments.Count(a=>a.Expected.VariantType=="Color"&&a.Origin=="HE1"),
                incorrectMappingsCorrected=assignments.Count(a=>a.LegacyIssue!=null),
                intentionalDefaults=assignments.Count(a=>a.Origin=="ConverterDefault"),
                intentionallyUnused=parameters.Count(p=>p.Classification=="intentionally_unused"),
                missingMappings=parameters.Count(p=>p.Classification=="missing_mapping"), unsupported=parameters.Count(p=>p.Classification=="unsupported"),
                ambiguous=parameters.Count(p=>p.Classification=="ambiguous"), failedConversions=parameters.Count(p=>p.Classification=="failed_conversion"),
                failedValidations=assignments.Sum(a=>a.Actual.Values.Count(v=>v.Status!="verified"))+textures.Sum(t=>t.Actual.Values.Count(v=>v.Status!="verified")),
                failedValidationRuns=result.ValidationErrors.Count,
                totalTextureBindings=textures.Length, mappedTextures=textures.Count(t=>t.Status=="mapped"),
                runtimeVerifiedTextures=textures.Count(t=>t.Actual.GetValueOrDefault("text")?.Status=="verified"),
                binaryVerifiedTextures=textures.Count(t=>t.Actual.GetValueOrDefault("binary")?.Status=="verified"),
                spatialFallbackMaterials=result.Audits.Count(m=>m.OutputType=="SpatialMaterial"),
                unsupportedTextures=textures.Count(t=>t.Status=="unsupported"), missingTextures=textures.Count(t=>t.Status=="missing"),
                unmappedTextureRoles=textures.Count(t=>t.Status=="missing_mapping")
            },
            materials=result.Audits.Select(m=>new {
                m.Material,m.Slot,m.ResourceName,m.SourceShader,m.PrrShader,m.OutputType,m.MaterialVersion,m.SourceFile,m.SourceMaterialName,m.SourceHeader,m.RawParameters,m.FallbackReason,
                parameters=m.Mapping.Parameters.Select(p=>new { p.SourceName,p.Storage,p.RawSourceValue,p.Classification,p.Reason,p.Evidence,p.Confidence,
                    mappings=m.Mapping.Assignments.Where(a=>a.SourceName==p.SourceName) }),
                assignments=m.Mapping.Assignments, textures=m.Textures
            }),
            validationErrors=result.ValidationErrors,
            validationPolicy="Expected typed values are derived before text serialization. Text and binary readbacks compare against these independent expectations. Absent readbacks mean not runtime verified."
        };
        File.WriteAllText(Path.Combine(directory,"prr-parameter-audit.json"),JsonSerializer.Serialize(report,JsonOptions));
    }

    public static void WriteInventory(string path)
    {
        // Blend/cull presets share an interface; coverage specializations do not.
        var library=PrrShaderLibrary.Default;
        var paths=library.Paths.SelectMany(path=>new[]{"opaque","punch","transparent"}
            .Select(slot=>library.SelectPreset(library.Get(path),slot,false,false).Path));
        var entries=paths.Select(path=>{
            var shader=PrrShaderLibrary.Default.Get(path);
            string body=Regex.Replace(shader.Code,@"\buniform\b[^;]*;","");
            return new { shader=path,game=path.Split('/')[^2],family=Path.GetFileName(path).Split('_')[0],
                sha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(shader.Code))).ToLowerInvariant(),
                uniforms=shader.Uniforms.Select(u=>new {u.Name,u.Type,u.Hint,u.DefaultValue,u.ArrayDeclaration,
                    sampler=u.Type.StartsWith("sampler",StringComparison.Ordinal),
                    usage=body.Split('\n').Select((line,i)=>(line,i)).Where(l=>Regex.IsMatch(l.line,@"\b"+Regex.Escape(u.Name)+@"\b"))
                        .Select(l=>l.line.Trim()).Distinct().ToArray(),
                    compatibility=u.ArrayDeclaration!=null?"Unsupported Godot 3.1 uniform array":u.Type=="mat4"?
                        "Godot Transform represents affine matrices; source layout requires verified binding":u.IsColor?
                        "Godot Color Variant; native spatial renderer performs its own color linearization":"Godot 3.1 declared Variant type" }) };
        }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path,JsonSerializer.Serialize(new {canonicalPrograms=library.Paths.Count,nativePresets=library.PresetPaths.Count,
            shaders=entries.Length,uniforms=entries.Sum(s=>s.uniforms.Count()),entries},JsonOptions));
    }

    internal static void WriteExpected(PrrExportResult result,string directory)
    {
        var expected=new Dictionary<string,object>(StringComparer.Ordinal);
        int index=0;
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var material in result.Audits.Where(m=>m.OutputType=="ShaderMaterial"))
        {
            var textures=material.Textures.Where(t=>t.Status=="mapped").ToArray();
            foreach(var texture in textures)
            {
                string hash=texture.PixelSha256??Convert.ToHexString(SHA256.HashData(texture.Pixels!));
                if(!files.TryGetValue(hash,out string? expectedFile))
                {
                    expectedFile="__prr-textures/"+(index++)+".bin";
                    Directory.CreateDirectory(Path.Combine(directory,"__prr-textures"));
                    File.WriteAllBytes(Path.Combine(directory,expectedFile),texture.Pixels!);
                    files.Add(hash,expectedFile);
                }
                texture.ExpectedFile=expectedFile;
            }
            // Coverage presets can have different uniforms even for the same source material.
            expected.Add(material.ResourceName,new {
                values=material.Mapping.Assignments.Select(a=>new {name=a.Name,kind=a.Expected.VariantType,value=a.Expected.Components,
                    exactInteger=a.Expected.VariantType=="int"?a.Expected.Literal:null}),
                textures=textures.Select(t=>new{name=t.Sampler,width=t.Width,height=t.Height,flags=t.Flags,file=t.ExpectedFile})
            });
        }
        File.WriteAllText(Path.Combine(directory,"__prr-expected.json"),JsonSerializer.Serialize(expected,JsonOptions));
    }

    internal static void ReadBack(PrrExportResult result,string directory)
    {
        foreach(string stage in new[]{"text","binary"})
        {
            string file=Path.Combine(directory,"__prr-readback-"+stage+".json");
            if(!File.Exists(file))continue;
            using var document=JsonDocument.Parse(File.ReadAllText(file));
            foreach(var item in document.RootElement.EnumerateArray())
            {
                string material=item.GetProperty("material").GetString()!,name=item.GetProperty("name").GetString()!;
                var readback=new PrrReadback(item.GetProperty("status").GetString()!,item.GetProperty("kind").GetString()!,
                    item.GetProperty("value").EnumerateArray().Select(v=>v.GetDouble()).ToArray(),
                    item.TryGetProperty("reason",out var reason)?reason.GetString():null);
                foreach(var audit in result.Audits.Where(m=>m.ResourceName==material))
                {
                    foreach(var assignment in audit.Mapping.Assignments.Where(a=>a.Name==name))assignment.Actual[stage]=readback;
                    foreach(var texture in audit.Textures.Where(t=>t.Sampler==name))texture.Actual[stage]=readback;
                }
            }
        }
        Write(result,directory);
    }
    internal static void CleanExpected(string directory)
    {
        foreach(string file in new[]{"__prr-expected.json","__prr-readback-text.json","__prr-readback-binary.json"})File.Delete(Path.Combine(directory,file));
        string textures=Path.Combine(directory,"__prr-textures");
        if(Directory.Exists(textures))
        {
            foreach(string file in Directory.GetFiles(textures,"*.bin"))File.Delete(file);
            if(!Directory.EnumerateFileSystemEntries(textures).Any())Directory.Delete(textures);
        }
    }
}
