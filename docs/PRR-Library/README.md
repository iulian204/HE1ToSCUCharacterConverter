# Sonic Generations / Sonic Unleashed shaders

Godot **3.1.2 GLES3**. This is the finished, script-free shader/material library, including the Generations eye-reflection repair. It contains **152 base programs** (99 Generations, 53 Unleashed), **four mapped exact aliases**, and **24 native draw-state presets per program**: 3,648 shaders and 3,648 optional ordinary ShaderMaterial resources.

## Start here

1. Copy this **entire Release folder** into your Godot project's `res://` folder. It can be nested or renamed. Do not copy a `project.godot` into your project; this library does not contain one.
2. Create your own **ShaderMaterial** and assign the desired `.shader`, or duplicate an optional matching `.tres` under `Materials`.
3. Bind the original material textures and numeric parameters. The templates are empty native materials, **not preconfigured Sonic materials**. Required textures and original material values are not bundled game assets.
4. Choose the correct game's shader. The top-level `Generations` and `Unleashed` folders are **Opaque / Normal / SingleSided**. Other states are under `Render States/<coverage>/<blend>/<faces>/<game>/`.

There is no plugin, custom material class, runtime script or engine patch. Rendering is in the shaders. Native blending, culling and queue/depth behavior are selected by the shader file, not by an ineffective uniform.

## Documentation and inventory

- [Setup, binding, UVs, lighting and troubleshooting](Documentation/USAGE.md)
- [Render states, alpha, blending and double-sided rendering](Documentation/RENDER_STATES.md)
- [Every shader and its individual parameters](Inventory/SHADERS.md)
- [Shared parameter reference](Documentation/PARAMETERS.md)
- [Environment maps and cubemap layout](Documentation/ENVIRONMENT_MAPS.md)
- [Rendering architecture and original mathematics](Documentation/RENDERING.md)
- [Validation results and their tested boundaries](Documentation/VALIDATION.md)
- [Known limitations and required scene inputs](Documentation/LIMITATIONS.md)
- [File map, history and continuation notes](Documentation/PROJECT_MAP.md)
- [Machine inventory](Inventory/shaders.json), [all preset paths as CSV](Inventory/presets.csv), [aliases](Inventory/aliases.json)

The original foundation remains unchanged. The delivery is verified for the recorded source-operation, foundation, interface and stock-engine checks. **Full original-game visual parity for all programs is not established.** Read the limitations rather than treating “implemented” or “compiles” as a visual-equivalence claim. `release-manifest.json` and `SHA256SUMS.txt` identify the exact packaged bytes.
