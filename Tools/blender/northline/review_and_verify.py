"""Independent rail FBX round trips and brighter CPU-only reviews. Writes only fresh Logs outputs."""
import argparse,bpy,hashlib,json,math,struct,sys,time,traceback
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);ap.add_argument('--source',type=Path,required=True);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
P=a.project.resolve();SOURCE=a.source.resolve();OUT=a.out.resolve();assert (SOURCE.is_relative_to(P/'Logs/rail-author-runs') or SOURCE==P/'ArtSource/NorthlineRail') and OUT.is_relative_to(P/'Logs') and not OUT.exists();OUT.mkdir(parents=True);(OUT/'Review').mkdir()
sha=lambda q:hashlib.sha256(Path(q).read_bytes()).hexdigest()
manifest=json.loads((SOURCE/'production-manifest.json').read_text());assert len(manifest['exports'])==11 and manifest['distinct_triangles']==7336
inputs={str(q):sha(q) for q in SOURCE.rglob('*') if q.is_file()};inputs.update({str(P/r['path']):sha(P/r['path']) for r in manifest['source_atlas']['textures'].values()});inputs[str(P/manifest['source_atlas']['gate_blend']['path'])]=sha(P/manifest['source_atlas']['gate_blend']['path'])
# The promoted source package omits only provenance-only author review PNGs; their original hashes remain in the manifest.
omitted_original_review_images=[]
for f,row in manifest['files'].items():
 if f.startswith('Review/') and f.endswith('.png') and not (SOURCE/f).exists():omitted_original_review_images.append({'file':f,'sha256':row['sha256'],'scope':'Original authoring review remains outside the promoted source package.'});continue
 assert sha(SOURCE/f)==row['sha256']
for row in manifest['source_atlas']['textures'].values():assert sha(P/row['path'])==row['sha256']
result={'schema':1,'status':'RUNNING','script_sha256':sha(__file__),'source_manifest_sha256':sha(SOURCE/'production-manifest.json'),'blender':bpy.app.version_string,'roundtrip':[],'reviews':[],'omitted_original_review_images':omitted_original_review_images,'limits':['Uses the established gate/booth project FBX boundary: imported world(x,z,y) to Unity. Alternate axis error is recorded, not selected to force a pass.','No Unity importer/native graphics/physics validation. Review images use studio lighting, not game lighting.','Distinct manufactured pieces may overlap; closed-component checks do not claim a boolean-unioned mesh.']}
def save(): (OUT/'review-verification.json').write_text(json.dumps(result,indent=2)+'\n')
def need(x,message):
 if not x:raise AssertionError(message)
