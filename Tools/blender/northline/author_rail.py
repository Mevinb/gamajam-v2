"""Stage-only Northline visual-kit author. CPU2, shared existing atlas, no output without --go. Never run beside the frozen native suite."""
import argparse,bpy,bmesh,hashlib,json,math,sys,time
from pathlib import Path
from mathutils import Matrix,Vector
ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,default=Path.cwd());ap.add_argument('--out',type=Path,required=True);ap.add_argument('--go',action='store_true');ap.add_argument('--cpu-previews',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
P=a.project.resolve();OUT=(a.out if a.out.is_absolute() else P/a.out).resolve()
assert (P/'Assets/Parasite').is_dir()
assert OUT.is_relative_to(P/'Logs'),'This staged recipe writes only to a fresh project Logs child'
NAMES=('Tram','TransferCradle','Control','LowSignal','PointBlade','CrossingBarrier','ServiceGate','PlatformGate','PortalGate','DepartureCoach','BoardingRamp')
if not a.go:print('DRY: eleven Northline assemblies, existing packed gate atlas, zero textures/colliders, CPU2, no output created:',','.join(NAMES));raise SystemExit(0)
assert not OUT.exists(),'Use a fresh output; never overwrite authored material or an old review'
START=time.monotonic();OUT.mkdir(parents=True);EXPORT=OUT/'Exports';QA=OUT/'Review';EXPORT.mkdir();QA.mkdir()
sha=lambda q:hashlib.sha256(Path(q).read_bytes()).hexdigest()
vec=lambda q:Vector((q[0],-q[2],q[1]))
ATLAS=P/'ArtSource/CityGatePilot/Textures';GATE=P/'ArtSource/CityGatePilot/CityGatePilot.blend'
SOURCE={'gate_blend':{'path':str(GATE.relative_to(P)),'sha256':sha(GATE)},'textures':{k:{'path':str((ATLAS/('CityGatePilot_'+k+'.png')).relative_to(P)),'sha256':sha(ATLAS/('CityGatePilot_'+k+'.png'))} for k in ('A','N','M')}}
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
s=bpy.context.scene;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s.render.threads_mode='FIXED';s.render.threads=2
with bpy.data.libraries.load(str(GATE),link=False) as (src,dst):
 assert 'CityGatePilot_SharedPBR' in src.materials;dst.materials=['CityGatePilot_SharedPBR']
mat=dst.materials[0];assert mat and mat.name=='CityGatePilot_SharedPBR'
images=[]
for node in mat.node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  im=node.image;k=im.name.rsplit('_',1)[-1];assert k in SOURCE['textures'] and im.packed_file
  assert hashlib.sha256(bytes(im.packed_file.data)).hexdigest()==SOURCE['textures'][k]['sha256']
  im.filepath=bpy.path.relpath(str(ATLAS/('CityGatePilot_'+k+'.png')),start=str(OUT));images.append(im)
assert len(images)==3
DATA={'v':[],'f':[],'uv':[]};PIECES=[];SIZE=2048;PAD=16;GROUPS={};OBJECTS={};CURRENT=''

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
 PIECES.append({'group':CURRENT,'name':name,'tile':tile,'glass':glass,'bounds_min':low,'bounds_max':high,'source_vertices':len(vertices),'outward_signed_volume':bm.calc_volume(signed=True)})
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

def group(name):
 global DATA,CURRENT
 CURRENT=name;DATA={'v':[],'f':[],'uv':[]};GROUPS[name]=DATA

def ring(name,center,radius,tube_radius,tile=1,major=20,minor=6):
 bm=bmesh.new();verts=[]
 for i in range(major):
  u=2*math.pi*i/major
  for j in range(minor):
   v=2*math.pi*j/minor;r=radius+tube_radius*math.cos(v)
   verts.append(bm.verts.new((center[0]+r*math.cos(u),center[1]+r*math.sin(u),center[2]+tube_radius*math.sin(v))))
 for i in range(major):
  for j in range(minor):bm.faces.new([verts[i*minor+j],verts[((i+1)%major)*minor+j],verts[((i+1)%major)*minor+(j+1)%minor],verts[i*minor+(j+1)%minor]])
 append(bm,name,(0,0,0),tile)

def bolts(prefix,points,axis=(0,0,.025),radius=.013):
 for p in points:tube(prefix,p,Vector(p)+Vector(axis),radius,1,6)

group('Tram')
box('welded powered tram underframe',(0,.25,0),(5.8,.40,2.18),1,.025,2)
for x in (-2.92,2.92):box('tram buffing beam',(x,.39,0),(.16,.30,2.4),2,.012)
for z in (-1.10,1.10):box('tram side skirt',(0,.51,z),(5.7,.25,.10),2,.012)
box('sealed tram cab',(0,1.12,0),(2.5,1.10,2.12),1,.045,3)
box('folded tram roof',(0,1.73,0),(2.65,.12,2.22),2,.025,2)
for x in (-1.255,1.255):
 box('laminated frost windscreen',(x,1.30,0),(.012,.47,1.70),3,.004,glass=True)
 for z in (-.91,.91):box('windscreen pillar',(x,1.30,z),(.025,.58,.06),1,.004)
for z in (-1.066,1.066):
 box('cab door frame',(0,1.10,z),(1.28,1.02,.012),2,.012)
 box('cab side glass',(0,1.35,z*1.005),(1.02,.39,.012),3,.004,glass=True)
for x in (-2.2,2.2):
 tube('tram axle',(x,.30,-1.13),(x,.30,1.13),.07,1,10)
 for z in (-1.08,1.08):
  tube('tram wheel',(x,.30,z-.10),(x,.30,z+.10),.28,3,20)
  tube('tram wheel hub',(x,.30,z-.115),(x,.30,z+.115),.10,1,12)
for x in (-2.2,-1.7,1.7,2.2):box('tram deck grip',(x,.461,0),(.10,.012,1.9),3,.002)
for x in (-2.65,2.65):
 box('tram nonemissive lamp seat',(x,.65,-.97),(.25,.18,.06),3,.01,glass=True)
 bolts('tram deck captive bolts',[(x,.46,z) for z in (-.84,.84)],(0,.015,0),.015)

group('TransferCradle')
box('four-axis transfer deck',(0,.21,0),(3,.38,2),1,.025,2)
for x in (-1.30,1.30):
 for z in (-.77,.77):tube('roller bearing',(x,.17,z-.12),(x,.17,z+.12),.14,3,12)
box('sealed freight cabinet',(0,.94,0),(1.35,1.05,1.45),3,.025,2)
for x in (-.60,.60):
 box('sealed cabinet upright',(x,.94,-.74),(.08,1.02,.04),1,.005)
 box('sealed cabinet restraint band',(x,.94,.74),(.07,1.02,.025),2,.004)
box('tamper proof manifest plate',(0,1.04,-.75),(.55,.32,.018),2,.004)
for i in range(11):box('manufactured seal barcode',(-.22+i*.043,1.04,-.763),(.012 if i%3 else .023,.22,.005),3)
for z in (-.88,.88):box('transfer safety rim',(0,.43,z),(2.9,.06,.10),2,.006)

group('Control')
box('bolted control plinth',(0,.05,0),(.70,.10,.50),0,.01)
box('cast signal pedestal',(0,.66,.05),(.20,1.20,.20),1,.015,2)
box('control desk housing',(0,1.18,-.03),(.62,.44,.42),1,.025,2)
box('enamel instruction face',(0,1.19,-.247),(.48,.28,.012),2,.004)
for x in (-.16,.02,.18):tube('sealed input button',(x,1.20,-.262),(x,1.20,-.247),.035,3,12)
box('raised keyed control',(0,1.42,-.02),(.16,.035,.18),2,.004)
bolts('control plinth anchors',[(x,.10,z) for x in (-.27,.27) for z in (-.17,.17)],(0,.02,0),.016)

group('LowSignal')
box('low access bolted foot',(0,.035,0),(.48,.07,.40),1,.006)
box('low signal pedestal',(0,.36,.04),(.10,.64,.10),1,.008)
box('protected low signal head',(0,.68,0),(.38,.22,.28),1,.018,2)
box('recessed optical seat',(0,.68,-.145),(.23,.13,.012),3,.005,glass=True)
box('low release pressure tab',(.17,.48,-.05),(.08,.19,.18),2,.006)

group('PointBlade')
box('mechanical point blade',(0,0,0),(2.8,.09,.10),1,.006)
for x in (-1.15,-.55,.05,.65,1.25):box('captive blade clamp',(x,0,0),(.08,.09,.10),2,.003)

group('CrossingBarrier')
box('opaque crossing lift leaf',(0,0,0),(8,2.6,.14),2,.018,2)
for x in (-3.88,3.88):box('crossing leaf folded edge',(x,0,-.08),(.16,2.56,.02),1,.003)
for y in (-1.18,1.18):box('crossing leaf stiffener',(0,y,-.08),(7.78,.12,.02),1,.003)
box('crossing warning plaque',(0,.32,-.079),(2.7,.55,.016),3,.01)
for x in (-1.05,-.70,-.35,0,.35,.70,1.05):box('crossing warning plaque rib',(x,.32,-.090),(.10,.30,.005),2)

group('ServiceGate')
box('service duct pressure leaf',(0,0,0),(.14,1,1.4),2,.012,2)
for z in (-.62,.62):box('service gate folded edge',(-.08,0,z),(.02,.97,.12),1,.003)
for y in (-.39,.39):box('service gate stiffener',(-.08,y,0),(.02,.10,1.28),1,.003)

group('PlatformGate')
box('platform fullheight leaf',(0,0,0),(.25,3,4),2,.018,2)
for z in (-1.86,1.86):box('platform gate folded edge',(-.14,0,z),(.02,2.97,.18),1,.003)
for y in (-1.32,0,1.32):box('platform gate reinforcement',(-.14,y,0),(.02,.12,3.78),1,.003)

group('PortalGate')
box('north departure portal leaf',(0,0,0),(4.2,3.4,.20),1,.022,2)
for x in (-1.99,1.99):box('north portal folded edge',(x,0,-.115),(.18,3.36,.02),2,.003)
for x in (-1.0,0,1.0):box('north portal vertical stiffener',(x,0,-.115),(.10,3.30,.02),2,.003)
for y in (-1.55,1.55):box('north portal horizontal stiffener',(0,y,-.115),(4.0,.12,.02),2,.003)

group('DepartureCoach')
# Conventional rail geometry:3.2m acrossX,14m alongZ; deck at.75m, never a broadside platform.
box('coach welded underframe',(0,.43,0),(3.05,.32,13.8),1,.020,2)
box('departure steel deck',(0,.67,0),(3.2,.16,14),1,.012,2)
box('departure folded canopy',(0,3.2,0),(3.2,.18,14),1,.018,2)
for z in (-6.9,6.9):
 box('coach end wall',(0,1.94,z),(3.2,2.38,.18),1,.020,2)
 box('coach end windscreen',(0,2.12,z*1.015),(2.50,.68,.012),3,.006,glass=True)
box('coach east side',(1.52,1.94,0),(.16,2.38,13.8),1,.018,2)
for z in (-4.05,4.05):box('coach west side beside boarding door',(-1.52,1.94,z),(.16,2.38,5.7),1,.018,2)
for z in (-1.2,1.2):box('coach boarding door jamb',(-1.52,1.94,z),(.16,2.38,.08),2,.006)
for x in (-1.598,1.598):
 for z in (-5.6,-3.6,3.6,5.6):box('coach laminated side glass',(x,2.17,z),(.008,.70,1.55),3,.004,glass=True)
for z in (-4.5,4.5):
 box('coach sprung bogie frame',(0,.43,z),(2.85,.26,2.2),1,.025,2)
 for dz in (-.5,.5):
  tube('coach axle',(-1.45,.40,z+dz),(1.45,.40,z+dz),.055,1,12)
  for x in (-1.45,1.45):
   tube('coach flanged wheel',(x-.10,.40,z+dz),(x+.10,.40,z+dz),.25,3,20)
   tube('coach machined axle cap',(x-.12,.40,z+dz),(x+.12,.40,z+dz),.085,1,12)
for z in (-1.2,.4):box('coach free-body boarding mark',(0,.753,z),(2.65,.006,.08),2)
for z in (-5,-2.5,0,2.5,5):box('coach canopy underside rib',(0,3.075,z),(3.00,.07,.08),1,.004)

group('BoardingRamp')
# Local pivot at the low western foot. Runtime places this at(3,0,45), upper edge5cm short ofcoachX=6.4.
bm=bmesh.new()
coordinates=[(0,.13,-1),(0,.13,1),(3.35,.13,1),(3.35,.13,-1),(0,.15,-1),(0,.15,1),(3.35,.75,1),(3.35,.75,-1)]
verts=[bm.verts.new(v) for v in coordinates]
for face in ((4,5,6,7),(0,3,2,1),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)):bm.faces.new([verts[i] for i in face])
append(bm,'continuous nonslip boarding wedge',(0,0,0),1)
for i in range(12):
 x=.10+i*.28;y=.15+.60*x/3.35
 box('boarding ramp transverse nonslip tread',(x,y+.003,0),(.018,.006,1.92),2)

