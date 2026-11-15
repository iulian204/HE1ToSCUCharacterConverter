using System.Numerics;
using System.Text.Json;
using HE1ToSCU.Core;
using Xunit;

namespace HE1ToSCU.Tests;

public class ScuNamingTests
{
    private static ModelAsset Model()=>new("original",[],[]);
    private static AnimationClip Clip(string name)=>new(name,1,[],[[],[]]);
    [Fact]
    public void BundledInventoryContainsActualBothFormsAndAllReferenceMetadata()
    {
        var data=ScuInventory.Default;
        Assert.Equal(720,data.Files.Length); Assert.Equal(104,data.References.Length);
        foreach(var (super,count,eligible,stem,mesh) in new[]{(false,304,303,"chr_Sonic_SD","Sonic_Body"),(true,78,75,"chr_SuperSonic_SD","mesh")})
        {
            var p=data.Profile(super);Assert.Equal(count,p.Animations.Length);Assert.Equal(eligible,p.Animations.Count(a=>a.Eligible));
            Assert.Equal(stem,p.SceneStem);Assert.Equal(mesh,p.MeshResourceName);Assert.Equal("Scene Root",p.RootNodeName);Assert.Equal("Skeleton",p.SkeletonNodeName);
            Assert.Equal(stem+".tscn.converted.res",p.NativeSceneFile);Assert.Contains(p.NativeSceneFile!,p.SceneRemapText!);
            Assert.All(p.Animations.Where(a=>a.Motion!=null),a=>a.Motion!.Validate());
        }
        Assert.False(data.Profile(true).Animations.Single(a=>a.Name=="ssn_boost_loop").Loop);
    }
    [Theory]
    [InlineData("sn_running_loop","run","loop","")]
    [InlineData("sn_jet_l_loop","jet","loop","left")]
    [InlineData("sn_jet_r_loop","jet","loop","right")]
    [InlineData("sn_jump_s","jump","start","")]
    [InlineData("sn_jump_e","jump","end","")]
    public void SemanticsPreserveActionPhaseAndDirection(string name,string action,string phase,string direction)
    {
        var s=AnimationSemantics.Classify(name);Assert.Equal(action,s.Action);Assert.Equal(phase,s.Phase);Assert.Equal(direction,s.Direction);
    }
    [Fact]
    public void GlobalAssignmentAvoidsGreedyConflictAndRejectsInvalidShape()
    {
        Assert.Equal(new[]{1,0},ScuNameMatcher.MaximumAssignment(new double[,]{{.9,.8},{.85,.1}}));
        Assert.Throws<ArgumentException>(()=>ScuNameMatcher.MaximumAssignment(new double[2,1]));
    }
    [Fact]
    public void GlobalAssignmentMatchesIndependentExhaustiveOptimum()
    {
        var random=new Random(17);
        for(int fixture=0;fixture<20;fixture++)
        {
            var w=new double[3,4];for(int r=0;r<3;r++)for(int c=0;c<4;c++)w[r,c]=random.NextDouble();
            double best=double.NegativeInfinity;
            for(int a=0;a<4;a++)for(int b=0;b<4;b++)for(int c=0;c<4;c++)if(a!=b&&b!=c&&a!=c)best=Math.Max(best,w[0,a]+w[1,b]+w[2,c]);
            var selected=ScuNameMatcher.MaximumAssignment(w);Assert.Equal(best,Enumerable.Range(0,3).Sum(r=>w[r,selected[r]]),10);
        }
    }
    [Fact]
    public void StrongContradictionsCannotBeOverriddenByMotionOrDuration()
    {
        var target=ScuInventory.Default.Profile(false).Animations.Single(a=>a.Name=="sn_run_loop");
        var exact=Clip(target.Name) with{Duration=target.Duration};
        Assert.True(ScuNameMatcher.Score(exact,target.Motion,target,"Sonic").Score>.78);
        Assert.True(ScuNameMatcher.Score(exact with{Name="sn_walk_loop"},target.Motion,target,"Sonic").Score<=.28);
        var left=ScuInventory.Default.Profile(false).Animations.First(a=>a.Semantic.Direction=="left");
        Assert.True(ScuNameMatcher.Score(Clip(left.Name.Replace("_l_","_r_")),null,left,"Sonic").Score<=.76);
    }
    [Fact]
    public void OneWeakSourceFillsEveryEligibleSlotWithoutChangingSourceIdentity()
    {
        var source=Clip("unrecognized_action");var plan=ScuNameMatcher.Match(Model(),[source],SourceGame.SonicGenerations,true);
        Assert.Equal(75,plan.Matches.Count);Assert.Equal(74,plan.Matches.Count(a=>a.Reused));Assert.Equal(75,plan.Matches.Select(a=>a.TargetName).Distinct().Count());
        Assert.All(plan.Matches,m=>Assert.Equal("Low",m.Confidence));Assert.Null(source.ExportIdentity);
        var again=ScuNameMatcher.Match(Model(),[source],SourceGame.SonicGenerations,true);
        Assert.Equal(plan.Matches.Select(m=>(m.SourceName,m.TargetName,m.Score)),again.Matches.Select(m=>(m.SourceName,m.TargetName,m.Score)));
        Assert.Equal(78,ScuNameMatcher.Match(Model(),[],SourceGame.SonicGenerations,true).Unfilled.Count);
    }
    [Fact]
    public void OptionalCorrectionsApplyOnlyKnownSourceAndTargetAndReportsRemainReusable()
    {
        string dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
        try
        {
            var target=ScuInventory.Default.Profile(true).Animations.First(a=>a.Eligible).Name;
            string path=Path.Combine(dir,"map.json");File.WriteAllText(path,JsonSerializer.Serialize(new{profile="SuperSonic",overrides=new Dictionary<string,string>{{target,"sn_idle00"},{"not_a_target","sn_idle00"}}}));
            var plan=ScuNameMatcher.Match(Model(),[Clip("sn_idle00"),Clip("sn_jump_s")],SourceGame.SonicGenerations,true,path);
            Assert.Equal("manual_override",plan.Matches.Single(m=>m.TargetName==target).Method);Assert.Single(plan.Warnings.Where(w=>w.Contains("Stale")));
            plan.WriteReports(dir);using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"scu-animation-mapping.json")));
            Assert.Equal(75,json.RootElement.GetProperty("matches").GetArrayLength());Assert.True(json.RootElement.TryGetProperty("overrides",out _));
        }
        finally{Directory.Delete(dir,true);}
    }
    [Fact]
    public void ExportNamesKeepSourceLoopingAndOriginalNamesWhenDisabled()
    {
        var model=Model();var clip=Clip("sn_run_loop");Assert.Equal("original.tscn",ExportNames.SceneText(model));Assert.Equal("sn_run_loop.anim",ExportNames.AnimationBinary(clip));
        var p=ScuInventory.Default.Profile(true);model=model with{ExportIdentity=new(p.SceneStem,p.RootNodeName,p.SkeletonNodeName,p.MeshNodeName,p.MeshResourceName,p.NativeSceneFile,p.SceneRemapText)};
        var named=clip with{ExportIdentity=new("ssn_fall","renamed","renamed")};Assert.True(ExportNames.AnimationLoops(named));Assert.Equal(clip.Frames,named.Frames);
        Assert.Equal("animations/ssn_fall.anim",ExportNames.AnimationBinary(named));Assert.Equal("chr_SuperSonic_SD.tscn.converted.res",ExportNames.SceneBinary(model));
        Assert.Equal("mesh.mesh",ExportNames.MeshBinary(model));Assert.Equal("renamed",ExportNames.AnimationName(named));ExportNames.Validate(model,[named]);
        Assert.Throws<InvalidDataException>(()=>ExportNames.Validate(model,[named,named]));
    }
    [Fact]
    public void StandardWriterPreservesOriginalModelAnimationAndSeparatedMeshFilenames()
    {
        string dir=Path.Combine(Path.GetTempPath(),"HE1SCU-standard-names-"+Guid.NewGuid().ToString("N"));
        try
        {
            var bone=new Bone("Root",-1,Matrix4x4.Identity);
            var model=new ModelAsset("chr_Example_HD",[bone],[],MouthMeshes:[new("Mouth",[],"Root"),new("Mouth_L",[],"Root",true)]);
            var pose=new Pose(Vector3.Zero,Quaternion.Identity,Vector3.One);var clip=new AnimationClip("sn_original_loop",1,[0],[[pose],[pose]]);
            GodotWriter.Write(model,[clip],dir,default);
            Assert.True(File.Exists(Path.Combine(dir,"chr_Example_HD.tscn")));
            foreach(string part in new[]{"","_Mouth","_Mouth_L"})
            {
                Assert.True(File.Exists(Path.Combine(dir,"chr_Example_HD"+part+".mesh.tres")));
                Assert.Equal("chr_Example_HD"+part+".mesh",ExportNames.MeshBinary(model,part==""?"Mesh":part[1..]));
            }
            Assert.True(File.Exists(Path.Combine(dir,"sn_original_loop.tres")));Assert.Equal("sn_original_loop.anim",ExportNames.AnimationBinary(clip));
            Assert.False(File.Exists(Path.Combine(dir,"Mesh.tres")));Assert.False(File.Exists(Path.Combine(dir,"Mesh.mesh.tres")));
            Assert.Contains("chr_Example_HD.mesh.tres",File.ReadAllText(Path.Combine(dir,"chr_Example_HD.tscn")));
        }
        finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Fact]
    public void MotionDescriptorsUseActualChannelsAndDoNotRewardMissingAnchors()
    {
        MotionDescriptor D(float x)=>new(){Mask=Enumerable.Repeat(true,6).ToArray(),Samples=Enumerable.Range(0,17).Select(i=>Enumerable.Repeat(x,18).ToArray()).ToArray(),RootSamples=Enumerable.Range(0,17).Select(i=>new float[]{x*i,0,0}).ToArray()};
        var a=D(0);var b=D(1);Assert.Equal(1,MotionDescriptor.PoseSimilarity(a,a));Assert.True(MotionDescriptor.PoseSimilarity(a,b)<.1);
        b.Mask=new bool[6];Assert.Equal(0,MotionDescriptor.PoseSimilarity(a,b));
        Assert.NotEqual(ScuNameMatcher.SourceFingerprint(Clip("x"),a),ScuNameMatcher.SourceFingerprint(Clip("x") with{Duration=2},a));
    }
    [AcornFact]
    public async Task LargeNamedBankEmbedsAnimationsAndReloadsInsideMovedProjectFolder()
    {
        string dir=Path.Combine(Path.GetTempPath(),"HE1SCU-named-bank-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            Vertex V(Vector3 position)=>new(position,Vector3.UnitZ,new(1,0,0,1),Vector2.Zero,Vector2.Zero,Vector4.One,[0,0,0,0],new(1,0,0,0));
            var model=new ModelAsset("source",Enumerable.Range(0,80).Select(i=>new Bone("bone"+i,i==0?-1:0,Matrix4x4.Identity)).ToList(),
                [new("body","opaque",[V(Vector3.Zero),V(Vector3.UnitX),V(Vector3.UnitY)],[0,1,2])],ExportIdentity:new("chr_Sonic_SD","Scene Root","Skeleton","Sonic_Body","Sonic_Body","chr_Sonic_SD.tscn.converted.res"));
            var pose=new Pose(Vector3.Zero,Quaternion.Identity,Vector3.One);
            var source=new AnimationClip("source_loop",.1f,[0],[[pose],[pose]]);
            var clips=Enumerable.Range(0,301).Select(i=>source with{ExportIdentity=new("target"+i,"target"+i,"target"+i)}).ToArray();
            GodotWriter.Write(model,clips,dir,default);
            var messages=new List<string>();
            await AcornResourceCompiler.CompileAsync(AcornFactAttribute.Engine,dir,model,clips,model,Enumerable.Repeat(source,301).ToArray(),messages.Add,default);
            Assert.Contains(messages,l=>l.Contains("HE1SCU_BINARY_VALIDATED"));Assert.Equal(301,Directory.GetFiles(Path.Combine(dir,"animations"),"*.anim").Length);
            Directory.CreateDirectory(Path.Combine(dir,"moved"));
            File.Copy(Path.Combine(dir,ExportNames.SceneBinary(model)),Path.Combine(dir,"moved","scene.res"));
            File.WriteAllText(Path.Combine(dir,"project.godot"),"config_version=4");
            File.WriteAllText(Path.Combine(dir,"nested.gd"),"extends SceneTree\nfunc _init():\n    var packed=load(\"res://moved/scene.res\")\n    if packed!=null:\n        var scene=packed.instance()\n        if scene.get_node(\"AnimationPlayer\").get_animation_list().size()==301:\n            print(\"NESTED_OK\")\n        scene.free()\n    quit()\n");
            var info=new System.Diagnostics.ProcessStartInfo(AcornFactAttribute.Engine){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            foreach(var argument in new[]{"--no-window","--path",dir,"--script",Path.Combine(dir,"nested.gd")})info.ArgumentList.Add(argument);
            using var process=System.Diagnostics.Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(1));}
            finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}}
            Assert.Contains("NESTED_OK",await stdout);Assert.DoesNotContain("ERROR:",await stderr);
        }
        finally{Directory.Delete(dir,true);}
    }
}
