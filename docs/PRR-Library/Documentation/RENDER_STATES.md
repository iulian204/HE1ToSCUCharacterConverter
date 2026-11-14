# Native render states

The easiest correct script-free method in stock Godot 3.1.2 is a fixed native-state shader file. Uniforms cannot switch native blend functions, opaque/transparent queue classification, depth policy or hardware culling. Reimplementing those with SCREEN_TEXTURE does not provide correct overlapping transparency. This release uses the real pipeline state and leaves all material shading in shader code.

There are 3 coverage modes × 4 blends × 2 sidedness choices = **24 presets per base shader**. The default `Opaque/Normal/SingleSided` files are at the top-level game directories; the other 23 are under `Render States`. Their `.tres` templates are ordinary native ShaderMaterials with no embedded script.

## Coverage

| Folder | Meaning | Alpha control |
|---|---|---|
| Opaque | Texture/material alpha does not affect coverage | No cutoff or transparency switch |
| Punch | Discard when the source alpha is below `alpha_cutoff`; surviving pixels have full coverage | Editable cutoff, default **0.502** |
| Trans | Preserve original source alpha and native blend | Original texture/material alpha expression, listed per shader |

The original source alpha expression is recorded for every preset. It may combine diffuse alpha, original diffuse-vector W, material_opacity and source-specific masks. Opaque correctly allows source diffuse W=0. Trans with the same W can be transparent by design. Cutoff0.502 is a Punch threshold, **not a universal material opacity default**.

## Blend modes

Let S be shaded source RGB, D destination RGB, and a the source coverage alpha used by the native state. The controlled stock-engine probes verify these RGB equations:

| Folder | Native mode | RGB equation |
|---|---|---|
| Normal | `blend_mix` | `a*S + (1-a)*D` (opaque surviving coverage uses a=1) |
| Additive | `blend_add` | `D + a*S` |
| Multiply | `blend_mul` | `D*S` |
| Subtract | `blend_sub` | `D - a*S` |

Native multiply in this Godot version does **not** alpha-weight its RGB multiplication. No invented alpha-aware multiply is applied. LDR/framebuffer processing can clamp output; these are native blend equations, not guarantees about display clipping. Punch discard still gates whether a fragment reaches any blend mode.

## Sidedness, depth and shadows

SingleSided uses native `cull_back`; DoubleSided uses `cull_disabled`. Double-sided rendering permits back faces; it is not a separate two-sided BRDF or an automatic normal reversal. All files use native `depth_draw_opaque`.

Normal Opaque/Punch ordinarily use the opaque queue and depth writes, including their native shadow behavior. **All non-Normal blend modes force the transparent pass in stock 3.1.2**, even under an Opaque/Punch coverage folder. They retain the engine's transparent sorting, depth-writing and shadow limitations. The folder coverage describes alpha/discard, not a promise that additive rendering can remain in the native opaque queue.

One source-specific exception is `Generations/IgnoreLightV_d@s@.shader`: its source depth-fade requires `DEPTH_TEXTURE` when coverage is used. Opaque specialization removes that unused alpha/depth path; Punch retains it, so that Punch file is a native depth-reader alpha-pass material. Every preset records `native_alpha_pass`, `screen_depth_reader` and `native_depth_policy` in [presets.csv](../Inventory/presets.csv).

The validation includes 8,512 blend/coverage/front-back/custom-cutoff cases, 48 actual native-shadow cases, 152 shader-switch/save/reload profiles and nested-folder resource tests. No shader-only emulation, engine changes or user-installed plugin is needed.
