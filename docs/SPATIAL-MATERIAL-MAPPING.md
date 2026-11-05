# HE1 to Godot 3.1 SpatialMaterial approximation

All converted materials are standard Godot 3.x `SpatialMaterial` resources. The converter generates no custom shaders and makes no renderer changes. Original material and texture stems are retained.

The Generations reference is the supplied `ChrSkin.hlsl` (SHA256 `A0059944E938803D2C91795C23736548A3B73B181ACBE16AB4C84A53A2AA7367`). The supplied Unleashed `SonicSkin_dspnf@@`, `SonicEnamel_dspe@@` (shoes), `SonicCloth_dsn@@` and `SonicEye_dpne@@` pixel-shader disassemblies were also inspected. These local reference files are not redistributed. They guide an approximation; their entire lighting graphs are not reproduced.

## Game-specific response

Let `y` and `z` denote `power_gloss_level.y/z`, and let `g` be the gloss texture sample or 1 when absent.

| Selected game | Specular power n | Brightness B |
|---|---|---|
| Generations | `clamp(g * y * 500, 1, 1024)` | `max(0, g * z * 5)` |
| Unleashed | `clamp(g * exp2(y * 10 + 2), 1, 512)` | `n * 0.125` |

These are the agreed single-lobe conversion profiles. Camera/eye-light contributions and source multi-lobe or packed-alpha material-family effects are not reconstructed. In particular, the profiles should not be mistaken for a complete implementation of every supplied shader variant.

Roughness is derived from power, independently of specular RGB intensity:

```
r = (2 / (n + 2)) ^ 0.25
```

This matches the normal-incidence peaks of normalized Blinn and GGX distributions: `D_Blinn(1)=(n+2)/(2*pi)` and `D_GGX(1)=1/(pi*r^4)`, where GGX alpha is `r^2`. It is a distribution approximation, not an exact BRDF equivalence.

With a gloss map, the converter bakes RGBA8 roughness using the same formula per texel, retaining the source power clamp. Explicit `gloss` and `power` slots and `_pow`/`_pow_HD` names are detected. R is used normally. ChrEye and SonicEye surface gloss use G, and Generations ChrMetal and PamChrMetal read A from their gloss textures. Roughness textures use linear sampling, generated mipmaps and material roughness 1. Without a gloss map, roughness is constant. Specular RGB intensity is fitted independently from roughness.

`metallic_specular` is fitted globally to source brightness, material specular luminance and the histogram of specular RGB luminance/gloss samples. The canonical view has `V=N`, samples a hemisphere of light directions, and minimizes integrated squared error against `B * (N.H)^n`. The fit averages the upstream GLES2/GLES3 geometry terms and uses Godot's dielectric `F0=0.16*s^2`. Numerical quadrature resolves narrow highlights. Exported base `s` stays in [0,1]. This global scalar cannot preserve colored, spatially varying specular intensity.

## Highlight and color adaptation

Reflective finishes can need more direct highlight brightness than the bounded base fit supplies. A separate desired fit (allowing scalar up to 2.5) defines the additional direct response. Built-in GTR1 clearcoat approximates that difference, preserving its normal-incidence peak and minimizing remaining angular shape error. This is a practical Godot approximation, not a reconstruction of the source camera-light lobe. Generations reflective finishes apply the final preview's 1.55 brightness factor to the desired fit; Unleashed uses its game-specific brightness directly.

In Godot 3.1.2, this clearcoat term adds direct lighting without increasing the base environment reflection. Clearcoat strength can exceed the Inspector's 0..1 hint: the unmodified stock float setter and binary resource serializer preserve these HDR values. Both Acorn and stock 3.1.2 were checked. Editing the value through a range-limited Inspector control may clamp it. The base roughness, albedo and sky-reflection scalar are unchanged by this highlight adjustment.

Generations `EnmMetal_*` materials with matching primary-UV diffuse and specular sampling bake per-pixel base color and metalness. The source shader uses specular RGB for the direct highlight and specular alpha for a separate matcap term. The bake uses RGB as a color input, excludes specular alpha, and converts the power-matched highlight peak to target reflectance with `F0 = 8*pi*brightness/(power+2)` before applying the texture color and material specular luminance. A hue-preserving clamp keeps reflectance within the representable range. The bake solves the Godot metallic mix against both the source diffuse color and this reflectance. It preserves diffuse alpha and the original roughness calculation. When the textures cannot share a bake, a scalar painted-metal approximation and fitted direct highlight are used.

