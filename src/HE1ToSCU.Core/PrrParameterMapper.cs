namespace HE1ToSCU.Core;

public static class PrrParameterMapper
{
    private sealed record Binding(string Name, string Lanes, double Scale, string Evidence);
    // HedgeLib in_add_to_hl_material uses as_vec3() for these material colors.
    // Original eye pixel shaders likewise consume HighLightColor.xyz, never W.
    private static readonly HashSet<string> RgbInputs=new(new[]{"diffuse","ambient","specular","emissive","g_SonicEyeHighLightColor"},StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (string Target,string Lanes)> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["diffuse"]=("diffuse_color","xyzw"), ["specular"]=("specular_color","xyzw"),
        ["g_SonicSkinFalloffParam"]=("sonic_skin_falloff_param","xyzw"),
        ["g_SonicEyeHighLightPosition"]=("eye_highlight_position","xyz"),
        ["g_SonicEyeHighLightColor"]=("eye_highlight_color","xyzw"),
        ["g_NPCClothParam"]=("cloth_transmission","xyz"), ["mrgFresnelParam"]=("sonic_fresnel_param","xy"),
        ["mrgLuminanceRange"]=("luminance_range","x"), ["mrgInShadowScale"]=("environment_shadow_scale","xz"),
        ["opacity_reflection_refraction_spectype"]=("material_opacity","x"),
        ["mrgEyeLight_Range"]=("eye_light_range","zw"), ["mrgEyeLight_Attribute"]=("eye_light_attribute","xyzw"),
        ["g_ForceAlphaColor"]=("force_alpha_color","xyzw"), ["g_GI1Scale"]=("gi_saturation","w"),
        ["g_IgnoreLightParam"]=("depth_fade","xy"),
        ["mrgEdgeEmissionParam"]=("edge_emission_color","xyzw"),
        ["g_FalloffColor0"]=("falloff_color_near","xyzw"), ["g_FalloffColor1"]=("falloff_color_edge","xyzw"),
        ["g_FalloffParam"]=("falloff_param","xy"), ["g_RefractionParam"]=("refraction_param","xyz"),
        ["g_NoiseScrollScale"]=("noise_scroll_scale","xyzw"), ["g_NoisePower"]=("noise_power","xy"),
        // NightEnemy PS: masked diffuse uses Z, authored rim uses Y (not XY).
        ["mrgEmissionPower"]=("emission_power","zy")
    };
    private static readonly HashSet<string> KnownInputs = new(Aliases.Keys.Concat(new[]
        {"ambient","emissive","mrgChrEmissionParam","g_GI0Scale","power_gloss_level","sky_color","ground_color"}),StringComparer.OrdinalIgnoreCase);
    public static string F(float value) => PrrUniformValue.Number(value);

