# Automatic Sonic asset naming

Enable **Match Sonic Asset Names** in Export settings to name the output for SCU's playable Sonic resources. Enable **Super Sonic** alongside it to use the Super Sonic resource set instead. Both options work with Generations and Unleashed input and either material export mode. Naming is off by default; preferences persist. No extracted SCU installation, network access, matching wizard, or approval step is needed.

With matching disabled, filenames retain the source model and clip stems: for example, `chr_Sonic_HD.tscn`, `chr_Sonic_HD.mesh.tres` / `.mesh`, `chr_Sonic_HD_Mouth.mesh.tres` and the original animation stem with `.tres` / `.anim`. The ordinary main mesh node may be called `Mesh`; that node label is not used as the output filename.

The embedded inventory was generated from the actual `player_Sonic` and `player_Supersonic` reference directories and original game PCK indexes. It contains 720 file metadata records, 21 scene records, 382 animation names, skeleton/mesh identities, track metadata and derived normalized motion descriptors. 303 Sonic and 75 Super Sonic clips have usable humanoid Skeleton tracks. Four effect-rig clips remain inventoried and are reported as unfilled instead of assigning incompatible humanoid tracks. Sparse track key counts are not claimed as authored frame counts.

Sonic uses scene `chr_Sonic_SD`, mesh `Sonic_Body`, root `Scene Root` and node `Skeleton`; Super Sonic uses `chr_SuperSonic_SD`, mesh `mesh`, the same root and Skeleton node. Bones are not renamed or retargeted. Text scenes use `.tscn`; native compilation writes the verified `.tscn.converted.res` resource. SCU already supplies its canonical remap, so the converter does not place that fixed-project-path `.tscn.remap` beside editable exports. The separately exported mesh file is named after the verified embedded mesh identity; the game did not necessarily ship a standalone mesh with that filename.

Animations are written under `animations/`, with target filenames and AnimationPlayer keys taken from the inventory. Original source identity remains available for parsing, pose checks and diagnostics. Material, texture and shader names are preserved. Timing, keyframes, interpolation and source playback behavior remain unchanged when names are applied.

Named native scenes embed their animations, in addition to the standalone `.anim` exports. This avoids Acorn's project-root resolution of external binary dependencies when moving a character folder inside a project. Large named banks therefore use more scene storage than exports that keep their animations external. Install the native resource at the verified SCU character resource location; the original game remap resolves it there. Exported meshes use render mask 3 (world and character layers), matching the original Sonic/Super Sonic bodies; this also applies when name matching is disabled.

### Updating an old v0.8.0 export

Re-export to a fresh folder with v0.8.1. If an old fixed-path `chr_Sonic_SD.tscn.remap` or `chr_SuperSonic_SD.tscn.remap` remains in an editable Godot project, back it up outside that project or remove that stale sidecar before opening the text scene. Keep the exported materials, textures, shaders and animations with the text resources. The game uses its original canonical remap when the native file is installed under `characters/player_Sonic` or `characters/player_Supersonic`; naming still does not retarget rigs or provide complete gameplay wiring.

## How matching works

The deterministic offline matcher combines normalized action names, start/loop/end phase, direction, gameplay context, normal/Super form, duration, native target loop metadata, root trajectory and normalized skeletal movement where available. Movement compares 17 time-normalized samples for six corresponding body anchors; it does not compare raw bone indices. A constrained dynamic time warp permits small timing differences. It cannot establish gameplay equivalence from missing motion data.

Global maximum-weight assignment first resolves competing source candidates. Remaining slots intentionally reuse the strongest available source. Every usable target is filled even at low confidence; only unavailable/incompatible slots remain unfilled. High, Medium and Low scores express evidence strength, not verified gameplay correctness. Action, direction and phase contradictions limit confidence. Unknown characters use the same runtime scorer. Precomputed, fingerprint/version-checked candidate profiles from 325 Generations and 219 Unleashed source animations reduce repeated analysis; these mappings are inferred, not author-certified correspondences.

The output includes `scu-animation-mapping.txt`, `scu-animation-mapping.json` and a `sonicNaming` section in `conversion.json`, with source/target identities, confidence, evidence, alternatives, reuse and unfilled slots. Each target name is unique. Source animations that exceed the target slot count may remain unused and are listed explicitly.

## Optional corrections

After inspecting a report, edit the JSON report's `overrides` object using actual target stems as keys and selected original source clip names as values. Keep `profile` as `Sonic` or `SuperSonic`. For example, this shape expresses a correction; replace placeholders with actual report names:

```json
{"profile":"Sonic","overrides":{"<target stem from report>":"<original source clip name>"}}
```

Load it with **Corrections…** or `--animation-name-map file.json`. Right-click Corrections to clear the saved file. Valid corrections apply automatically, while unknown names or a different profile are reported and ignored. An explicit missing or malformed correction file is a configuration error. No correction file is required. Inspect the portable distribution's `assets/SCU/sonic-inventory.json` for all real target names.

CLI usage adds `--match-scu-sonic-names`, optionally `--super-sonic`. `--super-sonic` and `--animation-name-map` require the master naming flag. CLI and GUI call the same matcher/conversion service.

## Compatibility limits

This feature supplies resource naming and best-effort action matching, not skeletal retargeting or a complete gameplay integration. Source rigs, helper bones and separate mouth meshes remain source-derived. Low-confidence substitutions can be unsuitable in gameplay and should be reviewed after export. Game-side attachments, character resource bundles and rig expectations may require additional mod integration. Binary loading and pose validation do not prove that a replacement works in the actual game.

## Rebuilding development metadata

Run `scripts/Build-ScuInventory.py --scu-root <extraction> --acorn <editor> --game-pck-directory <original-content/acorn>` to regenerate metadata, then run `tools/HE1ToSCU.ScuInventory` with the inventory JSON path, Generations Sonic directory and Unleashed Sonic directory. The latter builds inferred candidate profiles and provenance. Both steps are development tools, not end-user prerequisites. Change schema/scoring versions if the descriptor or scoring contract changes. Game binaries must not be committed or distributed.
