# Changelog

## 0.4.0

- Added Sonic Unleashed conversion support for models, skeletons, animations and materials, alongside existing Sonic Generations support.
- Added a game selector to the desktop UI and game selection to the CLI.
- Fixed skeletal distortions and improved animation interpolation, sparse tracks, scale handling and standalone additive playback.
- Improved SpatialMaterial accuracy with game-specific specular/roughness handling, stable gloss-map baking, corrected eye normals and refined color/highlight balance.
- Improved large animation-bank exports, native resource validation and compatibility while preserving original asset names.

Validated with 63 automated tests and all 544 supplied character animations across both games. Materials remain standard Godot 3.x SpatialMaterial approximations; SCU gameplay retargeting and in-game acceptance remain separate work. Move companion `.anim` files with large-bank `.scn` exports.
