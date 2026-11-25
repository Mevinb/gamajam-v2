"""Author a visual-only city gate pilot. CPU/two threads; never writes Assets, C# or generated scenes."""
import argparse,bpy,bmesh,hashlib,json,math,os,sys,time
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
import numpy as np
P=Path(__file__).resolve().parents[3]
ap=argparse.ArgumentParser();ap.add_argument('--go',action='store_true');ap.add_argument('--out',default='ArtSource/CityGatePilot');ap.add_argument('--cpu-previews',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=(P/a.out).resolve();assert OUT.is_relative_to(P/'ArtSource/CityGatePilot') or OUT.is_relative_to(P/'Logs')
if not a.go:print('DRY RUN: visual-only gate, original2Katlas, CPU2; --go required. No files authored.');raise SystemExit(0)
assert not (OUT/'CityGatePilot.blend').exists(),'Use a fresh output; never overwrite an edited source.'
START=time.monotonic();TEX=OUT/'Textures';EXP=OUT/'Exports';QA=OUT/'Review'
for d in (OUT,TEX,EXP,QA):d.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
s=bpy.context.scene;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s.render.threads_mode='FIXED';s.render.threads=2
SIZE=2048;TILE=1024;PAD=16;N=TILE-PAD*2;SEED=731405
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
vec=lambda q:Vector((q[0],-q[2],q[1]))
CONTRACT={'origin_unity':[0,.15,57.5],'outside_local_direction':[0,0,-1],
 'static_boxes':[{'id':'left-wall','center':[-4.85,1.625,0],'size':[6.3,3.55,.5]}, {'id':'right-wall','center':[4.85,1.625,0],'size':[6.3,3.55,.5]},
 {'id':'lintel','center':[0,3.2,0],'size':[2.4,.4,.5]}, {'id':'coping','center':[0,3.45,0],'size':[16,.1,.6]},
 {'id':'left-post','center':[-1.45,2.1,0],'size':[.5,4.5,.8]}, {'id':'right-post','center':[1.45,2.1,0],'size':[.5,4.5,.8]}],
 'opening':{'x':[-1.2,1.2],'y_local':[-.15,3.0],'clear_above_sidewalk_local_y':[.01,2.99]},
 'moving_panel':{'original_local_position':[0,1.5,0],'original_local_scale':[2.9,3,.2],'closed_bounds_center':[0,0,0],'closed_bounds_size':[2.9,3,.2],
 'open_offset':[3.05,0,0],'open_seconds':1.1,'existing_interact_point':[0,1.2,-.45],
 'new_visual_bounds_min':[-1.45,-1.5,-.13],'new_visual_bounds_max':[1.45,1.5,.1]},
 'invariants':['No collider, navigation, LOS or AccessDoor code changes.','Do not replace AccessDoor.panel or its collider/obstacle.','Static visual root uses GateRoot coordinates; moving geometry origin is original panel centre.',
 'To attach moving metre-authored mesh under existing scaled panel:localPosition=0,localRotation=identity,localScale=(1/2.9,1/3,1/.2).','Disable only replaced old renderers; preserve original static and moving physics objects.','Existing sign, warning lights, CCTV, road markings, booth and logical interaction point remain unchanged.']}
source_paths=['Assets/Parasite/Editor/CityBuilder.cs','Assets/Parasite/Editor/World/WorldKit.cs','Assets/Parasite/Scripts/Missions/AccessDoor.cs','Assets/Parasite/Scripts/World/CityLayout.cs']
CONTRACT['source_sha256']={p:sha(P/p) for p in source_paths}
CONTRACT['static_boxes_scope']='Visual pilot crops of existing volumes; these are not replacements for the complete existing collider list.'
CONTRACT['source_static_colliders_root_space']=[
 {'id':'entire-left-wall','center':[-12.6,1.625,0],'size':[22.8,3.55,.5]},
 {'id':'entire-right-wall','center':[25.725,1.625,0],'size':[49.05,3.55,.5]},
 {'id':'entire-lintel','center':[0,3.2,0],'size':[2.4,.4,.5]},
 {'id':'entire-left-post','center':[-1.45,2.1,0],'size':[.5,4.5,.8]},
 {'id':'entire-right-post','center':[1.45,2.1,0],'size':[.5,4.5,.8]}]
CONTRACT['original_visual_only_coping_root_space']={'center':[13.125,3.45,0],'size':[74.35,.1,.6]}
(OUT/'physical-contract.json').write_text(json.dumps(CONTRACT,indent=2)+'\n')

def noise(n,divisions,rng):
 g=rng.random((divisions+1,divisions+1),dtype=np.float32)*2-1;p=np.linspace(0,divisions-1e-5,n,dtype=np.float32);i=p.astype(np.int32);f=p-i;f=f*f*(3-2*f)
 return (g[i[:,None],i[None,:]]*(1-f[None,:])+g[i[:,None],i[None,:]+1]*f[None,:])*(1-f[:,None])+(g[i[:,None]+1,i[None,:]]*(1-f[None,:])+g[i[:,None]+1,i[None,:]+1]*f[None,:])*f[:,None]
arrays={key:np.ones((SIZE,SIZE,4),np.float32) for key in ('A','N','M')};stats=[]
x,y=np.meshgrid(np.linspace(0,1,N,dtype=np.float32),np.linspace(0,1,N,dtype=np.float32))
for tile in range(4):
 rng=np.random.default_rng(SEED+tile*53);broad=noise(N,8,rng);mid=noise(N,39,rng);fine=rng.standard_normal((N,N),dtype=np.float32)
 if tile==0:
  wet=np.exp(-y/.13);value=.44+broad*.035+mid*.018+fine*.004-wet*.05;height=mid*.00018+fine*.000035;rough=.81-wet*.16+mid*.025
  for u,v in ((.2,.25),(.8,.25),(.2,.75),(.8,.75)):
   r=np.sqrt(((x-u)*2.1)**2+((y-v)*3.3)**2);hole=np.exp(-(r/.018)**4);streak=np.exp(-((x-u)/.008)**2)*np.exp(-np.maximum(v-y,0)/.15)*(y<v)
   value-=hole*.10+streak*.032;height-=hole*.002;rough-=streak*.025
  rgb=np.stack((value*1.015,value,value*.965),axis=2);metal=np.zeros_like(value)
 elif tile==1:
  brush=np.sin(x*math.tau*370+mid*.2)*.0015;value=.10+broad*.009+mid*.005+fine*.002;rgb=np.stack((value*.94,value*.99,value*1.04),axis=2)
  height=brush+fine*.000003;rough=.40+mid*.05+fine*.008;metal=np.ones_like(value)*.88
 elif tile==2:
  edge=np.minimum.reduce((x,1-x,y,1-y));chip=(noise(N,75,rng)>.30)&(edge<.05);value=.62+broad*.025+mid*.009+fine*.002
  rgb=np.stack((value,value*.76,value*.105),axis=2);rgb[chip]=np.stack((.11+mid*.01,.115+mid*.01,.12+mid*.01),axis=2)[chip]
  height=mid*.00003-chip*.00025;rough=.63+mid*.035;metal=np.where(chip,.80,0)
 else:
  value=.022+broad*.003+fine*.0005;rgb=np.stack((value*.9,value,value*1.08),axis=2);height=mid*.000008+fine*.000001
  rough=np.where(y>.55,.16,.72)+mid*.02;metal=np.zeros_like(value)
 al=np.ones((N,N,4),np.float32);al[:,:,:3]=np.clip(rgb,.008,.8)
 dy,dx=np.gradient(height,3.3/(N-1),2.1/(N-1));length=np.sqrt(dx*dx+dy*dy+1);normal=np.ones_like(al)
 normal[:,:,0]=.5-dx/length*.5;normal[:,:,1]=.5-dy/length*.5;normal[:,:,2]=.5+1/length*.5
 packed=np.zeros_like(al);packed[:,:,:3]=metal[:,:,None];packed[:,:,3]=1-np.clip(rough,.09,.94)
 row,col=divmod(tile,2);sl=(slice(row*TILE,(row+1)*TILE),slice(col*TILE,(col+1)*TILE))
 for key,arr in (('A',al),('N',normal),('M',packed)):arrays[key][sl]=np.pad(arr,((PAD,PAD),(PAD,PAD),(0,0)),mode='edge')
 stats.append({'tile':tile,'material':['cast concrete','brushed dark steel','powder-coated yellow steel','matte polymer / scanner glass'][tile],'metallic':[float(metal.min()),float(metal.max())],'roughness':[float(np.min(rough)),float(np.max(rough))]})
images=[]
for key,arr in arrays.items():
 im=bpy.data.images.new('CityGatePilot_'+key,width=SIZE,height=SIZE,alpha=True,is_data=key!='A');im.colorspace_settings.name='sRGB' if key=='A' else 'Non-Color'
 im.pixels.foreach_set(arr.ravel());im.filepath_raw=str(TEX/(im.name+'.png'));im.file_format='PNG';im.save();images.append(im)
assert all(np.isfinite(a).all() for a in arrays.values());normal_error=float(np.max(np.abs(np.linalg.norm(arrays['N'][:,:,:3]*2-1,axis=2)-1)));assert normal_error<.00001
mat=bpy.data.materials.new('CityGatePilot_SharedPBR');mat.use_nodes=True;nt=mat.node_tree;bs=nt.nodes.get('Principled BSDF')
for key,im in zip(('A','N','M'),images):
 node=nt.nodes.new('ShaderNodeTexImage');node.name='CityGatePilot_'+key;node.image=im
 if key=='A':nt.links.new(node.outputs['Color'],bs.inputs['Base Color'])
 elif key=='N':
  normal=nt.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.65;nt.links.new(node.outputs['Color'],normal.inputs['Color']);nt.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
 else:
  split=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(node.outputs['Color'],split.inputs['Color']);nt.links.new(split.outputs['Red'],bs.inputs['Metallic'])
  invert=nt.nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1;nt.links.new(node.outputs['Alpha'],invert.inputs[1]);nt.links.new(invert.outputs[0],bs.inputs['Roughness'])
GROUPS={name:{'v':[],'f':[],'uv':[]} for name in ('GateStaticShell','GateMovingLeaf')};PIECES=[]

def append_bmesh(bm,group,name,center,tile,mirror=False,glass=False):
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();bm.normal_update();idx={v:i for i,v in enumerate(bm.verts)}
 verts=[v.co+Vector(center) for v in bm.verts];lo=[min(v[i] for v in verts) for i in range(3)];hi=[max(v[i] for v in verts) for i in range(3)];d=GROUPS[group];base=len(d['v']);d['v'].extend(tuple(vec(v)) for v in verts)
 for face in bm.faces:
  d['f'].append(tuple(base+idx[v] for v in face.verts));axis=max(range(3),key=lambda i:abs(face.normal[i]));uaxis,vaxis=[i for i in range(3) if i!=axis];uv=[]
  for vertex in face.verts:
   p=verts[idx[vertex]];u=(p[uaxis]-lo[uaxis])/max(hi[uaxis]-lo[uaxis],.000001);v=(p[vaxis]-lo[vaxis])/max(hi[vaxis]-lo[vaxis],.000001)
   if mirror:u=1-u
   if tile==3:v=.62+v*.32 if glass else .03+v*.43
   row,col=divmod(tile,2);uv.append((col*.5+PAD/SIZE+u*(.5-2*PAD/SIZE),row*.5+PAD/SIZE+v*(.5-2*PAD/SIZE)))
  d['uv'].append(uv)
 PIECES.append({'group':group,'name':name,'tile':tile,'bounds_min':lo,'bounds_max':hi});bm.free()

def box(group,name,center,size,tile=1,bevel=0,mirror=False,glass=False):
 bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
 for v in bm.verts:v.co=Vector([v.co[i]*size[i] for i in range(3)])
 if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(size)*.25),segments=2,affect='EDGES')
 append_bmesh(bm,group,name,center,tile,mirror,glass)

