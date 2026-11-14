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

## Version 0.6.1 desktop migration

- Clean Release build on .NET SDK 10.0.403 completed with zero warnings or errors. All 120 tests passed, including 7 Avalonia workflow tests covering both appearance variants, settings persistence, picker options, the animation list, keyboard input, progress, cancellation and existing-directory protection.
- Inspected the self-contained Windows x64 preview on Windows 11 in Light and Dark at its default and maximized sizes. Verified native chrome, Mica, the system accent, empty and selected animation rows, a native HKX picker, and live Windows Dark → Light → Dark in Automatic mode. Restored the original Windows appearance and application settings. Compact layout was checked headlessly. Physical high-DPI and every native dialog were outside the manual check.
- Converted Unleashed Sonic with a real HKX clip and the optional mirrored mouth through the portable GUI. Acorn validated and relocated the binary scene, and all 29 output resources matched an equivalent .NET 10 CLI run byte for byte. A second GUI run for Generations was unavailable after the Windows automation helper failed to activate its window. Both games were exercised through the CLI and automated GUI profile tests.
- Compared 33 native exports with v0.6.0, including the 325-clip Generations bank and the 219-clip Unleashed bank. All 654 clips retained their track structure, key counts and timing. All meshes, material resources and textures matched byte for byte. With .NET 10, rest transforms and animation poses differed slightly in floating-point rounding. The maximum component differences were 0.00000333 for rest transforms, 0.000003934 export units for animation translation and 0.00000072 for scale. All 33 binary scenes passed fresh-process Acorn relocation.

## Version 0.6.2 Mica hotfix

- The clean .NET 10 Release build passed with zero warnings and errors. All 129 tests passed, including simulated unsupported Windows, compositor rejection and API failure, both opaque palettes, and repeated Windows registry change notifications on an isolated test key.
- On Windows 11, the portable application started with transparency disabled and switched between Mica and opaque backgrounds across two Windows Settings toggle cycles. Verified Light, Dark and Automatic, live Windows theme changes, resizing, minimizing and restoring. The original Windows and application preferences were restored. A real Windows 10 installation was unavailable for testing.
- Both complete Sonic banks passed native Acorn export and relocation. All 1,142 generated resources were byte-identical to v0.6.1 across 544 clips. The manifest differs only in version and output directory.

## Unreleased PRR export

- The clean .NET 10 Release build passed with zero warnings/errors; all 303 tests passed (285 core, 18 Avalonia). Coverage includes all 152 embedded shader hashes, all 285 explicit matching identifiers, original parameter lanes, raw texture channels/mips, cube face/mip layout, external-path validation/staging, render-mode variants, missing-shader fallbacks, mixed texture isolation, mouth assignments, cancellation and GUI persistence/defaults.
- All 152 supplied shader files are byte-identical to the confirmed current Generations/Unleashed directories. Each loaded and rendered a test sphere without shader errors in stock Godot 3.1.2 and Acorn GLES3. This checks compilation/execution, not original-game visual parity.
- Nine representative models/effects covered Sonic from both games, Egg Fighter, Silver, Rouge, Tails, Shadow and both boost formats. All 154 default material/texture files matched an isolated build of the previous stable source using identical current inputs. Their 19 mesh text files and nine scenes also matched between default and PRR mode. Historical saved Sonic outputs were not a valid byte baseline because local source shader assignments/textures had changed; no source assets were altered to hide those differences.
- Native default exports with representative animations matched stable output byte-for-byte across 57 resources. PRR exports retained the same geometry, scenes and animation text; Acorn validated 324 Generations and 85 Unleashed source-derived pose samples, including subframes/loop boundaries and mirrored-mouth tracks. The complete animation banks were not rerun because their algorithms were unchanged.
- Bundled and external project shader references compiled for both games. Fresh-process checks compared material types, shader code, parameter values and sampler image bytes before/after binary serialization. Binary-only relocation succeeded without source shaders/textures, including a mixed-material fixture. Moved real text exports loaded in stock Godot 3.1.2. External text intentionally requires its configured project shaders.
- The self-contained portable GUI performed native PRR conversions for both games, including Unleashed Mouth_L; the new controls were visually inspected in Light and Dark. Headless tests cover compact layout, retained settings and disabled-mode requests. Original application settings were restored byte-for-byte after manual testing. Physical DPI settings and original-game visual parity were not tested in this feature pass.
- PRR uses GLES3; forcing the supplied cube programs through GLES2 genuinely fails (`textureSize` is unavailable). This was corrected by selecting GLES3 for PRR validation while keeping the existing SpatialMaterial backend unchanged. Scene-state requirements and shader-library limitations are documented in [PRR materials](PRR-MATERIALS.md).

