# Generations eye-reflection repair — November 14, 2026

The script-free native render-state library and native UV0/UV1 cleanup were finished and exported before this investigation began, as requested. This repair changes only the three canonical Generations eye programs and their 72 native-state presets.

## Finding

The original reflection texture was bound and sampled. Actual supplied eye meshes contain a deliberately small reflection mask in vertex alpha: approximately 0.125 on average and at most 0.27451. It is not correct to override that mask or brighten the texture.

The original `ChrEye_dpne@@`, `ChrEye_dpne1@@`, and `ChrEye_dpne2@@` pixel sources add their environment, ambient, and authored-highlight radiance before final vertex RGB/output. The adaptation kept the foundation's individual contribution squares but omitted their interactions. It therefore computed `A² + R² + H²`, instead of preserving the source addition through the existing PRR transfer as `A² + R² + H² + 2AR + 2AH + 2RH`.

The repair retains each actual foundation square and restores only those pairwise terms. It does not add a final-output square, a brightness multiplier, a new light, or a reflection compatibility uniform. Vertex-alpha masking, Fresnel, source-defined projection, gloss interpretation, normal decoding, native light(), the foundation's helpers, and both fake_specular blocks are unchanged. In particular, **Generations fake specular remains 0.8 before its square**, independent of the Unleashed 0.2 policy.

## Source and assets

- Original HLSL: `he1shaders/gens hlsl/ChrEye_dpne@@.wpu.hlsl`, `ChrEye_dpne1@@.wpu.hlsl`, and `ChrEye_dpne2@@.wpu.hlsl`.
- Original vertex pairing: `ChrDefault_Eye_@n@.wvu.hlsl`; source-list morph companion remains separately documented.
- Supplied Sonic uses **e1**, the sphere-map projection. The original `chr_sonic_white_ref_HD.dds` is bound to both eye materials, with original luminance range 1 and unchanged numeric material vectors. Cube e2 and dual-sphere e projections were source/GPU tested without changing their lookup functions.
- Asset audit: `comparison-generations/eye-asset-audit-2026-11-14.json`.

## Validation

- 432 original-HLSL fixture cases independently executed across the three projections, varying environment mask, HDR range, ambient input, vertex RGB and authored highlight.
- 864 baseline/corrected GPU cases passed in stock Godot 3.1.2 GLES3. The baseline is also tested against its own independent-squares oracle; it is not incorrectly described as failing to compile.
- All 72 affected plain material resources passed interface checks; all 15 distinct full GPU bodies passed lit draws. The three legacy generator-input outputs also passed parsing/lit draw and foundation enforcement.
- Exact generator/native-candidate comparisons preserve helpers, vertex, light, uniforms, alpha expressions and fake-specular policy. The generation pipeline and foundation checker now require the same source-backed eye interactions.
- Actual assets were rendered only after these checks. Geometry, original texture bytes, material values, lighting, camera, pose, and tone mapping were held fixed.
- Combined changes are confined to the eye meshes. Isolated fake specular, scene specular, standalone reflection, and authored highlight are pixel-identical. This is an accumulation correction, not a stronger individual reflection/highlight term.
- In the true eye-only mask (24,727 pixels), removing reflection affected **zero displayed eye pixels before** and **13,713 after**, with at most **12 RGB levels** of reflection contribution. The total combined repair also restores highlight/ambient interaction, so its maximum change is larger; that additional source-required term is not mislabeled as reflection alone.

The isolated reflection render remains subdued because it removes the very ambient/highlight interactions restored here. Use combined versus no_reflection to judge the reflection's contribution to the full material.

Evidence: `validation/generations-eye-fragment-addition-v4-2026-11-14`, `validation/generations-eye-repair-library-v2-2026-11-14`, and `comparison-generations/comparisons/generations-eye-addition-2026-11-14`.

## Retained boundaries and historical evidence

The numerical square describes the established PRR transfer policy; exact original-game framebuffer encoding, full native-light accumulator coupling, exposure, animation/morph and other documented renderer boundaries are not declared solved. The source sky helper's existing minimum brightness and clamp remain unchanged. Test fields were chosen within its matching range; an earlier oracle incorrectly expected a dim field below that minimum to remain dim and was rejected.

Source evidence and failed stages are preserved. A constructor-parenthesis bug in the shared interaction-term generator was fixed and covered by three tests. A staged generator-routing error was caught by independent output comparison before publication; the correct separate Generations/Unleashed fake-specular policies are now covered by a dedicated three-projection pipeline test. Windows HLSL text-normalized and physical-byte hashes are distinguished explicitly. A diagnostic eye mask was recaptured against black so grey background pixels could not contaminate eye-only visibility statistics.

The original foundation file, original HLSL/ASM, and Godot engine source remain unchanged. All other 149 canonical materials, including every Unleashed shader, remain unchanged by this eye repair. No plugin or runtime script is included in the updated editable library.