def tube(group,name,start,end,radius,tile=1,segments=12):
 bm=bmesh.new();bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=segments,radius1=radius,radius2=radius,depth=(Vector(end)-Vector(start)).length)
 rotation=Vector((0,0,1)).rotation_difference((Vector(end)-Vector(start)).normalized())
 for v in bm.verts:v.co=rotation@v.co
 append_bmesh(bm,group,name,(Vector(start)+Vector(end))/2,tile)

for side in (-1,1):
 cx=side*4.85;w=6.3;g='GateStaticShell'
 box(g,'opaque concrete backing',[cx,1.625,0],[w,3.55,.484],0)
 for j in range(3):
  x=cx-w/2+(j+.5)*w/3
  for z in (-.246,.246):box(g,'precast panel',[x,1.65,z],[w/3-.012,3.30,.008],0,.0015,(j+(side>0))%2==0)
 for y,h in ((-.10,.10),(3.365,.07)) :box(g,'continuous concrete edge',[cx,y,0],[w,h,.5],0)
 for x in (cx-w/2+.012,cx+w/2-.012):box(g,'opaque end cap',[x,1.625,0],[.024,3.55,.5],0)
 px=side*1.45
 box(g,'post opaque core',[px,2.1,0],[.5,4.5,.70],1)
 for py in (-.09,4.29):box(g,'post foot/cap',[px,py,0],[.5,.12,.8],1,.008)
 for x in (px-.235,px+.235):box(g,'post rail',[x,2.1,0],[.03,4.5,.8],1,.006)
 for z in (-.374,.374):box(g,'post face',[px,2.1,z],[.42,4.25,.016],1,.003)
 tube(g,'embedded electrical conduit',[px-.06,.35,-.375],[px-.06,3.74,-.375],.023)
 for py in (.5,1.55,2.6,3.65):
  box(g,'conduit retaining clip',[px-.06,py,-.382],[.09,.04,.03],1,.002)
  for xx in (-.17,.17):tube(g,'post fastener',[px+xx,py,-.386],[px+xx,py,-.399],.012,1,6)
