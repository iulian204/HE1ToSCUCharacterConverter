param([Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$workspace = Split-Path -Parent $PSScriptRoot
$targetRoot = Join-Path $workspace 'src/HE1ToSCU.Core/PrrShaders'
$manifestPath = Join-Path $sourceRoot 'release-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
$presets = (Get-Content -LiteralPath (Join-Path $sourceRoot 'preset-manifest.json') -Raw | ConvertFrom-Json -AsHashtable).presets
$catalog = Get-Content -LiteralPath (Join-Path $sourceRoot 'Inventory/shaders.json') -Raw | ConvertFrom-Json -AsHashtable
$exactAliases = Get-Content -LiteralPath (Join-Path $sourceRoot 'Inventory/aliases.json') -Raw | ConvertFrom-Json -AsHashtable
if ($catalog.Count -ne 152 -or $presets.Count -ne 3648) { throw 'Unexpected PRR Release inventory; review the new release schema before importing.' }

# Validate the complete delivery, including documentation and validation receipts, before replacing any bytes.
foreach ($entry in $manifest.files.GetEnumerator()) {
    $file = [IO.Path]::GetFullPath((Join-Path $sourceRoot $entry.Key))
    if (!$file.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe release path: $($entry.Key)" }
    if (!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value) {
        throw "Release integrity check failed: $($entry.Key)"
    }
}
$hashes = [ordered]@{}
foreach ($path in ($presets.Keys | Sort-Object -CaseSensitive)) {
    if (!$path.EndsWith('.shader') -or !$manifest.files.Contains($path) -or $manifest.files[$path] -ne $presets[$path].sha256) {
        throw "Preset/hash manifest mismatch: $path"
    }
    $hashes[$path] = $presets[$path].sha256
}
$bindings = @{}
foreach ($entry in $catalog.GetEnumerator()) {
    $game = $entry.Key.Split('/')[0]
    foreach ($reference in $entry.Value.shader_list_references) {
        $identity = $game + '/' + [IO.Path]::GetFileNameWithoutExtension($reference.list)
        $bindings[$identity] = @($bindings[$identity]) + @($entry.Key) | Where-Object { $_ } | Sort-Object -Unique -CaseSensitive
    }
}
foreach ($entry in $exactAliases.GetEnumerator()) {
    $name = $entry.Key.Substring(0, $entry.Key.Length - '.shader'.Length)
    $bindings[$name] = @($entry.Value.published_path)
    $bindings[$name -replace '@@$', ''] = @($entry.Value.published_path)
}
$orderedBindings = [ordered]@{}
foreach ($key in ($bindings.Keys | Sort-Object -CaseSensitive)) { $orderedBindings[$key] = @($bindings[$key]) }

foreach ($path in $hashes.Keys) {
    $target = Join-Path $targetRoot $path
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRoot $path) -Destination $target
}
$hashes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $targetRoot 'inventory.json') -Encoding utf8NoBOM
$orderedBindings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $targetRoot 'bindings.json') -Encoding utf8NoBOM
$provenance = [ordered]@{
    shader_code_sources = @('Release/Generations', 'Release/Unleashed', 'Release/Render States')
    release_date = $manifest.date
    release_manifest_sha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    canonical_counts = @{ Generations = 99; Unleashed = 53 }
    native_shader_presets = $hashes.Count
    source_identifier_aliases = $orderedBindings.Count
    source_documents_reviewed = @(Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Documentation') -Filter '*.md' | ForEach-Object { 'Release/Documentation/' + $_.Name })
    dependencies = 'Standalone, script-free Godot 3.1.2 GLES3 shaders; no runtime helpers or engine modifications'
    redistribution = 'Custom shader libraries supplied by the project owner for express inclusion; no separate shader license file supplied'
    excluded = 'Older PRR shader generator inputs, historical helpers, scripts and optional empty material templates'
}
$provenance | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $targetRoot 'provenance.json') -Encoding utf8NoBOM
$docs = Join-Path $workspace 'docs/PRR-Library'
New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach ($folder in @('Documentation', 'Inventory', 'Validation', 'Comparisons')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot $folder) -Destination $docs -Recurse -Force
}
foreach ($file in @('README.md', 'release-manifest.json', 'preset-manifest.json', 'SHA256SUMS.txt')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot $file) -Destination $docs -Force
}
Write-Output "Verified and imported $($catalog.Count) programs / $($hashes.Count) native shader presets; $($orderedBindings.Count) source identifiers."
