"""Author the full80s/11beat opening as one editable, packed Blender film scene.
No runtime Assets writes. This authoring command never renders on GPU.
"""
import sys
sys.dont_write_bytecode=True
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import bpy,math,json,argparse
from mathutils import Vector,Matrix,Quaternion
import common as C
from plan import SHOTS,CAPTIONS,TAPS,DURATION,FPS,frame
from motion.library import MotionLibrary
from motion.speech import SpeechPerformance

OUT=C.SOURCE/'Opening';OUT.mkdir(parents=True,exist_ok=True);(OUT/'Review').mkdir(exist_ok=True)
S=C.init();S.frame_start=1;S.frame_end=DURATION*FPS
lab=C.empty('Historical laboratory');lab_objects=C.append_set('ContainmentLaboratory',lab)
metal=C.material('Machined mechanism',(.18,.23,.25),.35,.8)
dark=C.material('Graphite seals',(.025,.035,.04),.5,.5)
warning=C.material('Amber interlock',(.9,.21,.025),.3,0,2)
green=C.material('Released interlock',(.1,.7,.35),.25,0,2)
blue=C.material('Clinical source',(.45,.68,.9),.4,0,.2)

# Physical vessel, with a small real release port instead of a sealed cylinder the creature passes through.
glass=C.material('Thin borosilicate',(.80,.94,.95),.045,0,0,1);glass.node_tree.nodes['Principled BSDF'].inputs['IOR'].default_value=1.46
bpy.ops.mesh.primitive_cylinder_add(vertices=96,radius=.445,depth=1.82,end_fill_type='NOTHING',location=C.v((0,1.4,0)))
vessel=bpy.context.object;vessel.name='Containment glass';vessel.parent=lab;vessel.data.materials.append(glass)
solid=vessel.modifiers.new('Four millimeter vessel','SOLIDIFY');solid.thickness=.004;bpy.context.view_layer.objects.active=vessel;bpy.ops.object.modifier_apply(modifier=solid.name)
for p in vessel.data.polygons:p.use_smooth=True
port_cutter=C.cylinder('Release port cutter',(0,.80,-.66),(0,.80,-.30),.17,None,lab,48)
mod=vessel.modifiers.new('Actual escape aperture','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=port_cutter;bpy.context.view_layer.objects.active=vessel;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(port_cutter,do_unlink=True)
port_hinge=C.empty('Release hatch hinge',lab,(.18,.80,-.456))
hatch=C.cylinder('Opening containment hatch',(-.18,0,-.005),(-.18,0,.025),.178,metal,port_hinge,48)
for t,a in [(0,0),(53.7,0),(55.2,105),(59,105)]:port_hinge.rotation_euler[2]=math.radians(a);port_hinge.keyframe_insert('rotation_euler',frame=frame(t))

# The floor escape has a real cutout, a hinged slotted grate and a dark pipe interior.
floorpaint=next(ob for ob in lab_objects if any(m and m.name.startswith('CinemaPaint') for m in ob.data.materials))
cutter=C.cylinder('Floor drain boolean',(0,-.35,-.8),(0,.2,-.8),.275,None,lab,48)
mod=floorpaint.modifiers.new('Actual storm service drain','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter;bpy.context.view_layer.objects.active=floorpaint;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
C.cylinder('Dark service drain',(0,-.55,-.8),(0,-.12,-.8),.268,dark,lab,48)
drain_hinge=C.empty('Floor grate hinge',lab,(0,.025,-.52))
for i in range(11):C.box('Drain grate bar',(-.25+i*.05,0,-.28),(.022,.035,.52),metal,drain_hinge,.006)
for x in [-.28,.28]:C.box('Drain grate frame',(x,0,-.28),(.035,.035,.57),metal,drain_hinge,.008)
for t,a in [(0,0),(56,0),(57.8,78),(59,78)]:drain_hinge.rotation_euler[0]=math.radians(a);drain_hinge.keyframe_insert('rotation_euler',frame=frame(t))

controller=C.empty('Interlock controller',lab,(.78,1.05,-.17))
C.box('Interlock machined enclosure',(0,0,0),(.34,.45,.18),metal,controller,.025)
C.box('Interlock faceplate',(0,0,-.104),(.29,.39,.025),dark,controller,.012)
indicator=C.box('Interlock indicator',(0,.105,-.125),(.19,.055,.018),warning,controller,.008)
lever=C.empty('Interlock manual release lever',controller,(0,-.045,-.137))
C.cylinder('Manual lever spindle',(0,0,-.02),(0,0,-.10),.027,metal,lever)
C.box('Manual lever grip',(0,.075,-.11),(.045,.19,.055),dark,lever,.012)
for t,angle in [(0,0),(51.7,0),(52.8,-68),(59,-68)]:lever.rotation_euler[1]=math.radians(angle);lever.keyframe_insert('rotation_euler',frame=frame(t))
led=warning.node_tree.nodes['Principled BSDF'];socket=led.inputs['Emission Color']
for t,col in [(0,(1,.32,.03,1)),(44,(1,.05,.015,1)),(51.9,(1,.25,.03,1)),(52.8,(.08,1,.35,1)),(59,(.08,1,.35,1))]:socket.default_value=col;socket.keyframe_insert('default_value',frame=frame(t))

restraints=[]
for side in [-1,1]:
 pivot=C.empty('Pressure restraint '+str(side),lab,(side*.69,1.17,0));restraints.append(pivot)
 C.cylinder('Hydraulic restraint arm',(0,-.12,0),(-side*.20,.05,0),.032,metal,pivot)
 C.box('Pressure pad',(-side*.21,.06,0),(.065,.35,.16),dark,pivot,.018)
 for t,x in [(0,side*.12),(43.8,side*.12),(46,0),(50,0),(54,side*.12)]:
  pivot.location=C.v((side*.69+x,1.17,0));pivot.keyframe_insert('location',frame=frame(t))

C.area('Laboratory soft key',(-2.2,3,-2),(0,1.3,0),180,(.66,.84,1),2.4,lab)
C.area('Specimen rim',(0,2.1,1.9),(0,1.3,0),75,(.16,1,.43),1.6,lab)
C.area('Practical tungsten edge',(3,2.3,-1),(0,1.3,0),60,(1,.56,.28),1.7,lab)
C.area('Actor soft frontal fill',(.1,2.5,-3.4),(0,1.2,.2),95,(.72,.83,1),3,lab)

jelly=C.material('Living translucent cytoplasm',(.035,.38,.085),.12,0,.035,.48);bs=jelly.node_tree.nodes['Principled BSDF'];bs.inputs['IOR'].default_value=1.36;bs.inputs['Subsurface Weight'].default_value=.2;bs.inputs['Subsurface Radius'].default_value=(.2,1,.25)
coremat=C.material('Textured living nucleus',(.12,.34,.035),.25,0,.12,.22)
corebs=coremat.node_tree.nodes['Principled BSDF'];corebs.inputs['Subsurface Weight'].default_value=.32;corebs.inputs['Subsurface Scale'].default_value=.025;corebs.inputs['Coat Weight'].default_value=.18
noise=coremat.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5;noise.inputs['Detail'].default_value=3
ramp=coremat.node_tree.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.035,.12,.008,1);ramp.color_ramp.elements[1].color=(.28,.55,.065,1);coremat.node_tree.links.new(noise.outputs['Fac'],ramp.inputs['Fac']);coremat.node_tree.links.new(ramp.outputs['Color'],corebs.inputs['Base Color'])
bump=coremat.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.009;bump.inputs['Strength'].default_value=.32;coremat.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);coremat.node_tree.links.new(bump.outputs['Normal'],corebs.inputs['Normal'])
organellemat=C.material('Amber vacuole membranes',(.36,.24,.025),.22,0,.035,.4);organellemat.node_tree.nodes['Principled BSDF'].inputs['Subsurface Weight'].default_value=.15
# Actual bounded interior medium gives the membrane depth instead of a flat emissive disk.
bs.inputs['Transmission Weight'].default_value=.72;bs.inputs['Coat Weight'].default_value=.25
absorption=jelly.node_tree.nodes.new('ShaderNodeVolumeAbsorption');absorption.inputs['Color'].default_value=(.12,.68,.18,1);absorption.inputs['Density'].default_value=2.2
scatter=jelly.node_tree.nodes.new('ShaderNodeVolumeScatter');scatter.inputs['Color'].default_value=(.18,.48,.09,1);scatter.inputs['Density'].default_value=.42;scatter.inputs['Anisotropy'].default_value=.12
combine=jelly.node_tree.nodes.new('ShaderNodeAddShader');jelly.node_tree.links.new(absorption.outputs[0],combine.inputs[0]);jelly.node_tree.links.new(scatter.outputs[0],combine.inputs[1]);jelly.node_tree.links.new(combine.outputs[0],jelly.node_tree.nodes['Material Output'].inputs['Volume'])

