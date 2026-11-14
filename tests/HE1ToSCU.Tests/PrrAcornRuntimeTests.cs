using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using HE1ToSCU.Core;

namespace HE1ToSCU.Tests;

public sealed class AcornFactAttribute : FactAttribute
{
    internal static string Engine => Environment.GetEnvironmentVariable("HE1SCU_TEST_ACORN") ?? @"D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe";
    public AcornFactAttribute() {if(!File.Exists(Engine))Skip="Set HE1SCU_TEST_ACORN to a Godot 3.1/Acorn executable for native parameter tests.";}
}

public sealed class PrrAcornRuntimeTests
{
    [AcornFact]
    public async Task SameSourceMaterialInDifferentCoverageSlotsRetainsIndependentTextAndBinaryParameters()
    {
        string directory=System.IO.Directory.CreateTempSubdirectory("PRR-native-coverage-").FullName;
        try
        {
            var source=new He1Material("eye","ChrEye_dpne1@@",.4f,false,false,new Dictionary<string,System.Numerics.Vector4[]>
            {
                ["diffuse"]=[new(.6f,.4f,.2f,0)], ["opacity_reflection_refraction_spectype"]=[new(.75f,0,0,0)],
                ["g_SonicEyeHighLightPosition"]=[new(2,-3,2.5f,0)]
            },[]);
            var model=new ModelAsset("eye",[new("Root",-1,System.Numerics.Matrix4x4.Identity)],
                new[]{"opaque","punch","transparent"}.Select(slot=>new Surface("eye",slot,[],[])).ToList(),
                new Dictionary<string,He1Material>{{"eye",source}});
            var result=new PrrMaterialWriter().Write(model,directory,SourceGame.SonicGenerations,new(null,directory,false),null,default);
            result.WriteExpected(directory);
            File.WriteAllText(Path.Combine(directory,"project.godot"),"config_version=4\n[rendering]\nquality/driver/driver_name=\"GLES3\"\n");
            string functions=(string)typeof(ConversionService).Assembly.GetType("HE1ToSCU.Core.MaterialResourceValidation")!.GetField("Functions",BindingFlags.Public|BindingFlags.Static)!.GetRawConstantValue()!;
            string[] files=result.MaterialFiles.Values.Select(f=>"res://"+f).ToArray();
            foreach(string stage in new[]{"text","binary"})
            {
                string script="extends SceneTree\nfunc fail(message):\n    printerr(\"COVERAGE_FAILED \"+message)\n    quit()\n"+functions+
                    "\nfunc _init():\n    load_prr_expectations(\""+stage+"\")\n    var index=0\n    for path in "+JsonSerializer.Serialize(stage=="text"?files:Enumerable.Range(0,files.Length).Select(i=>"res://slot"+i+".res").ToArray())+":\n"+
                    "        var material=load(path)\n        if material == null:\n            fail(\"load \"+path)\n            return\n        if not check_prr_material(material):\n            return\n"+
                    (stage=="text"?"        clear_material_paths(material)\n        if ResourceSaver.save(\"res://slot\"+str(index)+\".res\",material,65) != OK:\n            fail(\"save\")\n            return\n":"")+
                    "        index+=1\n    save_prr_readbacks()\n    print(\"COVERAGE_OK\")\n    quit()\n";
                File.WriteAllText(Path.Combine(directory,"verify.gd"),script);
                var info=new ProcessStartInfo(AcornFactAttribute.Engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                foreach(string arg in new[]{"--no-window","--path",directory,"--script","verify.gd"})info.ArgumentList.Add(arg);
                using var process=Process.Start(info)!;
                var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
                string output=await stdout+await stderr;
                Assert.Contains("COVERAGE_OK",output);Assert.DoesNotContain("ERROR",output);Assert.DoesNotContain("COVERAGE_FAILED",output);
            }
            result.ReadBack(directory);
            foreach(var audit in result.Audits)
                Assert.All(audit.Mapping.Assignments,a=>Assert.Equal("text_and_binary_verified",a.RuntimeStatus));
        }
        finally {System.IO.Directory.Delete(directory,true);}
    }

    [AcornFact]
    public async Task TypedParametersTexturesAndMatricesSurviveNativeSaveAndIndependentReload()
    {
        using var fixture=new Fixture();
        await fixture.Run("text",false);
        await fixture.Run("binary",false);
        fixture.Result.ReadBack(fixture.Directory);
        Assert.All(fixture.Mapping.Assignments,a=>
        {
            Assert.Equal("verified",a.Actual["text"].Status);
            Assert.Equal("verified",a.Actual["binary"].Status);
        });
        Assert.All(fixture.Result.Audits.Single().Textures,t=>Assert.Equal("verified",t.Actual["binary"].Status));
    }

    [AcornFact]
    public async Task IndependentExpectationsRejectWrongColorVariantWrongValuesScalarTypesAndSamplerPixels()
    {
        foreach(string mutation in new[]{"plane-color","black-color","integer-float","wrong-integer","zero-vector","wrong-pixels"})
        {
            using var fixture=new Fixture();
            string file=Path.Combine(fixture.Directory,"typed.tres"),text=File.ReadAllText(file);
            text=mutation switch
            {
                "plane-color"=>text.Replace("shader_param/tint = Color(","shader_param/tint = Plane("),
                "black-color"=>text.Replace(fixture.Mapping.Values["tint"],"Color( 0, 0, 0, 0 )"),
                "integer-float"=>text.Replace("shader_param/scalar = 1.0","shader_param/scalar = 1"),
                "wrong-integer"=>text.Replace("2147483647","2147483646"),
                "zero-vector"=>text.Replace("Vector3( 2, -3, 0 )","Vector3( 0, 0, 0 )"),
                _=>text
            };
            File.WriteAllText(file,text);
            if(mutation=="wrong-pixels")
            {
                string texture=Path.Combine(fixture.Directory,"texture.tres");
                File.WriteAllText(texture,File.ReadAllText(texture).Replace("16, 32, 64, 128","0, 0, 0, 0"));
            }
            await fixture.Run("text",true);
            fixture.Result.ReadBack(fixture.Directory);
            Assert.True(fixture.Mapping.Assignments.Any(a=>a.Actual.Values.Any(v=>v.Status=="failed")) ||
                fixture.Result.Audits.Single().Textures.Any(t=>t.Actual.Values.Any(v=>v.Status=="failed")),mutation);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory {get;}=System.IO.Directory.CreateTempSubdirectory("PRR-native-parameters-").FullName;
        public PrrMappingResult Mapping {get;}=new();
        public PrrExportResult Result {get;}
        private readonly string functions;
        public Fixture()
        {
            string code="""
                shader_type spatial;
                render_mode unshaded;
                uniform float scalar;
                uniform int integer;
                uniform bool enabled=true;
                uniform vec2 two;
                uniform vec3 three;
                uniform vec4 data;
                uniform vec4 tint : hint_color;
                uniform vec4 diffuse_color : hint_color;
                uniform vec4 eye_highlight_color : hint_color;
                uniform vec3 eye_highlight_position;
                uniform mat2 matrix2;
                uniform mat3 matrix3;
                uniform mat4 matrix4;
                uniform sampler2D map;
                void fragment(){ALBEDO=tint.rgb;}
                """;
            var uniforms=PrrShaderLibrary.ParseUniforms(code).ToDictionary(u=>u.Name);
            void Add(string name,double[] input)=>Mapping.Assign(uniforms[name],PrrUniformValue.Convert(uniforms[name],input),"HE1",name,"Deterministic typed source fixture","fixture");
            Add("scalar",[1,0,0,0]);Add("integer",[int.MaxValue,0,0,0]);Add("enabled",[0,0,0,0]);
            Add("two",[2,-3,0,0]);Add("three",[2,-3,0,99]);Add("data",[-2,0,8,4]);Add("tint",[(float).8f,(float).35f,(float).1f,0]);
            var eyeSource=new He1Material("eye","ChrEye_dpne1@@",.5f,false,false,new Dictionary<string,System.Numerics.Vector4[]>()
            {
                ["diffuse"]=[new(.8f,.35f,.1f,0)],
                ["g_SonicEyeHighLightColor"]=[new(.5f,.5f,.5f,0)],
                ["g_SonicEyeHighLightPosition"]=[new(2,-3,2.5f,0)]
            },[]);
            var eyeMapping=PrrParameterMapper.Map(eyeSource,new("eye",code,uniforms.Values.ToArray()));
            // Independent semantic expectations, not merely a serialization round trip.
            Assert.Equal(new double[]{(float).8f,(float).35f,(float).1f,1},eyeMapping.Assignments.Single(a=>a.Name=="diffuse_color").Expected.Components);
            Assert.Equal(new double[]{.5,.5,.5,1},eyeMapping.Assignments.Single(a=>a.Name=="eye_highlight_color").Expected.Components);
            Assert.Equal(new double[]{2,-3,2.5},eyeMapping.Assignments.Single(a=>a.Name=="eye_highlight_position").Expected.Components);
            foreach(var assignment in eyeMapping.Assignments)
                Mapping.Assign(uniforms[assignment.Name],assignment.Expected,assignment.Origin,assignment.SourceName,assignment.Evidence,assignment.Confidence);
            foreach(var(type,name,rows)in new[]{("mat2","matrix2",new double[][]{[1,2],[3,4]}),
                ("mat3","matrix3",new double[][]{[1,2,3],[4,5,6],[7,8,9]}),
                ("mat4","matrix4",new double[][]{[1,2,3,4],[5,6,7,8],[9,10,11,12],[0,0,0,1]})})
                Mapping.Assign(uniforms[name],PrrUniformValue.MatrixRows(type,rows),"HE1",name,"Explicit row-layout fixture","fixture");
            Result=new(){MaterialFiles=new(){[("typed","opaque")]="typed.tres"},Materials=[],Destination=new(null,Directory,false)};
            byte[] pixels=[16,32,64,128,1,2,3,4,5,6,7,8,9,10,11,12];
            Result.Audits.Add(new(){Material="typed",Slot="opaque",SourceShader="typed",PrrShader="typed.shader",OutputType="ShaderMaterial",Mapping=Mapping,
                Textures=[new(){Source=new("source","map",0,0,0,null),Sampler="map",Status="mapped",Width=2,Height=2,Flags=6,Pixels=pixels}]});
            File.WriteAllText(Path.Combine(Directory,"typed.shader"),code);
            File.WriteAllText(Path.Combine(Directory,"typed.tres"),"[gd_resource type=\"ShaderMaterial\" load_steps=3 format=2]\n"+
                "[ext_resource path=\"typed.shader\" type=\"Shader\" id=1]\n[ext_resource path=\"texture.tres\" type=\"Texture\" id=2]\n"+
                "[resource]\nresource_name=\"typed\"\nshader=ExtResource(1)\nshader_param/map=ExtResource(2)\n"+
                string.Join("\n",Mapping.Values.Select(p=>"shader_param/"+p.Key+" = "+p.Value)));
            File.WriteAllText(Path.Combine(Directory,"texture.tres"),"[gd_resource type=\"ImageTexture\" load_steps=2 format=2]\n[sub_resource type=\"Image\" id=1]\n"+
                "data={\"width\":2,\"height\":2,\"format\":\"RGBA8\",\"mipmaps\":false,\"data\":PoolByteArray( "+string.Join(", ",pixels)+" )}\n"+
                "[resource]\nimage=SubResource(1)\nflags=6\n");
            File.WriteAllText(Path.Combine(Directory,"project.godot"),"config_version=4\n[rendering]\nquality/driver/driver_name=\"GLES3\"\n");
            Result.WriteExpected(Directory);
            functions=(string)typeof(ConversionService).Assembly.GetType("HE1ToSCU.Core.MaterialResourceValidation")!
                .GetField("Functions",BindingFlags.Static|BindingFlags.Public)!.GetRawConstantValue()!;
        }
        public async Task Run(string stage,bool failure)
        {
            string script="extends SceneTree\nfunc fail(message):\n    printerr(\"PRR_TEST_FAILED \"+message)\n    quit()\n"+functions+
                "\nfunc _init():\n    load_prr_expectations(\""+stage+"\")\n    var material=load(\"res://typed."+(stage=="text"?"tres":"res")+"\")\n"+
                "    if material == null:\n        fail(\"load\")\n        return\n    if not check_prr_material(material):\n        return\n"+
                (stage=="text"?"    clear_material_paths(material)\n    if ResourceSaver.save(\"res://typed.res\",material,65) != OK:\n        fail(\"save\")\n        return\n":"")+
                "    save_prr_readbacks()\n    print(\"PRR_TEST_OK\")\n    quit()\n";
            File.WriteAllText(Path.Combine(Directory,"verify.gd"),script);
            var info=new ProcessStartInfo(AcornFactAttribute.Engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"--no-window","--path",Directory,"--script","verify.gd"})info.ArgumentList.Add(arg);
            using var process=Process.Start(info)!;
            var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
            string text=await output+await errors;
            Assert.DoesNotContain("SCRIPT ERROR",text);
            if(failure){Assert.Contains("PRR_TEST_FAILED",text);Assert.DoesNotContain("PRR_TEST_OK",text);}
            else {Assert.Contains("PRR_TEST_OK",text);Assert.DoesNotContain("ERROR:",text);Assert.DoesNotContain("PRR_TEST_FAILED",text);}
        }
        public void Dispose()=>System.IO.Directory.Delete(Directory,true);
    }
}
