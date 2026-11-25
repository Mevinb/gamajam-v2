"""CPU-only packed-source and FBX round-trip checks for the visual security-booth replacement."""
import bpy,hashlib,json,math,struct,sys,zlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parents[3]
OUT=(P/(sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'ArtSource/CitySecurityBooth')).resolve()
assert OUT.is_relative_to(P/'ArtSource/CitySecurityBooth') or OUT.is_relative_to(P/'Logs')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
manifest=json.loads((OUT/'production-manifest.json').read_text());contract=json.loads((OUT/'physical-contract.json').read_text())
for rel,row in manifest['files'].items():assert sha(OUT/rel)==row['sha256'],rel
for row in manifest['source_atlas']['textures'].values():assert sha(P/row['path'])==row['sha256']
assert sha(P/manifest['source_atlas']['gate_blend']['path'])==manifest['source_atlas']['gate_blend']['sha256']
# Inspect the real editable packed source before discarding it for independent FBX import.
bpy.ops.wm.open_mainfile(filepath=str(OUT/'CitySecurityBooth.blend'))
s=bpy.context.scene
assert s.render.engine=='CYCLES' and s.cycles.device=='CPU' and s.render.threads==2 and s.render.threads_mode=='FIXED'
assert s.cycles.samples==8 and (s.render.resolution_x,s.render.resolution_y)==(960,720)
assert not getattr(s.cycles,'denoising_use_gpu',False)
visual=bpy.data.collections.get('VISUAL EXPORT ONLY - no colliders');assert visual
source_objects=[o for o in visual.objects if o.type=='MESH'];assert len(source_objects)==1 and source_objects[0].name.startswith('SecurityBoothShell')
source=source_objects[0];assert source.location.length<1e-7 and max(abs(v) for v in source.rotation_euler)<1e-7 and max(abs(v-1) for v in source.scale)<1e-7
assert len(source.data.materials)==1 and source.data.materials[0].name=='CityGatePilot_SharedPBR'
bs=source.data.materials[0].node_tree.nodes.get('Principled BSDF');assert bs.inputs['Emission Strength'].default_value==0 and bs.inputs['Transmission Weight'].default_value==0 and bs.inputs['Alpha'].default_value==1
packed=[]
for node in source.data.materials[0].node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  im=node.image;suffix=im.name.rsplit('_',1)[-1];assert im.packed_file
  digest=hashlib.sha256(bytes(im.packed_file.data)).hexdigest();assert digest==manifest['source_atlas']['textures'][suffix]['sha256']
  packed.append({'name':im.name,'packed_bytes':im.packed_file.size,'sha256':digest,'original_source_unchanged':True})
assert len(packed)==3
source_proof={'objects':len(source_objects),'material':'CityGatePilot_SharedPBR','packed_original_images':packed,'opaque':True,'emission_strength':0,'transmission_weight':0,'root_identity':True,'cpu_threads':s.render.threads,'samples':s.cycles.samples,'resolution':[s.render.resolution_x,s.render.resolution_y],'denoising_gpu':False}
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.threads_mode='FIXED';bpy.context.scene.render.threads=2
fbx=OUT/'Exports/CitySecurityBooth.fbx';bpy.ops.import_scene.fbx(filepath=str(fbx),use_image_search=False,use_anim=False)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==1
ob=objects[0];assert ob.name.startswith('SecurityBoothShell');mesh=ob.data;mesh.calc_loop_triangles()
assert len(mesh.materials)==1 and mesh.materials[0].name=='CityGatePilot_SharedPBR'
assert len(mesh.loop_triangles)==manifest['mesh']['triangles']<7000 and not ob.modifiers and ob.rigid_body is None and ob.animation_data is None
assert mesh.uv_layers and all(len(p.vertices)==3 and p.material_index==0 for p in mesh.polygons)
assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
raw=[ob.matrix_world@v.co for v in mesh.vertices];unity=[Vector((p.x,p.z,p.y)) for p in raw]
low=[min(p[i] for p in unity) for i in range(3)];high=[max(p[i] for p in unity) for i in range(3)]
expected=contract['actual_visual_local_bounds'];max_bounds_error=max(abs(v[i]-expected[e][i]) for e,v in enumerate((low,high)) for i in range(3));assert max_bounds_error<.0002,(low,high)
assert all(low[i]>=contract['allowed_visual_local_bounds'][0][i]-.0002 and high[i]<=contract['allowed_visual_local_bounds'][1][i]+.0002 for i in range(3))
areas=[];uvareas=[];edges={};neighbors={i:set() for i in range(len(mesh.vertices))};faces=[];tiles=set()
for tri in mesh.loop_triangles:
 indices=tuple(tri.vertices);faces.append(indices);a,b,c=[mesh.vertices[i].co for i in indices];areas.append((b-a).cross(c-a).length*.5)
 u=[mesh.uv_layers.active.data[i].uv for i in tri.loops];uvareas.append(abs((u[1]-u[0]).cross(u[2]-u[0]))*.5)
 assert all(math.isfinite(c) and 0<=c<=1 for uv in u for c in uv)
 tile={int(uv.x>=.5)+2*int(uv.y>=.5) for uv in u};assert len(tile)==1;tiles.update(tile)
 for a,b in zip(indices,(*indices[1:],indices[0])):edges.setdefault(tuple(sorted((a,b))),[]).append((a,b));neighbors[a].add(b);neighbors[b].add(a)
