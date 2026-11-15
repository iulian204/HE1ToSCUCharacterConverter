using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace HE1ToSCU.Core;

public sealed class MotionDescriptor
{
    public bool[] Mask {get;set;}=[];
    public float[][] Samples {get;set;}=[];
    public float[][] RootSamples {get;set;}=[];
    public void Validate()
    {
        if(Mask.Length!=6||Samples.Length!=17||RootSamples.Length!=17||Samples.Any(s=>s.Length!=18||s.Any(v=>!float.IsFinite(v)))||RootSamples.Any(s=>s.Length!=3||s.Any(v=>!float.IsFinite(v))))
            throw new InvalidDataException("Invalid normalized SCU motion descriptor.");
    }
    public string Fingerprint()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",",Mask.Select(v=>v?"1":"0"))+"|"+
        string.Join(",",Samples.Concat(RootSamples).SelectMany(r=>r).Select(v=>Math.Round(v,4).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)))))).ToLowerInvariant();
    public static MotionDescriptor? Analyze(ModelAsset model,AnimationClip clip,ScuProfile profile)
    {
        string Normalize(string name)=>new(name.Replace("@LT","",StringComparison.Ordinal).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        string[] groups=["hips","head","handL","handR","footL","footR"];
        var indices=groups.Select(g=>model.Bones.FindIndex(b=>profile.BoneAliases[g].Any(a=>Normalize(a)==Normalize(b.Name)))).ToArray();
        if(indices[0]<0)return null;
        var restClip=new AnimationClip("rest",1,[],[[]]);
        var rest=PoseValidation.SourceGlobals(model,restClip,0);var hip=rest[indices[0]].Translation;
        float scale=indices[1]>=0&&indices[4]>=0&&indices[5]>=0?Vector3.Distance(rest[indices[1]].Translation,(rest[indices[4]].Translation+rest[indices[5]].Translation)*.5f):0;
        if(scale<1e-6)return null;
        var result=new MotionDescriptor{Mask=indices.Select(i=>i>=0).ToArray(),Samples=new float[17][],RootSamples=new float[17][]};
        for(int frame=0;frame<17;frame++)
        {
            float time=clip.Duration*frame/16;var poses=PoseValidation.SourceGlobals(model,clip,time);var hips=poses[indices[0]].Translation;
            result.Samples[frame]=indices.SelectMany((index,g)=>{
                var v=index<0?Vector3.Zero:(poses[index].Translation-(g==0?hip:hips))/scale;return new[]{v.X,v.Y,v.Z};}).ToArray();
            var root=poses[0].Translation;
            if(clip.RootMotion!=null)root=Vector3.Transform(root,clip.RootMotion.Sample(time).Matrix);
            result.RootSamples[frame]=[root.X/scale,root.Y/scale,root.Z/scale];
        }
        result.Validate();return result;
    }
    public static double PoseSimilarity(MotionDescriptor a,MotionDescriptor b,bool allowWarp=true)
    {
        var channels=Enumerable.Range(0,6).Where(i=>a.Mask[i]&&b.Mask[i]).SelectMany(i=>Enumerable.Range(i*3,3)).ToArray();
        if(channels.Length<6)return 0;
        if(!allowWarp)return Math.Exp(-4*Math.Sqrt(Enumerable.Range(0,17).Sum(i=>channels.Sum(c=>Math.Pow(a.Samples[i][c]-b.Samples[i][c],2)))/(17*channels.Length)));
        var dp=new double[18,18];for(int i=0;i<18;i++)for(int j=0;j<18;j++)dp[i,j]=double.PositiveInfinity;dp[0,0]=0;
        for(int i=1;i<=17;i++)for(int j=Math.Max(1,i-3);j<=Math.Min(17,i+3);j++)
        {
            double error=channels.Sum(c=>Math.Pow(a.Samples[i-1][c]-b.Samples[j-1][c],2))/channels.Length;
            dp[i,j]=error+Math.Min(dp[i-1,j-1],Math.Min(dp[i-1,j],dp[i,j-1]));
        }
        return Math.Exp(-4*Math.Sqrt(dp[17,17]/17));
    }
    public static double RootSimilarity(MotionDescriptor a,MotionDescriptor b)
    {
        static double[] Features(MotionDescriptor m)
        {
            var points=m.RootSamples.Select(p=>new Vector3(p[0],p[1],p[2])).ToArray();
            double distance=Enumerable.Range(1,16).Sum(i=>Vector3.Distance(points[i],points[i-1]));
            var travel=points[^1]-points[0];return [distance,travel.X,travel.Y,travel.Z,points.Max(p=>p.Y)-points.Min(p=>p.Y)];
        }
        var x=Features(a);var y=Features(b);return Math.Exp(-x.Zip(y,(p,q)=>Math.Abs(p-q)/(1+Math.Abs(p)+Math.Abs(q))).Average()*4);
    }
}
