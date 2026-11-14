# Unleashed: IgnoreLight_d@v@

[Inventory](../../SHADERS.md) · [Default shader](../../../Unleashed/IgnoreLight_d@v@.shader)

Architecture: **source-unlit**. Environment: **none**.

## Selecting and binding

Use the same filename from the desired native-state folder. Material numeric vectors are raw data; copy original values without color conversion. No optional engine effect registers are required. Supply any actual foundation/scene textures used below. See [setup](../../../Documentation/USAGE.md) and [renderer limits](../../../Documentation/LIMITATIONS.md).

## Source-defined behavior

- Original IgnoreLight pixel has no light, shadow, GI, Fresnel or normal material contribution. Independent unshaded path is source-required, not a family-name shortcut.
- Retain original texture/material/vertex multiplication, optional alpha or emission, force RGBA and cap at 4 before the established gamma-to-linear material transfer.
- Four authored UV lanes use the existing optional float vertex-UV table binding. Godot supplies skinning and blend shapes.

These source-review notes retain historical mathematical findings. References to removed optional camera/forced-color controls or old helper interfaces describe history, not the final uniform API. The declarations below are authoritative.

## Coverage and native draw state

Source alpha before state specialization: `min(diffuse.a * diffuse_color.a * material_opacity * vColor.a, 4.0)`.

Opaque ignores coverage alpha. Punch discards below alpha_cutoff (default 0.502) then uses full coverage. Trans writes the source alpha. Blend/culling use real native state. No material script or state-changing uniform is involved.

## Parameters

Tables follow shader declaration order. Godot 3.1.2 Inspector displays numeric controls before samplers while retaining their relative order. No explicit initializer means the shader does not declare an authored material default; bind original material/renderer inputs.

### Opaque

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |

### Punch

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `material_opacity` | float | 1.0 / none | Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression. | fragment |
| `alpha_cutoff` | float | 0.502 / hint_range(0.0, 1.0) | Punch-only discard threshold, default 0.502. Source material alpha below this is discarded; surviving pixels have full coverage. Not present in Opaque or Trans. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |

### Trans

| Parameter | Type | Explicit default / hint | Role | Stage path |
|---|---|---|---|---|
| `diffuse_texture` | sampler2D | not declared / none | Base diffuse/color texture; RGB and alpha use are source/state-specific. | fragment |
| `diffuse_uv_set` | int | 0 / hint_range(0, 1) | Per-map UV selector for diffuse_texture: 0 = mesh UV0 (Godot UV), 1 = mesh UV1 (Godot UV2). Default 0; corresponding uv0_offset or uv1_offset is applied. No external UV data texture or channels 2/3 are required. | fragment |
| `diffuse_color` | vec4 | vec4(1.0) / none | Original numeric material diffuse RGBA. RGB tints the source-specific direct/surface response; ambient tinting differs by game/variant. W participates only where the selected alpha expression uses it. Preserve authored values, including 0.6. | fragment |
| `material_opacity` | float | 1.0 / none | Original material opacity factor retained only when source coverage needs it in Punch/Trans. It does not turn an opaque shader transparent. Default 1; combine with texture/material alpha according to the listed expression. | fragment |
| `uv0_offset` | vec2 | not declared / none | 2D offset added to mesh UV0 before per-texture channel selection. Default has no explicit initializer. These are texture coordinates, not pixels. | vertex |
| `uv1_offset` | vec2 | not declared / none | 2D offset added to mesh UV1 (Godot UV2) before per-texture channel selection. Default has no explicit initializer. UV1 must exist in the mesh. | vertex |

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

### `diffuse_color`

Category: original_material_parameter. Source binding: g_Diffuse / material diffuse.

```glsl
uniform vec4 diffuse_color = vec4(1.0);
// Referenced in fragment
vec3 radiance = min(diffuse.rgb * diffuse_color.rgb * vColor.rgb, vec3(4.0));
float material_alpha = min(diffuse.a * diffuse_color.a * material_opacity * vColor.a, 4.0);
```

### `material_opacity`

Category: original_material_parameter. Source binding: g_OpacityReflectionRefractionSpectype.x.

```glsl
uniform float material_opacity = 1.0;
// Referenced in fragment
float material_alpha = min(diffuse.a * diffuse_color.a * material_opacity * vColor.a, 4.0);
```

### `uv0_offset`

Category: material_uv_control. 

```glsl
uniform vec2 uv0_offset;
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV, second_uv) + vec4(uv0_offset, uv1_offset);
```

### `uv1_offset`

Category: material_uv_control. 

```glsl
uniform vec2 uv1_offset;
// Referenced in vertex
uv_coordinates_0_1 = vec4(UV, second_uv) + vec4(uv0_offset, uv1_offset);
```

### `alpha_cutoff`

Category: native_cutout_coverage. 

```glsl
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.502;
// Referenced in fragment
if (material_alpha < alpha_cutoff)
```

## Foundation evidence

Preserved functions: .

Modified functions: `fragment`, `vertex`.

Removed functions: `compute_falloff`, `compute_fresnel`, `compute_shadow`, `decode_normal_map`, `get_light_field_color`, `get_rim_light_color`, `getq`, `light`, `sdBox`.

Introduced functions: `get_material_uv`.

