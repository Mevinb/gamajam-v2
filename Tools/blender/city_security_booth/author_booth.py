"""Author a visual-only security booth with the existing gate atlas; CPU2 only, dry unless --go."""
import argparse,bpy,bmesh,hashlib,json,math,sys,time
from pathlib import Path
from mathutils import Matrix,Vector
P=Path(__file__).resolve().parents[3]
ap=argparse.ArgumentParser();ap.add_argument('--go',action='store_true');ap.add_argument('--out',default='ArtSource/CitySecurityBooth');ap.add_argument('--cpu-previews',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=(P/a.out).resolve();assert OUT.is_relative_to(P/'ArtSource/CitySecurityBooth') or OUT.is_relative_to(P/'Logs')
if not a.go:print('DRY RUN: one visual booth / existing CityGatePilot_SharedPBR / no new textures / CPU2; --go required. No output created.');raise SystemExit(0)
for rel in ('CitySecurityBooth.blend','Exports/CitySecurityBooth.fbx','production-manifest.json'):
 assert not (OUT/rel).exists(),('Use a fresh output; edited/previous authored assets are never overwritten',rel)
START=time.monotonic();EXPORT=OUT/'Exports';QA=OUT/'Review'
for path in (OUT,EXPORT,QA):path.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
vec=lambda p:Vector((p[0],-p[2],p[1]))
ATLAS=P/'ArtSource/CityGatePilot/Textures';GATE=P/'ArtSource/CityGatePilot/CityGatePilot.blend'
SOURCE={'gate_blend':{'path':str(GATE.relative_to(P)),'sha256':sha(GATE)},'textures':{suffix:{'path':str((ATLAS/('CityGatePilot_'+suffix+'.png')).relative_to(P)),'sha256':sha(ATLAS/('CityGatePilot_'+suffix+'.png')),'bytes':(ATLAS/('CityGatePilot_'+suffix+'.png')).stat().st_size} for suffix in ('A','N','M')}}
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
s=bpy.context.scene;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s.render.threads_mode='FIXED';s.render.threads=2
with bpy.data.libraries.load(str(GATE),link=False) as (src,dst):
 assert 'CityGatePilot_SharedPBR' in src.materials;dst.materials=['CityGatePilot_SharedPBR']
mat=dst.materials[0];assert mat and mat.name=='CityGatePilot_SharedPBR'
images=[]
for node in mat.node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  im=node.image;suffix=im.name.rsplit('_',1)[-1];assert suffix in SOURCE['textures'];assert im.packed_file
  assert hashlib.sha256(bytes(im.packed_file.data)).hexdigest()==SOURCE['textures'][suffix]['sha256']
  im.filepath='//../CityGatePilot/Textures/CityGatePilot_'+suffix+'.png';images.append(im)
assert len(images)==3
bs=mat.node_tree.nodes.get('Principled BSDF');assert bs and bs.inputs['Emission Strength'].default_value==0 and bs.inputs['Transmission Weight'].default_value==0
DATA={'v':[],'f':[],'uv':[]};PIECES=[];SIZE=2048;PAD=16

def append(bm,name,center,tile,glass=False):
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();bm.normal_update()
 # Recalc can choose the inward orientation for a slanted capped cylinder. Each
 # authored piece is a closed solid, so require its local signed volume outward.
 if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
 bm.normal_update();assert bm.calc_volume(signed=True)>1e-12,(name,'Non-outward or zero-volume solid')
 vertices=[v.co+Vector(center) for v in bm.verts];index={v:i for i,v in enumerate(bm.verts)}
 low=[min(p[i] for p in vertices) for i in range(3)];high=[max(p[i] for p in vertices) for i in range(3)]
 offset=len(DATA['v']);DATA['v'].extend([tuple(vec(p)) for p in vertices])
 for face in bm.faces:
  DATA['f'].append(tuple(offset+index[v] for v in face.verts))
  axis=max(range(3),key=lambda i:abs(face.normal[i]));uaxis,vaxis=[i for i in range(3) if i!=axis];uv=[]
  for vertex in face.verts:
   p=vertices[index[vertex]];u=(p[uaxis]-low[uaxis])/max(high[uaxis]-low[uaxis],1e-8);v=(p[vaxis]-low[vaxis])/max(high[vaxis]-low[vaxis],1e-8)
   if tile==3:v=.62+v*.32 if glass else .03+v*.43
   row,col=divmod(tile,2);uv.append((col*.5+PAD/SIZE+u*(.5-2*PAD/SIZE),row*.5+PAD/SIZE+v*(.5-2*PAD/SIZE)))
  DATA['uv'].append(uv)
 PIECES.append({'name':name,'tile':tile,'glass':glass,'bounds_min':low,'bounds_max':high,'source_vertices':len(vertices),'outward_signed_volume':bm.calc_volume(signed=True)})
 bm.free()

def box(name,center,size,tile=0,bevel=0,segments=1,rotate_x=0,glass=False):
 bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
 for v in bm.verts:v.co=Vector([v.co[i]*size[i] for i in range(3)])
 if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(size)*.25),segments=segments,affect='EDGES')
 if rotate_x:
  r=Matrix.Rotation(math.radians(rotate_x),3,'X')
  for v in bm.verts:v.co=r@v.co
 append(bm,name,center,tile,glass)

