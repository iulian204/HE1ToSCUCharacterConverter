# Unleashed: NightEnemyCommon_drE@@

[Inventory](../../SHADERS.md) · [Default shader](../../../Unleashed/NightEnemyCommon_drE@@.shader)

Architecture: **foundation-lit**. Environment: **none**.

## Selecting and binding

Use the same filename from the desired native-state folder. Material numeric vectors are raw data; copy original values without color conversion. No optional engine effect registers are required. Supply any actual foundation/scene textures used below. See [setup](../../../Documentation/USAGE.md) and [renderer limits](../../../Documentation/LIMITATIONS.md).

## Source-defined behavior

- Xbox and Windows agree on diffuse, mask, noise and packed refraction slots.
- Source adds independent R-mask diffuse glow and inverse-G-mask authored vertex rim.
- Refraction samples two TIME-scrolled noise layers and mixes the packed projection by noise BA and inverse mask G.
- Charactor depth/grazing supplies the original refraction gain and falloff.
- Preserves the foundation view highlight with the material front gate before its square, attenuated by the same refraction mix as direct specular; renderer integration supplies camera intensity. Original camera-light range/cone attenuation is not reproduced.
- Original paired vertex Fresnel is evaluated by the retained compute_fresnel helper using source mrgFresnelParam endpoints. vertex_fresnel caches its interpolated result; no duplicate inline equation or unused helper remains.
- Godot 3.1 uploads scene light RGB times energy times PI. Convert native specular light units once and divide the measured downstream residual: default roughness 1, metallic 0 and specular 0.5 produce Lazarov env=(0.4524,-0.0024), F0=0.04, hence BRDF=0.015696 and retained gain 100 leaves 1.5696. Correct this renderer residual separately from HE1 material math and fake_specular. Foundation diffuse/rim inputs and final specular gain 100 remain unchanged. Material-default writes fail closed pending a revised transfer analysis.
- The source camera RGB multiplier and the actual foundation camera constant multiply the individual fake_specular contribution before its square. Preserve this ordering with the unchanged Unleashed default 0.2. The rejected post-square adapter changed I^2 into I and made 0.2 five times stronger in linear output. Source gloss, lobe mixture and final native-light 100 remain unchanged; Generations retains its existing 0.8 path.
- Bind original material diffuse RGB as raw Plane values and multiply the sampled texture before the foundation contribution square. Both linear-target and final displayed response are tested; moving tint after the square would apply an unintended output-gamma curve to the material parameter.
- Original camera range/cone attenuation multiplies each actual foundation fake_specular contribution before its square, alongside the source camera RGB input. Mode <=0 bypasses attenuation, mode >0 enables the Range.zw distance ramp, mode >1 also enables the Attribute.yz smoothstep cone raised to Attribute.w. Godot fragment VERTEX supplies camera-space distance and forward cosine without HE1 matrix uniforms. Bind range distances in model export units; bind Attribute as raw Plane. Default mode 0 preserves existing callers until original scene camera values are supplied. This does not implement the separate source camera diffuse contribution.

These source-review notes retain historical mathematical findings. References to removed optional camera/forced-color controls or old helper interfaces describe history, not the final uniform API. The declarations below are authoritative.

## Coverage and native draw state

Source alpha before state specialization: `min(diffuse.a * diffuse_color.a, 4.0)`.

Opaque ignores coverage alpha. Punch discards below alpha_cutoff (default 0.502) then uses full coverage. Trans writes the source alpha. Blend/culling use real native state. No material script or state-changing uniform is involved.

## Parameters

Tables follow shader declaration order. Godot 3.1.2 Inspector displays numeric controls before samplers while retaining their relative order. No explicit initializer means the shader does not declare an authored material default; bind original material/renderer inputs.