box('GateStaticShell','original lintel backing',[0,3.2,0],[2.4,.4,.5],0)
box('GateStaticShell','existing metal coping',[0,3.45,0],[16,.1,.6],1)
for z in (-.293,.293):box('GateStaticShell','folded drip lip',[0,3.43,z],[15.98,.045,.014],1,.002)
g='GateMovingLeaf';box(g,'continuous solid moving leaf',[0,0,.005],[2.9,3,.19],2)
for side in (-1,1):
 box(g,'leaf upright',[side*1.36,0,-.103],[.16,2.98,.034],1,.004)
 for y in (-1.37,1.37):tube(g,'upright fastener',[side*1.36,y,-.12],[side*1.36,y,-.13],.014,1,6)
for y in (-1.40,-.48,.48,1.40):
 box(g,'folded stiffener',[0,y,-.106],[2.88,.09,.036],1,.004)
 for x in (-1.14,-.57,0,.57,1.14):tube(g,'stiffener fastener',[x,y,-.12],[x,y,-.13],.011,1,6)
for x in (-.74,.74):box(g,'inset powder-coated field',[x,0,-.095],[1.13,2.61,.01],2,.002,x>0)
box(g,'central badge housing',[0,-.30,-.112],[.34,.46,.026],1,.003)
box(g,'nonemissive scanner glass',[0,-.23,-.127],[.23,.19,.006],3,.0007,glass=True)
box(g,'card slot',[0,-.43,-.127],[.18,.022,.006],3,.0007)
for x in (-.13,.13):tube(g,'reader fastener',[x,-.13,-.12],[x,-.13,-.13],.009,1,6)

