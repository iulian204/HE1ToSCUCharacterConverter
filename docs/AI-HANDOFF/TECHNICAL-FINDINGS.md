# Technical Findings

## PRR material pipeline

The native PRR path is implemented in `src/HE1ToSCU.Core/ConversionService.cs` and `PrrMaterialWriter.cs`. Source `.material` data is read by `He1MaterialReader.Read(path, game)` into `He1Material`; PRR selection happens against the selected game library in `PrrShaderLibrary`; `PrrParameterMapper.Map` discovers the selected shader's declared uniforms and produces typed values; `PrrMaterialWriter` writes `.tres`, sampler textures and an audit record; `PrrParameterAuditReport` writes expected independent values; Acorn scripts from `PrrRuntimeValidation` load text and freshly compiled binary resources and report actual Godot Variants and texture data. Readback is meaningful evidence for serialization, not proof that the selected semantic mapping matches the original game's rendered image.

### HE1 material data representation

- `He1MaterialReader.cs` preserves the material version/header, shader/subshader identifier, flags, texture table, named float-vector parameter arrays, and native/raw parameter records. Float parameters are read as arrays of four IEEE float components (`float4`, 16 bytes each); a name can therefore carry multiple rows. Native integer/unnamed/duplicate slots are retained separately instead of silently replacing a named row. See `He1Parameter`, `He1Material.AllParameters`, and `Read` near lines 6–75.
- The convenience `He1Material.Parameters` dictionary is `Vector4[]`; that shape does **not** make every source row a color. W can be a packed scalar, intensity, alpha, unused lane, or other shader-specific value. Preserve raw storage, flags, rows, and values in diagnostics.
- Source game and material version matter. Do not assume Generations and Unleashed have identical meanings because a parameter identifier matches.
- Confirm the parser's exact offsets and interpretation against `references/HedgeLib/HedgeLib/src/materials/hl_hh_material.cpp`, current `FORMAT-EVIDENCE.md`, and actual materials; do not infer semantics from the C# type alone.

### Uniform parser and serialization

- `PrrShaderLibrary.ParseUniforms` strips comments and parses Godot 3 shader uniform declarations including type, name, declaration, hint, default, and array notation. It validates `shader_type spatial`. Review parser coverage against shader files before expanding it; arrays/matrix types can be parsed but are not necessarily source-bindable or valid for the available Godot 3.1 Variant assignment contract.
- `PrrUniformValue.Convert` in `PrrUniformValue.cs` maps supported scalar/int/bool/vector declarations to Godot 3 Variant literals: `float`, exact `int`, explicit 0/1 `bool`, `Vector2`, `Vector3`, `Color` for `vec4:hint_color`, and `Plane` for ordinary `vec4`. It rejects non-finite values, nonintegral integer values, invalid booleans, nonrepresentable integer precision, and unsupported types rather than coercing silently. Matrix helper exists for verified layouts but generic HE1 matrix arrays are not guessed.
- For RGB-only source values sent to hint_color, Color alpha is explicitly 1 and source W is retained in raw audit. Genuine RGBA parameters must not be normalized this way without evidence.
- Godot text serialization can change apparent numeric forms or Variants if the wrong constructor is chosen. The runtime harness checks declared property type, Variant type, all components (including alpha), tolerance, sampler dimensions/flags/raw mip pixels after text load and fresh binary reload.

### Texture path

`PrrMaterialWriter.cs` binds only explicit sampler aliases or exact texture-role names. It deduplicates texture pixels, preserves source DDS RGBA/mips instead of SpatialMaterial roughness/repacking, records UV selectors when the shader declares them, and reports unknown/multiple/missing/unsupported roles. Limits are explicit: Godot texture wrap modes cannot represent every HE1 mixed-axis/mirror-once/border combination; UV indices outside 0/1 or mismatched UVs for one shared shader selector are not silently faked. Check sampler names against actual selected PRR variant, not a family-wide guess.

### Current high-confidence PRR semantic findings

