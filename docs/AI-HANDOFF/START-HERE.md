# Project Handoff — HE1ToSCU Character Converter

> Historical snapshot from the interrupted 2026-11-14 execution. Work resumed on 2026-11-15 with the same model. The Sonic naming implementation, tests, large-bank Acorn path fix, real exports and documentation were subsequently completed; see [current validation](../SCU-NAMING-VALIDATION.md). Statements below about unfinished tests/verification describe the interruption, not the latest working tree. Preserve this snapshot as history; use the current validation record and source code for present status.

## Project overview

Standalone Avalonia desktop application and CLI that convert Hedgehog Engine 1 model, skeleton, animation, material, and texture assets into Godot 3.1.2 / Acorn resources. Current release is v0.7.0. The repository is `D:\HE1ToSCUCharacterConverter`.

## Original and current task

The main active implementation is an optional, offline Sonic/Super Sonic asset naming feature. It inventories actual SCU assets, matches converted source animations from Generations or Unleashed to verified target animation names, and applies those names to scene, mesh, Skeleton node, Animation resources, AnimationPlayer keys, and binary references. The user clarified the exact GUI label must be **Match Sonic Asset Names**, and requested a separate **Super Sonic** selection using `D:\SCUExtract\characters\player_Supersonic`. This feature is not complete or committed.

There is also a separate requested comprehensive PRR material parameter correctness audit. The v0.7.0 baseline already contains a substantial PRR exporter, parameter mappings, report generation, and Acorn runtime tests; do not infer that all possible PRR shader parameters are correct. Existing handoff docs, inventories, audit outputs, and test logs are cited below. The user's required priority for the replacement model is to resume the PRR parameter audit from the unfinished issues documented here, while preserving the currently uncommitted naming feature.

## Exact current state

- Branch: `main`
- HEAD: `08d0ad7647ecc01d0753403dd8586887169ea1f2` (`Release v0.7.0 with native PRR material export`), also tagged `v0.7.0` and at `origin/main`.
- Working tree: **uncommitted changes exist**. No staged changes were reported. Do not reset, clean, or overwrite them.
- `git diff --check` found no whitespace errors; it emitted only line-ending conversion warnings for several edited Avalonia/C# files.
- Naming inventory and mapping cache exist as untracked generated JSON. Current build is successful, but automated tests and actual naming-enabled conversion validation have not run since these changes.
- No new release was requested for the active naming feature. Do not commit/publish unless the user asks.

## EXACT RESUME POINT

1. **Last task:** The interrupted work was implementing and validating fully automatic naming/matching for SCU Sonic and Super Sonic assets. The latest user clarification set the label to “Match Sonic Asset Names” and asked for a Super Sonic toggle/profile. The most recent additional user content was screenshots confirming those requirements and prior extraction findings.
2. **Last file/operation:** The last implementation run generated `assets/SCU/sonic-inventory.json` and `assets/SCU/animation-profiles.json`, then ran a .NET 10 Release build. The most recent inspection checked PRR sources/tests and historical logs for this handoff. There is no recoverable precise editor caret or single last function. The last naming code touched before that was versioning the animation profile cache and source/game mapping records in `tools/HE1ToSCU.ScuInventory/Program.cs` and `src/HE1ToSCU.Core/ScuNameMatcher.cs`.
3. **What was found:** Actual inventory extraction completed from local game assets: 304 Sonic animations (303 humanoid slots) and 78 Super Sonic animations (75 humanoid slots). Four effect-rig animations are inventoried but ineligible for humanoid matching. Every animation was decoded by Acorn. Verified target scene identities and PCK remaps are recorded in the inventory. 544 local source animations parsed; 1,088 source-to-profile records were generated. Release build passed with 0 warnings/errors.
4. **Changes already made:** Naming feature files and edits are listed in PROGRESS.md. They are present in the working tree and must be preserved. No tests for the feature, real conversion with naming enabled, or final regression pass have yet been verified.
5. **Very next action:** Honor the user's handoff instruction and resume the **PRR material parameter audit first**: compare the latest real-material audit reports (both games and representative shader families) with `PrrParameterMapper` and exact shader declarations/usage, paying special attention to raw-source values that are incorrectly multiplied/extracted and shader-specific vector/component semantics. Reproduce any remaining known errors in a focused regression test before modifying mappings. Then continue to the naming feature's tests and runtime integration only after preserving/recording this PRR audit state.
6. **Avoid repeating:** Do not re-extract the 720 SCU reference files or re-decode 382 target animations; logs/artifacts exist. Do not rebuild source profiles unless inventory/scoring version changes. Do not redo the completed v0.7.0 PRR baseline audit wholesale; use its reports/tests as evidence. Do not assume an animation filename ending in `loop` means its native loop flag is true.
7. **Unresolved blocker:** There is no environmental blocker. The unresolved functional work is unverified/unfinished PRR semantic correctness and untested naming integration. In the PRR audit, a machine-readable output report has verified values in one Generations capture, but this alone does not prove every mapping in both games/shader variants is semantically correct or visually equivalent. In the naming feature, no runtime matching result or renamed Acorn binary scene has yet been checked.

## Read next

- [PROGRESS.md](PROGRESS.md) — completed, partial, unstarted work
- [TECHNICAL-FINDINGS.md](TECHNICAL-FINDINGS.md) — pipeline and reverse-engineering evidence
- [PARAMETER-MAPPINGS.md](PARAMETER-MAPPINGS.md) — established PRR mappings and caveats
- [KNOWN-ISSUES.md](KNOWN-ISSUES.md) — prioritized outstanding work
- [VALIDATION-STATE.md](VALIDATION-STATE.md) — commands and verified results
- [NEXT-STEPS.md](NEXT-STEPS.md) — actionable ordered checklist
- [ENVIRONMENT.md](ENVIRONMENT.md) — verified paths/tools
- [DECISIONS.md](DECISIONS.md) — design constraints

## Protected working tree

Current uncommitted naming feature changes span the solution, app GUI, CLI, core exporter/validator, inventory scripts, embedded data, and inventory builder tool. The exact file list and the caution to preserve these files are in PROGRESS.md. Generated asset inventories are intended source-controlled data, not game binaries. Do not commit unrelated `artifacts/` build outputs.
