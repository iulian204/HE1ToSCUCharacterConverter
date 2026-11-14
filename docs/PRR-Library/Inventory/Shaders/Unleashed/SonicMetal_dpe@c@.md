# Unleashed: SonicMetal_dpe@c@

[Inventory](../../SHADERS.md) · [Default shader](../../../Unleashed/SonicMetal_dpe@c@.shader)

Architecture: **foundation-lit**. Environment: **4x3 cubemap cross**.

## Selecting and binding

Use the same filename from the desired native-state folder. Material numeric vectors are raw data; copy original values without color conversion. No optional engine effect registers are required. Supply any actual foundation/scene textures used below. See [setup](../../../Documentation/USAGE.md) and [renderer limits](../../../Documentation/LIMITATIONS.md).

## Source-defined behavior

- Reflection direction retains its original per-pixel world-space projection.
- Xbox and Windows agree on normal-map use and world cubemap reflection fetch.
- Metal power R drives gloss and reflection.
- Source uses an exponent-dependent gain of 1/8 and a front-facing specular cosine gate.
- Environment attenuation is 0.3 + 0.7 times region shadow, not a second view-angle Fresnel. The paired vertex supplies Fresnel.
- Retains the actual foundation view-highlight block with source exponent, gain, tint and the material front gate before the contribution square. Renderer integration supplies the independent camera intensity. It supplies the foundation camera response; original HE1 camera range/cone settings are not reproduced.
- Original paired vertex Fresnel is evaluated by the retained compute_fresnel helper using source mrgFresnelParam endpoints. vertex_fresnel caches its interpolated result; no duplicate inline equation or unused helper remains.
- Godot 3.1 uploads scene light RGB times energy times PI. Convert native specular light units once and divide the measured downstream residual: default roughness 1, metallic 0 and specular 0.5 produce Lazarov env=(0.4524,-0.0024), F0=0.04, hence BRDF=0.015696 and retained gain 100 leaves 1.5696. Correct this renderer residual separately from HE1 material math and fake_specular. Foundation diffuse/rim inputs and final specular gain 100 remain unchanged. Material-default writes fail closed pending a revised transfer analysis.
- The source camera RGB multiplier and the actual foundation camera constant multiply the individual fake_specular contribution before its square. Preserve this ordering with the unchanged Unleashed default 0.2. The rejected post-square adapter changed I^2 into I and made 0.2 five times stronger in linear output. Source gloss, lobe mixture and final native-light 100 remain unchanged; Generations retains its existing 0.8 path.
- Bind original material diffuse RGB as raw Plane values and multiply the sampled texture before the foundation contribution square. Both linear-target and final displayed response are tested; moving tint after the square would apply an unintended output-gamma curve to the material parameter.
- Directional cube lookup represented by a 4x3 DX horizontal-cross sampler2D. Supplied-image tiles match original DDS faces without rotation or mirroring. Face-local analytic derivatives select original cube mip levels; per-level half-texel bounds avoid neighboring atlas cells. environment_max_lod is sampler metadata, defaulting safely to base-only when no mip chain is supplied.
- Original camera range/cone attenuation multiplies each actual foundation fake_specular contribution before its square, alongside the source camera RGB input. Mode <=0 bypasses attenuation, mode >0 enables the Range.zw distance ramp, mode >1 also enables the Attribute.yz smoothstep cone raised to Attribute.w. Godot fragment VERTEX supplies camera-space distance and forward cosine without HE1 matrix uniforms. Bind range distances in model export units; bind Attribute as raw Plane. Default mode 0 preserves existing callers until original scene camera values are supplied. This does not implement the separate source camera diffuse contribution.
- Original fragment radiance terms add before output transfer. Retain the actual foundation individual squares and add the exact pairwise interaction terms between equally weighted fragment contributions: ['2.0 * light_field * environment.rgb']. This localized source-backed composition matches the user-identified 15:03 fragment checkpoint. Native light(), fake_specular transfer, per-light compensation, panorama/region helpers and vertex processing are unchanged. No native accumulator recovery or final-output compatibility square is introduced.

These source-review notes retain historical mathematical findings. References to removed optional camera/forced-color controls or old helper interfaces describe history, not the final uniform API. The declarations below are authoritative.

## Coverage and native draw state

Source alpha before state specialization: `min(diffuse.a * material_opacity, 4.0)`.

Opaque ignores coverage alpha. Punch discards below alpha_cutoff (default 0.502) then uses full coverage. Trans writes the source alpha. Blend/culling use real native state. No material script or state-changing uniform is involved.

