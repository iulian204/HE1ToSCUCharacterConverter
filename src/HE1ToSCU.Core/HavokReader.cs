using System.Numerics;
using HE1ToSCU.Core.ThirdParty;

namespace HE1ToSCU.Core;

public sealed class HavokReader
{
    public HavokAsset Read(string path,CancellationToken token=default,SourceGame game=SourceGame.SonicGenerations)
    {
        byte[] bytes=File.ReadAllBytes(path);token.ThrowIfCancellationRequested();
        if(System.Text.Encoding.UTF8.GetString(bytes.AsSpan(0,Math.Min(100,bytes.Length))).TrimStart('\uFEFF',' ','\r','\n','\t').StartsWith('<'))
            return HavokXmlReader.Read(path,token);
        var pack=new HavokPackfile(bytes,game);var b=pack.Data;var bones=new List<Bone>();var clips=new List<AnimationClip>();
        bool legacy=pack.Legacy; int baseSize=legacy?36:40;
        var skeletons=pack.Objects.Where(o=>o.Value=="hkaSkeleton").ToArray();
        if(skeletons.Length>1)throw new NotSupportedException("HKX contains multiple skeletons. Supply a file containing one skeleton.");
        foreach(var (s,_) in skeletons)
        {
            var parents=pack.Array(s+(legacy?4:12),2,4096);var names=pack.Array(s+(legacy?12:24),legacy?4:8,4096);var rest=pack.Array(s+(legacy?20:36),48,4096);
            if(parents.Count!=names.Count||parents.Count!=rest.Count)throw new InvalidDataException("Havok skeleton array lengths disagree.");
            for(int i=0;i<parents.Count;i++)
                bones.Add(new Bone(b.String(pack.Pointer(legacy?pack.Pointer(names.Address+i*4):names.Address+i*8)),b.I16(parents.Address+i*2),ReadPose(b,rest.Address+i*48).Matrix));
            Spatial.ValidateSkeleton(bones,false);
        }
        foreach(var (a,type) in pack.Objects.Where(o=>o.Value.EndsWith("Animation",StringComparison.Ordinal)&&o.Value.StartsWith("hka",StringComparison.Ordinal)))
        {
            token.ThrowIfCancellationRequested();
            if(type is not ("hkaSplineCompressedAnimation" or "hkaInterleavedUncompressedAnimation" or "hkaSplineSkeletalAnimation" or "hkaInterleavedSkeletalAnimation"))
                throw new NotSupportedException($"Animation codec {type} is not supported.");
            float duration=b.F32(a+12);int tracks=b.Count(a+16,4096);
            if(tracks==0||duration<=0)throw new InvalidDataException("Animation has zero tracks or nonpositive duration.");
            if(b.I32(a+20)!=0)throw new NotSupportedException("Animation has float tracks; these need material/morph channel mapping.");
            RootMotion? rootMotion = ReadRootMotion(pack, pack.Pointer(a+24,true));
            var bindings=pack.Objects.Where(o=>o.Value=="hkaAnimationBinding"&&pack.Pointer(o.Key+(legacy?0:12),true)==a).ToArray();
            if(bindings.Length!=1)throw new InvalidDataException("Animation must have exactly one matching hkaAnimationBinding.");
            int binding=bindings[0].Key;
            int blend=b.U8(binding+(legacy?20:40));
            if(blend>2)throw new NotSupportedException($"Unsupported animation blend hint {blend}.");
            var mapping=pack.Array(binding+(legacy?4:16),2,4096);
            int[] map=mapping.Count==0?Enumerable.Range(0,tracks).ToArray():Enumerable.Range(0,mapping.Count).Select(i=>(int)b.I16(mapping.Address+i*2)).ToArray();
            if(map.Length!=tracks||map.Any(i=>i<0))throw new InvalidDataException("Invalid transform-track to bone mapping.");
            Pose[][] frames;
            Func<float, Pose[]>? sampler = null;
            float[]? boundaries = null;
            if(type is "hkaInterleavedUncompressedAnimation" or "hkaInterleavedSkeletalAnimation")
            {
                var transforms=pack.Array(a+baseSize,48);
                if(transforms.Count%tracks!=0||transforms.Count==0)throw new InvalidDataException("Interleaved transform count is not a multiple of track count.");
                frames=new Pose[transforms.Count/tracks][];
                for(int f=0;f<frames.Length;f++)
                {
                    token.ThrowIfCancellationRequested();frames[f]=new Pose[tracks];
                    for(int t=0;t<tracks;t++)frames[f][t]=ReadPose(b,transforms.Address+(f*tracks+t)*48);
                }
            }
            else
            {
                int count=b.Count(a+baseSize,1_000_000),blocks=b.Count(a+baseSize+4,100_000),perBlock=b.Count(a+baseSize+8,256);
                if(count<1||blocks<1||perBlock<2||checked((long)count*tracks)>20_000_000)throw new InvalidDataException("Invalid or oversized spline animation.");
                var offsets=pack.Array(a+baseSize+28,4,100_000);var data=pack.Array(a+baseSize+76,1,512_000_000);
                if(offsets.Count!=blocks)throw new InvalidDataException("Spline block offset count mismatch.");
                int[] blockOffsets=Enumerable.Range(0,blocks).Select(i=>b.Count(offsets.Address+i*4,data.Count)).ToArray();
                // These HE1 spline byte streams are little-endian even in the supplied
                // big-endian Unleashed packfiles. Container/fixup data still uses its header endian.
                var decoded=SplineCompressedAnimation.ReadSplineCompressedAnimByteBlock(false,bytes.AsSpan(data.Address,data.Count).ToArray(),tracks,blocks,blockOffsets);
                float blockDuration=b.F32(a+baseSize+16), inverseBlockDuration=b.F32(a+baseSize+20), frameDuration=b.F32(a+baseSize+24);
                if(!float.IsFinite(blockDuration)||!float.IsFinite(inverseBlockDuration)||!float.IsFinite(frameDuration)||blockDuration<=0||inverseBlockDuration<=0||frameDuration<=0)
                    throw new InvalidDataException("Invalid spline timing fields.");
                sampler = time => {
                    time=Math.Clamp(time,0,duration);
                    int block=Math.Min((int)(time*inverseBlockDuration),blocks-1);
                    float local=Math.Max(0,(time-block*blockDuration)/frameDuration);
                    return decoded[block].Select(t=>Sample(t,local,perBlock)).ToArray();
                };
                boundaries=Enumerable.Range(1,blocks-1).Select(block=> {
                    float time=block/inverseBlockDuration;
                    while((int)(time*inverseBlockDuration)<block) time=MathF.BitIncrement(time);
                    while((int)(MathF.BitDecrement(time)*inverseBlockDuration)>=block) time=MathF.BitDecrement(time);
                    return time;
                }).Where(time=>time>0&&time<duration).ToArray();
                frames=new Pose[count][];
                for(int f=0;f<count;f++)
                {
                    token.ThrowIfCancellationRequested();int block=Math.Min(f/(perBlock-1),blocks-1),local=f-block*(perBlock-1);
                    if(local>=perBlock)throw new InvalidDataException("Spline frame exceeds block count.");
                    frames[f]=new Pose[tracks];
                    for(int t=0;t<tracks;t++)frames[f][t]=Sample(decoded[block][t],local,perBlock);
                }
            }
            clips.Add(new AnimationClip(ExportNames.AnimationStem(path),duration,map,frames,rootMotion,SourceSampler:sampler,Discontinuities:boundaries,Blend:(AnimationBlend)blend));
        }
        if(bones.Count==0&&clips.Count==0)throw new InvalidDataException("HKX contains no supported skeleton or animation.");
        return new HavokAsset(bones,clips);
    }
    private static RootMotion? ReadRootMotion(HavokPackfile pack, int address)
    {
        if (address == 0) return null;
        if (pack.Objects.GetValueOrDefault(address) != "hkaDefaultAnimatedReferenceFrame")
            throw new NotSupportedException($"Unsupported extracted reference-frame type: {pack.Objects.GetValueOrDefault(address, "unknown")}.");
        var b = pack.Data;
        var samples = pack.Array(address + 52, 16, 1_000_000);
        return new RootMotion(new(b.F32(address + 16), b.F32(address + 20), b.F32(address + 24)),
            new(b.F32(address + 32), b.F32(address + 36), b.F32(address + 40)), b.F32(address + 48),
            Enumerable.Range(0, samples.Count).Select(i => { int p = samples.Address + i * 16; return new Vector4(b.F32(p), b.F32(p + 4), b.F32(p + 8), b.F32(p + 12)); }).ToArray());
    }
    private static Pose ReadPose(BinaryData b,int p)=>ValidatePose(new Pose(new(b.F32(p),b.F32(p+4),b.F32(p+8)),new(b.F32(p+16),b.F32(p+20),b.F32(p+24),b.F32(p+28)),new(b.F32(p+32),b.F32(p+36),b.F32(p+40))));
    internal static Pose ValidatePose(Pose p)
    {
        float length=p.Rotation.LengthSquared();
        if(!float.IsFinite(length)||length<0.1f||length>2f||!float.IsFinite(p.Translation.LengthSquared())||!float.IsFinite(p.Scale.LengthSquared()))
            throw new InvalidDataException("Invalid Havok transform/quaternion.");
        return p with {Rotation=Quaternion.Normalize(p.Rotation)};
    }
    private static Pose Sample(SplineCompressedAnimation.TransformTrack t,float f,int count)
    {
        Vector3 pos=t.HasSplinePosition?new(t.SplinePosition.GetValueX(f,count)??0,t.SplinePosition.GetValueY(f,count)??0,t.SplinePosition.GetValueZ(f,count)??0):t.StaticPosition;
        Vector3 scale=t.HasSplineScale?new(t.SplineScale.GetValueX(f,count)??1,t.SplineScale.GetValueY(f,count)??1,t.SplineScale.GetValueZ(f,count)??1):t.StaticScale;
        Quaternion q=t.HasSplineRotation?t.SplineRotation.GetValue(f,count):t.StaticRotation;
        return ValidatePose(new Pose(pos,q,scale));
    }
}
