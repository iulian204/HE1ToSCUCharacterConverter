# Ordered Next Steps

## 1. Resume PRR material correctness audit (user-requested priority)

- **Files:** `src/HE1ToSCU.Core/PrrParameterMapper.cs`, `PrrUniformValue.cs`, `PrrShaderLibrary.cs`, `He1MaterialReader.cs`, `PrrMaterialWriter.cs`, `PrrParameterAuditReport.cs`; docs `PRR-PARAMETER-AUDIT.md`, `PRR-SHADER-UNIFORMS.json`; tests listed in VALIDATION-STATE.md; real reports under `artifacts/prr-parameter-audit/`.
- **Do:** Open latest actual Generations and Unleashed reports by timestamps and compare their raw source parameter records to actual selected shader uniform declarations and shader use. Focus the concrete users' reports of black hint_color and mis-scaled eye highlight, and all additional parameter rows. The two concrete causes are already corrected in baseline code; verify there are no regressions and examine new/latest report differences rather than reintroducing old mistakes.
- **Expected:** Every field in sampled material reports is classified with evidence; source values, mapped lane/type, serialized text, and binary value agree for supported mappings. Findings distinguish verified, ambiguous, unsupported, and intentionally unused.
- **Acceptance:** A deterministic regression test per confirmed bad mapping; run native Acorn text and binary readback for synthetic minimal examples and representative real materials from both games. Preserve `sky_color=0.5` and `ground_color=0.25` only when no explicit authored value maps.

## 2. Reproduce and resolve any concrete residual PRR mapping defects

- **Files:** Mapper and focused tests; avoid conversion engine / animation changes.
- **Rules:** Never use loose name matching or assume all float4s are color. Do not scale eye highlight position. For scalar extraction from packed registers, infer exact lanes from shader/reference evidence; if unknown, classify unsupported instead of guessing.
- **Acceptance:** Source → expected uniform → Godot text → Acorn loaded text → binary reload all equal within the established tolerance; texture sampler pixels and assignment verified; normal SpatialMaterial output unchanged.

## 3. Add naming-feature tests before more integration runs

- **Files:** New `tests/HE1ToSCU.Tests/ScuInventoryTests.cs`, `ScuNameMatcherTests.cs`, plus GUI tests and additions to pipeline regression tests as appropriate.
- **Cover:** bundled inventory validates both profiles/actual resource names, 303/75 eligible counts, no duplicate targets; semantic normalization preserving start/loop/end, walk/run, dash/boost/jet, left/right, ground/air; descriptors compare only common semantic anchors and missing motion reduces confidence; Hungarian maximum vs brute-force small matrices; deterministic assignment/conflict handling; low-confidence always assigned; intentional reuse/unfilled empty source behavior; optional valid/stale overrides; cache version/fingerprint invalidation; model/skeleton/mesh/animation names in all paths; source clip transforms/duration/interpolation/loop unchanged; PRR and SpatialMaterial/material names unchanged; naming-disabled byte parity against stable baseline.
- **Acceptance:** tests fail against old/broken behavior and pass after changes; no name collisions or source/target identity confusion.

## 4. Inspect inventory/profile schema and matching quality

- **Files:** `assets/SCU/*.json`, `ScuInventory.cs`, `ScuNameMatcher.cs`, source-profile tool.
- **Do:** Inspect exact JSON keys and profile file/model/skeleton data (runtime typed reader currently doesn't expose a `files` collection; raw JSON retained). Produce concise reports for both source games against both profiles: best candidate, score/confidence/evidence, reuse, unfilled targets. Look at exact loop flag independently from name suffix.
- **Acceptance:** Complete names/relationships found in local reference roots; cached profile key inventory/scoring version matches current generated inventory; report does not overstate motion coverage. Do not rerun Acorn inventory extraction unless code or source assets change.

## 5. Verify naming end-to-end on small real sample, then broader exports

- **Files:** `ConversionService.cs`, `GodotWriter.cs`, `ExportNames.cs`, `PoseValidation.cs`, `scripts/Verify-Export.ps1`.
- **Do:** Convert a small representative Generations model/clip and Unleashed model/clip with naming on and off, then a broader selected set. Confirm root `Scene Root`, `Skeleton`, Sonic mesh `Sonic_Body`, Super mesh `mesh`, scene names `chr_Sonic_SD` / `chr_SuperSonic_SD`, target animation resource/file/player key identities, loop/timing/key preservation, and target native scene `.converted.res` PCK identity. Ensure .tscn.remap is added only after compile, so staging load is not redirected prematurely.
- **Acceptance:** Acorn loads, compiles, independently reloads every named binary scene/mesh/animation; generated verifier checks source poses and exact clip data; animations remain bound to Skeleton; resource references relocate; distinct target names on reuse; materials/textures/PRR refs unchanged. The original SCU remap/scene name identity is not evidence of full game replacement.

## 6. Verify UI, CLI and packaging

- **GUI:** Exact label “Match Sonic Asset Names”; default off; “Super Sonic” toggle only meaningful when naming is enabled, settings persist, PRR setting independent, no blocking approval. Verify path/profile selection by logs/output. Corrections picker is optional; malformed explicit JSON gives actionable diagnostic. Test fresh launch and restart.
- **CLI:** Run equivalent args with `--match-scu-sonic-names`, optional `--super-sonic`, optional `--animation-name-map path`; compare reports/output target assignments with GUI request. Ensure `--super-sonic` or map path is rejected/ignored only with documented clear behavior when master flag is off.
- **Packaging:** Build/publish self-contained Windows x64 via `scripts/Publish.ps1`; inspect artifact includes embedded inventory and the two human-readable JSON data files; run packaged app with source development paths unavailable/renamed if safe, and confirm inventory loading. Do not package copyrighted game assets.
- **Acceptance:** actual UI and output verified; complete suite and clean Release build pass after final edits. No new GitHub release until user explicitly authorizes one.

## 7. Final review

- Recheck `git diff`, `git status`, `git diff --check`, version/changelog remain v0.7.0/Unreleased until requested otherwise, no generated artifacts or SCU binaries staged accidentally.
- Preserve all current uncommitted changes; report test limitations honestly.