visual=bpy.data.collections.new('VISUAL EXPORT ONLY - no physics');s.collection.children.link(visual);objects=[];reports=[]
for name,d in GROUPS.items():
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(d['v'],[],d['f']);mesh.materials.append(mat);mesh.update();layer=mesh.uv_layers.new(name='Shared2KTrim')
 for poly,uvs in zip(mesh.polygons,d['uv']):
  for li,uv in zip(poly.loop_indices,uvs):layer.data[li].uv=uv
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles();layer=mesh.uv_layers.active
 areas=[]
 for tri in mesh.loop_triangles:
  q=[layer.data[i].uv for i in tri.loops];areas.append(abs((q[1]-q[0]).cross(q[2]-q[0]))*.5)
 assert min(areas)>1e-13,(name,min(areas));assert all(math.isfinite(c) and 0<=c<=1 for item in layer.data for c in item.uv)
 ob=bpy.data.objects.new(name,mesh);visual.objects.link(ob);ob['visual_only']=True;ob['collider_policy']='None. Preserve all existing Unity physics.';objects.append(ob)
 raw=[Vector((v.co.x,v.co.z,-v.co.y)) for v in mesh.vertices];reports.append({'name':name,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'uv_min_area':min(areas),'bounds_min':[min(v[i] for v in raw) for i in range(3)],'bounds_max':[max(v[i] for v in raw) for i in range(3)]})
assert sum(r['triangles'] for r in reports)<16000
# Export corrected copies only: exact established project's asymmetric Blender -> Unity boundary.
bpy.ops.object.select_all(action='DESELECT');copies=[]
for ob in objects:
 copy=ob.copy();copy.data=ob.data.copy();s.collection.objects.link(copy);copy.data.transform(Matrix.Diagonal((1,-1,1,1)));copy.data.flip_normals();copy.select_set(True);copies.append(copy)
