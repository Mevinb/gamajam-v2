"""Author a visual-only Harbor mechanics kit with the existing gate atlas; CPU2 only, dry unless --go."""
import argparse,bpy,bmesh,hashlib,json,math,sys,time
from pathlib import Path
from mathutils import Matrix,Vector
P=Path(__file__).resolve().parents[3]
ap=argparse.ArgumentParser();ap.add_argument('--go',action='store_true');ap.add_argument('--out',default='ArtSource/Harbor');ap.add_argument('--cpu-previews',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=(P/a.out).resolve();assert OUT.is_relative_to(P/'ArtSource/Harbor') or OUT.is_relative_to(P/'Logs')
if not a.go:print('DRY RUN: nine visual Harbor assemblies / existing CityGatePilot_SharedPBR / no new textures / CPU2; --go required. No output created.');raise SystemExit(0)
for rel in ('Harbor.blend','production-manifest.json'):
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
  im.filepath=bpy.path.relpath(str(ATLAS/('CityGatePilot_'+suffix+'.png')),start=str(OUT));images.append(im)
assert len(images)==3
bs=mat.node_tree.nodes.get('Principled BSDF');assert bs and bs.inputs['Emission Strength'].default_value==0 and bs.inputs['Transmission Weight'].default_value==0
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

group('ValveBase')
box('anchored concrete plinth',(0,.075,0),(1.05,.15,.82),0,.02,2)
box('cast valve pedestal',(0,.58,.03),(.42,.92,.46),1,.025,2)
tube('sealed pipe riser',(0,.15,.04),(0,1.42,.04),.17,1,12)
for y in (.26,1.36):
 tube('bolted pipe flange',(0,y-.045,.04),(0,y+.045,.04),.235,1,12)
 for i in range(8):
  t=i*math.tau/8;bolts('flange bolt',[(.205*math.cos(t),y+.045,.04+.205*math.sin(t))],(0,.025,0))
tube('front spindle housing',(0,1.1,-.12),(0,1.1,-.34),.115,1,12)
box('yellow lockout plate',(.28,.74,-.22),(.22,.32,.045),2,.009)
for y in (.69,.81):box('lockout engraving',( .28,y,-.248),(.14,.012,.005),3)
# Rigid curved hose is manufactured as short capped segments, all independently closed.
hose=[(-.20,.94,.06),(-.39,.84,.10),(-.44,.52,.15),(-.32,.28,.21)]
for i in range(len(hose)-1):tube('protected hydraulic hose',hose[i],hose[i+1],.026,3)
bolts('plinth anchor',[(x,.151,z) for x in (-.40,.40) for z in (-.29,.29)],(0,.02,0),.021)

group('ValveWheel')
ring('handwheel rim',(0,0,0),.31,.027,2,24,6)
tube('wheel hub',(0,0,-.055),(0,0,.055),.075,1,12)
for i in range(4):
 t=i*math.pi/2;tube('wheel spoke',(.06*math.cos(t),.06*math.sin(t),0),(.29*math.cos(t),.29*math.sin(t),0),.018,1,6)
box('raised grip index',(0,.31,-.025),(.075,.055,.055),3,.005)

group('GaugeBase')
box('pressure station foot',(0,-1.385,.1),(.75,.13,.65),0,.015)
box('pressure station post',(0,-.66,.10),(.15,1.35,.15),1,.012)
tube('sealed pressure housing',(0,0,-.08),(0,0,.14),.40,1,32)
tube('pale calibrated dial',(0,0,-.108),(0,0,-.082),.354,0,32)
ring('machined gauge bezel',(0,0,-.12),.375,.027,1,32,6)
for i in range(17):
 t=math.radians(-130+i*260/16);x=.29*math.sin(t);y=.29*math.cos(t)
 tube('dial graduation',(x*.89,y*.89,-.125),(x,y,-.125),.0065,3,4)
for x in (-.25,.25):bolts('gauge housing fastener',[(x,.20,-.143)],(0,0,-.02),.013)
box('gauge serial plate',(0,-.18,-.13),(.22,.07,.006),2,.001)

group('GaugeNeedle')
tube('needle central pin',(0,0,-.018),(0,0,.018),.030,1,12)
box('pressure needle',(0,.105,0),(.014,.27,.012),2,.002)
box('needle counterweight',(0,-.05,0),(.045,.07,.016),1,.004)

group('LiftGate')
box('continuous quarantine lift leaf',(0,0,0),(4,3.5,.20),2,.012,2)
for x in (-1.87,1.87):box('lift gate side rail',(x,0,-.12),(.17,3.48,.01),1,.002)
for y in (-1.61,-.52,.52,1.61):
 box('folded gate reinforcement',(0,y,-.12),(3.76,.12,.01),1,.002)
 bolts('gate captive fastener',[(x,y,-.13) for x in (-1.7,-.85,0,.85,1.7)],(0,0,-.015),.015)
box('dark quarantine plaque',(0,.45,-.134),(1.12,.48,.012),3,.012)
for x in (-.42,-.28,-.14,0,.14,.28,.42):box('serial plaque ribs',(x,.45,-.143),(.035,.23,.005),2,.001)

group('Conveyor')
for z in (-1.04,1.04):
 box('rolled conveyor chassis',(0,.57,z),(8,.20,.16),1,.016)
 box('yellow safety rim',(0,.88,z),(8,.08,.11),2,.008)
 for x in (-3.5,0,3.5):box('conveyor leg',(x,.27,z),(.16,.54,.16),1,.01);box('conveyor foot',(x,.035,z),(.40,.07,.40),1,.008)
for i in range(20):
 x=-3.8+i*.4;tube('real transverse conveyor roller',(x,.69,-.97),(x,.69,.97),.083,1,10)
 for z in (-1.1,1.1):tube('roller axle bearing',(x,.69,z-.032),(x,.69,z+.032),.055,1,8)
box('drive gearbox',(-3.55,.45,1.30),(.70,.48,.44),1,.025,2)
for x in (-3.77,-3.56,-3.35):box('drive cooling fin',(x,.70,1.3),(.05,.04,.45),1,.003)
tube('guarded electric motor',(-3.55,.45,1.49),(-3.55,.45,1.87),.18,1,12)
bolts('chassis fastener',[(x,.58,z) for x in (-3.6,-2,-.4,1.2,2.8,3.6) for z in (-1.135,1.135)],(0,0,.02))

group('SealedCrate')
box('sealed polymer cargo core',(0,.5,0),(1.2,1,1.4),3,.025,2)
for x in (-.55,.55):
 box('extruded crate upright',(x,.50,-.69),(.08,.98,.06),1,.006)
 box('extruded crate upright',(x,.50,.69),(.08,.98,.06),1,.006)
for y in (.055,.945):box('continuous crate front edge',(0,y,-.70),(1.17,.09,.045),1,.005);box('continuous crate rear edge',(0,y,.70),(1.17,.09,.045),1,.005)
for x in (-.36,.36):box('sealed cargo restraint band',(x,.50,-.725),(.065,.90,.012),2,.002);box('sealed top restraint band',(x,1.005,0),(.065,.012,1.35),2,.002)
box('tamper evidence panel',(0,.57,-.725),(.40,.31,.012),2,.004)
for i in range(8):box('manufactured barcode groove',(-.15+i*.041,.57,-.735),(.012 if i%3 else .024,.19,.005),3)

group('RelayPad')
box('relay ground footing',(0,.035,0),(2.20,.07,2.20),0,.015)
box('relay sealed treadplate',(0,.095,0),(2.05,.05,2.05),1,.012)
for x in (-.94,.94):box('visible yellow pad edge',(x,.126,0),(.06,.012,1.94),2,.001)
for z in (-.94,.94):box('visible yellow pad edge',(0,.126,z),(1.94,.012,.06),2,.001)
for i in range(11):box('anti slip tread',(-.75+i*.15,.128,0),(.015,.008,1.60),1,.001)
bolts('pad countersunk fastener',[(x,.13,z) for x in (-.80,.80) for z in (-.80,.80)],(0,.01,0),.014)

group('Emitter')
box('emitter bolted foot',(0,.035,0),(.48,.07,.44),1,.007)
box('emitter post',(0,.42,0),(.10,.78,.10),1,.007)
box('shielded emitter head',(0,.83,-.04),(.30,.20,.22),1,.012,2)
box('nonemissive lens seat',(0,.83,-.158),(.19,.11,.018),3,.004,glass=True)
for x in (-.19,.19):bolts('emitter anchor',[(x,.071,.13)],(0,.018,0),.014)

visual=bpy.data.collections.new('HARBOR VISUAL EXPORTS - no physics');s.collection.children.link(visual)
records=[]
for name,d in GROUPS.items():
 mesh=bpy.data.meshes.new('Harbor'+name);mesh.from_pydata(d['v'],[],d['f']);mesh.materials.append(mat);mesh.update();uv=mesh.uv_layers.new(name='ExistingGate2KTrim')
 for face,coords in zip(mesh.polygons,d['uv']):
  for loop,coord in zip(face.loop_indices,coords):uv.data[loop].uv=coord
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
 ob=bpy.data.objects.new('Harbor'+name,mesh);visual.objects.link(ob);OBJECTS[name]=ob
 points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in mesh.vertices]
 bounds=[[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]
 bpy.ops.object.select_all(action='DESELECT');copy=ob.copy();copy.data=ob.data.copy();s.collection.objects.link(copy);copy.data.transform(Matrix.Diagonal((1,-1,1,1)));copy.data.flip_normals();copy.select_set(True);bpy.context.view_layer.objects.active=copy
 path=EXPORT/('Harbor'+name+'.fbx');bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True);bpy.data.objects.remove(copy,do_unlink=True)
 records.append({'name':'Harbor'+name,'file':str(path.relative_to(OUT)),'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'bounds':bounds,'closed_solids':sum(p['group']==name for p in PIECES),'pivot':'local origin; rotations around Unity Z for wheel/needle','material':mat.name})
assert sum(r['triangles'] for r in records)<18000
# Source remains editable at origin; review layout uses linked mesh instances in a separate non-export collection.
qa=bpy.data.collections.new('CPU REVIEW ONLY - linked instances, cameras, lights');s.collection.children.link(qa)
visual.hide_render=True
layout={'ValveBase':(-4,0,0),'ValveWheel':(-4,1.1,-.36),'GaugeBase':(-1.7,1.6,0),'GaugeNeedle':(-1.7,1.6,-.18),'LiftGate':(2.4,1.9,1),'Conveyor':(0,0,6),'SealedCrate':(0,.78,6),'RelayPad':(5.5,0,5),'Emitter':(5.5,0,5.9)}
for name,position in layout.items():
 ob=bpy.data.objects.new('QA '+name,OBJECTS[name].data);qa.objects.link(ob);ob.location=vec(position)
 if name=='GaugeNeedle':ob.rotation_euler[1]=math.radians(45)
bpy.ops.mesh.primitive_plane_add(size=40,location=vec((0,-.015,3)));ground=bpy.context.object;ground.name='CPU review ground';qa.objects.link(ground)
for col in list(ground.users_collection):
 if col!=qa:col.objects.unlink(ground)
gm=bpy.data.materials.new('CPU review ground only');gm.diffuse_color=(.095,.11,.12,1);ground.data.materials.append(gm)
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=8;s.cycles.max_bounces=4;s.cycles.use_denoising=True;s.cycles.denoiser='OPENIMAGEDENOISE'
if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
s.render.resolution_x=960;s.render.resolution_y=720;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.view_settings.view_transform='AgX'
w=bpy.data.worlds.new('CPU Harbor studio');w.use_nodes=True;w.node_tree.nodes['Background'].inputs[0].default_value=(.14,.18,.22,1);w.node_tree.nodes['Background'].inputs[1].default_value=.5;s.world=w
for name,at,power,size in [('Key',(-5,8,-5),1700,7),('Rim',(7,6,8),1400,6)]:
 data=bpy.data.lights.new('CPU '+name,'AREA');data.energy=power;data.shape='DISK';data.size=size;ob=bpy.data.objects.new('CPU '+name,data);qa.objects.link(ob);ob.location=vec(at);ob.rotation_euler=(vec((0,1,2))-ob.location).to_track_quat('-Z','Y').to_euler()
cameras=[]
for name,eye,target,lens in [('kit-overview',(13,10,-14),(0,1.2,3),42),('pressure-station',(-8,4,-6),(-3,1,0),48),('conveyor-cargo',(9,5,-1),(0,.7,6),44),('relay-manufacturing',(8.5,3,1),(5.5,.5,5),48)]:
 data=bpy.data.cameras.new(name);data.lens=lens;cam=bpy.data.objects.new('CPU '+name,data);qa.objects.link(cam);cam.location=vec(eye);cam.rotation_euler=(vec(target)-cam.location).to_track_quat('-Z','Y').to_euler();cameras.append((name,cam))
s.camera=cameras[0][1];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Harbor.blend'),compress=True)
previews=[]
if a.cpu_previews:
 for name,cam in cameras:
  s.camera=cam;s.render.filepath=str(QA/(name+'.png'));bpy.ops.render.render(write_still=True);previews.append({'file':str(Path(s.render.filepath).relative_to(OUT)),'dimensions':[960,720],'samples':8,'threads':2,'device':'CPU','denoiser':'OPENIMAGEDENOISE CPU'})
(OUT/'piece-map.json').write_text(json.dumps(PIECES,indent=2)+'\n')
for row in SOURCE['textures'].values():assert sha(P/row['path'])==row['sha256']
assert sha(GATE)==SOURCE['gate_blend']['sha256']
files={str(p.relative_to(OUT)):{'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(OUT.rglob('*')) if p.is_file() and p.suffix!='.log'}
manifest={'schema':1,'status':'CPU authored; Unity import/native verification pending','author_script_sha256':sha(__file__),'blender':bpy.app.version_string,'source_atlas':SOURCE,'new_textures':0,'new_colliders':0,'shared_material':mat.name,'exports':records,'distinct_triangles':sum(r['triangles'] for r in records),'closed_solids':len(PIECES),'cpu_previews':previews,'files':files,'elapsed_seconds':round(time.monotonic()-START,3),'license':'Original project geometry; unchanged original CityGatePilot procedural atlas. No external models/textures.'}
(OUT/'production-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print('[Harbor] AUTHOR PASS',manifest['distinct_triangles'],'distinct triangles /9exports /',len(PIECES),'closed solids / CPU2 / new textures0',flush=True)
