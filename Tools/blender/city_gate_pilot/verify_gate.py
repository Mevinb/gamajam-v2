"""CPU-only FBX round-trip, UV/winding, opaque panel and original AccessDoor transform contract checks."""
import bpy,hashlib,json,math,sys,zlib,struct
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parents[3];out=(P/(sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'ArtSource/CityGatePilot')).resolve()
assert out.is_relative_to(P/'ArtSource/CityGatePilot') or out.is_relative_to(P/'Logs')
manifest=json.loads((out/'production-manifest.json').read_text());contract=json.loads((out/'physical-contract.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for name,row in manifest['files'].items():assert sha(out/name)==row['sha256'],name
for path,digest in contract['source_sha256'].items():assert sha(P/path)==digest,('Existing builder/physics changed',path)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.threads_mode='FIXED';bpy.context.scene.render.threads=2
fbx=out/'Exports/CityGatePilot.fbx';bpy.ops.import_scene.fbx(filepath=str(fbx),use_image_search=False,use_anim=False)
objects=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'];assert len(objects)==2
expected={'GateStaticShell':([-8,-.15,-.4],[8,4.35,.4]),'GateMovingLeaf':([-1.45,-1.5,-.13],[1.45,1.5,.1])}
meshes={};records=[]
for ob in objects:
 name=next(key for key in expected if ob.name.startswith(key));mesh=ob.data;mesh.calc_loop_triangles();raw=[ob.matrix_world@v.co for v in mesh.vertices]
 points=[Vector((v.x,v.z,v.y)) for v in raw];faces=[tuple(tri.vertices) for tri in mesh.loop_triangles]
 low=[min(v[i] for v in points) for i in range(3)];high=[max(v[i] for v in points) for i in range(3)]
 error=max(abs(actual[i]-expected[name][edge][i]) for edge,actual in enumerate((low,high)) for i in range(3));assert error<.0002,(name,low,high)
 assert all(len(p.vertices)==3 for p in mesh.polygons);assert mesh.uv_layers and not ob.modifiers and ob.rigid_body is None
 assert [m.name for m in mesh.materials]==['CityGatePilot_SharedPBR']
 areas=[];uvareas=[];edges={};neighbors={i:set() for i in range(len(mesh.vertices))}
 for tri in mesh.loop_triangles:
  a,b,c=[mesh.vertices[i].co for i in tri.vertices];areas.append((b-a).cross(c-a).length*.5)
  u=[mesh.uv_layers.active.data[i].uv for i in tri.loops];uvareas.append(abs((u[1]-u[0]).cross(u[2]-u[0]))*.5)
  for a,b in zip(tri.vertices,(*tri.vertices[1:],tri.vertices[0])):
   edges.setdefault(tuple(sorted((a,b))),[]).append((a,b));neighbors[a].add(b);neighbors[b].add(a)
 assert min(areas)>1e-11 and min(uvareas)>1e-13
 assert all(math.isfinite(c) and 0<=c<=1 for item in mesh.uv_layers.active.data for c in item.uv)
 volume=sum(mesh.vertices[t.vertices[0]].co.dot(mesh.vertices[t.vertices[1]].co.cross(mesh.vertices[t.vertices[2]].co))/6 for t in mesh.loop_triangles);assert volume>0,(name,volume)
 assert all(len(pair)==2 and pair[0]==tuple(reversed(pair[1])) for pair in edges.values()),(name,'open edge or inconsistent winding')
 unseen=set(neighbors);components=[]
 while unseen:
  start=unseen.pop();connected={start};pending=[start]
  while pending:
   for vertex in neighbors[pending.pop()]:
    if vertex in unseen:unseen.remove(vertex);connected.add(vertex);pending.append(vertex)
  components.append(connected)
 component_volumes=[]
 for component in components:
  signed=sum(mesh.vertices[t.vertices[0]].co.dot(mesh.vertices[t.vertices[1]].co.cross(mesh.vertices[t.vertices[2]].co))/6 for t in mesh.loop_triangles if t.vertices[0] in component)
  assert signed>1e-12,(name,'inward or zero-volume solid',signed);component_volumes.append(signed)
 expected_tris=next(row['triangles'] for row in manifest['meshes'] if row['name']==name);assert len(faces)==expected_tris
 meshes[name]=(points,faces);records.append({'name':name,'imported_name':ob.name,'bounds_min':low,'bounds_max':high,'max_bounds_error':error,'triangles':len(faces),'uv_min_area':min(uvareas),'triangle_min_area':min(areas),'signed_volume':volume,'closed_outward_solids':len(components),'smallest_solid_volume':min(component_volumes),'all_edges_closed_consistently_wound':True,'materials':[m.name for m in mesh.materials],'modifiers':0,'rigid_body':False})
static_points,static_faces=meshes['GateStaticShell'];leaf_points,leaf_faces=meshes['GateMovingLeaf']
static_bvh=BVHTree.FromPolygons(static_points,static_faces,all_triangles=True)
def assembled(offset):
 vertices=static_points+[p+Vector((offset,1.5,0)) for p in leaf_points];faces=static_faces+[tuple(len(static_points)+i for i in f) for f in leaf_faces]
 return BVHTree.FromPolygons(vertices,faces,all_triangles=True)
closed,opened=assembled(0),assembled(3.05);clear=blocked=static_clear=0
for xi in range(17):
 for yi in range(19):
  x=-1.199+2.398*xi/16;y=.01+2.98*yi/18;origin=Vector((x,y,-1));direction=Vector((0,0,1))
  assert static_bvh.ray_cast(origin,direction,2)[0] is None,(x,y,'static intrudes opening');static_clear+=1
  assert opened.ray_cast(origin,direction,2)[0] is None,(x,y,'open leaf intrudes opening');clear+=1
  assert closed.ray_cast(origin,direction,2)[0] is not None,(x,y,'closed leaf has hole');blocked+=1
# Validate metre-authored child placement below the SAME scaled AccessDoor.panel at every animation sample.
transform_error=0;samples=[]
scale=contract['moving_panel']['original_local_scale'];inverse=[1/v for v in scale]
for i in range(21):
 t=i/20;e=t*t*(3-2*t);parent=Vector((3.05*e,1.5,0));error=0
 for point in leaf_points[::17]:
  inherited=Vector([point[j]*inverse[j]*scale[j]+parent[j] for j in range(3)]);error=max(error,(inherited-(point+parent)).length)
 transform_error=max(transform_error,error);samples.append({'t':t,'smoothstep':e,'panel_center':[parent.x,parent.y,parent.z],'max_transform_error':error})
assert transform_error<.000001
# Byte-level PNG integrity and exact dimensions: no image reauthoring/editing here.
textures=[]
for suffix in ('A','N','M'):
 p=out/'Textures'/('CityGatePilot_'+suffix+'.png');data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';index=8;chunks=0;width=height=None
 while index<len(data):
  length=struct.unpack('>I',data[index:index+4])[0];kind=data[index+4:index+8];payload=data[index+8:index+8+length];crc=struct.unpack('>I',data[index+8+length:index+12+length])[0]
  assert zlib.crc32(kind+payload)&0xffffffff==crc
  if kind==b'IHDR':width,height=struct.unpack('>II',payload[:8]);assert (width,height)==(2048,2048)
  index+=length+12;chunks+=1
  if kind==b'IEND':break
 assert index==len(data)
 textures.append({'path':str(p.relative_to(out)),'width':width,'height':height,'png_crc_chunks':chunks,'sha256':sha(p),'bytes':len(data)})
result={'schema':1,'status':'PASS CPU FBX/UV/winding/bounds and gate-motion mapping; Unity integration/native comparison pending','verifier_sha256':sha(__file__),'blender':bpy.app.version_string,'cpu_threads':2,'render_calls':0,'gpu_rendering':False,
 'fbx_sha256':sha(fbx),'source_blend_sha256':sha(out/'CityGatePilot.blend'),'meshes':records,'triangles':sum(r['triangles'] for r in records),'materials':1,
 'doorway':{'static_clear_rays':static_clear,'open_clear_rays':clear,'closed_opaque_rays':blocked,'x':[-1.199,1.199],'root_local_y':[.01,2.99],'original_open_offset':[3.05,0,0]},
 'motion':{'parent_scale':scale,'visual_child_scale':inverse,'samples':samples,'max_transform_error':transform_error,'animation_or_controller_added':False},
 'textures':textures,'original_source_hashes_unchanged':True,'new_colliders':0,'limits':['FBX round-trip uses the established project export boundary; actual Unity import remains unrun.','No scene generation, collider, LOS or NavMesh mutation occurred.','No native gameplay visual, performance or subjective improvement claim.']}
(out/'export-verification.json').write_text(json.dumps(result,indent=2)+'\n')
print('[gate-pilot] ROUNDTRIP PASS',result['triangles'],'triangles;',clear,'open /',blocked,'closed rays;21motion samples,0newphysics, noUnity/GPU',flush=True)
