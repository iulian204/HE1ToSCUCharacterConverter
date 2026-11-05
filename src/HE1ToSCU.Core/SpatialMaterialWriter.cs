using System.Globalization;
using System.Numerics;
using System.Text;

namespace HE1ToSCU.Core;

/// <summary>Approximation of HE1 material inputs using built-in Godot 3 SpatialMaterial.
/// ChrSkin: gloss exponent=clamp(power_gloss_level.y*500,1,1024), intensity=z*5;
/// specular texture RGB modulates intensity; its alpha is not a gloss channel.</summary>
public sealed class SpatialMaterialWriter
{
    private readonly Dictionary<string, string> exported = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DdsImage> decoded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> outputOwners = new(StringComparer.OrdinalIgnoreCase);
    private static string F(float value) => value.ToString("G9", CultureInfo.InvariantCulture).Replace('E', 'e');
    private static string Color(Vector4 c) => $"Color( {F(c.X)}, {F(c.Y)}, {F(c.Z)}, {F(c.W)} )";
    private static float Luminance(Vector4 c) => .2126f * c.X + .7152f * c.Y + .0722f * c.Z;
    public static float Roughness(float exponent) => SpecularApproximation.PeakRoughness(exponent);

    public Dictionary<(string Material, string Slot), string> Write(ModelAsset model, string directory, Action<string>? log, CancellationToken token,SourceGame game=SourceGame.SonicGenerations)
    {
        Directory.CreateDirectory(Path.Combine(directory, "materials"));
        Directory.CreateDirectory(Path.Combine(directory, "textures"));
        var result = new Dictionary<(string, string), string>();
        foreach (var key in model.AllSurfaces.Select(s => (s.Material, s.Slot)).Distinct())
        {
            token.ThrowIfCancellationRequested();
            He1Material? source = model.Materials?.GetValueOrDefault(key.Material);
            string file = "materials/" + key.Material + (result.Keys.Any(k => k.Item1 == key.Material) ? "_" + key.Slot : "") + ".tres";
            if (Path.GetFileName(key.Material) != key.Material || key.Material.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("Invalid source material filename.");
            result.Add(key, file);
            var bindings = new Dictionary<string, string>();
            string? shader = source?.Shader;
            bool generations = game == SourceGame.SonicGenerations;
            bool Family(string name) => MaterialAppearance.ShaderFamily(shader, name);
            string variant = (shader?.Split('_', 3).ElementAtOrDefault(1) ?? "").Split('@', 2)[0];
            He1Texture? Find(string type) => source?.Textures.FirstOrDefault(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            DdsImage? Load(He1Texture? texture, bool supportsUv2 = false, bool optionalFormat = false)
            {
                if (texture == null) return null;
                if (texture.SourcePath == null) { log?.Invoke($"Missing texture {texture.Name} referenced by {key.Material}; using its material parameters."); return null; }
                if (texture.UvIndex != 0 && !(supportsUv2 && texture.UvIndex == 1))
                    log?.Invoke($"{key.Material}/{texture.Type}: source UV index {texture.UvIndex} is unsupported for this slot; using primary UVs.");
                try
                {
                    if (!decoded.TryGetValue(texture.SourcePath, out var image)) decoded[texture.SourcePath] = image = DdsImage.Read(texture.SourcePath);
                    return image;
                }
                catch (NotSupportedException ex) when (optionalFormat)
                {
                    log?.Invoke($"{key.Material}/{texture.Type}: skipped optional texture {texture.Name}. {ex.Message}");
                    return null;
                }
            }
            string Texture(string name, string identity, DdsImage image, bool srgb, He1Texture? reference)
            {
                int flags = 1 | 4 | (srgb ? 16 : 0);
                if (reference == null || (reference.WrapU != 2 && reference.WrapV != 2)) flags |= 2;
                if (reference is { WrapU: 1, WrapV: 1 }) flags |= 32;
                string cache = identity + "|" + flags;
                if (exported.TryGetValue(cache, out var existing)) return existing;
                string relative = "textures/" + name + ".texture.tres";
                if (outputOwners.TryGetValue(relative, out var owner) && owner != cache)
                    relative = "textures/" + name + "_" + flags + ".texture.tres";
                if (outputOwners.TryGetValue(relative, out owner) && owner != cache) throw new InvalidDataException($"Texture output name collision: {relative}.");
                outputOwners[relative] = cache;
                byte[] mipData = Mipmaps(image, token);
                using var writer = new StreamWriter(Path.Combine(directory, relative), false, new UTF8Encoding(false));
                writer.Write($"[gd_resource type=\"ImageTexture\" load_steps=2 format=2]\n\n[sub_resource type=\"Image\" id=1]\ndata = {{\n\"width\": {image.Width},\n\"height\": {image.Height},\n\"format\": \"RGBA8\",\n\"mipmaps\": true,\n\"data\": PoolByteArray( ");
                for (int i = 0; i < mipData.Length; i++) { if (i > 0) writer.Write(", "); writer.Write(mipData[i].ToString(CultureInfo.InvariantCulture)); }
                writer.Write($" )\n}}\n\n[resource]\nresource_name = {ExportNames.Literal(name)}\nflags = {flags}\nimage = SubResource( 1 )\n");
                exported[cache] = relative; return relative;
            }
            string Stem(He1Texture texture) => Path.GetFileNameWithoutExtension(texture.Name.Replace('\\', '/'));
            var diffuse = MaterialAppearance.Diffuse(source,game);
            var opacity = source?.Parameter("opacity_reflection_refraction_spectype", Vector4.One).X ?? 1;
            // The supplied materials store diffuse.w=0; treat that as default opacity
            // for this approximation rather than making every opaque surface invisible.
            diffuse.W = Math.Clamp((diffuse.W > 0 ? diffuse.W : 1) * opacity, 0, 1);
            bool ignoreLightTwice = generations && Family("IgnoreLightTwice");
            bool unshaded = Family("IgnoreLight") || ignoreLightTwice;
            var albedoRef = Find("diffuse"); var albedo = Load(albedoRef);
            // Some effects store a zero diffuse tint and a nonzero effect
            // color. Use the latter as a standalone tint when the texture exists.
            if (unshaded && albedo != null && diffuse.X == 0 && diffuse.Y == 0 && diffuse.Z == 0)
            {
                var effectColor = source!.Parameter("emissive", Vector4.Zero);
                diffuse = effectColor with { W = diffuse.W };
            }
            if (ignoreLightTwice) diffuse *= new Vector4(2, 2, 2, 1);
            var specular = source?.Parameter("specular", Vector4.One) ?? Vector4.One;
            var gloss = source?.Parameter("power_gloss_level", new Vector4(50, .1f, .1f, 0)) ?? new Vector4(50, .1f, .1f, 0);
            float roughness = Roughness(SpecularApproximation.Power(gloss.Y));
            var glossRef = Find("gloss") ?? Find("power") ?? source?.Textures.FirstOrDefault(t =>
                System.Text.RegularExpressions.Regex.IsMatch(Stem(t), @"_pow(?:_hd)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            var specRef = Find("specular");
            if (specRef == glossRef) specRef = null;
            var specMap = Load(specRef); var glossMap = Load(glossRef);
            float metallic = MaterialAppearance.Metalness(source, game);
            bool glass = generations && Family("EnmGlass");
            bool enemyEmission = generations && (Family("EnmEmission") || Family("EnmIgnore"));
            var glassMatcap = glass ? Load(Find("reflection"), optionalFormat: true) : null;
            int glassAlpha = glassMatcap == null ? 0 : MeanMatcapAlpha(glassMatcap, token);
            // These enemy programs derive coverage from diffuse/matcap alpha.
            // A legacy opacity entry may alias the diffuse map, as on Silver.
            var opacityRef = enemyEmission || glass ? null : Find("opacity");
            var opacityMap = Load(opacityRef);
            bool bakedMetal = false;
            if (albedo != null && albedoRef != null)
            {
                if (opacityMap != null || glassAlpha > 0)
                {
                    byte[] pixels = albedo.Pixels.ToArray();
                    for (int y = 0; y < albedo.Height; y++) for (int x = 0; x < albedo.Width; x++)
                    {
                        int p = (y * albedo.Width + x) * 4;
                        int alpha = Math.Min(255, pixels[p + 3] + glassAlpha);
                        if (opacityMap != null)
                        {
                            int sample = opacityMap.ResampleOffset(x, y, albedo.Width, albedo.Height);
                            alpha = alpha * opacityMap.Pixels[sample] / 255;
                        }
                        pixels[p + 3] = (byte)alpha;
                    }
                    albedo = albedo with { Pixels = pixels };
                }
                if (metallic > 0 && albedoRef.UvIndex == 0 && specMap != null && MatchesSampling(albedoRef, specRef) &&
                    (glossMap == null || MatchesSampling(albedoRef, glossRef)))
                {
                    var bake = MetalFinishBake.Bake(albedo, specMap, glossMap, diffuse, specular, gloss, token);
                    bindings["albedo_texture"] = Texture(key.Material + "_metal_base", key.Material + "|metal-base", bake.Albedo, true, albedoRef);
                    bindings["metallic_texture"] = Texture(key.Material + "_metalness", key.Material + "|metalness", bake.Metalness, false, albedoRef);
                    diffuse = Vector4.One with { W = diffuse.W };
                    metallic = 1;
                    bakedMetal = true;
                }
                else
                {
                    string suffix = (opacityMap != null ? "_opacity" : "") + (glassAlpha > 0 ? "_glass" : "");
                    string name = glassAlpha > 0 ? key.Material : Stem(albedoRef);
                    bindings["albedo_texture"] = Texture(name + suffix, albedoRef.SourcePath + "|albedo|" + opacityRef?.SourcePath + (glassAlpha > 0 ? "|glass|" + glassAlpha : ""), albedo, true, albedoRef);
                }
            }
            var normalRef = Find("normal"); var normal = Load(normalRef, supportsUv2: true);
            if (normal != null && normalRef != null)
            {
                bool eye = MaterialAppearance.EyeShader(source!.Shader);
                byte[] pixels = normal.Pixels.ToArray();
                for (int p = 0; p < pixels.Length; p += 4)
                {
                    // HE1 DXT5nm uses R*A for X; ChrEye stores the surface normal in B/A.
                    float x = eye ? normal.Pixels[p + 2] / 255f : normal.Pixels[p] * normal.Pixels[p + 3] / 65025f;
                    float y = eye ? normal.Pixels[p + 3] / 255f : normal.Pixels[p + 1] / 255f;
                    pixels[p] = (byte)Math.Clamp((int)MathF.Round(x * 255), 0, 255);
                    pixels[p + 1] = (byte)Math.Clamp((int)MathF.Round((1 - y) * 255), 0, 255);
                    pixels[p + 2] = (byte)Math.Clamp((int)MathF.Round((MathF.Sqrt(Math.Max(0, 1 - MathF.Pow(x * 2 - 1, 2) - MathF.Pow(y * 2 - 1, 2))) * .5f + .5f) * 255), 0, 255);
                    pixels[p + 3] = 255;
                }
                // The ordinary normal slot uses UV1. Godot's detail normal can
                // use UV2 while the albedo still samples its original UV1.
                string slot = normalRef.UvIndex == 1 ? "detail_normal" : "normal_texture";
                bindings[slot] = Texture(Stem(normalRef), normalRef.SourcePath + "|normal|" + eye, normal with { Pixels = pixels }, false, normalRef);
            }
            int width = Math.Max(specMap?.Width ?? 1, glossMap?.Width ?? 1), height = Math.Max(specMap?.Height ?? 1, glossMap?.Height ?? 1);
            if ((long)width*height > 16_777_216) throw new InvalidDataException("Specular bake exceeds the texture size limit.");
            var keys = new int[width*height];var histogram = new Dictionary<int,int>();
            int glossChannel = MaterialAppearance.GlossChannel(shader, game);
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
                byte g = glossMap == null ? (byte)255 : glossMap.Pixels[glossMap.ResampleOffset(x,y,width,height)+glossChannel];
                byte mask=255;
                if(specMap!=null) {int p=specMap.ResampleOffset(x,y,width,height);mask=(byte)Math.Clamp((int)MathF.Round(.2126f*specMap.Pixels[p]+.7152f*specMap.Pixels[p+1]+.0722f*specMap.Pixels[p+2]),0,255);}
                int bakeKey=(g<<8)|mask;keys[y*width+x]=bakeKey;histogram[bakeKey]=histogram.GetValueOrDefault(bakeKey)+1;
            }
            var samples=histogram.Select(p=>new SpecularSample((byte)(p.Key>>8),(byte)p.Key,p.Value)).ToArray();
            float specularTint=Luminance(specular)*MaterialAppearance.SkinFresnelScale(source);
            var fit=SpecularApproximation.Fit(gloss.Y,gloss.Z,specularTint,samples,game);
            var highlight=new HighlightFit(0,0,0);
            if (!bakedMetal && metallic > 0)
            {
                // Fit the remaining direct lobe after accounting for the metal's
                // colored GGX reflection. Cap the target to a non-mirror F0 of .64.
                var baseline = fit with { Specular = MaterialAppearance.EquivalentMetalSpecular(fit.Specular, metallic, diffuse, albedo) };
                var target = SpecularApproximation.Fit(gloss.Y, gloss.Z, specularTint, samples, game, maximumSpecular: 2);
                highlight = HighlightApproximation.Fit(baseline, target, samples);
            }
            else if(!bakedMetal && MaterialAppearance.ReflectiveFinish(source)) {
                var target=SpecularApproximation.Fit(gloss.Y,gloss.Z,specularTint,samples,game,MaterialAppearance.MaximumSpecular(source),MaterialAppearance.IntensityScale(source,game));
                highlight=HighlightApproximation.Fit(fit,target,samples);
            }
            float scalarSpecular = bakedMetal ? MetalFinishBake.DielectricSpecular : fit.Specular;
            if(glossMap!=null) {
                var pixels=new byte[width*height*4];
                for(int i=0;i<keys.Length;i++) {pixels[i*4]=pixels[i*4+1]=pixels[i*4+2]=fit.Roughness[keys[i]];pixels[i*4+3]=255;}
                bindings["roughness_texture"]=Texture(key.Material+"_roughness",key.Material+"|fitted-roughness",new DdsImage(width,height,pixels),false,glossRef??specRef);
            } else roughness=fit.Roughness[keys[0]]/255f;
            log?.Invoke($"{key.Material}: GGX angular fit specular={scalarSpecular:G4}, integrated error {fit.InitialError:G5} -> {fit.Error:G5}, gloss map={(glossRef?.Name ?? "none")}.");
            bool ignoreEmission = generations && Family("IgnoreLight") && variant.Contains('E');
            bool luminescenceMap = Family("Luminescence") && variant.Contains('E');
            var emissive = unshaded ? Vector4.Zero : source?.Parameter("emissive", Vector4.Zero) ?? Vector4.Zero;
            var emissionRef = unshaded && !ignoreEmission ? null : Find("emission") ?? Find("emissive");
            bool inferredEmission = emissionRef == null &&
                (((enemyEmission || glass) && variant.Contains('l')) || ignoreEmission || luminescenceMap);
            if (inferredEmission) emissionRef = Find("displacement");
            var emissionMap = Load(emissionRef, supportsUv2: true, optionalFormat: inferredEmission);
            float emissionEnergy = 1;
            Vector4 emissionMapGain = Vector4.One;
            bool authoredEmission = enemyEmission || ignoreEmission || glass;
            if (authoredEmission)
            {
                var param = ignoreEmission
                    ? source!.Parameter("g_EmissionParam", new Vector4(0, 0, 0, 1))
                    : source!.Parameter("mrgChrEmissionParam", Vector4.Zero);
                emissionMapGain = Vector4.Max(Vector4.Zero, source.Parameter("ambient", Vector4.One)) * Math.Max(0, param.W);
                var sourceEmission = param * emissionMapGain;
                emissive = glass ? emissive + sourceEmission : sourceEmission;
            }
            else if (generations && luminescenceMap)
                emissionMapGain = Vector4.Max(Vector4.Zero, source!.Parameter("ambient", Vector4.One));

            if (emissionMap != null && emissionRef != null)
            {
                if (authoredEmission || luminescenceMap)
                {
                    float maxGain = Math.Max(emissionMapGain.X, Math.Max(emissionMapGain.Y, emissionMapGain.Z));
                    if (maxGain > 0)
                    {
                        // The source clamps the final lit output. Bound its
                        // emission contribution here, where SpatialMaterial
                        // still exposes the authored constant plus texture.
                        var offset = authoredEmission ? emissive : Vector4.Zero;
                        var peak = offset + emissionMapGain;
                        float maxEmission = Math.Max(peak.X, Math.Max(peak.Y, peak.Z));
                        emissionEnergy = Math.Clamp(maxEmission, 1, 4);
                        emissive = authoredEmission ? Vector4.Zero : emissive / emissionEnergy;
                        var image = BakeEmission(emissionMap, emissionMapGain, offset, emissionEnergy, token);
                        bindings["emission_texture"] = Texture(key.Material + "_emission", key.Material + "|emission-tinted", image, true, emissionRef);
                    }
                }
                else
                {
                    bindings["emission_texture"] = Texture(Stem(emissionRef), emissionRef.SourcePath + "|emission", emissionMap, true, emissionRef);
                    if (new Vector3(emissive.X, emissive.Y, emissive.Z).LengthSquared() == 0) emissive = Vector4.One;
                }
            }
            if (authoredEmission) emissive = Vector4.Min(emissive, new Vector4(4));
            var depthRef = Family("Parallax") ? Find("height") ?? Find("displacement") : null;
            if (depthRef is { UvIndex: 0 } && normalRef?.UvIndex != 1 && Load(depthRef, optionalFormat: true) is { } depth)
                bindings["depth_texture"] = Texture(Stem(depthRef) + "_height", depthRef.SourcePath + "|depth", depth, false, depthRef);
            var falloff = source?.Parameter("g_SonicSkinFalloffParam", Vector4.Zero) ?? Vector4.Zero;
            bool skin = source?.Shader.StartsWith("ChrSkin", StringComparison.OrdinalIgnoreCase)==true || source?.Shader.StartsWith("SonicSkin", StringComparison.OrdinalIgnoreCase)==true;
            var rimRef = Find("displacement"); var rimMap = skin ? Load(rimRef) : null;
            if (rimMap != null && rimRef != null) bindings["rim_texture"] = Texture(Stem(rimRef), rimRef.SourcePath + "|rim", rimMap, false, rimRef);
            var text = new StringBuilder($"[gd_resource type=\"SpatialMaterial\" load_steps={bindings.Count + 1} format=2]\n\n");
            int id = 1; foreach (var binding in bindings) text.AppendLine($"[ext_resource path={ExportNames.Literal("../" + binding.Value)} type=\"Texture\" id={id++}]");
            text.AppendLine($"\n[resource]\nresource_name = {ExportNames.Literal(key.Material)}\nalbedo_color = {Color(diffuse)}\nparams_specular_mode = 0\nmetallic = {F(metallic)}\nmetallic_specular = {F(scalarSpecular)}\nroughness = {F(bindings.ContainsKey("roughness_texture") ? 1 : roughness)}\nvertex_color_use_as_albedo = true\nvertex_color_is_srgb = false");
            if (source?.DoubleSided == true) text.AppendLine("params_cull_mode = 2");
            if (unshaded) text.AppendLine("flags_unshaded = true");
            if (key.Slot == "transparent" || diffuse.W < .999f || source?.Additive == true) text.AppendLine("flags_transparent = true");
            if (source?.Additive == true) text.AppendLine("params_blend_mode = 1");
            if (key.Slot == "punch") text.AppendLine($"params_use_alpha_scissor = true\nparams_alpha_scissor_threshold = {F(source?.AlphaThreshold ?? .5f)}");
            if (bindings.ContainsKey("normal_texture")) text.AppendLine("normal_enabled = true\nnormal_scale = 1");
            // Multiply by the default white detail albedo so this layer only affects normals.
            if (bindings.ContainsKey("detail_normal")) text.AppendLine("detail_enabled = true\ndetail_uv_layer = 1\ndetail_blend_mode = 3");
            if (bindings.ContainsKey("depth_texture")) text.AppendLine("depth_enabled = true\ndepth_scale = -0.03");
            if (highlight.Strength>0) text.AppendLine($"clearcoat_enabled = true\nclearcoat = {F(highlight.Strength)}\nclearcoat_gloss = {F(highlight.Gloss)}");
            bool hasEmissionTexture = bindings.ContainsKey("emission_texture");
            if (hasEmissionTexture || new Vector3(emissive.X, emissive.Y, emissive.Z).LengthSquared() > 0)
                text.AppendLine($"emission_enabled = true\nemission = {Color(emissive with { W = 1 })}\nemission_energy = {F(emissionEnergy)}");
            if (hasEmissionTexture && emissionRef?.UvIndex == 1) text.AppendLine("emission_on_uv2 = true");
            if (falloff.Y > 0 && rimMap != null) text.AppendLine($"rim_enabled = true\nrim = {F(Math.Clamp(falloff.Y / 5, 0, 1))}\nrim_tint = 1");
            id = 1; foreach (var binding in bindings) text.AppendLine($"{binding.Key} = ExtResource( {id++} )");
            File.WriteAllText(Path.Combine(directory, file), text.ToString(), new UTF8Encoding(false));
            if (source != null)
            {
                log?.Invoke($"Material {key.Material}: {source.Shader} -> SpatialMaterial, {bindings.Count} texture bindings.");
                foreach (var texture in source.Textures.Where(t => t.Type.Equals("reflection", StringComparison.OrdinalIgnoreCase)))
                    log?.Invoke($"{key.Material}: reflection/matcap texture {texture.Name} has no direct SpatialMaterial slot; environment lighting supplies reflections.");
            }
        }
        return result;
    }

    private static bool MatchesSampling(He1Texture a, He1Texture? b) =>
        b != null && a.UvIndex == b.UvIndex && a.WrapU == b.WrapU && a.WrapV == b.WrapV;

    private static DdsImage BakeEmission(DdsImage source, Vector4 tint, Vector4 offset, float energy, CancellationToken token)
    {
        var pixels = source.Pixels.ToArray();
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
            pixels[i] = MaterialAppearance.EncodeSrgb((MaterialAppearance.LinearSrgb[pixels[i]] * tint.X + offset.X) / energy);
            pixels[i + 1] = MaterialAppearance.EncodeSrgb((MaterialAppearance.LinearSrgb[pixels[i + 1]] * tint.Y + offset.Y) / energy);
            pixels[i + 2] = MaterialAppearance.EncodeSrgb((MaterialAppearance.LinearSrgb[pixels[i + 2]] * tint.Z + offset.Z) / energy);
        }
        return source with { Pixels = pixels };
    }

    private static int MeanMatcapAlpha(DdsImage image, CancellationToken token)
    {
        // EnmGlass adds view-normal matcap alpha to diffuse alpha. Average
        // across the visible disc with a cosine weight for front-facing normals.
        double sum = 0, weight = 0;
        for (int y = 0; y < image.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            double v = (2 * y + 1.0) / image.Height - 1;
            for (int x = 0; x < image.Width; x++)
            {
                double u = (2 * x + 1.0) / image.Width - 1;
                double cosine = Math.Sqrt(Math.Max(0, 1 - u * u - v * v));
                weight += cosine;
                sum += cosine * image.Pixels[(y * image.Width + x) * 4 + 3];
            }
        }
        return (int)Math.Round(sum / weight);
    }

    private static byte[] Mipmaps(DdsImage image, CancellationToken token)
    {
        using var stream = new MemoryStream(); byte[] current = image.Pixels; int width = image.Width, height = image.Height;
        while (true)
        {
            token.ThrowIfCancellationRequested(); stream.Write(current);
            if (width == 1 && height == 1) break;
            int nw = Math.Max(1, width / 2), nh = Math.Max(1, height / 2); var next = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) for (int channel = 0; channel < 4; channel++)
            {
                int sum = 0, count = 0;
                for (int dy = 0; dy < 2 && y * 2 + dy < height; dy++) for (int dx = 0; dx < 2 && x * 2 + dx < width; dx++) { sum += current[((y * 2 + dy) * width + x * 2 + dx) * 4 + channel]; count++; }
                next[(y * nw + x) * 4 + channel] = (byte)(sum / count);
            }
            current = next; width = nw; height = nh;
        }
        return stream.ToArray();
    }
}