def roundtrip(row):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.threads_mode='FIXED';bpy.context.scene.render.threads=2
 bpy.ops.import_scene.fbx(filepath=str(SOURCE/row['file']),use_image_search=False,use_anim=False)
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];rec={'name':row['name'],'imported_object_types':[(o.name,o.type) for o in bpy.context.scene.objects],'mesh_count':len(meshes),'fbx_sha256':sha(SOURCE/row['file'])};result['roundtrip'].append(rec)
 need(len(meshes)==1,'Exactly one imported mesh per FBX required');ob=meshes[0];mesh=ob.data;mesh.calc_loop_triangles();need(ob.name.startswith(row['name']),'Semantic mesh name mismatch')
 raw=[ob.matrix_world@v.co for v in mesh.vertices];points=[Vector((v.x,v.z,v.y)) for v in raw];alternate=[Vector((v.x,v.z,-v.y)) for v in raw]
 def bounds(ps):return [[min(v[i] for v in ps) for i in range(3)],[max(v[i] for v in ps) for i in range(3)]]
 actual=bounds(points);alt=bounds(alternate);error=max(abs(actual[e][i]-row['bounds_unity'][e][i]) for e in range(2) for i in range(3));alt_error=max(abs(alt[e][i]-row['bounds_unity'][e][i]) for e in range(2) for i in range(3))
 rec.update(imported_name=ob.name,vertices=len(mesh.vertices),triangles=len(mesh.loop_triangles),bounds_unity=actual,max_bounds_error=error,alternate_x_z_minus_y_error=alt_error,axis_ambiguity='Both transforms match symmetric bounds; established project mapping remains required.' if alt_error<.0002 else 'Asymmetric bounds distinguish the established mapping.',materials=[m.name if m else None for m in mesh.materials])
 need(error<.0002,'Established axis bounds mismatch: '+str(actual));need(len(mesh.loop_triangles)==row['triangles'],'Exact source triangle mismatch');need(len(mesh.vertices)==row['vertices'],'Exact source vertex mismatch')
 need(len(mesh.materials)==1 and mesh.materials[0] and mesh.materials[0].name=='CityGatePilot_SharedPBR','Single shared material semantic mismatch')
 need(not ob.modifiers and ob.rigid_body is None and all(o.type in ('MESH','EMPTY') and o.animation_data is None and o.rigid_body is None for o in bpy.context.scene.objects),'Imported animation/physics/camera/light/modifier present')
 need(mesh.uv_layers and all(len(poly.vertices)==3 and poly.material_index==0 for poly in mesh.polygons),'Triangulated single material UV mesh required');need(all(math.isfinite(c) for v in mesh.vertices for c in v.co),'Nonfinite coordinate')
 areas=[];uvareas=[];edges={};neighbors={i:set() for i in range(len(mesh.vertices))};normal_dots=[];tiles=set()
 for tri in mesh.loop_triangles:
  ii=tuple(tri.vertices);v=[mesh.vertices[i].co for i in ii];areas.append((v[1]-v[0]).cross(v[2]-v[0]).length*.5);uv=[mesh.uv_layers.active.data[i].uv for i in tri.loops];uvareas.append(abs((uv[1]-uv[0]).cross(uv[2]-uv[0]))*.5)
  need(all(math.isfinite(c) and 0<=c<=1 for q in uv for c in q),'Nonfinite/out-of-atlas UV');tile={int(q.x>=.5)+2*int(q.y>=.5) for q in uv};need(len(tile)==1,'Triangle crosses atlas tile');tiles.update(tile)
  for loop in tri.loops:
   n=mesh.corner_normals[loop].vector;need(all(math.isfinite(c) for c in n) and abs(n.length-1)<.001,'Invalid imported corner normal');normal_dots.append(n.dot(tri.normal))
  for v1,v2 in zip(ii,(*ii[1:],ii[0])):edges.setdefault(tuple(sorted((v1,v2))),[]).append((v1,v2));neighbors[v1].add(v2);neighbors[v2].add(v1)
 need(min(areas)>1e-11 and min(uvareas)>1e-13,'Degenerate geometric or UV triangle');need(min(normal_dots)>.1,'Imported shading normal opposes triangle winding');need(all(len(pair)==2 and pair[0]==tuple(reversed(pair[1])) for pair in edges.values()),'Open/nonmanifold/inconsistent edge')
 unseen=set(neighbors);components=[]
 while unseen:
  seed=unseen.pop();component={seed};todo=[seed]
  while todo:
   for vertex in neighbors[todo.pop()]:
    if vertex in unseen:unseen.remove(vertex);component.add(vertex);todo.append(vertex)
  components.append(component)
 def signed_volume(component):
  center=tuple(sum(float(mesh.vertices[i].co[j]) for i in component)/len(component) for j in range(3));total=0
  for tri in mesh.loop_triangles:
   if tri.vertices[0] not in component:continue
   v=[tuple(float(mesh.vertices[i].co[j])-center[j] for j in range(3)) for i in tri.vertices];aa,bb,cc=v;total+=aa[0]*(bb[1]*cc[2]-bb[2]*cc[1])+aa[1]*(bb[2]*cc[0]-bb[0]*cc[2])+aa[2]*(bb[0]*cc[1]-bb[1]*cc[0])
  return total/6
 volumes=[signed_volume(c) for c in components];rec.update(components=len(components),expected_components=row['closed_solids'],minimum_signed_volume=min(volumes),minimum_triangle_area=min(areas),minimum_uv_area=min(uvareas),minimum_corner_normal_dot=min(normal_dots),atlas_tiles=sorted(tiles),all_edges_closed_consistently_wound=True,all_solids_outward=all(v>1e-12 for v in volumes))
 need(len(components)==row['closed_solids'],'Authored independent-solid count mismatch');need(all(v>1e-12 for v in volumes),'Inward/zero-volume imported solid');rec['status']='PASS'
for row in manifest['exports']:
 try:roundtrip(row)
 except Exception as error:
  rec=result['roundtrip'][-1] if result['roundtrip'] and result['roundtrip'][-1]['name']==row['name'] else {'name':row['name']};rec.update(status='FAIL',error=str(error),traceback=traceback.format_exc())
  if rec not in result['roundtrip']:result['roundtrip'].append(rec)
 save()
