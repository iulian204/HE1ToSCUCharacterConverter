using System.Diagnostics;
using System.Numerics;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class SceneVisibilityHotfixTests
{
    [AcornFact]
    public async Task StandardAndNamedScenesRenderOnTheScuCharacterLayer()
    {
        string dir=Path.Combine(Path.GetTempPath(),"HE1SCU-visibility-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            Vertex V(float x,float y)=>new(new(x,y,0),Vector3.UnitZ,new(1,0,0,1),Vector2.Zero,Vector2.Zero,Vector4.One,[0,0,0,0],new(1,0,0,0));
            var model=new ModelAsset("source",[new("root",-1,Matrix4x4.Identity)],[new("body","opaque",[V(-.1f,-.1f),V(.1f,-.1f),V(0,.1f)],[0,1,2])]);
            foreach(bool named in new[]{false,true})
            {
                string sub=Path.Combine(dir,named?"named":"standard");
                var actual=named?model with{ExportIdentity=new("chr_Sonic_SD","Scene Root","Skeleton","Sonic_Body","Sonic_Body")}:model;
                GodotWriter.Write(actual,[],sub,default);
                string text=File.ReadAllText(Path.Combine(sub,ExportNames.SceneText(actual)));
                Assert.Contains("layers = 3",text);
            }
            File.WriteAllText(Path.Combine(dir,"project.godot"),"config_version=4\n[rendering]\nquality/driver/driver_name=\"GLES3\"\n");
            string script="""
                extends SceneTree
                func _init():
                    call_deferred("check")
                func check():
                    for path in ["res://standard/source.tscn","res://named/chr_Sonic_SD.tscn"]:
                        var scene=load(path).instance()
                        var viewport=Viewport.new()
                        viewport.size=Vector2(64,64)
                        viewport.transparent_bg=true
                        viewport.render_target_update_mode=Viewport.UPDATE_ALWAYS
                        get_root().add_child(viewport)
                        viewport.add_child(scene)
                        var skeleton=scene.get_node("Skeleton")
                        var mesh=skeleton.get_child(0)
                        for i in range(mesh.mesh.get_surface_count()):
                            var material=mesh.mesh.surface_get_material(i)
                            material.flags_unshaded=true
                            material.params_cull_mode=2
                        var camera=Camera.new()
                        camera.translation=Vector3(0,0,4)
                        camera.cull_mask=2
                        viewport.add_child(camera)
                        camera.current=true
                        yield(self,"idle_frame")
                        VisualServer.force_draw(true)
                        var image=viewport.get_texture().get_data()
                        var visible=0
                        image.lock()
                        for y in range(64):
                            for x in range(64):
                                if image.get_pixel(x,y).a>.5:
                                    visible+=1
                        image.unlock()
                        if visible<8:
                            printerr("VISIBILITY_FAILED "+path+" pixels="+str(visible))
                            quit()
                            return
                        print("VISIBLE_CHARACTER_LAYER "+path+" pixels="+str(visible))
                        viewport.free()
                    quit()
                """;
            string output=await Run(dir,script);
            Assert.Contains("VISIBLE_CHARACTER_LAYER res://standard",output);Assert.Contains("VISIBLE_CHARACTER_LAYER res://named",output);
        }
        finally{Directory.Delete(dir,true);}
    }

    [LocalUnleashedFact]
    public async Task ActualUnleashedExportsOpenThroughPublicTextAndBinaryNamesAfterMoving()
    {
        string root=Path.Combine(Path.GetTempPath(),"HE1SCU-entrypoints-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            foreach(var (named,super) in new[]{(false,false),(true,false),(true,true)})
            {
                string folder=named?(super?"player_Supersonic":"player_Sonic"):"standard";
                string output=Path.Combine(root,folder);
                await new ConversionService().ConvertAsync(new(LocalUnleashedFactAttribute.Source,null,[],output,AcornFactAttribute.Engine,
                    SourceGame.SonicUnleashed,GenerateMouthL:true,ExportPrrMaterials:true,MatchSonicAssetNames:named,SuperSonic:super));
                string stem=named?(super?"chr_SuperSonic_SD":"chr_Sonic_SD"):"SonicRoot";
                Assert.False(File.Exists(Path.Combine(output,stem+".tscn.remap")));
                string binary=named?stem+".tscn.converted.res":stem+".scn";
                File.WriteAllText(Path.Combine(root,"project.godot"),"config_version=4\n[rendering]\nquality/driver/driver_name=\"GLES3\"\n");
                string script=$$"""
                    extends SceneTree
                    func _init():
                        for path in ["res://{{folder}}/{{stem}}.tscn","res://{{folder}}/{{binary}}"]:
                            var packed=ResourceLoader.load(path,"PackedScene",true)
                            if packed==null:
                                printerr("ENTRYPOINT_FAILED "+path)
                                quit()
                                return
                            var scene=packed.instance()
                            for child in scene.get_node("Skeleton").get_children():
                                if child is MeshInstance and child.layers!=3:
                                    printerr("LAYER_FAILED "+path)
                                    quit()
                                    return
                            scene.free()
                            print("ENTRYPOINT_OK "+path)
                        quit()
                    """;
                Assert.Contains("ENTRYPOINT_OK res://"+folder+"/"+stem+".tscn",await Run(root,script));
            }
        }
        finally{Directory.Delete(root,true);}
    }
    private static async Task<string> Run(string project,string script)
    {
        string file=Path.Combine(project,"check.gd");File.WriteAllText(file,script);
        var info=new ProcessStartInfo(AcornFactAttribute.Engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"--no-window","--path",project,"--script",file})info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));}
        finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}}
        string result=await stdout+await stderr;Assert.True(!result.Contains("ERROR:",StringComparison.Ordinal),result);Assert.Equal(0,process.ExitCode);return result;
    }
}
public sealed class LocalUnleashedFactAttribute:FactAttribute
{
    public static string Source=>@"C:\Users\jigai\Desktop\CorrectSUSonic\Sonic\SonicRoot.model";
    public LocalUnleashedFactAttribute(){if(!File.Exists(Source)||!File.Exists(AcornFactAttribute.Engine))Skip="Requires local Unleashed model/material bank and Acorn for the scene-entrypoint regression.";}
}
