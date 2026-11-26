"""Independent import comparison of unit-corrected NPOT and POT LakeGirl FBXs."""
import hashlib
import json
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[3]
STAGE = ROOT / 'Logs/unity-oral-pilot-lakegirl-pot-stage'


def read(path):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=False)
    result = {}
    for index in range(3):
        ob = bpy.data.objects[f'LakeGirl_LOD{index}']
        result[index] = {
            'vertices': [tuple(v.co) for v in ob.data.vertices],
            'faces': [tuple(p.vertices) for p in ob.data.polygons],
            'weights': [[(ob.vertex_groups[g.group].name, g.weight) for g in v.groups]
                        for v in ob.data.vertices],
            'uv': [tuple(item.uv) for item in ob.data.uv_layers.active.data],
            'slots': [p.material_index for p in ob.data.polygons],
            'clothLoops': [i for p in ob.data.polygons if p.material_index == 3
                           for i in p.loop_indices],
        }
    return result


def main():
    previous = ROOT / 'Logs/unity-oral-pilot-lakegirl-v2-stage/incoming/Models/LakeGirl.fbx'
    candidate = STAGE / 'incoming/Models/LakeGirl.fbx'
    a, b = read(previous), read(candidate)
    rows = []
    for index in range(3):
        x, y = a[index], b[index]
        for key in ['vertices', 'faces', 'weights', 'slots', 'clothLoops']:
            assert x[key] == y[key], (index, key)
        cloth = set(x['clothLoops'])
        assert all(u == v for i, (u, v) in enumerate(zip(x['uv'], y['uv'])) if i not in cloth)
        pixel_delta = max(abs(x['uv'][i][1] * 2112 - y['uv'][i][1] * 4096) for i in cloth)
        assert pixel_delta < .001
        assert all(x['uv'][i][0] == y['uv'][i][0] for i in cloth)
        extent = [[min(y['uv'][i][axis] for i in cloth),
                   max(y['uv'][i][axis] for i in cloth)] for axis in [0, 1]]
        assert all(0 <= low <= high <= 1 for low, high in extent), 'No Repeat-wrap mapping equivalence'
        rows.append({'lod': index, 'fullGeometryWeightsTopologySlotsExactlyPreserved': True,
                     'nonClothUVExactlyPreserved': True, 'clothUVExtents': extent,
                     'clothUExactlyPreserved': True, 'pixelSampleCoordinateMaxDelta': pixel_delta})
    report = {'scope': 'Independent Blender FBX import; no Unity/GPU acceptance',
              'previousSha256': hashlib.sha256(previous.read_bytes()).hexdigest(),
              'candidateSha256': hashlib.sha256(candidate.read_bytes()).hexdigest(),
              'lods': rows, 'mipCaveat': 'Previous NPOT mip grids diverge after A/N mip6. POT retains the original cloth power-of-two mip grid; actual BC compression and LOD bleed require Unity pixel review.'}
    (STAGE / 'verification/uv-roundtrip.json').write_text(json.dumps(report, indent=2) + '\n')
    print('[oral-POT-UV] PASS', json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