1. HedgeLib interprets diffuse, ambient, specular and emissive material colors as RGB (`as_vec3()`); original Sonic eye pixel shader consumes `g_SonicEyeHighLightColor.xyz`. Therefore source W is not Color alpha for those inputs; emit alpha 1, keep W in raw audit.
2. `g_SonicEyeHighLightPosition` is authored `(2,-3,2.5)` for the cited EyeL case and the user's own comparison found those original values visually correct. Do not multiply this parameter by the model position scale; export `Vector3(2,-3,2.5)`. The earlier `(20,-30,25)` was wrong. This does not establish visual equivalence for all eye shaders.
3. `g_SonicEyeLight_Range` / release aliases can have scale semantics distinct from the eye highlight position. Do not remove a separately evidenced range scaling when fixing position.
4. Enemy emission registers differ: `mrgChrEmissionParam` is used by enemy shader paths and is not interchangeable with generic `g_Emissive`; exact `vec3`/`vec4` destination presence determines their current mapping.
5. `g_GI0Scale.xyz` and `.w` are consumed separately by PRR adapters as `gi_rgb_scale` and `ambient_shadow_scale`; do not map the whole packed register indiscriminately.
6. `mrgEmissionPower` on NightEnemy uses source Z for masked diffuse and Y for rim; the current mapping intentionally emits `Vector2(z,y)`.
7. The converter-requested sky/ground defaults use only declared `sky_color`/`ground_color`; source exact-name authored values win. Do not confuse those environment terms with diffuse/ambient material color.

Full current alias table is in `PrrParameterMapper.cs`. Existing inventory and semantic notes: `docs/PRR-PARAMETER-AUDIT.md`, `docs/PRR-SHADER-UNIFORMS.json`, `docs/PRR-MATERIALS.md`, `docs/FORMAT-EVIDENCE.md`, `docs/VALIDATION.md`.

## SCU naming/matching work in current tree

- Real reference directories: Sonic `D:\SCUExtract\characters\player_Sonic`; Super Sonic `D:\SCUExtract\characters\player_Supersonic`. `scripts/Build-ScuInventory.py` parses actual scene files, file names/hash metadata, mesh/skeleton structure, embedded clips, external references, and optional original PCK v1 indexes/remaps. It uses the local Acorn executable to decode animations; extracted data contains metadata/descriptors, not copyrighted game binaries.
- `scripts/Extract-ScuAnimationMetadata.gd` successfully decoded 382 target `.anim` files using Acorn. Target counts: Sonic 304 total, 303 humanoid; Super Sonic 78 total, 75 humanoid. Four effect-rig clips are excluded from humanoid scoring due to their separate rigs / absence of playable Skeleton transform tracks.
- Target Sonic scene: `chr_Sonic_SD.tscn`, root `Scene Root`, Skeleton `Skeleton`, main mesh node/resource `Sonic_Body`. Target Super scene: `chr_SuperSonic_SD.tscn`, same root/Skeleton names, main mesh node/resource `mesh`.
- Original game `sonic3.pck` contains `.tscn.converted.res` scene resources; `sonic5.pck` contains `.tscn.remap` files that map `.tscn` to `.converted.res`. PCK index parsing was checked against Acorn/Godot `core/io/file_access_pack.cpp`. `ConversionService` defers writing the `.tscn.remap` until after native binary compilation so the staging text scene is not redirected to a not-yet-existing binary.
- Animation metadata stores native duration, native loop flag, editor step, track paths/types/counts, interpolation/enabled data, sample timing, normalized 17-phase skeletal descriptors (6 semantic anchors), and optional root trajectory. Do not call editor-step/key count an authored frame count or assert a reliable FPS where none was present. `ssn_boost_loop` is a concrete counterexample: name says loop while native loop flag is false. Renaming must not alter source loop/timing/transforms.
- Source cache builder parsed 544 files (325 Generations, 219 Unleashed) and generated 1,088 game/profile candidate records; cache uses inventory/scoring version and motion fingerprint. This cache has not been evaluated by a full matching quality report.
- Matcher combines normalized/action/phase/direction/context/form, duration, pose and root features, conflict caps and deterministic global Hungarian assignment. It automatically assigns low-confidence candidates and reuses a source only for remaining target slots. These algorithm choices are implemented but not test-verified yet.
- Target inventory JSON current content version: `2026-11-14.7da219239e39`; cache schema 1/scoring schema 1; output files: `assets/SCU/sonic-inventory.json`, `animation-profiles.json`, `source-profile-provenance.json`.

## What not to claim

- A C# unit test or Godot text/binary round trip is not visual parity or original-game shader semantics.
- Source and target animation name similarity is not proof of gameplay equivalence; no user playback review of automatic matches has occurred.
- Resource naming alone is not full character replacement, rig retargeting, attachment compatibility, gameplay integration, or SCU runtime acceptance.
- The latest actual PRR audit report records no runtime mismatches for its tested material set; it does not certify every material/shader combination in the library or the completeness of source semantics.
