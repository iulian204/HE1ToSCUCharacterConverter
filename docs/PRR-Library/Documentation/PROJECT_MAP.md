# File map, history and continuation

## Release files

| Path | Purpose |
|---|---|
| Generations, Unleashed | Default Opaque/Normal/SingleSided shaders with original material identities |
| Render States | Other23native coverage/blend/sidedness presets, including matching Materials folders |
| Materials | Optional ordinary native ShaderMaterial templates with relative shader references |
| Inventory/SHADERS.md, Inventory/Shaders | Complete readable catalog and per-program parameter/source/behavior pages |
| Inventory/shaders.json | Machine-readable canonical catalog, exact parameter declarations/defaults/stages, source/list links, source-review operations and validation distinctions |
| Inventory/parameters.json | Every uniform name, descriptions, distinct declarations and programs using it |
| Inventory/presets.csv, presets.json | All3,648 shader/material paths, state flags, alpha-pass/depth behavior and hashes |
| Inventory/aliases.json | Four exact aliases mapped to their canonical material programs |
| Inventory/unresolved-source-links.json | Ten remaining source-companion evidence limits |
| Validation | Fresh release results plus retained source/GPU/publication evidence and immutable foundation reference |
| Comparisons | Controlled before/after and representative accepted preview images with provenance |
| release-manifest.json, SHA256SUMS.txt | Exact packaged file integrity, counts, validation scope and provenance |

No tools, runtime scripts, editor plugins, engine binaries or original game textures are required from this Release folder. The build-time toolchain and full failed/historical evidence stay safely in the original project. Per-source evidence paths in the inventory are relative to that project, not promises that the source game files are bundled here.

## Original project (not runtime dependencies)

Project root: `D:/HE1TOPRRShaders`. Immutable foundation: `PRR shaders/ChrSkin_dsnfe1.shader`. Original sources: `he1shaders/gens hlsl`, `he1shaders/unleashed hlsl`, `he1shaders/unleashed asm`. Shader lists determine actual program identities and stage pairings.

`CURRENT_STATE.md` is the continuation entry point; `PROJECT_PROGRESS.md` is chronological history. `PROJECT_HANDOFF.md` is a historical export and contains superseded counts. `PRR shaders/conversion-manifest.json` is the canonical source/verification record. Its `active_outputs` identifies152current programs; historical `outputs` entries are not additional required deliverables.

The `PRR shaders/Generations` and `Unleashed` directories are verified **generator inputs/history with the older richer interface**. The current user's minimal script-free/two-UV deliverable is **this Release folder**, derived from `Shader Playground/Native-Render-State-Shaders-Eye-Repair-2026-11-14`. Do not replace it by copying the older PRR directories or RuntimeHelpers.

Build-time tools: `adapt_remaining.py` integrates source behavior into the actual foundation; `check_foundation.py` checks actual preserved/adapted functions; `shader_dependencies.py` performs conservative backward liveness across helpers/stages/branches; `material_interface.py` specializes the material-editor interface; `uniform_layout.py` and `shader_style.py` preserve source-aware naming/order/style. `fragment_aggregation.py` restores source-proven interactions. `native_render_presets.py` selects genuine native draw state; `native_two_uvs.py` removes optional packed extra UVs. `build_release.py` verifies every final preset against both generators and builds the complete catalog. `release_documentation.py` writes these user guides.

`stage_native_preset_checks.py`, `verify_native_presets.gd`, `run_native_check.py`, `run_material_profiles.py` and source numerical tests provide stock-engine and operation checks. They are validation utilities, not dependencies of the shipped materials. `cubemap_atlas.py` is the shared directional cross lookup generator; `pack_cube_mips.py` packs original DDS mips offline; the existing preview `he1_cross_texture.gd` demonstrates constructing a full atlas ImageTexture. That demonstration is outside this script-free library, and is only needed by a custom original-mip import workflow.

The original-asset comparison projects are `comparison-preview` (Unleashed, original textures from `C:/Users/jigai/Desktop/CorrectSUSonic/Sonic`) and `comparison-generations` (model from `C:/Users/jigai/Desktop/Gens Sonic/chr_Sonic_HD`). Their opt-in `--native-presets=<absolute library root>` path uses ordinary materials. Do not sync older candidate shaders over the release or retune original material values/camera/light during regression captures.

## Condensed development history

Earlier mechanical register translations lost the foundation custom light/region/panorama/rim system and generated incorrect standalone stage outputs. Those were archived (including1,485historical stage-derived files). The strategy was rebuilt around actual foundation copies plus source-supported differences and shader-list stage pairing. Unleashed HLSL was added alongside independent ASM evidence; mismatched variants were not merged.

Historical scopes640/568included permutations later explicitly excluded by the user. After base-only selection and the added IgnoreLight/Luminescence source lists, the final scope became152canonical base programs and4aliases. All152implementations were completed; no historical56/611pending conversions should be restarted.

Accuracy work corrected numeric material color conversion, game-specific diffuse/ambient separation, native specular transfer, missing second specular contributions, eye masks/highlight signs and order, actual foundation fake_specular restoration, source-defined BINORMAL handling, cubemap orientation/ties and proven fragment addition. Original foundation/source files and Godot source stayed unchanged. Failed probes/stages were preserved and corrected at their actual oracle/dataflow cause, rather than hidden by gains.

Later interface cleanup removed optional engine-only controls, added per-map UV selectors and minimal original material parameters, defaulted Common GI false and reordered/renamed gloss inputs. Script-based render-state helpers and auto-width packed UV experiments were superseded by the user's explicit script-free/UV0-and-UV1-only requirement. The current fixed native-state library is the accepted implementation of that requirement; earlier helper libraries are preserved as history, not shipped.

The latest Generations eye repair changed only72presets across three canonical programs. It retained standalone reflection/fake/highlight/native-light behavior and restored source-backed ambient/reflection/highlight interactions. Other3,576preset shaders and all3,648material templates stayed byte-identical to their verified parent. This release packages those verified results without retuning or rewriting shaders.

## Resuming research

Begin with this release's receipts and the newest `CURRENT_STATE.md` entry. Check hashes before changes. Work only on a source-supported unresolved behavior, stage/validate candidates and preserve the accepted appearance of unaffected materials. Distinguish implementation, parse/draw, foundation, source-operation, binding and actual-asset visual validation. Never claim an original-game framebuffer match from compilation or isolated tests alone.
