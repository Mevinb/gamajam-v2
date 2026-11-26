"""Portable LakeGirl oral runtime authoring from canonical V2 source only.

No Logs input, MPFB installation, home-directory source or current runtime
asset is required. Writes only a fresh explicitly selected output directory.
The source master is immutable; runtime acceptance remains a separate Unity gate.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys

import bpy
import numpy as np

from io_scene_fbx import parse_fbx

ROOT = None
OUT = None


def raw(path):
    tree,_=parse_fbx.parse(str(path));find=lambda e,k:next((x for x in e.elems if x.id==k),None)
    p=find(find(tree,b'GlobalSettings'),b'Properties70');units={e.props[0].decode():e.props[4:]for e in p.elems if e.id==b'P'and e.props[0]in [b'UnitScaleFactor',b'OriginalUnitScaleFactor']};models={}
    for e in find(tree,b'Objects').elems:
        if e.id!=b'Model':continue
        name=e.props[1].decode(errors='replace').split('\x00')[0];p=find(e,b'Properties70');values={x.props[0].decode():x.props[4:]for x in p.elems if x.id==b'P'}if p else{}
        models[name]={'scale':values.get('Lcl Scaling',[1.,1.,1.]),'translation':values.get('Lcl Translation',[0.,0.,0.]),'rotation':values.get('Lcl Rotation',[0.,0.,0.])}
    return {'units':units,'models':models}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_pixels(path):
    image = bpy.data.images.load(str(path), check_existing=False)
    image.colorspace_settings.name = 'Non-Color'
    w, h = image.size
    values = np.empty(w * h * 4, np.float32)
    image.pixels.foreach_get(values)
    bpy.data.images.remove(image)
    return np.rint(values.reshape(h, w, 4)[..., :3] * 255).astype(np.uint8)


def write_pixels(values, path):
    h, w, _ = values.shape
    image = bpy.data.images.new(path.stem, w, h, alpha=False, float_buffer=False)
    image.colorspace_settings.name = 'Non-Color'
    rgba = np.ones((h, w, 4), np.float32)
    rgba[..., :3] = values.astype(np.float32) / 255
    image.pixels.foreach_set(rgba.ravel())
    image.file_format = 'PNG'
    image.filepath_raw = str(path)
    image.save()
    bpy.data.images.remove(image)


def geometry(mesh):
    # Deliberately excludes UV; includes all unchanged surface/rest inputs.
    body = {
        'vertices': [list(v.co) for v in mesh.data.vertices],
        'normals': [list(v.normal) for v in mesh.data.vertices],
        'faces': [list(p.vertices) for p in mesh.data.polygons],
        'materialIndices': [p.material_index for p in mesh.data.polygons],
        'weights': [[(mesh.vertex_groups[g.group].name, g.weight)
                     for g in v.groups] for v in mesh.data.vertices],
        'matrix': [v for row in mesh.matrix_world for v in row],
    }
    return hashlib.sha256(json.dumps(body, sort_keys=True).encode()).hexdigest()


def main():
    global ROOT, OUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--recipe', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    ROOT, OUT = args.project.resolve(), args.output.resolve()
    assert OUT != ROOT and ROOT / 'Assets' not in [OUT, *OUT.parents]
    assert ROOT / 'ArtSource' not in [OUT, *OUT.parents], 'Never overwrite canonical sources'
    recipe_path = args.recipe or ROOT / 'ArtSource/Characters/OralPilotV2/runtime-integration.json'
    recipe = json.loads(recipe_path.read_text())
    assert list(bpy.app.version) == recipe['reviewedBlenderVersion'], 'Revalidate a different exporter version'
    source = ROOT / recipe['sourceBlend']['path']
    assert sha(source) == recipe['sourceBlend']['sha256']
    assert not OUT.exists(), 'Choose a fresh entire output directory'
    (OUT / 'incoming/Textures').mkdir(parents=True)
    (OUT / 'incoming/Models').mkdir()
    (OUT / 'verification').mkdir()
    textures = {}
    for suffix, width, old_height, new_height in [('A', 2048, 2112, 4096),
                                                ('N', 2048, 2112, 4096),
                                                ('M', 1024, 1056, 2048)]:
        old_path = ROOT / recipe['sourceAtlases'][suffix]['path']
        assert sha(old_path) == recipe['sourceAtlases'][suffix]['sha256']
        old = read_pixels(old_path)
        assert old.shape == (old_height, width, 3)
        canvas = np.empty((new_height, width, 3), np.uint8)
        canvas[:old_height] = old
        canvas[old_height:] = old[-1:]
        target = OUT / f'incoming/Textures/LakeGirl_Cloth_{suffix}.png'
        write_pixels(canvas, target)
        assert sha(target) == recipe['expectedRuntimeTextures'][suffix]['sha256'], 'POT image bytes differ from reviewed recipe'
        decoded = read_pixels(target)
        assert np.array_equal(decoded[:old_height], old), 'Original texels changed'
        assert np.array_equal(decoded[old_height:], canvas[old_height:]), 'Padding changed'
        textures[suffix] = {
            'source': str(old_path.relative_to(ROOT)), 'sourceSha256': sha(old_path),
            'path': str(target.relative_to(OUT)), 'sha256': sha(target),
            'bytes': target.stat().st_size, 'dimensions': [width, new_height],
            'preservedLowerRows': old_height, 'maxDecodedByteError': 0,
            'padding': 'repeat final NPOT atlas row; no resampling',
            'uvVTransformFromPrevious': [33, 64],
        }
    bpy.ops.wm.open_mainfile(filepath=str(source))
    bpy.context.scene.frame_set(1)
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    rest = {b.name: [v for row in b.matrix_local for v in row] for b in rig.data.bones}
    rest_contract = {b.name: {'parent': b.parent.name if b.parent else None,
                              'matrix': [float(v) for row in b.matrix_local for v in row]}
                     for b in rig.data.bones}
    rest_sha = hashlib.sha256(json.dumps(rest_contract, sort_keys=True,
                                         separators=(',', ':')).encode()).hexdigest()
    assert rest_sha == recipe['expectedSourceRestSha256'], 'Canonical rest contract changed'
    lods = [bpy.data.objects[f'LakeGirl_LOD{i}'] for i in range(3)]
    lod_checks = []
    for mesh in lods:
        before = geometry(mesh)
        index = len(lod_checks)
        assert before == recipe['expectedLods'][index]['surfaceAndWeightsHash']
        assert [m.name.split('.')[0] for m in mesh.data.materials] == ['Skin', 'Eyes', 'Hair', 'Cloth']
        uv = mesh.data.uv_layers.active.data
        old = {loop: tuple(uv[loop].uv) for p in mesh.data.polygons for loop in p.loop_indices}
        cloth = {loop for p in mesh.data.polygons if p.material_index == 3 for loop in p.loop_indices}
        for loop in cloth:
            uv[loop].uv.y *= 33 / 64
        max_pixel_delta = max(abs(old[i][1] * 2112 - uv[i].uv.y * 4096) for i in cloth)
        assert max_pixel_delta < .0003
        assert all(tuple(uv[i].uv) == old[i] for i in set(old) - cloth)
        assert before == geometry(mesh), 'Geometry, weights, normals or slots changed'
        lod_checks.append({'name': mesh.name, 'surfaceAndWeightsHash': before,
                           'triangles': sum(len(p.vertices) - 2 for p in mesh.data.polygons),
                           'clothUVLoops': len(cloth), 'sampleCoordinateMaxErrorPixels': max_pixel_delta,
                           'nonClothUVUnchanged': True, 'geometryWeightsNormalsUnchanged': True})
    assert rest == {b.name: [v for row in b.matrix_local for v in row] for b in rig.data.bones}
    for ob in list(bpy.context.selected_objects):
        ob.select_set(False)
    rig.select_set(True)
    for ob in lods:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = rig
    target = OUT / 'incoming/Models/LakeGirl.fbx'
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True,
                            object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True,
                            apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                            use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=True,
                            use_armature_deform_only=True, add_leaf_bones=False,
                            primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=False,
                            path_mode='STRIP', embed_textures=False, use_custom_props=False)
    a, b = recipe['rawFBXContract'], raw(target)
    assert a['units'] == b['units'] == {'UnitScaleFactor': [100.], 'OriginalUnitScaleFactor': [100.]}
    assert a['models'].keys() == b['models'].keys()
    for name in a['models']:
        assert a['models'][name] == b['models'][name], (name, 'raw FBX transform changed')
    meta = recipe['finalMetadata']
    meta['oralPilot'].update(revision=4, integrationRevision=4, clothVScale=[1, 2],
                             albedoNormalDimensions=[2048, 4096], maskDimensions=[1024, 2048],
                             previousCompactAtlasUVScale=[33, 64],
                             sourceUnitPolicy='FBX_SCALE_UNITS',
                             padding='exact NPOT lower rows plus repeated final row')
    (OUT / 'incoming/Models/LakeGirl.json').write_text(json.dumps(meta, indent=1) + '\n')
    receipt = {
        'revision': 4, 'scope': 'Portable canonical-input LakeGirl-only POT reconstruction; no live Assets/Unity/GPU changes',
        'sourceBlendSha256': sha(source), 'authorSourceSha256': sha(Path(__file__)),
        'recipeSha256': sha(recipe_path), 'blenderVersion': bpy.app.version_string,
        'sourceRestSha256': rest_sha,
        'textures': textures, 'lods': lod_checks, 'restRigUnchanged': True,
        'fourMaterialSlotsPreserved': True, 'rawFBXUnits': b['units'],
        'rawModelTransformsExactlyPreserved': len(a['models']),
        'fbxSha256': sha(target), 'fbxBytes': target.stat().st_size,
        'actualUnityFormatProof': 'PENDING; BC7/BC5/BC7 assertions must pass after scoped Root import',
        'sourceBlendUnchanged': True,
    }
    (OUT / 'verification/pot-authoring.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('[oral-POT] STAGED', json.dumps(receipt), flush=True)


if __name__ == '__main__':
    main()