assert min(areas)>1e-11 and min(uvareas)>1e-13
assert all(len(pair)==2 and pair[0]==tuple(reversed(pair[1])) for pair in edges.values()),'Every solid must be closed and consistently wound.'
unseen=set(neighbors);components=[]
while unseen:
 seed=unseen.pop();component={seed};todo=[seed]
 while todo:
  for vertex in neighbors[todo.pop()]:
   if vertex in unseen:unseen.remove(vertex);component.add(vertex);todo.append(vertex)
 components.append(component)
# Translation-relative scalar-double volume avoids cancellation for millimetre fasteners away from the origin.
def signed_volume(component):
 center=tuple(sum(float(mesh.vertices[i].co[j]) for i in component)/len(component) for j in range(3));total=0
 for tri in mesh.loop_triangles:
  if tri.vertices[0] not in component:continue
  a,b,c=[tuple(float(mesh.vertices[i].co[j])-center[j] for j in range(3)) for i in tri.vertices]
  total+=a[0]*(b[1]*c[2]-b[2]*c[1])+a[1]*(b[2]*c[0]-b[0]*c[2])+a[2]*(b[0]*c[1]-b[1]*c[0])
 return total/6
volumes=[signed_volume(c) for c in components]
if not all(v>1e-12 for v in volumes):
 diagnostic=[]
 for component,volume in zip(components,volumes):
  if volume<=1e-12:
   points=[unity[i] for i in component]
   diagnostic.append({'volume':volume,'vertices':len(component),'minimum_vertex':min(component),'bounds_min':[min(p[j] for p in points) for j in range(3)],'bounds_max':[max(p[j] for p in points) for j in range(3)]})
 (OUT/'failed-solid-diagnostic.json').write_text(json.dumps(diagnostic,indent=2)+'\n')
assert all(v>1e-12 for v in volumes),('Non-outward or zero-volume solid',min(volumes))
assert len(components)==manifest['closed_solid_pieces']
bvh=BVHTree.FromPolygons(unity,faces,all_triangles=True);opaque_front=opaque_sides=opaque_rear=0
for xi in range(13):
 for yi in range(16):
  x=-1.20+2.40*xi/12;y=.08+2.78*yi/15
  assert bvh.ray_cast(Vector((x,y,-2)),Vector((0,0,1)),4)[0] is not None;opaque_front+=1
  assert bvh.ray_cast(Vector((x,y,2)),Vector((0,0,-1)),4)[0] is not None;opaque_rear+=1
for zi in range(15):
 for yi in range(16):
  z=-1.45+2.90*zi/14;y=.08+2.78*yi/15
  for sign in (-1,1):assert bvh.ray_cast(Vector((sign*2,y,z)),Vector((-sign,0,0)),4)[0] is not None;opaque_sides+=1
previews=[]
for row in manifest['cpu_previews']:
 p=OUT/row['file'];data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';index=8;chunks=0
 while index<len(data):
  length=struct.unpack('>I',data[index:index+4])[0];kind=data[index+4:index+8];body=data[index+8:index+8+length];crc=struct.unpack('>I',data[index+8+length:index+12+length])[0];assert zlib.crc32(kind+body)&0xffffffff==crc
  if kind==b'IHDR':assert struct.unpack('>II',body[:8])==(960,720)
  index+=length+12;chunks+=1
  if kind==b'IEND':break
 assert index==len(data);previews.append({'path':row['file'],'sha256':sha(p),'bytes':len(data),'png_crc_chunks':chunks,'dimensions':[960,720]})
assert len(previews)==2
result={'schema':1,'status':'PASS_CPU_PACKED_SOURCE_AND_FBX_ROUNDTRIP','blender':bpy.app.version_string,'cpu_threads':2,'render_calls':0,'gpu_rendering':False,'verifier_sha256':sha(__file__),'fbx_sha256':sha(fbx),'blend_sha256':sha(OUT/'CitySecurityBooth.blend'),'packed_source':source_proof,'mesh':{'name':ob.name,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'bounds_min_unity':low,'bounds_max_unity':high,'max_bounds_error':max_bounds_error,'minimum_triangle_area':min(areas),'minimum_uv_area':min(uvareas),'atlas_tiles_used':sorted(tiles),'closed_outward_solids':len(components),'minimum_solid_volume':min(volumes),'all_edges_two_consistently_wound_faces':True,'materials':[m.name for m in mesh.materials],'modifiers':0,'rigid_body':False,'animation':False},'opaque_surface_rays':{'front':opaque_front,'rear':opaque_rear,'sides':opaque_sides},'previews':previews,'new_textures':0,'new_colliders':0,'original_source_hashes_unchanged':True,'limits':['CPU authoring previews are not native gameplay quality or performance evidence.','Existing collider, navigation, LOS, SECURITY text and scripts were not touched. Actual Unity import/integration remains pending.','Each manufactured detail is a closed solid; overlapping detail pieces are not claimed to be one boolean-unioned volume.','Uses the established gate project export handedness boundary; actual Unity culling/material reassignment must still be verified.']}
(OUT/'export-verification.json').write_text(json.dumps(result,indent=2)+'\n');print('[security-booth] VERIFY PASS',len(mesh.loop_triangles),'triangles;',len(components),'closed outward solids;',opaque_front+opaque_rear+opaque_sides,'opaque rays; CPU2/GPUfalse, no new textures/colliders',flush=True)
