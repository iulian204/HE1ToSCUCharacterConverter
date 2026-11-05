# Format evidence and implementation decisions

All seven requested repositories were cloned and inspected. Exact commits are recorded in [reference-lock.json](reference-lock.json). Reference checkouts are development-only and are not required at runtime.

| Reference | Files inspected / use |
|---|---|
| [Radfordhound/HedgeLib](https://github.com/Radfordhound/HedgeLib) | `HedgeLib/include/hedgelib/models/hl_hh_model.h`, `src/models/hl_hh_model.cpp`, `include/hedgelib/io/hl_hh_mirage.h`: standard Mirage header, model versions, node matrices, vertex declarations, palette widths, special slots, strip decoding and inverse-bind interpretation. Primary HE1 format reference. |
| [blueskythlikesclouds/BlueBlur](https://github.com/blueskythlikesclouds/BlueBlur) | `Hedgehog/MirageCore/RenderData/hhModelData.h`, `hhModelNodeData.h`, `Hedgehog/Animation/hhAnimationSkeleton.h`, `Hedgehog/Database/System/hhArchiveDatabaseLoader.h`: runtime node/mesh/animation relationships. Runtime C++ structs are not mistaken for serialized file layouts; archive loading is an engine service and remains out of scope. |
| [blueskythlikesclouds/SkythTools](https://github.com/blueskythlikesclouds/SkythTools) | `Sonic Forces/HKX Converter/sonicForcesHkxConverter.py`: tagfile type/item separation and skeleton/animation templates. This is a Forces tagfile utility, not a universal Generations decoder; that parser is not reused as a packfile parser. Much of this repository contains tools/binaries rather than reusable C# source. |
| [blueskythlikesclouds/HavokAnimationExporter](https://github.com/blueskythlikesclouds/HavokAnimationExporter) | `Source/HavokAnimationExporter/Main.cpp`, `HKXConverter.cpp`, README: parent indexes, local reference poses, quaternion QS transforms, transform-to-bone bindings, interleaved frame ordering and section/fixup structures. The tool primarily creates HKX from FBX and relies on separately supplied Havok SDKs; it is not a bundled generic decompression library. |
| [blueskythlikesclouds/ModelConverter](https://github.com/blueskythlikesclouds/ModelConverter) | `Model.cpp`, `Node.cpp`, `ModelProcessor.cpp`: node parent/name serialization, legacy slot layout versus v5 groups, palettes and vertex processing. It primarily exports HE models from imported geometry; file layout and processing concepts were used, not an assumed inbound C# API. |
| [HedgeDocs/HedgeDocs.github.io](https://github.com/HedgeDocs/HedgeDocs.github.io) | `hedgedocs/tools/hedgehog-engine/common/models.md`: tool provenance and conversion direction. Documentation supplements the source layout evidence rather than replacing it. |
| [DarioSamo/libgens-sonicglvl](https://github.com/DarioSamo/libgens-sonicglvl) | `src/LibGens/Model.cpp`, `Bone.cpp`, `HavokAnimationCache.cpp`: version-dependent legacy slots/morph header offsets, skeleton matrix layout and the separation between an animation and its binding. In particular, legacy v4 uses three slots before the morph header. |

## Godot / Acorn resources

Upstream baseline: Godot `3.1.2-stable`, commit `0587df4aa5f2977350cc80b1522cdc1e483c4515`, inspected through the local Git object database under `D:\Acorn Engine\godot`.

- `scene/resources/mesh.cpp`, `ArrayMesh::_set`: the compatibility `arrays`/`morph_arrays` surface dictionary is accepted. It avoids guessing Acorn GPU vertex byte packing; the selected engine emits its own packed binary mesh.
- `scene/resources/animation.cpp`, `Animation::_set`: each transform key contains 12 scalars: time, transition, translation XYZ, quaternion XYZW, scale XYZ.
- `scene/3d/skeleton.cpp`: the local bone transform is `rest * pose` in Godot column-vector notation. The managed row-vector equivalent is `pose * rest`; therefore export pose is `animatedLocal * inverse(rest)`.
- `core/variant_parser.cpp`: textual `Transform` accepts nine **row-major basis entries**, unlike GDScript's vector-axis constructor. Managed matrices are transposed when serialized. This distinction has a regression test and numerical engine verification.
- `core/variant_parser.cpp`: numeric exponents use lowercase `e`; export uses invariant-culture numbers and normalizes the exponent character.
- Binary serialization is delegated to the selected Acorn executable. The app does not label stock Godot binary headers as native Acorn output.

Verified editor: `D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe`, SHA256 `194753AE98AB27E585307C2579FEC52134B26482B5018AE22300481DD8F66090`. It identifies itself as `Acorn Engine v1.0.0.stable.custom_build.609b955`.

The reference `D:\SCUExtract\characters\player_sonic\chr_Sonic_SD.tscn` contains a Skeleton, MeshInstance, `cast_character_shadow = true`, and ordinary shadow disabled. These shadow defaults are written; arbitrary SCU shader parameters are not invented. Its first bone is `SonicRoot`, whereas HE1 uses `chr_Sonic_HD`: this is evidence that source scene conversion and gameplay rig replacement need separate treatment.

## Spatial math

HE1 matrices in `.model` are global inverse binds, stored using the opposite matrix convention from System.Numerics. Read and transpose, invert to global rest, then compute `localRest = globalRest * inverse(parentGlobalRest)` with row vectors.

The source Generations Sonic Hips global position is about Y `0.501910`; the SCU Sonic reference has Y `5.01910`. Both use Y up. The character profile scales vertices and translations by 10 while preserving orientation. Normals and bone scales are not multiplied by 10. Strip indexes are converted to clockwise Godot front-face triangles without a mirror transform. This is observed Sonic character evidence, not a claim that all HE1 assets share one asset-authoring scale.

Havok reference poses are read and validated, and bone indexes are matched by exact name and compatible parent hierarchy. Skin inverse binds from the model remain authoritative for Godot rest matrices; animated Havok local transforms are converted into poses relative to those rests. Replacing model binds with unrelated Havok reference matrices would shift skinning.

## Spline decoding

The required references establish Havok semantics but do not supply a standalone managed spline decoder. An additional attributed GPL C# decoder from [Meowmaritus/SoulsAssetPipeline](https://github.com/Meowmaritus/SoulsAssetPipeline/blob/master/SoulsAssetPipeline/Animation/HKX/SplineCompressedAnimation.cs), itself adapted from PredatorCZ/HavokLib, is vendored with modifications and its license preserved.

Changes include a narrow managed binary reader, explicit block offsets, removal of unrelated importing APIs/debug output, a Polar32 integer-conversion correction, knot-domain validation, explicit unsupported-format errors, and normalized quaternions. Frame block overlap uses `maxFramesPerBlock - 1`. No proprietary Havok SDK or executable is redistributed.

## Validation boundary

Current validation covers the supplied Generations and Unleashed asset sets, native Acorn resource loading/saving, binary-only relocation and source-derived key/subframe bone transform agreement after playback. See the version 0.4 section below and VALIDATION.md for counts and limits. It does not prove the decoder is bit-for-bit identical to the proprietary Havok sampler, visually validate all animations, retarget the rig, or establish in-game rendering behavior. Material conversion is an approximation, detailed in [SPATIAL-MATERIAL-MAPPING.md](SPATIAL-MATERIAL-MAPPING.md).

## Version 0.2: extracted motion and original names

The reported batch failed at `sn_dead.anm.hkx` because the old reader explicitly rejected a non-null `extractedMotion` pointer. The supplied file contains a 32-bit Havok 2010 `hkaDefaultAnimatedReferenceFrame`: up at +0x10, forward at +0x20, duration at +0x30 and an hkArray of Vector4 samples at +0x34. Its 91 samples contain translation XYZ and heading in W. Layout and clamped sampling were cross-checked against the existing SoulsAssetPipeline reference (`Animation/HKX/Animation.cs` and `RootMotionData.cs`) and the real source bytes.

The managed reader retains the reference-frame samples; a separate transform track targets the model Spatial. Quaternion rotation uses the authored up axis and heading, while translations receive the same positional scale as the mesh and bones. Root heading keys are subdivided when needed to retain complete turns. In-place animations include an identity root track to clear a previously played motion clip. Engine checks now compare the model root as well as bone transforms.

`ExportNames` is the shared contract for editable resources, native compiler input/output, and binary verification. It preserves model and animation stems and checks filename collisions using Windows case-insensitive comparison. The export manifest records the resulting files and per-clip motion sample counts.

## Version 0.3: winding, nonuniform scaling and materials

The source Sonic strips already have clockwise front faces. The old exporter unnecessarily reversed them, making the mesh appear inside-out. Strip expansion now preserves source parity and authored normals. A geometric winding test covers the correction; disabling culling is not used as a workaround.

The boost distortion comes from nonuniform parent scales combined with rotated children. Havok composes quaternion, translation and scale components independently; an ordinary matrix hierarchy introduces shear. For example, `Calf_R` has X scale about 0.081 at boost frame 7 while `Foot_R` compensates with X scale about 10.916. Direct Godot hierarchy composition stretches the foot despite near-unit source model-space scale. Compensation helper bones remove parent scale before each child's rotation and model-space scale. Original bones retain their names/indexes and bind transforms. All 81 boost sampled poses, including the final key with looping temporarily disabled, pass Acorn position/basis checks. These are sampled-frame checks, not a proof of identical Havok interpolation between keys.

HE1 material layouts were checked against HedgeLib's Mirage material/texture structures. The supplied `ChrSkin.hlsl` establishes packed normal decoding, specular texture RGB, the Y/Z components of `power_gloss_level`, and the falloff inputs. Additional local GenerationsRaytracing shader references distinguish ChrEye's packed surface-normal B/A channels and green gloss channel. Neither reference shader is bundled. Standard SpatialMaterial resources and embedded ImageTextures are emitted by managed C# and compiled by the selected Acorn editor.

## Version 0.4: HE1 animation semantics and Unleashed profiles

The v0.3 scale-compensation approach was incomplete: it still multiplied child translations by parent scale and inverted animated scales. The corrected HE1 playback profile composes rigid translation/rotation separately and accumulates scale componentwise. Only nonleaf source bones need rigid control bones. Original skin-bone indexes/names and global binds are preserved. Signed/zero scales are carried explicitly rather than recovered by decomposing singular animated matrices.

Sparse transform maps are expanded against the matching Havok reference pose, avoiding residual transforms when switching clips. Additive binding modes 1/2 are resolved in their respective composition order against that reference for standalone playback. The manifest records this bake; it does not imply runtime additive-overlay behavior.

Spline evaluation now uses continuous knot-domain sampling and serialized block/frame timing. Adaptive baking checks quarter/mid/three-quarter interval poses and adds keys until the source-pose bound is met, or stops explicitly. Adjacent representable times preserve quantization discontinuities at block boundaries. Quaternion tracks stay normalized and sign-continuous. No individual animation names select conversion fixes.

HavokLib's versioned skeleton, binding and spline layouts were inspected at the revision in `reference-lock.json`. Havok 5.5 uses a different section-header version and pointer-based bone entries, legacy class names, and older binding/spline offsets. The supplied Unleashed containers are big-endian, while their compressed spline streams are little-endian. Synthetic fixtures cover both container profiles and their fixups. The game selection controls the Havok profile and material calculations; Mirage models/materials dispatch by the actual shared file version. The supplied Unleashed `SonicRoot.model` is v5; its folder also contains real v4 effect models and v1 materials. Bone mapping tries exact names first, then an unambiguous `@LT` suffix alias, with hierarchy checks retained.

Native validation compares every original bone plus rigid controls against a separately composed rigid-matrix source oracle at source/baked keys, intermediate times and inferred loop boundaries. This validates export against the managed decoder; it does not establish equality with an independently executed proprietary Havok sampler. Loop inference uses `loop` in filenames because these inputs do not provide a conversion-level loop flag.

Large banks keep relative companion `.anim` resources when track count would risk Godot 3.1's pooled-array allocation limit. Mesh/material/texture resources remain embedded. Validation data is streamed through a temporary binary file and removed after success, rather than generating an enormous GDScript literal. Both native compile and fresh-process verification yield periodically during long checks.

Material mapping now uses independent power-to-roughness conversion, game-specific brightness fitting and built-in direct clearcoat for reflective finishes. See [SPATIAL-MATERIAL-MAPPING.md](SPATIAL-MATERIAL-MAPPING.md) for equations, source-shader routing and practical limits. All resources remain ordinary SpatialMaterial instances; camera-light source lobes are excluded.

## Automatic mouth mesh partitioning

The converter groups mouth/jaw/lip/teeth/tongue bones into independent branches rooted at their highest mouth-related ancestor. Descendants stay in the same group even when their own names are not anatomical. Each triangle goes to the mouth branch owning more than half its aggregate skin weight, or remains with the body. Tiny mouth blend weights cannot pull unrelated head triangles into a mouth mesh. Zero-weight bone slots and unweighted locator branches are ignored. Matching handles common case/separator/camel-case conventions without guessing from material names or vertex positions.

Partitioning assigns every face exactly once and compacts only affected vertex buffers. It retains original vertex data, winding, materials/slots, bone indexes and weights. The split creates no new bones and does not alter animations or rest poses. `Skeleton/Mesh` remains the body node. `Skeleton/Mouth` and additional independent mouth nodes such as `Skeleton/Mouth_L` use `skeleton = NodePath("..")`, so they share the same skeleton and animation player. Mesh filenames derive from the original model stem plus the mouth-node name; the manifest records each resource and source root bone.

The actual Generations Sonic uses independent `MouthReference` and `MouthReference_L` branches. Each produces 705 triangles, including teeth found in a body-material surface. Unleashed Sonic has one weighted `MouthReference` branch, producing 1,482 triangles; its unweighted `Sonic_Mouth` locator does not create another mesh. The result is three MeshInstances for Generations and two for Unleashed, in one scene per character. Models without weighted mouth geometry retain their existing single mesh.

## Optional Unleashed mirrored mouth

`GenerateMouthL` is an opt-in conversion request flag, exposed as `Generate Mouth_L` only when Unleashed is selected in the desktop app, and as the value-free CLI flag `--generate-mouth-l`. The UI remembers the preference while gating it off for Generations; the core rejects a Generations request with this flag. The default remains disabled. Existing authored left mouths are retained rather than duplicated, empty/unweighted mouth locators are ignored, and conflicting generated bone names produce an explicit error.

The new mesh reflects positions and normals across model X, reverses triangle winding, reflects tangent XYZ and negates tangent handedness. UVs, colors, material assignments and weight magnitudes remain unchanged. Every bone in the weighted mouth branch is duplicated with `_L` appended; vertex indexes are remapped to those new bones. Original bone indexes and geometry are untouched. No negative MeshInstance scale is used.

Let `S` be the model-X reflection and `P` the shared parent's global bind. Define the parent-frame reflection `H = P * S * inverse(P)` using row-vector notation. A mirrored mouth-root local transform is `S * L * H`; descendants use `S * L * S`. The shared parent keeps its original animation. This accounts for the nontrivial Head axes in the real asset and reflects mouth motion relative to the moving head, rather than reversing head turns. Rotation/translation are mirrored before authored signed/zero scale is applied, preserving scale-zero crossings and quaternion orientation. Extracted character root motion stays unchanged.

Mirrored tracks are evaluated continuously before the existing adaptive bake and rigid scale-control stage. They are exported as independent tracks on the same Skeleton and AnimationPlayer. The generated `Mouth_L` starts hidden and the original `Mouth` starts visible, per the requested default; switch their visibility to use the alternate without coplanar overlap. Existing Generations mouth visibility is unchanged.