## Parameters

Tables follow shader declaration order. Godot 3.1.2 Inspector displays numeric controls before samplers while retaining their relative order. No explicit initializer means the shader does not declare an authored material default; bind original material/renderer inputs.

### Opaque

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `gloss_texture` | sampler2D | not declared / none | Original power/gloss map. This renamed sampler retains original channel/exponent/intensity use; eye variants can use multiple channels. | fragment, light |
| `gloss_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for gloss_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment, light |
| `environment_texture` | sampler2D | not declared / none | Original reflection/environment input as sampler2D. Its projection is recorded per shader: sphere, dual sphere, or 4x3 cross. Bind the matching representation; it is independent from sky_panorama. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `specular_color` | vec4 | vec4(1.0) / none | Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion. | fragment, light |
| `power_gloss_level` | vec4 | not declared / none | Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture. | fragment, light |
| `luminance_range` | float | not declared / none | Original environment HDR decode factor: sampled RGB is increased by RGB * sampled alpha * luminance_range in variants using that encoding. Zero disables the encoded extra range, not the base reflection. | fragment |
| `sonic_fresnel_param` | vec2 | vec2(0.3, 1.0) / none | Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions. | vertex |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |
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
| `environment_max_lod` | float | 0.0 / none | Last ORIGINAL cubemap mip level available in the assigned 4x3 atlas chain. Default 0 restricts sampling to base level, suitable for a single extracted cross image. Never include generated mixed-face tail mips. | fragment |

### Punch

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `gloss_texture` | sampler2D | not declared / none | Original power/gloss map. This renamed sampler retains original channel/exponent/intensity use; eye variants can use multiple channels. | fragment, light |
| `gloss_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for gloss_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment, light |
| `environment_texture` | sampler2D | not declared / none | Original reflection/environment input as sampler2D. Its projection is recorded per shader: sphere, dual sphere, or 4x3 cross. Bind the matching representation; it is independent from sky_panorama. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `specular_color` | vec4 | vec4(1.0) / none | Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion. | fragment, light |
| `power_gloss_level` | vec4 | not declared / none | Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture. | fragment, light |
| `material_opacity` | float | 1.0 / none | Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression. | fragment |
| `luminance_range` | float | not declared / none | Original environment HDR decode factor: sampled RGB is increased by RGB * sampled alpha * luminance_range in variants using that encoding. Zero disables the encoded extra range, not the base reflection. | fragment |
| `sonic_fresnel_param` | vec2 | vec2(0.3, 1.0) / none | Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions. | vertex |
| `alpha_cutoff` | float | 0.502 / hint_range(0.0, 1.0) | Punch-only discard threshold, default 0.502. Source material alpha below this is discarded; surviving pixels have full coverage. Not present in Opaque or Trans. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |
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
| `environment_max_lod` | float | 0.0 / none | Last ORIGINAL cubemap mip level available in the assigned 4x3 atlas chain. Default 0 restricts sampling to base level, suitable for a single extracted cross image. Never include generated mixed-face tail mips. | fragment |

