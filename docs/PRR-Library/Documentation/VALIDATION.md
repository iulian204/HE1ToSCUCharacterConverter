# Verification results

## Fresh Release checks (2026-11-14)

| Check | Result | Evidence |
|---|---|---|
| Canonical inventory | 152 programs:99Generations,53Unleashed;4exact aliases;no missing implementation | Inventory/summary.json |
| All native presets and ordinary materials | 3,648shaders and3,648materials;24states each | Inventory/presets.csv |
| Exact regeneration of final files | 3,648/3,648match current source generator + native-state specialization + two-UV specialization | Validation/artifact-audit.json |
| Final dependency analysis | All744distinct GPU bodies:0unused uniforms,0unreachable helpers | Validation/artifact-audit.json |
| Source foundation/style/interface audit | 152passed:145foundation-lit,7source-unlit | Validation/source-foundation-audit.json |
| Original-source numerical regression suites | 22passed,0failures/errors | Validation/original-source-numerical-tests.json and .log |
| Fragment-weight parser regression | 3passed | Validation/fragment-weights-tests.log |
| Eye generator/policy regression | 1test covering all3eye projections passed | Validation/eye-pipeline-tests.log |
| Actual packaged native materials in nested project folder | 3,648resource/interface checks passed,0failures | Validation/release-native-results.json |
| Exact stockGodot3.1.2GLES3 full-source lit draws | 744distinct GPU bodies passed;stdout/stderr checked | Validation/release-native.out and .err |
| Immutable foundation | Original SHA-256 unchanged | Validation/original-foundation.shader.txt |

The nested project test loads the packaged ordinary `.tres` files and checks numeric types, actual Inspector ordering, UV default0, Common GI false, Punch threshold0.502 and absent runtime material scripts. Blend/cull header variants share GPU bodies; every native state has separate analytic evidence below. A basic lit draw verifies actual parsing/shader compilation, **not** material appearance with source assets. The final replay changes no shader or material bytes.

Stock executable used: `C:/Users/jigai/Documents/Codex/2026-11-01/act-as-a-senior-graphics-programmer/work/godot-3.1.2/Godot_v3.1.2-stable_win64.exe`. Fresh logs identify `3.1.2.stable.official` and the GLES3 renderer. No engine source edits or custom build were used.

## Retained native-state/UV evidence

The validated parent library passed8,512native blend/alpha/front-back/custom-cutoff GPU cases,48native shadow cases,1,548UV0/UV1/offset cases and152native shader-switch/save/reload profiles. Their individual results/logs are included under Validation/parent-*. Exact state/fragment/light proofs preserve these checks through the two-UV specialization. The eye repair subsequently changed source RGB accumulation only in72presets; it has its own original-source/GPU proof and all final full-source resources were checked again in this Release.

The paired parent HDR comparison had456frames:433bit-identical;the other23each differed at one component by one adjacent finite half-float storage code. Maximum measured absolute difference0.00048828125. These are reported storage-boundary differences, **not** called bit-identical. Raw diagnostic frames and strict failed evidence remain in the original workspace; the per-component boundary report is bundled.

Before the intentional eye repair, actual-asset comparisons had7Generations and5Unleashed images pixel-identical;2Unleashed images each differed at one component of one pixel by one8-bit step. Inputs, textures, camera/pose/light/tone mapping were fixed. These historical regression statements describe that parent, not a claim that the eye repair should produce the old incorrect combined eyes.

## Generations eye repair evidence

432original-HLSL fixture cases and864baseline/candidate GPU executions passed across the three eye projections. The72changed presets passed72material/interface checks and15distinct full-source lit draws. The source generator replays all152canonical programs identically. Other3,576preset shaders and all3,648plain materials are byte-identical to the verified parent. Current Release regeneration and all3,648resource checks independently include the repaired files.

Actual e1 Sonic eyes:24,727eye pixels;reflection changed0displayed eye pixels before and13,713after,maximum12RGB levels of reflection contribution. Full combined correction changes13,714eye pixels (including source-required ambient/highlight interactions),max61RGB levels. Changes are confined to eye meshes. Fake,native scene-specular,standalone reflection and authored-highlight images remain pixel-identical. The other two projections are source/GPU-tested without the same actual-asset coverage. See GENERATIONS_EYE_REFLECTION.md and the included Comparisons/Generations before/after images.

## Appearance and remaining boundaries

**All14fresh Release-path images are pixel-identical** to the currently corrected reference:7Unleashed and7Generations modes (combined,fake,scene_specular,no_fake,no_scene_specular,reflection,authored_highlight). Zero differing pixels/components in every mode. These final captures were performed after the stock-engine/source checks.

Fresh release-path representative comparisons are recorded in `Validation/release-comparisons.json` and `Comparisons/Release-Verification` after all mathematical/material checks. They compare against the currently corrected accepted library, not the superseded eye result. All source material values and preview conditions are held fixed.

Compilation, basic lit draws, foundation fidelity, isolated mathematical equivalence, runtime binding and whole-image/original-game visual parity are distinct. All152programs are implemented; full original-game appearance equivalence has not been established for all152. Four Unleashed eye local-light cases, framebuffer/animation/source-companion/resource/atlas filtering boundaries remain in [LIMITATIONS](LIMITATIONS.md). Historical source-library records can mention retired helper/UV interfaces; Inventory documents the actual final interface. No unverified visual fix was introduced while packaging.
