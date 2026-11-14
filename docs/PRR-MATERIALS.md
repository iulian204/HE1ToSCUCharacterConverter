# Project Re-Rainbow material export

**Export PRR materials** is optional and off by default. It exports Godot 3.1 `ShaderMaterial` resources using the supplied Project Re-Rainbow shaders. Leaving it off uses the existing `SpatialMaterial` conversion, including its texture processing, without changing that path.

Both games, mesh surfaces, separated mouths and generated Mouth_L use the same material selection. Settings persist independently of the game and mouth options. Use **GLES3** in the destination project: the supplied cube and multi-UV shaders use functions unavailable in Godot's GLES2 backend. Acorn compilation selects GLES3 for PRR materials automatically.

## Shader selection and distribution

The converter embeds the current **2026-11-14 Release**: **152 programs (99 Generations, 53 Unleashed), with all 3,648 supplied native draw-state presets**. Shader bytes come exclusively from `D:/HE1TOPRRShaders/Release/Generations`, `Unleashed` and `Render States`. The older `PRR shaders` folders are generator history and are no longer the active library. `inventory.json` records every preset hash; `bindings.json` retains 285 exact source identifiers and four canonical aliases. The import script validates the complete upstream release manifest before copying any files.

The supplied [library guides and per-shader catalog](PRR-Library/README.md) document the actual interface, mathematical policy, eye-reflection repair, render states, texture representation and known limitations. Their historical research notes are evidence, not a request to restore retired controls.

Matching uses the selected game, exact/case-insensitive names, recognized extensions, and explicit source-list aliases. Bracket and pixel-variant distinctions are retained: Unleashed `SonicEye_dpne[bcu]` resolves to `SonicEye_dpne@c@.shader`, while `[bu]` resolves to `SonicEye_dpne@@.shader`. There is no broad family substring guessing. Ambiguity is reported rather than resolved arbitrarily.

HE1 mesh slots select **Opaque**, **Punch** or **Trans** coverage. Material additive and double-sided flags select the matching **Additive/Normal** and **DoubleSided/SingleSided** preset. The converter copies the supplied file byte for byte; it does not patch shader headers or RGB math. Opaque ignores source alpha for coverage, Punch uses the material threshold, and Trans retains the original alpha expression. Non-Normal blends have Godot's native transparent-pass restrictions. Multiply/Subtract presets are included in the library, but the source material's boolean additive flag does not encode those modes.

All shaders are standalone, script-free embedded .NET resources, including in the portable build. No development shader directory is needed at runtime. Only selected presets are parsed/exported, and parsed shaders are cached. Unsupported shader includes remain explicit errors.

## Choose shader path

Leave **Choose shader path** empty for the recommended, self-contained export. Only unique shaders actually used by the model are written under `shaders/`; shared shaders are deduplicated. Materials use `../shaders/<game>/<filename>.shader` or `../shaders/Render States/<coverage>/<blend>/<sides>/<game>/<filename>.shader` and `../textures/prr/<filename>.texture.tres`. Raw PRR textures have their own subdirectory so mixed SpatialMaterial fallbacks cannot overwrite them with processed versions. Godot 3.1 resolves references relative to the material resource. Move the whole character directory together; no fixed universal shader directory is required.

Alternatively, select existing shaders inside a destination Godot project:

- `res://PRR` can refer to a copy of the complete new Release folder in that project.
- The folder picker accepts an absolute filesystem folder only when a parent `project.godot` identifies the resource root. The converter serializes its corresponding `res://` path, never a Windows drive path.
- Prefer the complete Release root so all game and native-state subdirectories resolve. A directly selected game/preset directory or a flat installation is also accepted when it contains the exact selected file. Without local project access, references use the complete Release hierarchy. A saved directory is retained when PRR export is disabled.
- When the project is accessible, every used external shader must exist and match the bundled program and uniform interface. Missing/incompatible files are configuration errors; they do not silently become SpatialMaterials. If required, first make a bundled export and install its render-mode variants alongside your project shaders.
- Text-only export permits an explicit `res://` directory whose project is not locally accessible, with an unverified-reference warning for every used shader. Its validity must then be checked in the destination project. Binary export requires access to the actual project shaders.

External mode does not copy shaders into the final character directory. For Acorn, verified shader files are placed temporarily at their expected paths inside the separate staging project. Shader, sampler and material resource paths are cleared before native embedding. A fresh process checks binary material types, shader code, parameter values and texture pixels against the text inputs. Temporary shader copies are removed after successful compilation. Binary-only relocation checks require no project shaders; editable text materials intentionally retain their external project references.

## Source parameters

Source values are preserved; fitted GGX roughness, metallic estimates and compensatory color adjustments belong exclusively to SpatialMaterial mode. Exact uniform names take precedence, followed by case-insensitive names and the verified aliases below. Values absent from source data leave PRR defaults intact.

