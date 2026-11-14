# Parameter reference

This reference covers every uniform in all 152 programs and all 24 native states. Not every uniform appears in every program. Per-program pages list exact types, defaults, stage/helper uses, source operations and source alpha.

Original material numeric vectors have no color-picker hint. Foundation-authored sky, ground and ambient colors keep their declared color hint. No optional forced-color/camera registers, packed UV textures or UV2/UV3 inputs are required.

## `alpha_cutoff`

Punch-only discard threshold, default 0.502. Source material alpha below this is discarded; surviving pixels have full coverage. Not present in Opaque or Trans.

Category: native_cutout_coverage. Present in 152 canonical programs (one or more coverage states).

```glsl
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.502;
```

## `alpha_texture`

Dedicated source coverage mask; operative only in a preset whose alpha data flow uses it.

Category: source_material_texture. Present in 3 canonical programs (one or more coverage states).

```glsl
uniform sampler2D alpha_texture;
```

## `alpha_uv_set`

Per-map UV selector for alpha_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 3 canonical programs (one or more coverage states).

```glsl
uniform int alpha_uv_set : hint_range(0, 1) = 0;
```

## `ambient_light_color`

Foundation ambient color in lit materials (color picker). A declaration without hint_color is a numeric source-use specialization; the exact declaration and uses below take precedence. Not Godot Environment ambient by itself.

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform vec4 ambient_light_color : hint_color;
uniform vec4 ambient_light_color;
```

## `ambient_light_sky_contribution`

Foundation interpolation weight mixing supplied ambient with sampled panorama/sky/ground. Use the actual foundation function, including brightness floor and clamp; it is not a generic scene-light intensity.

Category: foundation_renderer_input. Present in 117 canonical programs (one or more coverage states).

```glsl
uniform float ambient_light_sky_contribution;
```

## `cloth_transmission`

Original g_NPCClothParam.xyz weighting of cloth lighting terms. Preserve source values and the signed/gated response shown in this shader; this is not Godot PBR transmission.

Category: original_material_parameter. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform vec3 cloth_transmission;
```

## `depth_fade`

Original g_IgnoreLightParam.xy depth-fade control. Bind authored values and a nonzero Y when using depth-faded coverage. Uses stock scene depth, and its Punch preset has the documented transparent-depth-reader exception.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec2 depth_fade;
```

## `detail_diffuse_texture`

Second/detail diffuse layer of the source material, not a duplicate base input.

Category: source_material_texture. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform sampler2D detail_diffuse_texture;
```

## `detail_diffuse_uv_set`

Per-map UV selector for detail_diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform int detail_diffuse_uv_set : hint_range(0, 1) = 0;
```

## `detail_gloss_texture`

Second/detail original power/gloss layer, preserving its own channel contribution.

Category: source_material_texture. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform sampler2D detail_gloss_texture;
```

## `detail_gloss_uv_set`

Per-map UV selector for detail_gloss_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform int detail_gloss_uv_set : hint_range(0, 1) = 0;
```

## `detail_normal_texture`

Second/detail normal layer with its original blending/normalization path.

Category: source_material_texture. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform sampler2D detail_normal_texture;
```

## `detail_normal_uv_set`

Per-map UV selector for detail_normal_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform int detail_normal_uv_set : hint_range(0, 1) = 0;
```

## `diffuse_color`

Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6.

Category: original_material_parameter. Present in 152 canonical programs (one or more coverage states).

```glsl
uniform vec4 diffuse_color = vec4(1.0);
```

## `diffuse_texture`

Base diffuse/color texture; RGB and alpha use are source/state-specific.

Category: source_material_texture. Present in 149 canonical programs (one or more coverage states).

```glsl
uniform sampler2D diffuse_texture;
```

## `diffuse_uv_set`

Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 149 canonical programs (one or more coverage states).

```glsl
uniform int diffuse_uv_set : hint_range(0, 1) = 0;
```

## `edge_emission_color`

Original numeric edge-emission vector; controls the source edge/facing emission contribution through its recorded falloff expression.

Category: original_material_parameter. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform vec4 edge_emission_color;
```

## `emission_ambient`

Numeric original g_Ambient RGB for the source unlit/emission path, not the foundation panorama color picker.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec3 emission_ambient;
```

## `emission_mask_texture`

Original emission mask; channel use is source-specific and shown in the code uses.

Category: source_material_texture. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform sampler2D emission_mask_texture;
```

