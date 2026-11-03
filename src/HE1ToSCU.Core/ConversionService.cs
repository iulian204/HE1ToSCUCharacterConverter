using System.Diagnostics;
using System.Text.Json;

namespace HE1ToSCU.Core;

public sealed record ConversionRequest(string ModelPath,string? SkeletonPath,IReadOnlyList<string> AnimationPaths,string OutputDirectory,string? AcornEditorPath=null);
public sealed record ConversionProgress(int Percent,string Message);
public sealed record ConversionResult(string Directory,int Surfaces,int Vertices,int Bones,int Animations,bool BinaryValidated);

public sealed class ConversionService
{
    public async Task<ConversionResult> ConvertAsync(ConversionRequest request,IProgress<ConversionProgress>? progress=null,CancellationToken token=default)
    {
        void Report(int p,string message)=>progress?.Report(new ConversionProgress(p,message));
        if(!File.Exists(request.ModelPath))throw new FileNotFoundException("Select an existing HE1 .model file.",request.ModelPath);
        string destination=Path.GetFullPath(request.OutputDirectory);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException($"Output already exists: {destination}. Choose a new export folder to preserve existing files.");
        if(request.AnimationPaths.Count>0&&string.IsNullOrWhiteSpace(request.SkeletonPath))throw new InvalidDataException("Animation export requires its matching skeleton .hkx file.");
        if(!string.IsNullOrWhiteSpace(request.AcornEditorPath)&&!File.Exists(request.AcornEditorPath))throw new FileNotFoundException("Acorn editor executable does not exist.",request.AcornEditorPath);
        Report(2,"Reading HE1 geometry, vertex declarations, bone palettes and inverse bind matrices...");
        var model=new He1ModelReader().Read(request.ModelPath,token);
        Report(18,$"Loaded {model.Surfaces.Count} surfaces, {model.Surfaces.Sum(s=>s.Vertices.Count):N0} vertices and {model.Bones.Count} model bones.");
        var materials = new Dictionary<string, He1Material>(StringComparer.OrdinalIgnoreCase);
        string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(request.ModelPath))!;
        foreach (string name in model.Surfaces.Select(s => s.Material).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            string? materialPath = He1MaterialReader.Find(sourceDirectory, name + ".material");
            if (materialPath == null) { Report(19,$"Missing {name}.material; using a neutral SpatialMaterial."); continue; }
            materials.Add(name, new He1MaterialReader().Read(materialPath));
        }
        model = model with { Materials = materials };
        Report(19,$"Read {materials.Count} HE1 materials and their texture references.");
        Report(20,"Spatial profile: preserve Y-up basis; scale positions and translations by 10 for SCU character space.");
        List<Bone>? havokBones=null;
        if(!string.IsNullOrWhiteSpace(request.SkeletonPath))
        {
            var skeleton=new HavokReader().Read(request.SkeletonPath,token);
            if(skeleton.Bones.Count==0)throw new InvalidDataException("Selected skeleton HKX has no skeleton.");
            havokBones=skeleton.Bones;
            Report(25,$"Loaded {havokBones.Count} Havok reference bones; matching model bones by exact name.");
        }
        var clips=new List<AnimationClip>();
        var modelLookup=model.Bones.Select((b,i)=>(b.Name,i)).ToDictionary(p=>p.Name,p=>p.i,StringComparer.Ordinal);
        int[]? boneMap=havokBones?.Select(b=>modelLookup.GetValueOrDefault(b.Name,-1)).ToArray();
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
            var asset=new HavokReader().Read(path,token);
            if(asset.Animations.Count==0)throw new InvalidDataException($"{Path.GetFileName(path)} contains no animation.");
            foreach(var clip in asset.Animations)
            {
                int[] map=clip.TrackToBone.Select(index=>index>=0&&index<boneMap!.Length?boneMap[index]:-1).ToArray();
                if(map.Any(index=>index<0))throw new InvalidDataException($"{clip.Name}: a bound animation bone is absent from the model or skeleton. Supply a matching asset set.");
                if(map.Distinct().Count()!=map.Length)throw new InvalidDataException("Multiple transform tracks target the same export bone.");
                if(clips.Any(c=>c.Name==clip.Name))throw new InvalidDataException($"Duplicate animation name: {clip.Name}.");
                if(clip.Name.IndexOfAny(['/',':','"','\n','\r'])>=0)throw new InvalidDataException("Animation name contains characters unsupported in Godot animation property names.");
                clips.Add(clip with{TrackToBone=map});
                Report(28+30*(i+1)/request.AnimationPaths.Count,$"Decoded {clip.Name}: {clip.Frames.Length} frames, {map.Length} quaternion tracks, {clip.Duration:F3}s.");
                if (clip.RootMotion != null) Report(28+30*(i+1)/request.AnimationPaths.Count,$"Preserving {clip.Name} root motion: {clip.RootMotion.Samples.Count} translation/heading samples.");
            }
        }
        int sourceBoneCount = model.Bones.Count;
        var prepared = AnimationRig.Prepare(model, clips, token);
        model = prepared.Model; clips = prepared.Clips;
        if (prepared.Helpers > 0) Report(59,$"Added {prepared.Helpers} scale compensation bones to preserve Havok quaternion/scale inheritance ({sourceBoneCount} original bones).");
        string parent=Path.GetDirectoryName(destination)??throw new IOException("Export folder has no parent.");
        Directory.CreateDirectory(parent);
        string staging=Path.Combine(parent,".he1scu-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            Report(60,"Writing Godot 3 ArrayMesh, Skeleton and rest-relative quaternion Animation resources...");
            GodotWriter.Write(model,clips,staging,token,message=>Report(64,message));
            bool binary=!string.IsNullOrWhiteSpace(request.AcornEditorPath);
            if(binary)
            {
                Report(75,"Loading resources in Acorn and saving native binary mesh, animation and scene files...");
                await AcornResourceCompiler.CompileAsync(request.AcornEditorPath!,staging,model,clips,log=>Report(82,log),token);
            }
            else Report(85,"Text resources written. Acorn binary validation skipped because no editor was selected.");
            Report(90,$"Converted {materials.Count} HE1 materials to textured SpatialMaterials. Custom HE1 lighting is approximated using built-in shading.");
            var result=new ConversionResult(destination,model.Surfaces.Count,model.Surfaces.Sum(s=>s.Vertices.Count),model.Bones.Count,clips.Count,binary);
            var manifest=new
            {
                converter="HE1ToSCU 0.3",result,sourceModel=Path.GetFullPath(request.ModelPath),sourceSkeleton=request.SkeletonPath,
                sourceAnimations=request.AnimationPaths,spatialProfile=new{scale=Spatial.Scale,basis="Y-up preserved"},
                outputScene=binary?ExportNames.SceneBinary(model):ExportNames.SceneText(model),outputMesh=binary?ExportNames.MeshBinary(model):ExportNames.MeshText(model),
                animations=clips.Select(c=>new{name=c.Name,textFile=ExportNames.AnimationText(c),binaryFile=binary?ExportNames.AnimationBinary(c):null,rootMotionSamples=c.RootMotion?.Samples.Count??0}),
                materials=materials.Values.Select(m=>new{m.Name,m.Shader,m.AlphaThreshold,m.DoubleSided,m.Additive,parameters=m.Parameters.ToDictionary(p=>p.Key,p=>p.Value.Select(v=>new[]{v.X,v.Y,v.Z,v.W}).ToArray()),m.Textures}),
                sourceBoneCount,scaleCompensationBones=prepared.Helpers,
                limitations=new[]{"Custom HE1 lighting, colored specular maps and reflection/matcap shaders are approximations to SpatialMaterial.","Original HE1 rig exported; SCU player retargeting and gameplay wiring pending.","In-game acceptance and renderer parity not established."}
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
    public static async Task CompileAsync(string editor,string directory,ModelAsset model,IReadOnlyList<AnimationClip> clips,Action<string> log,CancellationToken token)
    {
        File.WriteAllText(Path.Combine(directory,"project.godot"),"config_version=4\n[application]\nconfig/name=\"HE1SCU Resource Compile\"\n[rendering]\nquality/driver/driver_name=\"GLES2\"\n");
        string script=BuildScript(model,clips);
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
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,timeout.Token);
        try { await process.WaitForExitAsync(linked.Token);await Task.WhenAll(stdout,stderr); }
        catch(OperationCanceledException)
        {
            if(!process.HasExited)process.Kill(true);
            await Task.WhenAll(stdout,stderr);
            if(token.IsCancellationRequested)throw;
            throw new TimeoutException("Acorn resource compilation exceeded three minutes.");
        }
        await File.WriteAllLinesAsync(Path.Combine(directory,logFile),lines,CancellationToken.None);
        if(process.ExitCode!=0||!lines.Any(l=>l.Contains(marker,StringComparison.Ordinal))||lines.Any(l=>l.Contains("SCRIPT ERROR",StringComparison.Ordinal)||l.StartsWith("ERROR:",StringComparison.Ordinal)))
            throw new InvalidDataException("Acorn could not validate the exported resources. See acorn-validation.log in the diagnostic staging folder.");
        }
        await RunAsync(scriptPath,"HE1SCU_VALIDATED","acorn-validation.log");
        string verifyPath=Path.Combine(directory,"__verify.gd");
        File.WriteAllText(verifyPath,BuildVerifyScript(model,clips));
        await RunAsync(verifyPath,"HE1SCU_BINARY_VALIDATED","acorn-binary-validation.log");
        File.Delete(verifyPath);
        foreach(string file in new[]{ExportNames.SceneBinary(model),ExportNames.MeshBinary(model)})
        {
            byte[] header=File.ReadAllBytes(Path.Combine(directory,file)).Take(4).ToArray();
            if(!header.SequenceEqual("RSRC"u8.ToArray())&&!header.SequenceEqual("RSCC"u8.ToArray()))throw new InvalidDataException($"Acorn did not produce a binary Godot resource: {file}.");
        }
        File.Delete(scriptPath);File.Delete(Path.Combine(directory,"project.godot"));
    }
    private static string BuildVerifyScript(ModelAsset model,IReadOnlyList<AnimationClip> clips)=>$$"""
        extends SceneTree
        func fail(message):
            printerr("HE1SCU_FAILED: " + message)
            quit()
        func _init():
            var packed = load({{ExportNames.Literal("res://" + ExportNames.SceneBinary(model))}})
            if packed == null:
                fail("binary scene load in a fresh process")
                return
            var instance = packed.instance()
            get_root().add_child(instance)
            var skeleton = instance.get_node("Skeleton")
            var mesh = skeleton.get_node("Mesh").mesh
            if skeleton.get_bone_count() != {{model.Bones.Count}} or mesh.get_surface_count() != {{model.Surfaces.Count}}:
                fail("binary scene counts")
                return
            var standalone_mesh = load({{ExportNames.Literal("res://" + ExportNames.MeshBinary(model))}})
            if standalone_mesh == null or standalone_mesh.get_surface_count() != {{model.Surfaces.Count}}:
                fail("standalone mesh load")
                return
            for surface in range(mesh.get_surface_count()):
                if mesh.surface_get_material(surface) == null or standalone_mesh.surface_get_material(surface) == null:
                    fail("binary material load")
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
                var expected_checks = {{PoseValidation.GodotChecks(model,clips)}}
                for entry in expected_checks:
                    var checked_animation = player.get_animation(entry[0])
                    var original_loop = checked_animation.loop
                    checked_animation.loop = false
                    player.play(entry[0])
                    player.seek(entry[1], true)
                    var actual_root = instance.transform
                    if actual_root.origin.distance_to(entry[4].origin) > 0.02:
                        fail("root motion translation mismatch: " + str(entry[0]))
                        return
                    for axis in range(3):
                        if actual_root.basis[axis].distance_to(entry[4].basis[axis]) > 0.003:
                            fail("root motion heading mismatch: " + str(entry[0]))
                            return
                    for bone in range(skeleton.get_bone_count()):
                        var actual = skeleton.get_bone_global_pose(bone).origin
                        if is_nan(actual.length()) or actual.distance_to(entry[2][bone]) > 0.02:
                            fail("binary bone pose mismatch")
                            return
                        for axis in range(3):
                            if skeleton.get_bone_global_pose(bone).basis[axis].distance_to(entry[3][bone][axis]) > 0.003:
                                fail("binary bone rotation/scale mismatch")
                                return
                    checked_animation.loop = original_loop
            instance.free()
            print("HE1SCU_BINARY_VALIDATED bones={{model.Bones.Count}} surfaces={{model.Surfaces.Count}} animations={{clips.Count}}")
            quit()
        """;
    private static string BuildScript(ModelAsset model,IReadOnlyList<AnimationClip> clips)=>$$"""
        extends SceneTree
        func fail(message):
            printerr("HE1SCU_FAILED: " + message)
            quit()
        func clear_material_paths(material):
            material.resource_path = ""
            for property_name in ["albedo_texture", "normal_texture", "roughness_texture", "emission_texture", "rim_texture"]:
                var texture = material.get(property_name)
                if texture != null:
                    texture.resource_path = ""
        func _init():
            var packed = load({{ExportNames.Literal("res://" + ExportNames.SceneText(model))}})
            if packed == null:
                fail("scene load")
                return
            var instance = packed.instance()
            get_root().add_child(instance)
            var skeleton = instance.get_node("Skeleton")
            var mesh_instance = skeleton.get_node("Mesh")
            var mesh = mesh_instance.mesh
            if skeleton.get_bone_count() != {{model.Bones.Count}} or mesh.get_surface_count() != {{model.Surfaces.Count}}:
                fail("bone/surface counts")
                return
            for surface in range(mesh.get_surface_count()):
                var arrays = mesh.surface_get_arrays(surface)
                if arrays[0].size() == 0 or arrays[8].size() == 0:
                    fail("empty vertex/index buffer")
                    return
                var material = mesh.surface_get_material(surface)
                if material != null:
                    clear_material_paths(material)
            mesh.resource_path = ""
            if ResourceSaver.save({{ExportNames.Literal("res://" + ExportNames.MeshBinary(model))}}, mesh, 65) != OK:
                fail("mesh save")
                return
            mesh.take_over_path({{ExportNames.Literal("res://" + ExportNames.MeshBinary(model))}})
            # Embed resources in the binary scene so it can be moved into any SCU folder.
            # This also avoids Acorn's res://./ external-path/subresource cache mismatch.
            for surface in range(mesh.get_surface_count()):
                var material = mesh.surface_get_material(surface)
                if material != null:
                    clear_material_paths(material)
            mesh.resource_path = ""
            if {{clips.Count}} > 0:
                var player = instance.get_node("AnimationPlayer")
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
                    var filename = animation.resource_path.get_file().replace(".tres", ".anim")
                    if ResourceSaver.save("res://" + filename, animation, 1) != OK:
                        fail("animation save")
                        return
                    animation.resource_path = ""
                player.stop(true)
                for bone in range(skeleton.get_bone_count()):
                    skeleton.set_bone_pose(bone, Transform())
                var expected_checks = {{PoseValidation.GodotChecks(model,clips)}}
                for entry in expected_checks:
                    var checked_animation = player.get_animation(entry[0])
                    var original_loop = checked_animation.loop
                    checked_animation.loop = false
                    player.play(entry[0])
                    player.seek(entry[1], true)
                    var actual_root = instance.transform
                    if actual_root.origin.distance_to(entry[4].origin) > 0.02:
                        fail("root motion translation mismatch: " + str(entry[0]))
                        return
                    for axis in range(3):
                        if actual_root.basis[axis].distance_to(entry[4].basis[axis]) > 0.003:
                            fail("root motion heading mismatch: " + str(entry[0]))
                            return
                    for bone in range(skeleton.get_bone_count()):
                        var actual = skeleton.get_bone_global_pose(bone).origin
                        if actual.distance_to(entry[2][bone]) > 0.02:
                            fail("pose mismatch: " + str(entry[0]) + " time=" + str(entry[1]) + " bone=" + str(bone) + " actual=" + str(actual) + " expected=" + str(entry[2][bone]))
                            return
                        for axis in range(3):
                            if skeleton.get_bone_global_pose(bone).basis[axis].distance_to(entry[3][bone][axis]) > 0.003:
                                fail("bone rotation/scale mismatch: " + str(entry[0]) + " bone=" + str(bone))
                                return
                    checked_animation.loop = original_loop
                player.stop(true)
                for bone in range(skeleton.get_bone_count()):
                    skeleton.set_bone_pose(bone, Transform())
            instance.transform = Transform()
            var output = PackedScene.new()
            if output.pack(instance) != OK or ResourceSaver.save({{ExportNames.Literal("res://" + ExportNames.SceneBinary(model))}}, output, 65) != OK:
                fail("scene save")
                return
            var reloaded = ResourceLoader.load({{ExportNames.Literal("res://" + ExportNames.SceneBinary(model))}}, "PackedScene", true)
            if reloaded == null:
                fail("binary scene reload")
                return
            var check = reloaded.instance()
            if check.get_node("Skeleton").get_bone_count() != {{model.Bones.Count}}:
                fail("binary skeleton reload")
                return
            check.free()
            instance.free()
            print("HE1SCU_VALIDATED bones={{model.Bones.Count}} surfaces={{model.Surfaces.Count}} animations={{clips.Count}}")
            quit()
        """;
}