def culture(name,parent,p,size):
 root=C.empty(name,parent,p);shell=C.sphere(name+' membrane',(0,0,0),size,jelly,root)
 for vert in shell.data.vertices:
  co=vert.co;co*=1+.035*math.sin(co.x*13+co.y*7)*math.sin(co.z*9)
 C.sphere(name+' nucleus',(.01,.01,-.02),(.046,.046,.043),coremat,root)
 for i,(q,r) in enumerate([((-.1,.06,.03),.022),((.1,-.06,-.02),.029),((-.06,-.08,-.04),.019)]):C.sphere(name+' organelle '+str(i),q,(r,r,r),organellemat if i%2==0 else coremat,root)
 return root
subject=culture('Specimen07',lab,(0,1.28,0),(.24,.20,.23))
lobes=[culture('Specimen lobe '+str(i),lab,(0,1.28,0),(.17,.16,.17)) for i in range(2)]
filament=C.curve('Contact filament',[(.15,1.28,0),(.33,1.23,-.09),(.43,1.2,-.16),(.60,1.2,-.22),(.65,1.2,-.25)],.013,jelly,lab)
C.show_between([filament],34.2,37.3)

petrov=C.Actor('DrPetrov','Dr Hana Petrov',lab,(-.72,0,.08),90)
vale=C.Actor('DrVale','Dr Vale',lab,(.9,0,1.05),-139)
worker=C.Actor('MaintEli','Contact volunteer',lab,(2.3,0,1.5),-90)
actors=[petrov,vale,worker]
M=MotionLibrary(C.PROJECT)
speech=SpeechPerformance(C.PROJECT)

