"""Build editable, compact motion libraries; CPU-only numeric/pose verification.

blender -b --factory-startup --python Tools/blender/cinematics/full_story/motion/build_library.py
"""
import sys
sys.dont_write_bytecode = True
import json, math, time
from pathlib import Path
import bpy
from mathutils import Matrix, Vector
HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[4]
sys.path.insert(0, str(HERE.parent))
from motion.library import MotionLibrary

CLIPS = ['Idle_Neutral','Walk','Point_Loop','Gesture_Explain','Gesture_Shrug','Talk_Loop',
         'Console_Loop','Collapse','Unconscious_Loop']
OUT = PROJECT / 'ArtSource/FullCinematics/Motion'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = Path.home() / 'Data/parasite-work/animation/dev.blend'
if not SOURCE.exists():
    SOURCE = Path.home() / 'Data/parasite-work/animation/build.blend'
bpy.ops.wm.read_factory_settings(use_empty=True)
lib = MotionLibrary(PROJECT, SOURCE)
manifest_path = PROJECT / 'Tools/blender/animation/manifest.json'
(OUT / 'manifest.json').write_text(manifest_path.read_text())
bpy.data.libraries.write(str(OUT / 'UAL_story_source.blend'), lib.source_datablocks(),
                         path_remap='RELATIVE_ALL', fake_user=True, compress=True)
print('[motion] source',len(lib.available_clip_names),'clips',flush=True)


def load_actor(name):
    path = Path.home() / f'Data/parasite-work/characters/out/{name}.blend'
    with bpy.data.libraries.load(str(path), link=False) as (src,dst):
        dst.objects = [n for n in src.objects if n == 'Armature' or n == name + '_LOD0']
    arm = next(o for o in dst.objects if o.type == 'ARMATURE')
    mesh = next(o for o in dst.objects if o.type == 'MESH')
    arm.name = name + '_MotionRig'
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.scene.collection.objects.link(mesh)
    return arm,mesh,path


def bake(arm, name, poses):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = action
    slot = action.slots.new(id_type='OBJECT',name=arm.name)
    arm.animation_data.action_slot = slot
    frames = [float(i + 1) for i in range(len(poses))]
    for bone in arm.pose.bones:
        bn = bone.name
        quats = []
        for pose in poses:
            q = pose[bn][0].copy()
            if quats and q.dot(quats[-1]) < 0:q.negate()
            quats.append(q)
        paths = [('rotation_quaternion',quats,4)]
        if any(p[bn][1].length > 1e-8 for p in poses):
            paths.append(('location',[p[bn][1] for p in poses],3))
        for prop, values, size in paths:
            for index in range(size):
                curve = action.fcurve_ensure_for_datablock(arm,f'pose.bones["{bn}"].{prop}',index=index)
                curve.keyframe_points.add(len(poses))
                curve.keyframe_points.foreach_set('co',[v for fr,value in zip(frames,values) for v in (fr,value[index])])
                curve.keyframe_points.foreach_set('interpolation',[1]*len(poses))
                curve.update()
    action.frame_range=(1,len(poses))
    action.use_frame_range=True
    return action


def difference(a,b):
    return max(abs(a[r][c]-b[r][c]) for r in range(4) for c in range(4))

report = {'blender':bpy.app.version_string,'fps':30,'method':'armature-space world-rotation delta; target rest proportions',
          'source_blend':str(SOURCE),'available_clips':list(lib.available_clip_names),'actors':{},'pose_diagrams':[],
          'root_motion':'false strips source root only; preserves pelvis sway/crouch/collapse; in-place Walk remains in-place',
          'verification':'CPU-only numeric transforms and 2-view pose diagrams; no GPU renderer or Unity started'}