### Opaque

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `refraction_texture` | sampler2D | not declared / none | Original refraction input sampled by the source refraction path; not an environment cube or a generated capture. | fragment |
| `mask_texture` | sampler2D | not declared / none | Original source material mask; consult the exact sample and downstream source operations for channel use. | fragment, light |
| `mask_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for mask_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment, light |
| `noise_texture` | sampler2D | not declared / none | Original animated/procedural material noise input, using source scroll/scale/time coordinates. | fragment, light |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `specular_color` | vec4 | vec4(1.0) / none | Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion. | fragment, light |
| `power_gloss_level` | vec4 | not declared / none | Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture. | fragment, light |
| `sonic_fresnel_param` | vec2 | vec2(0.3, 1.0) / none | Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions. | vertex |
| `falloff_color_near` | vec4 | not declared / none | Original numeric near/facing falloff color vector; blended with edge color using the source falloff equation. | vertex |
| `falloff_color_edge` | vec4 | not declared / none | Original numeric edge falloff color vector; blended with near color using the source falloff equation. | vertex |
| `falloff_param` | vec2 | not declared / none | Original two-component falloff control; exact exponent/scale/bias use is visible in the shader expressions. | vertex |
| `noise_scroll_scale` | vec4 | not declared / none | Original four-component noise scroll/scale control. TIME and UV dependencies, where present, remain in the listed expressions; use original material lanes. | fragment, light |
| `noise_power` | vec2 | not declared / none | Original numeric two-component noise response. This name remains power because it is a source parameter, not a gloss sampler. | fragment, light |
| `emission_power` | vec2 | not declared / none | Original numeric two-component emission control from mrgEmissionPower; source Z/Y packing is variant-defined and documented by the source review/code. | fragment |
| `refraction_param` | vec3 | not declared / none | Original numeric refraction displacement/response vector. Requires the refraction input expected by this variant; supplying this vector cannot generate a scene capture. | vertex |
| `gi_uv_scale` | vec2 | vec2(1.0) / none | Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors. | vertex |
| `gi_uv_offset` | vec2 | vec2(0.0) / none | Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset. | vertex |
| `sky_color` | vec4 | not declared / hint_color | Foundation upper-hemisphere lighting color. A color-picker input intentionally retained from the foundation; used by actual panorama/light-field or rim helpers where present. | fragment |
| `ground_color` | vec4 | not declared / hint_color | Foundation lower-hemisphere lighting color. A color-picker input intentionally retained from the foundation; not an original numeric HE1 material vector. | fragment |
| `ambient_light_color` | vec4 | not declared / hint_color | Foundation ambient color in lit materials (color picker). A declaration without hint_color is a numeric source-use specialization; the exact declaration and uses below take precedence. Not Godot Environment ambient by itself. | fragment |
| `sky_panorama` | sampler2D | not declared / none | Foundation 2D panorama used by its five-sample light-field and reflected-direction rim functions. This input is distinct from environment_texture and is not a 4x3 cubemap cross. | fragment |
| `ambient_light_sky_contribution` | float | not declared / none | Foundation interpolation weight mixing supplied ambient with sampled panorama/sky/ground. Use the actual foundation function, including brightness floor and clamp; it is not a generic scene-light intensity. | fragment |
| `inner_shadow_amount` | float | 1.0 / hint_range(0.0, 1.0, 0.05) | Foundation region-shadow strength inside the region; default 1. Applied through compute_shadow and the texture/box mask. With no valid region the shader bypasses this shadow. | vertex |
| `outer_shadow_amount` | float | 0.0 / hint_range(0.0, 1.0, 0.05) | Foundation region-shadow strength outside the region; default 0. Not the native DirectionalLight shadow strength. | vertex |
| `inner_min` | vec3 | vec3(-1.0) / none | Foundation local shadow-region lower bounds, clamped componentwise to [-1,0] in sdBox. Default (-1,-1,-1). | vertex |
| `inner_max` | vec3 | vec3(1.0) / none | Foundation local shadow-region upper bounds, clamped componentwise to [0,1] in sdBox. Default (1,1,1). | vertex |
| `shadow_texture` | sampler2D | not declared / none | Foundation region shadow mask. Red is inverted in sdBox; bind together with the region transform/bounds. Not an HE1 shadow-register dump or an automatically generated engine shadow map. | vertex |
| `region_transform` | mat4 | not declared / none | Foundation region-to-world transform consumed by compute_shadow. Supply it only for a functioning region-shadow setup; a degenerate axis bypasses the region. It is a real mandatory integration input when that system is used. | vertex |

### Punch

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `refraction_texture` | sampler2D | not declared / none | Original refraction input sampled by the source refraction path; not an environment cube or a generated capture. | fragment |
| `mask_texture` | sampler2D | not declared / none | Original source material mask; consult the exact sample and downstream source operations for channel use. | fragment, light |
| `mask_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for mask_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment, light |
| `noise_texture` | sampler2D | not declared / none | Original animated/procedural material noise input, using source scroll/scale/time coordinates. | fragment, light |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `specular_color` | vec4 | vec4(1.0) / none | Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion. | fragment, light |
| `power_gloss_level` | vec4 | not declared / none | Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture. | fragment, light |
| `sonic_fresnel_param` | vec2 | vec2(0.3, 1.0) / none | Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions. | vertex |
| `falloff_color_near` | vec4 | not declared / none | Original numeric near/facing falloff color vector; blended with edge color using the source falloff equation. | vertex |
| `falloff_color_edge` | vec4 | not declared / none | Original numeric edge falloff color vector; blended with near color using the source falloff equation. | vertex |
| `falloff_param` | vec2 | not declared / none | Original two-component falloff control; exact exponent/scale/bias use is visible in the shader expressions. | vertex |
| `noise_scroll_scale` | vec4 | not declared / none | Original four-component noise scroll/scale control. TIME and UV dependencies, where present, remain in the listed expressions; use original material lanes. | fragment, light |
| `noise_power` | vec2 | not declared / none | Original numeric two-component noise response. This name remains power because it is a source parameter, not a gloss sampler. | fragment, light |
| `emission_power` | vec2 | not declared / none | Original numeric two-component emission control from mrgEmissionPower; source Z/Y packing is variant-defined and documented by the source review/code. | fragment |
| `refraction_param` | vec3 | not declared / none | Original numeric refraction displacement/response vector. Requires the refraction input expected by this variant; supplying this vector cannot generate a scene capture. | vertex |
| `alpha_cutoff` | float | 0.502 / hint_range(0.0, 1.0) | Punch-only discard threshold, default 0.502. Source material alpha below this is discarded; surviving pixels have full coverage. Not present in Opaque or Trans. | fragment |
| `gi_uv_scale` | vec2 | vec2(1.0) / none | Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors. | vertex |
| `gi_uv_offset` | vec2 | vec2(0.0) / none | Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset. | vertex |
| `sky_color` | vec4 | not declared / hint_color | Foundation upper-hemisphere lighting color. A color-picker input intentionally retained from the foundation; used by actual panorama/light-field or rim helpers where present. | fragment |
| `ground_color` | vec4 | not declared / hint_color | Foundation lower-hemisphere lighting color. A color-picker input intentionally retained from the foundation; not an original numeric HE1 material vector. | fragment |
| `ambient_light_color` | vec4 | not declared / hint_color | Foundation ambient color in lit materials (color picker). A declaration without hint_color is a numeric source-use specialization; the exact declaration and uses below take precedence. Not Godot Environment ambient by itself. | fragment |
| `sky_panorama` | sampler2D | not declared / none | Foundation 2D panorama used by its five-sample light-field and reflected-direction rim functions. This input is distinct from environment_texture and is not a 4x3 cubemap cross. | fragment |
| `ambient_light_sky_contribution` | float | not declared / none | Foundation interpolation weight mixing supplied ambient with sampled panorama/sky/ground. Use the actual foundation function, including brightness floor and clamp; it is not a generic scene-light intensity. | fragment |
| `inner_shadow_amount` | float | 1.0 / hint_range(0.0, 1.0, 0.05) | Foundation region-shadow strength inside the region; default 1. Applied through compute_shadow and the texture/box mask. With no valid region the shader bypasses this shadow. | vertex |
| `outer_shadow_amount` | float | 0.0 / hint_range(0.0, 1.0, 0.05) | Foundation region-shadow strength outside the region; default 0. Not the native DirectionalLight shadow strength. | vertex |
| `inner_min` | vec3 | vec3(-1.0) / none | Foundation local shadow-region lower bounds, clamped componentwise to [-1,0] in sdBox. Default (-1,-1,-1). | vertex |
| `inner_max` | vec3 | vec3(1.0) / none | Foundation local shadow-region upper bounds, clamped componentwise to [0,1] in sdBox. Default (1,1,1). | vertex |
| `shadow_texture` | sampler2D | not declared / none | Foundation region shadow mask. Red is inverted in sdBox; bind together with the region transform/bounds. Not an HE1 shadow-register dump or an automatically generated engine shadow map. | vertex |
| `region_transform` | mat4 | not declared / none | Foundation region-to-world transform consumed by compute_shadow. Supply it only for a functioning region-shadow setup; a degenerate axis bypasses the region. It is a real mandatory integration input when that system is used. | vertex |

