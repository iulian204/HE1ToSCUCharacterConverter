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
    public static float Roughness(float exponent) => Math.Clamp(MathF.Pow(2 / (Math.Clamp(exponent, 1, 1024) + 2), .25f), .04f, 1);

    public Dictionary<(string Material, string Slot), string> Write(ModelAsset model, string directory, Action<string>? log, CancellationToken token)
    {
        Directory.CreateDirectory(Path.Combine(directory, "materials"));
        Directory.CreateDirectory(Path.Combine(directory, "textures"));
        var result = new Dictionary<(string, string), string>();
        foreach (var key in model.Surfaces.Select(s => (s.Material, s.Slot)).Distinct())
        {
            token.ThrowIfCancellationRequested();
            He1Material? source = model.Materials?.GetValueOrDefault(key.Material);
            string file = "materials/" + key.Material + (result.Keys.Any(k => k.Item1 == key.Material) ? "_" + key.Slot : "") + ".tres";
            if (Path.GetFileName(key.Material) != key.Material || key.Material.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("Invalid source material filename.");
            result.Add(key, file);
            var bindings = new Dictionary<string, string>();
            He1Texture? Find(string type) => source?.Textures.FirstOrDefault(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            DdsImage? Load(He1Texture? texture)
            {
                if (texture == null) return null;
                if (texture.SourcePath == null) { log?.Invoke($"Missing texture {texture.Name} referenced by {key.Material}; using its material parameters."); return null; }
                if (texture.UvIndex != 0) log?.Invoke($"{key.Material}/{texture.Type}: source UV{texture.UvIndex} needs additional UV routing; primary UVs are used by the basic SpatialMaterial.");
                if (!decoded.TryGetValue(texture.SourcePath, out var image)) decoded[texture.SourcePath] = image = DdsImage.Read(texture.SourcePath);
                return image;
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
            var diffuse = source?.Parameter("diffuse", Vector4.One) ?? new Vector4(.72f, .77f, .86f, 1);
            var opacity = source?.Parameter("opacity_reflection_refraction_spectype", Vector4.One).X ?? 1;
            // The supplied materials store diffuse.w=0; treat that as default opacity
            // for this approximation rather than making every opaque surface invisible.
            diffuse.W = Math.Clamp((diffuse.W > 0 ? diffuse.W : 1) * opacity, 0, 1);
            var specular = source?.Parameter("specular", Vector4.One) ?? Vector4.One;
            var gloss = source?.Parameter("power_gloss_level", new Vector4(50, .1f, .1f, 0)) ?? new Vector4(50, .1f, .1f, 0);
            float exponent = Math.Clamp(gloss.Y * 500, 1, 1024), roughness = Roughness(exponent), specularMapAverage = 1;
            var albedoRef = Find("diffuse"); var albedo = Load(albedoRef);
            var opacityMap = Load(Find("opacity"));
            if (albedo != null && albedoRef != null)
            {
                if (opacityMap != null)
                {
                    byte[] pixels = albedo.Pixels.ToArray();
                    for (int y = 0; y < albedo.Height; y++) for (int x = 0; x < albedo.Width; x++)
                    {
                        int p = (y * albedo.Width + x) * 4, sample = ((y * opacityMap.Height / albedo.Height) * opacityMap.Width + x * opacityMap.Width / albedo.Width) * 4;
                        pixels[p + 3] = (byte)(pixels[p + 3] * opacityMap.Pixels[sample] / 255);
                    }
                    albedo = albedo with { Pixels = pixels };
                }
                bindings["albedo_texture"] = Texture(Stem(albedoRef) + (opacityMap != null ? "_opacity" : ""), albedoRef.SourcePath + "|albedo|" + Find("opacity")?.SourcePath, albedo, true, albedoRef);
            }
            var normalRef = Find("normal"); var normal = Load(normalRef);
            if (normal != null && normalRef != null)
            {
                bool eye = source!.Shader.StartsWith("ChrEye", StringComparison.OrdinalIgnoreCase);
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
                bindings["normal_texture"] = Texture(Stem(normalRef), normalRef.SourcePath + "|normal|" + eye, normal with { Pixels = pixels }, false, normalRef);
            }
            var specRef = Find("specular"); var specMap = Load(specRef);
            var glossRef = Find("gloss"); var glossMap = Load(glossRef);
            if (specMap != null)
            {
                double sum = 0;
                for (int p = 0; p < specMap.Pixels.Length; p += 4) sum += .2126 * specMap.Pixels[p] + .7152 * specMap.Pixels[p + 1] + .0722 * specMap.Pixels[p + 2];
                specularMapAverage = (float)(sum / (specMap.Width * specMap.Height * 255));
            }
            var surfaceMap = glossMap ?? specMap;
            if (surfaceMap != null)
            {
                byte[] pixels = new byte[surfaceMap.Pixels.Length];
                for (int p = 0; p < pixels.Length; p += 4)
                {
                    float glossMask = glossMap != null ? glossMap.Pixels[p + (source!.Shader.StartsWith("ChrEye", StringComparison.OrdinalIgnoreCase) ? 1 : 0)] / 255f : 1;
                    float value = Roughness(exponent * glossMask);
                    if (glossMap == null && specMap != null)
                    {
                        float strength = (.2126f * specMap.Pixels[p] + .7152f * specMap.Pixels[p + 1] + .0722f * specMap.Pixels[p + 2]) / 255;
                        value = value + (1 - value) * (1 - strength) * .5f;
                    }
                    pixels[p] = pixels[p + 1] = pixels[p + 2] = (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255); pixels[p + 3] = 255;
                }
                bindings["roughness_texture"] = Texture(key.Material + "_roughness", key.Material + "|roughness", surfaceMap with { Pixels = pixels }, false, glossRef ?? specRef);
            }
            var emissive = source?.Parameter("emissive", Vector4.Zero) ?? Vector4.Zero;
            var emissionRef = Find("emission") ?? Find("emissive"); var emissionMap = Load(emissionRef);
            if (emissionMap != null && emissionRef != null)
            {
                bindings["emission_texture"] = Texture(Stem(emissionRef), emissionRef.SourcePath + "|emission", emissionMap, true, emissionRef);
                if (new Vector3(emissive.X, emissive.Y, emissive.Z).LengthSquared() == 0) emissive = Vector4.One;
            }
            var falloff = source?.Parameter("g_SonicSkinFalloffParam", Vector4.Zero) ?? Vector4.Zero;
            var rimRef = Find("displacement"); var rimMap = source?.Shader.StartsWith("ChrSkin", StringComparison.OrdinalIgnoreCase) == true ? Load(rimRef) : null;
            if (rimMap != null && rimRef != null) bindings["rim_texture"] = Texture(Stem(rimRef), rimRef.SourcePath + "|rim", rimMap, false, rimRef);
            var text = new StringBuilder($"[gd_resource type=\"SpatialMaterial\" load_steps={bindings.Count + 1} format=2]\n\n");
            int id = 1; foreach (var binding in bindings) text.AppendLine($"[ext_resource path={ExportNames.Literal("../" + binding.Value)} type=\"Texture\" id={id++}]");
            text.AppendLine($"\n[resource]\nresource_name = {ExportNames.Literal(key.Material)}\nalbedo_color = {Color(diffuse)}\nmetallic = 0\nmetallic_specular = {F(Math.Clamp(gloss.Z * 5 * Luminance(specular) * specularMapAverage, 0, 1))}\nroughness = {F(bindings.ContainsKey("roughness_texture") ? 1 : roughness)}\nvertex_color_use_as_albedo = true\nvertex_color_is_srgb = false");
            if (source?.DoubleSided == true) text.AppendLine("params_cull_mode = 2");
            if (key.Slot == "transparent" || diffuse.W < .999f || source?.Additive == true) text.AppendLine("flags_transparent = true");
            if (source?.Additive == true) text.AppendLine("params_blend_mode = 1");
            if (key.Slot == "punch") text.AppendLine($"params_use_alpha_scissor = true\nparams_alpha_scissor_threshold = {F(source?.AlphaThreshold ?? .5f)}");
            if (bindings.ContainsKey("normal_texture")) text.AppendLine("normal_enabled = true\nnormal_scale = 1");
            if (new Vector3(emissive.X, emissive.Y, emissive.Z).LengthSquared() > 0) text.AppendLine($"emission_enabled = true\nemission = {Color(emissive with { W = 1 })}\nemission_energy = 1");
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