## `emission_mask_uv_set`

Per-map UV selector for emission_mask_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 2 canonical programs (one or more coverage states).

```glsl
uniform int emission_mask_uv_set : hint_range(0, 1) = 0;
```

## `emission_power`

Original numeric two-component emission control from mrgEmissionPower; source Z/Y packing is variant-defined and documented by the source review/code.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec2 emission_power;
```

## `emission_texture`

Original emission texture with source color/ambient/alpha weighting.

Category: source_material_texture. Present in 29 canonical programs (one or more coverage states).

```glsl
uniform sampler2D emission_texture;
```

## `emission_uv_set`

Per-map UV selector for emission_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 29 canonical programs (one or more coverage states).

```glsl
uniform int emission_uv_set : hint_range(0, 1) = 0;
```

## `environment_max_lod`

Last ORIGINAL cubemap mip level available in the assigned 4x3 atlas chain. Default 0 restricts sampling to base level, suitable for a single extracted cross image. Never include generated mixed-face tail mips.

Category: scene_or_texture_representation. Present in 24 canonical programs (one or more coverage states).

```glsl
uniform float environment_max_lod = 0.0;
```

## `environment_texture`

Original reflection/environment input as sampler2D. Its projection is recorded per shader: sphere, dual sphere, or 4x3 cross. Bind the matching representation; it is independent from sky_panorama.

Category: source_material_texture. Present in 87 canonical programs (one or more coverage states).

```glsl
uniform sampler2D environment_texture;
```

## `eye_highlight_color`

Raw original numeric authored-eye-highlight vector. Separate from camera fake_specular and native scene-light specular; never bind through a color picker.

Category: original_material_parameter. Present in 7 canonical programs (one or more coverage states).

```glsl
uniform vec4 eye_highlight_color;
```

## `eye_highlight_position`

Authored highlight position in exported object/model units, transformed by the shader vertex stage. It is not a light node or a color; preserve source coordinates and model-unit conversion.

Category: original_material_parameter. Present in 7 canonical programs (one or more coverage states).

```glsl
uniform vec3 eye_highlight_position;
```

## `falloff_color_edge`

Original numeric edge falloff color vector; blended with near color using the source falloff equation.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec4 falloff_color_edge;
```

## `falloff_color_near`

Original numeric near/facing falloff color vector; blended with edge color using the source falloff equation.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec4 falloff_color_near;
```

## `falloff_param`

Original two-component falloff control; exact exponent/scale/bias use is visible in the shader expressions.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec2 falloff_param;
```

## `falloff_texture`

Original falloff lookup texture, sampled using this variant's UV/data flow.

Category: source_material_texture. Present in 13 canonical programs (one or more coverage states).

```glsl
uniform sampler2D falloff_texture;
```

## `falloff_uv_set`

Per-map UV selector for falloff_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 13 canonical programs (one or more coverage states).

```glsl
uniform int falloff_uv_set : hint_range(0, 1) = 0;
```

## `gi_atlas`

Source baked-GI texture, sampled only in the selected GI branch. Not an environment reflection map. Requires the original atlas coordinates and encoding controls for correct static lighting.

Category: scene_or_texture_representation. Present in 85 canonical programs (one or more coverage states).

```glsl
uniform sampler2D gi_atlas : hint_white;
```

## `gi_rgb_scale`

Original baked-GI RGB decode scale after source squaring/saturation operations. Default (1,1,1). It is not a general material brightness fix.

Category: scene_or_texture_representation. Present in 85 canonical programs (one or more coverage states).

```glsl
uniform vec3 gi_rgb_scale = vec3(1.0);
```

## `gi_saturation`

Original baked-GI grayscale/color interpolation control. Default 1 keeps source chroma; see exact coefficients and clamp in the shader.

Category: scene_or_texture_representation. Present in 85 canonical programs (one or more coverage states).

```glsl
uniform float gi_saturation = 1.0;
```

## `gi_uv_offset`

Offset of the GI UV coordinates after scale. Default (0,0); bind original surface atlas rectangle offset.

Category: scene_or_texture_representation. Present in 89 canonical programs (one or more coverage states).

```glsl
uniform vec2 gi_uv_offset = vec2(0.0);
```

