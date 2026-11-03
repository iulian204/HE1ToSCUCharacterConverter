using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace HE1ToSCU.Core;

internal static partial class HavokXmlReader
{
    private static XElement Param(XElement e,string name)=>e.Elements("hkparam").SingleOrDefault(p=>(string?)p.Attribute("name")==name)??throw new InvalidDataException($"Missing Havok XML field {name}.");
    private static float[] Numbers(XElement e)=>NumberPattern().Matches(e.Value).Select(m=>float.Parse(m.Value,CultureInfo.InvariantCulture)).ToArray();
    [GeneratedRegex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex NumberPattern();
    public static HavokAsset Read(string path,CancellationToken token)
    {
        using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=256_000_000});
        var document=XDocument.Load(reader);
        if(document.Root?.Name!="hkpackfile")throw new NotSupportedException("Only XML hkpackfile is supported; XML tagfiles are not supported.");
        var objects=document.Descendants("hkobject").Where(o=>o.Attribute("class")!=null).ToArray();
        var skeletons=objects.Where(o=>(string?)o.Attribute("class")=="hkaSkeleton").ToArray();
        if(skeletons.Length>1)throw new InvalidDataException("XML HKX has multiple skeletons.");
        var bones=new List<Bone>();var clips=new List<AnimationClip>();
        Pose PoseAt(float[] v,int i)=>HavokReader.ValidatePose(new Pose(new(v[i],v[i+1],v[i+2]),new(v[i+4],v[i+5],v[i+6],v[i+7]),new(v[i+8],v[i+9],v[i+10])));
        foreach(var s in skeletons)
        {
            var parents=Numbers(Param(s,"parentIndices"));
            var entries=Param(s,"bones").Elements("hkobject").ToArray();var rest=Numbers(Param(s,"referencePose"));
            if(entries.Length!=parents.Length||rest.Length!=entries.Length*12)throw new InvalidDataException("XML skeleton array lengths disagree.");
            for(int i=0;i<entries.Length;i++)bones.Add(new Bone(Param(entries[i],"name").Value,checked((int)parents[i]),PoseAt(rest,i*12).Matrix));
            Spatial.ValidateSkeleton(bones);
        }
        foreach(var a in objects.Where(o=>((string?)o.Attribute("class"))?.EndsWith("Animation",StringComparison.Ordinal)==true))
        {
            token.ThrowIfCancellationRequested();
            if((string?)a.Attribute("class")!="hkaInterleavedUncompressedAnimation")throw new NotSupportedException("XML animation input currently requires hkaInterleavedUncompressedAnimation. Binary Generations spline animations can be read directly.");
            int tracks=int.Parse(Param(a,"numberOfTransformTracks").Value,CultureInfo.InvariantCulture);
            float duration=float.Parse(Param(a,"duration").Value,CultureInfo.InvariantCulture);
            if(tracks<=0||tracks>4096||!float.IsFinite(duration)||duration<=0)throw new InvalidDataException("Invalid XML animation header.");
            if(Param(a,"numberOfFloatTracks").Value.Trim()!="0")throw new NotSupportedException("XML animation float tracks are not supported.");
            RootMotion? rootMotion = null;
            string motionReference = Param(a,"extractedMotion").Value.Trim();
            if (motionReference != "null")
            {
                var motion = objects.SingleOrDefault(o => (string?)o.Attribute("name") == motionReference)
                    ?? throw new InvalidDataException("Missing XML extracted reference-frame object.");
                if ((string?)motion.Attribute("class") != "hkaDefaultAnimatedReferenceFrame")
                    throw new NotSupportedException("Only hkaDefaultAnimatedReferenceFrame root motion is supported.");
                var up = Numbers(Param(motion,"up")); var forward = Numbers(Param(motion,"forward"));
                var samples = Numbers(Param(motion,"referenceFrameSamples"));
                if (up.Length != 4 || forward.Length != 4 || samples.Length % 4 != 0)
                    throw new InvalidDataException("Invalid XML reference-frame vector array.");
                rootMotion = new RootMotion(new(up[0],up[1],up[2]), new(forward[0],forward[1],forward[2]),
                    float.Parse(Param(motion,"duration").Value,CultureInfo.InvariantCulture),
                    Enumerable.Range(0,samples.Length/4).Select(i=>new Vector4(samples[i*4],samples[i*4+1],samples[i*4+2],samples[i*4+3])).ToArray());
            }
            var binding=objects.SingleOrDefault(o=>(string?)o.Attribute("class")=="hkaAnimationBinding"&&Param(o,"animation").Value.Trim()==(string?)a.Attribute("name"))??throw new InvalidDataException("Missing XML animation binding.");
            if(Param(binding,"blendHint").Value.Trim() is not ("NORMAL" or "0"))throw new NotSupportedException("Additive XML animation is not supported.");
            int[] map=Numbers(Param(binding,"transformTrackToBoneIndices")).Select(v=>checked((int)v)).ToArray();
            if(map.Length==0)map=Enumerable.Range(0,tracks).ToArray();
            if(map.Length!=tracks||map.Any(i=>i<0))throw new InvalidDataException("XML animation track mapping mismatch.");
            var values=Numbers(Param(a,"transforms"));
            if(values.Length==0||values.Length%(tracks*12)!=0)throw new InvalidDataException("XML interleaved transforms are incomplete.");
            var frames=new Pose[values.Length/(tracks*12)][];
            for(int f=0;f<frames.Length;f++)
            {
                token.ThrowIfCancellationRequested();frames[f]=new Pose[tracks];
                for(int t=0;t<tracks;t++)frames[f][t]=PoseAt(values,(f*tracks+t)*12);
            }
            clips.Add(new AnimationClip(ExportNames.AnimationStem(path),duration,map,frames,rootMotion));
        }
        if(bones.Count==0&&clips.Count==0)throw new InvalidDataException("No skeleton or animation in XML HKX.");
        return new HavokAsset(bones,clips);
    }
}