### Trans

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `gloss_texture` | sampler2D | not declared / none | Original power/gloss map. This renamed sampler retains original channel/exponent/intensity use; eye variants can use multiple channels. | fragment, light |
| `gloss_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for gloss_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment, light |
| `environment_texture` | sampler2D | not declared / none | Original reflection/environment input as sampler2D. Its projection is recorded per shader: sphere, dual sphere, or 4x3 cross. Bind the matching representation; it is independent from sky_panorama. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `specular_color` | vec4 | vec4(1.0) / none | Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion. | fragment, light |
| `power_gloss_level` | vec4 | not declared / none | Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture. | fragment, light |
| `material_opacity` | float | 1.0 / none | Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression. | fragment |
| `luminance_range` | float | not declared / none | Original environment HDR decode factor: sampled RGB is increased by RGB * sampled alpha * luminance_range in variants using that encoding. Zero disables the encoded extra range, not the base reflection. | fragment |
| `sonic_fresnel_param` | vec2 | vec2(0.3, 1.0) / none | Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions. | vertex |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |
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
| `environment_max_lod` | float | 0.0 / none | Last ORIGINAL cubemap mip level available in the assigned 4x3 atlas chain. Default 0 restricts sampling to base level, suitable for a single extracted cross image. Never include generated mixed-face tail mips. | fragment |

## Parameter data-flow evidence

Exact references from the full Trans interface (the union of RGB and source-coverage inputs), plus Punch-only parameters. These excerpts document expression/channel uses; they do not replace the conservative backward-liveness audit in Validation.

### `diffuse_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D diffuse_texture;
// Referenced in fragment
vec4 diffuse_sample = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
```

### `diffuse_uv_set`

Category: material_uv_control. 

```glsl
uniform int diffuse_uv_set : hint_range(0, 1) = 0;
// Referenced in fragment
vec4 diffuse_sample = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
```

### `gloss_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D gloss_texture;
// Referenced in fragment
vec4 coating = texture(gloss_texture, get_material_uv(gloss_uv_set));
vec4 camera_coating = texture(gloss_texture, get_material_uv(gloss_uv_set));
// Referenced in light
vec4 coating = texture(gloss_texture, get_material_uv(gloss_uv_set));
```

### `gloss_uv_set`

Category: material_uv_control. 

```glsl
uniform int gloss_uv_set : hint_range(0, 1) = 0;
// Referenced in fragment
vec4 coating = texture(gloss_texture, get_material_uv(gloss_uv_set));
vec4 camera_coating = texture(gloss_texture, get_material_uv(gloss_uv_set));
// Referenced in light
vec4 coating = texture(gloss_texture, get_material_uv(gloss_uv_set));
```

### `environment_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D environment_texture;
// Referenced in sample_environment_cross_level
return textureLod(environment_texture, (face + bounded_uv) / vec2(4.0, 3.0), level);
// Referenced in sample_environment_cross
vec2 face_size = vec2(textureSize(environment_texture, 0)) / vec2(4.0, 3.0);
```

### `diffuse_color`

Category: original_material_parameter. Source binding: g_Diffuse / material diffuse.

```glsl
uniform vec4 diffuse_color = vec4(1.0);
// Referenced in fragment
vec4 diffuse = diffuse_sample * diffuse_color;
```

### `specular_color`

Category: original_material_parameter. Source binding: g_Specular / material specular.

```glsl
uniform vec4 specular_color = vec4(1.0);
// Referenced in fragment
fake_specular.rgb *= specular_color.rgb;
// Referenced in light
vec3 specular = vec3(1.0) * specular_color.rgb;
```

### `power_gloss_level`

Category: original_material_parameter. Source binding: g_PowerGlossLevel / power_gloss_level.

```glsl
uniform vec4 power_gloss_level;
// Referenced in fragment
float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0) * camera_coating.r, 1.0, 512.0);
// Referenced in light
float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0) * coating.r, 1.0, 512.0);
```

### `material_opacity`

Category: original_material_parameter. Source binding: g_OpacityReflectionRefractionSpectype.x.

```glsl
uniform float material_opacity = 1.0;
// Referenced in fragment
float material_alpha = min(diffuse.a * material_opacity, 4.0);
```

### `luminance_range`

Category: original_material_parameter. Source binding: mrgLuminanceRange.x.

```glsl
uniform float luminance_range;
// Referenced in fragment
environment_map.rgb += environment_map.rgb * environment_map.a * luminance_range;
```

### `sonic_fresnel_param`

Category: original_material_parameter. Source binding: mrgFresnelParam.xy.

```glsl
uniform vec2 sonic_fresnel_param = vec2(0.3, 1.0);
// Referenced in compute_fresnel
return pow(clamp(1.0 - cos_theta, 0.0, 1.0), 5.0) * (1.0 - sonic_fresnel_param.x) * sonic_fresnel_param.y + sonic_fresnel_param.x * sonic_fresnel_param.y;
```

### `uv0_offset`

Category: material_uv_control. 

```glsl
uniform vec2 uv0_offset;
// Referenced in vertex
UV = (0 == 1 ? UV2 : UV) + uv0_offset;
```

### `uv1_offset`

Category: material_uv_control. 

```glsl
uniform vec2 uv1_offset;
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV, texture_uv1 + uv1_offset);
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

### `environment_max_lod`

Category: scene_or_texture_representation. 

