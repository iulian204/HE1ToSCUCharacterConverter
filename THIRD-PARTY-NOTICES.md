# Third-party notices

This application is distributed under GPL-3.0-or-later. See `LICENSE` for the full license. Source and build scripts are supplied in this workspace/source package.

## Spline decoder

`src/HE1ToSCU.Core/ThirdParty/SplineCompressedAnimation.cs` is adapted from `SoulsAssetPipeline/Animation/HKX/SplineCompressedAnimation.cs` in Meowmaritus/SoulsAssetPipeline, itself adapted from the Havok Format Library / PredatorCZ/HavokLib.

- Original code: Copyright (C) 2016–2019 Lukas Cone.
- C# adaptation: Meowmaritus and Katalash.
- License: GNU General Public License version 3 or later.
- Local modifications are documented in `docs/FORMAT-EVIDENCE.md` and in the source file header. The file retains the original copyright/license notice.

## HedgeLib

HE1 structures and vertex format definitions were implemented with reference to HedgeLib. Copyright (c) 2023 Radfordhound. MIT license; the complete notice is retained at `src/HE1ToSCU.Core/ThirdParty/HedgeLib-LICENSE.txt`.

## Other references

BlueBlur, SkythTools, HavokAnimationExporter, ModelConverter, HedgeDocs, libgens-sonicglvl and upstream Godot were inspected for format and engine behavior. Exact repository revisions are in `docs/reference-lock.json`. These complete repositories are not linked into or shipped with the executable. No game assets, Acorn executable, proprietary Havok SDK or FBX SDK are included in the distributable.

## Project Re-Rainbow shaders

The complete user-supplied Project Re-Rainbow 2026-11-14 Release (152 programs with 3,648 native draw-state shader presets) are embedded and redistributed at the project owner's express request. Source hashes and exact shader files are retained in `src/HE1ToSCU.Core/PrrShaders`; mappings and integration boundaries are documented in `docs/PRR-MATERIALS.md`. No separate license file was supplied with these custom collections. This authorization does not license original Sonic game assets or proprietary shader sources; those are not included. Archived shaders and original HLSL/ASM reference files are not bundled.

## Desktop and runtime dependencies

The desktop app uses Avalonia and Avalonia Fluent (MIT), with MicroCom and Tmds.DBus Protocol (MIT) among its transitive dependencies. Their notices are in `docs/licenses`. The Windows x64 package also includes the notices from the bundled ANGLE, SkiaSharp and HarfBuzzSharp NuGet packages, plus the Microsoft .NET 10 runtime licenses. xUnit and Microsoft.NET.Test.Sdk are test dependencies.