| HE1 parameter | PRR uniform / lanes |
|---|---|
| `diffuse`, `ambient`, `specular` | `diffuse_color`, `ambient_light_color`, `specular_color` |
| `emissive` | `material_emission.xyz` only for vec3 Luminescence-style profiles |
| `mrgChrEmissionParam` | `material_emission.xyzw` only for enemy/IgnoreLight vec4 emission profiles; this is a different source register from `emissive` |
| `g_SonicSkinFalloffParam` | `sonic_skin_falloff_param` |
| `g_SonicEyeHighLightPosition` | `eye_highlight_position.xyz`, original authored values without mesh-scale multiplication |
| `g_SonicEyeHighLightColor` | `eye_highlight_color` |
| `g_NPCClothParam` | `cloth_transmission.xyz` |
| `mrgFresnelParam` | `sonic_fresnel_param.xy` |
| `mrgLuminanceRange` | `luminance_range.x` |
| `opacity_reflection_refraction_spectype` | `material_opacity.x`; remaining lanes recorded as source/runtime metadata |
| `g_IgnoreLightParam` | `depth_fade.xy`, only in coverage presets that declare it |
| `mrgEdgeEmissionParam` | `edge_emission_color.xyzw` |
| `g_FalloffColor0`, `g_FalloffColor1`, `g_FalloffParam` | `falloff_color_near.xyzw`, `falloff_color_edge.xyzw`, `falloff_param.xy` |
| `g_RefractionParam`, `g_NoiseScrollScale`, `g_NoisePower` | `refraction_param.xyz`, `noise_scroll_scale.xyzw`, `noise_power.xy` |
| `mrgEmissionPower` | `emission_power.zy`: source Z controls diffuse glow and Y controls rim |
| `g_GI0Scale`, `g_GI1Scale` | `gi_rgb_scale.xyz`; `gi_saturation.w` where declared. Retired ambient-shadow W remains in the raw audit. |

IgnoreLight profiles exposing `emission_ambient` receive source ambient RGB there. Punch presets alone receive the source threshold as `alpha_cutoff`. **The new library declares original numeric diffuse/specular/highlight inputs without color hints: these use Godot `Plane`, preserving all four authored components, including W=0.** This prevents an unintended renderer color conversion. It does not undo the earlier type-aware fix: a genuinely color-hinted uniform still uses `Color`. RGB-only HE1 ambient mapped to a foundation Color receives alpha 1; genuine authored RGBA values remain intact. Vec2/vec3 use Vector2/Vector3. Zero, negative and HDR values are retained without approximation, gamma compensation or arbitrary clamping. Eye-highlight XYZ remains unscaled as requested.

Removed camera/force-alpha/shadow-register controls are no longer emitted. Their source records remain classified in the audit. Scene GI/reflection/shadow inputs cannot be fabricated from a character material.

Mirage v1/v3 has three separate tables: big-endian IEEE float4 (16 bytes), signed int4 (16 bytes) and byte bool4 (4 bytes), each with independent counts and value arrays. The reader now retains all three, original names/flags, unnamed slots and duplicate payloads. Existing float dictionary behavior remains available to SpatialMaterial. PRR classifies duplicates targeting a real uniform as ambiguous rather than silently choosing the last value.

Float uniforms receive real literals such as `1.0`, not integer literals. Integer inputs must be integral and fit signed 32 bits; boolean inputs must be explicit 0/1. Nonrepresentable integer-to-float casts fail. Packed scalar inputs need a verified lane rule; an occupied Y/Z/W is not blindly truncated to X. Nonfinite data is rejected. Exact-name vector arity and the explicit aliases above determine selected lanes.

None of the supplied uniforms is an array. Multi-vector source arrays retain every row in the audit and are unsupported without a verified binding/layout contract. Explicit matrix adapters support mat2/Transform2D, mat3/Basis and affine mat4/Transform, with row ordering tested in Acorn. Projective mat4 values cannot be represented by Godot 3.1 Transform and are rejected. The library's `region_transform` is a scene matrix; unknown source float4 arrays are not guessed to be that matrix.

## Sky and Ground defaults

The actual uniforms are `sky_color` and `ground_color`, both vec4 `hint_color`, present in 117 of the 152 shaders. Applicable materials receive `Color(0.5, 0.5, 0.5, 1)` and `Color(0.25, 0.25, 0.25, 1)`. Verified authored values for those exact uniforms take precedence, including zero RGB/alpha and HDR values. Unrelated shader defaults remain unchanged. The audit identifies `HE1`, `ConverterDefault`, `MaterialHeader` and `TextureMetadata` origins separately.

## Independent parameter validation

