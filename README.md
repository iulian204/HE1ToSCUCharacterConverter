# HE1 → SCU Character Converter

A standalone **C# / .NET 10 desktop application** with an Avalonia Fluent interface, a reusable managed conversion library and a CLI.

Converts extracted Sonic Generations and Sonic Unleashed HE1 skeletal models, materials and matching Havok skeletons/animations into Godot 3.1.2 text resources. When an Acorn editor is selected, it also compiles and validates native Acorn binary resources.

## Run

Download the Windows x64 portable ZIP from [Releases](https://github.com/iulian204/HE1ToSCUCharacterConverter/releases/latest), extract it, and launch `HE1ToSCU.App.exe`. The .NET 10 runtime is included. Supply your own game assets and Acorn editor.

The **Appearance** selector offers **Automatic**, **Light** and **Dark**. Automatic follows the Windows app theme as it changes. The selection is saved immediately. The window uses Mica on supported Windows 11 systems when **Transparency effects** is on. A solid Fluent background is used on Windows 10 or whenever Mica is unavailable. Changing transparency takes effect without restarting. Windows controls the appearance of its native file dialogs.

Open `artifacts/HE1ToSCU-win-x64/HE1ToSCU.App.exe` after publishing, or run:

```powershell
dotnet run --project src/HE1ToSCU.App
```

1. Select **Sonic Generations** (default) or **Sonic Unleashed**, then pick an extracted source folder or a `.model` file. You can also paste a source folder and press Enter.
2. Choose the model. Its same-name `.skl.hkx` is selected automatically when present.
3. Add the matching `.anm.hkx` animations. An animation export requires the matching skeleton.
4. Choose a **new** export folder. Existing folders are never overwritten.
5. Select an Acorn editor executable to produce binary resources, or leave this field blank for editable text resources.
6. Click **Convert character**. Logging, progress, cancellation, and log saving are available.

Scale and coordinate conversion are automatic. There are no scale/orientation switches.

The published Windows x64 executable includes the .NET runtime. Acorn itself is **not** bundled; it is needed only for engine validation and binary resource compilation. Parsing, decoding, transforms and text resource writing are implemented in the external C# application.

## Output

| File | Purpose |
|---|---|
| `chr_Sonic_HD.tscn` | Editable Spatial → Skeleton → MeshInstance scene with AnimationPlayer |
| `chr_Sonic_HD.mesh.tres` | ArrayMesh with vertex channels, clockwise triangles, skin weights and source material references |
| `materials/<source-name>.tres` | Built-in SpatialMaterial approximations of the HE1 materials |
| `textures/<source-name>.texture.tres` | Decoded source DDS textures embedded in editable ImageTexture resources |
| `sn_dead.tres`, etc. | Native Godot Animation resources with quaternion transform tracks |
| `chr_Sonic_HD.scn` | Acorn binary PackedScene with embedded mesh/materials and embedded or companion animations |
| `chr_Sonic_HD.mesh` | Standalone binary ArrayMesh |
| `chr_Sonic_HD_Mouth.mesh.tres` / `.mesh`, `_Mouth_L.mesh.tres` / `.mesh` | Separate weighted mouth meshes; the second is generated when a second mouth branch exists |
| `sn_dead.anim`, etc. | Standalone binary Animation resources |
| `conversion.json` | Source paths, conversion counts, spatial profile and limitations |
| `acorn-validation.log` | Text load, pose comparisons and native save results |
| `acorn-binary-validation.log` | A second, fresh Acorn process loads binaries and compares bone positions |

Original source stems are preserved: `chr_Sonic_HD.model` produces `chr_Sonic_HD.tscn`/`.scn` and `chr_Sonic_HD.mesh`; `sn_dead.anm.hkx` produces `sn_dead.tres`/`.anim`. Filename collisions are reported instead of replacing names with numeric identifiers.

Binary files and engine logs are generated only when an editor is selected. Meshes, materials and textures are embedded. Large animation banks use relative companion `.anim` files to stay within Godot 3.1's resource limits: move these files together with the `.scn`. Smaller banks embed animations as well. Source and staging paths are not required by the relocated binary resources.

Mouth separation is automatic and follows mouth/jaw/lip/teeth/tongue bone branches and their skin weights, including mouth geometry sharing body materials. Each triangle belongs to exactly one mesh; vertex attributes, weights and bone indexes are preserved. The body stays at `Skeleton/Mesh`; Generations Sonic adds `Skeleton/Mouth` and `Skeleton/Mouth_L`, while Unleashed Sonic adds `Skeleton/Mouth`. All use the same Skeleton and AnimationPlayer. Models without weighted mouth geometry keep a single mesh. The export manifest lists every mesh and its source mouth-bone branch.

When **Sonic Unleashed** is selected, the optional **Generate Mouth_L** checkbox appears beside the game selector. It defaults off and remembers your preference. Enabling it creates a reflected alternate mouth with its own `_L` bone branch and mirrored animation tracks. Geometry, normals, triangle winding and tangent handedness are adjusted together. The original mouth starts visible and generated `Mouth_L` starts hidden: switch their visibility to use the alternate mouth without overlapping surfaces. Both remain children of the same Skeleton. Existing Generations mouths are retained as authored.

## Supported in this increment

- Standard big-endian HE1 Mirage skeletal `.model` containers, versions 2/4/5/6. Version 5 is validated against both supplied Sonic character models; real Unleashed v4 effect models and synthetic version fixtures also exercise the shared parser. Layout selection follows the file version, rather than assuming every Unleashed asset is v4.
- Opaque, transparent, punch-through and named special mesh slots; strip restarts and degenerate triangle handling; mesh-local bone palettes remapped to skeleton indexes.
- Float, half, byte/short normalized and selected packed normal vertex encodings. Unsupported encodings produce a specific error.
- Positions, normals, tangents/handedness, vertex colors, UV0/UV1, four bone indexes and normalized weights. Additional UV/color sets have no direct Godot 3 ArrayMesh equivalent and are not exported.
- **32-bit Havok 2010.2.0 (Generations) and 5.5.0 (Unleashed) binary packfiles:** skeletons, reference transforms, interleaved and spline-compressed animation, and transform-track-to-bone binding maps. The supplied Unleashed containers are big-endian with little-endian compressed spline payloads.
- Spline scalar 8/16-bit quantization; quaternion Polar32, ThreeComp40, ThreeComp48 and uncompressed encodings. Blocks use the actual block-offset table, including overlapping boundary frames.
- XML `hkpackfile` skeletons and interleaved animations.
- Binary/XML `hkaDefaultAnimatedReferenceFrame` extracted root motion: XYZ translation and heading about the authored up axis are preserved on the model root. Motion is independent of skeletal bone tracks. In-place clips explicitly reset root motion; sparse complete turns retain their intermediate rotation keys. Clips retain authored motion over one cycle; gameplay accumulation across repeated cycles remains the responsibility of the character controller.
- Automatic 10× positional scale for SCU character space, while preserving the shared Y-up basis. Translation is scaled consistently in geometry, bind/rest matrices and animation.
- Exact-name Havok → model bone mapping, an unambiguous legacy `@LT` alias fallback, parent hierarchy checks, rest-relative Godot bone poses, quaternion normalization and sign continuity. No Euler angle conversion.
- Rigid control bones preserve HE1 scale inheritance without scaling child translations or introducing shear. Signed and zero scales retain their rotations. Original skin bone names, indexes and global bind transforms remain intact.
- Continuous spline sampling, explicit block timing and adaptive subframe baking. Sparse tracks restore the matching reference pose; additive clips are baked against that pose for standalone playback.
- Standard Mirage `.material` v1/v3 float parameters and texture entries. Diffuse, packed normals, specular/gloss, emissive, opacity and ChrSkin falloff inputs are adapted to built-in SpatialMaterial. Materials and textures are found beside the model or in adjacent `textures`/`images` folders.
- Managed DDS decoding for BC1/2/3/4/5 and common uncompressed RGB/RGBA formats; generated mipmaps and embedded textures require no external texture converter or Godot import step. See [material mapping](docs/SPATIAL-MATERIAL-MAPPING.md).

## Explicit limits

This is a usable first conversion slice, **not a complete game-ready character replacement pipeline**.

- 64-bit packfiles, tagfiles, quantized animation (`hkaQuantizedAnimation`), delta/wavelet codecs and big-endian compressed spline payloads are unsupported. For a folder with mixed codecs, select the supported clips individually.
- Float/morph animation tracks and model morph targets are rejected. Additive clips become standalone reference-pose animations, not runtime additive overlays. Looping is inferred from `loop` in the source name; other clips can be set to loop in the target editor.
- Archives must already be extracted. `.ar`, `.ar.00`, `.pac` and PCK import/repacking are not implemented.
- Material conversion approximates the original shaders; it does not reproduce HE1 light probes, colored per-pixel specular, reflection/matcap sampling or custom eye highlights. Unsupported DDS codecs (including BC7), texture arrays and volume/cube textures produce specific errors. Missing material files use neutral fallbacks; missing textures are logged.
- Original HE1 bones and bind transforms are retained, with scale-compensation helpers added when needed. SCU player bone retargeting, attachment mapping, game scripts, animation trees, collision and gameplay wiring remain separate work.
- The 10× profile is directly checked against Generations/SCU Sonic reference bones. It has not been independently established for every asset from every HE1 game.
- Engine loading and numerical bone checks do not establish in-game acceptance, visual fidelity, DX11/shader-cache compatibility or complete renderer parity.

## Build and test

Install the .NET 10 SDK specified in `global.json` or a compatible newer .NET 10 SDK.

```powershell
dotnet build HE1ToSCU.sln -c Release
dotnet test HE1ToSCU.sln -c Release
./scripts/Publish.ps1
```

Automated tests cover formats, transforms, animation sampling, both Havok profiles, DDS and material conversion, mouth geometry and the Avalonia workflow. Real-asset regression validation covers all 325 supplied Generations animations and 219 supplied Unleashed animations. See [validation results](docs/VALIDATION.md) for the evidence and boundaries. No copyrighted game fixtures are bundled.

CLI example (repeat `--animation` for more clips, or use `--animation-folder` to load every `.anm.hkx` in a folder):

```powershell
dotnet run --project src/HE1ToSCU.Cli -c Release -- `
  'D:\SteamLibrary\steamapps\common\Sonic Generations\disk\bb3\Sonic\chr_Sonic_HD.model' `
  'D:\Exports\Sonic-new' `
  --game generations `
  --skeleton 'D:\SteamLibrary\steamapps\common\Sonic Generations\disk\bb3\Sonic\chr_Sonic_HD.skl.hkx' `
  --animation 'D:\SteamLibrary\steamapps\common\Sonic Generations\disk\bb3\Sonic\sn_idle_loop.anm.hkx' `
  --acorn 'D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe'
```

For an additional binary-only relocation check in a fresh temporary project:

```powershell
./scripts/Verify-Export.ps1 -ExportDirectory 'D:\Exports\Sonic-new'
```

Use `--game unleashed` for the older Havok profile and Unleashed material calculations. Model and material layouts remain version-aware within the shared conversion pipeline.

Add `--generate-mouth-l` to an Unleashed CLI conversion to create the same optional mirrored mouth. This flag takes no value and is rejected for Generations.

Failed/cancelled writes keep an isolated `.he1scu-*` staging folder for diagnosis. Source assets and existing exports are preserved. The application stores last-used folder/editor paths in `%LOCALAPPDATA%\HE1ToSCU\settings.json`.

## Solution layout

```text
src/HE1ToSCU.App/       Avalonia Fluent window, file/folder pickers, async conversion, log/progress
src/HE1ToSCU.Core/      HE1 reader, HKX fixups/decoding, math, Godot writer, Acorn compiler
src/HE1ToSCU.Cli/       Command-line access to the same conversion service
tests/HE1ToSCU.Tests/   Deterministic synthetic format/math regression tests
tests/HE1ToSCU.GuiTests/ Headless Avalonia workflow and appearance tests
scripts/               Publishing and binary-only relocation verification
docs/                  Reference evidence, pinned source revisions and validation results
```

See [format evidence](docs/FORMAT-EVIDENCE.md), [validation results](docs/VALIDATION.md), and [third-party notices](THIRD-PARTY-NOTICES.md). This project is licensed under **GPL-3.0-or-later**, including its attributed spline decoder adaptation.
