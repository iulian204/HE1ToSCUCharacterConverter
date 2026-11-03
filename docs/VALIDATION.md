# Validation results — 2026-11-03

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

No claim of bit-for-bit equality with the proprietary Havok sampler, complete coverage of all Havok compression modes, exhaustive animation visual correctness, Unleashed binary-HKX support, SCU gameplay retargeting, exact original shader reproduction, in-game replacement acceptance, or renderer parity is made. See the supported-format and remaining-work lists in README.md.

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