bpy.context.view_layer.objects.active=copies[0]
fbx=EXP/'CityGatePilot.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
for ob in copies:bpy.data.objects.remove(ob,do_unlink=True)
# Place only the authoring preview's leaf at the original panel centre. FBX already contains zero-origin local meshes.
objects[1].location=vec((0,1.5,0));guide=bpy.data.collections.new('ORIGINAL PHYSICAL CONTRACT - wire reference');s.collection.children.link(guide);guide.hide_render=True
for contract in CONTRACT['static_boxes']:
 bpy.ops.mesh.primitive_cube_add(size=1,location=vec(contract['center']));ob=bpy.context.object;ob.name=contract['id']+' EXISTING VOLUME CROP';ob.scale=Vector((contract['size'][0],contract['size'][2],contract['size'][1]));ob.display_type='WIRE';ob.hide_render=True
 for col in list(ob.users_collection):col.objects.unlink(ob)
 guide.objects.link(ob)
qa=bpy.data.collections.new('CPU AUTHORING QA ONLY - not exported');s.collection.children.link(qa)
bpy.ops.mesh.primitive_plane_add(size=50,location=vec((0,-.151,0)));ground=bpy.context.object;ground.name='QA ground only'
for col in list(ground.users_collection):col.objects.unlink(ground)
qa.objects.link(ground);gm=bpy.data.materials.new('QA ground');gm.diffuse_color=(.10,.115,.12,1);ground.data.materials.append(gm)
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=8;s.cycles.max_bounces=4;s.cycles.use_denoising=True
if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
s.render.resolution_x=640;s.render.resolution_y=360;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.view_settings.view_transform='AgX'
w=bpy.data.worlds.new('QA neutral studio world');w.use_nodes=True;w.node_tree.nodes['Background'].inputs[0].default_value=(.3,.35,.42,1);w.node_tree.nodes['Background'].inputs[1].default_value=.5;s.world=w
for name,at,power,size in [('QA soft key',(-5,8,-8),1300,8),('QA fill',(8,5,-3),500,6)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;ob=bpy.data.objects.new(name,data);qa.objects.link(ob);ob.location=vec(at);ob.rotation_euler=(vec((0,1.8,0))-ob.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('CPU authoring QA camera');cam=bpy.data.objects.new('CPU authoring QA camera',camdata);qa.objects.link(cam);cam.location=vec((8,3.5,-14));cam.rotation_euler=(vec((0,1.8,0))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.lens=43;s.camera=cam
for im in images:im.pack();im.filepath='//Textures/'+im.name+'.png'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CityGatePilot.blend'),compress=True)
previews=[]
if a.cpu_previews:
 for name,offset in [('closed',0),('open',3.05)]:
  objects[1].location=vec((offset,1.5,0));s.render.filepath=str(QA/(name+'-authoring-cpu.png'));bpy.ops.render.render(write_still=True);previews.append(str(Path(s.render.filepath).relative_to(OUT)))
 objects[1].location=vec((0,1.5,0))
files={str(p.relative_to(OUT)):{'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(OUT.rglob('*')) if p.is_file()}
assert sum(p['bytes'] for p in files.values())<100_000_000
report={'schema':1,'status':'AUTHORED ONLY; Unity import/integration and native comparison pending','generator_sha256':sha(__file__),'blender':bpy.app.version_string,'cpu_threads':2,'gpu_rendering':False,'cpu_previews':previews,'seed':SEED,
 'atlas':{'resolution':[SIZE,SIZE],'tiles':stats,'tile_effective_pixels':N,'padding':PAD,'channels':{'A':'opaque sRGB albedo','N':'linear tangent-space normal','M':'linear RGB metallic / alpha smoothness'},'normal_unit_max_error':normal_error,'original_procedural_authorship':True},
 'meshes':reports,'triangles':sum(r['triangles'] for r in reports),'materials':1,'new_colliders':0,'pieces':len(PIECES),'physical_contract':'physical-contract.json','files':files,'total_bytes':sum(p['bytes'] for p in files.values()),'elapsed_seconds':round(time.monotonic()-START,3)}
(OUT/'production-manifest.json').write_text(json.dumps(report,indent=2)+'\n');(OUT/'piece-map.json').write_text(json.dumps(PIECES,indent=2)+'\n')
print('[gate-pilot] AUTHOR PASS',report['triangles'],'triangles',report['total_bytes'],'bytes CPU2/GPUfalse; noAssets/C# changes',flush=True)
