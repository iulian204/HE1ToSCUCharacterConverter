using System.Diagnostics;
using System.Text.Json;

namespace HE1ToSCU.Core;

public sealed record ConversionRequest(string ModelPath,string? SkeletonPath,IReadOnlyList<string> AnimationPaths,string OutputDirectory,string? AcornEditorPath=null,SourceGame Game=SourceGame.SonicGenerations,bool GenerateMouthL=false,
    bool ExportPrrMaterials=false, string? PrrShaderPath=null,bool MatchSonicAssetNames=false,bool SuperSonic=false,string? AnimationNameMapPath=null);
public sealed record ConversionProgress(int Percent,string Message);
public sealed record ConversionResult(string Directory,int Surfaces,int Vertices,int Bones,int Animations,bool BinaryValidated);

public sealed class ConversionService
{
    public async Task<ConversionResult> ConvertAsync(ConversionRequest request,IProgress<ConversionProgress>? progress=null,CancellationToken token=default)
    {
        void Report(int p,string message)=>progress?.Report(new ConversionProgress(p,message));
        if(!request.MatchSonicAssetNames&&(request.SuperSonic||!string.IsNullOrWhiteSpace(request.AnimationNameMapPath)))
            throw new ArgumentException("Super Sonic and animation naming corrections require Match Sonic Asset Names (--match-scu-sonic-names).");
        if(!string.IsNullOrWhiteSpace(request.AnimationNameMapPath)&&!File.Exists(request.AnimationNameMapPath))
            throw new FileNotFoundException("Animation naming correction file does not exist.",request.AnimationNameMapPath);
        if (!request.ExportPrrMaterials && !string.IsNullOrWhiteSpace(request.PrrShaderPath))
            throw new ArgumentException("Choose shader path requires Export PRR materials (--export-prr-materials).");
        if(request.GenerateMouthL && request.Game!=SourceGame.SonicUnleashed) throw new ArgumentException("Mouth_L generation is available only for Sonic Unleashed.");
        if(!File.Exists(request.ModelPath))throw new FileNotFoundException("Select an existing HE1 .model file.",request.ModelPath);
        string destination=Path.GetFullPath(request.OutputDirectory);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException($"Output already exists: {destination}. Choose a new export folder to preserve existing files.");
        if(request.AnimationPaths.Count>0&&string.IsNullOrWhiteSpace(request.SkeletonPath))throw new InvalidDataException("Animation export requires its matching skeleton .hkx file.");
        if(!string.IsNullOrWhiteSpace(request.AcornEditorPath)&&!File.Exists(request.AcornEditorPath))throw new FileNotFoundException("Acorn editor executable does not exist.",request.AcornEditorPath);
        var shaderDestination = request.ExportPrrMaterials ? new PrrShaderDestination(request.PrrShaderPath, destination,
            !string.IsNullOrWhiteSpace(request.AcornEditorPath)) : null;
        Report(2,"Reading HE1 geometry, vertex declarations, bone palettes and inverse bind matrices...");
        Report(2,$"Source game: {request.Game}.");
        var model=new He1ModelReader().Read(request.ModelPath,token,request.Game);
        Report(18,$"Loaded {model.Surfaces.Count} surfaces, {model.Surfaces.Sum(s=>s.Vertices.Count):N0} vertices and {model.Bones.Count} model bones.");
        var materials = new Dictionary<string, He1Material>(StringComparer.OrdinalIgnoreCase);
        string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(request.ModelPath))!;
        foreach (string name in model.Surfaces.Select(s => s.Material).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            string? materialPath = He1MaterialReader.Find(sourceDirectory, name + ".material");
            if (materialPath == null) { Report(19,$"Missing {name}.material; using a neutral SpatialMaterial."); continue; }
            materials.Add(name, new He1MaterialReader().Read(materialPath,request.Game));
        }
        model = model with { Materials = materials };
        Report(19,$"Read {materials.Count} HE1 materials and their texture references.");
        model = MouthMeshSplitter.Split(model, token, message => Report(19, message));
        Report(20,"Spatial profile: preserve Y-up basis; scale positions and translations by 10 for SCU character space.");
        List<Bone>? havokBones=null;
        if(!string.IsNullOrWhiteSpace(request.SkeletonPath))
        {
            var skeleton=new HavokReader().Read(request.SkeletonPath,token,request.Game);
            if(skeleton.Bones.Count==0)throw new InvalidDataException("Selected skeleton HKX has no skeleton.");
            havokBones=skeleton.Bones;
            Report(25,$"Loaded {havokBones.Count} Havok reference bones; matching model bones by exact name.");
        }
        var clips=new List<AnimationClip>();
        var modelLookup=model.Bones.Select((b,i)=>(b.Name,i)).ToDictionary(p=>p.Name,p=>p.i,StringComparer.Ordinal);
        int[]? boneMap=havokBones?.Select(b=>modelLookup.TryGetValue(b.Name,out int exact) ? exact :
            b.Name.EndsWith("@LT",StringComparison.Ordinal) ? modelLookup.GetValueOrDefault(b.Name[..^3],-1) : -1).ToArray();
        if(boneMap!=null && boneMap.Where(i=>i>=0).Distinct().Count()!=boneMap.Count(i=>i>=0)) throw new InvalidDataException("Bone aliases are ambiguous.");
        if(havokBones!=null&&boneMap!=null)
        {
            for(int i=0;i<havokBones.Count;i++)
            {
                int mapped=boneMap[i];if(mapped<0)continue;
                int hParent=havokBones[i].Parent,mParent=model.Bones[mapped].Parent;
                if((hParent<0)!=(mParent<0)||(hParent>=0&&boneMap[hParent]!=mParent))
                    throw new InvalidDataException($"Havok/model parent hierarchy differs at '{havokBones[i].Name}'. Retargeting is required.");
            }
        }
        for(int i=0;i<request.AnimationPaths.Count;i++)
        {
            token.ThrowIfCancellationRequested();string path=request.AnimationPaths[i];
            Report(28+30*i/Math.Max(1,request.AnimationPaths.Count),$"Decoding {Path.GetFileName(path)}...");
            var asset=new HavokReader().Read(path,token,request.Game);
            if(asset.Animations.Count==0)throw new InvalidDataException($"{Path.GetFileName(path)} contains no animation.");
            foreach(var clip in asset.Animations)
            {
                int[] map=clip.TrackToBone.Select(index=>index>=0&&index<boneMap!.Length?boneMap[index]:-1).ToArray();
                if(map.Any(index=>index<0))throw new InvalidDataException($"{clip.Name}: a bound animation bone is absent from the model or skeleton. Supply a matching asset set.");
                if(map.Distinct().Count()!=map.Length)throw new InvalidDataException("Multiple transform tracks target the same export bone.");
                if(clips.Any(c=>c.Name==clip.Name))throw new InvalidDataException($"Duplicate animation name: {clip.Name}.");
                if(clip.Name.IndexOfAny(['/',':','"','\n','\r'])>=0)throw new InvalidDataException("Animation name contains characters unsupported in Godot animation property names.");
                var reference = model.Bones.Select(b=>Pose.FromMatrix(b.Rest)).ToArray();
                for(int h=0;h<havokBones!.Count;h++) if(boneMap![h]>=0) reference[boneMap[h]]=Pose.FromMatrix(havokBones[h].Rest);
                clips.Add(AnimationSampling.Complete(clip with{TrackToBone=map},reference));
                if(clip.Blend!=AnimationBlend.Normal) Report(58,$"{clip.Name}: baking {clip.Blend} over the matching Havok reference pose for standalone playback.");
                Report(28+30*(i+1)/request.AnimationPaths.Count,$"Decoded {clip.Name}: {clip.Frames.Length} frames, {map.Length} quaternion tracks, {clip.Duration:F3}s.");
                if (clip.RootMotion != null) Report(28+30*(i+1)/request.AnimationPaths.Count,$"Preserving {clip.Name} root motion: {clip.RootMotion.Samples.Count} translation/heading samples.");
            }
        }
        int sourceBoneCount = model.Bones.Count;
        int generatedMouthBones = 0;
        if(request.GenerateMouthL)
        {
            var mirrored = MirroredMouth.Create(model, clips, token, message=>Report(58,message));
            model = mirrored.Model; clips = mirrored.Clips; generatedMouthBones = mirrored.GeneratedBones;
        }
        var sourceModel = model; var sourceClips = clips;
        var prepared = AnimationRig.Prepare(model, clips, token);
        model = prepared.Model; clips = prepared.Clips;
        ScuNamingPlan? naming=null;
        if(request.MatchSonicAssetNames)
        {
            Report(59,"Matching against the bundled SCU inventory using semantic, timing and normalized motion evidence...");
            naming=ScuNameMatcher.Match(sourceModel,sourceClips,request.Game,request.SuperSonic,request.AnimationNameMapPath,message=>Report(59,message),token);
            var bySource=clips.ToDictionary(c=>c.Name,StringComparer.Ordinal);
            clips=naming.Matches.Select(m=>bySource[m.SourceName] with{ExportIdentity=new(m.TargetName,m.ResourceName,m.PlayerKey)}).ToList();
            model=model with{ExportIdentity=naming.TargetModel};
        }
        if (prepared.Helpers > 0) Report(59,$"Added {prepared.Helpers} rigid scale-control bones to preserve HE1 translation/rotation/scale inheritance ({sourceBoneCount} original bones, {generatedMouthBones} generated mouth bones).");
        string parent=Path.GetDirectoryName(destination)??throw new IOException("Export folder has no parent.");
        Directory.CreateDirectory(parent);
        string staging=Path.Combine(parent,".he1scu-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            naming?.WriteReports(staging);
            Report(60,"Writing Godot 3 ArrayMesh, Skeleton and rest-relative quaternion Animation resources...");
            var prr = shaderDestination == null ? null : new PrrMaterialWriter().Write(model, staging, request.Game,
                shaderDestination, message => Report(64, message), token);
            GodotWriter.Write(model,clips,staging,token,message=>Report(64,message),request.Game,prr?.MaterialFiles);
            bool binary=!string.IsNullOrWhiteSpace(request.AcornEditorPath);
            if(binary)
            {
                Report(75,"Loading resources in Acorn and saving native binary mesh, animation and scene files...");
                prr?.StageExternalShaders(staging);
                prr?.WriteExpected(staging);
                try
                {
                    var validationSources=naming==null?sourceClips:clips.Select(c=>sourceClips.Single(s=>s.Name==c.Name)).ToList();
                    await AcornResourceCompiler.CompileAsync(request.AcornEditorPath!,staging,model,clips,sourceModel,validationSources,log=>Report(82,log),token,prr?.ShaderMaterials > 0,
                        prr?.ShaderFiles.Keys.Select(path => shaderDestination!.External ? path : "res://" + path).ToArray());
                    if(prr?.ShaderMaterials>0 && (!File.Exists(Path.Combine(staging,"__prr-readback-text.json")) ||
                        !File.Exists(Path.Combine(staging,"__prr-readback-binary.json"))))
                        throw new InvalidDataException("Acorn did not produce both independent PRR parameter readback reports.");
                }
                catch(Exception e) when(e is not OperationCanceledException)
                {
                    prr?.ValidationErrors.Add(e.Message);
                    throw;
                }
                finally
                {
                    if(token.IsCancellationRequested)
                    {
                        try { prr?.ReadBack(staging); }
                        catch(Exception e) when(e is JsonException or IOException)
                        { Report(0,"PRR validation was cancelled before its readback report completed: "+e.Message); }
                    }
                    else prr?.ReadBack(staging);
                }
                if(prr!=null) PrrParameterAuditReport.CleanExpected(staging);
                prr?.RemoveStagedShaders(staging);
                // SCU already supplies its canonical remap. Exporting that absolute path beside
                // the editable scene redirects Godot away from this folder and breaks opening it.
            }
            else Report(85,"Text resources written. Acorn binary validation skipped because no editor was selected.");
            if (prr == null) Report(90,$"Converted {materials.Count} HE1 materials to textured SpatialMaterials. Custom HE1 lighting is approximated using built-in shading.");
            var result=new ConversionResult(destination,model.AllSurfaces.Count(),model.AllSurfaces.Sum(s=>s.Vertices.Count),model.Bones.Count,clips.Count,binary);
            var manifest=new
            {
                converter="HE1ToSCU 0.8.1",game=request.Game.ToString(),request.GenerateMouthL,result,sourceModel=Path.GetFullPath(request.ModelPath),sourceSkeleton=request.SkeletonPath,
                sourceAnimations=request.AnimationPaths,spatialProfile=new{scale=Spatial.Scale,basis="Y-up preserved"},
                outputScene=binary?ExportNames.SceneBinary(model):ExportNames.SceneText(model),outputMesh=binary?ExportNames.MeshBinary(model):ExportNames.MeshText(model),
                meshes=model.MeshParts.Select(p=>new {nodePath=ExportNames.SkeletonNode(model)+"/"+p.Name,textFile=ExportNames.MeshText(model,p.Name),binaryFile=binary?ExportNames.MeshBinary(model,p.Name):null,p.MouthRootBone,p.Mirrored,layers=GodotWriter.CharacterRenderLayers,surfaces=p.Surfaces.Count,vertices=p.Surfaces.Sum(s=>s.Vertices.Count)}),
                animations=clips.Select(c=>new{name=c.Name,targetName=ExportNames.AnimationName(c),textFile=ExportNames.AnimationText(c),binaryFile=binary?ExportNames.AnimationBinary(c):null,rootMotionSamples=c.RootMotion?.Samples.Count??0,sourceFrames=sourceClips.Single(s=>s.Name==c.Name).Frames.Length,exportFrames=c.Frames.Length,c.BakedReference}),
                sonicNaming=new{enabled=request.MatchSonicAssetNames,profile=naming?.Profile,inventoryVersion=naming?.InventoryVersion,sourceModel=model.Name,targetModel=model.ExportIdentity,
                    matches=naming?.Matches,unfilled=naming?.Unfilled,unusedSources=naming?.UnusedSources,warnings=naming?.Warnings,rigPolicy=naming?.RigPolicy,
                    report=naming==null?null:"scu-animation-mapping.txt",corrections=request.AnimationNameMapPath},
                materials=materials.Values.Select(m=>new{m.Name,m.Shader,m.AlphaThreshold,m.DoubleSided,m.Additive,parameters=m.Parameters.ToDictionary(p=>p.Key,p=>p.Value.Select(v=>new[]{v.X,v.Y,v.Z,v.W}).ToArray()),m.Textures}),
                prr = new { enabled = request.ExportPrrMaterials, game = request.Game.ToString(), shaderPath = shaderDestination?.ResourceDirectory,
                    referenceMode = shaderDestination == null ? "disabled" : shaderDestination.External ? "external" : "bundled",
                    materials = prr?.Materials, shaderMaterials = prr?.ShaderMaterials ?? 0, spatialFallbacks = prr?.SpatialFallbacks ?? 0,
                    uniqueShaders = prr?.ShaderFiles.Keys.ToArray() ?? [],
                    bundledShaders = prr != null && !prr.Destination.External ? prr.ShaderFiles.Count : 0,
                    externalShaders = prr != null && prr.Destination.External ? prr.ShaderFiles.Count : 0 },
                prrParameterAudit = prr == null ? null : "prr-parameter-audit.json",
                sourceBoneCount,generatedMouthBones,scaleCompensationBones=prepared.Helpers,
                limitations=new[]{prr == null ? "Custom HE1 lighting, colored specular maps and reflection/matcap shaders are approximations to SpatialMaterial." : "PRR preserves mapped source material inputs; scene lighting, GI, region shadows, projected reflections and runtime controls remain project inputs. Unmapped inputs are listed per material.","Original HE1 rig exported; SCU player retargeting and gameplay wiring pending.","In-game acceptance and renderer parity not established."}
            };
            File.WriteAllText(Path.Combine(staging,"conversion.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));
            token.ThrowIfCancellationRequested();
            Directory.Move(staging,destination);
            Report(100,$"Export complete: {destination}");return result;
        }
        catch
        {
            // Keep diagnostic files on error; never erase existing user output or source assets.
            Report(0,$"Export did not complete. Diagnostic staging files: {staging}");throw;
        }
    }
}

internal static class AcornResourceCompiler
{
    public static async Task CompileAsync(string editor,string directory,ModelAsset model,IReadOnlyList<AnimationClip> clips,ModelAsset source,IReadOnlyList<AnimationClip> originals,Action<string> log,CancellationToken token,bool prrShaders=false,IReadOnlyList<string>? shaderPaths=null)
    {
        // Supplied PRR cube/UV shaders require GLES3 (textureSize/textureLod). Keep the legacy mode unchanged.
        string renderer = prrShaders ? "GLES3" : "GLES2";
        log("Resource validation renderer: " + renderer);
        File.WriteAllText(Path.Combine(directory,"project.godot"),$"config_version=4\n[application]\nconfig/name=\"HE1SCU Resource Compile\"\n[rendering]\nquality/driver/driver_name=\"{renderer}\"\n");
        int samples = PoseValidation.Write(Path.Combine(directory,"__poses.bin"),model,source,clips,originals,token);
        log($"Validating {samples:N0} source-derived keyframe, subframe and loop-boundary poses.");
        string script=BuildScript(model,clips,shaderPaths??[]);
        string scriptPath=Path.Combine(directory,"__compile.gd");File.WriteAllText(scriptPath,script);
        async Task RunAsync(string path,string marker,string logFile)
        {
            var info=new ProcessStartInfo(editor){WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"--no-window","--path",directory,"--script",path}) info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new IOException("Unable to start Acorn editor.");
        var lines=new List<string>();object sync=new();
        async Task ReadAsync(StreamReader stream)
        {
            while(await stream.ReadLineAsync() is{} line)
            {
                lock(sync)lines.Add(line);
                if(line.Length>0)log(line);
            }
        }
        var stdout=ReadAsync(process.StandardOutput);var stderr=ReadAsync(process.StandardError);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,timeout.Token);
        try { await process.WaitForExitAsync(linked.Token);await Task.WhenAll(stdout,stderr); }
        catch(OperationCanceledException)
        {
            if(!process.HasExited)process.Kill(true);
            await Task.WhenAll(stdout,stderr);
            if(token.IsCancellationRequested)throw;
            throw new TimeoutException("Acorn resource compilation exceeded ten minutes.");
        }
        await File.WriteAllLinesAsync(Path.Combine(directory,logFile),lines,CancellationToken.None);
        if(process.ExitCode!=0||!lines.Any(l=>l.Contains(marker,StringComparison.Ordinal))||lines.Any(l=>l.Contains("SCRIPT ERROR",StringComparison.Ordinal)||l.StartsWith("ERROR:",StringComparison.Ordinal)))
            throw new InvalidDataException($"Acorn could not validate the exported resources. See {logFile} in the diagnostic staging folder. " +
                string.Join(Environment.NewLine, lines.Where(l => l.Contains("ERROR",StringComparison.Ordinal) || l.Contains(".shader",StringComparison.Ordinal)).TakeLast(8)));
        }
        await RunAsync(scriptPath,"HE1SCU_VALIDATED","acorn-validation.log");
        string verifyPath=Path.Combine(directory,"__verify.gd");
        File.WriteAllText(verifyPath,BuildVerifyScript(model,clips));
        await RunAsync(verifyPath,"HE1SCU_BINARY_VALIDATED","acorn-binary-validation.log");
        File.Delete(verifyPath);File.Delete(Path.Combine(directory,"__poses.bin"));File.Delete(Path.Combine(directory,"__materials.bin"));
        var header = new byte[4];
        foreach(string file in model.MeshParts.Select(p=>ExportNames.MeshBinary(model,p.Name)).Prepend(ExportNames.SceneBinary(model)))
        {
            using var stream = File.OpenRead(Path.Combine(directory, file));
            if (stream.ReadAtLeast(header, header.Length, false) != header.Length ||
                (!header.AsSpan().SequenceEqual("RSRC"u8) && !header.AsSpan().SequenceEqual("RSCC"u8)))
                throw new InvalidDataException($"Acorn did not produce a binary Godot resource: {file}.");
        }
        File.Delete(scriptPath);File.Delete(Path.Combine(directory,"project.godot"));
    }
    private static string NamedAnimationChecks(IReadOnlyList<AnimationClip> clips) => JsonSerializer.Serialize(
        clips.Where(c=>c.ExportIdentity!=null).Select(c=>new[]{ExportNames.AnimationName(c),
            ExportNames.AnimationName(clips.First(original=>original.Name==c.Name)),"res://"+ExportNames.AnimationBinary(c),c.ExportIdentity!.ResourceName}));
    private const string NamingValidationFunctions = """
        func named_animation_data(a):
            var data = [a.length,a.loop,a.step,a.get_track_count()]
            for t in range(a.get_track_count()):
                data.append([a.track_get_type(t),str(a.track_get_path(t)),a.track_is_enabled(t),a.track_get_interpolation_type(t),a.track_get_interpolation_loop_wrap(t)])
                for k in range(a.track_get_key_count(t)):
                    data.append([a.track_get_key_time(t,k),a.track_get_key_transition(t,k),a.track_get_key_value(t,k)])
            return var2bytes(data)
        func check_named_animations(player, entries):
            var representatives = {}
            for entry in entries:
                var actual = player.get_animation(entry[0])
                if actual == null or actual.resource_name != entry[3]:
                    fail("renamed animation identity " + entry[0])
                    return false
                if not representatives.has(entry[1]):
                    representatives[entry[1]] = named_animation_data(player.get_animation(entry[1]))
                if named_animation_data(actual) != representatives[entry[1]]:
                    fail("reused animation data differs from source representative " + entry[0])
                    return false
                if naming_binary_stage:
                    var standalone = load(entry[2])
                    if standalone == null or standalone.resource_name != entry[3] or named_animation_data(standalone) != representatives[entry[1]]:
                        fail("renamed standalone animation differs " + entry[0])
                        return false
            return true
        """;
    private static string MeshChecks(ModelAsset model) => JsonSerializer.Serialize(model.MeshParts.Select(p=>new object[] {p.Name,"res://"+ExportNames.MeshBinary(model,p.Name),p.Surfaces.Count,!p.Mirrored}));
    private static string BuildVerifyScript(ModelAsset model,IReadOnlyList<AnimationClip> clips)=>$$"""
        extends SceneTree
        func fail(message):
            printerr("HE1SCU_FAILED: " + message)
            quit()
        {{MaterialResourceValidation.Functions}}
        {{PoseValidation.GodotFunctions}}
        {{NamingValidationFunctions}}
        var naming_binary_stage = true
        func _init():
            load_prr_expectations("binary")
            var expected = File.new()
            if expected.open("res://__materials.bin", File.READ) != OK:
                fail("missing material validation data")
                return
            material_snapshots = expected.get_var()
            expected.close()
            var packed = load({{ExportNames.Literal("res://" + ExportNames.SceneBinary(model))}})
            if packed == null:
                fail("binary scene load in a fresh process")
                return
            var instance = packed.instance()
            get_root().add_child(instance)
            var skeleton = instance.get_node({{ExportNames.Literal(ExportNames.SkeletonNode(model))}})
            if skeleton.get_bone_count() != {{model.Bones.Count}}:
                fail("binary scene counts")
                return
            for entry in {{MeshChecks(model)}}:
                var mesh_instance = skeleton.get_node(entry[0])
                var mesh = mesh_instance.mesh
                var standalone_mesh = load(entry[1])
                if mesh == null or mesh.get_surface_count() != entry[2] or mesh_instance.get_node(mesh_instance.skeleton) != skeleton or mesh_instance.visible != entry[3] or mesh_instance.layers != {{GodotWriter.CharacterRenderLayers}}:
                    fail("binary mesh/skeleton binding " + entry[0])
                    return
                if standalone_mesh == null or standalone_mesh.get_surface_count() != entry[2]:
                    fail("standalone mesh load " + entry[0])
                    return
                for surface in range(mesh.get_surface_count()):
                    if not check_material(mesh.surface_get_material(surface), entry[0] + "/" + str(surface)) or not check_material(standalone_mesh.surface_get_material(surface), entry[0] + "/" + str(surface)):
                        fail("binary material load " + entry[0])
                        return
            if {{clips.Count}} > 0:
                var player = instance.get_node("AnimationPlayer")
                if player.get_animation_list().size() != {{clips.Count}}:
                    fail("binary animation count")
                    return
                for animation_path in {{ExportNames.AnimationPaths(clips)}}:
                    var standalone_animation = load(animation_path)
                    if standalone_animation == null or standalone_animation.get_track_count() == 0:
                        fail("standalone animation load")
                        return
                if not check_named_animations(player, {{NamedAnimationChecks(clips)}}):
                    return
                var checked_poses = check_poses(instance, skeleton)
                if checked_poses is GDScriptFunctionState:
                    checked_poses = yield(checked_poses, "completed")
                if not checked_poses:
                    return
            instance.free()
            # Exercise the public editable entry point too, not only the native resource.
            # An absolute .tscn.remap can otherwise silently redirect it to a missing file.
            var text_scene = ResourceLoader.load({{ExportNames.Literal("res://" + ExportNames.SceneText(model))}}, "PackedScene", true)
            if text_scene == null:
                fail("editable scene entry point")
                return
            var text_instance = text_scene.instance()
            for part in {{MeshChecks(model)}}:
                var mesh_instance = text_instance.get_node({{ExportNames.Literal(ExportNames.SkeletonNode(model))}}).get_node(part[0])
                if mesh_instance.layers != {{GodotWriter.CharacterRenderLayers}}:
                    fail("editable scene character render layers")
                    return
            text_instance.free()
            save_prr_readbacks()
            print("HE1SCU_BINARY_VALIDATED bones={{model.Bones.Count}} meshes={{model.MeshParts.Count()}} surfaces={{model.AllSurfaces.Count()}} animations={{clips.Count}}")
            quit()
        """;
    private static string BuildScript(ModelAsset model,IReadOnlyList<AnimationClip> clips,IReadOnlyList<string> shaderPaths)=>$$"""
        extends SceneTree
        func fail(message):
            printerr("HE1SCU_FAILED: " + message)
            quit()
        {{MaterialResourceValidation.Functions}}
        {{PoseValidation.GodotFunctions}}
        {{NamingValidationFunctions}}
        var naming_binary_stage = false
        func _init():
            load_prr_expectations("text")
            for shader_path in {{JsonSerializer.Serialize(shaderPaths)}}:
                print("HE1SCU_SHADER_VALIDATE " + shader_path)
                var shader = load(shader_path)
                if not (shader is Shader):
                    fail("PRR shader load: " + shader_path)
                    return
                var material_probe = ShaderMaterial.new()
                material_probe.shader = shader
                material_probe.get_property_list()
            var packed = load({{ExportNames.Literal("res://" + ExportNames.SceneText(model))}})
            if packed == null:
                fail("scene load")
                return
            var instance = packed.instance()
            get_root().add_child(instance)
            var skeleton = instance.get_node({{ExportNames.Literal(ExportNames.SkeletonNode(model))}})
            if skeleton.get_bone_count() != {{model.Bones.Count}}:
                fail("bone counts")
                return
            for entry in {{MeshChecks(model)}}:
                var mesh_instance = skeleton.get_node(entry[0])
                var mesh = mesh_instance.mesh
                if mesh == null or mesh.get_surface_count() != entry[2] or mesh_instance.get_node(mesh_instance.skeleton) != skeleton or mesh_instance.visible != entry[3] or mesh_instance.layers != {{GodotWriter.CharacterRenderLayers}}:
                    fail("mesh/skeleton binding " + entry[0])
                    return
                for surface in range(mesh.get_surface_count()):
                    var arrays = mesh.surface_get_arrays(surface)
                    if arrays[0].size() == 0 or arrays[8].size() == 0:
                        fail("empty vertex/index buffer " + entry[0])
                        return
                    var material = mesh.surface_get_material(surface)
                    if not (material is SpatialMaterial or material is ShaderMaterial):
                        fail("expected SpatialMaterial or ShaderMaterial")
                        return
                    if not check_prr_material(material):
                        return
                    var snapshot = material_snapshot(material)
                    if snapshot.empty():
                        fail("invalid shader or sampler " + entry[0] + "/" + str(surface))
                        return
                    material_snapshots[entry[0] + "/" + str(surface)] = snapshot
                    clear_material_paths(material)
                mesh.resource_path = ""
                if ResourceSaver.save(entry[1], mesh, 65) != OK:
                    fail("mesh save " + entry[0])
                    return
                mesh.take_over_path(entry[1])
                # Embed each mesh's resources and avoid Acorn's relative-path cache mismatch.
                for surface in range(mesh.get_surface_count()):
                    clear_material_paths(mesh.surface_get_material(surface))
                mesh.resource_path = ""
            if {{clips.Count}} > 0:
                var player = instance.get_node("AnimationPlayer")
                var animation_files = {{JsonSerializer.Serialize(clips.ToDictionary(ExportNames.AnimationName,c=>"res://"+ExportNames.AnimationBinary(c)))}}
                var names = player.get_animation_list()
                if names.size() != {{clips.Count}}:
                    fail("animation count")
                    return
                for name in names:
                    var animation = player.get_animation(name)
                    player.play(name)
                    for fraction in [0.0, 0.5, 1.0]:
                        player.seek(animation.length * fraction, true)
                        for bone in range(skeleton.get_bone_count()):
                            var pose = skeleton.get_bone_global_pose(bone)
                            if is_nan(pose.origin.x) or is_nan(pose.origin.y) or is_nan(pose.origin.z) or is_inf(pose.origin.length()):
                                fail("non-finite bone pose")
                                return
                    var filename = animation_files[name]
                    if ResourceSaver.save(filename, animation, 1) != OK:
                        fail("animation save")
                        return
                    # Named character folders must also load below an arbitrary project folder.
                    # Acorn preloads external binary dependencies relative to the project root,
                    # so keep their animations embedded while still exporting standalone files.
                    if {{(model.ExportIdentity!=null||(long)clips.Count*model.Bones.Count<24000?"true":"false")}}:
                        animation.resource_path = ""
                    else:
                        animation.take_over_path(filename)
                player.stop(true)
                for bone in range(skeleton.get_bone_count()):
                    skeleton.set_bone_pose(bone, Transform())
                if not check_named_animations(player, {{NamedAnimationChecks(clips)}}):
                    return
                var checked_poses = check_poses(instance, skeleton)
                if checked_poses is GDScriptFunctionState:
                    checked_poses = yield(checked_poses, "completed")
                if not checked_poses:
                    return
                player.stop(true)
                for bone in range(skeleton.get_bone_count()):
                    skeleton.set_bone_pose(bone, Transform())
            var expected = File.new()
            if expected.open("res://__materials.bin", File.WRITE) != OK:
                fail("material validation snapshot write")
                return
            expected.store_var(material_snapshots)
            expected.close()
            instance.transform = Transform()
            var output = PackedScene.new()
            if output.pack(instance) != OK or ResourceSaver.save({{ExportNames.Literal("res://" + ExportNames.SceneBinary(model))}}, output, {{(model.ExportIdentity!=null?64:65)}}) != OK:
                fail("scene save")
                return
            var reloaded = ResourceLoader.load({{ExportNames.Literal("res://" + ExportNames.SceneBinary(model))}}, "PackedScene", true)
            if reloaded == null:
                fail("binary scene reload")
                return
            var check = reloaded.instance()
            if check.get_node({{ExportNames.Literal(ExportNames.SkeletonNode(model))}}).get_bone_count() != {{model.Bones.Count}}:
                fail("binary skeleton reload")
                return
            check.free()
            instance.free()
            save_prr_readbacks()
            print("HE1SCU_VALIDATED bones={{model.Bones.Count}} meshes={{model.MeshParts.Count()}} surfaces={{model.AllSurfaces.Count()}} animations={{clips.Count}}")
            quit()
        """;
}
