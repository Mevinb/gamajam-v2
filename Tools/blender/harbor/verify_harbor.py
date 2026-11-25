"""CPU-only independent packed-source and nine FBX roundtrip verification. No Unity/GPU/render calls."""
import bpy,hashlib,json,math,sys,struct,zlib
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parents[3];OUT=(P/(sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'ArtSource/Harbor')).resolve()
assert OUT.is_relative_to(P/'ArtSource/Harbor') or OUT.is_relative_to(P/'Logs')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
m=json.loads((OUT/'production-manifest.json').read_text())
for rel,r in m['files'].items():assert sha(OUT/rel)==r['sha256'],rel
for r in m['source_atlas']['textures'].values():assert sha(P/r['path'])==r['sha256']
assert sha(P/m['source_atlas']['gate_blend']['path'])==m['source_atlas']['gate_blend']['sha256']
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Harbor.blend'));s=bpy.context.scene
assert s.render.engine=='CYCLES' and s.cycles.device=='CPU' and s.render.threads_mode=='FIXED' and s.render.threads==2 and s.cycles.samples==8
assert (s.render.resolution_x,s.render.resolution_y)==(960,720) and not getattr(s.cycles,'denoising_use_gpu',False)
visual=bpy.data.collections.get('HARBOR VISUAL EXPORTS - no physics');assert visual and len(visual.objects)==9
assert all(o.location.length<1e-7 and max(abs(v) for v in o.rotation_euler)<1e-7 and max(abs(v-1) for v in o.scale)<1e-7 for o in visual.objects)
packed=[]
for node in bpy.data.materials['CityGatePilot_SharedPBR'].node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  im=node.image;assert im.packed_file;digest=hashlib.sha256(bytes(im.packed_file.data)).hexdigest();assert digest==m['source_atlas']['textures'][im.name.rsplit('_',1)[-1]]['sha256'];packed.append({'name':im.name,'sha256':digest,'bytes':im.packed_file.size})
assert len(packed)==3
bs=bpy.data.materials['CityGatePilot_SharedPBR'].node_tree.nodes.get('Principled BSDF');assert bs.inputs['Emission Strength'].default_value==0 and bs.inputs['Transmission Weight'].default_value==0
records=[]
for expected in m['exports']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.threads=2;bpy.context.scene.render.threads_mode='FIXED'
 bpy.ops.import_scene.fbx(filepath=str(OUT/expected['file']),use_image_search=False,use_anim=False)
 objects=list(bpy.context.scene.objects);assert len(objects)==1 and objects[0].type=='MESH'
 ob=objects[0];mesh=ob.data;mesh.calc_loop_triangles();assert ob.name.startswith(expected['name'])
 assert ob.location.length<1e-6 and max(abs(v-1) for v in ob.scale)<1e-5 and ob.matrix_world.determinant()>0 # importer axis rotation is accounted in world bounds
 assert not ob.modifiers and not ob.animation_data and ob.rigid_body is None and len(mesh.materials)==1 and mesh.materials[0].name=='CityGatePilot_SharedPBR'
 assert len(mesh.loop_triangles)==expected['triangles'] and all(len(p.vertices)==3 for p in mesh.polygons)
 raw=[ob.matrix_world@v.co for v in mesh.vertices];points=[Vector((p.x,p.z,p.y)) for p in raw]
 low=[min(p[i] for p in points) for i in range(3)];high=[max(p[i] for p in points) for i in range(3)]
 assert max(abs(v[i]-expected['bounds'][b][i]) for b,v in enumerate((low,high)) for i in range(3))<.0002
 assert all(math.isfinite(v) for p in points for v in p)
 edges={};neighbors={i:set() for i in range(len(mesh.vertices))};areas=[];uvareas=[];tiles=set()
 for tri in mesh.loop_triangles:
  indices=tuple(tri.vertices);a,b,c=[mesh.vertices[i].co for i in indices];areas.append((b-a).cross(c-a).length*.5)
  u=[mesh.uv_layers.active.data[i].uv for i in tri.loops];uvareas.append(abs((u[1]-u[0]).cross(u[2]-u[0]))*.5)
  assert all(math.isfinite(c) and 0<=c<=1 for uv in u for c in uv)
  tile={int(uv.x>=.5)+2*int(uv.y>=.5) for uv in u};assert len(tile)==1;tiles.update(tile)
  for a,b in zip(indices,(*indices[1:],indices[0])):edges.setdefault(tuple(sorted((a,b))),[]).append((a,b));neighbors[a].add(b);neighbors[b].add(a)
 assert min(areas)>1e-11 and min(uvareas)>1e-13 and all(len(pair)==2 and pair[0]==tuple(reversed(pair[1])) for pair in edges.values())
 unseen=set(neighbors);volumes=[]
 while unseen:
  seed=unseen.pop();component={seed};todo=[seed]
  while todo:
   for vertex in neighbors[todo.pop()]:
    if vertex in unseen:unseen.remove(vertex);component.add(vertex);todo.append(vertex)
  center=tuple(sum(float(mesh.vertices[i].co[j]) for i in component)/len(component) for j in range(3));total=0
  for tri in mesh.loop_triangles:
   if tri.vertices[0] not in component:continue
   a,b,c=[tuple(float(mesh.vertices[i].co[j])-center[j] for j in range(3)) for i in tri.vertices]
   total+=a[0]*(b[1]*c[2]-b[2]*c[1])+a[1]*(b[2]*c[0]-b[0]*c[2])+a[2]*(b[0]*c[1]-b[1]*c[0])
  volumes.append(total/6)
 assert all(v>1e-12 for v in volumes),(expected['name'],min(volumes))
 assert len(volumes)==expected['closed_solids']
 records.append({'name':ob.name,'triangles':len(mesh.loop_triangles),'bounds':[low,high],'closed_outward_solids':len(volumes),'minimum_signed_volume':min(volumes),'minimum_triangle_area':min(areas),'minimum_uv_area':min(uvareas),'tiles':sorted(tiles),'sha256':sha(OUT/expected['file'])})