Every PRR export writes **`prr-parameter-audit.json`**. It includes every source record, raw arrays/storage/flags, source shader/file, destination type/hint, exact expected Variant/value/literal, mapping evidence/confidence, classifications, sampler metadata and runtime readbacks. Classifications distinguish mapped, intentionally unused, missing mapping, unsupported, ambiguous and failed conversion. Defaults have their own assignments; they are not misreported as original HE1 values. Fallback material parameters and textures are retained too.

With Acorn selected, source-derived expectations are created **before** text serialization. Acorn checks declared property types, actual Variant types and assigned values against those expectations, then checks raw source-derived sampler mip bytes and flags. A fresh process repeats the comparisons on binary resources. Floating values use absolute `1e-6` plus relative `2e-6` tolerance; integer/bool values use exact comparisons. Material/shader snapshots additionally check binary retention. Corrupt Color types, black substitutions, integer-form float values, wrong integer values, zeroed vectors and altered sampler pixels are covered by executed native negative tests.

Text-only exports without an editor have expected values/classifications but **are not runtime verified**: assignment `runtimeStatus` and summary counts say so explicitly. A successful load or text-to-binary round trip alone is not counted as correctness. Temporary independent-validation payloads are deduplicated and removed from successful exports.

[Full shader/uniform inventory](PRR-SHADER-UNIFORMS.json) records all three coverage interfaces for each of the 152 programs, actual types/hints/default expressions, usage snippets and Godot 3.1.2/Acorn property checks. Source-preservation tests are distinct from visual/source-renderer parity.

## Textures

Declared samplers are matched to source texture roles: diffuse, specular, normal, displacement/falloff, reflection/environment, gloss/power, emission, alpha, mask, noise, detail and refraction. HE1 gloss/power binds the new `gloss_texture` / `detail_gloss_texture` names without roughness baking. Each declared `<map>_uv_set` receives that texture's UV index; only UV0/UV1 are supported by this library. `conversion.json` records the assignments and warnings. Original channels and DDS mip levels are decoded without roughness baking, normal reconstruction, sRGB conversion or material-dependent recoloring. Missing textures produce diagnostics and retain shader defaults. Identical source texture/settings bindings are deduplicated.

UV0/UV1 selectors are supplied where the shader supports them. A shader exposing only one shared UV selector cannot represent conflicting source sampler UV sets; that incompatibility is an explicit error. UV2/UV3 source sets are not present in the existing ArrayMesh pipeline; affected bindings are warned and left at shader defaults. Mixed-axis, mirror-once and border wrapping cannot be preserved by Godot 3.1 ImageTexture flags and are explicitly unsupported, rather than silently changed to repeat. Repeat, mirror and clamp are preserved when representable. HE1 texture entries have no filter field; linear filtering and mipmaps are explicit converter policy.

Cube environment DDS files are packed into the shader's 4×3 face atlas (+X, −X, +Y, −Y, +Z, −Z). Original face mip levels are kept isolated; `environment_max_lod` prevents sampling the mixed-face tail levels required by Godot's image storage. This preserves the supplied PRR sampling contract, while native HE1 cube seam/implicit-LOD behavior remains a shader-library limitation.

## Fallbacks, diagnostics and limitations

A missing compatible PRR implementation falls back **per material** to the existing SpatialMaterial writer. Missing original material files retain the neutral fallback. Mixed material meshes are supported. Logs identify material, game, original shader, reason and final type. Invalid external paths, incompatible source UV bindings, unsupported shader syntax and broken resources are errors, not disguised missing-shader fallbacks.

The manifest records PRR enablement, game, reference mode, override, per-material types and shader paths, parameter/sampler mappings, unmapped parameters, warnings and fallback reasons. The log summarizes shader/fallback counts, unique bundled/external shaders, missing shaders/textures and unmapped inputs.

**Preserving material inputs is not verified original-game visual parity.** Scene panorama/light-field colors, baked GI, region shadows, camera/runtime state and planar-reflection viewports remain project inputs. The supplied library documents unresolved local-light behavior for four Unleashed eye profiles, framebuffer transfer differences, animated eye/morph companions and missing original source links. This feature does not alter those shaders to hide those limitations or add game runtime systems.

CLI equivalents:

```powershell
dotnet run --project src/HE1ToSCU.Cli -- character.model new-export --export-prr-materials
dotnet run --project src/HE1ToSCU.Cli -- character.model new-export --game unleashed --export-prr-materials --prr-shader-path "D:\MyProject\PRR" --acorn "D:\Acorn\editor.exe"
```

`--prr-shader-path` without `--export-prr-materials` is rejected. Existing CLI invocations and conversion requests default to SpatialMaterial mode.

## Updating the embedded release

Run `scripts/Import-PrrRelease.ps1 -ReleaseDirectory <verified Release folder>` after reviewing the new library documentation. It verifies all upstream file hashes, imports byte-exact presets and derives aliases from the release catalog. Shader templates are not copied into individual character exports. Regression and native resource tests must be rerun before distributing the rebuilt application.