## `gi_uv_scale`

Scale of the GI UV coordinates before atlas sampling. Default (1,1); independent of diffuse/normal/gloss selectors.

Category: scene_or_texture_representation. Present in 89 canonical programs (one or more coverage states).

```glsl
uniform vec2 gi_uv_scale = vec2(1.0);
```

## `gloss_texture`

Original power/gloss map. This renamed sampler retains original channel/exponent/intensity use; eye variants can use multiple channels.

Category: source_material_texture. Present in 56 canonical programs (one or more coverage states).

```glsl
uniform sampler2D gloss_texture;
```

## `gloss_uv_set`

Per-map UV selector for gloss_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 56 canonical programs (one or more coverage states).

```glsl
uniform int gloss_uv_set : hint_range(0, 1) = 0;
```

## `ground_color`

Foundation lower-hemisphere lighting color. A color-picker input intentionally retained from the foundation; not an original numeric HE1 material vector.

Category: foundation_renderer_input. Present in 117 canonical programs (one or more coverage states).

```glsl
uniform vec4 ground_color : hint_color;
```

## `inner_max`

Foundation local shadow-region upper bounds, clamped componentwise to [0,1] in sdBox. Default (1,1,1).

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform vec3 inner_max = vec3(1.0);
```

## `inner_min`

Foundation local shadow-region lower bounds, clamped componentwise to [-1,0] in sdBox. Default (-1,-1,-1).

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform vec3 inner_min = vec3(-1.0);
```

## `inner_shadow_amount`

Foundation region-shadow strength inside the region; default 1. Applied through compute_shadow and the texture/box mask. With no valid region the shader bypasses this shadow.

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform float inner_shadow_amount : hint_range(0.0, 1.0, 0.05) = 1.0;
```

## `luminance_range`

Original environment HDR decode factor: sampled RGB is increased by RGB * sampled alpha * luminance_range in variants using that encoding. Zero disables the encoded extra range, not the base reflection.

Category: original_material_parameter. Present in 85 canonical programs (one or more coverage states).

```glsl
uniform float luminance_range;
```

## `mask_texture`

Original source material mask; consult the exact sample and downstream source operations for channel use.

Category: source_material_texture. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform sampler2D mask_texture;
```

## `mask_uv_set`

Per-map UV selector for mask_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform int mask_uv_set : hint_range(0, 1) = 0;
```

## `material_emission`

Source emission vector, mapped from the parameter identified in the per-shader source review. RGB/lane use and ambient multiplication differ among emission variants.

Category: original_material_parameter. Present in 20 canonical programs (one or more coverage states).

```glsl
uniform vec3 material_emission;
uniform vec4 material_emission;
```

## `material_opacity`

Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression.

Category: original_material_parameter. Present in 138 canonical programs (one or more coverage states).

```glsl
uniform float material_opacity = 1.0;
```

## `noise_power`

Original numeric two-component noise response. This name remains power because it is a source parameter, not a gloss sampler.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec2 noise_power;
```

## `noise_scroll_scale`

Original four-component noise scroll/scale control. TIME and UV dependencies, where present, remain in the listed expressions; use original material lanes.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec4 noise_scroll_scale;
```

## `noise_texture`

Original animated/procedural material noise input, using source scroll/scale/time coordinates.

Category: source_material_texture. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform sampler2D noise_texture;
```

## `normal_texture`

Source normal texture; preserve source channel packing. The linked source binormal sign is applied once; do not additionally invert green by assumption.

Category: source_material_texture. Present in 72 canonical programs (one or more coverage states).

```glsl
uniform sampler2D normal_texture;
```

## `normal_uv_set`

Per-map UV selector for normal_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 72 canonical programs (one or more coverage states).

```glsl
uniform int normal_uv_set : hint_range(0, 1) = 0;
```

## `outer_shadow_amount`

