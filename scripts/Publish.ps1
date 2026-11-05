param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\HE1ToSCU-win-x64'))
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$appDirectory = Join-Path $workspace 'src\HE1ToSCU.App'
& dotnet publish (Join-Path $appDirectory 'HE1ToSCU.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $OutputDirectory
if ($LASTEXITCODE -ne 0) {throw 'Publish failed.'}
# The native NuGet packages supply these symbols as separate files.
foreach ($name in 'libHarfBuzzSharp.pdb', 'libSkiaSharp.pdb') {
    $symbol = Join-Path $OutputDirectory $name
    if (Test-Path -LiteralPath $symbol -PathType Leaf) { Remove-Item -LiteralPath $symbol }
}
Copy-Item -LiteralPath (Join-Path $workspace 'LICENSE') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'THIRD-PARTY-NOTICES.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'README.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'CHANGELOG.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'docs') -Destination $OutputDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $workspace 'src\HE1ToSCU.Core\ThirdParty\HedgeLib-LICENSE.txt') -Destination $OutputDirectory
$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'LICENSE.txt') -Destination (Join-Path $OutputDirectory 'RUNTIME-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $OutputDirectory 'RUNTIME-THIRD-PARTY-NOTICES.txt')

$assets = Get-Content -LiteralPath (Join-Path $appDirectory 'obj\project.assets.json') -Raw | ConvertFrom-Json
$licenses = Join-Path $OutputDirectory 'docs\licenses'
function Copy-PackageNotice([string]$Package, [string]$File, [string]$Name) {
    $packagePath = ($assets.libraries.PSObject.Properties | Where-Object Name -Like "$Package/*").Value.path
    $source = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object {
        Join-Path (Join-Path $_ $packagePath) $File
    } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$source) { throw "Missing $File for $Package." }
    Copy-Item -LiteralPath $source -Destination (Join-Path $licenses $Name)
}
Copy-PackageNotice 'Avalonia.Angle.Windows.Natives' 'LICENSE' 'ANGLE.txt'
Copy-PackageNotice 'SkiaSharp.NativeAssets.Win32' 'LICENSE.txt' 'SkiaSharp.txt'
Copy-PackageNotice 'SkiaSharp.NativeAssets.Win32' 'THIRD-PARTY-NOTICES.txt' 'SkiaSharp-ThirdParty.txt'
Copy-PackageNotice 'HarfBuzzSharp.NativeAssets.Win32' 'LICENSE.txt' 'HarfBuzzSharp.txt'
Copy-PackageNotice 'HarfBuzzSharp.NativeAssets.Win32' 'THIRD-PARTY-NOTICES.txt' 'HarfBuzzSharp-ThirdParty.txt'
Write-Output "Portable app: $(Join-Path $OutputDirectory 'HE1ToSCU.App.exe')"
