# Generations: Common_dn@@

[Inventory](../../SHADERS.md) · [Default shader](../../../Generations/Common_dn@@.shader)

Architecture: **foundation-lit**. Environment: **none**.

## Selecting and binding

Use the same filename from the desired native-state folder. Material numeric vectors are raw data; copy original values without color conversion. No optional engine effect registers are required. Supply any actual foundation/scene textures used below. See [setup](../../../Documentation/USAGE.md) and [renderer limits](../../../Documentation/LIMITATIONS.md).

## Source-defined behavior

- Source-sampled material layers: sampDif, sampNrm.
- Common Lambert, source 0.4 + 0.6 fifth-power vertex Fresnel where sampled, and playable screen-Y whitening.
- GI atlas RGB square and saturation, with alpha-modulated direct shadow.
- Four authored material UV lanes, including atlas-transformed lane 1 with GI and optional float UV table.
- User-requested Common use_gi_atlas defaults false: reuse the actual foundation five-sample panorama and region-only direct shadow. True selects the preserved original baked branch. Material, specular, reflection and per-contribution transfer equations remain shared.
- Linked original vertex stages negate the authored BINORMAL before interpolation. Apply BINORMAL = -BINORMAL once before foundation basis caching; keep the original normal decoder and texture unchanged. Stage evidence: [{'source': 'he1shaders/gens hlsl/Default_@n@.wvu.hlsl', 'negated_input': 'v8', 'lines': [136, 137, 138, 143, 144]}, {'source': 'he1shaders/gens hlsl/Default_@n@p.wvu.hlsl', 'negated_input': 'v12', 'lines': [147, 148, 149, 157, 158]}]

These source-review notes retain historical mathematical findings. References to removed optional camera/forced-color controls or old helper interfaces describe history, not the final uniform API. The declarations below are authoritative.

## Coverage and native draw state

Source alpha before state specialization: `min(diffuse.a * diffuse_color.a * vColor.a * material_opacity * 1.0, 4.0)`.

Opaque ignores coverage alpha. Punch discards below alpha_cutoff (default 0.502) then uses full coverage. Trans writes the source alpha. Blend/culling use real native state. No material script or state-changing uniform is involved.

## Parameters

Tables follow shader declaration order. Godot 3.1.2 Inspector displays numeric controls before samplers while retaining their relative order. No explicit initializer means the shader does not declare an authored material default; bind original material/renderer inputs.