def tube(name,start,end,radius,tile=1,sides=8):
 bm=bmesh.new();bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=sides,radius1=radius,radius2=radius,depth=(Vector(end)-Vector(start)).length)
 q=Vector((0,0,1)).rotation_difference((Vector(end)-Vector(start)).normalized())
 for v in bm.verts:v.co=q@v.co
 append(bm,name,(Vector(start)+Vector(end))/2,tile)

# Continuous opaque core keeps the existing solid booth intent, including behind every dark glazed panel.
box('continuous solid booth core',(0,1.475,0),(2.5,2.95,3),0,.012,2)
# Separate precast face panels and deliberately fine, dark compression joints.
for face,z in [('front',-1.504),('rear',1.504)]:
 for i in range(3):
  x=(i-1)*.828
  box(face+' lower precast panel '+str(i),(x,.62,z),(.812,1.20,.012),0,.002)
  box(face+' upper precast panel '+str(i),(x,2.655,z),(.812,.55,.012),0,.002)
 for x in (-.414,.414):box(face+' vertical lower joint',(x,.62,z+(-.008 if z<0 else .008)),(.010,1.22,.004),3)
 for y in (.012,1.235,2.365,2.94):box(face+' horizontal panel joint',(0,y,z+(-.008 if z<0 else .008)),(2.48,.011,.004),3)
for side in (-1,1):
 for i in range(3):
  z=(i-1)*.993
  box('side lower precast panel',(side*1.254,.62,z),(.012,1.20,.975),0,.002)
  box('side upper precast panel',(side*1.254,2.655,z),(.012,.55,.975),0,.002)
 for z in (-.4965,.4965):box('side lower compression joint',(side*1.264,.62,z),(.004,1.22,.010),3)
 for y in (.012,1.235,2.365,2.94):box('side horizontal compression joint',(side*1.264,y,0),(.004,.011,2.98),3)
 for z in (-1.475,1.475):box('precast corner return',(side*1.224,1.475,z),(.055,2.93,.055),0,.006)
# Roof slab, folded coping and drip line stay within the supplied existing roof envelope.
box('roof precast slab',(0,3.015,0),(3.08,.12,3.58),0,.009,2)
for z in (-1.755,1.755):
 box('folded roof coping',(0,3.115,z),(3.10,.07,.09),1,.004)
 box('roof drip edge',(0,3.045,z),(3.08,.025,.085),1,.002)
for x in (-1.505,1.505):
 box('side roof coping',(x,3.115,0),(.09,.07,3.42),1,.004)
 box('side drip edge',(x,3.045,0),(.085,.025,3.40),1,.002)
