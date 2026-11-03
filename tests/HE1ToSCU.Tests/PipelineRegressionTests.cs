using System.Globalization;
using System.Numerics;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class PipelineRegressionTests
{
    [Fact]
    public void ParentScaleDoesNotMoveChildrenIncludingUniformScale()
    {
        var parent=new Pose(new(3,4,5),Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI/2),new(2,2,2));
        var child=new Pose(new(1,0,0),Quaternion.Identity,new(.5f,1,1));
        var world=AnimationRig.Compose(child,parent);
        Assert.True(Vector3.Distance(new(3,5,5),world.Translation)<1e-5);
        Assert.Equal(new Vector3(1,2,2),world.Scale);
        var model=new ModelAsset("rig",[new("root",-1,Matrix4x4.Identity),new("child",0,Matrix4x4.CreateTranslation(1,0,0))],[]);
        var clip=new AnimationClip("uniform",1,[0,1],[[parent,child]]);
        var prepared=AnimationRig.Prepare(model,[clip],CancellationToken.None);
        Assert.Equal(1,prepared.Helpers);
        var frame=prepared.Clips[0].Frames[0];var bones=prepared.Model.Bones;
        Matrix4x4 Global(int i)=>bones[i].Parent<0?frame[i].Matrix:frame[i].Matrix*Global(bones[i].Parent);
        Assert.True(Spatial.Near(Global(1),world.Matrix,1e-5f));
    }

    [Fact]
    public void SparseTracksRestoreReferenceAndUseCorrectBoneIndexes()
    {
        Pose[] reference=[new(new(1,2,3),Quaternion.Identity,Vector3.One),new(new(7,8,9),Quaternion.Identity,Vector3.One)];
        var driven=new Pose(Vector3.Zero,Quaternion.CreateFromAxisAngle(Vector3.UnitY,.7f),Vector3.One);
        var clip=new AnimationClip("eyes",1,[1],[[driven],[driven]]);
        var complete=AnimationSampling.Complete(clip,reference);
        Assert.Equal(new[]{0,1},complete.TrackToBone);
        Assert.Equal(reference[0],AnimationSampling.Sample(complete,.5f)[0]);
        Assert.Equal(driven,AnimationSampling.Sample(complete,.5f)[1]);
    }

    [Theory]
    [InlineData(AnimationBlend.AdditiveDeprecated)]
    [InlineData(AnimationBlend.Additive)]
    public void AdditiveBindingIsBakedAgainstExplicitReferenceInCorrectOrder(AnimationBlend blend)
    {
        var rest=new Pose(new(1,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitX,.4f),new(2,1,1));
        var delta=new Pose(new(0,1,0),Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.8f),new(.5f,1,1));
        var complete=AnimationSampling.Complete(new AnimationClip("additive",1,[0],[[delta]],Blend:blend),[rest]);
        var actual=complete.Frames[0][0];
        var expected=blend==AnimationBlend.AdditiveDeprecated ? AnimationRig.Compose(rest,delta) : AnimationRig.Compose(delta,rest);
        Assert.True(Spatial.Near(expected.Matrix,actual.Matrix,1e-5f));
        Assert.NotNull(complete.BakedReference);Assert.Equal(AnimationBlend.Normal,complete.Blend);
    }

    [Fact]
    public void AdaptiveBakePreservesContinuousScaleAndRotationBetweenSourceKeys()
    {
        var model=new ModelAsset("rig",[new("root",-1,Matrix4x4.Identity),new("child",0,Matrix4x4.CreateTranslation(1,0,0))],[]);
        Pose[] Sample(float t)=>[new(Vector3.Zero,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,t*t),new(.1f+t*t,1,1)),new(new(1,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,t),new(1/(.1f+t*t),1,1))];
        var source=new AnimationClip("continuous",1,[0,1],[Sample(0),Sample(1)],SourceSampler:Sample);
        var prepared=AnimationRig.Prepare(model,[source],CancellationToken.None);
        Assert.True(prepared.Clips[0].Frames.Length>2);Assert.Equal(1,prepared.Clips[0].Times![^1]);
        for(int n=0;n<=100;n++) {
            float time=n/100f;var frame=AnimationSampling.Sample(prepared.Clips[0],time);
            Matrix4x4 Global(int i)=>prepared.Model.Bones[i].Parent<0?frame[i].Matrix:frame[i].Matrix*Global(prepared.Model.Bones[i].Parent);
            var expected=AnimationRig.ModelPose(model.Bones,Sample(time));
            Assert.True(Vector3.Distance(Global(1).Translation,expected[1].Translation)<.0006f);
            Assert.True(Spatial.Near(Global(1),expected[1].Matrix,.0011f));
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(10)] [InlineData(50)] [InlineData(150)] [InlineData(512)] [InlineData(1024)]
    public void GgxRoughnessMatchesNormalizedSourceDistributionPeak(int power)
    {
        double r=SpecularApproximation.PeakRoughness(power),q=Math.Pow(r,4);
        Assert.Equal((power+2)/(2*Math.PI),1/(Math.PI*q),3);
    }

    [Fact]
    public void BothGamesClampPowerAfterGlossAndHaveDifferentBrightnessModels()
    {
        Assert.Equal(750,SpecularApproximation.Power(3,.5f));
        Assert.Equal(1024,SpecularApproximation.Power(30));Assert.Equal(1,SpecularApproximation.Power(-3));
        Assert.Equal(.5f,SpecularApproximation.Brightness(.2f,.5f));
        Assert.Equal(64,SpecularApproximation.Power(.4f,1,SourceGame.SonicUnleashed));
        Assert.Equal(32,SpecularApproximation.Power(.4f,.5f,SourceGame.SonicUnleashed));
        Assert.Equal(512,SpecularApproximation.Power(8,1,SourceGame.SonicUnleashed));
        Assert.Equal(1,SpecularApproximation.Power(8,0,SourceGame.SonicUnleashed));
        Assert.Equal(1,SpecularApproximation.Power(-8,1,SourceGame.SonicUnleashed));
    }

    [Fact]
    public void UnleashedBrightnessFollowsPowerAndIgnoresGenerationsZParameter()
    {
        SpecularSample[] samples=[new(128,255,1),new(255,128,1)];
        var unleashed=SpecularApproximation.Fit(.4f,0,1,samples,SourceGame.SonicUnleashed);
        var differentZ=SpecularApproximation.Fit(.4f,100,1,samples,SourceGame.SonicUnleashed);
        var equivalentGenerations=SpecularApproximation.Fit(64f/500,1.6f,1,samples);
        Assert.Equal(unleashed.Specular,differentZ.Specular);
        Assert.Equal(unleashed.Roughness.OrderBy(p=>p.Key),differentZ.Roughness.OrderBy(p=>p.Key));
        Assert.Equal(unleashed.Specular,equivalentGenerations.Specular,5);
        Assert.Equal(unleashed.Roughness.OrderBy(p=>p.Key),equivalentGenerations.Roughness.OrderBy(p=>p.Key));
    }

    [Theory]
    [InlineData(SourceGame.SonicGenerations)]
    [InlineData(SourceGame.SonicUnleashed)]
    public void SpecularMaskNoiseCannotChangeRoughnessWhenPowerIsConstant(SourceGame game)
    {
        var samples=Enumerable.Range(0,256).Select(i=>new SpecularSample(255,(byte)i,1)).ToArray();
        var fit=SpecularApproximation.Fit(.3f,.19f,1,samples,game);
        Assert.Single(fit.Roughness.Values.Distinct());
        Assert.InRange(fit.Specular,0,1);
    }

    [Theory]
    [InlineData(SourceGame.SonicGenerations)]
    [InlineData(SourceGame.SonicUnleashed)]
    public void BakedGlossRoughnessIsMonotonicAndIndependentOfSpecularMask(SourceGame game)
    {
        var samples=Enumerable.Range(0,256).SelectMany(g=>new[]{new SpecularSample((byte)g,0,1),new SpecularSample((byte)g,255,1)}).ToArray();
        var fit=SpecularApproximation.Fit(.6f,.2f,1,samples,game);
        byte previous=255;
        for(int g=0;g<256;g++) {
            byte value=fit.Roughness[(g<<8)|255];
            Assert.Equal(value,fit.Roughness[g<<8]);
            Assert.True(value<=previous);previous=value;
        }
    }

    [Theory]
    [InlineData(0.002f,0f)] [InlineData(0.002f,.1f)] [InlineData(.1f,.2f)] [InlineData(2.048f,1f)] [InlineData(3f,5f)]
    public void AngularFitIsFiniteBoundedAndImprovesItsWidthMatchedSeed(float power,float brightness)
    {
        var fit=SpecularApproximation.Fit(power,brightness,1,[new(0,255,1),new(64,128,1),new(128,64,1),new(255,255,1)]);
        Assert.InRange(fit.Specular,0,1);Assert.True(double.IsFinite(fit.Error));
        Assert.True(fit.Error<=fit.InitialError+1e-8);
        Assert.All(fit.Roughness.Values,value=>Assert.InRange(value,(byte)1,(byte)255));
        Assert.Equal(.04/(Math.PI*Math.Pow(.4,4)),SpecularApproximation.GodotLobe(.4,1,1),9);
    }

    [Fact]
    public void PowFilenameIsDetectedAndBakedIntoStandardSpatialMaterial()
    {
        string directory=Path.Combine(Path.GetTempPath(),"HE1SCU-pow-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            // RGBA 2x1 DDS: black/white gloss extremes, declared under a generic slot.
            var bytes=new byte[136];"DDS "u8.CopyTo(bytes);
            void U(int p,uint value)=>System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(p),value);
            U(4,124);U(12,1);U(16,2);U(76,32);U(80,0x41);U(88,32);U(92,0xff);U(96,0xff00);U(100,0xff0000);U(104,0xff000000);
            bytes[131]=255;bytes[132]=bytes[133]=bytes[134]=bytes[135]=255;
            string path=Path.Combine(directory,"body_pow_HD.dds");File.WriteAllBytes(path,bytes);
            var material=new He1Material("body","ChrSkin_dp",.5f,false,false,new Dictionary<string,Vector4[]>{["power_gloss_level"]=[new(50,.3f,.19f,0)]},[new("body_pow_HD","unknown",0,0,0,path)]);
            var model=new ModelAsset("rig",[],[new("body","opaque",[],[])],new Dictionary<string,He1Material>{["body"]=material});
            new SpatialMaterialWriter().Write(model,directory,null,CancellationToken.None);
            string text=File.ReadAllText(Path.Combine(directory,"materials/body.tres"));
            Assert.Contains("type=\"SpatialMaterial\"",text);Assert.DoesNotContain("ShaderMaterial",text);Assert.Contains("roughness_texture",text);
            string texture=File.ReadAllText(Path.Combine(directory,"textures/body_roughness.texture.tres"));
            Assert.Contains("flags = 7",texture);
            var match=System.Text.RegularExpressions.Regex.Match(texture,@"PoolByteArray\( ([^)]*)");
            var pixels=match.Groups[1].Value.Split(',').Select(v=>byte.Parse(v.Trim(),CultureInfo.InvariantCulture)).ToArray();
            Assert.NotEqual(pixels[0],pixels[4]);Assert.Equal(255,pixels[3]);Assert.Equal(255,pixels[7]);
        } finally {Directory.Delete(directory,true);}
    }
}