### Opaque

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `normal_texture` | sampler2D | not declared / none | Source normal texture; preserve source channel packing. The linked source binormal sign is applied once; do not additionally invert green by assumption. | fragment |
| `normal_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for normal_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |
| `use_gi_atlas` | bool | false / none | Common material switch: false (default) uses actual foundation panorama light-field behavior; true selects the source baked-GI atlas branch. Bind valid GI texture/UV/decode data before enabling. | fragment, light |
| `gi_atlas` | sampler2D | not declared / hint_white | Source baked-GI texture, sampled only in the selected GI branch. Not an environment reflection map. Requires the original atlas coordinates and encoding controls for correct static lighting. | fragment, light |
| `gi_uv_scale` | vec2 | vec2(1.0) / none | Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors. | vertex |
| `gi_uv_offset` | vec2 | vec2(0.0) / none | Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset. | vertex |
| `gi_rgb_scale` | vec3 | vec3(1.0) / none | Original baked-GI RGB decode scale after source squaring/saturation operations. Default (1,1,1). It is not a general material brightness fix. | fragment |
| `gi_saturation` | float | 1.0 / none | Original baked-GI grayscale/color interpolation control. Default 1 keeps source chroma; see exact coefficients and clamp in the shader. | fragment |
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
| `normal_texture` | sampler2D | not declared / none | Source normal texture; preserve source channel packing. The linked source binormal sign is applied once; do not additionally invert green by assumption. | fragment |
| `normal_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for normal_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `material_opacity` | float | 1.0 / none | Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression. | fragment |
| `alpha_cutoff` | float | 0.502 / hint_range(0.0, 1.0) | Punch-only discard threshold, default 0.502. Source material alpha below this is discarded; surviving pixels have full coverage. Not present in Opaque or Trans. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |
| `use_gi_atlas` | bool | false / none | Common material switch: false (default) uses actual foundation panorama light-field behavior; true selects the source baked-GI atlas branch. Bind valid GI texture/UV/decode data before enabling. | fragment, light |
| `gi_atlas` | sampler2D | not declared / hint_white | Source baked-GI texture, sampled only in the selected GI branch. Not an environment reflection map. Requires the original atlas coordinates and encoding controls for correct static lighting. | fragment, light |
| `gi_uv_scale` | vec2 | vec2(1.0) / none | Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors. | vertex |
| `gi_uv_offset` | vec2 | vec2(0.0) / none | Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset. | vertex |
| `gi_rgb_scale` | vec3 | vec3(1.0) / none | Original baked-GI RGB decode scale after source squaring/saturation operations. Default (1,1,1). It is not a general material brightness fix. | fragment |
| `gi_saturation` | float | 1.0 / none | Original baked-GI grayscale/color interpolation control. Default 1 keeps source chroma; see exact coefficients and clamp in the shader. | fragment |
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
| `normal_texture` | sampler2D | not declared / none | Source normal texture; preserve source channel packing. The linked source binormal sign is applied once; do not additionally invert green by assumption. | fragment |
| `normal_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for normal_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `material_opacity` | float | 1.0 / none | Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |
| `use_gi_atlas` | bool | false / none | Common material switch: false (default) uses actual foundation panorama light-field behavior; true selects the source baked-GI atlas branch. Bind valid GI texture/UV/decode data before enabling. | fragment, light |
| `gi_atlas` | sampler2D | not declared / hint_white | Source baked-GI texture, sampled only in the selected GI branch. Not an environment reflection map. Requires the original atlas coordinates and encoding controls for correct static lighting. | fragment, light |
| `gi_uv_scale` | vec2 | vec2(1.0) / none | Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors. | vertex |
| `gi_uv_offset` | vec2 | vec2(0.0) / none | Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset. | vertex |
| `gi_rgb_scale` | vec3 | vec3(1.0) / none | Original baked-GI RGB decode scale after source squaring/saturation operations. Default (1,1,1). It is not a general material brightness fix. | fragment |
| `gi_saturation` | float | 1.0 / none | Original baked-GI grayscale/color interpolation control. Default 1 keeps source chroma; see exact coefficients and clamp in the shader. | fragment |
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

### `normal_texture`

Category: source_material_texture. 

```glsl
uniform sampler2D normal_texture;
// Referenced in fragment
vec3 normal_map = decode_normal_map(texture(normal_texture, get_material_uv(normal_uv_set)));
```

### `normal_uv_set`

Category: material_uv_control. 

```glsl
uniform int normal_uv_set : hint_range(0, 1) = 0;
// Referenced in fragment
vec3 normal_map = decode_normal_map(texture(normal_texture, get_material_uv(normal_uv_set)));
```

### `diffuse_color`

Category: original_material_parameter. Source binding: g_Diffuse / material diffuse.

```glsl
uniform vec4 diffuse_color = vec4(1.0);
// Referenced in fragment
vec3 base_color = diffuse_color.rgb * mix(vec3(1.0), diffuse.rgb, fade);
float material_alpha = min(diffuse.a * diffuse_color.a * vColor.a * material_opacity * 1.0, 4.0);
```

### `material_opacity`

Category: original_material_parameter. Source binding: g_OpacityReflectionRefractionSpectype.x.

```glsl
uniform float material_opacity = 1.0;
// Referenced in fragment
float material_alpha = min(diffuse.a * diffuse_color.a * vColor.a * material_opacity * 1.0, 4.0);
```

### `uv0_offset`

Category: material_uv_control. 

```glsl
uniform vec2 uv0_offset;
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV + uv0_offset, gi_uv + uv1_offset);
```

### `uv1_offset`

Category: material_uv_control. 

```glsl
uniform vec2 uv1_offset;
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV + uv0_offset, gi_uv + uv1_offset);
```

### `use_gi_atlas`

Category: common_lighting_selection. 

```glsl
uniform bool use_gi_atlas = false;
// Referenced in compute_direct_shadow
if (!use_gi_atlas)
// Referenced in fragment
if (use_gi_atlas)
```

### `gi_atlas`

Category: scene_or_texture_representation. 

```glsl
uniform sampler2D gi_atlas : hint_white;
// Referenced in compute_direct_shadow
return bse_shadow * texture(gi_atlas, gi_uv).a;
// Referenced in fragment
vec3 baked = texture(gi_atlas, gi_uv).rgb;
```

### `gi_uv_scale`

Category: scene_or_texture_representation. 

```glsl
uniform vec2 gi_uv_scale = vec2(1.0);
// Referenced in vertex
gi_uv = source_uv1 * gi_uv_scale + gi_uv_offset;
```

### `gi_uv_offset`

Category: scene_or_texture_representation. 

```glsl
uniform vec2 gi_uv_offset = vec2(0.0);
// Referenced in vertex
gi_uv = source_uv1 * gi_uv_scale + gi_uv_offset;
```

### `gi_rgb_scale`

Category: scene_or_texture_representation. 

```glsl
uniform vec3 gi_rgb_scale = vec3(1.0);
// Referenced in fragment
gi_rgb_scale * ambient_light_color.rgb;
```

### `gi_saturation`

Category: scene_or_texture_representation. 

```glsl
uniform float gi_saturation = 1.0;
// Referenced in fragment
light_field = clamp(mix(vec3(gray), baked, gi_saturation), 0.0, 1.0) *
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
// Referenced in fragment
gi_rgb_scale * ambient_light_color.rgb;
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

