"""Native Blender authoring helpers. Read project assets; write only the full-story source tree."""
import sys
sys.dont_write_bytecode=True
import bpy,math,json
from pathlib import Path
from mathutils import Vector,Matrix
PROJECT=Path(__file__).resolve().parents[4]
SOURCE=PROJECT/'ArtSource/FullCinematics'
FPS=24

def v(p):return Vector((p[0],-p[2],p[1]))
def uf(p):return (p.x,p.z,-p.y)
def frame(t):return round(t*FPS)+1

def init():
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.context.preferences.filepaths.save_version=0
 s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='GPU';s.cycles.samples=48;s.cycles.use_denoising=True
 s.cycles.max_bounces=6;s.cycles.transmission_bounces=4;s.cycles.transparent_max_bounces=6;s.cycles.use_adaptive_sampling=True;s.cycles.adaptive_threshold=.04
 s.render.fps=FPS;s.render.resolution_x=1920;s.render.resolution_y=1080;s.render.resolution_percentage=100
 s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.view_settings.view_transform='AgX'
 s.render.film_transparent=False;s.render.threads_mode='FIXED';s.render.threads=2
 w=bpy.data.worlds.new('Full story world');w.use_nodes=True;w.node_tree.nodes['Background'].inputs['Color'].default_value=(.08,.13,.18,1);w.node_tree.nodes['Background'].inputs['Strength'].default_value=.14;s.world=w
 return s

def empty(name,parent=None,p=(0,0,0)):
 ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob)
 if parent:ob.parent=parent
 ob.location=v(p);return ob

def material(name,color,rough=.4,metal=0,emit=0,transmission=0):
 m=bpy.data.materials.new(name);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF')
 b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=rough;b.inputs['Metallic'].default_value=metal
 b.inputs['Emission Color'].default_value=(*color,1);b.inputs['Emission Strength'].default_value=emit;b.inputs['Transmission Weight'].default_value=transmission
 return m

def tex(path,data=False):
 im=bpy.data.images.load(str(path),check_existing=True)
 if data:im.colorspace_settings.name='Non-Color'
 return im

def pbr(name,folder,prefix,tint=(1,1,1),rough=.6,metal=0):
 m=material(name,tint,rough,metal);nt=m.node_tree;b=nt.nodes.get('Principled BSDF')
 choices=[folder/(prefix+'_albedo.jpg'),folder/(prefix+'_A.png'),folder/(prefix+'_diff_1k.jpg')]
 al=next((p for p in choices if p.is_file()),None)
 if al:
  t=nt.nodes.new('ShaderNodeTexImage');t.image=tex(al);nt.links.new(t.outputs['Color'],b.inputs['Base Color'])
 normal=next((p for p in [folder/(prefix+'_normal.jpg'),folder/(prefix+'_N.png'),folder/(prefix+'_nor_gl_1k.jpg')] if p.is_file()),None)
 if normal:
  t=nt.nodes.new('ShaderNodeTexImage');t.image=tex(normal,True);n=nt.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.55;nt.links.new(t.outputs['Color'],n.inputs['Color']);nt.links.new(n.outputs['Normal'],b.inputs['Normal'])
 return m

def finish(ob,name,mat,parent=None,bevel=0):
 ob.name=name
 if parent:ob.parent=parent
 if mat:ob.data.materials.clear();ob.data.materials.append(mat)
 bpy.context.view_layer.objects.active=ob;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=ob.modifiers.new('Manufactured edge radii','BEVEL');mod.width=min(bevel,min(ob.dimensions)*.4);mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
 if ob.type=='MESH':
  layer=ob.data.uv_layers.active or ob.data.uv_layers.new(name='Meter surface')
  for polygon in ob.data.polygons:
   axis=max(range(3),key=lambda i:abs(polygon.normal[i]));axes=[i for i in range(3) if i!=axis]
   for loop in polygon.loop_indices:
    co=ob.data.vertices[ob.data.loops[loop].vertex_index].co;layer.data[loop].uv=(co[axes[0]],co[axes[1]])
 return ob

def box(name,p,d,mat,parent=None,bevel=.012):
 bpy.ops.mesh.primitive_cube_add(size=1,location=v(p));ob=bpy.context.object;ob.dimensions=(d[0],d[2],d[1]);return finish(ob,name,mat,parent,bevel)

