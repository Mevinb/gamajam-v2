"""Fresh-process CPU checks of the complete authored opening and actual evaluated actor poses."""
import sys
sys.dont_write_bytecode=True
import bpy,json,math,hashlib
from pathlib import Path
from bpy_extras.object_utils import world_to_camera_view
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
from plan import SHOTS,CAPTIONS,frame
ROOT=HERE.parents[3];OUT=ROOT/'ArtSource/FullCinematics/Opening';source=OUT/'OpeningFull.blend'
def main():
 bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene
 assert s.frame_start==1 and s.frame_end==1920 and s.render.fps==24
 assert (s.render.resolution_x,s.render.resolution_y)==(1920,1080)
 assert len(s.timeline_markers)==11
 for shot in SHOTS:
  marker=next(m for m in s.timeline_markers if m.name==shot['id']);assert marker.frame==frame(shot['start']);assert marker.camera.name=='Camera '+shot['id']
 images=[]
 for im in bpy.data.images:
  if im.users and im.source=='FILE':assert im.packed_file or len(im.packed_files),im.name;images.append(im.name)
 # The actor atlas must retain its real skin/eyes/hair/clothing slots, not flatten every face to skin.
 for actor in ['Dr Hana Petrov','Dr Vale','Contact volunteer','Unaware pedestrian']:
  mesh=bpy.data.objects[actor+' Hero'];assert len({p.material_index for p in mesh.data.polygons})>=4,actor+' lost material slots'
 records=[]
 for sec,actor in [(7,'Dr Hana Petrov'),(16.5,'Dr Hana Petrov'),(21,'Dr Vale'),(35.2,'Contact volunteer'),(39,'Contact volunteer'),(39,'Dr Hana Petrov'),(45,'Dr Hana Petrov'),(52.4,'Dr Vale')]:
  s.frame_set(frame(sec));rig=bpy.data.objects[actor+' Rig'];head=rig.matrix_world@rig.pose.bones['Head'].matrix.translation;wrist=rig.matrix_world@rig.pose.bones['RightHand'].matrix.translation
  record={'seconds':sec,'actor':actor,'head_unity':[head.x,head.z,-head.y],'right_wrist_unity':[wrist.x,wrist.z,-wrist.y]};records.append(record)
  if sec==39 and actor=='Contact volunteer':assert head.z<.6,'Worker must visibly lie down after the contact collapse'
  if sec==7:assert abs(wrist.x+.52)<.1 and abs(wrist.z-1.4)<.1,'Petrov must physically reach the glass'
 s.frame_set(frame(39));helper=bpy.data.objects['Dr Hana Petrov Rig'];patient=bpy.data.objects['Contact volunteer Rig']
 help_hand=helper.matrix_world@helper.pose.bones['RightHand'].matrix.translation;patient_chest=patient.matrix_world@patient.pose.bones['UpperChest'].matrix.translation
 care_error=(help_hand-patient_chest).length
 print('[care-probe]',{name:tuple(helper.matrix_world@helper.pose.bones[name].matrix.translation) for name in ['Hips','UpperChest','Head','RightUpperArm','RightLowerArm','RightHand']},'patient_chest',tuple(patient_chest),flush=True)
 assert care_error<.12,f'Petrov must physically reach the collapsed patient, error={care_error}'
 s.frame_set(frame(16.5));rig=bpy.data.objects['Dr Hana Petrov Rig'];cam=bpy.data.objects['Camera 03-recognition'];target=rig.matrix_world@rig.pose.bones['Head'].matrix.translation
 hit,point,normal,index,ob,matrix=s.ray_cast(bpy.context.evaluated_depsgraph_get(),cam.matrix_world.translation,(target-cam.matrix_world.translation).normalized(),distance=(target-cam.matrix_world.translation).length)
 assert hit and ob.name.startswith('Dr Hana Petrov Hero'),'Recognition face is blocked by set geometry'
 escape=[]
 for sec in [57.25,57.5,57.75,58.0]:
  s.frame_set(frame(sec));culture=bpy.data.objects['Specimen07'];extent=max(.244*culture.scale.x,.204*culture.scale.z)
  assert extent<.17,'Culture must squeeze inside the actual port radius'
  assert abs(culture.matrix_world.translation.z-.80)<.01,'Culture must align vertically with the release aperture'
  escape.append({'seconds':sec,'radial_extent_m':extent,'port_radius_m':.17,'volume_scale':culture.scale.x*culture.scale.y*culture.scale.z})
 s.frame_set(frame(55.5));assert abs(bpy.data.objects['Release hatch hinge'].rotation_euler[2])>math.radians(90)
 s.frame_set(frame(58));assert abs(bpy.data.objects['Floor grate hinge'].rotation_euler[0])>math.radians(60)
 frames=[]
 for sec,name in [(2.5,'Specimen07'),(29,'Specimen lobe 0'),(35.2,'Contact volunteer Rig'),(39,'Contact volunteer Rig'),(39,'Dr Hana Petrov Rig'),(53.2,'Interlock controller'),(63.5,'Escaping free culture'),(71,'Specimen in the city')]:
  s.frame_set(frame(sec));shot=next(x for x in SHOTS if x['start']<=sec<x['end']);cam=bpy.data.objects['Camera '+shot['id']];obj=bpy.data.objects[name]
  point=obj.matrix_world.translation
  if name.endswith('Rig'):point=obj.matrix_world@obj.pose.bones['Head'].matrix.translation
  projected=world_to_camera_view(s,cam,point);frames.append({'seconds':sec,'target':name,'screen':[projected.x,projected.y,projected.z]})
  assert projected.z>0 and -.15<projected.x<1.15 and -.15<projected.y<1.15,f'Key action outside shot at{sec}: {tuple(projected)}'
 assert source.stat().st_size<100_000_000
 result={'status':'passed','source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'file_bytes':source.stat().st_size,'frame_count':1920,'fps':24,'shots':11,'captions':len(CAPTIONS),'packed_images':images,'care_contact_error_m':care_error,'escape_fit_samples':escape,'actual_pose_samples':records,'camera_action_samples':frames,'gpu_rendered':False,'runtime_candidate_modified':False}
 (OUT/'authoring-audit.json').write_text(json.dumps(result,indent=2)+'\n');print('[full-opening-audit] PASS',json.dumps(result),flush=True)

try:
 main()
except BaseException as error:
 failure={'status':'failed','source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'error':str(error),'gpu_rendered':False}
 (OUT/'authoring-audit.json').write_text(json.dumps(failure,indent=2)+'\n')
 raise