Foundation region-shadow strength outside the region; default 0. Not the native DirectionalLight shadow strength.

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform float outer_shadow_amount : hint_range(0.0, 1.0, 0.05) = 0.0;
```

## `planar_reflection_texture`

Source projected scene-reflection ViewportTexture. A correctly positioned reflection camera/viewport must exist externally; assigning an environment cross does not implement this path. Default black hint avoids an unbound bright reflection.

Category: scene_or_texture_representation. Present in 4 canonical programs (one or more coverage states).

```glsl
uniform sampler2D planar_reflection_texture : hint_black;
```

## `power_gloss_level`

Original numeric g_PowerGlossLevel vector; name is intentionally unchanged. Y controls exponent and Z controls applicable Generations intensity. Other used lanes are variant-defined; see expressions. This is not the renamed gloss texture.

Category: original_material_parameter. Present in 134 canonical programs (one or more coverage states).

```glsl
uniform vec4 power_gloss_level;
```

## `refraction_param`

Original numeric refraction displacement/response vector. Requires the refraction input expected by this variant; supplying this vector cannot generate a scene capture.

Category: original_material_parameter. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform vec3 refraction_param;
```

## `refraction_texture`

Original refraction input sampled by the source refraction path; not an environment cube or a generated capture.

Category: source_material_texture. Present in 1 canonical programs (one or more coverage states).

```glsl
uniform sampler2D refraction_texture;
```

## `region_transform`

Foundation region-to-world transform consumed by compute_shadow. Supply it only for a functioning region-shadow setup; a degenerate axis bypasses the region. It is a real mandatory integration input when that system is used.

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform mat4 region_transform;
```

## `shadow_texture`

Foundation region shadow mask. Red is inverted in sdBox; bind together with the region transform/bounds. Not an HE1 shadow-register dump or an automatically generated engine shadow map.

Category: foundation_renderer_input. Present in 145 canonical programs (one or more coverage states).

```glsl
uniform sampler2D shadow_texture;
```

## `sky_color`

Foundation upper-hemisphere lighting color. A color-picker input intentionally retained from the foundation; used by actual panorama/light-field or rim helpers where present.

Category: foundation_renderer_input. Present in 117 canonical programs (one or more coverage states).

```glsl
uniform vec4 sky_color : hint_color;
```

## `sky_panorama`

Foundation 2D panorama used by its five-sample light-field and reflected-direction rim functions. This input is distinct from environment_texture and is not a 4x3 cubemap cross.

Category: foundation_renderer_input. Present in 117 canonical programs (one or more coverage states).

```glsl
uniform sampler2D sky_panorama;
```

## `sonic_fresnel_param`

Original numeric two-component Fresnel parameter used by this variant. Exact scale/bias or exponent interpretation is recorded in its shader expressions.

Category: original_material_parameter. Present in 16 canonical programs (one or more coverage states).

```glsl
uniform vec2 sonic_fresnel_param = vec2(0.3, 1.0);
```

## `sonic_skin_falloff_param`

Original Sonic skin falloff/fresnel/material vector. Preserve the authored lanes; skin variants retain their own multi-exponent response rather than ordinary specular rules.

Category: original_material_parameter. Present in 24 canonical programs (one or more coverage states).

```glsl
uniform vec4 sonic_skin_falloff_param;
```

## `specular_color`

Original numeric material specular RGBA. RGB weights source specular and/or reflection terms as shown in this shader. Do not apply an sRGB color-picker conversion.

Category: original_material_parameter. Present in 134 canonical programs (one or more coverage states).

```glsl
uniform vec4 specular_color = vec4(1.0);
```

## `specular_texture`

Original RGB specular/material response and/or alpha reflection mask as this variant requires; it is not universally a scalar gloss map.

Category: source_material_texture. Present in 56 canonical programs (one or more coverage states).

```glsl
uniform sampler2D specular_texture;
```

## `specular_uv_set`

Per-map UV selector for specular_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required.

Category: material_uv_control. Present in 56 canonical programs (one or more coverage states).

```glsl
uniform int specular_uv_set : hint_range(0, 1) = 0;
```

## `use_gi_atlas`

Common material switch: false (default) uses actual foundation panorama light-field behavior; true selects the source baked-GI atlas branch. Bind valid GI texture/UV/decode data before enabling.

Category: common_lighting_selection. Present in 57 canonical programs (one or more coverage states).

```glsl
uniform bool use_gi_atlas = false;
```

## `uv0_offset`

2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels.

Category: material_uv_control. Present in 140 canonical programs (one or more coverage states).

```glsl
uniform vec2 uv0_offset;
```

## `uv1_offset`

2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh.

Category: material_uv_control. Present in 147 canonical programs (one or more coverage states).

```glsl
uniform vec2 uv1_offset;
```

