# PRR Parameter Mapping Record

This is the mapping evidence available at handoff. Current implementation is centralized in `src/HE1ToSCU.Core/PrrParameterMapper.cs`; verify each alias against the actual selected shader, source game, source HLSL/assembly, and original parameter reader before extending it. “Implemented” means the mapper has a rule and a regression test/report path; it does not imply every game shader was checked for visual parity.

| Source/game/family | HE1 source field | Destination PRR uniform/type | Lanes / transform | Evidence and status |
|---|---|---|---|---|
| Generations + Unleashed character material RGB color fields | diffuse | diffuse_color, usually vec4 hint_color | source XYZ -> Color RGB, alpha=1; preserve source W only in raw audit | Confirmed via HedgeLib `as_vec3()` and shader use; implemented/tested. |
| Both, shader-specific ambient | ambient | emission_ambient if declared, otherwise ambient_light_color | XYZW for destination; RGB-only hint_color alpha=1 where source semantics are RGB | Current mapper conditional; review each shader because ambient/emission behavior differs. |
| Both, specular | specular | specular_color if declared | RGB-only values become hint_color alpha 1; otherwise exact source/destination type rules | Alias exists; shader-specific usage still needs complete inventory audit. |
| Both, ordinary generic emission | emissive | material_emission only for destination vec3 | XYZ | Keep distinct from enemy `mrgChrEmissionParam`; implemented. |
| Both, enemy emission shaders | mrgChrEmissionParam | material_emission when destination is vec4 | XYZW | Enemy path adds RGB and multiplies by W; do not exchange with generic emissive. Implemented; confirm shader family individually. |
| Sonic eye materials, Generations/Unleashed | g_SonicEyeHighLightPosition | eye_highlight_position, vec3 | XYZ unchanged; **no Spatial.Scale multiplication** | User concrete value 2,-3,2.5; prior x10 error corrected. Current Acorn test asserts exact vector. |
| Sonic eye materials | g_SonicEyeHighLightColor | eye_highlight_color, commonly hint_color | XYZ -> RGB, alpha=1 for RGB-only source | Original pixel shader uses `.xyz`; implementation/tests cover. |
| Sonic skin | g_SonicSkinFalloffParam | sonic_skin_falloff_param | XYZW | Explicit alias, based on supplied PRR release shader/source parameter contract; needs exhaustive variant audit. |
| Cloth | g_NPCClothParam | cloth_transmission | XYZ | Explicit alias; validate which games/shaders declare and consume it. |
| Fresnel material family | mrgFresnelParam | sonic_fresnel_param | XY | Explicit alias; confirm per shader. |
| Material family with luminance range | mrgLuminanceRange | luminance_range | X | Explicit alias; confirm per shader. |
| Material family with in-shadow scale | mrgInShadowScale | environment_shadow_scale | XZ | Explicit packed-component mapping; confirm across shader families. |
| General opacity/reflection/refraction flags | opacity_reflection_refraction_spectype | material_opacity | X | Explicit alias; destination meaning is shader-specific and incomplete without per-shader evidence. |
| Eye light | mrgEyeLight_Range | eye_light_range | ZW, current binding applies `Spatial.Scale` | Separate from eye-highlight position; re-verify intended scale/source units. |
| Eye light | mrgEyeLight_Attribute | eye_light_attribute | XYZW | Explicit alias. |
| Forced alpha | g_ForceAlphaColor | force_alpha_color | XYZW | Explicit alias. |
| GI | g_GI0Scale | gi_rgb_scale and ambient_shadow_scale | XYZ and W respectively | Shader adapter consumes components separately; both destinations must exist and raw W stays auditable. |
| GI | g_GI1Scale | gi_saturation | W | Explicit alias; confirm each consuming shader. |
| Depth/ignore-light family | g_IgnoreLightParam | depth_fade | XY | Explicit alias; verify destination declaration. |
| Edge emission | mrgEdgeEmissionParam | edge_emission_color | XYZW | Explicit alias; semantics need shader-family check. |
| Falloff | g_FalloffColor0 / g_FalloffColor1 | falloff_color_near / falloff_color_edge | XYZW | Explicit aliases. |
| Falloff | g_FalloffParam | falloff_param | XY | Explicit alias. |
| Refraction | g_RefractionParam | refraction_param | XYZ | Explicit alias. |
| Noise | g_NoiseScrollScale | noise_scroll_scale | XYZW | Explicit alias; ensure actual shader and source use same order. |
| Noise | g_NoisePower | noise_power | XY | Explicit alias. |
| NightEnemy material | mrgEmissionPower | emission_power vec2 | ZY (masked diffuse, rim) | Current source shader inspection confirms ordering; test exists. |
| Any selected shader exact uniform | any exact-name source parameter | same declared scalar/vector uniform | XYZW truncated only to declared vector arity; scalar X only when YZW are zero; occupied packed lanes rejected absent rule | Generic exact path implemented; exact name alone is not proof of semantic equivalence. |
| Converter PRR environment defaults | no explicit source parameter | sky_color / ground_color, declared type | 0.5 / 0.25; RGB grayscale + alpha 1 for hint_color | Explicit user request; source mapped value wins; only declared exact names. |
| Material header | alphaThreshold byte | alpha_cutoff | byte / 255 | Format-level mapping implemented and tested. |

## Generic conversion constraints

- `float`: explicit scalar and valid float literal.
- `int`: requires exact integral 32-bit value, not rounding.
- `bool`: requires source value exactly 0 or 1.
- `vec2/vec3/vec4`: destination arity uses an explicit source lane string; do not treat arbitrary packed Vector4 as RGB or assume X for every scalar.
- `vec4 : hint_color`: serialize as Godot Color, not Plane. RGB source fields use alpha 1; verified RGBA uses all four lanes.
- ordinary `vec4`: Godot Plane in this implementation. Confirm valid target semantics at each shader declaration.
- matrices and multi-row source arrays: no generic layout is assumed. Unsupported/ambiguous inputs should be reported with all original rows retained; no “take first element” fallback.
- Non-finite values, invalid bools, int precision loss, duplicate parameter names, and multiple source bindings to one destination should fail/classify, not silently become zero/black.
- Texture roles use a centralized sampler alias map in `PrrMaterialWriter.cs`; exact texture role is also supported. No gloss-to-roughness bake in PRR mode.

## Known historical wrong rules / regression targets

- Wrong: RGB float4 W=0 copied as hint_color alpha, making colors black/transparent. Fixed: source is RGB by evidence; alpha 1 and raw W audit.
- Wrong: eye highlight XYZ multiplied by model spatial scale 10, producing `(20,-30,25)` instead of authored `(2,-3,2.5)`. Fixed in current alias table and test. Do not revive the scale multiplication.
- The user says source values for the cited eye highlight look correct. Do not reapply speculative normalization/scaling because a scene coordinate-scale argument appears plausible.
- The full parameter list from source materials is not equivalent to the set mapped: currently many are intentionally unused per a selected shader, but every “intentionally unused” classification should be grounded in declarations and usage before asserting correctness.
