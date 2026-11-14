using System.Buffers.Binary;
using System.Numerics;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class PrrParameterCorrectnessTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("PRR-parameters-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);
    private static He1Material Source(Dictionary<string,Vector4[]>? parameters=null)=>new("test","test",.5f,false,false,parameters??[],[]);
    private static PrrShader Shader(string declarations)=>new("test","shader_type spatial;\n"+declarations,
        PrrShaderLibrary.ParseUniforms("shader_type spatial;\n"+declarations));

    [Fact]
    public void UniformDiscoveryRetainsHintsInitializersArraysAndIgnoresComments()
    {
        var uniforms=PrrShaderLibrary.ParseUniforms("""
            shader_type spatial;
            // uniform vec4 invisible : hint_color;
            uniform highp float intensity : hint_range(-2.0, 4.0, 0.01) = 1.0;
            uniform vec3 direction /* inline */ = vec3(1.0, 0.0, -1.0);
            uniform vec4 diffuse : hint_color;
            uniform vec4 ambient : hint_color = vec4(1.0);
            uniform sampler2D map : hint_albedo;
            uniform float unsupported_array[4];
            """);
        Assert.Equal(6,uniforms.Count);
        Assert.Equal("hint_range(-2.0, 4.0, 0.01)",uniforms[0].Hint);
        Assert.Equal("1.0",uniforms[0].DefaultValue);
        Assert.Equal("hint_color",uniforms[2].Hint);
        Assert.Null(uniforms[2].DefaultValue);
        Assert.Equal("vec4(1.0)",uniforms[3].DefaultValue);
        Assert.Equal("hint_albedo",uniforms[4].Hint);
        Assert.Equal("[4]",uniforms[5].ArrayDeclaration);
        Assert.Throws<InvalidDataException>(()=>PrrShaderLibrary.ParseUniforms("shader_type spatial; uniform float a,b;"));
        Assert.Throws<InvalidDataException>(()=>PrrShaderLibrary.ParseUniforms("shader_type spatial; uniform float unfinished"));
    }

    [Fact]
    public void ColorAndDataTypesPreserveZeroNegativeHdrAndAlphaWithoutCompensation()
    {
        var source=Source(new(){["color"]=[new(.8f,.35f,.1f,0)],["data"]=[new(-2,0,8,4)],["vector"]=[new(2,-3,0,99)]});
        var result=PrrParameterMapper.Map(source,Shader("uniform vec4 color : hint_color; uniform vec4 data; uniform vec3 vector;"));
        Assert.Equal("Color",result.Assignments.Single(a=>a.Name=="color").Expected.VariantType);
        Assert.Equal(new double[]{(float).8f,(float).35f,(float).1f,0},result.Assignments.Single(a=>a.Name=="color").Expected.Components);
        Assert.Equal("Plane( -2, 0, 8, 4 )",result.Values["data"]);
        Assert.Equal("Vector3( 2, -3, 0 )",result.Values["vector"]);
        Assert.Empty(result.Unmapped);
        Assert.StartsWith("Plane( -0.0,",PrrUniformValue.Convert(new("vec4","zero",""),[-0.0,0,0,0]).Literal);
    }

    [Fact]
    public void SourceRgbPaddingIsNotOpacityAndPackedEmissionWIsNotDiscarded()
    {
        var source=Source(new(){["diffuse"]=[new(.8f,.35f,.1f,0)],["ambient"]=[new(0,0,0,0)],
            ["specular"]=[new(2,3,4,0)],["g_SonicEyeHighLightColor"]=[new(.5f,.5f,.5f,0)],
            ["mrgChrEmissionParam"]=[new(1,2,3,4)]});
        var result=PrrParameterMapper.Map(source,Shader("uniform vec4 diffuse_color:hint_color; uniform vec4 ambient_light_color:hint_color; uniform vec4 specular_color:hint_color; uniform vec4 eye_highlight_color:hint_color; uniform vec4 material_emission;"));
        foreach(var name in new[]{"diffuse_color","ambient_light_color","specular_color","eye_highlight_color"})
            Assert.Equal(1,result.Assignments.Single(a=>a.Name==name).Expected.Components[3]);
        Assert.Equal(0,result.Parameters.Single(p=>p.SourceName=="diffuse").RawSourceValue[0][3]);
        Assert.Equal("Plane( 1, 2, 3, 4 )",result.Values["material_emission"]);
    }

    [Theory]
    [InlineData(2,-3,2.5)]
    [InlineData(-2,1.3,.4)]
    [InlineData(0,0,0)]
    public void EyeHighlightAliasAndExactUniformPreserveAuthoredComponents(float x,float y,float z)
    {
        foreach(var game in new[]{SourceGame.SonicGenerations,SourceGame.SonicUnleashed})
        {
            var shader=PrrShaderLibrary.Default.Match(game,game==SourceGame.SonicGenerations?"ChrEye_dpne1@@":"SonicEye_dpne@c@").Shader!;
            foreach(string name in new[]{"g_SonicEyeHighLightPosition","eye_highlight_position"})
            {
                var result=PrrParameterMapper.Map(Source(new(){[name]=[new(x,y,z,99)]}),shader);
                Assert.Equal(new double[]{x,y,z},result.Assignments.Single(a=>a.Name=="eye_highlight_position").Expected.Components);
                Assert.DoesNotContain("model position scale",result.Mappings[name]);
                Assert.Equal(99,result.Parameters.Single().RawSourceValue[0][3]);
            }
        }
    }

    [Theory]
    [InlineData("float",1,"float","1.0")]
    [InlineData("float",0,"float","0.0")]
    [InlineData("float",-4,"float","-4.0")]
    [InlineData("int",2147483647,"int","2147483647")]
    [InlineData("int",-2147483648,"int","-2147483648")]
    [InlineData("bool",0,"bool","false")]
    [InlineData("bool",1,"bool","true")]
    public void ScalarsUseExactGodotVariants(string type,double input,string expectedKind,string literal)
    {
        var value=PrrUniformValue.Convert(new(type,"value",""),[input,0,0,0],storage:type=="int"?"int4":"float4");
        Assert.Equal(expectedKind,value.VariantType);Assert.Equal(literal,value.Literal);
    }

    [Fact]
    public void InvalidBoolFractionalIntAndLossyIntToFloatAreNotSilentlyCast()
    {
        Assert.Throws<InvalidDataException>(()=>PrrUniformValue.Convert(new("bool","v",""),[2,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>PrrUniformValue.Convert(new("int","v",""),[1.5,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>PrrUniformValue.Convert(new("float","v",""),[2147483647,0,0,0],storage:"int4"));
        var packed=PrrParameterMapper.Map(Source(new(){["intensity"]=[new(1,4,8,0)]}),Shader("uniform float intensity;"));
        Assert.Equal("unsupported",Assert.Single(packed.Parameters).Classification);
        Assert.Empty(packed.Values);
        Assert.Throws<NotSupportedException>(()=>PrrUniformValue.Convert(new("vec4","v",""),[1,0,1,0],storage:"bool4"));
    }

    [Theory]
    [InlineData(0,2)] [InlineData(3,3)] [InlineData(4,4)]
    public void UnrepresentableSamplerWrappingIsReportedRatherThanChangedToRepeat(int u,int v)
    {
        var material=Source() with {Shader="ChrSkin_dsn",Textures=[new("map","diffuse",0,u,v,null)]};
        var model=new ModelAsset("test",[],[new("test","opaque",[],[])],new Dictionary<string,He1Material>{{"test",material}});
        string output=Path.Combine(directory,"out");
        var result=new PrrMaterialWriter().Write(model,output,SourceGame.SonicGenerations,new(null,output,false),null,default);
        var texture=Assert.Single(Assert.Single(result.Audits).Textures);
        Assert.Equal("unsupported",texture.Status);Assert.False(texture.WrapModesPreserved);
    }

    [Fact]
    public void VerifiedPackedRulesUseXzWAndKeepBothGiOutputs()
    {
        var source=Source(new(){["mrgInShadowScale"]=[new(2,9,4,8)],["g_GI0Scale"]=[new(1,2,3,.5f)],["g_GI1Scale"]=[new(9,8,7,.25f)]});
        var mapped=PrrParameterMapper.Map(source,Shader("uniform vec2 environment_shadow_scale; uniform vec3 gi_rgb_scale; uniform float ambient_shadow_scale; uniform float gi_saturation;"));
        Assert.Equal("Vector2( 2, 4 )",mapped.Values["environment_shadow_scale"]);
        Assert.Equal("Vector3( 1, 2, 3 )",mapped.Values["gi_rgb_scale"]);
        Assert.Equal("0.5",mapped.Values["ambient_shadow_scale"]);
        Assert.Equal("0.25",mapped.Values["gi_saturation"]);
        Assert.Equal(2,mapped.Parameters.Single(p=>p.SourceName=="g_GI0Scale").Destinations.Length);
    }

    [Fact]
    public void EmissionRegistersAreFamilySpecificRatherThanUniversalAliases()
    {
        var source=Source(new(){["emissive"]=[new(1,2,3,9)],["mrgChrEmissionParam"]=[new(4,5,6,7)]});
        var material=PrrParameterMapper.Map(source,Shader("uniform vec3 material_emission;"));
        Assert.Equal("Vector3( 1, 2, 3 )",material.Values["material_emission"]);
        Assert.Equal("intentionally_unused",material.Parameters.Single(p=>p.SourceName=="mrgChrEmissionParam").Classification);
        var enemy=PrrParameterMapper.Map(source,Shader("uniform vec4 material_emission;"));
        Assert.Equal("Plane( 4, 5, 6, 7 )",enemy.Values["material_emission"]);
        Assert.Equal("intentionally_unused",enemy.Parameters.Single(p=>p.SourceName=="emissive").Classification);
        Assert.Empty(PrrParameterMapper.Map(Source(new(){["emissive"]=[Vector4.Zero]}),Shader("uniform vec4 material_emission=vec4(0,0,0,1);" )).Values);
    }

    [Theory]
    [MemberData(nameof(PrrMaterialTests.ShaderPaths),MemberType=typeof(PrrMaterialTests))]
    public void EveryActualShaderGetsOnlyItsDeclaredSkyGroundDefaultsAndKeepsAuthoredValues(string path)
    {
        var shader=PrrShaderLibrary.Default.Get(path);
        var result=PrrParameterMapper.Map(Source(),shader);
        foreach(var (name,value) in new[]{("sky_color",.5), ("ground_color",.25)})
        {
            bool declares=shader.Uniforms.Any(u=>u.Name==name);
            Assert.Equal(declares,result.Values.ContainsKey(name));
            if(!declares)continue;
            var assignment=result.Assignments.Single(a=>a.Name==name);
            Assert.Equal("Color",assignment.Expected.VariantType);
            Assert.Equal(new double[]{value,value,value,1},assignment.Expected.Components);
            Assert.Equal("ConverterDefault",assignment.Origin);
            var authored=PrrParameterMapper.Map(Source(new(){[name]=[new(0,2,-1,0)]}),shader).Assignments.Single(a=>a.Name==name);
            Assert.Equal("HE1",authored.Origin);Assert.Equal(new double[]{0,2,-1,0},authored.Expected.Components);
        }
    }

    [Fact]
    public void SkyDefaultsAdaptToScalarAndVectorTypesWithoutChangingUnrelatedDefaults()
    {
        var result=PrrParameterMapper.Map(Source(),Shader("uniform float sky_color=9.0; uniform vec3 ground_color=vec3(8.0); uniform float untouched=7.0;"));
        Assert.Equal("0.5",result.Values["sky_color"]);Assert.Equal("Vector3( 0.25, 0.25, 0.25 )",result.Values["ground_color"]);
        Assert.False(result.Values.ContainsKey("untouched"));
    }

    [Fact]
    public void ArraysMatricesUnknownAndDuplicateInputsReceiveExplicitClassifications()
    {
        var source=Source(new(){["data"]=[Vector4.One,Vector4.Zero],["unknown"]=[Vector4.Zero],["region_transform"]=[Vector4.One,Vector4.One,Vector4.One,Vector4.One]});
        var result=PrrParameterMapper.Map(source,Shader("uniform vec4 data; uniform mat4 region_transform;"));
        Assert.Equal(2,result.Parameters.Count(p=>p.Classification=="unsupported"));
        Assert.Equal("missing_mapping",result.Parameters.Single(p=>p.SourceName=="unknown").Classification);
        Assert.Equal(2,result.Parameters.Single(p=>p.SourceName=="data").RawSourceValue.Length);
        var duplicate=Source(new(){["diffuse"]=[Vector4.One]}) with {NativeParameters=[new("diffuse","float4",[new double[]{0,0,0,0}])]};
        var mapped=PrrParameterMapper.Map(duplicate,Shader("uniform vec4 diffuse_color : hint_color;"));
        Assert.All(mapped.Parameters,p=>Assert.Equal("ambiguous",p.Classification));Assert.Empty(mapped.Values);
    }

    [Fact]
    public void ExplicitMatrixAdaptersPreserveRowsAndRejectProjectiveMat4Loss()
    {
        Assert.Equal("Transform2D( 1, 3, 2, 4, 0, 0 )",PrrUniformValue.MatrixRows("mat2",[[1,2],[3,4]]).Literal);
        Assert.Equal("Basis( 1, 2, 3, 4, 5, 6, 7, 8, 9 )",PrrUniformValue.MatrixRows("mat3",[[1,2,3],[4,5,6],[7,8,9]]).Literal);
        Assert.Equal("Transform( 1, 2, 3, 5, 6, 7, 9, 10, 11, 4, 8, 12 )",
            PrrUniformValue.MatrixRows("mat4",[[1,2,3,4],[5,6,7,8],[9,10,11,12],[0,0,0,1]]).Literal);
        Assert.Throws<NotSupportedException>(()=>PrrUniformValue.MatrixRows("mat4",[[1,2,3,4],[5,6,7,8],[9,10,11,12],[1,0,0,1]]));
    }

    [Theory]
    [InlineData(1)] [InlineData(3)]
    public void MirageReadsFloatArraysSignedInt4AndByteBool4WithIndependentCounts(int version)
    {
        string path=WriteFixture(version);
        var material=new He1MaterialReader().Read(path,version==1?SourceGame.SonicUnleashed:SourceGame.SonicGenerations);
        Assert.Equal(version,material.MaterialVersion);
        Assert.Equal(new Vector4(-2,0,4,1),material.Parameters["data"][0]);
        Assert.Equal(new Vector4(0,-8,16,0),material.Parameters["data"][1]);
        Assert.Equal("data\r",material.FloatMetadata["data"].RawName);
        Assert.Equal(new double[]{int.MinValue,int.MaxValue,0,42},material.NativeParameters.Single(p=>p.Storage=="int4").Values[0]);
        Assert.Equal(new double[]{1,0,0,1},material.NativeParameters.Single(p=>p.Storage=="bool4").Values[0]);
        Assert.Equal(3,material.AllParameters.Count());
    }

    [Theory]
    [InlineData(1)] [InlineData(3)]
    public void EmptyDuplicateArrayIsRetainedWithoutErasingTheLegacyFloatDictionary(int version)
    {
        var material=new He1MaterialReader().Read(WriteFixture(version,true));
        Assert.Equal(2,material.Parameters["data"].Length);
        var records=material.AllParameters.Where(p=>p.Name=="data").ToArray();
        Assert.Equal(2,records.Length);
        Assert.Contains(records,p=>p.Values.Length==0);
    }

    private string WriteFixture(int version,bool emptyDuplicate=false)
    {
        var bytes=new List<byte>(new byte[60]);
        int Reserve(int n){int p=bytes.Count;bytes.AddRange(new byte[n]);return p;}
        void U(int p,uint value){Span<byte>b=stackalloc byte[4];BinaryPrimitives.WriteUInt32BigEndian(b,value);for(int i=0;i<4;i++)bytes[p+i]=b[i];}
        int Str(string s){int p=bytes.Count;bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(s));bytes.Add(0);return p;}
        void Ptr(int p,int target)=>U(p,(uint)(target-24));
        Ptr(24,Str("fixture"));if(version==1)Ptr(32,Str("empty"));
        foreach(var (countOffset,pointerOffset,name,storage) in new[]{(44,48,"data\r","float4"),(45,52,"integer","int4"),(46,56,"boolean","bool4")})
        {
            bool duplicate=storage=="float4"&&emptyDuplicate;
            bytes[countOffset]=(byte)(duplicate?2:1);int table=Reserve(duplicate?8:4);Ptr(pointerOffset,table);int p=Reserve(12);Ptr(table,p);
            bytes[p]=2;bytes[p+2]=(byte)(storage=="float4"?2:1);Ptr(p+4,Str(name));
            int values=Reserve(storage=="float4"?32:storage=="int4"?16:4);Ptr(p+8,values);
            if(storage=="float4") {float[] f=[-2,0,4,1,0,-8,16,0];for(int i=0;i<f.Length;i++)U(values+i*4,BitConverter.SingleToUInt32Bits(f[i]));}
            else if(storage=="int4") {int[] ints=[int.MinValue,int.MaxValue,0,42];for(int i=0;i<4;i++)U(values+i*4,unchecked((uint)ints[i]));}
            else {bytes[values]=1;bytes[values+3]=1;}
            if(duplicate){int empty=Reserve(12);Ptr(table+4,empty);Ptr(empty+4,Str("data"));}
        }
        U(0,(uint)bytes.Count);U(4,(uint)version);U(12,24);
        string path=Path.Combine(directory,"fixture.material");File.WriteAllBytes(path,bytes.ToArray());
        if(version==1)
        {
            byte[] set=new byte[32];BinaryPrimitives.WriteUInt32BigEndian(set,32);BinaryPrimitives.WriteUInt32BigEndian(set.AsSpan(4),1);
            BinaryPrimitives.WriteUInt32BigEndian(set.AsSpan(12),24);File.WriteAllBytes(Path.Combine(directory,"empty.texset"),set);
        }
        return path;
    }
}