def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def mix(a,b,t):return tuple(x+(y-x)*smooth(t) for x,y in zip(a,b))
def path(t,keys):
 if t<=keys[0][0]:return keys[0][1]
 for (a,p),(b,q) in zip(keys,keys[1:]):
  if t<=b:return mix(p,q,(t-a)/(b-a))
 return keys[-1][1]
def angle(t,keys):return path(t,[(x,(y,)) for x,y in keys])[0]
def select_pose(actor,t):
 if actor==petrov:
  schedule=[(0,5,'Idle_Neutral'),(5,16,'Point_Loop'),(16,19,'Talk_Loop'),(19,26,'Gesture_Explain'),(26,34,'Idle_Clipboard'),(34,36.5,'Point_Loop'),(36.5,37.8,'Walk'),(37.8,41,'Captured_Loop'),(41,43,'Walk'),(43,51,'Point_Loop'),(51,59,'Idle_LookAround')]
 elif actor==vale:schedule=[(0,19,'Idle_FoldArms'),(19,22,'Talk_Loop'),(22,26,'Gesture_Shrug'),(26,46.5,'Idle_FoldArms'),(46.5,51,'Walk'),(51,54,'Console_Loop'),(54,59,'Idle_Neutral')]
 else:schedule=[(0,31,'Idle_Neutral'),(31,34,'Walk'),(34,36,'Point_Loop'),(36,38.4,'Collapse'),(38.4,59,'Unconscious_Loop')]
 i=next((i for i,(a,b,c) in enumerate(schedule) if a<=t<b),len(schedule)-1);a,b,clip=schedule[i]
 if clip not in M.available_clip_names:clip='Idle_Neutral'
 phase=.72 if actor==petrov and a==36.5 else .47 if actor==petrov else .11 if actor==vale else .40
 local=max(0,t-a)*(phase if clip=='Walk' else 1)
 pose=M.pose_for(actor.rig,clip,local,root_motion=False)
 if i and t-a<.28:
  pa,pb,pc=schedule[i-1]
  if pc not in M.available_clip_names:pc='Idle_Neutral'
  previous=M.pose_for(actor.rig,pc,(pb-pa)*(.72 if actor==petrov and pa==36.5 else .47 if actor==petrov else .11 if actor==vale else .40) if pc=='Walk' else pb-pa,root_motion=False);blend=smooth((t-a)/.28)
  pose={n:(previous[n][0].slerp(q,blend),previous[n][1].lerp(loc,blend)) for n,(q,loc) in pose.items()}
 for name,(q,loc) in pose.items():
  bone=actor.rig.pose.bones[name];bone.rotation_quaternion=q;bone.location=loc
 return clip

