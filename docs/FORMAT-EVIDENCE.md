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

Current validation proves parsing on supplied Generations assets, native Acorn resource loading/saving, binary-only relocation and sampled bone transform agreement after playback. Version 0.3 also renders source-textured boost poses. It does not prove the decoder is bit-for-bit identical to the proprietary Havok sampler, visually validate all animations, retarget the rig, or establish in-game rendering behavior. Material conversion is an approximation, detailed in [SPATIAL-MATERIAL-MAPPING.md](SPATIAL-MATERIAL-MAPPING.md).

## Version 0.2: extracted motion and original names

The reported batch failed at `sn_dead.anm.hkx` because the old reader explicitly rejected a non-null `extractedMotion` pointer. The supplied file contains a 32-bit Havok 2010 `hkaDefaultAnimatedReferenceFrame`: up at +0x10, forward at +0x20, duration at +0x30 and an hkArray of Vector4 samples at +0x34. Its 91 samples contain translation XYZ and heading in W. Layout and clamped sampling were cross-checked against the existing SoulsAssetPipeline reference (`Animation/HKX/Animation.cs` and `RootMotionData.cs`) and the real source bytes.

The managed reader retains the reference-frame samples; a separate transform track targets the model Spatial. Quaternion rotation uses the authored up axis and heading, while translations receive the same positional scale as the mesh and bones. Root heading keys are subdivided when needed to retain complete turns. In-place animations include an identity root track to clear a previously played motion clip. Engine checks now compare the model root as well as bone transforms.

`ExportNames` is the shared contract for editable resources, native compiler input/output, and binary verification. It preserves model and animation stems and checks filename collisions using Windows case-insensitive comparison. The export manifest records the resulting files and per-clip motion sample counts.

## Version 0.3: winding, nonuniform scaling and materials

The source Sonic strips already have clockwise front faces. The old exporter unnecessarily reversed them, making the mesh appear inside-out. Strip expansion now preserves source parity and authored normals. A geometric winding test covers the correction; disabling culling is not used as a workaround.

The boost distortion comes from nonuniform parent scales combined with rotated children. Havok composes quaternion, translation and scale components independently; an ordinary matrix hierarchy introduces shear. For example, `Calf_R` has X scale about 0.081 at boost frame 7 while `Foot_R` compensates with X scale about 10.916. Direct Godot hierarchy composition stretches the foot despite near-unit source model-space scale. Compensation helper bones remove parent scale before each child's rotation and model-space scale. Original bones retain their names/indexes and bind transforms. All 81 boost sampled poses, including the final key with looping temporarily disabled, pass Acorn position/basis checks. These are sampled-frame checks, not a proof of identical Havok interpolation between keys.

HE1 material layouts were checked against HedgeLib's Mirage material/texture structures. The supplied `ChrSkin.hlsl` establishes packed normal decoding, specular texture RGB, the Y/Z components of `power_gloss_level`, and the falloff inputs. Additional local GenerationsRaytracing shader references distinguish ChrEye's packed surface-normal B/A channels and green gloss channel. Neither reference shader is bundled. Standard SpatialMaterial resources and embedded ImageTextures are emitted by managed C# and compiled by the selected Acorn editor.
