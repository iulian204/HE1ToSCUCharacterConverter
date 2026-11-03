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

The application uses the .NET 8 runtime and WPF. Microsoft .NET runtime license notices are included by the self-contained publish output. xUnit and Microsoft.NET.Test.Sdk are development/test dependencies only.
