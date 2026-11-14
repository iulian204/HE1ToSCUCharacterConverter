# PRR parameter correctness audit

This audit checks stored material inputs and resource serialization. It does not establish original-game rendering parity.

## Current Release integration

The application now uses the supplied 2026-11-14 **Release** exclusively. All 152 original numeric material color declarations now intentionally omit `hint_color`, so those values serialize as raw Plane, including source W. This is the new shader API, not a reversion to the earlier blanket-Plane bug. Genuine foundation color hints still require Color; Sky/Ground retain the requested defaults. Original eye positions remain unchanged.

All 3,648 supplied native-state shader bytes are embedded. Coverage is selected before uniform mapping, and same-source materials in different slots have independent text/binary expectations. Original power samplers bind the new gloss names; per-map UV0/UV1 selectors are retained. Removed optional scene controls are recorded as unused rather than reintroduced. See [current mapping rules](PRR-MATERIALS.md).

Current rerun: 173 supported original materials, 1,582 raw records; 747 source mappings, 1,413 assignments and 436 samplers passed text/binary checks, with zero failed validations. Removed optional controls explain the lower mapping count. All 456 coverage interfaces / 11,650 declarations were checked in both engines.

The earlier findings and counts below describe the preceding shader interface. They remain historical evidence, not measurements of the new Release. Current verification is recorded in [VALIDATION](VALIDATION.md).

## Earlier causes and corrections

- **Wrong Variant contract for colors.** The previous mapper ignored hints and emitted Plane for every vec4. Acorn's property API reports Color for `hint_color` and Plane for unhinted vec4. The old values could round-trip while remaining the wrong inspector/property type. Color-hinted inputs now use Color; coefficient/packed vec4 data keeps Plane. Plane can reach some renderer paths as a raw vector, so this finding is not a claim that every old Plane necessarily rendered black.
- **Untyped scalar serialization.** Whole-number float values such as `1` loaded as int Variants. Float output now explicitly uses real literals, including `1.0`/`0.0`. Integer/bool conversions are checked rather than truncated or inferred. Integer runtime comparisons are exact, avoiding float rounding near signed 32-bit limits.
- **Incomplete source capture.** The reader previously handled only float4 and skipped unnamed/empty records, losing evidence needed to explain unused inputs. Mirage v1/v3 int4 and byte bool4 tables, arrays, flags, duplicate payloads and reserved slots are now retained separately. The legacy float dictionary remains compatible with SpatialMaterial, including empty duplicate handling.
- **Overgeneralized interpretation.** Ordinary `emissive` maps to vec3 material emission; dedicated `mrgChrEmissionParam` maps to the different vec4 enemy/IgnoreLight emission register. Packed XZ/W and multi-output GI bindings are explicit. Unknown packed scalar lanes, ambiguous bindings and unannotated matrix/array layouts are classified instead of guessed.
- **Validation checked persistence, not correctness.** The old text snapshot could preserve an already incorrect value into binary. New independent expectations are derived before resource serialization. Acorn checks declared types, actual Variants, numeric values and original decoded sampler pixels before native save and again in a fresh process. Snapshots additionally check shader/binary retention.
- **Missing environment defaults.** Applicable materials now explicitly receive Sky `Color(0.5,0.5,0.5,1)` and Ground `Color(0.25,0.25,0.25,1)`. Verified authored inputs override these values. No unrelated defaults or authored RGBA alpha values are replaced.
- **Unrepresentable sampler settings.** Mixed-axis/mirror-once/border wrapping is reported as unsupported instead of silently becoming repeat. All fallback and unbound source texture records are included in the audit, not only successful PRR bindings.

No SpatialMaterial fitting/roughness/metallic/compensatory-color formulas were found in the PRR numeric mapping path. None was added. The supplied shader code and alias matching are unchanged. Godot applies its native Color linearization; the converter preserves stored source components without an inverse-gamma workaround.

## RGB semantic correction after the initial audit

The first audit incorrectly treated W in RGB-only HE1 inputs as Godot Color alpha. This could preserve zero perfectly through text and binary while still producing an incorrect material. Verified RGB bindings now set Color alpha to 1, retaining the original W in the raw record. HedgeLib's material adapter explicitly reads diffuse/ambient/specular/emissive as RGB; original eye pixel shaders consume highlight color XYZ. True RGBA inputs and packed emission W are not changed.