Generations `EnmGlass_*` materials use the source shader's separate `mrgChrEmissionParam.rgb * ambient.rgb * mrgChrEmissionParam.w` contribution. Its alpha combines diffuse alpha with a view-normal matcap sample. SpatialMaterial uses a front-weighted average of the matcap alpha for that coverage, retaining the source blend mode and mapped normals. Brightness and coverage can therefore approximate the original lens, while its view-dependent glint pattern depends on target lighting.

Where a ChrSkin/SonicSkin material supplies `mrgFresnelParam`, the normal-incidence specular fitting scale uses `clamp(x,0,1)*max(y,0)`. This reduces the Unleashed fur's broad white sheen without recoloring its diffuse texture. The full view-dependent source Fresnel curve is not reproduced.

The supplied Unleashed eye, cloth and enamel shaders tint direct diffuse separately from ambient. SpatialMaterial albedo affects both. For their legacy neutral diffuse multiplier 0.6, an equal direct/ambient reference mix produces `(diffuse+ambient)/2`, typically 0.8. This intentionally moderate compensation is lighting-dependent, not a gamma conversion or an exact original-shader match. Colored authored tints and already-white multipliers are retained.

## Other inputs retained

| Source input | Built-in output |
|---|---|
| Diffuse RGB/texture and vertex color | Albedo color, sRGB texture and linear vertex-color multiplication |
| Alpha, optional opacity texture, scalar opacity | Albedo alpha, transparency/alpha scissor as appropriate |
| ChrSkin/SonicCloth packed normal | X=R*A, Y=G, reconstructed Z; green inverted for the tangent convention |
| ChrEye/SonicEye packed surface normal | B/A channels, with reconstructed Z; custom highlight normals excluded |
| Normal texture assigned to the second source UV set | `detail_normal` on UV2 with neutral multiplicative detail albedo. The original albedo and mesh UVs stay in place |
| Emissive parameters and texture | Emission color/texture, default white multiplier for a texture with zero stored emission |
| Skin falloff/displacement texture | Approximate built-in rim texture and intensity |
| Double-sided, additive material, punch slot | Culling, blending and alpha-scissor settings |

Zero stored `diffuse.w` is treated as default opacity 1 for the supplied opaque materials. Missing textures are logged. DDS BC1/2/3/4/5 and supported uncompressed RGB/RGBA are decoded in managed C#; mipmaps and embedded ImageTextures require no external converter or import step. Colors use sRGB; normal/roughness maps remain linear.

Reflection/matcap sampling, original GI/probes, independent ambient tint, colored per-pixel specular, refraction and custom eye highlights remain outside this approximation. Unsupported UV assignments are logged and fall back to primary UVs. Native loading, numerical fitting and preview renders cover the converter output. In-game SCU acceptance and pixel parity remain unverified.

## Shader library audit

The supplied library contains 3,081 Generations HLSL files and 1,446 Unleashed assembly files. The per-file pass indexed 3,973 pixel programs and 554 vertex programs into 294 game/family groups. The local reports at `artifacts/shader-family-audit/files.csv` and `artifacts/shader-family-audit/families.tsv` include file hashes, used sampler and parameter names, and operation counts. Register-use and data-flow checks on the affected variants guided these conversion rules.

| Source family | SpatialMaterial conversion |
|---|---|
| Generations `EnmEmission`, `EnmIgnore` and `EnmGlass` | Constant emission and optional texture from `(mrgChrEmissionParam.rgb + sample) * ambient.rgb * mrgChrEmissionParam.w`. Variants with the `l` sampler accept a displacement-tagged emission texture. Coverage follows the sampled diffuse/matcap alpha, including when an unused opacity entry aliases the diffuse texture. |
| `Luminescence` variants with an emission sampler | Emit from the authored texture, with ambient tint for the Generations path. The Unleashed assembly adds the sampled RGB independently of its diffuse lighting. |
| `IgnoreLight` in both games and Generations `IgnoreLightTwice` | Use unshaded albedo. For textured effects with zero diffuse RGB, use the stored effect color as the standalone tint and retain the source opacity and blend mode. Twice doubles the RGB. Generations `E` variants also read their separate emission parameter and texture. `IgnoreLightV` includes vertex lighting. |
| Emission texture on source UV index 1 | Use Godot's emission UV2 setting. |
| `Parallax` in both games | Map compatible primary-UV height textures to the built-in red-channel depth slot. The source offset magnitude is 0.03. Godot uses its own tangent-space offset model, and stock GLES2 does not render this depth effect. |

The audit also includes screen passes, vertex programs, runtime lighting, water, fur shells, multi-layer blends and distortion shaders. These can require stage or mesh data beyond a material resource. The converter keeps its supported texture and parameter mappings for those families. In particular, the Unleashed `DayEnemyMetal*` assembly uses paired highlight exponents from diffuse alpha and a separate environment term, so the Generations `EnmMetal` bake is not applied to it.
