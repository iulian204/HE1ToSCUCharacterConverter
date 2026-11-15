# Known Issues and Open Work

## P0 — PRR source semantics still need exhaustive confirmation

- **Description:** The user reports parameter correctness remains uncertain, beyond the known hint_color alpha and Sonic eye highlight examples. The mapping implementation contains a finite alias table and shader-declaration checks; the request is to audit all supported parameters against actual shader usage for both games, not to assume alias presence means correctness.
- **Severity:** High; visual/material correctness is the user's highest PRR priority.
- **Affected:** Every PRR family in Generations and Unleashed; especially packed float4 scalar/vector semantics, shader-specific uniforms, material values that seem scaled, and parameters classified intentionally unused.
- **Evidence/current state:** `PrrParameterMapper.cs`; reference parameter and shader inventory docs; reports under `artifacts/prr-parameter-audit/`. Latest inspected Generations report is `actual-SonicGenerations-4/prr-parameter-audit.json`: 1,442 raw parameter rows; 821 mapped and text+binary verified; 578 listed as corrected; 621 intentionally unused; zero failed validations in that capture. A prior memory checkpoint reports 911 mapped source records and 436 sampler bindings verified, 14 eye positions preserved. These counts represent different captures/versions; do not add or conflate them.
- **Next action:** Select latest current Generations and Unleashed reports and compare each source field/value/float lane with exact selected PRR shader uniform declaration and its code use. Build a table of source shader/family and any mismatches; create focused failing regression fixtures before changing mappings. Keep hypotheses labeled.

## P0 — No current-session PRR suite has been run

- **Description:** Current modifications are the Sonic naming implementation. The pre-existing v0.7.0 PRR tests/logs show prior work, but this session did not rerun them; the model handoff task should not claim current clean validation of PRR.
- **Severity:** High before any future material fix/release.
- **Relevant files:** `tests/HE1ToSCU.Tests/PrrParameterCorrectnessTests.cs`, `PrrAcornRuntimeTests.cs`, `PrrMaterialTests.cs`, `PrrReleaseTests.cs`.
- **Next action:** After any PRR changes, run focused tests, then full solution build/test and Acorn text + fresh-process binary readback using real materials from both games.

## P1 — Sonic naming changes are not tested or end-to-end validated

- **Description:** Inventory generation and build passed; automatic match quality, named scenes/material-preserving output, standalone animation resources, Acorn binary compile/reload, and actual UI paths remain unverified.
- **Severity:** High before exposing/releasing the feature.
- **Affected files:** New uncommitted naming files in PROGRESS.md.
- **Next action:** Add deterministic unit tests for actual inventory identities, scoring, Hungarian optimality, low-confidence automatic assignment, reuse, corrections, serialization name projections, and disabled-mode byte identity. Then run real selected conversions on both games and verify binary resource graph.

## P1 — Source animation match quality is unknown

- **Description:** 1,088 precomputed records contain ranked candidates, but no report has yet quantified top-1 assignment, conflict/reuse quality, confidence distribution, or erroneous semantic pairs against known expected mappings.
- **Severity:** High for user trust; feature applies uncertain matches without approval by design.
- **Next action:** Generate a compact machine-readable and text score report for both source games/profile combinations, inspect high/medium/low examples including different variants (start/loop/end, direction, ground/air, walk/run, dash/boost/jet), improve general semantic/score rules only when evidence supports it.

## P1 — Inventory profile file/binary names field needs schema verification

- **Description:** A diagnostic PowerShell read printed zero under `profile.files` although the generated inventory includes file records elsewhere and the script logged counts. Runtime typed model only requires animations/models/skeletons and preserves raw JSON. Confirm the actual profile JSON schema has all file/scene/resource references expected and avoid concluding the inventory is empty.
- **Severity:** Medium-high, because complete model/skeleton/animation inventory is required.
- **Next action:** Inspect concise JSON keys/counts with Python/JQ; add a schema-level completeness test against the generated profile and actual extracted paths.

## P2 — GUI correction editor and inventory viewer absent

- **Description:** GUI provides optional correction JSON picker/clear behavior, not an interactive row editor; optional inventory viewer is not implemented. Both are optional in the request. Do not block ordinary conversion on them.
- **Next action:** Only add if time and primary matching/verification complete; correction JSON format is documented in `ScuNameMatcher.cs`.

## P2 — Character replacement scope boundary

- **Description:** Naming resource files/internal AnimationPlayer identifiers is not rig retargeting or full SCU character replacement. The target `.tscn.remap` directs the engine to a compiled scene resource, but game-side character resource bundles and attachment/gameplay requirements may require additional SCU-compatible resource structures.
- **Next action:** Report that naming/asset identity behavior was verified only at export/resource level unless actual SCU runtime replacement is tested.

## Resolved historical bugs to guard

- Hint-color RGB values with source W=0 becoming alpha 0: current explicit RGB Color alpha 1 rule; add/retain tests.
- Sonic eye highlight `(2,-3,2.5)` becoming `(20,-30,25)`: current direct XYZ mapping; never apply mesh Spatial.Scale to this material uniform.
- Animation filename suffix “loop” conflicting with authored loop flag: preserve the source flag/timing during rename.