# Small gutter along the rear; an actual three-part channel, not a solid decorative block.
box('rear gutter sole',(0,3.008,1.733),(2.94,.018,.125),1,.002)
for z in (1.674,1.792):box('rear gutter lip',(0,3.045,z),(2.94,.075,.014),1,.002)
for x in (-1.46,1.46):box('gutter end cap',(x,3.045,1.735),(.022,.075,.125),1,.002)
for x in (-1.2,-.4,.4,1.2):
 box('gutter retaining strap',(x,2.995,1.59),(.04,.09,.18),1,.002)
 tube('gutter strap fastener',(x,2.96,1.506),(x,2.96,1.518),.008,1,6)
# Opaque window modules retain the exact given overall window width/height and centres.
def window(label,width,side=0):
 def at(u,v,d):return (u,1.8+v,-1.53-d) if side==0 else (side*(1.28+d),1.8+v,u)
 def part(name,u,v,d,w,h,t,tile=1,bevel=.002,glass=False):
  box(label+' '+name,at(u,v,d),(w,h,t) if side==0 else (t,h,w),tile,bevel,1,glass=glass)
 part('opaque dark reflective glazing',0,0,.005,width-.084,1.016,.008,3,.001,True)
 for u in (-width/2+.026,width/2-.026):part('manufactured jamb',u,0,0,.052,1.1,.06)
 for v in (-.524,.524):part('manufactured head/sill',0,v,0,width,.052,.06)
 for u in (-width/2+.058,width/2-.058):part('EPDM glazing gasket',u,0,.014,.012,1.002,.009,3,.001)
 for v in (-.488,.488):part('EPDM horizontal gasket',0,v,.014,width-.105,.012,.009,3,.001)
 # Narrow mullions express a manufactured observation window, never a new walk-through opening.
 for u in ((0,) if side==0 else (-width/6,width/6)):
  part('vertical mullion',u,0,.012,.032,1.01,.042)
 part('sloping sill lip',0,-.566,.033,width+.05,.045,.14,1,.003)
 part('sill drainage groove',0,-.589,.051,width-.09,.009,.012,3,.001)
 for u in (-width/2+.045,width/2-.045):
  for v in (-.455,0,.455):
   begin=at(u,v,.032);end=at(u,v,.043);tube(label+' visible frame screw',begin,end,.0075,1,6)
window('front observation window',2.2)
window('left observation window',2.6,-1)
window('right observation window',2.6,1)
# Flush rear maintenance components are too small for a human door and have no handle/hinge affordance.
box('rear vent recessed backing',(0,1.91,1.525),(1.18,.76,.035),3,.003)
for x in (-.606,.606):box('rear vent side frame',(x,1.91,1.541),(.045,.84,.045),1,.003)
for y in (1.505,2.315):box('rear vent cross frame',(0,y,1.541),(1.25,.045,.045),1,.003)
for i in range(11):box('rear angled ventilation louver',(0,1.575+i*.066,1.557),(1.14,.032,.075),1,.001,1,rotate_x=-18)
box('flush rear service plate',(0,.73,1.522),(.91,.73,.025),1,.008)
box('rear service plate inset',(0,.73,1.54),(.80,.62,.012),3,.004)
for x in (-.385,.385):
 for y in (.45,1.01):tube('rear service plate fastener',(x,y,1.544),(x,y,1.558),.010,1,6)
box('small physical inspection marker',(.35,.48,1.553),(.13,.07,.006),2,.001)
# Rainwater downpipe and restrained mechanical clips, all inside the roof envelope.
tube('downpipe upper elbow',(-1.31,2.985,1.735),(-1.36,2.91,1.60),.039)
tube('downpipe vertical',(-1.36,.14,1.60),(-1.36,2.91,1.60),.039)
tube('downpipe discharge elbow',(-1.36,.14,1.60),(-1.36,.07,1.735),.039)
for y in (.38,1.15,1.92,2.69):
 box('downpipe retaining clip',(-1.36,y,1.60),(.12,.045,.10),1,.003)
 box('downpipe wall bracket',(-1.274,y,1.55),(.055,.065,.13),1,.002)
 for x in (-1.398,-1.322):tube('downpipe clip screw',(x,y,1.65),(x,y,1.661),.007,1,6)

