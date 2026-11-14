# Render-state research — November 14, 2026

## Current instruction and result

User requires no plugin, no material script, no engine modification, and proposes keeping the actual renderer opaque/normal while recreating render modes in fragment code through uniforms.

That combination cannot reproduce general transparent/additive/multiply/subtractive compositing correctly in stock Godot 3.1.2 GLES3. Fragment code supplies source values; the renderer selects blending, culling, depth policy, and render-list membership separately. These findings do not authorize approximate screen compositing in final shaders.

No active shader, original foundation, original game source, or existing material resource was changed during this investigation. All 152 active shader hashes and the foundation hash match the starting snapshot.

## Primary references inspected

- Exact stock renderer: https://github.com/godotengine/godot/blob/3.1.2-stable/drivers/gles3/rasterizer_scene_gles3.cpp
  - `_render_list`, approximately lines 1978–2015: native GL blend equations and factors.
  - `_add_geometry_with_material`, approximately lines 2145–2173: alpha-pass classification and depth/shadow eligibility.
  - `uses_screen_texture` and `uses_depth_texture` force alpha classification independently of the runtime ALPHA value.
- Exact shader metadata: https://github.com/godotengine/godot/blob/3.1.2-stable/drivers/gles3/rasterizer_storage_gles3.cpp
  - Approximately lines 1937–1976: render modes become compiled shader flags; ALPHA and ALPHA_SCISSOR uses are compiler metadata, not runtime uniform decisions.
- Exact parser: https://github.com/godotengine/godot/blob/3.1.2-stable/servers/visual/shader_language.cpp
  - Approximately lines 3831–3852: render_mode accepts named modes, not uniform-controlled expressions.
- Shader interface: https://docs.godotengine.org/en/3.1/tutorials/shading/shading_reference/spatial_shader.html
- Screen-read limitation: https://docs.godotengine.org/en/3.1/tutorials/shading/screen-reading_shaders.html
  - The 3D screen copy is made after opaque rendering. Overlapping transparent screen readers do not receive each other's output.
- Khronos blend factors: https://raw.githubusercontent.com/KhronosGroup/OpenGL-Refpages/main/es3.0/glBlendFunc.xml
- Khronos blend equations: https://raw.githubusercontent.com/KhronosGroup/OpenGL-Refpages/main/es3.0/glBlendEquation.xml
- Khronos face culling: https://raw.githubusercontent.com/KhronosGroup/OpenGL-Refpages/main/es3.0/glCullFace.xml

## Runtime evidence

Exact executable:

`C:/Users/jigai/Documents/Codex/2026-11-01/act-as-a-senior-graphics-programmer/work/godot-3.1.2/Godot_v3.1.2-stable_win64.exe`

Official 3.1.2 GLES3, AMD Radeon RX 9060 XT. No engine modifications.

Reproducible isolated research:

```powershell
python tools/research_render_states.py validation/<fresh-directory>
python tools/run_material_profiles.py validation/<fresh-directory> --log gpu
python tools/research_render_states.py validation/<fresh-directory> --verify-protected
```

Passing evidence: `validation/render-state-shader-only-v3-2026-11-14`.

**23 cases, zero failures**, covering the four native blend modes at alpha 0/0.5/1, opaque depth behavior, uniform alpha branches, uniform scissor branches, overlapping native/screen-copy multiplication, and front-face culling/discard alternatives.

- Native opaque red blocks a later-drawn green transparent quad behind it: readback red.
- An alpha-writing shader whose uniform chooses ALPHA=1 does not become native opaque: the rear green quad can draw over it, readback green.
- A shader using ALPHA_SCISSOR remains in the opaque classification for normal blending even when its uniform selects scissor=0. Selecting fractional ALPHA does not turn it into proper transparent compositing.
- Two native multiply layers of 0.5 over a 0.25 background produce 0.0625.
- Two screen-copy multiply layers with the same inputs produce 0.125: each reads the opaque snapshot, losing one layer's multiplication.
- A double-sided uniform cannot restore back faces already removed by native cull_back.
- Fixed cull_disabled plus FRONT_FACING/discard can control visible back-face coverage. It does not dynamically change hardware culling and should not be represented as identical performance/complete shadow-state control.
- Native blend_mul RGB ignores source alpha in this engine; the alpha 0/0.5/1 cases produce identical multiplied RGB. Do not assume the mix/add alpha rule applies to multiply.

Earlier harness evidence was retained:

- v1 had 17 oracle failures because Environment Color background values were converted from display to linear. Replaced that input with an opaque constant shader supplying exact linear values.
- v2 had one additive blue component above 1 clipped by the tested display path. v3 uses bounded exact binary inputs below 1 to isolate blend equations without testing unrelated HDR transfer.
- No shader behavior was modified to satisfy these oracle errors, and no tolerance was loosened.

## What can be shader-controlled

- Numeric alpha cutoff and punch discard, including the requested default 0.502.
- Opaque-versus-punch coverage in an otherwise opaque normal-blend shader.
- Visible back-face suppression with fixed native cull_disabled and conditional FRONT_FACING discard, subject to the distinction above.
- Source shading, RGB, alpha, UVs, normal/specular/reflection calculations.

What cannot be switched correctly by a uniform alone:

- Native opaque versus transparent queue/depth policy.
- Native framebuffer mix/add/multiply/subtract equations.
- Actual hardware cull mode.

For source RGB S, current destination D, and alpha a, the tested RGB equations are mix `a*S+(1-a)*D`, add `D+a*S`, subtract `D-a*S`, multiply `D*S`. With the actual draw kept opaque, the destination is overwritten. The fragment shader has no API for the current framebuffer destination needed to reconstruct these equations for arbitrary overlapping geometry. SCREEN_TEXTURE is a snapshot, and using it also changes the stock draw classification.

## Alternative investigated, then explicitly rejected by user

Before the latest no-script clarification, an isolated self-contained ShaderMaterial .tres with embedded tool script was prototyped. It exposes the ordinary Shader picker and native-state controls, without plugin or external script attachment. It passed 480 controlled front/back/state cases and 20 resource API checks. Initial prototype errors from the Godot 3.1 String.substr signature were fixed in a separate evidence revision. This is not a complete collection-wide/publication validation; separate-process save/reload and full-source regression remain unperformed.

The user subsequently explicitly rejected scripts, including embedded material scripts. **Do not package, publish, or roll this prototype into the active library.** Its tools and isolated validation directories are research evidence only:

- `tools/render_state_material.gd`
- `tools/stage_render_state_material.py`
- `tools/verify_render_state_resource.gd`
- `tools/reload_render_state_resource.gd` (prepared, not executed)
- `validation/render-state-embedded-material-v3-2026-11-14`

## Continuation boundary

Current deliverable remains the unchanged 152-shader Uniform-Ordered-Shaders-2026-11-14 library. No new render-state solution has been published. No final comparison renders were needed or performed because no active shader/material or preview was changed.

For a strictly plain-shader/normal-material workflow, correct native blend/pass settings must be fixed in each selected shader's render_mode/alpha code. Uniforms can control the compatible per-fragment operations above, but an all-modes uniform selector with actual state held opaque/normal is not supported. Do not silently ship an incorrect approximation or reintroduce rejected scripts.
