# Validation results

The sections below retain historical checks. The latest release evidence is at the end.

## Initial 0.1 build and automated checks

- .NET SDK: 8.0.418; target runtime: .NET 8; UI: WPF; Windows x64.
- Release solution build: **0 warnings, 0 errors**.
- Automated tests: **13 passed, 0 failed, 0 skipped**.
- Self-contained single-file Windows application published and launched successfully.

## Real Generations assets

Read from the user-supplied source directory:

`D:\SteamLibrary\steamapps\common\Sonic Generations\disk\bb3\Sonic`

Input identity hashes are in [source-validation-hashes.json](source-validation-hashes.json). No source game files are included in the source or portable-app archives.

| Asset | Observed result |
|---|---|
| `chr_Sonic_HD.model` | Standard Mirage v5; 13 surfaces, 10,156 vertices, 114 model bones |
| `chr_Sonic_HD.skl.hkx` | 32-bit Havok 2010.2.0 packfile; 114 reference bones |
| `sn_idle_loop.anm.hkx` | Spline animation; 161 frames, 114 bound quaternion tracks, approximately 2.6667 seconds |
| `sn_idle03.anm.hkx` | Spline animation; 551 frames across three blocks, 114 bound tracks, approximately 9.1667 seconds |

The completed example export is `artifacts/Sonic-example/character.scn`, with editable text resources and independent binary mesh/animation resources alongside it.

## Acorn checks

Used the specified `BSE_Editor_Acorn1.1.1.exe`, identifying itself as `Acorn Engine v1.0.0.stable.custom_build.609b955`, on the local GLES2 renderer.

1. Loaded generated text resources and instantiated the scene.
2. Confirmed skeleton/surface counts and nonempty vertex/index arrays.
3. Sampled each animation and checked finite bone poses.
4. Compared **every bone's global position and basis** against managed decoded transforms at the first, middle and penultimate sampled frames. Tolerance: 0.02 SCU units for positions, 0.003 per basis axis vector for rotation/scale.
5. Saved native binary PackedScene, ArrayMesh and Animation resources. Checked Godot binary magic.
6. A **second engine process**, without the first process's resource cache, loaded the binary scene and independent mesh/animation files, checked material presence, and repeated the sampled transform comparisons.
7. A separate relocation check copied only `.scn`, `.mesh` and `.anim` files into a fresh temporary Godot project, then loaded and played them successfully without the text resources.

Expected success markers:

```text
HE1SCU_VALIDATED bones=114 surfaces=13 animations=2
HE1SCU_BINARY_VALIDATED bones=114 surfaces=13 animations=2
RELOCATION_OK bones=114 surfaces=13 animations=2
```

## GUI smoke check

The published executable was launched using the computer-use skill. The window layout and controls were inspected. The source-folder textbox scanned the supplied folder on Enter and found 19 models. Model selection populated the matching skeleton path. A model/skeleton conversion launched through **Convert character** completed with 100% progress and successful native compile/fresh-process validation, producing `artifacts/GUI-smoke-export`.

The file picker opened successfully. The animation conversion scenarios above were exercised through the CLI, which uses exactly the same `ConversionService` as WPF. End-to-end animation selection through the native file dialog was not automated.

## Issues found and corrected during validation

- Godot 3.1 `SceneTree.quit()` accepts no exit-code argument.
- Godot text parsing needs lowercase numeric exponents.
- Text `Transform` basis ordering differs from the GDScript vector constructor. Corrected matrix transposition; numerical pose checks catch this regression.
- Acorn can emit relative paths containing `res://./` that fail internal material subresource cache lookup on a fresh load. The binary scene embeds its resources; mesh material paths are reset before native serialization. Fresh-process and relocation checks verify the workaround.
- Multi-block spline decoding uses the file's actual block offsets and overlapping frame boundaries.

## Not established

The initial checks did not establish proprietary Havok sampler parity, exhaustive codec/visual coverage, Unleashed binary-HKX support, gameplay retargeting, exact shader reproduction, in-game acceptance or renderer parity. Unleashed support and broader animation regression coverage were subsequently added in version 0.4; the other boundaries still apply.

## Version 0.2 regression: user-reported batch

The supplied `HE1ToSCU.log` listed 45 attempted animation files and stopped on `sn_dead.anm.hkx` with the explicit extracted-root-motion rejection. Those exact 45 input files were replayed successfully against `chr_Sonic_HD.model` and its skeleton. The export at `artifacts/reported-batch-fixed` passed both Acorn processes and a separate binary-only relocation check:

```text
HE1SCU_VALIDATED bones=114 surfaces=13 animations=45
HE1SCU_BINARY_VALIDATED bones=114 surfaces=13 animations=45
RELOCATION_OK bones=114 surfaces=13 animations=45
```

Filenames are now `chr_Sonic_HD.scn`, `chr_Sonic_HD.mesh`, `sn_dead.anim`, etc., with matching named text resources and scene references. The 91 root-motion samples from `sn_dead` are retained. Five additional regression tests bring the suite to 18 tests, covering original-name references/collisions, XML motion semantics, axis-angle interpolation, complete turns, and clearing root motion for in-place clips. Binary motion decoding is validated with the real source file; XML and sampling have synthetic fixtures.

An additional synthetic clip with nonzero XYZ motion and a 90-degree heading was exported on the real model to `artifacts/root-motion-nonzero-proof`; both Acorn processes passed root translation/rotation comparisons at intermediate times. This exercises the new motion track beyond the nearly constant motion in `sn_dead`.

## Version 0.3: textured boost and full batch

- Release tests: **23 passed, 0 failed**. New coverage checks scale-compensated model-space transforms and original bind preservation, source winding, material float/texture entries, DDS alpha/block decoding and SpatialMaterial mapping.
- `artifacts/v0.3-validated` contains the previous 45 input clips plus `sn_idle_loop` and `sn_idle03`, with original filenames.
- Original mesh: 13 surfaces, 10,156 vertices and 114 original bones. Nonuniform animation scales require 113 compensation bones, bringing the exported skeleton to 227 bones. Original bone indexes and skin palettes remain unchanged.
- Nine original HE1 materials were parsed and assigned their source textures. Both native compile and fresh-process binary validation passed for all 47 clips.
- All **81** sampled frames of `sn_boost_loop` were compared, including the endpoint with looping temporarily disabled. Other clips receive first/middle/penultimate checks. Both passes compare every bone's position and basis and the animated model root.
- Binary-only relocation passed after copying only `.scn`, `.mesh` and `.anim` into an independent temporary project. Textures/materials remain embedded and load without editable source resources.
- Acorn rendered boost frames 0, 6, 7, 8, 47 and 48 into `artifacts/render-preview`. Frames 7 and 48 were inspected: textured, outward-facing surfaces and no long-leg stretch. This is a targeted visual check, not exhaustive visual validation of all clips.

```text
HE1SCU_VALIDATED bones=227 surfaces=13 animations=47
HE1SCU_BINARY_VALIDATED bones=227 surfaces=13 animations=47
RELOCATION_OK bones=227 surfaces=13 animations=47
```

## Version 0.4 final regression — 2026-11-04

The final source build completed with **0 warnings and 0 errors**. All **63 automated tests** passed (62 core and 1 WPF/STA GUI test), including both game profiles, game selection feeding the conversion request, the source-folder button layout, signed/zero scales, sparse/reference/additive handling, continuous interpolation, material parsing, power clamps and gloss/roughness behavior. The GUI test instantiates the actual window and verifies both options and retained workflow controls; native file dialogs were not exhaustively automated.

| Final character export | Generations | Unleashed |
|---|---:|---:|
| Supplied source model | `chr_Sonic_HD.model` (v5) | `SonicRoot.model` (v5) |
| Vertices / surfaces | 10,156 / 13 | 13,021 / 13 |
| Original / exported bones | 114 / 170 | 111 / 161 |
| Matching skeleton | Havok 2010.2, 32-bit LE | Havok 5.5, 32-bit BE container |
| Animations | 325 | 219 |
| Source-derived pose checks per engine process | 58,193 | 46,991 |
| Maximum position error (SCU units) | 0.012900 | 0.011079 |
| Maximum basis-axis error | 0.000823 | 0.002078 |
| Text and fresh-process binary checks | Passed | Passed |
| Binary-only relocation/playback checks | Passed | Passed |

Both banks were regenerated from the final converter and source folders, including `C:\Users\jigai\Desktop\CorrectSUSonic\Sonic` for Unleashed. The errors remain below the 0.02 SCU position / 0.003 basis-axis thresholds. Checks cover every bone at source and adaptive keys, intermediate times and inferred loop boundaries; they compare against the managed decoder's continuously sampled source transforms through an independently composed matrix oracle. They are not an independent proprietary Havok sampler comparison. The previously reported idle/jet/boost cases use the same general logic as the rest of the bank.