def sphere(name,p,size,mat,parent=None):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=24,radius=1,location=v(p));ob=bpy.context.object;ob.scale=(size[0],size[2],size[1]);finish(ob,name,mat,parent)
 for poly in ob.data.polygons:poly.use_smooth=True
 return ob

def cylinder(name,a,b,r,mat,parent=None,vertices=32):
 aa,bb=v(a),v(b);d=bb-aa;bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(aa+bb)/2)
 ob=bpy.context.object;ob.rotation_mode='QUATERNION';ob.rotation_quaternion=d.to_track_quat('Z','Y');finish(ob,name,mat,parent,min(.006,r*.12))
 for poly in ob.data.polygons:poly.use_smooth=len(poly.vertices)==4
 return ob

def curve(name,points,r,mat,parent=None):
 data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.bevel_depth=r;data.bevel_resolution=3
 spline=data.splines.new('POLY');spline.points.add(len(points)-1)
 for p,co in zip(spline.points,points):p.co=(*v(co),1)
 ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob)
 if parent:ob.parent=parent
 data.materials.append(mat);return ob

def append_set(name,parent):
 with bpy.data.libraries.load(str(PROJECT/'ArtSource/Cinematics'/(name+'.blend')),link=False) as (a,b):b.objects=a.objects
 objects=[]
 for ob in b.objects:
  if not ob:continue
  bpy.context.collection.objects.link(ob);ob.parent=parent;objects.append(ob)
 for mat in {m for ob in objects if ob.type=='MESH' for m in ob.data.materials if m}:
  kind=mat.name.split('.')[0].replace('Cinema','');path=PROJECT/'Assets/Parasite/Resources/Cinematics/Textures'/(kind+'_N.png')
  if path.is_file() and mat.use_nodes:
   bs=mat.node_tree.nodes.get('Principled BSDF');n=mat.node_tree.nodes.new('ShaderNodeTexImage');n.image=tex(path,True);nm=mat.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.5;mat.node_tree.links.new(n.outputs['Color'],nm.inputs['Color']);mat.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
 return objects

def area(name,p,target,energy,color,size=2,parent=None):
 d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size
 ob=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(ob)
 if parent:ob.parent=parent
 ob.location=v(p);ob.rotation_euler=(v(target)-ob.location).to_track_quat('-Z','Y').to_euler();return ob

def point(name,p,energy,color,radius=.15,parent=None):
 d=bpy.data.lights.new(name,'POINT');d.energy=energy;d.color=color;d.shadow_soft_size=radius
 ob=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(ob)
 if parent:ob.parent=parent
 ob.location=v(p);return ob

def set_linear(action):
 if not action:return
 for f in action.fcurves:
  for k in f.keyframe_points:k.interpolation='CONSTANT' if f.data_path in ('hide_render','hide_viewport') else 'LINEAR'

_TRANSFORM_KEYS={}
def transform_key(ob,t,property_name):
 _TRANSFORM_KEYS.setdefault(ob,{}).setdefault(property_name,[]).append((frame(t),tuple(getattr(ob,property_name))))
def move(ob,t,p):ob.location=v(p);transform_key(ob,t,'location')
def rotate(ob,t,yaw):ob.rotation_euler[2]=math.radians(yaw);transform_key(ob,t,'rotation_euler')
def scale(ob,t,value):ob.scale=value;transform_key(ob,t,'scale')
def flush_transform_keys():
 for ob,properties in _TRANSFORM_KEYS.items():
  ob.animation_data_create()
  if ob.animation_data.action is None:ob.animation_data.action=bpy.data.actions.new(ob.name+' Motion')
  for property_name,rows in properties.items():
   for component in range(len(rows[0][1])):
    fc=ob.animation_data.action.fcurves.new(property_name,index=component,action_group='World motion')
    fc.keyframe_points.add(len(rows));fc.keyframe_points.foreach_set('co',[number for row in rows for number in (row[0],row[1][component])])
    for point in fc.keyframe_points:point.interpolation='LINEAR'
    fc.update()
 _TRANSFORM_KEYS.clear()
def show_between(objects,start,end):
 for ob in objects:
  for t,on in [(0,start==0),(max(0,start-.04),False),(start,True),(end,False)]:
   if start==0 and t==0 and not on:continue
   ob.hide_render=not on;ob.keyframe_insert('hide_render',frame=frame(t))
  if ob.animation_data:
   for fc in ob.animation_data.action.fcurves:
    if fc.data_path=='hide_render':
     for key in fc.keyframe_points:key.interpolation='CONSTANT'

