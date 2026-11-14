# Rendering architecture and mathematical policy

The immutable foundation is the user's `PRR shaders/ChrSkin_dsnfe1.shader`, SHA-256 `43a2723f858ace10624b70d2be52fb5e0b378ecc9e8536073e4d740faaba9d46`. An exact reference copy is included as [original-foundation.shader.txt](../Validation/original-foundation.shader.txt), not as an additional assignable program.

Sonic, Chr, Common and enemy shaders default to its actual rendering infrastructure: custom `light()`, region shadow, panorama light field, source-relevant rim, Fresnel/falloff, normal decoding, vertex space conventions, per-contribution squaring and fake camera specular. Function bodies are retained where applicable. Source-backed modifications/removals and the actual function diff are recorded per shader; function-name matching alone is not treated as proof. **145 programs are foundation-lit. Seven IgnoreLight programs are source-unlit exceptions.** Luminescence is not automatically unlit.

## Ordinary gloss/specular equations

For applicable Generations ordinary shaders:

```text
P = clamp(gloss * power_gloss_level.y * 500, 1, 1024)
brightness = gloss * power_gloss_level.z * 5
```

An ordinary mapless Generations material has no ordinary gloss-map specular. Source exceptions remain, including Luminescence's constant-gloss behavior. Do not apply one generic rule to all programs.

For applicable ordinary Unleashed shaders:

```text
gloss = sampled gloss, or 1 when the source has no gloss map
P = clamp(gloss * exp2(power_gloss_level.y * 10 + 2), 1, 512)
brightness = P * 0.125
```

SonicSkin preserves its two exponent factors1.2/0.2, weights0.4/0.6 and weighted-exponent gain0.0625. DayEnemy preserves its source0.3/20 mixture and `(maximum_power + 1) * 0.025` gain. Eyes retain separate outer/iris/authored-highlight operations and source-specific signs, masks and channel uses. These are material responses, not additional scene-light systems.

## Native renderer transfer

Exactly one final foundation `SPECULAR_LIGHT` gain100 remains. Source-derived material equations are separate from the shader adapter for stock Godot's downstream light/BRDF transfer. The accepted adapter divides the native input by PI and the measured residual1.5696, derived from `100 * (0.4524 * 0.04 - 0.0024)`. This corrects the measured roughly4.931× native transfer rather than weakening authored gloss/specular values. The full original-source controls and batch GPU proof are retained in the project evidence.

The gain100 and scene-light compensation are **not applied to fake_specular**. Generations retains the foundation **0.8 before squaring** policy; Unleashed retains its accepted **0.2 before squaring** camera policy. Older historical reports described the placement differently; the actual current shader and source/GPU-verified input order are authoritative. No extra camera-light implementation or brightness uniform is delivered.

## Contribution order and color

The foundation squares diffuse/specular/rim/environment/light-field contributions at their established stages; squaring the final ALBEDO is not a substitute for those operations. Source-backed fragment interaction terms are restored where proven, including Unleashed and the three Generations eyes. The eye result does not justify silently modifying all non-eye Generations materials. Source direct/ambient tint differences remain game/variant-specific. Raw material numeric vectors do not use color hints; foundation-authored colors retain them.

The panorama helper keeps its actual five samples, hue/brightness normalization, sky/ground interpolation, minimum brightness0.5 and output clamp0.8. Those are intentional PRR foundation integration choices, not a claim of exact HE1 light-field reproduction in every scene. Native diffuse/rim handling has not received a blanket PI correction.

The original BINORMAL sign is applied once where required. Eye RG/BA normal packing and authored-highlight transform/order retain their source-specific behavior. Environment lookup, reflection masking and GI are documented in their own guides. No original HLSL/ASM or Godot engine source was changed.
