# Scene visibility and entry-point hotfix (v0.8.1)

Two v0.8.0 export defects were reproduced against an actual Unleashed Sonic export.

## Fixed-path remap broke the editor entry point

The export wrote `chr_Sonic_SD.tscn.remap` beside the editable scene, redirecting it to `res://characters/player_Sonic/chr_Sonic_SD.tscn.converted.res` regardless of the destination project layout. A scene under `res://player_Sonic/` therefore redirected outside its folder. Earlier verification loaded the native file directly and did not exercise this final public text-scene entry point after the remap was added.

The converter now omits that sidecar. Text scenes keep their relative resource references, and SCU's existing original remap resolves the native resource at its canonical game path. Binary validation also loads the editable scene, and new integration tests run the complete ConversionService and open both text and native scene names from noncanonical subfolders for standard naming, matched Sonic, and matched Super Sonic.

For an existing editable v0.8.0 project, back up the stale `.tscn.remap` outside the project or rename it so it no longer redirects the scene. Do not remove the original game's own canonical remap. Re-export into a new folder with v0.8.1 for the complete correction.

## Missing character render layer

The original SCU Sonic and Super Sonic body nodes have `layers = 3`, enabling both world and character bits. The converter omitted this field, leaving Godot's default mask 1. Such a mesh is invisible to a camera restricted to the character bit (mask 2). The inventory previously recorded the node names but omitted this visibility property.

Exported meshes now explicitly use mask 3, including separated mouth parts and exports with name matching disabled. Visibility of mirrored mouths remains unchanged. The inventory extractor records the actual original mesh masks, and native/text validation checks the exported value.

## Executed validation

- Exact failed user scene rendered through a character-only camera: 0 visible pixels before the correction, 2,636 afterward, with the existing PRR materials and textures.
- The repaired native resource preserved all 161 bones, 303 animations, vertex/index arrays, material/shader parameter values, and sampler data in independent comparisons; only mesh layer masks changed.
- Both public entry points in the user's actual editor project loaded successfully after repair.
- Real Unleashed conversions with standard, Sonic-matched and Super Sonic-matched naming compiled and reloaded both resource formats from moved project folders.
- Synthetic standard and named meshes rendered successfully through a character-only camera.
- Complete suite: 492 Core + 19 GUI tests, **511 passed, 0 failed, 0 skipped**. Clean Release build: 0 warnings/errors.

Evidence and backups are kept locally under `artifacts/v0.8.1-hotfix/`; they are not release assets. The original user export copies are backed up with a restore map. This validates scene loading, character-layer rendering, and serialized data; full gameplay/rig integration is a separate concern, and in-game visibility confirmation is reported separately rather than inferred from resource loading alone.
