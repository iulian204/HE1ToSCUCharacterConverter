using HE1ToSCU.Core;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

if(args.Length<3){Console.WriteLine("ScuInventory <inventory.json> <Generations Sonic directory> <Unleashed Sonic directory>");return 2;}
var inventory=ScuInventory.Default;
var profiles=new List<ScuCachedSource>();
var evidence=new List<object>();
foreach(var(game,folder,modelName)in new[]{(SourceGame.SonicGenerations,args[1],"chr_Sonic_HD"),(SourceGame.SonicUnleashed,args[2],"SonicRoot")})
{
    var model=new He1ModelReader().Read(Path.Combine(folder,modelName+".model"),default,game);
    var bones=new HavokReader().Read(Path.Combine(folder,modelName+".skl.hkx"),default,game).Bones;
    var lookup=model.Bones.Select((b,i)=>(b.Name,i)).ToDictionary(b=>b.Name,b=>b.i);
    int[] map=bones.Select(b=>lookup.GetValueOrDefault(b.Name,lookup.GetValueOrDefault(b.Name.Replace("@LT","",StringComparison.Ordinal),-1))).ToArray();
    var reference=model.Bones.Select(b=>Pose.FromMatrix(b.Rest)).ToArray();
    for(int i=0;i<map.Length;i++)if(map[i]>=0)reference[map[i]]=Pose.FromMatrix(bones[i].Rest);
    foreach(string file in Directory.GetFiles(folder,"*.anm.hkx").Order(StringComparer.Ordinal))
    {
        var asset=new HavokReader().Read(file,default,game);
        foreach(var raw in asset.Animations)
        {
            int[] mapped=raw.TrackToBone.Select(i=>i>=0&&i<map.Length?map[i]:-1).ToArray();
            if(mapped.Any(i=>i<0)){evidence.Add(new{game=game.ToString(),file=Path.GetFileName(file),status="unsupported bone binding"});continue;}
            var clip=AnimationSampling.Complete(raw with{TrackToBone=mapped},reference);
            foreach(var target in inventory.Profiles)
            {
                var motion=MotionDescriptor.Analyze(model,clip,target);if(motion==null)continue;
                var ranked=target.Animations.Where(t=>t.Eligible).Select(t=>(t.Name,Score:ScuNameMatcher.Score(clip,motion,t,target.Id)))
                    .OrderByDescending(p=>p.Score.Score).ThenBy(p=>p.Name,StringComparer.Ordinal).Take(16).ToArray();
                profiles.Add(new(){Game=game.ToString(),Name=clip.Name,Profile=target.Id,Fingerprint=ScuNameMatcher.SourceFingerprint(clip,motion),Candidates=ranked.ToDictionary(p=>p.Name,p=>p.Score)});
            }
            evidence.Add(new{game=game.ToString(),file=Path.GetFileName(file),name=clip.Name,status="decoded; inferred candidate profiles, not verified gameplay equivalence",clip.Duration,frames=clip.Frames.Length,
                sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant()});
        }
        if(evidence.Count%25==0)Console.WriteLine($"Analyzed {evidence.Count} source animations");
    }
}
string destination=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
File.WriteAllText(Path.Combine(destination,"animation-profiles.json"),JsonSerializer.Serialize(new ScuProfileCache{
    SchemaVersion=1,InventoryVersion=inventory.InventoryVersion,ScoringVersion=ScuNameMatcher.ScoringVersion,Entries=profiles.ToArray()},ScuInventory.JsonOptions));
File.WriteAllText(Path.Combine(destination,"source-profile-provenance.json"),JsonSerializer.Serialize(new{schemaVersion=1,inventory.InventoryVersion,descriptorVersion=1,entries=evidence},ScuInventory.JsonOptions));
var json=JsonNode.Parse(File.ReadAllText(args[0]))!;
foreach(var p in json["profiles"]!.AsArray())foreach(var a in p!["animations"]!.AsArray())
    a!["semantic"]=JsonSerializer.SerializeToNode(AnimationSemantics.Classify(a["name"]!.GetValue<string>()),ScuInventory.JsonOptions);
File.WriteAllText(args[0],json.ToJsonString(ScuInventory.JsonOptions));
Console.WriteLine($"Built {profiles.Count} game/profile-specific source records from {evidence.Count} real animation files.");
return 0;
