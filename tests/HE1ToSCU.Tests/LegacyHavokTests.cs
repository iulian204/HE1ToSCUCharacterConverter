using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class LegacyHavokTests
{
    [Theory]
    [InlineData(true,true)] [InlineData(false,false)]
    public void VersionAwareSkeletonResolvesPointerBonesAndInlineBones(bool legacy,bool bigEndian)
    {
        var f=new Fixture(legacy,bigEndian);int root=f.Allocate(legacy?36:84);
        int parents=f.Allocate(2),entries=f.Allocate(legacy?4:8),bone=legacy?f.Allocate(8):entries,rest=f.Allocate(48),name=f.String("root");
        f.Short(parents,-1);f.Pointer(root+(legacy?4:12),parents);f.UInt(root+(legacy?8:16),1);
        f.Pointer(root+(legacy?12:24),entries);f.UInt(root+(legacy?16:28),1);
        if(legacy)f.Pointer(entries,bone);f.Pointer(bone,name);
        f.Pointer(root+(legacy?20:36),rest);f.UInt(root+(legacy?24:40),1);
        f.Float(rest,1);f.Float(rest+4,2);f.Float(rest+8,3);f.Float(rest+28,1);f.Float(rest+32,1);f.Float(rest+36,1);f.Float(rest+40,1);
        f.Object(root,"hkaSkeleton");
        WithFile(f.Build(),path=>{
            var bone=Assert.Single(new HavokReader().Read(path,game:legacy?SourceGame.SonicUnleashed:SourceGame.SonicGenerations).Bones);
            Assert.Equal("root",bone.Name);Assert.Equal(-1,bone.Parent);Assert.Equal(new Vector3(1,2,3),bone.Rest.Translation);
            Assert.Throws<NotSupportedException>(()=>new HavokReader().Read(path,game:legacy?SourceGame.SonicGenerations:SourceGame.SonicUnleashed));
        });
    }

    [Theory]
    [InlineData(true,true)] [InlineData(false,false)]
    public void BigEndianLegacyContainerKeepsLittleEndianSplinePayload(bool legacy,bool bigEndian)
    {
        var f=new Fixture(legacy,bigEndian);int size=legacy?36:40,animation=f.Allocate(size+88),binding=f.Allocate(legacy?24:44),map=f.Allocate(2),offsets=f.Allocate(4),data=f.Allocate(32);
        f.Float(animation+12,1);f.UInt(animation+16,1);f.UInt(animation+size,2);f.UInt(animation+size+4,1);f.UInt(animation+size+8,256);
        f.Float(animation+size+16,255);f.Float(animation+size+20,1f/255);f.Float(animation+size+24,1);
        f.Pointer(animation+size+28,offsets);f.UInt(animation+size+32,1);f.Pointer(animation+size+76,data);f.UInt(animation+size+80,32);
        f.Pointer(binding+(legacy?0:12),animation);f.Pointer(binding+(legacy?4:16),map);f.UInt(binding+(legacy?8:20),1);
        f.Bytes(data,[0x14,7,15,0]);
        f.Float(data+4,1,false);f.Float(data+8,2,false);f.Float(data+12,3,false);f.Float(data+28,1,false);
        f.Object(animation,legacy?"hkaSplineSkeletalAnimation":"hkaSplineCompressedAnimation");f.Object(binding,"hkaAnimationBinding");
        WithFile(f.Build(),path=>{
            var clip=Assert.Single(new HavokReader().Read(path,game:legacy?SourceGame.SonicUnleashed:SourceGame.SonicGenerations).Animations);
            Assert.Equal(2,clip.Frames.Length);Assert.Equal(new Vector3(1,2,3),clip.Frames[0][0].Translation);
            Assert.Equal(Quaternion.Identity,AnimationSampling.Sample(clip,.5f)[0].Rotation);
        });
    }

    [Fact]
    public void ZeroAndNegativeScaleRetainRotationAndDoNotCollapseChildPositions()
    {
        var model=new ModelAsset("rig",[new("root",-1,Matrix4x4.Identity),new("child",0,Matrix4x4.CreateTranslation(1,0,0))],[]);
        var q=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.7f);
        Pose[] Sample(float t)=>[new(Vector3.Zero,q,new(1-2*t,1,1)),new(new(1,0,0),Quaternion.Identity,Vector3.One)];
        var prepared=AnimationRig.Prepare(model,[new("signed",1,[0,1],[Sample(0),Sample(1)],SourceSampler:Sample)],CancellationToken.None);
        foreach(float time in new[]{0f,.25f,.5f,.75f,1f}) {
            var frame=AnimationSampling.Sample(prepared.Clips[0],time);
            Matrix4x4 Global(int i)=>prepared.Model.Bones[i].Parent<0?frame[i].Matrix:frame[i].Matrix*Global(prepared.Model.Bones[i].Parent);
            Assert.True(Vector3.Distance(Global(1).Translation,Vector3.Transform(Vector3.UnitX,q))<.0005f);
            for(int i=0;i<frame.Length;i++) Assert.True(Spatial.Near(Pose.Relative(frame[i],prepared.Model.Bones[i].Rest).Matrix*prepared.Model.Bones[i].Rest,frame[i].Matrix,.002f));
        }
    }

    private static void WithFile(byte[] bytes,Action<string> action)
    {
        string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".hkx");
        try {File.WriteAllBytes(path,bytes);action(path);} finally {File.Delete(path);}
    }
    private sealed class Fixture(bool legacy,bool bigEndian)
    {
        private readonly List<byte> data=[];
        private readonly List<(int Field,int Target)> pointers=[];
        private readonly List<(int Address,int Class)> objects=[];
        public int Allocate(int size) {while(data.Count%16!=0)data.Add(0);int p=data.Count;data.AddRange(new byte[size]);return p;}
        public int String(string text) {int p=data.Count;data.AddRange(Encoding.UTF8.GetBytes(text));data.Add(0);return p;}
        public void Bytes(int p,byte[] bytes) {for(int i=0;i<bytes.Length;i++)data[p+i]=bytes[i];}
        public void UInt(int p,uint value,bool? endian=null) {byte[] bytes=new byte[4];if(endian??bigEndian)BinaryPrimitives.WriteUInt32BigEndian(bytes,value);else BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);Bytes(p,bytes);}
        public void Float(int p,float value,bool? endian=null)=>UInt(p,BitConverter.SingleToUInt32Bits(value),endian);
        public void Short(int p,short value) {byte[] bytes=new byte[2];if(bigEndian)BinaryPrimitives.WriteInt16BigEndian(bytes,value);else BinaryPrimitives.WriteInt16LittleEndian(bytes,value);Bytes(p,bytes);}
        public void Pointer(int p,int target)=>pointers.Add((p,target));
        public void Object(int p,string name)=>objects.Add((p,String(name)));
        public byte[] Build() {
            while(data.Count%16!=0)data.Add(0);int local=data.Count;
            foreach(var pair in pointers) {int p=data.Count;data.AddRange(new byte[8]);UInt(p,(uint)pair.Field);UInt(p+4,(uint)pair.Target);}
            int global=data.Count;
            foreach(var obj in objects) {int p=data.Count;data.AddRange(new byte[12]);UInt(p,(uint)obj.Address);UInt(p+8,(uint)obj.Class);}
            int end=data.Count;var body=data.ToArray();data.Clear();data.AddRange(new byte[112]);data.AddRange(body);
            Bytes(0,[0x57,0xe0,0xe0,0x57,0x10,0xc0,0xc0,0x10]);UInt(12,legacy?5u:8u);data[16]=4;data[17]=bigEndian?(byte)0:(byte)1;UInt(20,1);
            Bytes(40,Encoding.ASCII.GetBytes(legacy?"Havok-5.5.0-r1":"hk_2010.2.0-r1"));
            UInt(84,112);UInt(88,(uint)local);UInt(92,(uint)global);UInt(96,(uint)global);UInt(100,(uint)end);UInt(104,(uint)end);UInt(108,(uint)end);
            return data.ToArray();
        }
    }
}
