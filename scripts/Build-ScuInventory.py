"""Development-only SCU metadata extractor. Game resources are never copied into the app."""
import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import struct

parser = argparse.ArgumentParser()
parser.add_argument('--scu-root', required=True)
parser.add_argument('--acorn', required=True)
parser.add_argument('--game-pck-directory', help='Optional original SCU content/acorn folder for verified native scene remaps')
args = parser.parse_args()
workspace = pathlib.Path(__file__).resolve().parents[1]
root = pathlib.Path(args.scu_root).resolve()
stage = workspace / 'artifacts/scu-naming/inventory-build'
stage.mkdir(parents=True, exist_ok=True)
output = workspace / 'assets/SCU'
output.mkdir(parents=True, exist_ok=True)
number = r'[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def resource(p): return 'res://' + p.relative_to(root).as_posix()
def fields(header): return dict(re.findall(r'(\w+)="([^"]*)"', header))
def scenes(p):
    text = p.read_text(encoding='utf-8-sig')
    nodes, skeletons, meshes, embedded = [], [], [], []
    blocks = re.split(r'(?=^\[)', text, flags=re.M)
    mesh_resources = {}
    for block in blocks:
        header = block.split('\n', 1)[0]
        if header.startswith('[sub_resource type="ArrayMesh"'):
            name = re.search(r'^resource_name\s*=\s*"([^"]*)"', block, re.M)
            mesh_resources[int(re.search(r'id=(\d+)', header)[1])] = {
                'resourceName': name[1] if name else None,
                'surfaceCount': len(set(re.findall(r'^surfaces/(\d+)\s*=', block, re.M)))}
        if header.startswith('[sub_resource type="Animation"'):
            name = re.search(r'^resource_name\s*=\s*"([^"]*)"', block, re.M)
            length = re.search(r'^length\s*=\s*('+number+')', block, re.M)
            embedded.append({'subresourceId': int(re.search(r'id=(\d+)', header)[1]),
                'resourceName': name[1] if name else None, 'duration': float(length[1]) if length else 1.0,
                'loop': bool(re.search(r'^loop\s*=\s*true', block, re.M)),
                'trackPaths': re.findall(r'^tracks/\d+/path\s*=\s*NodePath\(\s*"([^"]*)"', block, re.M)})
        if not header.startswith('[node '): continue
        node = fields(header)
        nodes.append(node)
        if node.get('type') == 'MeshInstance':
            m = re.search(r'^mesh\s*=\s*SubResource\(\s*(\d+)', block, re.M)
            layers = re.search(r'^layers\s*=\s*(\d+)', block, re.M)
            meshes.append(dict(node, layers=int(layers[1]) if layers else 1, **(mesh_resources.get(int(m[1]), {}) if m else {})))
        if node.get('type') == 'Skeleton':
            bones = []
            for i, name in re.findall(r'^bones/(\d+)/name\s*=\s*"([^"]*)"', block, re.M):
                parent = re.search(r'^bones/'+i+r'/parent\s*=\s*(-?\d+)', block, re.M)
                rest = re.search(r'^bones/'+i+r'/rest\s*=\s*Transform\(([^)]*)\)', block, re.M)
                bones.append({'name': name, 'parent': int(parent[1]) if parent else -1,
                    'rest': [float(v) for v in re.findall(number, rest[1])] if rest else [1,0,0,0,1,0,0,0,1,0,0,0]})
            skeletons.append(dict(node, bones=bones))
    return {'path': resource(p), 'rootNode': nodes[0].get('name') if nodes else None,
        'nodes': nodes, 'meshes': meshes, 'skeletons': skeletons, 'embeddedAnimations': embedded,
        'references': re.findall(r'\[ext_resource path="([^"]+)"', text)}

profiles, all_files = [], []
for identity, folder in [('Sonic', 'player_Sonic'), ('SuperSonic', 'player_Supersonic')]:
    directory = root / 'characters' / folder
    models = [scenes(p) for p in sorted(directory.rglob('*.tscn'))]
    primary = max(models, key=lambda s: max((len(b['bones']) for b in s['skeletons']), default=0))
    skeleton = max(primary['skeletons'], key=lambda s: len(s['bones']))
    mesh = next(m for m in primary['meshes'] if m.get('parent') == skeleton['name'])
    files = sorted(p for p in directory.rglob('*') if p.is_file())
    for p in files:
        all_files.append({'path': resource(p), 'bytes': p.stat().st_size, 'sha256': digest(p),
            'metadataKind': 'animation' if p.suffix == '.anim' else 'scene' if p.suffix == '.tscn' else 'non-naming asset; contents not interpreted'})
    profiles.append({'id': identity, 'resourceRoot': resource(directory),
        'sceneStem': pathlib.PurePosixPath(primary['path']).stem, 'sceneResource': primary['path'],
        'rootNodeName': primary['rootNode'], 'skeletonNodeName': skeleton['name'],
        'meshNodeName': mesh['name'], 'meshResourceName': mesh.get('resourceName') or mesh['name'],
        'meshRenderLayers': mesh['layers'],
        'models': models, 'skeletons': [dict(s, scene=m['path']) for m in models for s in m['skeletons']],
        'primaryBones': skeleton['bones'],
        'animationFiles': [{'path': resource(p), 'diskPath': str(p), 'sha256': digest(p)} for p in files if p.suffix == '.anim']})
