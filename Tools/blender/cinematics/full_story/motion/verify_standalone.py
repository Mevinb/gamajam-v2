"""Fresh-process compact-source and baked-action replay checks, CPU only."""
import sys
sys.dont_write_bytecode=True
import json,math,time,hashlib
from pathlib import Path
import bpy
HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[4]
OUT=PROJECT/'ArtSource/FullCinematics/Motion'
sys.path.insert(0,str(HERE.parent))
from motion import MotionLibrary
bpy.ops.wm.read_factory_settings(use_empty=True)
lib=MotionLibrary(PROJECT)
assert lib.source_path==OUT/'UAL_story_source.blend'
report={'source_path':str(lib.source_path),'actors':{},'baked_actions':{},'cpu_only':True}
for actor in ['DrPetrov','DrVale','MaintEli','LakeGirl']:
    with bpy.data.libraries.load(str(Path.home()/f'Data/parasite-work/characters/out/{actor}.blend'),link=False) as (src,dst):
        dst.objects=['Armature']
    arm=dst.objects[0]
    arm.name=actor+'_Standalone'
    bpy.context.scene.collection.objects.link(arm)
    maxerror=0.0
    for clip in lib.available_clip_names:
        duration=lib.clip_info(clip)['seconds']
        for t in [0,min(.37,duration),duration]:
            world=lib.apply_pose(arm,clip,t)
            bpy.context.view_layer.update()
            error=max(abs(m[r][c]-arm.pose.bones[n].matrix[r][c]) for n,m in world.items() for r in range(4) for c in range(4))
            maxerror=max(error,maxerror)
            assert error<2e-4,(actor,clip,t,error)
            assert all(math.isfinite(v) for mat in world.values() for row in mat for v in row)
        duration=lib.clip_info(clip)['seconds']
        a=lib.pose_for(arm,clip,.123 if lib.clip_info(clip)['loop'] else duration)
        b=lib.pose_for(arm,clip,duration+.123 if lib.clip_info(clip)['loop'] else duration+100)
        assert max(abs(abs(a[n][0].dot(b[n][0]))-1) for n in a)<1e-5
        assert max((a[n][1]-b[n][1]).length for n in a)<1e-5
    report['actors'][actor]={'clips':len(lib.available_clip_names),'max_blender_fk_error':maxerror}
with bpy.data.libraries.load(str(OUT/'Actors_story_actions.blend'),link=False) as (src,dst):
    objectnames=src.objects[:]
    actionnames=src.actions[:]
    dst.objects=objectnames[:]
    dst.actions=actionnames[:]
actions=dict(zip(actionnames,dst.actions))
for arm in dst.objects:
    assert arm.type=='ARMATURE'
    bpy.context.scene.collection.objects.link(arm)
    actor=arm.name.split('_MotionRig')[0]
    for actionname,action in actions.items():
        if not actionname.startswith(actor+'__'):continue
        clip=actionname.split('__',1)[1]
        expected=lib._retarget(arm,clip,16/30.0,False)[1]
        arm.animation_data_create()
        arm.animation_data.action=action
        arm.animation_data.action_slot=action.slots[0]
        bpy.context.scene.frame_set(17)
        error=max(abs(m[r][c]-arm.pose.bones[n].matrix[r][c]) for n,m in expected.items() for r in range(4) for c in range(4))
        assert error<3e-4,(actionname,error,'baked replay')
        report['baked_actions'][actionname]={'frame':17,'max_blender_fk_error':error}
        arm.animation_data.action=None
# Approximate parent film load: 3 actors x 1416 24fps samples, entirely CPU FK.
arms=[o for o in bpy.data.objects if o.type=='ARMATURE' and o.name.endswith('_Standalone')][:3]
start=time.perf_counter()
for frame in range(1416):
    for i,arm in enumerate(arms):
        clip=['Idle_Neutral','Console_Loop','Talk_Loop'][i]
        lib.apply_pose(arm,clip,frame/24.0)
report['performance']={'actor_frames':1416*len(arms),'seconds':round(time.perf_counter()-start,4),
                       'method':'apply_pose, no depsgraph or key insertion; 3 real actors at 24fps'}
report['sha256']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob('*.blend')}
(OUT/'standalone_verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('[motion] STANDALONE PASS',json.dumps(report),flush=True)