def bone_world_rotation(arm,name,desired,W):
 bone=arm.data.bones[name]
 if bone.parent:
  restrel=bone.parent.matrix_local.inverted()@bone.matrix_local;P=W[bone.parent.name]@restrel
 else:P=bone.matrix_local
 arm.pose.bones[name].rotation_quaternion=(P.to_3x3().normalized().inverted()@desired).to_quaternion()

def reach(actor,side,target):
 arm=actor.rig;W=M.fk(arm);upper=side+'UpperArm';lower=side+'LowerArm';hand=side+'Hand'
 root=Matrix.Translation(arm.location)@arm.rotation_euler.to_matrix().to_4x4();T=root.inverted()@C.v(target)
 shoulder=W[upper].translation;elbow=W[lower].translation;wrist=W[hand].translation;a=(elbow-shoulder).length;b=(wrist-elbow).length;delta=T-shoulder;distance=max(.02,min(delta.length,a+b-.004));direction=delta.normalized()
 pole=elbow-shoulder;pole-=direction*pole.dot(direction)
 if pole.length<.01:pole=Vector((.2 if side=='Left' else -.2,-.2,-.3));pole-=direction*pole.dot(direction)
 pole.normalize();cosine=max(-1,min(1,(a*a+distance*distance-b*b)/(2*a*distance)))
 E=shoulder+direction*a*cosine+pole*a*math.sqrt(max(0,1-cosine*cosine))
 desired=(elbow-shoulder).rotation_difference(E-shoulder).to_matrix()@W[upper].to_3x3().normalized();bone_world_rotation(arm,upper,desired,W)
 W=M.fk(arm);elbow=W[lower].translation;wrist=W[hand].translation
 desired=(wrist-elbow).rotation_difference(T-elbow).to_matrix()@W[lower].to_3x3().normalized();bone_world_rotation(arm,lower,desired,W)
 return M.fk(arm)

speaker_windows=[]
for i,(t,who,text) in enumerate(CAPTIONS):
 if not text:continue
 duration=speech.duration(who,text) if who in ('DR. HANA PETROV','DR. VALE','MAINTENANCE WORKER') else max(1.2,len(text)*.047)
 end=min(CAPTIONS[i+1][0] if i+1<len(CAPTIONS) else 80,t+duration)
 speaker_windows.append((t,end,who,text))

