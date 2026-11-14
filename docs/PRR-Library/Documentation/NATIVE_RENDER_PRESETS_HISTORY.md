# Script-free shader library

Copy the library folder into your Godot 3.1.2 GLES3 project. In your own **ShaderMaterial**, assign the desired `.shader` through its normal **Shader** field. No plugin, script attachment, helper node, or engine change is required.

The top-level **Generations** and **Unleashed** folders contain the ordinary **Opaque / Normal / SingleSided** presets. Other settings are under:

`Render States / <Opaque, Punch, Trans> / <Normal, Additive, Multiply, Subtract> / <SingleSided, DoubleSided> / <game>`

Shader filenames keep their original identities. To change rendering state, assign the same filename from another preset folder. Existing matching texture/numeric inputs remain on your material. The optional **Materials** folders contain ordinary empty ShaderMaterial templates with the appropriate shader assigned; these also contain no scripts.

There are **152 material programs** (99 Generations, 53 Unleashed), four mapped exact aliases, and 24 native draw-state presets per program. The 3,648 preset files are rendering configurations of those programs, not additional conversions or NoGI/NoLight feature permutations.

## Alpha, blending, and sidedness

- **Opaque:** ignores material/texture alpha for coverage. Original opaque materials whose diffuse alpha is zero remain visible.
- **Punch:** discards pixels below **Alpha Cutoff**, default **0.502**, adjustable in Shader Params. Remaining pixels have full coverage.
- **Trans:** uses the original material alpha expression and native blending.
- **SingleSided:** native back-face culling. **DoubleSided:** native culling disabled, retaining the shader's established normal/lighting implementation.

With **Normal** blending, Opaque and Punch use the native opaque/depth-writing path and cast normal/cutout shadows; Trans uses native transparent sorting and the usual transparent depth/shadow restrictions.

Godot 3.1.2 sends **Additive, Multiply, and Subtract** through its transparent pass regardless of the coverage folder. Opaque/Punch still select full/cutout coverage, but cannot force those blends into Godot's opaque queue. They inherit native transparent sorting, depth, and shadow limits. These are native blend modes, not screen-copy imitations.

Native multiply computes destination RGB × source RGB and **does not fade RGB with source alpha** in Godot 3.1.2. Additive and subtractive blending use source alpha. No artificial alpha-dependent material equation was added to disguise this engine behavior.

One source-specific exception is `Generations/IgnoreLightV_d@s@.shader`: its depth-fade coverage requires the scene-depth copy when enabled. Punch retains that dependency and therefore uses the native screen/depth-reader transparent path. The Opaque specialization ignores alpha and removes the then-unused depth-fade code/input. This is recorded per preset in the manifest.

## Material interface

Every applicable texture retains its independent **UV 0/1** selector, default **0**, and its existing scale/offset controls. Godot's `UV` is UV 0; `UV2` is UV 1. The optional four-channel packed-mesh extension was removed at the user's request: **no `uv_data_texture`, `uv_data_texture_width`, UV 2/3 selector values, or UV 2/3 offset inputs** remain. No special UV-data preparation is needed.

Original material numeric vectors remain numeric, including diffuse/specular/highlight colors. Foundation-authored sky, ground and ambient color inputs retain their intended color hints. **power_gloss_level** keeps its original name. Sampler/UV names use **gloss**, and texture/selector ordering follows the shader suffix where applicable. Common **use_gi_atlas** defaults false; real GI resources remain necessary if you enable baked GI.

Opaque/Trans presets omit the unused alpha-cutoff control. Other inputs made dead by the chosen coverage mode are removed using the existing conservative data-flow analysis. Source-defined lighting, specular lobes, reflections, fake_specular, shadow-region, light-field and rim calculations remain unchanged by preset selection.

## Verification and limits

The final validation receipt and per-file source/math proofs are in **Validation**. Checks include 3,648 actual plain-resource/interface loads, 744 distinct full-source lit draws, 8,512 native-state GPU cases, 48 native shadow cases, 1,548 UV0/UV1/offset cases, and 152 native material switch/save/reload profiles. Of 456 HDR frame pairs, 433 are bit-identical; each of the other 23 differs at exactly one component by one adjacent finite half-float storage value. Maximum measured absolute difference is 0.00048828125. Raw diagnostic frames are preserved in the workspace; these differences are not called exact parity. Header-only blend/cull variants share full GPU source bodies; every native state is exercised separately by the analytic GPU suite. State results are carried through strict unchanged fragment/light/state proofs; UV/interface/full-source/HDR checks are rerun after the UV simplification.

Both real-asset projects were captured only after implementation and validation. All seven Generations images and five Unleashed images are pixel-identical to the accepted output. The other two Unleashed images each differ at one color component of one pixel by exactly one 8-bit step; this is reported explicitly alongside the measured HDR storage boundaries. Material values, textures, camera, pose, lighting, and tone mapping are unchanged. These comparisons test preservation of the accepted appearance and do not prove original-game parity for every material.

The immutable foundation and original HLSL/ASM files were not changed. Older libraries and failed-test evidence remain preserved in the project workspace. Prior documented stock-engine/source limitations remain, including the four Unleashed eye local-vs-directional lighting cases, original framebuffer/animation integration, native cubemap filtering/LOD boundaries, scene-resource requirements and unresolved source companions. A separately reported Generations eye-reflection visibility issue is queued for investigation after this render-state delivery; this package does not claim that issue is fixed.