Final evidence is retained locally under `artifacts/v0.4-release-validation-generations` and `artifacts/v0.4-release-validation-unleashed`, with sibling `.log` files. A parser audit also read all **19 Generations models / 32 materials** and **16 Unleashed models / 19 materials** in the supplied folders with zero failures, including real v2/v4/v5 layouts. This broader parser audit does not imply full animated or visual validation of every effect model.

Final text materials/textures are byte-identical to the reviewed Generations feedback-8 and Unleashed feedback-6 exports. Selected character/material renders and earlier per-clip midframe renders were inspected during the session. This is not exhaustive visual inspection of every frame.

Additional material checks:

- Real Generations board `_pow` textures: native compile and fresh-process binary load passed for two surfaces/materials, alongside both character material sets.
- Stock **Godot 3.1.2 official**, GLES2: all **20** character/board materials loaded as SpatialMaterial; base specular remained bounded; HDR clearcoat strength/gloss survived a bundled binary save/load unchanged.
- All **655,360 top-mip texels across seven baked roughness textures** matched the game-specific power/clamp/roughness calculation exactly after RGBA8 quantization. All had opaque alpha and linear texture flags.
- Automated tests exercise both games' full 0..255 gloss range, specular-mask independence, power limits, zero/positive brightness, fitted response bounds and unchanged roughness when direct highlight strength changes.

Large banks retain companion `.anim` files. Binary-only relocation copied those alongside the scene and mesh and played all 544 clips successfully. No temporary compiler scripts or source-pose binaries remained in successful exports.

Still unverified: SCU gameplay acceptance/retargeting, DX11/custom renderer parity, every Havok platform/codec variant and pixel-identical original lighting. Additive clips are standalone reference-pose bakes; original runtime overlays and camera-light lobes are outside the material/animation approximation. Supported output and required companion files are documented in README.md.

The self-contained Windows x64 portable executable was also launched successfully with product version 0.4.0. This checks packaged startup separately from the WPF/STA test; full end-user interaction through every native dialog remains outside the automated checks.

## Mouth mesh preview — 2026-11-04

- Build succeeded with zero warnings/errors; **69 tests passed** (68 core, one GUI). Added coverage for independent mouth branches sharing a material, descendant/teeth geometry, case variants, ignored locator/zero-weight slots, conservative boundary assignment, unchanged faces/weights/attributes, shared skeleton references, original animation paths and output-name collisions.
- Real Generations Sonic: body **15,888 triangles / 9,310 vertices**; `Mouth` and `Mouth_L` each **705 triangles / 423 vertices**, including teeth. Combined totals remain **17,298 triangles / 10,156 vertices**. Twelve body surfaces and two surfaces per mouth yield sixteen surfaces across three mesh nodes.
- Real Unleashed Sonic: body **18,408 triangles / 12,160 vertices**; `Mouth` **1,482 triangles / 861 vertices**. Combined totals remain **19,890 triangles / 13,021 vertices**, with thirteen surfaces across two mesh nodes.
- The real-asset audit matched every output face to its original ordered Vertex objects, preserving winding, every attribute and all skin weights/indexes. Materials/textures for both games match v0.4.0 byte-for-byte.
- Both complete banks (**325 + 219 animations**) passed text/native compile, fresh-process binary verification and source-pose comparisons. The expected body/mouth surface counts and each MeshInstance's shared Skeleton binding are checked. Pose counts/error bounds remain unchanged from v0.4.0.
- Binary-only relocation checks passed for all three Generations meshes and both Unleashed meshes, with all 544 animations. Every mesh has an independent `.mesh` resource and is embedded in its character scene. Existing resource/in-game validation boundaries still apply.

Local evidence: `artifacts/mouth-split-generations`, `artifacts/mouth-split-unleashed` and their sibling logs. This preview is a local development build following v0.4.0.

## Optional mirrored Mouth_L preview — 2026-11-04