# Bake actual retargeted body/finger animation at film24fps, with authored hand contact and small face layers.
for f in range(1,59*FPS+1):
 t=(f-1)/FPS
 C.move(petrov.rig,t,path(t,[(0,(-.72,0,.08)),(36.5,(-.72,0,.08)),(37.8,(.45,0,-1.25)),(41,(.45,0,-1.25)),(43,(-.72,0,.08)),(59,(-.72,0,.08))]))
 C.rotate(petrov.rig,t,angle(t,[(0,90),(18,90),(20.0,59),(24,59),(26,90),(36.2,90),(36.5,139),(37.6,139),(37.8,90),(40.5,90),(41,-41),(42.6,-41),(43,90),(52,90),(54,30),(59,30)]))
 C.move(vale.rig,t,path(t,[(0,(.9,0,1.05)),(46.5,(.9,0,1.05)),(51,(1.15,0,.15)),(59,(1.15,0,.15))]));C.rotate(vale.rig,t,angle(t,[(0,-139),(46.2,-139),(46.5,-195),(50.7,-195),(51,-90),(59,-90)]))
 C.move(worker.rig,t,path(t,[(0,(2.3,0,1.5)),(31,(2.3,0,1.5)),(34,(.95,0,-.48)),(36,(.95,0,-.48)),(38.4,(.95,.07706,-.48)),(59,(.95,.07706,-.48))]));C.rotate(worker.rig,t,angle(t,[(0,-90),(30.7,-90),(31,-146),(33.7,-146),(34,-63),(35,-63),(36.2,5),(59,5)]))
 for actor in actors:
  clip=select_pose(actor,t)
  head=actor.rig.pose.bones.get('Head');jaw=actor.rig.pose.bones.get('Jaw')
  if head:head.rotation_quaternion=head.rotation_quaternion@Quaternion((0,1,0),math.sin(t*1.3)*.012)
  who='DR. HANA PETROV' if actor==petrov else 'DR. VALE' if actor==vale else 'MAINTENANCE WORKER'
  amplitude=max((speech.amplitude(who,line,t-a) for a,b,w,line in speaker_windows if a<=t<b and w==who),default=0.0)
  if jaw and amplitude>0:jaw.rotation_quaternion=jaw.rotation_quaternion@Quaternion((1,0,0),math.radians(8*amplitude))
 if 37.8<t<41:
  # Kneel beside the collapsed person; release the capture clip's hands and use them to help instead.
  weight=smooth((t-37.8)/.35)*(1-smooth((t-40.65)/.35));relaxed=M.pose_for(petrov.rig,'Console_Loop',t-37.8,root_motion=False)
  for name,(q,loc) in relaxed.items():
   if name.startswith(('LeftHand','RightHand','LeftIndex','RightIndex','LeftMiddle','RightMiddle','LeftRing','RightRing','LeftLittle','RightLittle','LeftThumb','RightThumb')):
    petrov.rig.pose.bones[name].rotation_quaternion=q
  for name,amount in [('Spine',.65),('Chest',.35),('UpperChest',.20)]:
   W=M.fk(petrov.rig);desired=Matrix.Rotation(amount*weight,3,'X')@W[name].to_3x3().normalized();bone_world_rotation(petrov.rig,name,desired,W)
  W=M.fk(worker.rig);root=Matrix.Translation(worker.rig.location)@worker.rig.rotation_euler.to_matrix().to_4x4();chest=root@W['UpperChest'].translation;shoulder=root@W['LeftUpperArm'].translation
  reach(petrov,'Right',C.uf(chest+Vector((0,0,.10))));reach(petrov,'Left',C.uf(shoulder+Vector((0,0,.08))))
 if (5.4<t<8.2) or (44.5<t<46.1):
  pulse=max(math.exp(-((t-hit)/.075)**2) for hit in TAPS);reach(petrov,'Right',(-.58+.06*pulse,1.40,.10))
 if 34.0<t<35.9:reach(worker,'Right',(.65,1.20,-.25))
 if 51.2<t<53.2:reach(vale,'Right',(.83,1.05,-.34))
 for actor in actors:C.key_pose(actor.rig,t)
 if 34.0<t<37.5:
  W=M.fk(worker.rig);root=Matrix.Translation(worker.rig.location)@worker.rig.rotation_euler.to_matrix().to_4x4();hand=root@W['RightHand'].translation;tip=C.uf(hand)
  points=[(.15,1.28,0),(.33,1.23,-.09),(.43,1.2,-.16),((tip[0]+.43)*.5,(tip[1]+1.2)*.5,(tip[2]-.16)*.5),tip]
  for p,co in zip(filament.data.splines[0].points,points):p.co=(*C.v(co),1);p.keyframe_insert('co',frame=f)
 if f%240==1:print('[full-opening] body animation',f,'/',59*FPS,flush=True)

# Culture movement responds to individual stimulus beats; both lobes share one physiological rhythm.
for f in range(1,59*FPS+1):
 t=(f-1)/FPS;beat=max(math.exp(-((t-hit)/.12)**2) for hit in TAPS);breath=1+.025*math.sin(t*3)+beat*.035
 if t<26.8:scale=1
 elif t<27.4:scale=1-smooth((t-26.8)/.6)
 elif t<33.5:scale=.001
 else:scale=smooth((t-33.5)/.5)
 # Squeeze through the real 17cm-radius aperture while preserving approximate volume.
 squeeze=smooth((t-57.0)/.25)*(1-smooth((t-58.05)/.30))
 radial=1-.35*squeeze;longitudinal=1/(radial*radial)
 C.scale(subject,t,Vector((radial*breath,longitudinal*breath,radial*breath))*max(.001,scale))
 position=path(t,[(0,(0,1.28,0)),(56.5,(0,1.28,0)),(57.0,(0,.80,0)),(57.4,(0,.80,-.20)),(57.8,(0,.80,-.65)),(58.05,(0,.80,-1.10)),(58.35,(0,.35,-.80)),(59,(0,-.35,-.8))]);C.move(subject,t,position)
 for i,lobe in enumerate(lobes):
  split=smooth((t-26.75)/.7)*(1-smooth((t-33.5)/.5));C.scale(lobe,t,Vector((1,1,1))*max(.001,split*breath))
  C.move(lobe,t,((i*2-1)*(.18+.018*math.sin(t)),1.28+.018*math.sin(t*1.1+i),0))

