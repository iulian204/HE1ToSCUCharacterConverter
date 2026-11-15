# Sonic naming validation — 2026-11-15

Release target: v0.8.0, based on v0.7.0 (`08d0ad7647ecc01d0753403dd8586887169ea1f2`). The user authorized publication after the final checks below.

## Executed checks

- Clean .NET 10 Release build: 0 warnings, 0 errors.
- Final v0.8.0 suite: **490 Core + 19 Avalonia tests = 509 passed, 0 failed, 0 skipped**. Includes existing PRR semantic/Variant/texture round-trip tests, new naming/matching/native-bank coverage, standard filenames and an independent current Release-library check.
- Inventory decoded using Acorn: 304 Sonic animations (303 humanoid-eligible) and 78 Super Sonic animations (75 eligible), 21 scenes, 720 file records, 104 references and 700 original PCK index records. No game binaries bundled.
- Complete source-bank matching: all 325 Generations and 219 Unleashed animation files decoded and scored against both profiles. No cached-profile version warnings or duplicate target names.
- Generations → Sonic: 303 assigned, 172 High / 44 Medium / 87 Low, 0 reuse. Generations → Super: 75 assigned, 46 High / 26 Medium / 3 Low, 0 reuse.
- Unleashed → Sonic: 303 assigned, 76 High / 146 Medium / 81 Low, 84 intentional reuses. Unleashed → Super: 75 assigned, 0 High / 59 Medium / 16 Low, 0 reuse.
- Real Generations PRR export to Super profile: 75 target animations, three mesh parts, 170 bones; source-derived pose checks and independent binary reload passed. PRR audit: 94 typed assignments and 29 sampler bindings independently verified after text and binary loading.
- Real Unleashed PRR export to Sonic profile with generated Mouth_L: 303 animations, three mesh parts, 167 bones; source-pose and independent binary reload passed. PRR audit: 106 typed assignments and 29 sampler bindings verified in both stages. Mirrored mouth remains hidden by default.
- Binary-only relocation checks passed for both above exports after copying only native scene/mesh/animation resources to independent temporary projects, without source text/shader files.
- The final 303-animation, 6-second Unleashed bank also passed binary-only relocation and scene-only loading from a moved project subfolder with no companion animation files: `NESTED_REAL_SCENE_OK animations=303 bones=167`. This exercises the final embedding behavior on the large real scene, not just a synthetic fixture.
- Unleashed SpatialMaterial text export with 303 renamed animations and generated Mouth_L completed.
- Naming-disabled PRR output vs v0.7.0: all 31 generated resource files compared byte for byte with no differences (manifest and audit reports excluded because they contain output paths/report metadata).
- Current supplied Release-library check: all 3,648 shader files match both the bundled files and the v0.7.0 Git tag byte for byte. The current source release-manifest SHA-256 is `8445b7d26bd60c3279b89286a232644455bd8401f62771b5629fb223d8e692dc`, identical to bundled provenance. A test compares the embedded programs directly to the current independent local Release folder when accessible; it does not rely only on the converter's own inventory.
- Portable self-contained Windows x64 publishing completed; inventory JSON files are included beside the executable and embedded in Core. All PRR libraries remain embedded and existing hash/interface tests pass.
- Actual portable GUI was launched and visually inspected. Exact label, Super Sonic enabling and optional corrections control were exercised; preferences restored afterward. Automated GUI test verifies defaults, settings persistence and actual ConversionRequest values. No new manual DPI/system-theme test was performed; existing theme/backdrop tests pass.

## Issue found and fixed during validation

The 303-animation bank crossed the existing native external-resource threshold. Acorn's `FLAG_RELATIVE_PATHS` writer generated `../animations/...` dependencies for nested output files, causing scene reload failure. A first correction using package-relative paths passed root-project relocation but a deeper folder probe exposed Acorn's project-root dependency preload behavior. Named scenes now embed their animations while still writing individual `.anim` files; ordinary exports retain their previous threshold/paths. A 301-animation/80-bone native regression fixture crosses that threshold and verifies standalone files, a fresh binary reload, and scene-only loading from a moved subfolder. The real long 303-animation export is checked with this final behavior; larger named scenes are an intentional storage tradeoff for relocation correctness.

## Evidence retained locally

Logs and exports are under `artifacts/scu-naming/`: `native-regression.log`, `bank-check.log`, `bank-results/*/scu-animation-mapping.json`, `gens-super-native`, `su-sonic-native-embedded`, `gens-super-relocation.log`, `publish.log`, `disabled-stable` and `disabled-current`. Final clean/build/test logs and real relocation receipts are in `artifacts/v0.8.0-release/`. The failed intermediate run `su-sonic-native.log` and diagnostic staging directory remain as root-cause evidence; final `su-sonic-native-embedded.log` supersedes them. An intermediate clean/build attempt collided with the still-running CLI's DLL lock; the final clean/build was rerun after the process completed and passed with no warnings/errors.

## Boundaries

Confidence is inferred action similarity, not verified gameplay equivalence. Four nonhumanoid effect clips are inventoried but intentionally left unfilled. Source naming and rig identity remain separate; no retargeting or complete SCU gameplay integration is claimed. The tests verify exported resource structure, data persistence, source poses and materials; actual in-game replacement and playback review of every automatic pairing were not performed. GUI conversion requests are integration-tested and real conversions were executed through the shared CLI service, rather than clicking a full conversion in the packaged GUI.
