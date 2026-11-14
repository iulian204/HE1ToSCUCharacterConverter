# Setup and use

## Choose the material program

Use [the inventory](../Inventory/SHADERS.md) to match the original material/shader-list identity. Each linked page records source vertex/pixel pairings and variant-specific operations. Choose the game's directory even when another game has an identically named shader. NoLight/NoGI/instancing/PAM permutations are deliberately outside this base-only delivery. Native draw-state presets are rendering choices for the same program, not extra source permutations.

For default opaque rendering, assign `Generations/<name>.shader` or `Unleashed/<name>.shader` to a native ShaderMaterial. For Punch, Trans, additive/multiply/subtract or double-sided rendering, assign the same name from the matching `Render States` folder. See [render states](RENDER_STATES.md). Parameter names shared by the selected presets are retained by a native material; a parameter removed from a specialized interface is not an editable control in that state. Recheck bindings after changing shader families. Never overwrite your only saved material as a migration shortcut.

The optional `.tres` files are convenience starting points with relative shader references. Duplicate one before editing it so its original remains available. If moving an individual `.tres` rather than the whole library, let Godot update its dependency or reassign its shader. The library has no project file and can be placed inside an existing project at any depth.

## Bind the original material values

The per-shader table is the exact current interface. Source parameters such as `diffuse_color`, `specular_color`, `power_gloss_level` and `eye_highlight_color` are **raw numbers**. Godot 3.1.2 represents a numeric shader `vec4` using a Plane-style Inspector value: its XYZ/normal fields and D field are the four numeric components. They are shader data, not a geometric plane. Do not normalize them or apply color conversion. Original diffuse `(0.6,0.6,0.6)` must stay 0.6; replacing it with 1 hides a binding/lighting problem.

Foundation-authored `sky_color`, `ground_color` and applicable `ambient_light_color` retain their declared color hints. Copy source values into original numeric parameters; use the color controls for the foundation environment. A default declaration is not a substitute for the original `.material` values. “No explicit initializer” in the inventory means no authored default was declared; it is not a recommendation to leave a material or required renderer input unset.

`power_gloss_level` deliberately keeps its name. Original power-map sampler names are now `gloss_texture` / `gloss_uv_set`, including detail equivalents. `noise_power` and `emission_power` are unrelated source parameters and were not renamed.

## Texture binding and import

Bind only the samplers present in the selected shader. Diffuse, gloss/power, specular, packed normal, emission and masks have source-specific channel uses. For example, specular RGB and specular alpha are not universally interchangeable; eye packed normal and power channels have independent uses. Exact code uses are included in every inventory page.

Keep numeric normal, gloss and mask textures as data rather than applying an extra color conversion. The shaders deliberately control their established texture/material transfer; do not infer that adding an albedo/color hint, forcing a normal-map import conversion or changing material numbers preserves it. The reference project used original DDS data. When importing another format, ensure its Godot import settings preserve the same sampled channel values. Automatic normal recompression can destroy nonstandard eye channel packing.

Source BINORMAL negation has already been integrated once where the linked source requires it. **Do not additionally invert the normal-map green channel by default.** That would apply the DirectX/OpenGL orientation correction twice. Imported tangent basis and source channel packing still need to be correct.

See [environment maps](ENVIRONMENT_MAPS.md) before assigning reflections: a sphere map, dual-sphere map, cubemap cross and panorama are distinct representations.

## UVs

Each applicable diffuse/gloss/normal/specular/detail/mask input has an independent `<map>_uv_set`. Default **0** uses mesh UV0 (`UV` in Godot); **1** uses mesh UV1 (`UV2` in Godot). Both channels must actually exist if selected. `uv0_offset` and `uv1_offset` offset those coordinates before sampling. GI has separate `gi_uv_scale` and `gi_uv_offset` where present; animated/source-defined UV transformations remain in their shader paths.

No `uv_data_texture`, `uv_data_texture_width`, packed UV table, helper node, or UV2/UV3 selector is required. There is no universal scale uniform beyond the declarations listed for that variant; do not mistake a GI atlas scale for a scale of all material maps. Texture/selector order follows the original shader suffix; Godot 3.1.2's Inspector groups numeric controls before samplers, retaining relative order within those groups.

## Lighting and shadows

Foundation-lit shaders use their custom `light()` with actual Godot lights. Keep the foundation sky/ground/ambient controls and panorama supplied consistently with the intended scene. The five-sample panorama light field and source-specific rim are distinct from real scene-light diffuse/specular and from `fake_specular`. An unconfigured material in a different scene is not an equal-input comparison to the controlled preview.

The foundation's region shadow uses `region_transform`, `shadow_texture`, `inner_min`, `inner_max` and inner/outer shadow amounts. It is not an HE1 matrix-palette/shadow-register API. A degenerate region axis bypasses the region shadow. Bind a meaningful region transform and mask only when using that system; native light shadows remain separate. A native shadow-enabled light and shadow-casting geometry must exist for native shadows.

Common shaders expose `use_gi_atlas = false`: dynamic use gets the retained panorama light field. Enable it only with a real original baked-GI atlas, original surface UV mapping and appropriate decode scale/saturation. Default-white texture hints are safety defaults, not an original baked-light solution. Projected-reflection variants need a matching scene reflection texture and camera integration; a shader alone cannot generate that camera.

## Troubleshooting

- **Too dark with original diffuse 0.6:** check numeric vector binding first, then texture import, panorama/ambient/scene lighting, camera, exposure and tone mapping. Do not change the authored diffuse to 1 or add a brightness multiplier.
- **Reflection absent:** verify shader projection, environment binding, source masks and alpha, numeric specular/Fresnel parameters, normals/tangents and camera space. Compare combined against no-reflection; an isolated reflection image omits restored fragment interactions.
- **Opaque material disappears with alpha 0:** ensure the Opaque file is assigned. Trans deliberately respects source material/texture alpha. Set original alpha values intentionally for Punch/Trans.
- **Specular too bright:** keep native scene specular separate from fake camera specular and authored eye highlights. The renderer transfer is already compensated once. Do not add another gain100, PI division, material tint conversion or fake-light workaround.
- **Unexpected draw ordering or missing shadows:** check the native alpha-pass restrictions of the selected blend/state, including non-Normal blends and the depth-fade exception.
- **A required input is not present:** do not copy uniforms from another variant. Check that variant's inventory and source behavior. Optional engine-only effects were deliberately removed from this material-editor interface.