assert tuple(GROUPS)==NAMES
visual=bpy.data.collections.new('NORTHLINE VISUAL EXPORTS - no physics');s.collection.children.link(visual)
records=[]
for name,d in GROUPS.items():
 mesh=bpy.data.meshes.new('Rail'+name);mesh.from_pydata(d['v'],[],d['f']);mesh.materials.append(mat);mesh.update();uv=mesh.uv_layers.new(name='ExistingGate2KTrim')
 for face,coords in zip(mesh.polygons,d['uv']):
  for loop,coord in zip(face.loop_indices,coords):uv.data[loop].uv=coord
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
 ob=bpy.data.objects.new('Rail'+name,mesh);visual.objects.link(ob);OBJECTS[name]=ob
 points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in mesh.vertices]
 bounds=[[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]
 bpy.ops.object.select_all(action='DESELECT');copy=ob.copy();copy.data=ob.data.copy();s.collection.objects.link(copy);copy.data.transform(Matrix.Diagonal((1,-1,1,1)));copy.data.flip_normals();copy.select_set(True);bpy.context.view_layer.objects.active=copy
 path=EXPORT/('Rail'+name+'.fbx');bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True);bpy.data.objects.remove(copy,do_unlink=True)
 records.append({'name':'Rail'+name,'file':str(path.relative_to(OUT)),'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'bounds_unity':bounds,'closed_solids':sum(p['group']==name for p in PIECES),'material':mat.name,'physics':'visual only; runtime retains original authored collider contract'})
assert sum(r['triangles'] for r in records)<30000
qa=bpy.data.collections.new('CPU REVIEW ONLY - linked copies');s.collection.children.link(qa);visual.hide_render=True
layout={'Tram':(-8,0,0),'TransferCradle':(-2,0,0),'Control':(2,0,0),'LowSignal':(4,0,0),'PointBlade':(1,.3,3),'CrossingBarrier':(-7,1.45,7),'ServiceGate':(0,.65,7),'PlatformGate':(4,1.65,7),'PortalGate':(8,1.85,17),'DepartureCoach':(0,0,28),'BoardingRamp':(-5,0,27)}
for name,position in layout.items():
 ob=bpy.data.objects.new('Review '+name,OBJECTS[name].data);qa.objects.link(ob);ob.location=vec(position)
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=8;s.cycles.max_bounces=4;s.cycles.use_denoising=True;s.cycles.denoiser='OPENIMAGEDENOISE'
if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
s.render.resolution_x=1280;s.render.resolution_y=800;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.view_settings.view_transform='AgX'
w=bpy.data.worlds.new('Northline CPU studio');w.use_nodes=True;w.node_tree.nodes['Background'].inputs[0].default_value=(.14,.18,.22,1);w.node_tree.nodes['Background'].inputs[1].default_value=.5;s.world=w
for name,at,power,size in [('Key',(-12,12,-8),2200,10),('Rim',(12,10,22),1900,10)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;ob=bpy.data.objects.new(name,data);qa.objects.link(ob);ob.location=vec(at);ob.rotation_euler=(vec((0,1,10))-ob.location).to_track_quat('-Z','Y').to_euler()
cameras=[]
for name,eye,target,lens in [('kit-overview',(27,18,-22),(0,1,12),38),('tram-manufacturing',(-2,4,-8),(-8,.9,0),48),('coach-detail',(10,5,18),(0,1.8,28),40)]:
 data=bpy.data.cameras.new(name);data.lens=lens;cam=bpy.data.objects.new(name,data);qa.objects.link(cam);cam.location=vec(eye);cam.rotation_euler=(vec(target)-cam.location).to_track_quat('-Z','Y').to_euler();cameras.append((name,cam))
s.camera=cameras[0][1];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'NorthlineRail.blend'),compress=True)
previews=[]
if a.cpu_previews:
 for name,camera in cameras:
  s.camera=camera;s.render.filepath=str(QA/(name+'.png'));bpy.ops.render.render(write_still=True);previews.append({'file':str(Path(s.render.filepath).relative_to(OUT)),'device':'CPU','threads':2,'samples':8,'dimensions':[1280,800]})
(OUT/'piece-map.json').write_text(json.dumps(PIECES,indent=2)+'\n')
assert sha(GATE)==SOURCE['gate_blend']['sha256']
for row in SOURCE['textures'].values():assert sha(P/row['path'])==row['sha256']
files={str(q.relative_to(OUT)):{'bytes':q.stat().st_size,'sha256':sha(q)} for q in sorted(OUT.rglob('*')) if q.is_file()}
manifest={'schema':1,'status':'Authored source/export only; Unity import/runtime/visual validation pending','author_script_sha256':sha(__file__),'blender':bpy.app.version_string,'source_atlas':SOURCE,'new_textures':0,'new_colliders':0,'exports':records,'distinct_triangles':sum(r['triangles'] for r in records),'closed_solids':len(PIECES),'cpu_previews':previews,'files':files,'elapsed_seconds':round(time.monotonic()-START,3),'license':'Original project geometry and unchanged original project CityGatePilot procedural atlas. No external models/textures.'}
(OUT/'production-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print('[Northline] AUTHOR PASS',manifest['distinct_triangles'],'triangles /11models /',len(PIECES),'closed solids / CPU2 / textures0')