- **75 automated tests passed** (74 core, one WPF/STA GUI). The game-dependent checkbox visibility and gated request flag are tested. Geometry tests check reflected normals, reversed winding, tangent handedness, weight-index remapping, unchanged UVs/colors, positive instance transforms, hidden default, idempotence and name collisions. A skinning oracle covers head/body rotation, moving parents, nonuniform scales and signed scale crossing through zero, followed by the adaptive/scale-control stage.
- All **219 Unleashed clips** were independently compared at **23,655 source-key/intermediate/block-boundary times**. All original sampled bone poses stayed unchanged. The five generated bones' skin matrices matched the analytical parent-frame reflection with maximum element error **3.81469727e-6**. This includes the additive/reference-baked `sonic_mouth_reverse` clip.
- Enabled Unleashed full-bank export: **111 original + 5 generated + 51 control bones = 167**, three meshes, fourteen surfaces, 219 animations. Text and fresh binary processes each passed **47,003 pose checks**, with maximum original/global position error 0.011079 SCU units and basis-axis error 0.002078. Both verify the generated mesh is hidden and bound to the same Skeleton.
- Generations regression: all **325 clips / 58,193 pose checks per process** passed, retaining 170 bones and the authored three-mesh layout. Maximum errors remain 0.012900 SCU position / 0.000823 basis-axis. The generator is not run for this profile.
- Binary-only relocation/playback passed for both complete banks. Static Unleashed exports also passed with the option off (111 bones/two meshes) and on (116 bones/three meshes). The value-free CLI flag was exercised as the last argument.
- Original body/mouth text meshes match byte-for-byte between static off/on exports, and both games' materials/textures match the preceding mouth-split preview byte-for-byte. Native renders checked original and alternate mouths in idle and mouth-reversal poses; the original alone is visible on initial load. These targeted visual checks do not claim exhaustive per-frame pixel parity or in-game SCU acceptance.

Local evidence: `artifacts/mirrored-mouth-unleashed`, `artifacts/mirrored-mouth-generations-regression`, `artifacts/mirrored-mouth-static-on`, `artifacts/mirrored-mouth-static-off`, `artifacts/mirrored-mouth-animation-audit.log` and `artifacts/mirrored-mouth-renders`.

## Version 0.5.0 release finalization — 2026-11-04

The complete mouth separation/mirroring diff was reviewed after user acceptance. Finalization changed version metadata and release documentation only; the validated conversion behavior was retained. The final Release solution build has zero warnings/errors, and all 75 automated tests pass.

The 219-clip independent mirrored-skinning audit was rerun successfully (23,655 samples, maximum skin-matrix error 3.81469727e-6, original tracks unchanged). Fresh binary-only relocation/playback checks again passed for all 219 Unleashed and 325 Generations animations in the approved full-bank exports. Their text/fresh-binary pose-check results remain recorded above. The versioned v0.5.0 CLI also generated and fresh-process validated new static Generations and mirrored Unleashed exports; manifests report the correct converter version and mirror option.

Release packaging uses a self-contained Windows x64 application and a source archive of the release commit. Both archives are checked for integrity and exclusion of game assets, references, diagnostic output and build intermediates. Published artifact digests are checked against local SHA-256 values. Materials remain standard SpatialMaterial; in-game SCU acceptance and gameplay retargeting are not established by these checks.

## Additional Generations character regression — 2026-11-05

Tested the `EnemyEFighter`, `silver`, `rouge` and `tails` folders under the extracted Generations `disk/bb2` directory using the development build with the Shadow tangent fix. Each animated model used its matching skeleton. Acorn validated text and fresh binary resources and compared source-derived key, subframe and loop-boundary poses.

| Asset | Models exported | Animations passed | Pose samples per process | Unsupported clip |
|---|---:|---:|---:|---|
| Egg Fighter | 7 | 41/41 | 8,055 | |
| Silver | 1 | 15/16 | 4,826 | `sv_idle_loop` |
| Rouge | 1 | 17/18 | 7,504 | `ru_pam_pose` |
| Tails | 1 | 21/22 | 7,302 | `tl_pam_pose` |

The three excluded clips use `hkaQuantizedAnimation`. Full-bank attempts stopped at those clips with the unsupported-codec error. The supported sets were rerun by passing each remaining clip explicitly. Egg Fighter's sword and five breakable pieces were exported separately from its animated model. Silver and Rouge each produced two mouth meshes. Tails produced one mouth branch mesh that also contains the nose surface weighted to that branch.

Binary-only relocation passed for all ten scenes, including material type checks and midpoint playback of all 94 exported clips. Local evidence is in `artifacts/character-regression-20261105`. Visual parity and in-game playback were outside these checks.

## UV2 normal and masked metallic regression — 2026-11-05

