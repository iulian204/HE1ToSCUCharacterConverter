using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using HE1ToSCU.Core;
using HE1ToSCU.Core.ThirdParty;

namespace HE1ToSCU.Tests;

public sealed class ConversionTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"HE1SCU-tests-"+Guid.NewGuid().ToString("N"));
    public ConversionTests()=>Directory.CreateDirectory(directory);
    public void Dispose()=>Directory.Delete(directory,true);
    private string Fixture(string name,string text){string p=Path.Combine(directory,name);File.WriteAllText(p,text);return p;}

    [Fact]
    public void StripParitySurvivesDegeneratesAndRestarts()
    {
        Assert.Equal(new[]{0,1,2,2,3,1,4,5,6},He1ModelReader.DecodeStrip([0,1,2,2,1,3,65535,4,5,6],7));
        Assert.Throws<InvalidDataException>(()=>He1ModelReader.DecodeStrip([0,1,9],3));
    }
    [Fact]
    public void RestRelativeQuaternionPoseReconstructsAbsoluteLocalTransform()
    {
        var rest=Matrix4x4.CreateFromYawPitchRoll(.7f,-.2f,.3f)*Matrix4x4.CreateTranslation(1,2,3);
        var local=new Pose(new(4,5,6),Quaternion.CreateFromYawPitchRoll(-.4f,.8f,.2f),new(1.1f));
        var delta=Pose.FromMatrix(Spatial.Convert(local.Matrix)*Spatial.Inverse(Spatial.Convert(rest)));
        Assert.True(Spatial.Near(delta.Matrix*Spatial.Convert(rest),Spatial.Convert(local.Matrix),.0001f));
        Assert.Equal(20,Spatial.Convert(rest).M42);
    }
    [Fact]
    public void CyclesDuplicateNamesAndSingularMatricesAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>Spatial.ValidateSkeleton([new("a",1,Matrix4x4.Identity),new("b",0,Matrix4x4.Identity)]));
        Assert.Throws<InvalidDataException>(()=>Spatial.ValidateSkeleton([new("a",-1,Matrix4x4.Identity),new("a",0,Matrix4x4.Identity)]));
        Assert.Throws<InvalidDataException>(()=>Spatial.Inverse(new Matrix4x4()));
    }
    [Theory]
    [InlineData(2)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void BinaryModelReadsBindMatricesPalettesAndVertexWeights(int version)
    {
        string path=Path.Combine(directory,"fixture.model");File.WriteAllBytes(path,ModelFixture(version));
        var model=new He1ModelReader().Read(path);
        Assert.Equal(2,model.Bones.Count);Assert.Equal("child",model.Bones[1].Name);
        Assert.True(Spatial.Near(model.Bones[1].Rest,Matrix4x4.CreateTranslation(0,2,0),.0001f));
        var surface=Assert.Single(model.Surfaces);Assert.Equal(3,surface.Vertices.Count);
        Assert.Equal(new[]{0,1,2},surface.Indices);Assert.Equal(1,surface.Vertices[1].Bones[0]);
        Assert.Equal(new Vector4(1,0,0,0),surface.Vertices[1].Weights);
        var corrupt=ModelFixture(version);BinaryPrimitives.WriteUInt32BigEndian(corrupt.AsSpan(28),uint.MaxValue);
        File.WriteAllBytes(path,corrupt);Assert.ThrowsAny<Exception>(()=>new He1ModelReader().Read(path));
    }
    [Theory]
    [InlineData(1, -1)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    public void MissingTangentsUseUvDirectionAndPreserveAuthoredVertices(int uvV, int handedness)
    {
        string path = Path.Combine(directory, "missing-tangent.model");
        File.WriteAllBytes(path, ModelFixture(5, 0x7FFFFFFF, uvV));

        var vertices = Assert.Single(new He1ModelReader().Read(path).Surfaces).Vertices;
        Vector3 direction = uvV == 0 ? Vector3.UnitX : Vector3.UnitY;
        Assert.Equal(new Vector4(direction, handedness), vertices[0].Tangent);
        Assert.Equal(new Vector4(direction, handedness), vertices[1].Tangent);
        Assert.Equal(new Vector4(Vector3.UnitX, 1), vertices[2].Tangent);
    }

    [Fact]
    public void OtherNonFiniteVertexFloatsAreRejected()
    {
        string path = Path.Combine(directory, "invalid-vertex.model");
        File.WriteAllBytes(path, ModelFixture(5, 0x7FC00000));
        Assert.Contains("Non-finite float", Assert.Throws<InvalidDataException>(() => new He1ModelReader().Read(path)).Message);

        var bytes = ModelFixture(5);
        int Offset(int p) => 24 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(p));
        int group = Offset(Offset(28));
        int mesh = Offset(Offset(group + 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(Offset(mesh + 20)), 0x7FFFFFFF);
        File.WriteAllBytes(path, bytes);
        Assert.Contains("Non-finite float", Assert.Throws<InvalidDataException>(() => new He1ModelReader().Read(path)).Message);
    }

    [Fact]
    public void NonFiniteHalfUvIsRejectedBeforeExport()
    {
        var bytes = ModelFixture(5);
        int Offset(int p) => 24 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(p));
        int group = Offset(Offset(28));
        int mesh = Offset(Offset(group + 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(Offset(mesh + 24) + 76), 0x2C235F);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(Offset(mesh + 20) + 56), 0x7C00);
        string path = Path.Combine(directory, "invalid-half-uv.model");
        File.WriteAllBytes(path, bytes);
        Assert.Contains("Non-finite half float", Assert.Throws<InvalidDataException>(() => new He1ModelReader().Read(path)).Message);
    }
    [Fact]
    public void TextTransformsUseGodotRowsAndLowercaseExponentUnderForeignLocale()
    {
        var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ro-RO");
            var rest=Matrix4x4.CreateRotationZ(.7f)*Matrix4x4.CreateTranslation(1e-8f,2,3);
            var model=new ModelAsset("fixture",[new("root",-1,rest)],[]);
            GodotWriter.Write(model,[],directory,CancellationToken.None);
            string scene=File.ReadAllText(Path.Combine(directory,"fixture.tscn"));
            var match=Regex.Match(scene,@"bones/0/rest = Transform\( (.*?) \)");
            float[] v=match.Groups[1].Value.Split(',').Select(s=>float.Parse(s,CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(rest.M21,v[1],5);Assert.Equal(rest.M12,v[3],5);
            Assert.Contains("e-",scene);Assert.DoesNotContain("E-",scene);
        }
        finally{CultureInfo.CurrentCulture=previous;}
    }
    [Fact]
    public void QuaternionSignContinuityAndTwelveScalarKeysArePreserved()
    {
        var q=Quaternion.CreateFromYawPitchRoll(.3f,.4f,.2f);
        var clip=new AnimationClip("idle_loop",1,[0],[[new(Vector3.Zero,q,Vector3.One)],[new(Vector3.Zero,-q,Vector3.One)]]);
        GodotWriter.Write(new ModelAsset("fixture",[new("root",-1,Matrix4x4.Identity)],[]),[clip],directory,CancellationToken.None);
        string text=File.ReadAllText(Path.Combine(directory,"idle_loop.tres"));
        var match=Regex.Match(text,@"keys = PoolRealArray\( (.*?) \)");
        var v=match.Groups[1].Value.Split(',').Select(s=>float.Parse(s,CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(24,v.Length);Assert.Equal(1,v[12]);
        Assert.True(Quaternion.Dot(new(v[5],v[6],v[7],v[8]),new(v[17],v[18],v[19],v[20]))>.999f);
        Assert.Contains("Skeleton:root",text);
    }
    [Fact]
    public void XmlSkeletonAndInterleavedBindingAreRead()
    {
        string path=Fixture("fixture.hkx","""
        <hkpackfile><hksection>
        <hkobject class="hkaSkeleton" name="#1"><hkparam name="parentIndices">-1</hkparam><hkparam name="bones"><hkobject><hkparam name="name">root</hkparam></hkobject></hkparam><hkparam name="referencePose">(0 0 0 0)(0 0 0 1)(1 1 1 0)</hkparam></hkobject>
        <hkobject class="hkaInterleavedUncompressedAnimation" name="#2"><hkparam name="duration">1</hkparam><hkparam name="numberOfTransformTracks">1</hkparam><hkparam name="numberOfFloatTracks">0</hkparam><hkparam name="extractedMotion">null</hkparam><hkparam name="transforms">(0 0 0 0)(0 0 0 1)(1 1 1 0) (1 2 3 0)(0 0 0 1)(1 1 1 0)</hkparam></hkobject>
        <hkobject class="hkaAnimationBinding" name="#3"><hkparam name="animation">#2</hkparam><hkparam name="blendHint">NORMAL</hkparam><hkparam name="transformTrackToBoneIndices">0</hkparam></hkobject>
        </hksection></hkpackfile>
        """);
        var asset=new HavokReader().Read(path);Assert.Single(asset.Bones);
        var clip=Assert.Single(asset.Animations);Assert.Equal(2,clip.Frames.Length);Assert.Equal(new Vector3(1,2,3),clip.Frames[1][0].Translation);
    }
    [Fact]
    public void SplineBlocksUseExplicitOffsetsInsteadOfAssumingContiguousData()
    {
        byte[] bytes=new byte[112];
        void Block(int offset,float x)
        {
            bytes[offset]=20;bytes[offset+1]=1;bytes[offset+2]=15;
            using var stream=new MemoryStream(bytes);using var writer=new BinaryWriter(stream);
            stream.Position=offset+4;writer.Write(x);writer.Write(0f);writer.Write(0f);writer.Write(0f);writer.Write(1f);
        }
        Block(0,2);Block(64,9);
        var blocks=SplineCompressedAnimation.ReadSplineCompressedAnimByteBlock(false,bytes,1,2,[0,64]);
        Assert.Equal(2,blocks[0][0].StaticPosition.X);Assert.Equal(9,blocks[1][0].StaticPosition.X);
        Assert.Equal(Quaternion.Identity,blocks[1][0].StaticRotation);
    }
    [Fact]
    public async Task ExistingExportCannotBeOverwritten()
    {
        string model=Path.Combine(directory,"fixture.model");File.WriteAllBytes(model,ModelFixture(5));
        string sentinel=Fixture("keep.txt","preserve");
        await Assert.ThrowsAsync<IOException>(()=>new ConversionService().ConvertAsync(new(model,null,[],directory)));
        Assert.Equal("preserve",File.ReadAllText(sentinel));
    }
    [Fact]
    public void TagfilesAndTruncatedBinaryFailExplicitly()
    {
        string path=Path.Combine(directory,"bad.hkx");File.WriteAllBytes(path,[0,1,2]);
        Assert.Throws<NotSupportedException>(()=>new HavokReader().Read(path));
        path=Fixture("bad.model","truncated");Assert.Throws<InvalidDataException>(()=>new He1ModelReader().Read(path));
    }
    private static byte[] ModelFixture(int version, uint missingTangentBits = 0, int uvV = 1)
    {
        var bytes=new List<byte>(new byte[24]);
        int Reserve(int n){int p=bytes.Count;bytes.AddRange(new byte[n]);return p;}
        void U(int p,uint v){byte[] a=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(a,v);for(int i=0;i<4;i++)bytes[p+i]=a[i];}
        void F(int p,float v)=>U(p,BitConverter.SingleToUInt32Bits(v));
        void Pointer(int p,int target)=>U(p,(uint)(target-24));
        int Str(string s){int p=bytes.Count;bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(s));bytes.Add(0);return p;}
        int root=Reserve(version==4?48:version==2?44:32);
        int group=version<=4?root:Reserve(40);
        if(version>=5){U(root,1);int table=Reserve(4);Pointer(root+4,table);Pointer(table,group);}
        U(group,1);int meshes=Reserve(4);Pointer(group+4,meshes);int mesh=Reserve(44);Pointer(meshes,mesh);
        Pointer(mesh,Str("material"));U(mesh+4,3);int faces=Reserve(6);Pointer(mesh+8,faces);bytes[faces+3]=1;bytes[faces+5]=2;
        const int stride = 64;
        U(mesh+12,3);U(mesh+16,stride);int vertices=Reserve(3 * stride);Pointer(mesh+20,vertices);
        for (int i = 0; i < 3; i++)
        {
            int v = vertices + i * stride;
            F(v, i == 1 ? 1 : 0);
            F(v + 4, i == 2 ? 1 : 0);
            bytes[v + 16] = 255;
            F(v + 28, 1); // normal Z
            F(v + 32, 1); // tangent X
            if (i < 2 && missingTangentBits != 0)
                for (int component = 0; component < 3; component++) U(v + 32 + component * 4, missingTangentBits);
            F(v + 48, 1); // binormal Y
            F(v + 56, i == 2 ? 1 : 0);
            F(v + 60, i == 1 ? uvV : 0);
        }
        int declaration=Reserve(96);Pointer(mesh+24,declaration);
        void Element(int p,int offset,uint format,byte type){bytes[p+3]=(byte)offset;U(p+4,format);bytes[p+9]=type;}
        Element(declaration,0,0x2A23B9,0);Element(declaration+12,12,0x1A2286,2);Element(declaration+24,16,0x1A2086,1);
        Element(declaration+36,20,0x2A23B9,3);Element(declaration+48,32,0x2A23B9,6);Element(declaration+60,44,0x2A23B9,7);
        Element(declaration+72,56,0x2C23A5,5);U(declaration+88,uint.MaxValue);
        U(mesh+28,1);int palette=Reserve(2);Pointer(mesh+32,palette);bytes[palette+(version==6?1:0)]=1;
        int nodeField=root+(version==4?32:version==2?24:16);U(nodeField,2);int nodes=Reserve(8);Pointer(nodeField+4,nodes);
        for(int i=0;i<2;i++){int n=Reserve(8);Pointer(nodes+i*4,n);U(n,i==0?uint.MaxValue:0);Pointer(n+4,Str(i==0?"root":"child"));}
        int matrices=Reserve(128);Pointer(nodeField+8,matrices);
        for(int i=0;i<2;i++){int m=matrices+i*64;for(int j=0;j<4;j++)F(m+j*20,1);if(i==1)F(m+28,-2);}
        U(0,(uint)bytes.Count);U(4,(uint)version);U(8,(uint)(bytes.Count-24));U(12,24);
        return bytes.ToArray();
    }
}