previews=[]
for row in m['cpu_previews']:
 p=OUT/row['file'];data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';i=8
 while i<len(data):
  length=struct.unpack('>I',data[i:i+4])[0];kind=data[i+4:i+8];body=data[i+8:i+8+length];crc=struct.unpack('>I',data[i+8+length:i+12+length])[0];assert zlib.crc32(kind+body)&0xffffffff==crc
  if kind==b'IHDR':assert struct.unpack('>II',body[:8])==(960,720)
  i+=length+12
  if kind==b'IEND':break
 assert i==len(data);previews.append({'file':row['file'],'sha256':sha(p),'width':960,'height':720})
assert len(previews)==4 and len(records)==9 and sum(r['triangles'] for r in records)==m['distinct_triangles']<18000
result={'schema':1,'status':'PASS_CPU_SOURCE_AND_NINE_FBX_ROUNDTRIPS','verifier_sha256':sha(__file__),'blender':bpy.app.version_string,'cpu_threads':2,'gpu_calls':0,'render_calls':0,'packed_original_images':packed,'exports':records,'distinct_triangles':m['distinct_triangles'],'closed_outward_solids':sum(r['closed_outward_solids'] for r in records),'previews':previews,'new_textures':0,'new_imported_physics':0,'limits':['Unity import, generated district navigation/LOS, physical gameplay and native appearance/performance remain separate unrun checks.','Each manufactured detail is a closed solid; overlapping components are not claimed as a boolean-unioned volume.','Four images show CPU authoring setups, not rendered gameplay.']}
(OUT/'export-verification.json').write_text(json.dumps(result,indent=2)+'\n');print('[Harbor] VERIFY PASS',len(records),'FBXs /',m['distinct_triangles'],'distinct triangles /',result['closed_outward_solids'],'closed outward solids / CPU2',flush=True)
