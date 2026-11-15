# Changelog

## 0.8.0

- Added optional automatic Sonic and Super Sonic resource naming with a bundled offline asset inventory.
- Added motion-aware animation matching, confidence reports and optional saved corrections for GUI and CLI exports.
- Preserved source animation playback and material identities across named text and native Acorn resources.
- Fixed named native scenes to retain animations when moved within a project, with standalone animation files also exported.
- Added regression coverage for original output filenames, shader-library freshness, GUI preferences and large native animation banks.

## 0.7.0

- Add optional PRR ShaderMaterial export for Sonic Generations and Sonic Unleashed, with saved GUI settings and matching CLI options.
- Include 152 shader programs and 3,648 native presets, selecting coverage, additive blending and sidedness from source assets.
- Bundle only used shaders, or reference an existing project library with validated paths and self-contained Acorn binaries.
- Preserve original numeric parameters, eye-highlight positions, texture channels and per-map UVs; initialize applicable Sky/Ground colors to 0.5/0.25.
- Add detailed parameter audits and independent text/binary validation while preserving default SpatialMaterial output and existing model, animation and mouth workflows.

## 0.6.2

- Use an opaque Light or Dark Fluent background when Windows or the compositor cannot provide Mica.
- Apply Windows transparency changes live while retaining the existing appearance modes and Mica focus fade.

## 0.6.1

- Replace WPF with Avalonia Fluent while preserving the existing conversion workflow and settings.
- Adopt Windows 11 card and control styling, with native window chrome, system accent colors and a focus-aware Mica backdrop where supported.
- Make the animation list easier to read, with an empty state and clearer selection and focus indicators.
- Choose Automatic, Light or Dark from the main window. Automatic follows the Windows app theme live.
- Bundle .NET 10 LTS in the portable Windows x64 release. Existing Generations and Unleashed formats and export options carry over.

## 0.6.0

- Recover missing tangents from mesh geometry and UVs, fixing exports such as Generations Shadow.
- Route secondary UV normal and emission maps correctly and embed their textures in native exports.
- Improve Generations enemy materials with per-pixel painted-metal reflectance, brighter glass and authored emission.
- Restore textured boost and stomp effects, and improve shader-specific gloss and depth mapping.
- Improve export stability and diagnostics for malformed geometry, large textures and optional maps.

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
