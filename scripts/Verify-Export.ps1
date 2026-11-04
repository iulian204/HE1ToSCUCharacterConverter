param(
    [Parameter(Mandatory=$true)][string]$ExportDirectory,
    [string]$AcornEditor = 'D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe'
)
$ErrorActionPreference = 'Stop'
$exportPath = (Resolve-Path -LiteralPath $ExportDirectory).Path
$sceneFiles = @(Get-ChildItem -LiteralPath $exportPath -File -Filter '*.scn')
if ($sceneFiles.Count -ne 1) { throw 'Expected exactly one exported .scn scene.' }
$sceneResource = ConvertTo-Json -InputObject ('res://' + $sceneFiles[0].Name) -Compress
$verifyPath = Join-Path ([System.IO.Path]::GetTempPath()) ('HE1SCU-relocation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verifyPath | Out-Null
Get-ChildItem -LiteralPath $exportPath -File | Where-Object {$_.Extension -in '.scn','.mesh','.anim'} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $verifyPath}
Set-Content -LiteralPath (Join-Path $verifyPath 'project.godot') -Value "config_version=4`n[rendering]`nquality/driver/driver_name=`"GLES2`"" -Encoding utf8
$script = @'
extends SceneTree
func _init():
    var packed = load(__SCENE_RESOURCE__)
    if packed == null:
        printerr("RELOCATION_FAILED scene")
        quit()
        return
    var instance = packed.instance()
    get_root().add_child(instance)
    var skeleton = instance.get_node("Skeleton")
    if skeleton.get_bone_count() == 0:
        printerr("RELOCATION_FAILED rig")
        quit()
        return
    var meshes = 0
    var surfaces = 0
    for child in skeleton.get_children():
        if not (child is MeshInstance):
            continue
        var mesh = child.mesh
        if mesh == null or child.get_node(child.skeleton) != skeleton:
            printerr("RELOCATION_FAILED mesh/skeleton")
            quit()
            return
        for surface in range(mesh.get_surface_count()):
            if not (mesh.surface_get_material(surface) is SpatialMaterial):
                printerr("RELOCATION_FAILED material")
                quit()
                return
        meshes += 1
        surfaces += mesh.get_surface_count()
    if meshes == 0:
        printerr("RELOCATION_FAILED no meshes")
        quit()
        return
    var count = 0
    if instance.has_node("AnimationPlayer"):
        var player = instance.get_node("AnimationPlayer")
        for name in player.get_animation_list():
            var animation = player.get_animation(name)
            if animation == null or animation.get_track_count() == 0:
                printerr("RELOCATION_FAILED animation")
                quit()
                return
            player.play(name)
            player.seek(animation.length * 0.5, true)
            count += 1
    print("RELOCATION_OK bones=" + str(skeleton.get_bone_count()) + " meshes=" + str(meshes) + " surfaces=" + str(surfaces) + " animations=" + str(count))
    instance.free()
    quit()
'@
$scriptPath = Join-Path $verifyPath 'verify.gd'
$script = $script.Replace('__SCENE_RESOURCE__', $sceneResource)
Set-Content -LiteralPath $scriptPath -Value $script -Encoding utf8
$result = & $AcornEditor --no-window --path $verifyPath --script $scriptPath 2>&1
$result | Set-Content -LiteralPath (Join-Path $verifyPath 'verification.log')
$result | Write-Output
if (-not ($result -match 'RELOCATION_OK') -or ($result -match 'ERROR:|RELOCATION_FAILED')) {throw "Relocated binary-only load failed. Evidence: $verifyPath"}
Write-Output "Binary resources loaded independently, without text source resources. Evidence: $verifyPath"
