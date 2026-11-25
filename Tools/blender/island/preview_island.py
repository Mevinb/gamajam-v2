"""CPU-only still reviews of real staged meshes. Cutaways are authoring inspections, not gameplay captures."""
import bpy,bmesh,sys,os,math
from pathlib import Path
from mathutils import Vector
stage=Path(sys.argv[sys.argv.index('--')+1]).resolve()
source=stage/'ArtSource/Island/IslandArchitecture.blend';output=stage/'previews';output.mkdir(exist_ok=True)
def v(p):return Vector((p[0],p[2],p[1]))
shots=[('IslandRefugeClinic','clinic-cutaway', (15,15,-17),(0,1,0),3.15,22),('IslandTidalCulvert','culvert-interior',(0,.63,-2.8),(0,.57,6),None,None),('IslandHandoffGate','handoff-gate',(5,3.8,-7),(0,1.2,0),None,7),('IslandFlareCache','flare-case',(2.2,1.6,-2.4),(0,.4,0),None,2.5)]
for asset,name,eye,target,cut,ortho in shots:
 bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
 source_materials={material.name:material for material in bpy.data.materials}
 for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
 bpy.ops.import_scene.fbx(filepath=str(stage/'Resources/Island'/(asset+'.fbx')),use_image_search=False,use_anim=False)
 selected=[obj for obj in scene.objects if obj.type=='MESH']
 for obj in selected:
  for slot in obj.material_slots:
   base=slot.material.name.rsplit('.',1)[0] if slot.material and slot.material.name.rsplit('.',1)[-1].isdigit() else slot.material.name
   if base in source_materials:slot.material=source_materials[base]
 bpy.context.view_layer.update()
 if cut is not None:
  for obj in selected:
   bm=bmesh.new();bm.from_mesh(obj.data)
   remove=[face for face in bm.faces if all((obj.matrix_world@vert.co).z>cut for vert in face.verts)]
   if remove:bmesh.ops.delete(bm,geom=remove,context='FACES_ONLY')
   bm.to_mesh(obj.data);bm.free();obj.data.update()
 scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=False
 scene.render.threads_mode='FIXED';scene.render.threads=2
 scene.render.resolution_x=960;scene.render.resolution_y=640;scene.render.resolution_percentage=100
 scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.22,.26,.31,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7
 for label,pos,energy,size in [('Soft overcast',(3,12,-5),1800,10),('Warm shore fill',(-7,6,3),900,8)]:
  data=bpy.data.lights.new(label,'AREA');data.energy=energy;data.shape='DISK';data.size=size
  ob=bpy.data.objects.new(label,data);scene.collection.objects.link(ob);ob.location=v(pos);ob.rotation_euler=(v(target)-ob.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('Review camera');ob=bpy.data.objects.new('Review camera',data);scene.collection.objects.link(ob);ob.location=v(eye);ob.rotation_euler=(v(target)-ob.location).to_track_quat('-Z','Y').to_euler();data.lens=32;data.clip_start=.03;data.clip_end=100
 if ortho:data.type='ORTHO';data.ortho_scale=ortho
 scene.camera=ob;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(output/(name+'.png'))
 bpy.ops.render.render(write_still=True)
 print('[island-preview] CPU_RENDERED',name,'cutaway='+str(cut is not None),flush=True)