# A fresh review-only scene shares original packed meshes/materials. No source mesh, material, or saved blend is changed.
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'NorthlineRail.blend'))
originals={r['name']:bpy.data.objects[r['name']] for r in manifest['exports']};material=originals['RailDepartureCoach'].data.materials[0]
def mesh_digest(mesh):
 h=hashlib.sha256()
 for v in mesh.vertices:h.update(struct.pack('<3d',*v.co))
 for poly in mesh.polygons:h.update(struct.pack('<'+str(len(poly.vertices))+'i',*poly.vertices))
 for loop in mesh.uv_layers.active.data:h.update(struct.pack('<2d',*loop.uv))
 return h.hexdigest()
def mat_digest(mat):
 records=[]
 for n in mat.node_tree.nodes:
  values=[]
  for socket in n.inputs:
   if hasattr(socket,'default_value'):
    val=socket.default_value
    if hasattr(val,'__iter__') and not isinstance(val,str):val=list(val)
    if isinstance(val,(int,float,bool,str,list)):values.append([socket.name,val])
  records.append([n.name,n.bl_idname,values,sha_image(n.image) if n.type=='TEX_IMAGE' and n.image else None])
 return hashlib.sha256(json.dumps([records,[(l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name) for l in mat.node_tree.links]],sort_keys=True).encode()).hexdigest()
def sha_image(im):
 need(im.packed_file is not None,'Original map must remain packed');return hashlib.sha256(bytes(im.packed_file.data)).hexdigest()
geometry_before={k:mesh_digest(o.data) for k,o in originals.items()};material_before=mat_digest(material);transform_before={k:[list(o.location),list(o.rotation_euler),list(o.scale)] for k,o in originals.items()}
packed=[]
for n in material.node_tree.nodes:
 if n.type=='TEX_IMAGE' and n.image:
  suffix=n.image.name.rsplit('_',1)[-1];digest=sha_image(n.image);need(digest==manifest['source_atlas']['textures'][suffix]['sha256'],'Packed atlas mismatch');packed.append({'name':n.image.name,'sha256':digest})
need(len(packed)==3,'Exactly three unchanged original packed maps required')
s=bpy.data.scenes.new('Northline bright CPU review only');bpy.context.window.scene=s;s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=8;s.cycles.max_bounces=4;s.cycles.use_denoising=True;s.cycles.denoiser='OPENIMAGEDENOISE';s.render.threads_mode='FIXED';s.render.threads=2
if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
s.render.resolution_x=1280;s.render.resolution_y=800;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.view_settings.view_transform='AgX';s.view_settings.exposure=1.0
w=bpy.data.worlds.new('Bright neutral studio only');w.use_nodes=True;w.node_tree.nodes['Background'].inputs[0].default_value=(.45,.48,.52,1);w.node_tree.nodes['Background'].inputs[1].default_value=1;s.world=w
layout={'RailTram':(-8,0,-14),'RailTransferCradle':(-2,0,-14),'RailControl':(3,0,-14),'RailLowSignal':(5,0,-14),'RailPointBlade':(1,.3,-10),'RailCrossingBarrier':(-7,1.45,-7),'RailServiceGate':(0,.65,-7),'RailPlatformGate':(4,1.65,-7),'RailPortalGate':(8,1.85,1),'RailDepartureCoach':(0,0,0),'RailBoardingRamp':(-5,0,0)}
vec=lambda u:Vector((u[0],-u[2],u[1]));copies={}
for name,position in layout.items():
 ob=originals[name].copy();ob.name='Studio '+name;s.collection.objects.link(ob);ob.location=vec(position);ob.rotation_euler=(0,0,0);ob.scale=(1,1,1);ob.hide_render=False;copies[name]=ob
lights=[]
for name,power,size in [('Key',8000,8),('Fill',5000,8),('Rim',7000,6)]:
 data=bpy.data.lights.new('Review '+name,'AREA');data.energy=power;data.shape='DISK';data.size=size;ob=bpy.data.objects.new('Review '+name,data);s.collection.objects.link(ob);lights.append(ob)
