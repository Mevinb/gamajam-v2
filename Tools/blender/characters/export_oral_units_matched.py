"""Export a separate FBX candidate with the original catalog unit policy.

Reads frozen oral V2; writes only Logs/oral-unit-diagnostic-stage. No live edits.
"""
from pathlib import Path
import hashlib
import json
import bpy
from io_scene_fbx import parse_fbx

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Logs/oral-unit-diagnostic-stage'


def raw(path):
    tree,_=parse_fbx.parse(str(path));find=lambda e,k:next((x for x in e.elems if x.id==k),None)
    p=find(find(tree,b'GlobalSettings'),b'Properties70');units={e.props[0].decode():e.props[4:]for e in p.elems if e.id==b'P'and e.props[0]in [b'UnitScaleFactor',b'OriginalUnitScaleFactor']};models={}
    for e in find(tree,b'Objects').elems:
        if e.id!=b'Model':continue
        name=e.props[1].decode(errors='replace').split('\x00')[0];p=find(e,b'Properties70');values={x.props[0].decode():x.props[4:]for x in p.elems if x.id==b'P'}if p else{}
        models[name]={'scale':values.get('Lcl Scaling',[1.,1.,1.]),'translation':values.get('Lcl Translation',[0.,0.,0.]),'rotation':values.get('Lcl Rotation',[0.,0.,0.])}
    return {'units':units,'models':models}


def main():
    OUT.mkdir(parents=True,exist_ok=True);source=ROOT/'ArtSource/Characters/OralPilotV2/LakeGirl_OralPilotV2.blend';expected=json.loads(source.with_name('audit.json').read_text())['blend_sha256'];assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
    bpy.ops.wm.open_mainfile(filepath=str(source));bpy.context.scene.frame_set(1);rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');lods=[bpy.data.objects['LakeGirl_LOD'+str(i)]for i in range(3)]
    for o in list(bpy.context.selected_objects):o.select_set(False)
    rig.select_set(True)
    for o in lods:o.select_set(True)
    bpy.context.view_layer.objects.active=rig;target=OUT/'LakeGirl.units-matched.fbx';assert not target.exists(),'Keep the existing candidate immutable'
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,mesh_smooth_type='FACE',use_tspace=True,use_armature_deform_only=True,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=False)
    original=ROOT/'Logs/unity-oral-pilot-lakegirl-v2-stage/baseline/Assets/Parasite/Art/Characters/Models/LakeGirl.fbx';a=raw(original);b=raw(target);assert a['units']==b['units']=={'UnitScaleFactor':[100.],'OriginalUnitScaleFactor':[100.]}
    shared=set(a['models'])&set(b['models']);scale_delta=max(abs(x-y)for name in shared for x,y in zip(a['models'][name]['scale'],b['models'][name]['scale']));translation_delta=max(abs(x-y)for name in shared for x,y in zip(a['models'][name]['translation'],b['models'][name]['translation']));assert scale_delta<1e-5 and translation_delta<1e-5
    report={'scope':'separate export candidate; no live Asset/import/globalScale changes','sourceBlendSha256':expected,'candidate':str(target.relative_to(ROOT)),'candidateSha256':hashlib.sha256(target.read_bytes()).hexdigest(),'originalFbxSha256':hashlib.sha256(original.read_bytes()).hexdigest(),'originalRaw':a,'candidateRaw':b,'sharedModelCount':len(shared),'rawLocalScaleMaxDelta':scale_delta,'rawLocalTranslationMaxDelta':translation_delta,'unityTestExecuted':False};(OUT/'units-matched-export.json').write_text(json.dumps(report,indent=2)+'\n');print('[oral-unit] EXPORTED',json.dumps({k:report[k]for k in ['candidate','candidateSha256','sharedModelCount','rawLocalScaleMaxDelta','rawLocalTranslationMaxDelta','unityTestExecuted']}),flush=True)


if __name__=='__main__':main()
