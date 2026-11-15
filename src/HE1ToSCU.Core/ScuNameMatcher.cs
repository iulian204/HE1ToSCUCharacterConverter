using System.Text;
using System.Text.Json;

namespace HE1ToSCU.Core;

public sealed record ScuCandidate(string SourceName,double Score,string[] Evidence,bool Cached=false);
public sealed record ScuAnimationMatch(string SourceName,string TargetName,string ResourceName,string PlayerKey,double Score,string Confidence,
    string Method,string[] Evidence,bool Reused,IReadOnlyList<ScuCandidate> Alternatives);
public sealed record ScuUnfilledSlot(string Name,string Reason);
public sealed class ScuNamingPlan
{
    public string InventoryVersion {get;init;}="";
    public required string Profile {get;init;}
    public required string SourceModel {get;init;}
    public required ModelExportIdentity TargetModel {get;init;}
    public List<ScuAnimationMatch> Matches {get;}=[];
    public List<ScuUnfilledSlot> Unfilled {get;}=[];
    public List<string> Warnings {get;}=[];
    public string[] UnusedSources {get;set;}=[];
    public int CachedCandidates {get;set;}
    public string AssignmentPolicy=>"Global maximum-weight unique assignment followed by intentional best-candidate reuse for remaining eligible target slots. No confidence approval gate.";
    public string RigPolicy=>"Source bones, transforms, timings and source-name-derived looping retained; no retargeting. Additional mouth meshes remain weighted source parts.";
    public void WriteReports(string directory)
    {
        File.WriteAllText(Path.Combine(directory,"scu-animation-mapping.json"),JsonSerializer.Serialize(new {
            schemaVersion=1,InventoryVersion,Profile,SourceModel,TargetModel,AssignmentPolicy,RigPolicy,
            Matches,Unfilled,UnusedSources,Warnings,CachedCandidates,overrides=new Dictionary<string,string>()},ScuInventory.JsonOptions));
        var text=new StringBuilder($"SCU naming inventory {InventoryVersion} | {Profile}\nModel: {SourceModel} -> {TargetModel.SceneStem}\nSkeleton node: {TargetModel.SkeletonNodeName}; source bones retained\n{AssignmentPolicy}\n{RigPolicy}\n\n");
        foreach(string level in new[]{"High","Medium","Low"})text.AppendLine($"{level}: {Matches.Count(m=>m.Confidence==level)}");
        text.AppendLine($"Reused: {Matches.Count(m=>m.Reused)} | Unfilled: {Unfilled.Count} | Unused source clips: {UnusedSources.Length}\n");
        foreach(var m in Matches)text.AppendLine($"{m.TargetName} <- {m.SourceName} | {m.Confidence} {m.Score:F3} | {m.Method}{(m.Reused?" | intentional reuse":"")}\n  {string.Join("; ",m.Evidence)}");
        foreach(var s in Unfilled)text.AppendLine($"UNFILLED {s.Name}: {s.Reason}");
        foreach(string warning in Warnings)text.AppendLine("WARNING "+warning);
        File.WriteAllText(Path.Combine(directory,"scu-animation-mapping.txt"),text.ToString());
    }
}
public sealed class ScuCachedSource
{
    public string Game {get;set;}="";
    public string Name {get;set;}="";
    public string Fingerprint {get;set;}="";
    public string Profile {get;set;}="";
    public Dictionary<string,ScuCandidate> Candidates {get;set;}=[];
}
public sealed class ScuProfileCache
{
    public int SchemaVersion {get;set;}
    public int ScoringVersion {get;set;}
    public string InventoryVersion {get;set;}="";
    public ScuCachedSource[] Entries {get;set;}=[];
}
public static class ScuNameMatcher
{
    public const int ScoringVersion=1;
    public static string SourceFingerprint(AnimationClip clip,MotionDescriptor motion)=>motion.Fingerprint()+":"+clip.Duration.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)+":"+ExportNames.AnimationLoops(clip);
    private static readonly Lazy<ScuProfileCache?> Cache=new(()=>{
        using var stream=typeof(ScuNameMatcher).Assembly.GetManifestResourceStream("SCU/animation-profiles.json");
        if(stream==null)return null;
        var cache=JsonSerializer.Deserialize<ScuProfileCache>(stream,ScuInventory.JsonOptions);
        return cache?.SchemaVersion==1&&cache.ScoringVersion==ScoringVersion&&cache.InventoryVersion==ScuInventory.Default.InventoryVersion?cache:null;
    });
    public static ScuCandidate Score(AnimationClip source,MotionDescriptor? motion,ScuAnimation target,string targetProfile)
    {
        var a=AnimationSemantics.Classify(source.Name);var b=target.Semantic;
        var evidence=new List<string>();
        double name=source.Name.Equals(target.Name,StringComparison.OrdinalIgnoreCase)?1:a.Normalized==b.Normalized?.93:AnimationSemantics.TokenSimilarity(a,b);
        double action=a.Action==b.Action&&a.Action!="unknown"?1:0;
        double phase=a.Phase==b.Phase&&a.Phase!="unspecified"?1:a.Phase=="unspecified"||b.Phase=="unspecified"?.2:0;
        double direction=a.Direction.Length>0&&b.Direction.Length>0?(a.Direction==b.Direction?1:0):a.Direction==b.Direction?.4:.15;
        double context=a.Context==b.Context?(a.Context.Length>0?1:.4):0;
        double duration=source.Duration>0&&target.Duration>0?Math.Exp(-Math.Abs(Math.Log(source.Duration/target.Duration))):0;
        double form=a.Form=="unknown"?.25:a.Form==(targetProfile=="SuperSonic"?"super":"normal")?1:0;
        bool motionCompatible=a.Action==b.Action||a.Action=="unknown"||b.Action=="unknown";
        double pose=motion!=null&&target.Motion!=null&&motionCompatible?MotionDescriptor.PoseSimilarity(motion,target.Motion,name<1||duration<.98):0;
        double root=motion!=null&&target.Motion!=null?MotionDescriptor.RootSimilarity(motion,target.Motion):0;
        double loop=source.Name.Contains("loop",StringComparison.OrdinalIgnoreCase)==target.Loop?1:0;
        double score=.28*name+.22*action+.07*phase+.05*direction+.04*context+.06*duration+.04*form+.17*pose+.05*root+.02*loop;
        evidence.Add($"names={name:F3}; action={a.Action}/{b.Action}; phase={a.Phase}/{b.Phase}; directions={a.Direction}/{b.Direction}");
        evidence.Add($"duration similarity={duration:F3}; source={source.Duration:G6}s target={target.Duration:G6}s");
        evidence.Add("Source looping inferred from original name; actual target flag="+target.Loop+". Export retains source playback.");
        if(motion==null||target.Motion==null)evidence.Add("Motion unavailable; missing evidence earns no similarity.");
        else if(!motionCompatible)evidence.Add("Detailed pose comparison skipped because explicit action families conflict.");
        else evidence.Add($"normalized pose DTW={pose:F3}; root-trajectory similarity={root:F3}; common anchors={motion.Mask.Zip(target.Motion.Mask,(x,y)=>x&&y).Count(v=>v)} (asset motion only)");
        if(a.Action!="unknown"&&b.Action!="unknown"&&a.Action!=b.Action){score=Math.Min(score,.28);evidence.Add("Contradictory action families cap confidence.");}
        if(a.Direction.Length>0&&b.Direction.Length>0&&a.Direction!=b.Direction){score=Math.Min(score,.40);evidence.Add("Directional conflict penalized.");}
        if(a.Phase!="unspecified"&&b.Phase!="unspecified"&&a.Phase!=b.Phase){score=Math.Min(score,.48);evidence.Add("Transition/loop phase conflict penalized.");}
        if(form==0){score=Math.Min(score,.65);evidence.Add("Normal/Super form mismatch; retained only as a best-effort substitute.");}
        if(motion==null||target.Motion==null)score=Math.Min(score,.76);
        return new(source.Name,Math.Clamp(score,0,1),evidence.ToArray());
    }
    public static ScuNamingPlan Match(ModelAsset model,IReadOnlyList<AnimationClip> clips,SourceGame game,bool superSonic,string? overridesPath=null,
        Action<string>? log=null,CancellationToken token=default)
    {
        var inventory=ScuInventory.Default;var profile=inventory.Profile(superSonic);
        var plan=new ScuNamingPlan{InventoryVersion=inventory.InventoryVersion,Profile=profile.Id,SourceModel=model.Name,
            TargetModel=new(profile.SceneStem,profile.RootNodeName,profile.SkeletonNodeName,profile.MeshNodeName,profile.MeshResourceName,profile.NativeSceneFile,profile.SceneRemapText)};
        if(Cache.Value==null)plan.Warnings.Add("Built-in source cache is unavailable or from another inventory/scoring version; automatic runtime scoring used.");
        var targets=profile.Animations.Where(a=>a.Eligible).OrderBy(a=>a.Name,StringComparer.Ordinal).ToArray();
        foreach(var a in profile.Animations.Where(a=>!a.Eligible))plan.Unfilled.Add(new(a.Name,a.Error??a.EligibilityReason));
        var sources=clips.Where(c=>c.Duration>0&&c.Frames.Length>0).OrderBy(c=>c.Name,StringComparer.Ordinal).ToArray();
        var motions=sources.Select(c=>{token.ThrowIfCancellationRequested();return MotionDescriptor.Analyze(model,c,profile);}).ToArray();
        var candidates=new ScuCandidate[targets.Length,sources.Length];
        for(int s=0;s<sources.Length;s++)
        {
            token.ThrowIfCancellationRequested();
            string? fingerprint=motions[s] is{} motion?SourceFingerprint(sources[s],motion):null;
            var cache=Cache.Value?.Entries.FirstOrDefault(c=>c.Game==game.ToString()&&c.Name==sources[s].Name&&c.Profile==profile.Id&&fingerprint!=null&&c.Fingerprint==fingerprint);
            for(int t=0;t<targets.Length;t++)
            {
                if(cache?.Candidates.TryGetValue(targets[t].Name,out var cached)==true){candidates[t,s]=cached with{Cached=true};plan.CachedCandidates++;}
                else candidates[t,s]=Score(sources[s],motions[s],targets[t],profile.Id);
            }
        }
        var assignments=new Dictionary<int,int>();
        if(!string.IsNullOrWhiteSpace(overridesPath))
        {
            using var document=JsonDocument.Parse(File.ReadAllText(overridesPath));
            var data=document.RootElement;
            if(data.TryGetProperty("profile",out var p)&&p.GetString()!=profile.Id)plan.Warnings.Add("Saved corrections use another target profile and were ignored.");
            else if(data.TryGetProperty("overrides",out var overrides))
                foreach(var item in overrides.EnumerateObject())
                {
                    int t=Array.FindIndex(targets,a=>a.Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase));
                    int s=Array.FindIndex(sources,a=>a.Name.Equals(item.Value.GetString(),StringComparison.OrdinalIgnoreCase));
                    if(t>=0&&s>=0)assignments[t]=s;
                    else plan.Warnings.Add("Stale/unknown correction ignored: "+item.Name+" <- "+item.Value.GetString());
                }
        }
        var manual=assignments.Keys.ToHashSet();var used=assignments.Values.ToHashSet();
        var freeTargets=Enumerable.Range(0,targets.Length).Where(t=>!assignments.ContainsKey(t)).ToArray();
        var freeSources=Enumerable.Range(0,sources.Length).Where(s=>!used.Contains(s)).ToArray();
        bool transpose=freeSources.Length<freeTargets.Length;
        int rows=Math.Min(freeSources.Length,freeTargets.Length),columns=Math.Max(freeSources.Length,freeTargets.Length);
        var weights=new double[rows,columns];
        for(int i=0;i<rows;i++)for(int j=0;j<columns;j++)weights[i,j]=transpose?candidates[freeTargets[j],freeSources[i]].Score:candidates[freeTargets[i],freeSources[j]].Score;
        var optimum=MaximumAssignment(weights,token);
        for(int i=0;i<optimum.Length;i++)assignments[transpose?freeTargets[optimum[i]]:freeTargets[i]]=transpose?freeSources[i]:freeSources[optimum[i]];
        for(int t=0;t<targets.Length;t++)
        {
            token.ThrowIfCancellationRequested();
            if(sources.Length==0){plan.Unfilled.Add(new(targets[t].Name,"No usable source animation selected."));continue;}
            bool primary=assignments.TryGetValue(t,out int sourceIndex);
            if(!primary)sourceIndex=Enumerable.Range(0,sources.Length).OrderByDescending(s=>candidates[t,s].Score).ThenBy(s=>sources[s].Name,StringComparer.Ordinal).First();
            var candidate=candidates[t,sourceIndex];bool reused=!primary||manual.Contains(t)&&manual.Where(k=>assignments[k]==sourceIndex).Min()!=t;
            double score=Math.Max(0,candidate.Score-(reused?.04:0));
            string level=score>=.78?"High":score>=.50?"Medium":"Low";
            var evidence=candidate.Evidence.Concat(new[]{manual.Contains(t)?"Explicit valid user correction.":primary?"Global maximum-weight unique assignment.":"Remaining target filled by intentional best-source reuse.",candidate.Cached?"Fingerprint-matched built-in source profile.":"Runtime deterministic multi-factor scoring."}).ToArray();
            plan.Matches.Add(new(sources[sourceIndex].Name,targets[t].Name,targets[t].PlayerKey,targets[t].PlayerKey,score,level,
                manual.Contains(t)?"manual_override":"inferred",evidence,reused,
                Enumerable.Range(0,sources.Length).OrderByDescending(s=>candidates[t,s].Score).ThenBy(s=>sources[s].Name,StringComparer.Ordinal).Take(3).Select(s=>candidates[t,s]).ToArray()));
        }
        plan.UnusedSources=sources.Select(c=>c.Name).Except(plan.Matches.Select(m=>m.SourceName),StringComparer.Ordinal).ToArray();
        log?.Invoke($"SCU {profile.Id}: {plan.Matches.Count} automatic names ({plan.Matches.Count(m=>m.Confidence=="High")} high, {plan.Matches.Count(m=>m.Confidence=="Medium")} medium, {plan.Matches.Count(m=>m.Confidence=="Low")} low), {plan.Matches.Count(m=>m.Reused)} reused, {plan.Unfilled.Count} unfilled; details in scu-animation-mapping.txt.");
        return plan;
    }
    /// <summary>Rectangular Hungarian assignment; rows <= columns, stable iteration resolves ties.</summary>
    public static int[] MaximumAssignment(double[,] weights,CancellationToken token=default)
    {
        int n=weights.GetLength(0),m=weights.GetLength(1);if(n>m)throw new ArgumentException("Assignment rows must not exceed columns.");
        var u=new double[n+1];var v=new double[m+1];var p=new int[m+1];var way=new int[m+1];
        for(int i=1;i<=n;i++)
        {
            token.ThrowIfCancellationRequested();p[0]=i;int j0=0;var min=Enumerable.Repeat(double.PositiveInfinity,m+1).ToArray();var used=new bool[m+1];
            do
            {
                used[j0]=true;int i0=p[j0],j1=0;double delta=double.PositiveInfinity;
                for(int j=1;j<=m;j++)if(!used[j]){double current=-weights[i0-1,j-1]-u[i0]-v[j];if(current<min[j]){min[j]=current;way[j]=j0;}if(min[j]<delta){delta=min[j];j1=j;}}
                for(int j=0;j<=m;j++)if(used[j]){u[p[j]]+=delta;v[j]-=delta;}else min[j]-=delta;
                j0=j1;
            }while(p[j0]!=0);
            do{int j1=way[j0];p[j0]=p[j1];j0=j1;}while(j0!=0);
        }
        var result=new int[n];for(int j=1;j<=m;j++)if(p[j]>0)result[p[j]-1]=j-1;return result;
    }
}
