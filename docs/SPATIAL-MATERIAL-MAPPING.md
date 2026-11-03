# HE1 → Godot 3.1 SpatialMaterial approximation

The converter reads original Mirage `.material` float parameters and texture entries. It writes built-in `SpatialMaterial`, not a custom shader. Source material/texture stems are retained. Missing files are logged; unsupported texture encodings produce explicit errors.

Primary shader evidence supplied by the user: `D:\Tools\SkythTools-master\Sonic Generations\chrskin\ChrSkin.hlsl`, SHA256 `A0059944E938803D2C91795C23736548A3B73B181ACBE16AB4C84A53A2AA7367`. This file is referenced locally and is not redistributed. Parameter names below correspond to the HE1 material entries used by that shader. Godot property names were checked against the local 3.1.2 source and loaded in Acorn.

| HE1 input | SpatialMaterial approximation |
|---|---|
| `diffuse.rgb`, diffuse texture RGB, vertex color | `albedo_color`, sRGB `albedo_texture`, linear vertex-color multiplication |
| Diffuse texture alpha, optional opacity texture, `opacity_reflection_refraction_spectype.x` | Albedo alpha; transparency enabled for transparent slots or reduced scalar opacity |
| `diffuse.w` | Positive values multiply opacity. The supplied nine materials all store zero; the approximation treats zero as default opacity 1 to keep their opaque surfaces visible. This is a compatibility policy, not a verified explanation of runtime parameter overrides. |
| ChrSkin normal texture | Decode X = R × A, Y = G; reconstruct Z. Invert the green channel for the source tangent/binormal convention; emit a linear RGB normal texture. |
| ChrEye normal texture | Use B/A for the surface normal; custom highlight normals are not reproduced. |
| `power_gloss_level.y` | Blinn-Phong exponent `n = clamp(y × 500, 1, 1024)`, approximated as GGX roughness `(2 / (n + 2)) ^ 0.25` |
| `power_gloss_level.z`, `specular.rgb`, specular texture RGB | Scalar `metallic_specular = clamp(z × 5 × luminance(specular) × mean(texture luminance), 0, 1)`; lower-texture-intensity regions receive increased roughness. Metallic remains zero. |
| Specular texture alpha | Unused for ChrSkin. It is **not** interpreted as gloss. |
| Gloss texture | Multiplies the gloss exponent before roughness conversion; R normally, G for ChrEye. |
| `power_gloss_level.x` | No ChrSkin mapping: the supplied shader uses Y for exponent and Z for strength. |
| `emissive.rgb`, emission/emissive texture | Emission color/texture; textured emission defaults to white when the stored color is zero. |
| `g_SonicSkinFalloffParam`, ChrSkin displacement/falloff texture | Approximate rim light and rim texture; `rim = clamp(parameter.y / 5, 0, 1)`. The original offset/exponent/probe modulation has no direct built-in equivalent. |
| Double-sided flag, additive flag, punch slot and alpha threshold | Culling disabled only when requested; additive blending, transparency or alpha scissor as appropriate |
| Reflection/matcap texture, probe-light terms, ambient material parameter | No direct equivalent. Reflection textures are reported in the log; environment lighting provides the built-in lighting response. |

SpatialMaterial has no colored per-pixel specular slot. Converting its intensity variation into roughness preserves some surface contrast but changes highlight shape. Reflection/matcap effects, original probe lighting, refraction, extra UV routing, and custom eye highlights are not reproduced. The supplied Sonic materials use UV0; other UV indexes are logged when encountered.

Original DDS top-mip data is decoded in C# (BC1/2/3/4/5 or supported uncompressed RGB/RGBA), adapted where needed, and given a generated mip chain. Color maps use sRGB conversion; normal/roughness maps stay linear. Editable textures are ImageTexture `.texture.tres` resources. Acorn binary scenes and meshes embed their materials and textures so they can be moved independently.

The nine supplied Sonic materials were parsed and rendered in Acorn. This validates the input routing and a useful approximation, not pixel equivalence to Generations or in-game SCU acceptance.