- All 85 automated tests passed. Acorn validated 13 native exports and their binary-only relocation, including Egg Fighter, its sword and breakable pieces, the Generations shock bar, Silver, Rouge, Tails, and Sonic from both games. The 94 supported clips in the earlier character regression passed again.
- Fresh Sonic, Silver, Rouge and Tails text resources matched the previous exports byte-for-byte. Egg Fighter's native body materials reloaded with detail normals on UV2 and specular alpha bound to the metallic channel. The shock bar exercised the same metallic rule through a separate asset.
- The Egg Fighter and Generations Sonic source meshes measure 2.090 and 1.103 units high at rest. Their exports measure 20.902 and 11.032, preserving the source height ratio of 1.895. Egg Fighter's two exported idle loops measured 19.398–20.651 units high across 11 samples per clip. The 85 Egg Fighter placement entries checked in six Generations `#euc` archives carry no scale field. Runtime scaling and in-game size remain unverified.

Local evidence is in `artifacts/material-uv2-metal-final` and `artifacts/material-uv2-metal-preview`.

The follow-up painted-metal finish replaced the alpha-based approximation. All 86 tests and all 13 native and relocation checks passed, covering the same 94 animations. Across those exports, 270 text resources matched byte-for-byte and 12 `EnmMetal_*` materials changed. The Egg Fighter comparison uses fresh native exports, with the same pose, camera and lights. See `artifacts/metallic-finish-final` and `artifacts/material-uv2-metal-preview/egg-fighter-finish-comparison.png`.

The reference-based follow-up uses the specular RGB atlas to distinguish paint and bare metal and restores the separate enemy-glass emission/opacity inputs. All 87 tests passed. Fresh native exports of the same 13 scenes passed the Acorn and binary-only relocation checks with 94 supported animations. The separate shock bar exercised the metal bake. The Egg Fighter comparison from fresh exports is `artifacts/material-uv2-metal-preview/egg-fighter-source-channels-final.png`. View-dependent source matcap highlights and in-game visual parity remain unverified.

## Full shader library audit — 2026-11-05

- The per-file audit indexed 4,527 shaders across both games. See the [material mapping notes](SPATIAL-MATERIAL-MAPPING.md#shader-library-audit) for the applied rules and the boundary between material inputs and runtime shader effects.
- Release build passed with zero warnings/errors and all 105 tests passed. Native conversion and binary-only relocation passed for 17 scenes and 94 supported animations. The new scene checks cover the Generations dash ring and upreel emission maps and unshaded boost effects in both games.
- The original 13-case bank produced 293 byte-identical text resources. Within that bank, only the intended Silver and shock-bar emission/coverage resources changed. The rendered Silver comparison uses the final binary scene and shows the restored cyan trim.
- Generations character-metal alpha gloss, emission UV2 and the parallax mapping were covered by controlled fixtures. A native GLES3 visual comparison for parallax remains unverified.

Local evidence is in `artifacts/shader-family-audit`, `artifacts/shader-audit-exports` and `artifacts/shader-audit-preview/silver-shader-audit.png`.

## Version 0.6.0 release audit — 2026-11-05

- Clean restore and Release build passed with zero warnings/errors. All 114 automated tests passed, including WPF game selection, tangent recovery and malformed half-floats, UV routing, material-family behavior, wide/tall texture resampling and optional-map diagnostics.
- Acorn compiled and reloaded 33 scenes with 654 supported animations. Every scene passed binary-only relocation and SpatialMaterial checks. This includes both complete Sonic banks, Shadow, Silver, Rouge, Tails, Egg Fighter and boost/stomp assets from both games.
- Fresh static exports from the v0.5.0 source archive and the prior complete Sonic banks match the candidate's 636 comparable text resources byte-for-byte. A fresh v0.5.0 replay reproduced Shadow's tangent failure. The candidate exported Shadow and all 16 matching animations successfully.
- Stock Godot 3.1.2 official, GLES2, loaded the Egg Fighter, Silver, dash-ring and boost text scenes and confirmed their UV2 normal, metallic, emission and unshaded properties. The candidate also matched the previously rendered Egg Fighter, Silver and boost text outputs byte-for-byte.
- Verified the existing explicit failures for the three quantized clips in the additional Generations fixtures. In-game SCU playback, renderer parity and a native GLES3 parallax visual comparison remain unverified.

Local audit evidence is in `artifacts/v0.6-audit`.