Modified functions: `compute_shadow`, `decode_normal_map`, `fragment`, `get_light_field_color`, `getq`, `light`, `vertex`.

Removed functions: `compute_falloff`, `compute_fresnel`, `get_rim_light_color`.

Introduced functions: `compute_direct_shadow`, `get_material_uv`.

Modified/removed functions are not assumed valid merely because their names match. Source enforcement checks actual bodies; final exact-regeneration proofs cover render-state/UV specializations. Documented source differences:

- `get_rim_light_color`: The original fragment has no reflected light-field falloff lobe. Its declared terms accumulate directly before vertex color and fog. Keeping the rim term would add a separate view-dependent lobe.
- `compute_falloff`: The original fragment has no reflected light-field falloff lobe. Its declared terms accumulate directly before vertex color and fog. Keeping the rim term would add a separate view-dependent lobe.
- `compute_fresnel`: The source has diffuse lighting only and does not consume a specular or environment Fresnel.
- `light`: Source diffuse uses saturate(N·L), replacing the template's +0.35 wrap. The original Blinn half vector and per-contribution square remain in Godot light(). Source camera specular, where present, also retains the actual foundation fake_specular block with matching material inputs.

## Original source mapping

Paths below are relative to the original project root and are not bundled game source files.

- Pixel: `he1shaders/gens hlsl/Common_dn@@.wpu.hlsl`
- Vertex companion: `he1shaders/gens hlsl/Default_@n@.wvu.hlsl`
- Vertex companion: `he1shaders/gens hlsl/Default_@n@p.wvu.hlsl`

| Shader list | Pixel mode | Vertex mode | Vertex identity |
|---|---|---|---|
| he1shaders/gens hlsl/Common_dn.shader-list | default | none | Default_@n@ |
| he1shaders/gens hlsl/Common_dn[b].shader-list | default | none | Default_@n@ |
| he1shaders/gens hlsl/Common_dn[bv].shader-list | default | none | Default_@n@ |
| he1shaders/gens hlsl/Common_dn[bw].shader-list | default | none | Default_@n@ |
| he1shaders/gens hlsl/Common_dn[v].shader-list | default | none | Default_@n@ |
| he1shaders/gens hlsl/Common_dn.shader-list | default | p | Default_@n@p |
| he1shaders/gens hlsl/Common_dn[b].shader-list | default | p | Default_@n@p |
| he1shaders/gens hlsl/Common_dn[bv].shader-list | default | p | Default_@n@p |
| he1shaders/gens hlsl/Common_dn[bw].shader-list | default | p | Default_@n@p |
| he1shaders/gens hlsl/Common_dn[v].shader-list | default | p | Default_@n@p |