Modified/removed functions are not assumed valid merely because their names match. Source enforcement checks actual bodies; final exact-regeneration proofs cover render-state/UV specializations. Documented source differences:

- `architecture`: Original IgnoreLight pixel has no light, shadow, GI, Fresnel or normal material contribution. Independent unshaded path is source-required, not a family-name shortcut.

## Original source mapping

Paths below are relative to the original project root and are not bundled game source files.

- Pixel: `he1shaders/unleashed asm/IgnoreLight_d@v@.xpu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@buv@.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@bv@.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@uv@.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@v@.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@buv@p.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@bv@p.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@uv@p.xvu.asm`
- Vertex companion: `he1shaders/unleashed asm/IgnoreLight_@v@p.xvu.asm`

| Shader list | Pixel mode | Vertex mode | Vertex identity |
|---|---|---|---|
| he1shaders/unleashed hlsl/IgnoreLight_d[buv].shader-list | default | none | IgnoreLight_@buv@ |
| he1shaders/unleashed hlsl/IgnoreLight_d[bv].shader-list | default | none | IgnoreLight_@bv@ |
| he1shaders/unleashed hlsl/IgnoreLight_d[uv].shader-list | default | none | IgnoreLight_@uv@ |
| he1shaders/unleashed hlsl/IgnoreLight_d[v].shader-list | default | none | IgnoreLight_@v@ |
| he1shaders/unleashed hlsl/IgnoreLight_d[buv].shader-list | default | p | IgnoreLight_@buv@p |
| he1shaders/unleashed hlsl/IgnoreLight_d[bv].shader-list | default | p | IgnoreLight_@bv@p |
| he1shaders/unleashed hlsl/IgnoreLight_d[uv].shader-list | default | p | IgnoreLight_@uv@p |
| he1shaders/unleashed hlsl/IgnoreLight_d[v].shader-list | default | p | IgnoreLight_@v@p |

## Validation status

Final artifact: exact source-generator/native-state/two-UV regeneration and dependency audit passed. Whole-library stock-engine results are in [VALIDATION](../../../Documentation/VALIDATION.md).

Source mathematics: source-backed material equations retained; neutral engine-effect specialization and per-map UV routes verified; full renderer equivalence remains partial

Visual validation: {'status': 'not original-game image parity'}

Original-game renderer and appearance equivalence is not claimed. [Known boundaries](../../../Documentation/LIMITATIONS.md) apply.

## Available preset paths

| Coverage | Blend | Faces | Shader | Optional material |
|---|---|---|---|---|
| Opaque | Additive | DoubleSided | [Shader](../../../Render%20States/Opaque/Additive/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Additive/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Additive | SingleSided | [Shader](../../../Render%20States/Opaque/Additive/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Additive/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Multiply | DoubleSided | [Shader](../../../Render%20States/Opaque/Multiply/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Multiply/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Multiply | SingleSided | [Shader](../../../Render%20States/Opaque/Multiply/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Multiply/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Normal | DoubleSided | [Shader](../../../Render%20States/Opaque/Normal/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Normal/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Subtract | DoubleSided | [Shader](../../../Render%20States/Opaque/Subtract/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Subtract/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Subtract | SingleSided | [Shader](../../../Render%20States/Opaque/Subtract/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Opaque/Subtract/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Additive | DoubleSided | [Shader](../../../Render%20States/Punch/Additive/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Additive/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Additive | SingleSided | [Shader](../../../Render%20States/Punch/Additive/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Additive/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Multiply | DoubleSided | [Shader](../../../Render%20States/Punch/Multiply/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Multiply/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Multiply | SingleSided | [Shader](../../../Render%20States/Punch/Multiply/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Multiply/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Normal | DoubleSided | [Shader](../../../Render%20States/Punch/Normal/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Normal/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Normal | SingleSided | [Shader](../../../Render%20States/Punch/Normal/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Normal/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Subtract | DoubleSided | [Shader](../../../Render%20States/Punch/Subtract/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Subtract/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Punch | Subtract | SingleSided | [Shader](../../../Render%20States/Punch/Subtract/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Punch/Subtract/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Additive | DoubleSided | [Shader](../../../Render%20States/Trans/Additive/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Additive/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Additive | SingleSided | [Shader](../../../Render%20States/Trans/Additive/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Additive/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Multiply | DoubleSided | [Shader](../../../Render%20States/Trans/Multiply/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Multiply/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Multiply | SingleSided | [Shader](../../../Render%20States/Trans/Multiply/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Multiply/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Normal | DoubleSided | [Shader](../../../Render%20States/Trans/Normal/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Normal/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Normal | SingleSided | [Shader](../../../Render%20States/Trans/Normal/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Normal/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Subtract | DoubleSided | [Shader](../../../Render%20States/Trans/Subtract/DoubleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Subtract/DoubleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Trans | Subtract | SingleSided | [Shader](../../../Render%20States/Trans/Subtract/SingleSided/Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Render%20States/Trans/Subtract/SingleSided/Materials/Unleashed/IgnoreLight_d@v@.tres) |
| Opaque | Normal | SingleSided | [Shader](../../../Unleashed/IgnoreLight_d@v@.shader) | [Material](../../../Materials/Unleashed/IgnoreLight_d@v@.tres) |
