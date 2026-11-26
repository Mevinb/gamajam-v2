"""Bounded MPFB eyelid correspondence probe, never touches frozen finale sources.

BLENDER_USER_RESOURCES=.../characters/blender_user nice -n 15 blender -b -t 2
  --python-exit-code 1 --python blink_pilot.py
Only exact native-neutral coordinate correspondences are accepted. No guessed face deformation.
"""
import bpy,hashlib,json,sys
from pathlib import Path
from mathutils import Vector,kdtree

ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'ArtSource/Characters/LakeBlinkPilot'
FINALS=ROOT/'ArtSource/RealisticFinales/Lake';SOURCE=FINALS/'Lake_Hold.blend'
MPFB=Path.home()/'Data/parasite-work/characters/blender_user/extensions/user_default/mpfb/data/targets/expression/units'
sys.path.insert(0,str(Path(__file__).resolve().parent))
import chartools as ct
import specs
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def coords(ob,rig,scale=1):
    bpy.context.view_layer.update();ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh()
    matrix=rig.matrix_world.inverted()@ob.matrix_world
    result=[(matrix@v.co)*scale for v in me.vertices];ev.to_mesh_clear();return result


def frozen_snapshot(hero,rig,scene):
    return {'vertices':[tuple(v.co) for v in hero.data.vertices],
        'weights':[[(g.group,g.weight) for g in v.groups] for v in hero.data.vertices],
        'faces':[(tuple(p.vertices),p.material_index) for p in hero.data.polygons],
        'uvs':[[tuple(v.uv) for v in layer.data] for layer in hero.data.uv_layers],
        'rest_rig':{b.name:[float(v) for row in b.matrix_local for v in row] for b in rig.data.bones},
        'pose_rig':{b.name:[float(v) for row in b.matrix_basis for v in row] for b in rig.pose.bones},
        'action':[(fc.data_path,fc.array_index,[(tuple(k.co),k.interpolation) for k in fc.keyframe_points]) for fc in rig.animation_data.action.fcurves],
        'camera':(scene.camera.name,[float(v) for row in scene.camera.matrix_world for v in row],scene.camera.data.lens,scene.camera.data.dof.aperture_fstop),
        'world':scene.world.name,'shape_keys':[k.name for k in hero.data.shape_keys.key_blocks] if hero.data.shape_keys else []}


def mapping(native,delta,hero,indices,matrix,tolerance=2e-6,near=None):
    tree=kdtree.KDTree(len(native))
    for i,p in enumerate(native):tree.insert(p,i)
    tree.balance();mapped={};unreliable=[];distances=[];candidates=0
    for i in indices:
        point=matrix@hero.data.vertices[i].co;location,index,distance=tree.find(point)
        if near is not None and distance>near:continue
        if delta[index].length<1e-7:continue
        candidates+=1;distances.append(distance)
        if distance>tolerance:unreliable.append({'hero_vertex':i,'native_vertex':index,'distance_metres':distance})
        else:mapped[i]=matrix.to_3x3().inverted()@delta[index]
    return mapped,{'candidates':candidates,'exact_mapped':len(mapped),'unreliable':len(unreliable),
        'max_correspondence_error_metres':max(distances,default=0),'samples':sorted(unreliable,key=lambda v:v['distance_metres'],reverse=True)[:15]}