def make_camera(shot):
 d=bpy.data.cameras.new(shot['id']);d.sensor_width=36;d.dof.use_dof=True;d.dof.aperture_fstop=5.6
 cam=bpy.data.objects.new('Camera '+shot['id'],d);bpy.context.collection.objects.link(cam);cam.rotation_mode='QUATERNION'
 focus=empty('Focus '+shot['id']);d.dof.focus_object=focus
 previous=None
 keys=shot.get('keys',[(shot['start'],shot['eye'],shot['target'],shot['lens']),(shot['end'],shot['end_eye'],shot.get('end_target',shot['target']),shot.get('end_lens',shot['lens']))])
 for t,p,target,lens in keys:
  cam.location=v(p);q=(v(target)-cam.location).to_track_quat('-Z','Y')
  if previous is not None and previous.dot(q)<0:q.negate()
  previous=q.copy();cam.rotation_quaternion=q;d.lens=lens
  cam.keyframe_insert('location',frame=frame(t));cam.keyframe_insert('rotation_quaternion',frame=frame(t));d.keyframe_insert('lens',frame=frame(t));move(focus,t,target)
 marker=bpy.context.scene.timeline_markers.new(shot['id'],frame=frame(shot['start']));marker.camera=cam
 return cam

def char_materials(cid,mesh,meta):
 folder=PROJECT/'Assets/Parasite/Art/Characters/Textures';result={}
 for source in list(mesh.data.materials):
  kind=source.name.split('.')[0]
  if kind not in ('Skin','Eyes','Hair','Cloth'):continue
  m=material(cid+' '+kind,(1,1,1),.55 if kind=='Skin' else .65);nt=m.node_tree;bs=nt.nodes.get('Principled BSDF');tx=nt.nodes.new('ShaderNodeTexImage')
  key={'Skin':'skin','Eyes':'eyes','Hair':'hairA','Cloth':'clothA'}[kind];tx.image=tex(folder/meta['textures'][key]);col=tx.outputs['Color']
  if kind=='Cloth':
   mask=nt.nodes.new('ShaderNodeTexImage');mask.image=tex(folder/meta['textures']['clothM'],True);sep=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(mask.outputs['Color'],sep.inputs['Color'])
   for region,key in [(1,'A'),(2,'B'),(3,'C')]:
    tint=meta['tints'].get(key)
    if not tint:continue
    mul=nt.nodes.new('ShaderNodeMath');mul.operation='MULTIPLY';mul.inputs[1].default_value=3;nt.links.new(sep.outputs[0],mul.inputs[0])
    sub=nt.nodes.new('ShaderNodeMath');sub.operation='SUBTRACT';sub.inputs[1].default_value=region;nt.links.new(mul.outputs[0],sub.inputs[0])
    ab=nt.nodes.new('ShaderNodeMath');ab.operation='ABSOLUTE';nt.links.new(sub.outputs[0],ab.inputs[0]);less=nt.nodes.new('ShaderNodeMath');less.operation='LESS_THAN';less.inputs[1].default_value=.5;nt.links.new(ab.outputs[0],less.inputs[0])
    mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[2].default_value=(*tint,1);nt.links.new(less.outputs[0],mix.inputs[0]);nt.links.new(col,mix.inputs[1]);col=mix.outputs[0]
   inv=nt.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;nt.links.new(sep.outputs[2],inv.inputs[1]);nt.links.new(inv.outputs[0],bs.inputs['Roughness']);nt.links.new(sep.outputs[1],bs.inputs['Metallic'])
   nm=nt.nodes.new('ShaderNodeTexImage');nm.image=tex(folder/meta['textures']['clothN'],True);normal=nt.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.65;nt.links.new(nm.outputs['Color'],normal.inputs['Color']);nt.links.new(normal.outputs[0],bs.inputs['Normal'])
  if kind=='Skin':
   bs.inputs['Subsurface Weight'].default_value=.12;bs.inputs['Subsurface Radius'].default_value=(1,.35,.2);bs.inputs['Subsurface Scale'].default_value=.01
   noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=180;bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.00025;bump.inputs['Strength'].default_value=.2;nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])
  if kind=='Hair':nt.links.new(tx.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Roughness'].default_value=.42
  if kind=='Eyes':bs.inputs['Roughness'].default_value=.12;bs.inputs['Coat Weight'].default_value=.3
  nt.links.new(col,bs.inputs['Base Color']);result[kind]=m
 # Preserve each original polygon material slot order.
 return result

class Actor:
 def __init__(self,cid,name,parent,p,yaw):
  meta=json.loads((PROJECT/'Assets/Parasite/Art/Characters/Models'/(cid+'.json')).read_text())
  source=Path.home()/'Data/parasite-work/characters/out'/(cid+'.blend')
  if source.is_file():
   with bpy.data.libraries.load(str(source),link=False) as (a,b):b.objects=[n for n in a.objects if n=='Armature' or n==cid+'_LOD0']
   for ob in b.objects:
    if ob:bpy.context.collection.objects.link(ob)
   self.rig=next(o for o in b.objects if o and o.type=='ARMATURE');self.mesh=next(o for o in b.objects if o and o.type=='MESH')
  else:
   before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(PROJECT/'Assets/Parasite/Art/Characters/Models'/(cid+'.fbx')))
   loaded=set(bpy.data.objects)-before;self.rig=next(o for o in loaded if o.type=='ARMATURE');self.mesh=next(o for o in loaded if o.type=='MESH' and o.name.endswith('_LOD0'))
   for ob in loaded:
    if ob not in (self.rig,self.mesh):bpy.data.objects.remove(ob,do_unlink=True)
  self.rig.name=name+' Rig';self.mesh.name=name+' Hero';self.rig.parent=parent;self.mesh.parent=self.rig
  slots=[m.name.split('.')[0] for m in self.mesh.data.materials];mats=char_materials(cid,self.mesh,meta)
  for i,kind in enumerate(slots):self.mesh.data.materials[i]=mats[kind]
  self.rig.location=v(p);self.rig.rotation_euler=(0,0,math.radians(yaw));self.parts=[self.rig,self.mesh];self.name=name
  self.rig.animation_data_clear()
  for pb in self.rig.pose.bones:pb.rotation_mode='QUATERNION'

