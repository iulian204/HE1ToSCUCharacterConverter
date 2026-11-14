# Known limits and verification boundaries

All152 in-scope base implementations exist. This is **not** a claim that every original engine resource, framebuffer stage or scene effect is reproduced. Implementation, compilation, foundation compliance, source-operation tests, runtime bindings and visual comparison are separate statuses.

## Stock renderer and source behavior

- Four Unleashed eye programs (`SonicEye_dpe@@`, `SonicEye_dpe@c@`, `SonicEye_dpne@@`, `SonicEye_dpne@c@`) retain the known local-light diffuse boundary. Their source distinguishes directional diffuse from local specular-only response; stock3.1.2 custom `light()` has no supported LIGHT_IS_DIRECTIONAL discriminator. No engine patch or guessed heuristic is used.
- Original full-frame accumulation/transfer, framebuffer cap/scattering and exact scene-light aggregation remain incomplete. Source operations tested in isolation do not establish original-game framebuffer or exposure parity.
- Signed cloth directional behavior versus local/camera gating, original camera-light diffuse/range/cone integration, animated eye-highlight skinning and NodeInvX/morph companion behavior retain documented partial coverage. Optional engine-only scene register controls were removed from the final material interface rather than exposed as misleading material knobs.
- The existing source-list audit records ten unresolved in-scope companion links. They are included verbatim in [unresolved-source-links.json](../Inventory/unresolved-source-links.json). Deficient Dummy vertex companions do not justify fabricating semantics or another implementation. Canonical shaders already exist; these are source-evidence limitations.
- Native implicit cubemap LOD and face filtering differ at some high-gradient/seam cases from the 2D atlas representation. Existing direction/material/mip checks do not justify arbitrary mip bias or reflection gains.
- The Generations eye source-addition repair is verified. Broader non-eye aggregation differences have not been inferred from it or silently rewritten.

## Inputs the shader cannot generate

Original material textures/numeric values, an appropriate mesh/tangent basis/UVs, original animation data, actual baked GI, projected-reflection captures and meaningful region-shadow configuration must be supplied by the scene/material. Native lights, camera, exposure and tone mapping must match for equal-input comparisons. A ShaderMaterial template is not a prebound asset or a renderer setup. The source game assets and full comparison projects are not redistributed in this shader release.

No optional UV-data texture system remains. Only UV0 and UV1 are supported. Source instancing/NoGI/NoLight/ConstTexCoord/PAM permutations are excluded intentionally. Native draw-state variants remain because coverage/blending/culling require real pipeline state.

## Native render-state constraints

Non-Normal blends enter stock Godot's transparent pass even under an Opaque/Punch coverage folder. Native multiply ignores alpha in RGB multiplication. Transparent sorting/depth-writing/shadow behavior and the source depth-reader IgnoreLight exception remain native-engine constraints; see [RENDER_STATES](RENDER_STATES.md). Double-sided is a culling choice, not a new BRDF.

## Visual evidence

Actual-asset controlled previews cover representative Sonic materials, not every shader. The supplied screenshots use differing camera, scene lighting, GI, shadow and exposure conditions across their history. Historical legacy GLVL references were superseded by the user's accepted GLVL Plus/15:03 baseline. Do not tune original material numbers to fit an uncontrolled screenshot.

The packaged comparison images establish preservation of accepted Godot output and the intentional eye correction under recorded conditions. They do not establish universal original-game image identity. Small measured half-float or display-rounding differences are explicitly reported, not called bit-identical. No outstanding shader-math issue is concealed by a general visual-parity claim.