```glsl
uniform float environment_max_lod = 0.0;
// Referenced in sample_environment_cross
float maximum_level = max(floor(min(environment_max_lod, log2(min(face_size.x, face_size.y)))), 0.0);
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

Introduced functions: `get_material_uv`, `sample_environment_cross`, `sample_environment_cross_level`.

Modified/removed functions are not assumed valid merely because their names match. Source enforcement checks actual bodies; final exact-regeneration proofs cover render-state/UV specializations. Documented source differences:

- `compute_fresnel`: Reuse the foundation fifth-power/clamp helper with the paired source vertex Fresnel endpoints; cache its result per vertex. The source material determines coefficients, not a second unused inline implementation.
- `get_rim_light_color`: The original fragment has no reflected light-field falloff lobe. Its declared terms accumulate directly before vertex color and fog. Keeping the rim term would add a separate view-dependent lobe.
- `compute_falloff`: The original fragment has no reflected light-field falloff lobe. Its declared terms accumulate directly before vertex color and fog. Keeping the rim term would add a separate view-dependent lobe.
- `decode_normal_map`: The source pixel body has no material normal sample. The linked vertex normal remains the surface basis.

## Original source mapping

Paths below are relative to the original project root and are not bundled game source files.

- Pixel: `he1shaders/unleashed asm/SonicMetal_dpe@c@.xpu.asm`
- Vertex companion: `he1shaders/unleashed asm/CharactorSphereRef_@b@.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/Dummy_@@p.xvu.asm`

| Shader list | Pixel mode | Vertex mode | Vertex identity |
|---|---|---|---|
| he1shaders/unleashed asm/SonicMetal_dpe[bc].shader-list | default | none | CharactorSphereRef_@b@ |
| he1shaders/unleashed asm/SonicMetal_dpe[bc].shader-list | default | p | Dummy_@@p |

## Validation status

Final artifact: exact source-generator/native-state/two-UV regeneration and dependency audit passed. Whole-library stock-engine results are in [VALIDATION](../../../Documentation/VALIDATION.md).

Source mathematics: source-backed material equations retained; neutral engine-effect specialization and per-map UV routes verified; full renderer equivalence remains partial

Visual validation: {'status': 'not original-game image parity'}

Original-game renderer and appearance equivalence is not claimed. [Known boundaries](../../../Documentation/LIMITATIONS.md) apply.

## Available preset paths

| Coverage | Blend | Faces | Shader | Optional material |
|---|---|---|---|---|
| Opaque | Additive | DoubleSided | [Shader](../../../Render%20States/Opaque/Additive/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Additive/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Additive | SingleSided | [Shader](../../../Render%20States/Opaque/Additive/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Additive/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Multiply | DoubleSided | [Shader](../../../Render%20States/Opaque/Multiply/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Multiply/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Multiply | SingleSided | [Shader](../../../Render%20States/Opaque/Multiply/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Multiply/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Normal | DoubleSided | [Shader](../../../Render%20States/Opaque/Normal/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Normal/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Subtract | DoubleSided | [Shader](../../../Render%20States/Opaque/Subtract/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Subtract/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Subtract | SingleSided | [Shader](../../../Render%20States/Opaque/Subtract/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Opaque/Subtract/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Additive | DoubleSided | [Shader](../../../Render%20States/Punch/Additive/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Additive/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Additive | SingleSided | [Shader](../../../Render%20States/Punch/Additive/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Additive/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Multiply | DoubleSided | [Shader](../../../Render%20States/Punch/Multiply/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Multiply/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Multiply | SingleSided | [Shader](../../../Render%20States/Punch/Multiply/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Multiply/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Normal | DoubleSided | [Shader](../../../Render%20States/Punch/Normal/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Normal/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Normal | SingleSided | [Shader](../../../Render%20States/Punch/Normal/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Normal/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Subtract | DoubleSided | [Shader](../../../Render%20States/Punch/Subtract/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Subtract/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Punch | Subtract | SingleSided | [Shader](../../../Render%20States/Punch/Subtract/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Punch/Subtract/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Additive | DoubleSided | [Shader](../../../Render%20States/Trans/Additive/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Additive/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Additive | SingleSided | [Shader](../../../Render%20States/Trans/Additive/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Additive/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Multiply | DoubleSided | [Shader](../../../Render%20States/Trans/Multiply/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Multiply/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Multiply | SingleSided | [Shader](../../../Render%20States/Trans/Multiply/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Multiply/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Normal | DoubleSided | [Shader](../../../Render%20States/Trans/Normal/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Normal/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Normal | SingleSided | [Shader](../../../Render%20States/Trans/Normal/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Normal/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Subtract | DoubleSided | [Shader](../../../Render%20States/Trans/Subtract/DoubleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Subtract/DoubleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Trans | Subtract | SingleSided | [Shader](../../../Render%20States/Trans/Subtract/SingleSided/Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Render%20States/Trans/Subtract/SingleSided/Materials/Unleashed/SonicMetal_dpe@c@.tres) |
| Opaque | Normal | SingleSided | [Shader](../../../Unleashed/SonicMetal_dpe@c@.shader) | [Material](../../../Materials/Unleashed/SonicMetal_dpe@c@.tres) |
