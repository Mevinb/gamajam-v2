"""CPU-only FBX round-trip for the opaque quarantine kit. No rendering or live publication."""
import bpy,json,math,os,sys,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
project,stage=map(lambda p:Path(p).resolve(),sys.argv[sys.argv.index('--')+1:])
source=stage/'staged/ArtSource/Island';manifest=json.loads((source/'quarantine-wall-manifest.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.threads_mode='FIXED';bpy.context.scene.render.threads=2
fbx=stage/'staged/Assets/Parasite/Resources/Island/IslandQuarantineWalls.fbx'
bpy.ops.import_scene.fbx(filepath=str(fbx),use_image_search=False,use_anim=False)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.context.view_layer.update();assert len(objects)==2
expected={'QuarantineFacade':([-53,.15,-18.2],[53,3.75,-17.8]),'SouthernQuayFacade':([-52,.15,-38],[52,1.35,-37.4])}
all_vertices=[];all_faces=[];records=[];total=0
for obj in objects:
 name=next(k for k in expected if obj.name.startswith(k));mesh=obj.data;mesh.calc_loop_triangles()
 raw=[obj.matrix_world@v.co for v in mesh.vertices]
 # Actual Blender→Unity import mapping measured on the current project's asymmetric assets.
 city=[Vector((v.x,v.z,v.y)) for v in raw]
 low=[min(v[i] for v in city) for i in range(3)];high=[max(v[i] for v in city) for i in range(3)]
 error=max(abs(actual[i]-expected[name][edge][i]) for edge,actual in enumerate((low,high)) for i in range(3));assert error<.0002,(name,low,high)
 assert all(len(p.vertices)==3 for p in mesh.polygons)
 assert mesh.uv_layers and all(math.isfinite(c) for uv in mesh.uv_layers.active.data for c in uv.uv)
 uvareas=[]
 for tri in mesh.loop_triangles:
  a,b,c=[mesh.vertices[i].co for i in tri.vertices];assert (b-a).cross(c-a).length*.5>1e-10
  q=[mesh.uv_layers.active.data[i].uv for i in tri.loops];uvareas.append(abs((q[1]-q[0]).cross(q[2]-q[0]))*.5)
 assert min(uvareas)>1e-13
 signed=sum(mesh.vertices[t.vertices[0]].co.dot(mesh.vertices[t.vertices[1]].co.cross(mesh.vertices[t.vertices[2]].co))/6 for t in mesh.loop_triangles);assert signed>0,(name,signed)
 assert [m.name for m in mesh.materials]==['IslandQuarantineConcrete']
 assert not obj.modifiers
 base=len(all_vertices);all_vertices.extend(city);all_faces.extend(tuple(base+i for i in tri.vertices) for tri in mesh.loop_triangles)
 total+=len(mesh.loop_triangles)
 records.append(dict(mesh=name,imported_name=obj.name,bounds_min=low,bounds_max=high,bounds_max_error=error,triangles=len(mesh.loop_triangles),uv_min_area=min(uvareas),zero_area_triangles=0,signed_volume=signed,material_slots=[m.name for m in mesh.materials]))
assert total==manifest['after']['triangles'] and total<=12000
bvh=BVHTree.FromPolygons(all_vertices,all_faces,all_triangles=True);openings=[]
for opening in manifest['openings']:
 x0,x1=opening['x'];y0,y1=opening['y'];z=-37.7 if opening['name']=='arrival-gangway' else -18;count=0
 for xi in range(13):
  for yi in range(13):
   x=x0+.0002+(x1-x0-.0004)*xi/12;y=y0+.0002+(y1-y0-.0004)*yi/12
   assert bvh.ray_cast(Vector((x,y,z-.8)),Vector((0,0,1)),1.6)[0] is None,(opening['name'],x,y)
   count+=1
 # Each mirrored +X counterpart is solid, catching X reflection even when aggregate bounds are symmetric.
 probe=Vector((-(x0+x1)/2,(y0+y1)/2,z-.8))
 assert bvh.ray_cast(probe,Vector((0,0,1)),1.6)[0] is not None,('unexpected X reflection',opening['name'])
 openings.append(dict(name=opening['name'],clear_rays=count,positive_x_mirror_solid=True))
# Recheck six-sided opaque coverage for every original box against actual exported triangles.
coverage=[]
for wall in manifest['walls']:
 count=0
 for axis in range(3):
  a,b=[i for i in range(3) if i!=axis]
  for sign in (-1,1):
   for u in (.0002,.25,.5,.75,.9998):
    for v in (.0002,.25,.5,.75,.9998):
     point=list(wall['center']);point[axis]+=sign*(wall['size'][axis]/2+.3)
     point[a]=wall['bounds_min'][a]+u*wall['size'][a];point[b]=wall['bounds_min'][b]+v*wall['size'][b]
     direction=Vector((0,0,0));direction[axis]=-sign
     hit=bvh.ray_cast(Vector(point),direction,wall['size'][axis]+.6)[0];assert hit is not None,(wall['id'],axis,sign,u,v)
     count+=1
 coverage.append(dict(wall=wall['id'],opaque_rays=count,opaque_hits=count))
result=dict(status='PASS CPU FBX round-trip; actual Unity visuals/performance still pending',blender_version=bpy.app.version_string,threads=2,gpu_rendering=False,source_fbx_sha256=hashlib.sha256(fbx.read_bytes()).hexdigest(),meshes=records,total_triangles=total,openings=openings,opaque_coverage=coverage)
(source/'quarantine-wall-export-verification.json').write_text(json.dumps(result,indent=2)+'\n');(stage/'evidence/export-verification.json').write_text(json.dumps(result,indent=2)+'\n')
print('[wall-kit] ROUNDTRIP PASS',total,'triangles,2meshes,1material, exact asymmetric openings and opaque backing; no Unity/rendering',flush=True)
