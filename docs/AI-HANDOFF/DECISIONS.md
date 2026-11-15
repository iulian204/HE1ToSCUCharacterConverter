# Significant Decisions and Constraints

## PRR conversion

1. **PRR export is optional and separate from SpatialMaterial.** Keep SpatialMaterial mode unchanged when PRR is disabled; PRR materials should receive original HE1 inputs rather than GGX/roughness approximations.
2. **Evidence before semantic coercion.** Exact name match is useful but not enough when source storage is packed. Aliases specify lanes explicitly. Reject ambiguity, unsupported arrays, occupied packed lanes without a verified rule, nonfinite values, and invalid integer/bool coercions; preserve raw values in audit.
3. **Godot hint_color means Color Variant.** It is not interchangeable with Plane even though both serialize four numbers. For fields established as source RGB, use alpha 1; do not apply that to all float4 parameters.
4. **The user's observed eye highlight values take precedence over coordinate-scale speculation.** `g_SonicEyeHighLightPosition` is authored material data, not vertex position; preserve `(2,-3,2.5)`. Keep any separately justified eye-light range adjustment distinct.
5. **Defaults are deliberately narrow.** Sky/ground defaults only initialize the corresponding declared uniforms; explicitly mapped source values take precedence. Do not spread arbitrary defaults to ordinary material colors.
6. **Independent runtime oracle.** Expected values are generated before serialization and compared against Godot/Acorn loaded Variant types/values and texture data. Roundtrip is serialization evidence, not semantic/visual truth.
7. **Shader libraries remain those supplied by the user.** Do not silently select unrelated shader sources or edit shader code to compensate for mapper mistakes.

## Sonic asset naming

1. **Offline and automatic.** Bundle inventory and source candidate cache; user does not supply SCU files or approve uncertain mappings.
2. **Exact label and profiles.** UI label is “Match Sonic Asset Names”; separate “Super Sonic” selects actual `player_Supersonic` profile. Master defaults off and Super option depends on master, while saved preference may remain stored.
3. **Preserve source identity/data.** Keep source names for parsing/pose validation and separately attach target export names. Renaming does not change keyframes, transforms, interpolation, duration, root motion or native loop flag.
4. **Score explains, does not block.** Matching is deterministic, records confidence/evidence, applies best available even when low confidence. Global assignment first; reuse only for targets remaining without a distinct source.
5. **Profile scenes reflect actual game data.** Sonic main mesh resource name `Sonic_Body`; Super Sonic `mesh`; both root `Scene Root` and Skeleton node `Skeleton`. Native binary scene resource and remap identities come from original PCK, not guessed names.
6. **Motion comparison has limits.** Normalize descriptors using semantic bone aliases and rest scale; do not compare raw bone indices. Missing motion evidence is not similarity. No claim of gameplay action or full character retarget based on name/motion descriptors.
7. **Optional manual corrections happen afterward.** JSON corrections are optional; they are not a wizard/gate. Inventory viewer and interactive mapping table were not implemented and are optional.

## Preserve the current work

- Current changes are uncommitted and all belong to the requested naming work. Do not reset or clean.
- Current stable v0.7.0 remains the comparison baseline. Do not alter its version or publish a new release without an explicit user instruction.
- The next requested work prioritizes PRR semantic correctness audit, then naming tests/integration; no conversion algorithms should be rewritten wholesale.