visual=bpy.data.collections.new('VISUAL EXPORT ONLY - no colliders');s.collection.children.link(visual)
mesh=bpy.data.meshes.new('SecurityBoothShell');mesh.from_pydata(DATA['v'],[],DATA['f']);mesh.materials.append(mat);mesh.update();uv=mesh.uv_layers.new(name='ExistingGate2KTrim')
for polygon,coords in zip(mesh.polygons,DATA['uv']):
 for loop,coord in zip(polygon.loop_indices,coords):uv.data[loop].uv=coord
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
assert len(mesh.loop_triangles)<7000,('Triangle budget',len(mesh.loop_triangles))
ob=bpy.data.objects.new('SecurityBoothShell',mesh);visual.objects.link(ob);ob['visual_only']=True;ob['new_colliders']=0;ob['unity_root']=[4.25,0,55.1]
raw=[Vector((v.co.x,v.co.z,-v.co.y)) for v in mesh.vertices];bounds=[[min(v[i] for v in raw) for i in range(3)],[max(v[i] for v in raw) for i in range(3)]]
assert all(bounds[0][i]>=(-1.55,0,-1.8)[i]-.00001 and bounds[1][i]<=(1.55,3.15,1.8)[i]+.00001 for i in range(3)),bounds
# The established project boundary is applied only to a temporary export copy.
bpy.ops.object.select_all(action='DESELECT');copy=ob.copy();copy.data=ob.data.copy();s.collection.objects.link(copy);copy.data.transform(Matrix.Diagonal((1,-1,1,1)));copy.data.flip_normals();copy.select_set(True);bpy.context.view_layer.objects.active=copy
fbx=EXPORT/'CitySecurityBooth.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
bpy.data.objects.remove(copy,do_unlink=True)
# Reference-only body/roof volumes aid editing and are never exported or rendered.
ref=bpy.data.collections.new('EXISTING COLLIDER AND ROOF REFERENCES - not exported');s.collection.children.link(ref);ref.hide_render=True
for name,center,size in [('Existing solid body collider',(0,1.475,0),(2.5,2.95,3)),('Existing roof envelope',(0,3.05,0),(3.1,.2,3.6))]:
 bpy.ops.mesh.primitive_cube_add(size=1,location=vec(center));guide=bpy.context.object;guide.name=name;guide.scale=(size[0],size[2],size[1]);guide.display_type='WIRE';guide.hide_render=True
 for col in list(guide.users_collection):col.objects.unlink(guide)
 ref.objects.link(guide)