# Separate native duct stage, fully lit inside; it stays off-camera until the physical escape cut.
duct=C.empty('Service conduit stage',p=(0,-4,0));C.append_set('ServiceConduit',duct)
for i in range(5):C.point('Recessed duct practical '+str(i),(.45,.24,i*2.4),4,(1,.62,.28),.08,duct)
C.area('Duct lens side fill',(0,.25,.1),(0,0,5),8,(.40,.67,1),.5,duct)
escaping=culture('Escaping free culture',duct,(0,-.19,4.5),(.18,.14,.20))
for f in range(frame(59),frame(67)):
 t=(f-1)/FPS;u=(t-59)/8;C.move(escaping,t,(.045*math.sin(t*2),-.16+.012*math.sin(t*5),4.4+3.5*u));C.scale(escaping,t,(1+.06*math.sin(t*4),1+.035*math.cos(t*4),1-.04*math.sin(t*4)))
for i in range(18):
 drop=C.sphere('Conduit condensation '+str(i),((i%3-1)*.4,.44,i*.6),(.009,.02,.009),glass,duct)
 for t,y in [(59,.44),(62+i*.12,-.30),(67,-.30)]:C.move(drop,t,((i%3-1)*.4,y,i*.6))

# A detailed real-scale alley connects the lab escape to an inhabited city, rather than a black final frame.
alley=C.empty('Outside city alley',p=(0,0,40));city=C.PROJECT/'Assets/Parasite/Art/City'
brick=C.pbr('Wet brick masonry',city/'brick','brick',rough=.72);asphalt=C.pbr('Wet road aggregate',city/'asphalt','asphalt',rough=.24);concrete=C.material('Curb concrete',(.22,.24,.25),.85)
window=C.material('Apartment glass',(.035,.06,.075),.13,.2);warm=C.material('Occupied window glow',(.8,.42,.17),.5,0,1.3)
C.box('Alley asphalt',(0,-.15,5),(9,.3,30),asphalt,alley,.025)
for side in [-1,1]:
 C.box('Brick tenement wall',(side*4.8,5.5,6),(.35,11,25),brick,alley,.035)
 C.box('Alley curb',(side*3.8,.04,5),(.26,.16,30),concrete,alley,.025)
 for row in range(4):
  for col in range(7):
   z=-4+col*3.1;y=1.7+row*2.4;x=side*4.59
   C.box('Recessed apartment frame',(x,y,z),(.13,1.35,1.35),metal,alley,.035)
   C.box('Deep window glass',(x-side*.08,y,z),(.03,1.16,1.16),window,alley,.008)
   if (row*5+col+side)%3==0:
    C.box('Occupied room',(x-side*.07,y,z),(.02,1.12,1.12),warm,alley,.004)
    if row<2:C.area('Window practical',(x-side*.18,y,z),(0,y*.35,z),18,(1,.65,.37),1,alley)
   C.cylinder('Exterior drain pipe',(side*4.5,.15,z+1.45),(side*4.5,10.8,z+1.45),.035,metal,alley)
 C.box('Street opening building',(side*8,8,22),(7,16,8),brick,alley,.05)
C.area('Alley dusk sky',(0,12,8),(0,0,3),110,(.31,.44,.70),12,alley)
C.area('Street sodium rim',(0,5,19),(0,.2,1),90,(1,.51,.23),5,alley)
# A real skyline and depth atmosphere continue beyond the street opening; the horizon is never a flat void.
cityglass=C.material('Distant city glazing',(.045,.065,.09),.24,.25);citywall=C.material('Distant masonry',(.13,.16,.20),.78)
for i,(x,z,w,h) in enumerate([(-12,43,7,20),(10,48,8,16),(-5,70,6,12),(5,80,7,10),(-18,85,10,30),(16,90,9,25),(0,110,7,9),(-30,125,14,38),(30,125,15,32)]):
 C.box('Distant occupied city block '+str(i),(x,h*.5,z),(w,h,7+(i%3)*2),citywall,alley,.04)
 for row in range(max(2,round(h/2.8))):
  for col in range(max(2,round(w/1.6))):
   wx=x-w*.38+col*1.5;wy=1.6+row*2.7
   C.box('Distant individual window',(wx,wy,z-3.55-(i%3)),(.74,1.05,.035),warm if (row*3+col+i)%4==0 else cityglass,alley,.006)