## PRR parameter correctness audit

- Clean .NET 10 Release build: zero warnings/errors. All 479 tests passed (461 core, 18 Avalonia), with no skips. Executed native tests cover actual Godot Variant types, matrix row serialization, text/binary retention and deliberate wrong-type/value/pixel failures.
- Inventoried all 152 current shader programs and 4,773 declarations. Both stock Godot 3.1.2 and Acorn matched every name/property type. Canonical shader bytes remain unchanged. Each applicable Sky/Ground uniform passed default and authored-override tests; no unrelated default was replaced.
- Enumerated 180 original material files, parsed 173 supported v1/v3 files, and reported seven already-unsupported v2 files. An independent binary walk matched all 1,582 raw name/flag/value records. Acorn independently verified all 911 mapped source records, 1,603 typed assignments and 436 sampler bindings in text and fresh-process binary resources. Remaining source records and fallback textures are retained/classified, not omitted. All positive validations passed; synthetic evidence is labeled separately.
- Real original materials exercised 14 matched shader families; synthetic fixtures covered the entire 152-shader collection, integer/bool controls, defaults and HDR/negative/zero values. Original int4/bool4 or multi-vector arrays were not present in the corpus: their format behavior was tested with deterministic v1/v3 binary fixtures. Unknown source matrix layouts and shader uniform arrays remain explicitly unsupported.
- Rechecked nine representative default exports: all 154 material/texture resources remained byte-identical to stable source using identical current inputs, and all 19 mesh text files/nine scenes matched PRR versus default. Representative native default exports retained all 57 resources byte-for-byte. Animated Generations and Unleashed PRR scenes, including mirrored mouth, passed pose checks and binary-only relocation; external shader-path mode also passed.
- See [audit findings](PRR-PARAMETER-AUDIT.md), [full uniform inventory](PRR-SHADER-UNIFORMS.json) and [mapping policies](PRR-MATERIALS.md). Stored input verification does not claim original-game visual parity or fix the shader library's documented scene/lighting limitations.


## PRR RGB semantics follow-up

- All 481 tests passed (463 core and 18 Avalonia), with no skips, after adding RGB-alpha and eye-position unit regressions (superseded by direct authored-value preservation below). Native Acorn tests additionally load and save RGB colors with alpha 1, an authored RGBA color with alpha 0, and the then-scaled eye point.
- Repeated the complete original-material and all-152 synthetic material batches after the RGB semantic correction. All 911 mapped source records, 1,603 typed assignments and 436 original sampler bindings passed text and fresh-process binary readback. An independent source-byte walk again matched all 1,582 records. Both engines again matched all 4,773 declared uniform types.
- These readbacks verify expected stored values, not every source semantic or visual eye-highlight placement. Original and PRR eye shaders subtract the highlight point from the unskinned vertex, so both retain the same automatic unit conversion. The user's reported placement discrepancy has not yet been reproduced or explained; no speculative correction was applied.
- Fresh complete Generations and Unleashed Sonic PRR native exports passed after this correction: 324 and 85 source-pose samples respectively, text/binary parameter checks, and native resource reloads. The Unleashed case retained mirrored Mouth_L. The rebuilt self-contained executable launched responsively and included all 152 canonical shader byte sequences. Visual highlight placement remains unverified.

## PRR authored eye-highlight position correction