qa=bpy.data.collections.new('CPU AUTHORING QA ONLY - not exported');s.collection.children.link(qa)
bpy.ops.mesh.primitive_plane_add(size=25,location=vec((0,-.012,0)));ground=bpy.context.object;ground.name='QA ground only'
for col in list(ground.users_collection):col.objects.unlink(ground)
qa.objects.link(ground);groundmat=bpy.data.materials.new('QA ground only');groundmat.diffuse_color=(.10,.12,.135,1);ground.data.materials.append(groundmat)
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=8;s.cycles.max_bounces=4;s.cycles.use_denoising=True;s.cycles.denoiser='OPENIMAGEDENOISE'
if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
s.render.resolution_x=960;s.render.resolution_y=720;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.view_settings.view_transform='AgX'
world=bpy.data.worlds.new('CPU QA neutral dark studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.15,.20,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;s.world=world
for name,at,power,size in [('QA soft key',(-4,7,-4),950,5),('QA soft fill',(6,5,3),650,4)]:
 light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;obj=bpy.data.objects.new(name,light);qa.objects.link(obj);obj.location=vec(at);obj.rotation_euler=(vec((0,1.5,0))-obj.location).to_track_quat('-Z','Y').to_euler()
cameras=[]
for name,eye in [('front-right',(5.6,3.9,-7.6)),('rear-left',(-5.2,3.5,7.2))]:
 cd=bpy.data.cameras.new('CPU QA '+name);cam=bpy.data.objects.new('CPU QA '+name,cd);qa.objects.link(cam);cam.location=vec(eye);cam.rotation_euler=(vec((0,1.55,0))-cam.location).to_track_quat('-Z','Y').to_euler();cd.lens=48;cameras.append((name,cam))
s.camera=cameras[0][1]
for im in images:assert im.packed_file
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CitySecurityBooth.blend'),compress=True)
previews=[]
if a.cpu_previews:
 for name,cam in cameras:
  s.camera=cam;s.render.filepath=str(QA/(name+'-authoring-cpu.png'));bpy.ops.render.render(write_still=True);previews.append({'name':name,'file':str(Path(s.render.filepath).relative_to(OUT)),'eye_blender':list(cam.location),'resolution':[960,720],'samples':8,'threads':2,'device':'CPU','denoiser':'OPENIMAGEDENOISE CPU'})
contract={'schema':1,'unity_root':[4.25,0,55.1],'existing_body_local_bounds':[[-1.25,0,-1.5],[1.25,2.95,1.5]],'allowed_visual_local_bounds':[[-1.55,0,-1.8],[1.55,3.15,1.8]],'actual_visual_local_bounds':bounds,'window_modules':[{'name':'front','center':[0,1.8,-1.53],'overall_width':2.2,'overall_height':1.1},{'name':'left','center':[-1.28,1.8,0],'overall_depth':2.6,'overall_height':1.1},{'name':'right','center':[1.28,1.8,0],'overall_depth':2.6,'overall_height':1.1}],'existing_security_text_world_position':[4.25,3.05,53.27],'existing_security_text_policy':'Retain the original Unity text, not included in this export.','scope':'Visual replacement only. Existing solid body collider/navigation/LOS and text remain. No new door or human entrance, no transparent interior, no new collider.','coordinate_boundary':'Unity(x,y,z) -> Blender(x,-z,y); temporary export copy mirrors BlenderY and flips face winding, FBX-Zforward/Yup/baked-space; importer proof decodesUnity(x,z,y).'}
(OUT/'physical-contract.json').write_text(json.dumps(contract,indent=2)+'\n');(OUT/'piece-map.json').write_text(json.dumps(PIECES,indent=2)+'\n')
for suffix,row in SOURCE['textures'].items():assert sha(P/row['path'])==row['sha256']
assert sha(GATE)==SOURCE['gate_blend']['sha256']
files={str(p.relative_to(OUT)):{'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(OUT.rglob('*')) if p.is_file() and p.suffix!='.log'}
manifest={'schema':1,'status':'AUTHORED ONLY; actual Unity import/integration/performance pending','author_script_sha256':sha(__file__),'blender':bpy.app.version_string,'cpu_threads':2,'gpu_rendering':False,'new_textures':0,'shared_material':'CityGatePilot_SharedPBR','source_atlas':SOURCE,'mesh':{'name':ob.name,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'bounds_min':bounds[0],'bounds_max':bounds[1],'materials':[m.name for m in mesh.materials]},'closed_solid_pieces':len(PIECES),'new_colliders':0,'cpu_previews':previews,'files':files,'elapsed_seconds':round(time.monotonic()-START,3),'license':'Original project-authored mesh and geometry recipe. Reuses original project-authored CityGatePilot procedural atlas unchanged. No external textures/models were added.'}
(OUT/'production-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('[security-booth] AUTHOR PASS',manifest['mesh']['triangles'],'triangles;',len(PIECES),'closed solid pieces; one existing atlas/material, new textures0, CPU2/GPUfalse',flush=True)