pose_diagrams = []
write_blocks = set()
t0=time.time()
for actor in ['DrPetrov','MaintEli']:
    arm,mesh,path=load_actor(actor)
    rig=lib._target(arm)
    actor_report={'source':str(path),'mapped_bones':len(rig.mapping),'target_bones':len(rig.order),'leg_scale':rig.scale,
                  'clips':{},'max_limb_length_error_m':0.0,'max_blender_fk_error':0.0}
    links=[(side+a,side+b) for side in ['Left','Right'] for a,b in
           [('UpperArm','LowerArm'),('LowerArm','Hand'),('UpperLeg','LowerLeg'),('LowerLeg','Foot')]]
    rest_lengths={(a,b):(rig.rest[b].translation-rig.rest[a].translation).length for a,b in links}
    for clip in CLIPS:
        info=lib.clip_info(clip)
        duration=info['seconds']
        count=int(round(duration*30))+1
        poses=[]
        for i in range(count):
            # Nonloop endpoint must be sampled exactly; loops bake the repeated first frame at the end.
            pose,world=lib._retarget(arm,clip,i/30.0,False)
            assert all(math.isfinite(v) for q,l in pose.values() for v in [*q,*l]),(actor,clip,i,'nonfinite pose')
            assert all(math.isfinite(v) for mat in world.values() for row in mat for v in row),(actor,clip,i,'nonfinite FK')
            error=max(abs((world[b].translation-world[a].translation).length-rest_lengths[a,b]) for a,b in links)
            actor_report['max_limb_length_error_m']=max(actor_report['max_limb_length_error_m'],error)
            poses.append(pose)
        action=bake(arm,actor+'__'+clip,poses)
        write_blocks.add(action)
        actor_report['clips'][clip]={'frames':count,'seconds':duration,'loop':info['loop'],'action':action.name}
        sample_time=duration if clip=='Collapse' else min(.8,duration*.4)
        arm.animation_data.action=None
        world=lib.apply_pose(arm,clip,sample_time)
        bpy.context.view_layer.update()
        error=max(difference(world[n],arm.pose.bones[n].matrix) for n in world)
        actor_report['max_blender_fk_error']=max(actor_report['max_blender_fk_error'],error)
        assert error<2e-4,(actor,clip,error,'FK does not match Blender bone matrix')
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        geometry=evaluated.to_mesh()
        assert all(math.isfinite(v) for vertex in geometry.vertices for v in vertex.co)
        mins=[min(vertex.co[i] for vertex in geometry.vertices) for i in range(3)]
        maxs=[max(vertex.co[i] for vertex in geometry.vertices) for i in range(3)]
        actor_report['clips'][clip]['deformed_mesh_bounds_m']={'min':mins,'max':maxs}
        actor_report['clips'][clip]['ground_offset_m']=max(0.0,-mins[2])
        evaluated.to_mesh_clear()
        pose_diagrams.append({'actor':actor,'clip':clip,'seconds':sample_time,
                              'bones':{n:{'head':list(world[n].translation),
                                          'tail':list(world[n]@Vector((0,arm.data.bones[n].length,0))),
                                          'parent':rig.parents[n]} for n in world}})
        if clip in ['Idle_Neutral','Collapse','Unconscious_Loop']:
            actor_report['clips'][clip]['head_height_m']=world['Head'].translation.z
            actor_report['clips'][clip]['hips_height_m']=world['Hips'].translation.z
            actor_report['clips'][clip]['torso_up_axis']=list((world['Head'].translation-world['Hips'].translation).normalized())
        print('[motion]',actor,clip,count,'frames FKerror',round(error,8),flush=True)
    assert actor_report['max_limb_length_error_m']<1e-4
    assert actor_report['clips']['Idle_Neutral']['head_height_m']>1.3
    assert actor_report['clips']['Collapse']['head_height_m']<.5
    assert actor_report['clips']['Unconscious_Loop']['head_height_m']<.5
    # Applying an identical armature-space pose under a placed/scaled object cannot move its object.
    reference=lib.apply_pose(arm,'Point_Loop',.75)
    placed=Matrix.Translation((7,-3,2))@Matrix.Rotation(.43,4,'Z')@Matrix.Scale(.8,4)
    arm.matrix_world=placed
    relocated=lib.apply_pose(arm,'Point_Loop',.75)
    assert difference(placed,arm.matrix_world)<1e-6
    assert max(difference(reference[n],relocated[n]) for n in reference)<1e-6
    arm.matrix_world=Matrix.Identity(4)
    # Authoritative loop and clamp behavior at arbitrary cinematic times.
    for clip in CLIPS:
        duration=lib.clip_info(clip)['seconds']
        if lib.clip_info(clip)['loop']:
            a=lib.pose_for(arm,clip,.123); b=lib.pose_for(arm,clip,duration+.123)
        else:
            a=lib.pose_for(arm,clip,duration); b=lib.pose_for(arm,clip,duration+100)
        assert max(abs(abs(a[n][0].dot(b[n][0]))-1) for n in a)<1e-5
        assert max((a[n][1]-b[n][1]).length for n in a)<1e-5
    arm.animation_data_clear()
    lib.apply_pose(arm,'Idle_Neutral',0)
    write_blocks.add(arm)
    report['actors'][actor]=actor_report
bpy.data.libraries.write(str(OUT/'Actors_story_actions.blend'),write_blocks,
                         path_remap='RELATIVE_ALL',fake_user=True,compress=True)
(OUT/'pose_diagrams.json').write_text(json.dumps(pose_diagrams,indent=1))
report['elapsed_seconds']=round(time.time()-t0,2)
report['files']={p.name:p.stat().st_size for p in OUT.glob('*.blend')}
assert all(size<100*1024*1024 for size in report['files'].values())
(OUT/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('[motion] PASS',json.dumps(report['files']),'seconds',report['elapsed_seconds'],flush=True)