# Use the project's actual sunset sky only for the exterior section; laboratory ambience stays controlled.
world=S.world.node_tree;env=world.nodes.new('ShaderNodeTexEnvironment');env.image=C.tex(city/'sky/belfast_sunset_puresky_4k.hdr');env.texture_mapping.rotation[2]=math.radians(95)
mixsky=world.nodes.new('ShaderNodeMixRGB');mixsky.inputs[1].default_value=(.08,.13,.18,1);world.links.new(env.outputs['Color'],mixsky.inputs[2]);world.links.new(mixsky.outputs['Color'],world.nodes['Background'].inputs['Color'])
for t,value,strength in [(0,0,.14),(66.9,0,.14),(67,1,.38),(80,1,.38)]:
 mixsky.inputs[0].default_value=value;mixsky.inputs[0].keyframe_insert('default_value',frame=frame(t));world.nodes['Background'].inputs['Strength'].default_value=strength;world.nodes['Background'].inputs['Strength'].keyframe_insert('default_value',frame=frame(t))
hazemat=bpy.data.materials.new('Distant blue city haze');hazemat.use_nodes=True;nt=hazemat.node_tree;nt.nodes.clear();vo=nt.nodes.new('ShaderNodeOutputMaterial');scat=nt.nodes.new('ShaderNodeVolumeScatter');scat.inputs['Color'].default_value=(.46,.56,.70,1);scat.inputs['Density'].default_value=.0025;scat.inputs['Anisotropy'].default_value=.15;nt.links.new(scat.outputs[0],vo.inputs['Volume'])
C.box('Thin atmosphere behind street opening',(0,20,90),(180,44,140),hazemat,alley,0)
# Patch-scale wetness and millimetre-scale aggregate augment the source asphalt normal/roughness maps.
nt=asphalt.node_tree;road=nt.nodes['Principled BSDF'];geo=nt.nodes.new('ShaderNodeNewGeometry');grain=nt.nodes.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=180;grain.inputs['Detail'].default_value=2;nt.links.new(geo.outputs['Position'],grain.inputs['Vector'])
rb=nt.nodes.new('ShaderNodeBump');rb.inputs['Strength'].default_value=.28;rb.inputs['Distance'].default_value=.0025;nt.links.new(grain.outputs['Fac'],rb.inputs['Height'])
if road.inputs['Normal'].is_linked:nt.links.new(road.inputs['Normal'].links[0].from_socket,rb.inputs['Normal'])
nt.links.new(rb.outputs['Normal'],road.inputs['Normal'])
wet=nt.nodes.new('ShaderNodeTexNoise');wet.inputs['Scale'].default_value=.45;wet.inputs['Detail'].default_value=3;nt.links.new(geo.outputs['Position'],wet.inputs['Vector']);rough=nt.nodes.new('ShaderNodeValToRGB');rough.color_ramp.elements[0].color=(.12,.12,.12,1);rough.color_ramp.elements[1].color=(.66,.66,.66,1);nt.links.new(wet.outputs['Fac'],rough.inputs['Fac']);nt.links.new(rough.outputs['Color'],road.inputs['Roughness']);road.inputs['Coat Weight'].default_value=.18
C.box('Wall practical housing',(-4.54,2,-1),(.28,.24,.2),dark,alley,.025);C.box('Wall practical lens',(-4.37,1.95,-1),(.03,.16,.15),warm,alley,.008)
C.area('Motivated low alley practical',(-4.3,1.9,-1),(0,.12,-1.5),35,(1,.69,.41),.45,alley)

