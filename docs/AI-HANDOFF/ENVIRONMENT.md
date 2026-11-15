# Environment and Local References

## Repository / runtime

- Verified repository root: `D:\HE1ToSCUCharacterConverter`.
- Verified current branch: `main`; current HEAD `08d0ad7647ecc01d0753403dd8586887169ea1f2`, tag `v0.7.0`.
- App/core/test projects target .NET 10 in the current released baseline. Local SDK wrapper used in prior work: `D:\HE1ToSCUCharacterConverter\artifacts\dotnet10\dotnet.exe`.
- Shell used: Windows PowerShell. Build command: `dotnet build HE1ToSCU.sln -c Release`; test command: `dotnet test HE1ToSCU.sln -c Release`; portable build script: `scripts/Publish.ps1`.
- Godot/Acorn editor and runtime test executable (verified): `D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe`. Set `HE1SCU_TEST_ACORN` to override in test process.
- Temporary inventory work area: `artifacts\scu-naming\inventory-build\`; logs: `artifacts\scu-naming\inventory-build.log`, `source-profiles.log`, `build.log`. Do not delete useful logs while matching work remains.

## HE1 / PRR references

- Generations PRR library previously supplied and verified: `D:\HE1TOPRRShaders\PRR shaders\Generations`.
- Unleashed PRR library previously supplied and verified: `D:\HE1TOPRRShaders\PRR shaders\Unleashed`.
- Updated/release source shaders/docs: `D:\HE1TOPRRShaders\Release` was provided later and incorporated into the v0.7.0 source baseline. When investigating current behavior, use those supplied PRR shaders and the embedded copies; do not search/download other shader libraries as replacements.
- HedgeLib reference parser checkout: `references\HedgeLib\HedgeLib\src\materials\hl_hh_material.cpp` (verify path exists). It documents the `as_vec3()` color behavior referenced by mapper comments/tests.
- PRR source docs: `docs\PRR-MATERIALS.md`, `docs\PRR-PARAMETER-AUDIT.md`, `docs\PRR-SHADER-UNIFORMS.json`, `docs\FORMAT-EVIDENCE.md`, `docs\VALIDATION.md`.
- Historical PRR reports: `artifacts\prr-parameter-audit\`; test logs: `artifacts\prr-audit\`.

## Game assets (verified local paths from completed extraction)

- SCU Sonic: `D:\SCUExtract\characters\player_Sonic`.
- SCU Super Sonic: `D:\SCUExtract\characters\player_Supersonic`.
- SCU original PCK files: `D:\SteamLibrary\steamapps\common\SonicColorsUltimate\content\acorn` (verified original `sonic3.pck` scenes and `sonic5.pck` remap references).
- Generations Sonic source: `D:\SteamLibrary\steamapps\common\Sonic Generations\disk\bb3\Sonic`.
- Unleashed Sonic source: `C:\Users\jigai\Desktop\CorrectSUSonic\Sonic`.
- Neither SCU nor HE1 binary assets should be copied into source control or release artifacts. The inventory/provenance JSON holds names, derived metadata, hashes, and descriptors only.

## Portable outputs

- Prior PRR authored-value archive recorded in memory: `artifacts\HE1ToSCU-PRR-authored-values-win-x64.zip` (verify existence before reliance).
- Previous v0.7.0 release artifacts/baselines are under `artifacts\v0.7.0-release\` if retained. Current naming feature has no generated final portable package.
- Do not publish or modify release tags as part of the current request; user requested handoff, not a release.

No secrets, credentials, or tokens are included here.
