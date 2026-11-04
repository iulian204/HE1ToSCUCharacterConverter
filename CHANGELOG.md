# Changelog

## 0.5.0

- Automatically separate mouth geometry using bone weights, including both Generations mouths and their teeth, while sharing the existing Skeleton and animations.
- Added an Unleashed-only **Generate Mouth_L** option in the GUI and CLI; it defaults off and creates a hidden alternate mouth when enabled.
- Generated mirrored mouths have independent `_L` bones, remapped weights and matching animation tracks, with corrected normals, winding and tangent handedness.
- Export each mouth as a named mesh resource in the same character scene, retaining original materials and model filename stems.
- Expanded native export and relocation checks to validate every mesh, shared Skeleton binding and generated-mouth visibility.

Validated with 75 automated tests and all 544 supplied animations across both games, including independent mirrored-skinning checks for every Unleashed clip. Generated `Mouth_L` starts hidden; switch mouth visibility to use it without overlapping surfaces. In-game SCU acceptance and gameplay retargeting remain unverified.

## 0.4.0

- Added Sonic Unleashed conversion support for models, skeletons, animations and materials, alongside existing Sonic Generations support.
- Added a game selector to the desktop UI and game selection to the CLI.
- Fixed skeletal distortions and improved animation interpolation, sparse tracks, scale handling and standalone additive playback.
- Improved SpatialMaterial accuracy with game-specific specular/roughness handling, stable gloss-map baking, corrected eye normals and refined color/highlight balance.
- Improved large animation-bank exports, native resource validation and compatibility while preserving original asset names.

Validated with 63 automated tests and all 544 supplied character animations across both games. Materials remain standard Godot 3.x SpatialMaterial approximations; SCU gameplay retargeting and in-game acceptance remain separate work. Move companion `.anim` files with large-bank `.scn` exports.
