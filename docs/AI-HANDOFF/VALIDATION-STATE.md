# Validation State

## Current interrupted Sonic naming work

### Executed successfully

1. Inventory generator command:

    python scripts/Build-ScuInventory.py --scu-root D:/SCUExtract --acorn D:/AcornDirFix/BSE_Editor_Acorn1.1.1.exe --game-pck-directory D:/SteamLibrary/steamapps/common/SonicColorsUltimate/content/acorn

    Evidence: `artifacts/scu-naming/inventory-build.log` reports Sonic 16 scenes / 304 animation files / 303 eligible humanoid clips and SuperSonic 5 scenes / 78 animation files / 75 eligible clips. Extractor log `artifacts/scu-naming/inventory-build/extraction.log` completed with marker `SCU_INVENTORY_EXTRACTED`. All 382 SCU target `.anim` files were decoded in Acorn; four effect-rig clips marked ineligible for humanoid mapping.

2. Source-profile generation:

    `dotnet run --project tools/HE1ToSCU.ScuInventory -c Release -- assets/SCU/sonic-inventory.json "D:/SteamLibrary/steamapps/common/Sonic Generations/disk/bb3/Sonic" "C:/Users/jigai/Desktop/CorrectSUSonic/Sonic"`

    Evidence: `artifacts/scu-naming/source-profiles.log`: 544 source clips parsed, 1,088 game/profile source records built (325 Generations + 219 Unleashed). Source profile provenance includes hashes/metadata, no game binaries.

3. Build:

    `dotnet build HE1ToSCU.sln -c Release`

    Evidence: `artifacts/scu-naming/build.log` ends Build succeeded, 0 warnings, 0 errors, 4.19 s. This was before any later edits (handoff docs only since that build).

4. `git diff --check` executed. No whitespace errors; only Git warnings that working-copy LF files may become CRLF.

### Not executed in this feature work

- `dotnet test HE1ToSCU.sln -c Release` after the naming changes.
- Naming-enabled text or Acorn binary conversion on real Generations/SU models.
- Matcher diagnostics/quality review; cross-game correctness acceptance.
- Acorn source-pose and binary round-trip helper generated for naming-enabled outputs.
- GUI launch/interaction, saved setting restart test, optional JSON picker, CLI equivalence.
- Release publishing script / portable output and embedded inventory verification.
- Byte-for-byte disabled-mode comparison against v0.7.0 baseline.
- Full `scripts/Verify-Export.ps1` on named outputs.

No naming tests have been added yet.

## PRR validation from historical committed implementation/artifacts

- Previous memory checkpoint (PRR correction task) records 483 tests passing, no skips; clean Release build; 911 mapped source records and 436 sampler bindings checked in text and fresh-process binary resources; all 14 original eye-position assignments unchanged. Relevant artifact was `artifacts/HE1ToSCU-PRR-authored-values-win-x64.zip`; this is historical memory, recheck local archive if relying on it.
- Current repository artifact log `artifacts/prr-audit/final-tests.log` reports 284 core tests and 18 Avalonia tests passed, 0 failed/skipped (302 total) at the time of that saved run. Another saved `tests-final.log` reports 285 core + 18 GUI passed (303 total). These historical log counts differ and are not a claim about current working changes; inspect dates/content before citing.
- Current saved representative real Generations report `artifacts/prr-parameter-audit/actual-SonicGenerations-4/prr-parameter-audit.json` summary: 1,442 source parameters; 821 mapped/text+binary verified; 1,453 assignments text+binary verified; 393 corrected Color-variant assignments; 578 incorrect mappings corrected; 264 intentional defaults; 621 intentionally unused; 434 texture bindings, 399 mapped and text+binary verified; 0 unsupported/missing/ambiguous/failures in that capture; 6 SpatialMaterial fallbacks. This is one audit capture, not entire library coverage.
- Reports exist for multiple Generations, Unleashed, synthetic-authored/default, runtime, relocation and regression captures in `artifacts/prr-parameter-audit/`. Keep provenance clear; do not mistake an older capture for the latest implementation.
- `PrrAcornRuntimeTests.cs` uses local editor `D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe` unless `HE1SCU_TEST_ACORN` is set. It writes expected values before text serialization, reads back Godot Variants, compiles/reloads binary in a fresh process, and compares texture data. Runtime checks are skipped if editor path missing. Saved reports show tests were actually run in past on this machine, but current run still required after changes.
- Historical failure in `artifacts/prr-audit/tests-initial.log`: 3 GUI backdrop cleanup tests failed due `IOException: directory not empty` under temporary settings disposal; later `final-tests.log` passed. Not related to PRR/naming but retain if rerunning and investigate only if it recurs.

## Known unverified / limitations

- Text/binary resource readbacks demonstrate serialized values, not that every inferred mapping reproduces original in-game shading.
- Naming algorithm not manually reviewed against playback, and there is no in-game SCU replacement test.
- Windows Avalonia UI screenshot-level test has not occurred after the naming toggles were added.
- Acorn binary compilation tests must be rerun after edits to naming GDScript checks or scene remap output.
