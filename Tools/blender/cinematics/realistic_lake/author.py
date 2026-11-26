"""Build new packed realistic lake films. Never renders on invocation.

nice -n 15 blender -b -t 2 --python-exit-code 1 --python author.py -- build
nice -n 15 blender -b -t 2 --python-exit-code 1 --python author.py -- audit
"""
import argparse
import hashlib
import json
import math
import sys
import time
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
OUT=ROOT/'ArtSource/RealisticFinales/Lake'
OLD=ROOT/'ArtSource/FullCinematics/Lake'
sys.path.insert(0,str(HERE));sys.path.insert(0,str(ROOT/'Tools/blender/characters'))
import scenery
import portrait
from cinematic_surfaces import upgrade_character

FILMS={'arrival':360,'hold':144,'peace':168,'dominion':168}
QA={'arrival':[1,100,165,280,360],'hold':[1,73,144], 'peace':[1,62,103,168], 'dominion':[1,52,94,155]}
HOLD=(-.45,-2.65,1.32)


def snapshot(rig,length):
    scene=bpy.context.scene;result={}
    for f in sorted(set([1,length//2,length,length+1])):
        scene.frame_set(f);result[str(f)]={b.name:[float(x) for row in b.matrix_basis for x in row] for b in rig.pose.bones}
    scene.frame_set(1);return result


def configure(scene,film,length):
    scene.name='Realistic lake '+film
    scene.render.engine='CYCLES';scene.cycles.device='GPU';scene.cycles.samples=192;scene.cycles.use_adaptive_sampling=True;scene.cycles.adaptive_threshold=.008;scene.cycles.use_denoising=True;scene.cycles.denoiser='OPENIMAGEDENOISE';scene.cycles.max_bounces=10;scene.cycles.transmission_bounces=8;scene.cycles.transparent_max_bounces=12
    scene.render.use_motion_blur=True;scene.render.motion_blur_shutter=.5
    scene.render.use_compositing=False
    scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100;scene.render.fps=24;scene.render.fps_base=1;scene.frame_start=1;scene.frame_end=length
    scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.use_persistent_data=True
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB';scene.render.image_settings.color_depth='8';scene.render.film_transparent=False
    scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=0.;scene.view_settings.gamma=1.
    scene.render.filepath=str(ROOT/'Logs/realistic-lake-qa'/film/'frame-')
    scene['film']=film;scene['duration_seconds']=length/24;scene['explicit_choice_boundary']='neutral' if film in ('arrival','hold') else 'ONLY after deliberate '+film.upper();scene['loop_period_frames']=144 if film=='hold' else 0


def cameras(scene,focus,film,length,eye,bench_drop=0.0):
    # Portrait aim is just below the eyes; focus is the actual eye-bone depth.
    target=eye+Vector((0,0,-.12))
    shots={
     'arrival':[(1,120,'Arrival - a photographed human shore',(3.8,8.5,1.85),(2.4,5.8,1.66),(-1.7,-5,.82),(-1.7,-3,.88),28,5.6),
                (121,240,'Arrival - quiet face at the water',(-3.25,-2.25,1.32),(-3.0,-2.03,1.30),target,target,60,3.5),
                (241,360,'Arrival - approach deliberate choice',(-.35,-3.05,1.38),HOLD,target,target,60,3.5)],
     'hold':[(1,length,'Choice - ordinary eyes and patient breathing',HOLD,HOLD,target,target,60,3.5)],
     'peace':[(1,72,'Peace - breath releases',HOLD,(-.68,-2.50,1.30),target,target,60,3.5),
              (73,120,'Peace - relaxed hand detail',(-1.08,-1.65,1.08),(-1.18,-1.56,1.08),(-1.76,-.23,.72),(-1.76,-.23,.72),65,4.0),
              (121,168,'Peace - leave her human on the shore',(2.4,5.8,2.25),(4.0,10.2,3.0),(-1.7,-4,.85),(-1.7,-6,.75),32,5.6)],
     'dominion':[(1,60,'Dominion - chosen iris reveal',HOLD,(-1.18,-1.88,1.28),target,eye+Vector((0,0,-.04)),65,3.5),
                 (61,108,'Dominion - deliberate hand curl',(-2.45,-1.65,1.06),(-2.35,-1.52,1.05),(-1.76,-.23,.73),(-1.76,-.23,.73),65,4.0),
                 (109,168,'Dominion - quiet altered gaze',(-2.96,-2.26,1.33),(-2.70,-2.07,1.30),target,target,60,3.5)]}
    result=[]
    for first,last,name,p,q,t,u,lens,fstop in shots[film]:
        if 'hand' in name:
            t=Vector(t)-Vector((0,0,bench_drop));u=Vector(u)-Vector((0,0,bench_drop))
        cam=portrait.camera(scene,name,p,q,(t,u),lens,(first,last),focus,fstop)
        # A hand detail focuses on the hand, rather than mistakenly on the face.
        if 'hand' in name:
            pin=bpy.data.objects.new(name+' focus',None);scene.collection.objects.link(pin);pin.location=t;cam.data.dof.focus_object=pin
        result.append({'first':first,'last':last,'camera':name,'lens_mm':lens,'fstop':fstop})
    return result


def build(film):
    length=FILMS[film];source=OLD/('Lake_'+film.title()+'.blend');original_hash=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
    rig=bpy.data.objects['Armature'];before=snapshot(rig,length);hero=bpy.data.objects['LakeGirl_LOD0'];source_vertex_count=len(hero.data.vertices);source_faces=len(hero.data.polygons)
    scene.timeline_markers.clear()
    for ob in list(bpy.data.objects):
        if ob.type=='CAMERA':bpy.data.objects.remove(ob,do_unlink=True)
    # The chosen source's emissive eye slot is replaced and recreated explicitly;
    # no old eye emission/green sunlight/42-heart bank can leak into new films.
    if film=='dominion':
        hero.data.materials[1]=bpy.data.materials.get('Eyes') or hero.data.materials[1]
        if not hero.data.materials[1].name.startswith('Eyes'):hero.data.materials[1].name='Eyes.OriginalChosenSource'
    materials=upgrade_character(hero,'LakeGirl',ROOT)
    eye_uv_loops=portrait.refine_eye_uv(hero);portrait.refine_hair_cap(materials['Hair']);support=portrait.foot_support(hero,rig)
    for ob in bpy.data.objects:
        if ob.name.startswith('LakeGirl_LOD'):ob.hide_render=ob!=hero
    scene.frame_set(1);eye=portrait.eye_position(rig)
    subdiv=hero.modifiers.new('Cinematic render subdivision - original armature retained','SUBSURF');subdiv.subdivision_type='CATMULL_CLARK';subdiv.levels=2;subdiv.render_levels=2;subdiv.uv_smooth='PRESERVE_BOUNDARIES'
    bpy.context.view_layer.update();hair=portrait.hair_fibres(hero,rig);subdiv.levels=1
    scenery.rebuild(ROOT,length);configure(scene,film,length)
    focus=portrait.focus_pin(scene,rig,length);shotlist=cameras(scene,focus,film,length,eye,support['bench_and_actor_lowered_m'])
    if film=='dominion':portrait.dominion_iris(materials['Eyes'],length)
    assert snapshot(rig,length)==before,'Original rig animation changed'
    assert (len(hero.data.vertices),len(hero.data.polygons))==(source_vertex_count,source_faces)
    scene['original_armature_animation_sha256']=hashlib.sha256(json.dumps(before,sort_keys=True).encode()).hexdigest()
    scene['original_packed_source']=str(source.relative_to(ROOT));scene['original_packed_source_sha256']=original_hash
    scene['realism_version']=4;scene['gpu_qa_frames']=json.dumps(QA[film]);scene['scenery_seed']='8157/47291/9641';scene['head_pinned_hair_strands']=int(hair['strand_count']);scene['head_pinned_flyaways']=int(hair['flyaway_count']);scene['boot_ground_support']=json.dumps(support)
    # Drop old zero-user material/image libraries before packing each standalone.
    for _ in range(2):bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
    scene.frame_set(1);bpy.context.preferences.filepaths.save_version=0;bpy.ops.file.pack_all();path=OUT/('Lake_'+film.title()+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    assert hashlib.sha256(source.read_bytes()).hexdigest()==original_hash,'Read-only source changed'
    receipt={'version':4,'film':film,'path':str(path.relative_to(ROOT)),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'source_sha256':original_hash,'original_rig_animation_preserved':True,'original_geometry_counts_preserved':[source_vertex_count,source_faces],'cinematic_eye_uv_loops_refined':eye_uv_loops,'iris_uv_central_scale':1.42,'boot_ground_support':support,'hair_strands':int(hair['strand_count']),'flyaways':int(hair['flyaway_count']),'native_hair_primitives':True,'groom_conforms_to_render_subdivision':2,'guided_part_side_strands':int(hair['guided_part_side_strands']),'face_framing_strands':int(hair['face_framing_strands']),'render_subdivision':2,'eye_position_world':list(eye),'shots':shotlist,'gpu_qa_frames':QA[film],'saved_samples':192,'rendered':False}
    receipt['authoring_source_sha256']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [HERE/'author.py',HERE/'portrait.py',HERE/'scenery.py',ROOT/'Tools/blender/characters/cinematic_surfaces.py']}
    (OUT/(film+'-authoring.json')).write_text(json.dumps(receipt,indent=2)+'\n');print('[realistic-lake] AUTHORED',film,receipt['bytes'],flush=True)


def audit():
    result={};total=0
    for film,length in FILMS.items():
        path=OUT/('Lake_'+film.title()+'.blend');bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene;rig=bpy.data.objects['Armature'];hero=bpy.data.objects['LakeGirl_LOD0'];pack=[i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file]
        assert not pack,pack
        assert (s.frame_end,s.render.fps,s.render.resolution_x,s.render.resolution_y)==(length,24,1920,1080)
        assert s.cycles.samples==192 and s.cycles.max_bounces==10 and s.cycles.transmission_bounces==8
        assert not any(o.name.startswith(('Dominion heartbeat','Distant person')) for o in bpy.data.objects)
        assert not bpy.data.objects.get('Distant warm sun')
        assert hero.modifiers[-1].type=='SUBSURF' and hero.modifiers[-1].render_levels==2
        assert s['realism_version']==4 and hero['cinematic_iris_uv_central_scale']==1.42
        hair=bpy.data.objects['Head-pinned fine hair and restrained flyaways'];assert hair.type=='CURVES' and 25000<len(hair.data.curves)<=32000 and hair['groom_conforms_to_render_subdivision']==2
        assert min(p.radius for p in hair.data.points)>0 and max(p.radius for p in hair.data.points)<.000070
        skin=next(m for m in hero.data.materials if m.name.startswith('Skin.'));bs=skin.node_tree.nodes.get('Principled BSDF');assert abs(bs.inputs['Subsurface Scale'].default_value-.00125)<1e-7
        eye=next(m for m in hero.data.materials if m.name.startswith('Eyes.'));chosen=any(n.name=='Explicit Dominion reveal only' for n in eye.node_tree.nodes);assert chosen==(film=='dominion')
        after=snapshot(rig,length);assert hashlib.sha256(json.dumps(after,sort_keys=True).encode()).hexdigest()==s['original_armature_animation_sha256']
        loop_delta=0.;environment_delta=0.
        if film=='hold':
            s.frame_set(1);first={b.name:b.matrix_basis.copy() for b in rig.pose.bones};water=bpy.data.materials['Calm dielectric lake IOR 1.333'];noise=next(n for n in water.node_tree.nodes if n.type=='TEX_NOISE' and n.noise_dimensions=='4D');a=noise.inputs['W'].default_value;grass=bpy.data.objects['Irregular low grass and seedling tufts'].data.shape_keys.key_blocks['Six second shore breeze'];g=grass.value;focus=bpy.data.objects['Eye focus pin - measured original eye bones'].location.copy()
            s.frame_set(145);loop_delta=max(abs(b.matrix_basis[i][j]-first[b.name][i][j]) for b in rig.pose.bones for i in range(4) for j in range(4));environment_delta=max(abs(noise.inputs['W'].default_value-a),abs(grass.value-g),(bpy.data.objects['Eye focus pin - measured original eye bones'].location-focus).length);assert max(loop_delta,environment_delta)<1e-5
        s.frame_set(1);subdiv=hero.modifiers[-1];subdiv.show_viewport=False;bpy.context.view_layer.update();evaluated=hero.evaluated_get(bpy.context.evaluated_depsgraph_get());body=evaluated.to_mesh()
        ground=bpy.data.objects['Rolling shore and visible shallow lake bed'];tree=BVHTree.FromPolygons([ground.matrix_world@v.co for v in ground.data.vertices],[list(p.vertices) for p in ground.data.polygons]);gaps=[]
        try:
            ids={v.index for v in hero.data.vertices if any(hero.vertex_groups[g.group].name in ('LeftFoot','RightFoot','LeftToes','RightToes') and g.weight>.25 for g in v.groups)}
            for index in ids:
                p=evaluated.matrix_world@body.vertices[index].co;hit,_,_,_=tree.ray_cast(p+Vector((0,0,.5)),Vector((0,0,-1)),1)
                if hit is not None:gaps.append(p.z-hit.z)
            assert gaps and -.005<min(gaps)<.020,(film,'feet do not meet actual terrain',min(gaps) if gaps else None)
        finally:evaluated.to_mesh_clear();subdiv.show_viewport=True
        total+=path.stat().st_size;result[film]={'version':4,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'all_images_packed':True,'rig_animation_preserved':True,'hold_rig_delta':loop_delta,'hold_environment_delta':environment_delta,'minimum_boot_clearance_to_actual_terrain_m':min(gaps),'hair_strands':len(hair.data.curves),'native_hair_primitives':True,'groom_conforms_to_render_subdivision':2,'iris_uv_central_scale':1.42,'scenery':{k:s[k] for k in s.keys() if k.startswith('realism_') and k!='realism_version'},'objects':len(s.objects),'qa_frames':QA[film],'rendered':False}
    assert total<400_000_000,total
    report={'total_blend_bytes':total,'films':result,'verification_scope':'CPU authoring/data and packing only; GPU image quality remains pending'};(OUT/'audit.json').write_text(json.dumps(report,indent=2)+'\n');print('[realistic-lake] AUDIT PASS',total,flush=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['build','audit']);parser.add_argument('--film',choices=list(FILMS));args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);OUT.mkdir(parents=True,exist_ok=True)
    if args.mode=='build':
        for film in ([args.film] if args.film else FILMS):build(film)
        if not args.film:audit()
    else:audit()


if __name__=='__main__':main()