def main():
    OUT.mkdir(parents=True,exist_ok=True);pins={str(p.relative_to(ROOT)):sha(p) for p in FINALS.glob('Lake_*.blend')}
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False,use_scripts=False);scene=bpy.context.scene;hero=bpy.data.objects['LakeGirl_LOD0'];rig=bpy.data.objects['Armature']
    scene.frame_set(scene.frame_current);bpy.context.view_layer.update();frozen_before=frozen_snapshot(hero,rig,scene)
    existing=[k.name for k in hero.data.shape_keys.key_blocks] if hero.data.shape_keys else []
    groups=[b.name for b in rig.data.bones if any(s in b.name.lower() for s in ('lid','blink'))]
    source_matrix=rig.matrix_world.inverted()@hero.matrix_world
    originals={o.as_pointer() for o in bpy.data.objects}
    H,_=ct.mpfb_services();info=H._create_default_human_info_dict();spec=specs.get('LakeGirl');info['name']='Blink native correspondence source';info['rig']='mixamo_unity'
    for key in ('gender','age','muscle','weight','height','proportions'):
        if key in spec:info['phenotype'][key]=spec[key]
    info['phenotype']['race']={r:spec['race'].get(r,0) for r in ('asian','caucasian','african')}
    info['eyelashes']=ct.mhclo_rel('eyelashes','eyelashes02');settings=H.get_default_deserialization_settings();settings['subdiv_levels']=0
    body=H.deserialize_from_dict(info,settings);temp_rig=body.parent
    lashes=next(o for o in bpy.data.objects if o.as_pointer() not in originals and o.get('MPFB_GEN_object_type')=='Eyelashes')
    native_head=temp_rig.data.bones.get('mixamorig:Head') or temp_rig.data.bones.get('head')
    assert native_head,'Native head bone unavailable';scale=rig.data.bones['Head'].length/native_head.length
    rest_error=max((rig.data.bones['Head'].head_local-native_head.head_local*scale).length,(rig.data.bones['Head'].tail_local-native_head.tail_local*scale).length)
    assert rest_error<1e-5,('Native phenotype/rest alignment failed',rest_error)
    neutral=coords(body,temp_rig,scale);lash_neutral=coords(lashes,temp_rig,scale)
    from bl_ext.user_default.mpfb.services.targetservice import TargetService
    from bl_ext.user_default.mpfb.services.clothesservice import ClothesService
    targets=[]
    for race,weight in [('caucasian',.9),('asian',.1)]:
        for eye in ('left','right'):
            path=MPFB/race/('eye-'+eye+'-closure.target.gz');key=TargetService.load_target(body,str(path),weight=weight,name='Pilot_'+race+'_'+eye)
            targets.append({'path':str(path),'sha256':sha(path),'weight':weight,'key':key.name})
    closed=coords(body,temp_rig,scale);ClothesService.fit_clothes_to_human(lashes,body,set_parent=False);lash_closed=coords(lashes,temp_rig,scale)
    delta=[b-a for a,b in zip(neutral,closed)];lash_delta=[b-a for a,b in zip(lash_neutral,lash_closed)]
    skin_indices={i for p in hero.data.polygons if p.material_index==0 for i in p.vertices}
    hair_indices={i for p in hero.data.polygons if p.material_index==2 for i in p.vertices}
    skin_map,skin_report=mapping(neutral,delta,hero,skin_indices,source_matrix)
    lash_map,lash_report=mapping(lash_neutral,lash_delta,hero,hair_indices,source_matrix,near=.002)
    report={'status':'PROBED_ONLY','scope':'One isolated blink pilot; four frozen finale files unchanged','source':str(SOURCE.relative_to(ROOT)),
        'source_sha256':pins[str(SOURCE.relative_to(ROOT))],'original_vertex_count':len(hero.data.vertices),'existing_shape_keys':existing,
        'existing_eyelid_bones':groups,'native_scale':scale,'native_head_rest_error_metres':rest_error,'targets':targets,
        'skin_correspondence':skin_report,'lash_correspondence':lash_report,'required_exact_tolerance_metres':2e-6,
        'eyes_globes_excluded':True,'geometry_guessed':False,'finale_source_pins':pins}
    reliable=skin_report['candidates']>20 and lash_report['candidates']>10 and skin_report['unreliable']==0 and lash_report['unreliable']==0
    report['reliable_exact_transfer']=reliable
    # Bound the investigation: no interpolation, arbitrary clamping, extra surface or guessed lid fallback.
    if not reliable:
        report['status']='NO_PILOT_UNRELIABLE_DECIMATED_CORRESPONDENCE';report['reason']='Native MPFB closure deltas cannot be transferred by exact coordinates to every affected skin/lash candidate; no shape key was added.'
    else:
        report['status']='EXACT_TRANSFER_FEASIBLE_PROBE_ONLY'
    assert frozen_before==frozen_snapshot(hero,rig,scene),'Frozen hero or rig/camera/world changed in memory'
    report['exact_preservation']={'all_11769_base_vertices':True,'all_vertex_weights':True,'all_faces_and_material_assignments':True,
        'all_uv_coordinates':True,'all_rest_and_pose_rig_matrices':True,'all_rig_action_curves':True,'camera_and_world':True,
        'new_shape_keys_added':0,'outside_eye_region_unchanged':True,'eye_globes_unchanged':True}
    assert pins=={str(p.relative_to(ROOT)):sha(p) for p in FINALS.glob('Lake_*.blend')}
    (OUT/'correspondence-probe.json').write_text(json.dumps(report,indent=2)+'\n')
    print('[LakeBlinkPilot]',report['status'],json.dumps({'skin':skin_report,'lashes':lash_report}),flush=True)


if __name__=='__main__':main()