# Ground-level grate, bolts and runoff puddles frame the final creature's emergence.
for i in range(13):C.box('Street drain iron slat',(-.3+i*.05,.025,-2),(.022,.045,.68),metal,alley,.006)
for x in [-.34,.34]:C.box('Street drain border',(x,.025,-2),(.035,.045,.73),metal,alley,.008)
for i in range(7):
 C.sphere('Shallow runoff reflection '+str(i),(-2.4+i*.7,-.002,1.2+math.sin(i)*1.8),(.35+.04*i,.006,.7),C.material('Puddle '+str(i),(.035,.06,.075),.07,.1,0,.15),alley)
free=culture('Specimen in the city',alley,(0,-.12,-2),(.16,.10,.18))
for t,p in [(67,(0,-.12,-2)),(68.8,(0,.11,-2)),(71,(.15,.11,-1.5)),(75,(.35,.11,-.5)),(80,(.35,.11,.25))]:C.move(free,t,p)
for f in range(frame(67),frame(80)):
 t=(f-1)/FPS;C.scale(free,t,(1+.045*math.sin(t*4),1+.025*math.cos(t*4),1-.035*math.sin(t*4)))
# One ordinary passerby makes the city indifferent to the escaped experiment.
passer=C.Actor('CivKenji','Unaware pedestrian',alley,(-5,0,8),90)
for f in range(frame(67),frame(80)):
 t=(f-1)/FPS;C.move(passer.rig,t,(-5+(t-67)*.68,0,8));C.rotate(passer.rig,t,90);M.apply_pose(passer.rig,'Walk',(t-67)*.34,root_motion=False);C.key_pose(passer.rig,t)

# Key visibility by object so lighting, particles, actors and props all change stages together.
def descendants(root):return [root]+list(root.children_recursive)
C.show_between(descendants(lab),0,59);C.show_between(descendants(duct),59,67);C.show_between(descendants(alley),67,80)
# Contact filament additionally needs its within-stage interval, and source division lobes never appear early.
C.show_between([filament],34.2,37.3)
for lobe in lobes:C.show_between(descendants(lobe),26.75,34)

C.flush_pose_keys()
cameras={shot['id']:C.make_camera(shot) for shot in SHOTS}
for key in ['10-outside','11-next-beat']:cameras[key].data.dof.aperture_fstop=11
S.camera=cameras[SHOTS[0]['id']]
C.flush_transform_keys()
for actor in actors+[passer]:C.set_linear(actor.rig.animation_data.action)
for ob in [subject,*lobes,escaping,free]:
 if ob.animation_data:C.set_linear(ob.animation_data.action)
S.frame_set(1)
manifest={'version':1,'fps':FPS,'width':1920,'height':1080,'duration_seconds':DURATION,'frame_count':DURATION*FPS,'shots':SHOTS,'captions':[{'at':t,'speaker':who,'text':text} for t,who,text in CAPTIONS],'taps':TAPS,'caption_policy':'Live runtime overlay; no baked subtitles or automatic gameplay actions','render_profile':{'engine':'Cycles','device':'RTX4050 OptiX after explicit GPU release','samples':48,'adaptive_threshold':.04,'denoising':True},'actions':'Actual existing humanoid clips retargeted at24fps with physical hand IK and measured shipped-voice RMS jaw layer','speech_performance':{'cache':'ArtSource/FullCinematics/Motion/opening_speech_envelopes.json','actor_cues':[{'at':t,'speaker':who,'line':text,'clip':speech.info(who,text)} for t,who,text in CAPTIONS if who in ('DR. HANA PETROV','DR. VALE','MAINTENANCE WORKER')]} ,'remaining_verification':['GPU quality/timing calibration','Full frame render/encode','80s player decode/skip/end handoff']}
(OUT/'opening-timeline.json').write_text(json.dumps(manifest,indent=2)+'\n')
source=OUT/'OpeningFull.blend';C.save_portable(source)
print('[full-opening] AUTHORED',source,source.stat().st_size,'bytes',flush=True)

if '--cpu-previews' in sys.argv:
 for name,t in [('02-petrov',7),('03-recognition',16.5),('05-two-bodies',29),('06-contact',35.2),('06-contact-fall',39),('08-interlock',52.4),('08-interlock-escape',58.2),('09-conduit',63.5),('10-outside',71)]:
  cam=cameras[next(s['id'] for s in SHOTS if s['start']<=t<s['end'])]
  C.cpu_preview(OUT/'Review'/(name+'.png'),t,cam,width=480,height=270,samples=8)
  print('[full-opening] CPU PREVIEW',name,flush=True)