references = []
# Capture actual bundle/scene references elsewhere in the extraction without loading their game-specific classes.
process = subprocess.run(['rg', '-l', '--fixed-strings', '-e', 'characters/player_Sonic/', '-e', 'characters/player_Supersonic/',
    '-g', '*.tres', '-g', '*.tscn', '-g', '*.gd', str(root)], capture_output=True, text=True)
for name in sorted(process.stdout.splitlines()):
    p = pathlib.Path(name)
    text = p.read_text(encoding='utf-8-sig', errors='replace')
    links = sorted(set(re.findall(r'res://characters/player_(?:Sonic|Supersonic)/[^"\s]+', text)))
    references.append({'path': resource(p), 'sha256': digest(p), 'resources': links})
request = {'profiles': profiles}
(stage / 'request.json').write_text(json.dumps(request), encoding='utf-8')
(stage / 'project.godot').write_text('config_version=4\n[rendering]\nquality/driver/driver_name="GLES2"\n', encoding='utf-8')
script = pathlib.Path(__file__).with_name('Extract-ScuAnimationMetadata.gd').read_text(encoding='utf-8')
(stage / 'extract.gd').write_text(script, encoding='utf-8')
with (stage / 'extraction.log').open('w', encoding='utf-8') as log:
    run = subprocess.run([args.acorn, '--no-window', '--quit', '--path', str(stage), '--script', 'extract.gd'],
        stdout=log, stderr=subprocess.STDOUT, text=True, timeout=600)
log = (stage / 'extraction.log').read_text(encoding='utf-8')
if run.returncode or 'SCU_INVENTORY_EXTRACTED' not in log or 'SCRIPT ERROR' in log: raise RuntimeError(log)
decoded = json.loads((stage / 'decoded.json').read_text(encoding='utf-8'))
for profile in profiles:
    profile['animations'] = decoded['profiles'][profile['id']]
    for entry in profile['animations']:
        original = next(f for f in profile['animationFiles'] if f['path'] == entry['path'])
        entry['sha256'] = original['sha256']
    del profile['animationFiles']
    # Static rest translations are structural metadata. Complete rest bases are extraction inputs only.
    for scene in profile['models']:
        for s in scene['skeletons']:
            for bone in s['bones']: bone['restTranslation'] = bone.pop('rest')[9:]
    profile['boneAliases'] = decoded['boneAliases']
    del profile['primaryBones']
result = {'schemaVersion': 1, 'inventoryVersion': '2026-11-14.1', 'descriptorVersion': 1,
    'provenance': {'reference': str(root), 'developmentOnly': True, 'engine': decoded['engine'],
        'method': 'Original scene text, resource bundles, native Animation APIs and 17 normalized phase samples; no game binaries bundled'},
    'profiles': profiles, 'files': all_files, 'references': references,
    'limitations': ['Names do not retarget bones or recreate SCU gameplay/attachments.',
        'Coarse normalized motion descriptors are matching evidence, not original animation content.',
        'Sparse key counts and editor step are reported; authored frame counts cannot be recovered reliably.',
        'Embedded facial/effect animations are inventoried but not humanoid replacement slots.']}
if args.game_pck_directory:
    native = []
    remaps = {}
    for pck in sorted(pathlib.Path(args.game_pck_directory).glob('sonic*.pck')):
        with pck.open('rb') as f:
            if f.read(4) != b'GDPC' or struct.unpack('<I', f.read(4))[0] != 1: raise ValueError('Unsupported original PCK header')
            f.seek(84)
            count = struct.unpack('<I', f.read(4))[0]
            for _ in range(count):
                size = struct.unpack('<I', f.read(4))[0]
                path = f.read(size).rstrip(b'\0').decode('utf-8')
                offset, length = struct.unpack('<QQ', f.read(16))
                md5 = f.read(16).hex()
                if not any(path.startswith(profile['resourceRoot'] + '/') for profile in profiles): continue
                native.append({'path': path, 'pack': pck.name, 'bytes': length, 'md5': md5})
                if path.endswith('.remap'):
                    position = f.tell()
                    f.seek(offset)
                    remaps[path] = f.read(length).decode('utf-8')
                    f.seek(position)
    for profile in profiles:
        text = remaps.get(profile['sceneResource'] + '.remap')
        if text:
            target = re.search(r'path="([^"]+)"', text)[1]
            assert any(entry['path'] == target for entry in native), target
            profile['nativeSceneFile'] = target.rsplit('/', 1)[1]
            profile['nativeScenePath'] = target
            profile['sceneRemapText'] = text
    result['nativePackResources'] = native
result['inventoryVersion'] = '2026-11-14.' + hashlib.sha256(json.dumps({
    'files': all_files, 'native': result.get('nativePackResources', [])}, sort_keys=True).encode()).hexdigest()[:12]
(output / 'sonic-inventory.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print([(p['id'], len(p['models']), len(p['animations']), sum(a.get('eligible', False) for a in p['animations'])) for p in profiles])