camdata=bpy.data.cameras.new('Review fully framed camera');camdata.type='ORTHO';camdata.clip_end=500;camera=bpy.data.objects.new('Review fully framed camera',camdata);s.collection.objects.link(camera);s.camera=camera
corners=lambda names:[copies[n].matrix_world@Vector(c) for n in names for c in copies[n].bound_box]
def render(name,names,direction,detail_bounds=None):
 for key,ob in copies.items():ob.hide_render=key not in names
 bpy.context.view_layer.update()
 points=corners(names) if detail_bounds is None else [vec((x,y,z)) for x in detail_bounds[0] for y in detail_bounds[1] for z in detail_bounds[2]]
 low=Vector([min(q[i] for q in points) for i in range(3)]);high=Vector([max(q[i] for q in points) for i in range(3)]);center=(low+high)/2;camera.location=center+vec(direction).normalized()*60;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camdata.ortho_scale=max(high-low)
 for _ in range(80):
  bpy.context.view_layer.update();projected=[world_to_camera_view(s,camera,p) for p in points]
  if all(.06<=v.x<=.94 and .06<=v.y<=.94 and v.z>0 for v in projected):break
  camdata.ortho_scale*=1.04
 else:raise AssertionError('Cannot frame review bounds')
 for lamp,offset in zip(lights,[(-8,10,-10),(7,7,-1),(2,10,9)]):lamp.location=center+vec(offset);lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
 bpy.context.view_layer.update();settings={'engine':s.render.engine,'device':s.cycles.device,'threads_mode':s.render.threads_mode,'threads':s.render.threads,'samples':s.cycles.samples,'denoiser':s.cycles.denoiser,'denoising_use_gpu':getattr(s.cycles,'denoising_use_gpu',None),'dimensions':[s.render.resolution_x,s.render.resolution_y],'exposure':s.view_settings.exposure,'world_strength':w.node_tree.nodes['Background'].inputs[1].default_value}
 need(settings['device']=='CPU' and settings['threads']==2 and settings['samples']==8 and not settings['denoising_use_gpu'],'CPU review configuration mismatch');s.render.filepath=str(OUT/'Review'/(name+'.png'));row={'name':name,'scope':'Detail bounds intentionally crop beyond this region; complete assembly is framed separately.' if detail_bounds else 'All visible object bounding corners fully framed, no image crop.','objects':names,'settings_readback':settings,'camera_location_blender':list(camera.location),'camera_rotation_euler':list(camera.rotation_euler),'ortho_scale':camdata.ortho_scale,'frame_bounds_uv':[[min(v.x for v in projected),min(v.y for v in projected)],[max(v.x for v in projected),max(v.y for v in projected)]],'lights':[{'name':q.name,'energy':q.data.energy,'size':q.data.size,'location':list(q.location)} for q in lights]};result['reviews'].append(row);save();bpy.ops.render.render(write_still=True);row.update(file=str(Path(s.render.filepath).relative_to(OUT)),sha256=sha(s.render.filepath));save()
render('coach-west-boarding-full',['RailDepartureCoach','RailBoardingRamp'],(-1,.36,-.45))
render('coach-door-bogie-detail',['RailDepartureCoach','RailBoardingRamp'],(-1,.22,-.30),([ -5.1,.2],[.1,3.3],[-6,2]))
render('whole-kit-bright-studio',list(copies),(-1,.65,-1))
render('tram-manufacturing-bright',['RailTram'],(-1,.45,-1))
need(geometry_before=={k:mesh_digest(o.data) for k,o in originals.items()},'Source mesh geometry/UV changed');need(material_before==mat_digest(material),'Source material graph changed');need(transform_before=={k:[list(o.location),list(o.rotation_euler),list(o.scale)] for k,o in originals.items()},'Original source object transforms changed');need(all(sha(path)==digest for path,digest in inputs.items()),'Original author/export/atlas file changed')
result.update(status='PASS_CPU_ROUNDTRIP_AND_REVIEW' if all(r.get('status')=='PASS' for r in result['roundtrip']) else 'FAIL_ROUNDTRIP_REVIEW_IMAGES_RETAINED',original_geometry_unchanged=True,original_material_graph_unchanged=True,original_source_transform_unchanged=True,original_input_files_unchanged=True,packed_images=packed,review_only_assembly={'coach_origin':[0,0,0],'ramp_foot':[-5,0,0],'ramp_upper_x':-1.65,'coach_west_x':-1.602,'nominal_gap_m':.048,'note':'Review copies match runtime relative alignment; original authored review has a1mZoffset and is preserved.'},triangles_checked=sum(r.get('triangles',0) for r in result['roundtrip']),solid_components_checked=sum(r.get('components',0) for r in result['roundtrip']));save()
print('[Northline] INDEPENDENT ROUNDTRIP',result['status'],'models',len(result['roundtrip']),'triangles',result['triangles_checked'],'CPU2reviews',len(result['reviews']),flush=True)
if result['status']!='PASS_CPU_ROUNDTRIP_AND_REVIEW':raise RuntimeError('Roundtrip failed; exact failed records and rendered reviews preserved')