    public static PrrMappingResult Map(He1Material source,PrrShader shader)
    {
        var result=new PrrMappingResult();
        var parameters=source.AllParameters.ToArray();
        var uniforms=shader.Uniforms.ToDictionary(u=>u.Name,StringComparer.Ordinal);
        var duplicates=parameters.GroupBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1)
            .Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var parameter in parameters)
        {
            string name=parameter.Name;
            var candidates=shader.Uniforms.Where(u=>u.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
            var bindings=new List<Binding>();
            bool exact=candidates.Length==1;
            if(exact) bindings.Add(new(candidates[0].Name,"xyzw",1,"Exact uniform name and declared vector arity; scalar X accepted only with unoccupied other lanes."));
            else if(candidates.Length>1)
            {Record("ambiguous","Multiple case-insensitive uniforms match.",[],"Uniform declarations","unresolved");continue;}
            else if(name.Equals("ambient",StringComparison.OrdinalIgnoreCase))
                Add(uniforms.ContainsKey("emission_ambient")?"emission_ambient":"ambient_light_color","xyzw",1,"PRR adapters and original g_Ambient usage; emission-specific ambient takes precedence where declared.");
            else if(name.Equals("emissive",StringComparison.OrdinalIgnoreCase))
            {
                // Dedicated enemy emission and ordinary g_Emissive are different source registers.
                if(uniforms.GetValueOrDefault("material_emission")?.Type=="vec3")
                    Add("material_emission","xyz",1,"Luminescence source g_Emissive RGB; enemy vec4 emission instead uses mrgChrEmissionParam.");
            }
            else if(name.Equals("mrgChrEmissionParam",StringComparison.OrdinalIgnoreCase))
            {
                if(uniforms.GetValueOrDefault("material_emission")?.Type=="vec4")
                    Add("material_emission","xyzw",1,"Enemy/IgnoreLight source adds mrgChrEmissionParam.rgb and multiplies by its W; not g_Emissive.");
            }
            else if(name.Equals("g_GI0Scale",StringComparison.OrdinalIgnoreCase))
            {
                Add("gi_rgb_scale","xyz",1,"Source g_GI0Scale.xyz is the GI RGB decode scale; unconsumed W remains in the raw audit.");
                Add("ambient_shadow_scale","w",1,"PRR bind_baked_gi: source g_GI0Scale.w.");
            }
            else if(Aliases.TryGetValue(name,out var alias))
                Add(alias.Target,alias.Lanes,alias.Target == "eye_light_range"?Spatial.Scale:1,
                    alias.Target == "eye_highlight_position"
                        ? "Authored HE1 eye-highlight XYZ preserved directly for PRR; material tuning is not multiplied by mesh export scale."
                        : "Release parameter reference and original matching HLSL consumed components; lanes "+alias.Lanes+
                          (alias.Target == "eye_light_range"?" with model positional scale":""));
            if(bindings.Count==0)
            {
                bool reserved=name.StartsWith("<unnamed",StringComparison.Ordinal)||!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[A-Za-z_]\w*$");
                Record(reserved||KnownInputs.Contains(name)?"intentionally_unused":"missing_mapping",
                    reserved?"Unnamed/nonidentifier source slot has no shader address; raw payload retained.":
                    KnownInputs.Contains(name)?"The selected shader does not consume this material semantic.":"No verified destination binding.",[],"Selected shader declarations and semantic adapters",reserved||KnownInputs.Contains(name)?"confirmed":"unresolved");
                continue;
            }
            if(duplicates.Contains(name)) {Record("ambiguous","Duplicate source parameter name; no last-value-wins mapping.",[],"Mirage parameter tables","confirmed");continue;}
            if(parameter.Values.Length!=1 || bindings.Any(b=>uniforms[b.Name].ArrayDeclaration!=null || uniforms[b.Name].Type.StartsWith("mat",StringComparison.Ordinal)))
            {Record("unsupported","Multi-vector/array or matrix input requires an established layout binding; Godot 3.1 uniform arrays are unsupported. No elements discarded.",[],"Raw count and destination type","confirmed");continue;}
            var destinations=new List<string>();
            try
            {
                var planned=new List<(PrrUniform Uniform,PrrUniformValue Value,Binding Binding)>();
                foreach(var binding in bindings)
                {
                    var uniform=uniforms[binding.Name];
                    if(exact && (uniform.Type is "float" or "int" or "bool") && parameter.Values[0].Skip(1).Any(v=>v!=0))
                        throw new NotSupportedException("Packed scalar input has occupied Y/Z/W lanes but no verified component rule.");
                    var row=parameter.Values[0].Select(v=>v*binding.Scale).ToArray();
                    if(uniform.IsColor && RgbInputs.Contains(name))row[3]=1;
                    var value=PrrUniformValue.Convert(uniform,row,binding.Lanes,parameter.Storage);
                    if(result.Values.ContainsKey(uniform.Name))throw new InvalidDataException("Multiple source parameters target "+uniform.Name);
                    planned.Add((uniform,value,binding));
                }
                foreach(var (uniform,value,binding) in planned)
                {
                    bool rgb=uniform.IsColor&&RgbInputs.Contains(name);
                    result.Assign(uniform,value,"HE1",name,binding.Evidence+(rgb?
                        "; source is RGB (HedgeLib as_vec3 / original eye PS .xyz); Color alpha is 1, source W retained in raw audit, opacity is separate":""),exact?"exact_name":"source_verified_alias");
                    destinations.Add(uniform.Name);
                    int count=value.Components.Length;
                    result.Mappings[name]=(result.Mappings.TryGetValue(name,out var old)?old+"; ":"")+uniform.Name+"."+(rgb?"xyz; Color alpha=1":binding.Lanes[..count])+(binding.Scale!=1?" * model position scale":"");
                }
                Record("mapped","Explicit source lanes mapped; remaining lanes retained as raw source data.",destinations.ToArray(),string.Join("; ",bindings.Select(b=>b.Evidence)),exact?"exact_name":"source_verified_alias");
            }
            catch(NotSupportedException e) {Record("unsupported",e.Message,[],"Type/component contract","confirmed");}
            catch(InvalidDataException e) {Record(result.Assignments.Any(a=>a.Name==bindings[0].Name)?"ambiguous":"failed_conversion",e.Message,[],"Type/component contract","confirmed");}
            void Add(string target,string lanes,double scale,string evidence) {if(uniforms.ContainsKey(target))bindings.Add(new(target,lanes,scale,evidence));}
            void Record(string classification,string reason,string[] targets,string evidence,string confidence) =>
                result.Parameters.Add(new(name,parameter.Storage,parameter.Values,classification,reason,targets,evidence,confidence));
        }
        foreach(var (name,value) in new[]{("sky_color",.5), ("ground_color",.25)})
            if(uniforms.TryGetValue(name,out var uniform)&&!result.Values.ContainsKey(name))
                result.Assign(uniform,PrrUniformValue.Convert(uniform,[value,value,value,1]),"ConverterDefault",null,
                    "User-requested PRR environment default; authored exact-name source values take precedence.","explicit_user_requirement");
        if(uniforms.TryGetValue("alpha_cutoff",out var alpha)&&!result.Values.ContainsKey(alpha.Name))
            result.Assign(alpha,PrrUniformValue.Convert(alpha,[source.AlphaThreshold,0,0,0]),"MaterialHeader","alphaThreshold",
                "Mirage alphaThreshold byte / 255.","format_verified");
        return result;
    }
}
