"""Narrow camera-only revision; preserve every other packed camera, actor, shader and caption timing."""
import sys
sys.dont_write_bytecode=True
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import bpy,json,hashlib
import common as C
from plan import SHOTS,frame
out=C.SOURCE/'Opening';source=out/'OpeningFull.blend';bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene
shot=next(shot for shot in SHOTS if shot['id']=='04-disagreement')
cam=bpy.data.objects['Camera 04-disagreement'];focus=bpy.data.objects['Focus 04-disagreement']
unchanged={ob.name:ob.animation_data.action.name if ob.animation_data and ob.animation_data.action else None for ob in bpy.data.objects if ob not in (cam,focus)}
cam.animation_data_clear();cam.data.animation_data_clear();focus.animation_data_clear();cam.rotation_mode='QUATERNION';previous=None
for t,p,target in [(shot['start'],shot['eye'],shot['target']),(shot['end'],shot['end_eye'],shot['target'])]:
 cam.location=C.v(p);q=(C.v(target)-cam.location).to_track_quat('-Z','Y')
 if previous is not None and previous.dot(q)<0:q.negate()
 previous=q.copy();cam.rotation_quaternion=q;cam.keyframe_insert('location',frame=frame(t));cam.keyframe_insert('rotation_quaternion',frame=frame(t))
 focus.location=C.v(target);focus.keyframe_insert('location',frame=frame(t));cam.data.lens=shot['lens'];cam.data.keyframe_insert('lens',frame=frame(t))
cam.data.dof.focus_object=focus
for ob in bpy.data.objects:
 if ob.name in unchanged:
  current=ob.animation_data.action.name if ob.animation_data and ob.animation_data.action else None
  if current!=unchanged[ob.name]:raise RuntimeError('Camera revision changed another object action')
s.frame_set(1);bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
metadata=json.loads((out/'opening-timeline.json').read_text());metadata['shots']=SHOTS;(out/'opening-timeline.json').write_text(json.dumps(metadata,indent=2)+'\n')
print('[reframe04] Saved camera-only source',hashlib.sha256(source.read_bytes()).hexdigest(),flush=True)
C.cpu_preview(out/'Review/04-disagreement.png',21,cam,width=480,height=270,samples=8)
