# Validation results — 2026-11-03

The sections below retain historical checks. Current release evidence is recorded in the version 0.4 section at the end.

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