- Eye-highlight XYZ now preserves the source parameter directly in both games. Alias and exact-name input follow the same rule; mesh scale, attenuation distances, shader sources and SpatialMaterial output are unchanged.
- Replaced the coordinate-scale assumption test with explicit authored-value tests, including the reported Unleashed `(2, -3, 2.5)` case, negative/fractional values, zero, both games and both input names. Native tests independently require the original position in text and binary.
- Inspected paired Acorn renders using scaled and original eye positions. Both produced usable highlights in this test view; this is not proof of original-game visual parity.
- Final follow-up: all 483 tests passed with no skips; Release build completed with zero warnings/errors. The 173-material rerun verified 911 source mappings, 1,603 assignments and 436 samplers in text and binary with zero validation failures, including all 14 original eye-position assignments without multiplication.


## Updated PRR Release library — 2026-11-14

- Imported only the current Release shaders after verifying its complete upstream file manifest. All 3,648 embedded preset hashes match the supplied release; 152 canonical programs and 285 exact source identifiers remain available. All 11 supplied guides, machine/per-shader inventories and upstream validation receipts were reviewed and retained under `docs/PRR-Library`. Historical research notes are distinguished from the final declared interface.
- Clean .NET 10 Release build: zero warnings/errors. Final suite: **492 tests passed** (474 core, 18 Avalonia), no skips. New tests cover all preset hashes/interfaces, byte-exact state selection, original numeric vectors, renamed gloss samplers, independent per-map UVs, source-specific packed lanes and same-source/different-slot text/binary validation.
- Acorn loaded all **3,648** nested plain material/preset resources and rendered all **744** distinct full shader bodies: zero failures or shader errors. Stock Godot 3.1.2 and Acorn independently checked **456 coverage interfaces / 11,650 uniform declarations**. All 152 programs were also exercised with default and authored synthetic material bindings.
- Repeated original-material validation: 173 supported materials, 1,582 raw source records checked independently, **747 mapped source parameters / 1,413 typed assignments / 436 sampler bindings** verified in text and fresh-process binaries. All other records are retained/classified; retired optional inputs are intentionally unused. Seven already-unsupported Mirage v2 files and eight missing-implementation SpatialMaterial fallbacks remain explicit limitations.
- Fresh native Sonic exports from both games passed material checks, 324 Generations / 85 Unleashed animation-pose samples, mirrored Mouth_L and binary reload. External Release-root compilation succeeded without permanent shader copies. Three binary-only relocated projects independently verified numeric parameters and sampler pixels without shader/texture source files.
- Nine representative model/effect conversions covered both Sonics, Egg Fighter, Silver, Rouge, Tails, Shadow and boost effects. All **154** default material/texture files matched a fresh isolated stable-source run; **19** mesh text files and all nine scenes matched default versus PRR. Conversion algorithms and full animation banks were not rerun unnecessarily.
- Inspected fresh rendered models from both games, including an animated Generations pose. This confirms representative rendering, not original-game parity or complete visual coverage of every shader. The supplied library's local-light, scene-input, animated-eye/morph and framebuffer/atlas limitations remain. The GUI workflow is covered by Avalonia tests; no new full manual theme/DPI sweep was required for the shader/tooltip update.


## Version 0.7.0 release gate

- Rechecked the complete working-tree integration and all 3,648 shader hashes against the validated Release inventory. No credential-like contents or unintended large/generated binaries were found in the intended source changes.
- Updated App/Core/CLI versions and conversion manifests to 0.7.0. A clean .NET 10 Release build passed with zero warnings/errors; all 492 tests passed (474 core, 18 Avalonia), with no skips.
- Fresh versioned native Sonic exports from both games retained all 76 resources byte-for-byte against the immediately preceding validated PRR build. Text/binary parameter and sampler checks, representative animations, mirrored Mouth_L and independent binary-only relocation passed. The metadata-only version update did not invalidate the recorded all-preset/GPU/real-material checks above.
- No GitHub Actions workflows are configured for this repository. Original-game visual parity, the existing upstream shader limits and a new full manual GUI/DPI sweep remain outside these release checks.