### Trans

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `refraction_texture` | sampler2D | not declared / none | Original refraction input sampled by the source refraction path; not an environment cube or a generated capture. | fragment |
| `mask_texture` | sampler2D | not declared / none | Original source material mask; consult the exact sample and downstream source operations for channel use. | fragment, light |
| `mask_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for mask_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment, light |
| `noise_texture` | sampler2D | not declared / none | Original animated/procedural material noise input, using source scroll/scale/time coordinates. | fragment, light |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `specular_color` | vec4 | vec4(1.0) / none | Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion. | fragment, light |
| `power_gloss_level` | vec4 | not declared / none | Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture. | fragment, light |
| `sonic_fresnel_param` | vec2 | vec2(0.3, 1.0) / none | Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions. | vertex |
| `falloff_color_near` | vec4 | not declared / none | Original numeric near/facing falloff color vector; blended with edge color using the source falloff equation. | vertex |
| `falloff_color_edge` | vec4 | not declared / none | Original numeric edge falloff color vector; blended with near color using the source falloff equation. | vertex |
| `falloff_param` | vec2 | not declared / none | Original two-component falloff control; exact exponent/scale/bias use is visible in the shader expressions. | vertex |
| `noise_scroll_scale` | vec4 | not declared / none | Original four-component noise scroll/scale control. TIME and UV dependencies, where present, remain in the listed expressions; use original material lanes. | fragment, light |
| `noise_power` | vec2 | not declared / none | Original numeric two-component noise response. This name remains power because it is a source parameter, not a gloss sampler. | fragment, light |
| `emission_power` | vec2 | not declared / none | Original numeric two-component emission control from mrgEmissionPower; source Z/Y packing is variant-defined and documented by the source review/code. | fragment |
| `refraction_param` | vec3 | not declared / none | Original numeric refraction displacement/response vector. Requires the refraction input expected by this variant; supplying this vector cannot generate a scene capture. | vertex |
| `gi_uv_scale` | vec2 | vec2(1.0) / none | Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors. | vertex |
| `gi_uv_offset` | vec2 | vec2(0.0) / none | Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset. | vertex |
| `sky_color` | vec4 | not declared / hint_color | Foundation upper-hemisphere lighting color. A color-picker input intentionally retained from the foundation; used by actual panorama/light-field or rim helpers where present. | fragment |
| `ground_color` | vec4 | not declared / hint_color | Foundation lower-hemisphere lighting color. A color-picker input intentionally retained from the foundation; not an original numeric HE1 material vector. | fragment |
| `ambient_light_color` | vec4 | not declared / hint_color | Foundation ambient color in lit materials (color picker). A declaration without hint_color is a numeric source-use specialization; the exact declaration and uses below take precedence. Not Godot Environment ambient by itself. | fragment |
| `sky_panorama` | sampler2D | not declared / none | Foundation 2D panorama used by its five-sample light-field and reflected-direction rim functions. This input is distinct from environment_texture and is not a 4x3 cubemap cross. | fragment |
| `ambient_light_sky_contribution` | float | not declared / none | Foundation interpolation weight mixing supplied ambient with sampled panorama/sky/ground. Use the actual foundation function, including brightness floor and clamp; it is not a generic scene-light intensity. | fragment |
| `inner_shadow_amount` | float | 1.0 / hint_range(0.0, 1.0, 0.05) | Foundation region-shadow strength inside the region; default 1. Applied through compute_shadow and the texture/box mask. With no valid region the shader bypasses this shadow. | vertex |
| `outer_shadow_amount` | float | 0.0 / hint_range(0.0, 1.0, 0.05) | Foundation region-shadow strength outside the region; default 0. Not the native DirectionalLight shadow strength. | vertex |
| `inner_min` | vec3 | vec3(-1.0) / none | Foundation local shadow-region lower bounds, clamped componentwise to [-1,0] in sdBox. Default (-1,-1,-1). | vertex |
| `inner_max` | vec3 | vec3(1.0) / none | Foundation local shadow-region upper bounds, clamped componentwise to [0,1] in sdBox. Default (1,1,1). | vertex |
| `shadow_texture` | sampler2D | not declared / none | Foundation region shadow mask. Red is inverted in sdBox; bind together with the region transform/bounds. Not an HE1 shadow-register dump or an automatically generated engine shadow map. | vertex |
| `region_transform` | mat4 | not declared / none | Foundation region-to-world transform consumed by compute_shadow. Supply it only for a functioning region-shadow setup; a degenerate axis bypasses the region. It is a real mandatory integration input when that system is used. | vertex |

## Parameter data-flow evidence

Exact references from the full Trans interface (the union of RGB and source-coverage inputs), plus Punch-only parameters. These excerpts document expression/channel uses; they do not replace the conservative backward-liveness audit in Validation.

### `diffuse_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D diffuse_texture;
// Referenced in fragment
vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
```

### `diffuse_uv_set`

Category: material_uv_control. 

```glsl
uniform int diffuse_uv_set : hint_range(0, 1) = 0;
// Referenced in fragment
vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
```

### `refraction_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D refraction_texture;
// Referenced in fragment
vec3 refracted = texture(refraction_texture, refraction_uv).rgb;
```

### `mask_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D mask_texture;
// Referenced in fragment
vec2 mask = texture(mask_texture, get_material_uv(mask_uv_set)).rg;
// Referenced in light
float reflection_mix = compute_night_blend(get_night_noise(noise_uv), texture(mask_texture, get_material_uv(mask_uv_set)).rg);
```

### `mask_uv_set`

Category: material_uv_control. 

```glsl
uniform int mask_uv_set : hint_range(0, 1) = 0;
// Referenced in fragment
vec2 mask = texture(mask_texture, get_material_uv(mask_uv_set)).rg;
// Referenced in light
float reflection_mix = compute_night_blend(get_night_noise(noise_uv), texture(mask_texture, get_material_uv(mask_uv_set)).rg);
```

### `noise_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D noise_texture;
// Referenced in get_night_noise
return texture(noise_texture, uv + animation_time * noise_scroll_scale.xy) +
texture(noise_texture, uv + animation_time * noise_scroll_scale.zw);
```

### `diffuse_color`

Category: original_material_parameter. Source binding: g_Diffuse / material diffuse.

```glsl
uniform vec4 diffuse_color = vec4(1.0);
// Referenced in fragment
vec3 base_color = diffuse.rgb * diffuse_color.rgb;
float material_alpha = min(diffuse.a * diffuse_color.a, 4.0);
```

### `specular_color`

Category: original_material_parameter. Source binding: g_Specular / material specular.

```glsl
uniform vec4 specular_color = vec4(1.0);
// Referenced in fragment
fake_specular.rgb *= specular_color.rgb;
// Referenced in light
vec3 specular = specular_color.rgb * specular_gloss * 0.125;
```

### `power_gloss_level`

Category: original_material_parameter. Source binding: g_PowerGlossLevel / power_gloss_level.

```glsl
uniform vec4 power_gloss_level;
// Referenced in fragment
float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
// Referenced in light
float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
```

### `sonic_fresnel_param`

Category: original_material_parameter. Source binding: mrgFresnelParam.xy.

```glsl
uniform vec2 sonic_fresnel_param = vec2(0.3, 1.0);
// Referenced in compute_fresnel
return pow(clamp(1.0 - cos_theta, 0.0, 1.0), 5.0) * (1.0 - sonic_fresnel_param.x) * sonic_fresnel_param.y + sonic_fresnel_param.x * sonic_fresnel_param.y;
```

### `falloff_color_near`

Category: original_material_parameter. Source binding: g_FalloffColor0.

```glsl
uniform vec4 falloff_color_near;
// Referenced in vertex
edge_emission = mix(falloff_color_near.rgb, falloff_color_edge.rgb, edge) * falloff_param.x * edge;
```

### `falloff_color_edge`

Category: original_material_parameter. Source binding: g_FalloffColor1.

```glsl
uniform vec4 falloff_color_edge;
// Referenced in vertex
edge_emission = mix(falloff_color_near.rgb, falloff_color_edge.rgb, edge) * falloff_param.x * edge;
```

### `falloff_param`

Category: original_material_parameter. Source binding: g_FalloffParam.xy.

```glsl
uniform vec2 falloff_param;
// Referenced in vertex
float edge = min(pow(grazing, falloff_param.y), 1.0);
edge_emission = mix(falloff_color_near.rgb, falloff_color_edge.rgb, edge) * falloff_param.x * edge;
```

### `noise_scroll_scale`

Category: original_material_parameter. Source binding: g_NoiseScrollScale.

```glsl
uniform vec4 noise_scroll_scale;
// Referenced in get_night_noise
return texture(noise_texture, uv + animation_time * noise_scroll_scale.xy) +
texture(noise_texture, uv + animation_time * noise_scroll_scale.zw);
```

### `noise_power`

Category: original_material_parameter. Source binding: g_NoisePower.xy.

```glsl
uniform vec2 noise_power;
// Referenced in compute_night_blend
return (1.0 - mask.y) * clamp((noise_value.b + noise_value.a) * noise_power.y * refraction_gain, 0.0, 1.0);
// Referenced in fragment
vec3 direction = mat3(CAMERA_MATRIX) * VIEW + noise_value.rgb * noise_power.x;
```

### `emission_power`

Category: original_material_parameter. Source binding: mrgEmissionPower.z / mrgEmissionPower.y.

```glsl
uniform vec2 emission_power;
// Referenced in fragment
vec3 diffuse_emission = diffuse.rgb * mask.x * emission_power.x;
vec3 rim = edge_emission * (1.0 - mask.y) * emission_power.y;
```

### `refraction_param`

Category: original_material_parameter. Source binding: g_RefractionParam.xyz.

```glsl
uniform vec3 refraction_param;
// Referenced in vertex
float distance_gate = clamp(view_depth - refraction_param.z, 0.0, 1.0);
float view_gain = pow(abs(view_cosine), refraction_param.y);
refraction_gain = refraction_param.x * distance_gate * view_gain * view_gain;
```

### `gi_uv_scale`

Category: scene_or_texture_representation. 

```glsl
uniform vec2 gi_uv_scale = vec2(1.0);
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV, source_uv1 * gi_uv_scale + gi_uv_offset);
```

### `gi_uv_offset`

Category: scene_or_texture_representation. 

```glsl
uniform vec2 gi_uv_offset = vec2(0.0);
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV, source_uv1 * gi_uv_scale + gi_uv_offset);
```

### `sky_color`

Category: foundation_renderer_input. 

```glsl
uniform vec4 sky_color : hint_color;
// Referenced in get_light_field_color
vec3 up_color = mix(sky_color.rgb, final_ambient_color, ambient_light_sky_contribution);
```

### `ground_color`

Category: foundation_renderer_input. 

```glsl
uniform vec4 ground_color : hint_color;
// Referenced in get_light_field_color
vec3 down_color = mix(ground_color.rgb, final_ambient_color, ambient_light_sky_contribution);
```

### `ambient_light_color`

Category: foundation_renderer_input. 

```glsl
uniform vec4 ambient_light_color : hint_color;
// Referenced in get_light_field_color
vec3 final_ambient_color = ambient_light_color.rgb * (1.0 - ambient_light_sky_contribution) + final_avg_sky_tex.rgb * ambient_light_sky_contribution;
```

### `sky_panorama`

Category: foundation_renderer_input. 

```glsl
uniform sampler2D sky_panorama;
// Referenced in get_light_field_color
vec4 sky_tex1 = texture(sky_panorama, vec2(0.25, 0.25));
vec4 sky_tex2 = texture(sky_panorama, vec2(0.75, 0.25));
vec4 sky_tex3 = texture(sky_panorama, vec2(0.25, 0.75));
vec4 sky_tex4 = texture(sky_panorama, vec2(0.75, 0.75));
vec4 sky_tex5 = texture(sky_panorama, vec2(0.5, 0.5));
```

### `ambient_light_sky_contribution`

Category: foundation_renderer_input. 

```glsl
uniform float ambient_light_sky_contribution;
// Referenced in get_light_field_color
vec3 final_ambient_color = ambient_light_color.rgb * (1.0 - ambient_light_sky_contribution) + final_avg_sky_tex.rgb * ambient_light_sky_contribution;
vec3 up_color = mix(sky_color.rgb, final_ambient_color, ambient_light_sky_contribution);
vec3 down_color = mix(ground_color.rgb, final_ambient_color, ambient_light_sky_contribution);
```

### `inner_shadow_amount`

Category: foundation_renderer_input. 

```glsl
uniform float inner_shadow_amount : hint_range(0.0, 1.0, 0.05) = 1.0;
// Referenced in compute_shadow
return 1.0 - mix(inner_shadow_amount, outer_shadow_amount, clamp(sdBox(p, inner_min, inner_max, shadow_texture), 0.0, 1.0));
```

### `outer_shadow_amount`

Category: foundation_renderer_input. 

```glsl
uniform float outer_shadow_amount : hint_range(0.0, 1.0, 0.05) = 0.0;
// Referenced in compute_shadow
return 1.0 - mix(inner_shadow_amount, outer_shadow_amount, clamp(sdBox(p, inner_min, inner_max, shadow_texture), 0.0, 1.0));
```

### `inner_min`

Category: foundation_renderer_input. 

```glsl
uniform vec3 inner_min = vec3(-1.0);
// Referenced in sdBox
vec3 imin = clamp(inner_min, -1.0, 0.0);
// Referenced in compute_shadow
return 1.0 - mix(inner_shadow_amount, outer_shadow_amount, clamp(sdBox(p, inner_min, inner_max, shadow_texture), 0.0, 1.0));
```

### `inner_max`

Category: foundation_renderer_input. 

```glsl
uniform vec3 inner_max = vec3(1.0);
// Referenced in sdBox
vec3 imax = clamp(inner_max, 0.0, 1.0);
// Referenced in compute_shadow
return 1.0 - mix(inner_shadow_amount, outer_shadow_amount, clamp(sdBox(p, inner_min, inner_max, shadow_texture), 0.0, 1.0));
```

### `shadow_texture`

Category: foundation_renderer_input. 

```glsl
uniform sampler2D shadow_texture;
// Referenced in compute_shadow
return 1.0 - mix(inner_shadow_amount, outer_shadow_amount, clamp(sdBox(p, inner_min, inner_max, shadow_texture), 0.0, 1.0));
```

### `region_transform`

Category: foundation_renderer_input. 

```glsl
uniform mat4 region_transform;
// Referenced in compute_shadow
vec3 region_axis_length = vec3(length(region_transform[0]), length(region_transform[1]), length(region_transform[2]));
p = (p - region_transform[3].xyz) * mat3(region_transform);
```

### `alpha_cutoff`

Category: native_cutout_coverage. 

```glsl
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.502;
// Referenced in fragment
if (material_alpha < alpha_cutoff)
```

## Foundation evidence

Preserved functions: `sdBox`.

Modified functions: `compute_fresnel`, `compute_shadow`, `fragment`, `get_light_field_color`, `getq`, `light`, `vertex`.

Removed functions: `compute_falloff`, `decode_normal_map`, `get_rim_light_color`.

Introduced functions: `compute_night_blend`, `get_material_uv`, `get_night_noise`.

Modified/removed functions are not assumed valid merely because their names match. Source enforcement checks actual bodies; final exact-regeneration proofs cover render-state/UV specializations. Documented source differences:

- `compute_fresnel`: Reuse the foundation fifth-power/clamp helper with the paired source vertex Fresnel endpoints; cache its result per vertex. The source material determines coefficients, not a second unused inline implementation.
- `get_rim_light_color`: The original fragment has no reflected light-field falloff lobe. Its declared terms accumulate directly before vertex color and fog. Keeping the rim term would add a separate view-dependent lobe.
- `compute_falloff`: The original fragment has no reflected light-field falloff lobe. Its declared terms accumulate directly before vertex color and fog. Keeping the rim term would add a separate view-dependent lobe.
- `decode_normal_map`: The source pixel body has no material normal sample. The linked vertex normal remains the surface basis.

## Original source mapping

Paths below are relative to the original project root and are not bundled game source files.

- Pixel: `he1shaders/unleashed asm/NightEnemyCommon_drE@@.xpu.asm`
- Vertex companion: `he1shaders/unleashed asm/NightEnemyCommon_@b@.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/NightEnemyCommon_@b@p.xvu.asm`

| Shader list | Pixel mode | Vertex mode | Vertex identity |
|---|---|---|---|
| he1shaders/unleashed asm/NightEnemyCommon_drE[b].shader-list | default | none | NightEnemyCommon_@b@ |
| he1shaders/unleashed asm/NightEnemyCommon_drE[b].shader-list | default | p | NightEnemyCommon_@b@p |

## Validation status

Final artifact: exact source-generator/native-state/two-UV regeneration and dependency audit passed. Whole-library stock-engine results are in [VALIDATION](../../../Documentation/VALIDATION.md).

Source mathematics: source-backed material equations retained; neutral engine-effect specialization and per-map UV routes verified; full renderer equivalence remains partial

Visual validation: {'status': 'not original-game image parity'}

Original-game renderer and appearance equivalence is not claimed. [Known boundaries](../../../Documentation/LIMITATIONS.md) apply.

## Available preset paths

| Coverage | Blend | Faces | Shader | Optional material |
|---|---|---|---|---|
| Opaque | Additive | DoubleSided | [Shader](../../../Render%20States/Opaque/Additive/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Additive/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Additive | SingleSided | [Shader](../../../Render%20States/Opaque/Additive/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Additive/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Multiply | DoubleSided | [Shader](../../../Render%20States/Opaque/Multiply/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Multiply/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Multiply | SingleSided | [Shader](../../../Render%20States/Opaque/Multiply/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Multiply/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Normal | DoubleSided | [Shader](../../../Render%20States/Opaque/Normal/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Normal/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Subtract | DoubleSided | [Shader](../../../Render%20States/Opaque/Subtract/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Subtract/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Subtract | SingleSided | [Shader](../../../Render%20States/Opaque/Subtract/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Opaque/Subtract/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Additive | DoubleSided | [Shader](../../../Render%20States/Punch/Additive/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Additive/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Additive | SingleSided | [Shader](../../../Render%20States/Punch/Additive/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Additive/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Multiply | DoubleSided | [Shader](../../../Render%20States/Punch/Multiply/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Multiply/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Multiply | SingleSided | [Shader](../../../Render%20States/Punch/Multiply/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Multiply/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Normal | DoubleSided | [Shader](../../../Render%20States/Punch/Normal/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Normal/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Normal | SingleSided | [Shader](../../../Render%20States/Punch/Normal/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Normal/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Subtract | DoubleSided | [Shader](../../../Render%20States/Punch/Subtract/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Subtract/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Punch | Subtract | SingleSided | [Shader](../../../Render%20States/Punch/Subtract/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Punch/Subtract/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Additive | DoubleSided | [Shader](../../../Render%20States/Trans/Additive/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Additive/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Additive | SingleSided | [Shader](../../../Render%20States/Trans/Additive/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Additive/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Multiply | DoubleSided | [Shader](../../../Render%20States/Trans/Multiply/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Multiply/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Multiply | SingleSided | [Shader](../../../Render%20States/Trans/Multiply/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Multiply/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Normal | DoubleSided | [Shader](../../../Render%20States/Trans/Normal/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Normal/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Normal | SingleSided | [Shader](../../../Render%20States/Trans/Normal/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Normal/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Subtract | DoubleSided | [Shader](../../../Render%20States/Trans/Subtract/DoubleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Subtract/DoubleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Trans | Subtract | SingleSided | [Shader](../../../Render%20States/Trans/Subtract/SingleSided/Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Render%20States/Trans/Subtract/SingleSided/Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
| Opaque | Normal | SingleSided | [Shader](../../../Unleashed/NightEnemyCommon_drE@@.shader) | [Material](../../../Materials/Unleashed/NightEnemyCommon_drE@@.tres) |
