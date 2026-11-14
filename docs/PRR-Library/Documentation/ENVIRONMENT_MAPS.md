# Environment textures and reflection

All delivered environment inputs use **sampler2D**, including cubemap variants. Bind the representation used by that program, as identified from its emitted sampling path in [the inventory](../Inventory/SHADERS.md). Material naming is a useful source convention, not permission to substitute unrelated source variants.

## Source conventions

Generations `e1` is a single sphere map, `e2` a cubemap, and an unnumbered `e` commonly a double-sphere/dual-paraboloid map. Unleashed environment variants with `@c@` use cubemaps; other variants use their source-defined 2D sphere/dual-sphere path. Eyes can use a view-normal sphere projection while other materials use reflected directions: preserve each shader's actual operation. The map is not always applied using the same UV equation across families.

The double-sphere representation places its two hemispheres side by side. The original hemisphere selection, sign/orientation and source channel decoding are retained. The inventory's exact sample expressions distinguish this from a conventional lat-long panorama.

## Single-image cubemap cross

The verified layout is four columns by three rows, with equal square faces:

```text
       +Y
   -X  +Z  +X  -Z
       -Y
```

In zero-based grid coordinates: +X=(2,1), -X=(0,1), +Y=(1,0), -Y=(1,2), +Z=(1,1), -Z=(3,1). A 512×384 image has 128×128 faces. No six separate face files or samplerCube is required.

The reusable `sample_environment_cross` helper selects the dominant direction axis, preserves the verified face orientation/UV origin, computes face-local UVs and maps them into this cross. Equal dominant magnitudes choose Z, then Y, then X. Per-face half-texel limits bound filtering inside a face at each sampled mip. Reflection-vector construction remains source-specific; the helper only adapts the texture representation.

The map input is **environment_texture**, not **sky_panorama**. The latter remains the foundation's panorama light-field/rim resource. Do not assign the cross to the panorama slot.

## Mips and filtering

For a single extracted cross image, leave `environment_max_lod = 0`. This limits the shader to the base-level atlas and avoids sampling a normally generated mixed-face mip chain. Import must preserve the full 4×3 image without cropping, rotating faces or premultiplying the transparent unused cells into the used ones.

If using the original DDS cubemap mip chain, it must be packed **face by face at each original level**, preserving the cross layout at every level. Set `environment_max_lod` to the last original level, not to the last mixed atlas tail level. A generic image mip generator can mix unrelated atlas cells. The project's existing offline packing/reference tools are documented in [PROJECT_MAP](PROJECT_MAP.md); they are not runtime shader dependencies and are not installed by this release.

The shader's analytic footprint, per-face clamp and interpolation have source/native fixture evidence. They still have a documented boundary versus native implicit cube LOD and seamless edge filtering at high gradients. This does not establish that every visually dim reflection is a mip bug. Application masks, Fresnel, color-space inputs, addition order and lighting must be checked independently.

## Reflection application and eye fix

Source decode can include `RGB += RGB * A * luminance_range`. Reflection masking can use gloss/specular alpha, vertex alpha, material specular/Fresnel and shadow weights. They are not interchangeable. A low eye vertex-alpha mask is authored data; it is not forced to 1 for visibility.

The Generations eye repair retained source projection, sampler binding, mask, Fresnel, gloss and individual foundation squares. Its original source adds ambient A, reflection R and authored highlight H before output. The adaptation had lost the pairwise interactions by accumulating A²+R²+H² independently. Restoring **2AR+2AH+2RH** corrects that integration without a reflection boost, changed material default or final-output-square shortcut. Full combined-minus-no-reflection captures are the appropriate visibility check; isolated reflection excludes those interactions.

The actual Sonic e1 eye material changed from zero displayed eye pixels affected by reflection to 13,713 (out of 24,727 eye pixels), maximum12 RGB levels of reflection contribution. Standalone fake, native specular, reflection and authored-highlight captures stayed pixel-identical. The other two eye projections have source/GPU evidence, not the same actual-asset visual coverage. See the included [eye report](GENERATIONS_EYE_REFLECTION.md).
