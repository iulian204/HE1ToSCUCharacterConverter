# Progress Inventory

## Completed before the current naming feature (v0.7.0 baseline)

### PRR ShaderMaterial export exists

Implemented in the released baseline `08d0ad7` and current tracked source. It supports Generations and Unleashed shader libraries embedded in `src/HE1ToSCU.Core/PrrShaders`, per-material matching/fallback, bundled or external shader resource paths, texture export, mixed ShaderMaterial/SpatialMaterial, conversion manifest and `prr-parameter-audit.json`.

Key implementation files:

- `src/HE1ToSCU.Core/PrrShaderLibrary.cs` — uniform parsing, shader library/matching, variants.
- `src/HE1ToSCU.Core/PrrParameterMapper.cs` — source aliases, lane mappings, type-aware assignments, defaults.
- `src/HE1ToSCU.Core/PrrUniformValue.cs` — Godot Variant/literal conversion and audit record types.
- `src/HE1ToSCU.Core/PrrMaterialWriter.cs` — shader/material/sampler writing and per-material fallback.
- `src/HE1ToSCU.Core/PrrParameterAuditReport.cs` — mapping report, independent expectations, text/binary readback aggregation, uniform inventory.
- `src/HE1ToSCU.Core/PrrRuntimeValidation.cs` — Acorn/Godot GDScript readback of Variant types/values and texture pixels.
- `src/HE1ToSCU.Core/He1MaterialReader.cs` — Mirage material parser.
- Tests: `PrrMaterialTests.cs`, `PrrParameterCorrectnessTests.cs`, `PrrAcornRuntimeTests.cs`, `PrrReleaseTests.cs`.

The released code has regression coverage for vec2/vec3/vec4, RGB Color alpha, exact scalar types, aliases, packed values, authored source values, sky/ground defaults, texture UV/wrap/channel behavior, shader variants, ambiguity/unsupported diagnostics, and native resource readback. This is not proof all shader semantics are correct; see remaining work.

### Prior PRR bug corrections with evidence

- RGB-only HE1 color fields for diffuse/ambient/specular/emissive and Sonic eye highlight must export as Godot `Color(..., alpha=1)`, not preserve an unused float4 W of 0 as alpha. HedgeLib reads main colors with `as_vec3()` and the original eye shader consumes highlight `.xyz`. Raw W remains in the audit. Prior memory records 424 assignments corrected and Acorn text/binary readback; see `PARAMETER-MAPPINGS.md`.
- `g_SonicEyeHighLightPosition=(2,-3,2.5)` must be passed unchanged as XYZ to `eye_highlight_position`; an earlier factor-of-10 mesh-scale application produced `(20,-30,25)`. Current mapper says direct and `PrrAcornRuntimeTests.cs` asserts expected `(2,-3,2.5)`. Separate `eye_light_range` mapping has its own scale rule. Visual parity is not established by readback.
- Explicit PRR default uniforms `sky_color` = grayscale 0.5 and `ground_color` = grayscale 0.25, alpha 1 for Color type, are applied only when the named uniform is declared and no source value already occupies it.

### New Sonic naming feature — implemented but unverified

Current uncommitted files (preserve all):

- Modified: `HE1ToSCU.sln`, `scripts/Verify-Export.ps1`, `src/HE1ToSCU.App/HE1ToSCU.App.csproj`, `src/HE1ToSCU.App/MainWindow.axaml`, `src/HE1ToSCU.App/MainWindow.axaml.cs`, `src/HE1ToSCU.Cli/Program.cs`, `src/HE1ToSCU.Core/Assets.cs`, `src/HE1ToSCU.Core/ConversionService.cs`, `src/HE1ToSCU.Core/ExportNames.cs`, `src/HE1ToSCU.Core/GodotWriter.cs`, `src/HE1ToSCU.Core/HE1ToSCU.Core.csproj`, `src/HE1ToSCU.Core/PoseValidation.cs`.
- Added: `scripts/Build-ScuInventory.py`, `scripts/Extract-ScuAnimationMetadata.gd`, `src/HE1ToSCU.Core/AnimationSemantics.cs`, `src/HE1ToSCU.Core/MotionDescriptor.cs`, `src/HE1ToSCU.Core/ScuInventory.cs`, `src/HE1ToSCU.Core/ScuNameMatcher.cs`, `tools/HE1ToSCU.ScuInventory/HE1ToSCU.ScuInventory.csproj`, `tools/HE1ToSCU.ScuInventory/Program.cs`.
- Generated intended data: `assets/SCU/sonic-inventory.json` (12,050,781 bytes), `assets/SCU/animation-profiles.json` (11,669,947 bytes), `assets/SCU/source-profile-provenance.json` (191,521 bytes). These are untracked and need review/tests; they do not include game binaries.

Feature behavior already coded: optional “Match Sonic Asset Names” GUI setting, separate Super Sonic checkbox, saved settings, optional post-hoc corrections JSON, CLI flags `--match-scu-sonic-names`, `--super-sonic`, `--animation-name-map`; deterministic semantic + motion scorer; global Hungarian assignment plus best-source reuse for remaining targets; confidence/report; naming integrated into output resources; source clip identity preserved; post-compile binary name and source-pose checks; inventory embedded in Core and copied beside the app.

**Verification remaining:** no new feature tests added/run, no source-cache matching review, no conversion with naming enabled, no Acorn binary relocation/report verification, no packaged-app inventory validation, no full regression run after feature modifications. Release build and inventory/profile generation passed; see VALIDATION-STATE.md.

## Partial / requires verification

- **PRR semantic audit:** existing tests and saved audit reports are extensive, but task explicitly requests comprehensive per-parameter/per-shader validation, especially remaining wrong mapped values. Review both `artifacts/prr-parameter-audit/actual-SonicGenerations-4/prr-parameter-audit.json` and latest Unleashed equivalent; distinguish confirmed semantic mismatches from unmapped intentional parameters and absent evidence. No claim that every source parameter is solved.
- **Source-to-SCU animation matching:** source cache builder creates 1,088 candidates, but scores/rankings/confidence, bad matches, coverage, matching-with-real-output, and target reuse still need validation.
- **Inventory completeness:** actual JSON inspection confirms top-level 720 file records, 104 external references, and 700 original PCK resource records; profiles contain Sonic 16 models/2 skeletons/304 animations and Super Sonic 5 models/3 skeletons/78 animations. A diagnostic mistakenly queried nonexistent per-profile `files` and singular `model` properties, printing zero/empty; the mesh identity is directly at profile `meshResourceName`. Verify typed `ScuInventory` validation against this schema, not the erroneous diagnostic paths.
- **GUI:** labels/options/settings code present, not runtime-interacted after edits.
- **Binary scene name binding:** original PCK remap strategy is verified in source PCK index, but generated naming-enabled native binary scene and post-compilation remap were not run end-to-end.
- **Export verification script:** changed to copy and validate native `.converted.res` outputs with nested animation paths; not yet executed against a new named export.

## Not started in current feature

- Dedicated naming unit/integration tests.
- `docs/SCU-NAMING.md` or README/Changelog explanation for automatic mapping and limitations.
- GUI inventory viewer (optional in user request; no viewer implementation).
- Optional mapping editor GUI (correction JSON loading/picker exists; user-facing GUI table/editor does not).
- Full representative PRR audit for all shader families in both games after the `D:\HE1TOPRRShaders\Release` updates (baseline docs/results do exist; scope of latest updated collection must be checked).
- New commit/release (not requested for naming work).