_POSE_KEYS={}
def key_pose(arm,t):
 # Collect poses, then write complete F-curves in bulk. Per-bone keyframe_insert causes repeated dependency updates.
 record=_POSE_KEYS.setdefault(arm,{})
 for bone in arm.pose.bones:
  record.setdefault(bone.name,[]).append((frame(t),tuple(bone.rotation_quaternion),tuple(bone.location)))

def flush_pose_keys():
 for arm,bones in _POSE_KEYS.items():
  arm.animation_data_create()
  if arm.animation_data.action is None:arm.animation_data.action=bpy.data.actions.new(arm.name+' Film action')
  action=arm.animation_data.action
  for name,rows in bones.items():
   prefix=arm.pose.bones[name].path_from_id()
   for property_name,component_count,column in [('rotation_quaternion',4,1),('location',3,2)]:
    for component in range(component_count):
     fc=action.fcurves.new(prefix+'.'+property_name,index=component,action_group=name)
     fc.keyframe_points.add(len(rows));fc.keyframe_points.foreach_set('co',[number for row in rows for number in (row[0],row[column][component])])
     for point in fc.keyframe_points:point.interpolation='LINEAR'
     fc.update()
 _POSE_KEYS.clear()

def save_portable(path):
 bpy.context.view_layer.update()
 # Pack only live image users, not unused imported character material datablocks.
 for mat in list(bpy.data.materials):
  if mat.users==0:bpy.data.materials.remove(mat)
 for image in list(bpy.data.images):
  if image.users==0:bpy.data.images.remove(image)
 bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
 if Path(path).stat().st_size>=100_000_000:raise RuntimeError('Packed scene exceeds GitHub source-file limit: '+str(path))

def cpu_preview(path,seconds,camera=None,width=480,height=270,samples=8):
 s=bpy.context.scene;s.frame_set(frame(seconds))
 if camera:s.camera=bpy.data.objects.get(camera) if isinstance(camera,str) else camera
 s.cycles.device='CPU';s.cycles.samples=samples;s.cycles.denoiser='OPENIMAGEDENOISE';s.cycles.use_denoising=True
 if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
 s.render.threads_mode='FIXED';s.render.threads=2;s.render.resolution_x=width;s.render.resolution_y=height;s.render.resolution_percentage=100;s.render.filepath=str(path)
 bpy.ops.render.render(write_still=True)