## Validation status

Final artifact: exact source-generator/native-state/two-UV regeneration and dependency audit passed. Whole-library stock-engine results are in [VALIDATION](../../../Documentation/VALIDATION.md).

Source mathematics: source-backed material equations retained; neutral engine-effect specialization and per-map UV routes verified; full renderer equivalence remains partial

Visual validation: {'status': 'not original-game image parity'}

Original-game renderer and appearance equivalence is not claimed. [Known boundaries](../../../Documentation/LIMITATIONS.md) apply.

## Available preset paths

| Coverage | Blend | Faces | Shader | Optional material |
|---|---|---|---|---|
| Opaque | Normal | SingleSided | [Shader](../../../Generations/Common_dn@@.shader) | [Material](../../../Materials/Generations/Common_dn@@.tres) |
| Opaque | Additive | DoubleSided | [Shader](../../../Render%20States/Opaque/Additive/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Additive/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Opaque | Additive | SingleSided | [Shader](../../../Render%20States/Opaque/Additive/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Additive/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Opaque | Multiply | DoubleSided | [Shader](../../../Render%20States/Opaque/Multiply/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Multiply/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Opaque | Multiply | SingleSided | [Shader](../../../Render%20States/Opaque/Multiply/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Multiply/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Opaque | Normal | DoubleSided | [Shader](../../../Render%20States/Opaque/Normal/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Normal/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Opaque | Subtract | DoubleSided | [Shader](../../../Render%20States/Opaque/Subtract/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Subtract/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Opaque | Subtract | SingleSided | [Shader](../../../Render%20States/Opaque/Subtract/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Opaque/Subtract/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Additive | DoubleSided | [Shader](../../../Render%20States/Punch/Additive/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Additive/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Additive | SingleSided | [Shader](../../../Render%20States/Punch/Additive/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Additive/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Multiply | DoubleSided | [Shader](../../../Render%20States/Punch/Multiply/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Multiply/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Multiply | SingleSided | [Shader](../../../Render%20States/Punch/Multiply/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Multiply/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Normal | DoubleSided | [Shader](../../../Render%20States/Punch/Normal/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Normal/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Normal | SingleSided | [Shader](../../../Render%20States/Punch/Normal/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Normal/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Subtract | DoubleSided | [Shader](../../../Render%20States/Punch/Subtract/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Subtract/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Punch | Subtract | SingleSided | [Shader](../../../Render%20States/Punch/Subtract/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Punch/Subtract/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Additive | DoubleSided | [Shader](../../../Render%20States/Trans/Additive/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Additive/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Additive | SingleSided | [Shader](../../../Render%20States/Trans/Additive/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Additive/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Multiply | DoubleSided | [Shader](../../../Render%20States/Trans/Multiply/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Multiply/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Multiply | SingleSided | [Shader](../../../Render%20States/Trans/Multiply/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Multiply/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Normal | DoubleSided | [Shader](../../../Render%20States/Trans/Normal/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Normal/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Normal | SingleSided | [Shader](../../../Render%20States/Trans/Normal/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Normal/SingleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Subtract | DoubleSided | [Shader](../../../Render%20States/Trans/Subtract/DoubleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Subtract/DoubleSided/Materials/Generations/Common_dn@@.tres) |
| Trans | Subtract | SingleSided | [Shader](../../../Render%20States/Trans/Subtract/SingleSided/Generations/Common_dn@@.shader) | [Material](../../../Render%20States/Trans/Subtract/SingleSided/Materials/Generations/Common_dn@@.tres) |