Eye highlight position is now transferred directly from authored HE1 XYZ to the PRR uniform, consistently for an alias or an exact-name source parameter. The previous alias-only factor of 10 was an unwanted reinterpretation of this authored PRR input. The user confirmed the original values produce the desired result; a paired Acorn render was also inspected, without treating it as original-game parity. Mesh/skeleton scaling and physical camera-attenuation distances are unchanged. Direct material-parameter preservation here does not claim coordinate-equivalent highlight directions after mesh scaling.

The following runtime counts verify storage, declared types and the documented mapping outputs. They do not independently prove every shader-semantic interpretation or original-game appearance.

## Scope and results

| Check | Result |
|---|---|
| Confirmed current shader libraries | 152 programs, 99 Generations + 53 Unleashed; bundled source bytes unchanged |
| Complete declaration inventory | 4,773 uniforms, 82 distinct names; hints, defaults, sampler declarations and usage snippets retained |
| Godot 3.1.2 / Acorn API comparison | All 4,773 names and declared property types matched in both engines |
| Original material files enumerated | 180; 173 parsed, seven pre-existing unsupported Mirage v2 files reported |
| Original parameter records audited | 1,582 |
| Independent source binary check | All 1,582 raw name/flag/value-byte records matched across 173 files |
| Original source parameter assignments read back | 911 in text and binary; 671 intentionally unused/reserved/fallback records retained |
| Typed assignments verified | 1,603 in each stage, including header/texture metadata and defaults |
| Confirmed legacy Variant corrections in assignments | 637: 424 color-Variant contracts and 213 integer-form float literals |
| Original texture entries accounted for | 478; 436 PRR sampler bindings independently pixel/flag-verified in text and binary |
| Individual SpatialMaterial fallbacks | Eight unavailable PRR shader assignments; their source parameters/textures remain in the report |
| Failed runtime readback validations / unclassified numeric inputs | Zero |
| Synthetic all-shader runs | Every one of the 152 shaders tested with requested defaults and separately with authored overrides; includes all declared integer/bool controls |
| Native negative tests | Wrong Color Variant, black replacement, int-form float, wrong large integer, zeroed vector and changed sampler pixels were rejected |

Original assets exercised ChrEye, ChrSkin, Common, EnmEmission, EnmGlass, EnmIgnore, EnmMetal, IgnoreLight, Luminescence, SonicCloth, SonicEnamel, SonicEye, SonicMetal and SonicSkin. All remaining supplied programs were exercised through separately labeled synthetic fixtures; they are not presented as original-game asset evidence.

The parsed original corpus contains float4 records only, with one vector per record. Int4/bool4 endian/stride handling and multi-vector retention were checked with deterministic Mirage v1/v3 fixtures. Explicit matrix serialization and integer/bool value preservation were checked in native Acorn tests. Unknown source matrix layouts remain unsupported rather than being inferred from four vectors.

## Reports and reproduction

- [Complete shader/uniform inventory](PRR-SHADER-UNIFORMS.json) includes actual Godot/Acorn property readbacks and getter results for authored defaults. A nil getter is recorded honestly; it is not treated as proof that the shader's authored default is zero.
- Every PRR export writes `prr-parameter-audit.json`, with raw inputs, per-parameter classifications, expected typed values, origins/evidence/confidence, sampler diagnostics and text/binary readbacks. No-editor text exports explicitly remain `not_runtime_verified`.
- [Mapping rules and limitations](PRR-MATERIALS.md) document every alias and type policy.
- Run `dotnet build HE1ToSCU.sln -c Release`, `dotnet test HE1ToSCU.sln -c Release`, and `scripts/Publish.ps1`. Native xUnit checks use `HE1SCU_TEST_ACORN`, or the existing local Acorn path. If neither exists, those tests are explicitly skipped rather than reported as executed.

Whole-game appearance, runtime scene inputs, unsupported Mirage v2 materials, uniform arrays and unknown source matrix layouts remain outside the verified boundary. The existing geometry, skeleton, animation and SpatialMaterial conversion algorithms were not replaced.
