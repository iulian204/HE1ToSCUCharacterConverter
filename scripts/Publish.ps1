param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\HE1ToSCU-win-x64'))
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
& dotnet publish (Join-Path $workspace 'src\HE1ToSCU.App\HE1ToSCU.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $OutputDirectory
if ($LASTEXITCODE -ne 0) {throw 'Publish failed.'}
Copy-Item -LiteralPath (Join-Path $workspace 'LICENSE') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'THIRD-PARTY-NOTICES.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'README.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'CHANGELOG.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'docs') -Destination $OutputDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $workspace 'src\HE1ToSCU.Core\ThirdParty\HedgeLib-LICENSE.txt') -Destination $OutputDirectory
$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'LICENSE.txt') -Destination (Join-Path $OutputDirectory 'RUNTIME-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $OutputDirectory 'RUNTIME-THIRD-PARTY-NOTICES.txt')
Write-Output "Portable app: $(Join-Path $OutputDirectory 'HE1ToSCU.App.exe')"
