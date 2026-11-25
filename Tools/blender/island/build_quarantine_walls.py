"""Original opaque island wall kit: CPU mesh/atlas authoring and FBX export only. Never renders or publishes."""
import argparse,bpy,bmesh,hashlib,json,math,os,sys,time
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
import numpy as np
ARGS=sys.argv[sys.argv.index('--')+1:]
ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--stage',required=True)
a=ap.parse_args(ARGS);PROJECT=Path(a.project).resolve();STAGE=Path(a.stage).resolve()
assert STAGE.is_relative_to(PROJECT/'Logs')
OUT=STAGE/'staged/Assets/Parasite/Resources/Island';TEX=OUT/'Textures';SOURCE=STAGE/'staged/ArtSource/Island';EVIDENCE=STAGE/'evidence'
for path in (OUT,TEX,SOURCE,EVIDENCE):path.mkdir(parents=True,exist_ok=True)
START=time.monotonic();SEED=184201;SIZE=2048;TILE=1024;PAD=16;INNER=TILE-PAD*2;PANEL_W=3.2;MAP_H=3.6;RECESS=.012
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.render.threads_mode='FIXED';scene.render.threads=2
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
# These exact boxes are visual dressing targets only. Their existing Unity physics/LOS/nav objects remain authoritative.
WALLS=[
 dict(id='quarantine-west',group='QuarantineFacade',name='Quarantine wall',center=[-39.85,1.95,-18],size=[26.3,3.6,.4]),
 dict(id='quarantine-middle',group='QuarantineFacade',name='Quarantine wall',center=[-16.75,1.95,-18],size=[17.1,3.6,.4]),
 dict(id='quarantine-east',group='QuarantineFacade',name='Quarantine wall',center=[23.6,1.95,-18],size=[58.8,3.6,.4]),
 dict(id='culvert-lintel',group='QuarantineFacade',name='Low passage lintel',center=[-26,2.55,-18],size=[1.4,2.4,.4]),
 dict(id='service-lintel',group='QuarantineFacade',name='Service entrance lintel',center=[-7,3.25,-18],size=[2.4,1,.4]),
 dict(id='south-quay-west',group='SouthernQuayFacade',name='South coast west wall',center=[-44.75,.75,-37.7],size=[14.5,1.2,.6]),
 dict(id='south-quay-east',group='SouthernQuayFacade',name='South coast east wall',center=[8.75,.75,-37.7],size=[86.5,1.2,.6]),
]
GROUPS={name:dict(vertices=[],faces=[],uvs=[]) for name in ('QuarantineFacade','SouthernQuayFacade')}
PER_WALL={w['id']:dict(vertices=[],faces=[]) for w in WALLS};PIECES=[]
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def city_to_blender(p):return (p[0],-p[2],p[1])
def smooth_noise(n,divisions,rng):
    grid=rng.random((divisions+1,divisions+1),dtype=np.float32)*2-1
    p=np.linspace(0,divisions-1e-5,n,dtype=np.float32);i=p.astype(np.int32);f=p-i;f=f*f*(3-2*f)
    top=grid[i[:,None],i[None,:]]*(1-f[None,:])+grid[i[:,None],i[None,:]+1]*f[None,:]
    bottom=grid[i[:,None]+1,i[None,:]]*(1-f[None,:])+grid[i[:,None]+1,i[None,:]+1]*f[None,:]
    return top*(1-f[:,None])+bottom*f[:,None]
def make_image(name,array):
    im=bpy.data.images.new(name,width=SIZE,height=SIZE,alpha=True,is_data=name.endswith(('_N','_M')))
    im.colorspace_settings.name='sRGB' if name.endswith('_A') else 'Non-Color'
    im.pixels.foreach_set(array.astype(np.float32).ravel());im.file_format='PNG';im.filepath_raw=str(TEX/(name+'.png'));im.save()
    return im
def atlas():
    aa=np.ones((SIZE,SIZE,4),dtype=np.float32);nn=np.ones_like(aa);mm=np.ones_like(aa);mm[:,:,:3]=0
    x,y=np.meshgrid(np.linspace(0,1,INNER,dtype=np.float32),np.linspace(0,1,INNER,dtype=np.float32))
    for variant in range(4):
        rng=np.random.default_rng(SEED+variant*101)
        broad=smooth_noise(INNER,6,rng);medium=smooth_noise(INNER,37,rng);fine=rng.standard_normal((INNER,INNER),dtype=np.float32)
        wet=np.exp(-y/.11)*(0.85+broad*.15)
        height=fine*.000045+medium*.00015+broad*.00025
        value=.49+broad*.038+medium*.012+fine*.007-wet*.065
        rough=.80+broad*.025+fine*.012-wet*.19
        # Original cast-concrete form marks and tie recesses; no baked direct light or emissive pixels.
        form=(.5+.5*np.sin(y*math.tau*29+medium*.35))**20
        height-=form*.00028;value-=form*.012
        for u,v in ((.18,.29),(.82,.29),(.18,.73),(.82,.73)):
            d=np.sqrt(((x-u)*PANEL_W)**2+((y-v)*MAP_H)**2)
            hole=np.exp(-(d/.020)**4);rim=np.exp(-((d-.026)/.006)**2)
            height-=hole*.0032;height+=rim*.00045;value-=hole*.083;value+=rim*.008
            trail=np.exp(-((x-u)/(.008+variant*.001))**2)*np.exp(-np.maximum(v-y,0)/.18)*(y<v)
            value-=trail*.035;rough-=trail*.04
        edge=np.exp(-np.minimum.reduce((x,1-x,y,1-y))/.013)
        value+=edge*.025;height-=edge*.0005
        base=np.ones((INNER,INNER,4),dtype=np.float32)
        base[:,:,0]=np.clip(value*(1.015-variant*.004),.22,.66);base[:,:,1]=np.clip(value,.22,.66);base[:,:,2]=np.clip(value*.975,.22,.66)
        dy,dx=np.gradient(height,MAP_H/(INNER-1),PANEL_W/(INNER-1));length=np.sqrt(dx*dx+dy*dy+1)
        normal=np.ones_like(base);normal[:,:,0]=.5-dx/length*.5;normal[:,:,1]=.5-dy/length*.5;normal[:,:,2]=.5+1/length*.5
        packed=np.ones_like(base);packed[:,:,:3]=0;packed[:,:,3]=1-np.clip(rough,.48,.91)
        row,col=divmod(variant,2);sl=(slice(row*TILE,(row+1)*TILE),slice(col*TILE,(col+1)*TILE))
        for dst,src in ((aa,base),(nn,normal),(mm,packed)):dst[sl]=np.pad(src,((PAD,PAD),(PAD,PAD),(0,0)),mode='edge')
    images=[make_image('IslandQuarantine_'+s,arr) for s,arr in (('A',aa),('N',nn),('M',mm))]
    stats=dict(resolution=[SIZE,SIZE],variants=4,padding_pixels=PAD,seed=SEED,
      albedo_rgb_min=float(aa[:,:,:3].min()),albedo_rgb_max=float(aa[:,:,:3].max()),alpha_min=float(aa[:,:,3].min()),
      metallic_min=float(mm[:,:,0].min()),metallic_max=float(mm[:,:,0].max()),smoothness_min=float(mm[:,:,3].min()),smoothness_max=float(mm[:,:,3].max()),
      normal_finite=bool(np.isfinite(nn).all()),normal_unit_max_error=float(np.max(np.abs(np.linalg.norm(nn[:,:,:3]*2-1,axis=2)-1))),
      channels={'A':'opaque sRGB base colour','N':'linear tangent-space normal','M':'linear RGB metallic=0 / alpha smoothness'},
      compressed_gpu_budget_mib=16,uncompressed_gpu_mips_mib=64)
    return images,stats
images,ATLAS=atlas()
material=bpy.data.materials.new('IslandQuarantineConcrete');material.use_nodes=True;material.diffuse_color=(.47,.47,.455,1)
bs=material.node_tree.nodes.get('Principled BSDF');bs.inputs['Metallic'].default_value=0
nodes=material.node_tree.nodes;links=material.node_tree.links
for suffix,image in zip(('A','N','M'),images):
    node=nodes.new('ShaderNodeTexImage');node.image=image;node.label='2K shared '+suffix+' atlas';node.name='IslandQuarantine_'+suffix
    if suffix=='A':links.new(node.outputs['Color'],bs.inputs['Base Color'])
    elif suffix=='N':
        n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.6;links.new(node.outputs['Color'],n.inputs['Color']);links.new(n.outputs['Normal'],bs.inputs['Normal'])
    else:
        split=nodes.new('ShaderNodeSeparateColor');links.new(node.outputs['Color'],split.inputs['Color']);links.new(split.outputs['Red'],bs.inputs['Metallic'])
        inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;links.new(node.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bs.inputs['Roughness'])

def atlas_uv(point,normal,wall,anchor,variant):
    row,col=divmod(variant,2);axis=max(range(3),key=lambda i:abs(normal[i]));zmin=wall['center'][2]-wall['size'][2]/2
    if axis==2:u=(point[0]-anchor)/PANEL_W;v=(point[1]-.15)/MAP_H
    elif axis==0:u=(point[2]-zmin)/PANEL_W;v=(point[1]-.15)/MAP_H
    else:u=(point[0]-anchor)/PANEL_W;v=.86+(point[2]-zmin)/wall['size'][2]*.11
    u=min(1,max(0,u));v=min(1,max(0,v));pad=PAD/SIZE;scale=.5-pad*2
    return (col*.5+pad+u*scale,row*.5+pad+v*scale)

def piece(wall,kind,center,size,variant=0,anchor=None,bevel=0):
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
    for vertex in bm.verts:vertex.co=Vector((vertex.co.x*size[0],vertex.co.y*size[1],vertex.co.z*size[2]))
    if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=1,affect='EDGES')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();bm.normal_update()
    vertices=[tuple(vertex.co+Vector(center)) for vertex in bm.verts];index={vertex:i for i,vertex in enumerate(bm.verts)}
    faces=[tuple(index[v] for v in face.verts) for face in bm.faces]
    for face in bm.faces:
        assert face.normal.dot(face.calc_center_median())>0,'Inward authored piece normal'
    normal=[tuple(face.normal) for face in bm.faces]
    bm.free();anchor=anchor if anchor is not None else center[0]-size[0]/2
    group=GROUPS[wall['group']];base=len(group['vertices']);group['vertices'].extend(city_to_blender(v) for v in vertices)
    group['faces'].extend(tuple(base+i for i in f) for f in faces)
    group['uvs'].extend([[atlas_uv(vertices[i],normal[k],wall,anchor,variant) for i in face] for k,face in enumerate(faces)])
    record=PER_WALL[wall['id']];base2=len(record['vertices']);record['vertices'].extend(vertices);record['faces'].extend(tuple(base2+i for i in f) for f in faces)
    PIECES.append(dict(wall=wall['id'],kind=kind,center=list(center),size=list(size),bevel=bevel,variant=variant))

for wi,wall in enumerate(WALLS):
    x,y,z=wall['center'];w,h,d=wall['size'];xmin=x-w/2;ymin=y-h/2
    columns=math.ceil(w/PANEL_W);width=w/columns;toe=min(.14,h*.15);coping=min(.10,h*.13);edge=.025
    for col in range(columns):
        left=xmin+col*width;right=left+width;variant=(wi*17+col*7)%4;cx=(left+right)/2
        # Continuous closed opaque backing and full-depth cap/toe pieces keep the exact old silhouette.
        piece(wall,'opaque-backing',(cx,y,z),(width,h,d-RECESS*2),variant,left)
        piece(wall,'full-depth-coping',(cx,y+h/2-coping/2,z),(width,coping,d),variant,left)
        piece(wall,'full-depth-toe',(cx,ymin+toe/2,z),(width,toe,d),variant,left)
        low=ymin+toe;high=y+h/2-coping;rows=math.ceil((high-low)/1.8);height=(high-low)/rows
        for row in range(rows):
            py=low+(row+.5)*height;lx=left+(edge if col==0 else .004);rx=right-(edge if col==columns-1 else .004)
            for side in (-1,1):
                piece(wall,'shallow-precast-panel',( (lx+rx)/2,py,z+side*(d/2-RECESS/2)),(rx-lx,height-.008,RECESS),variant,left,.003)
    for side in (-1,1):piece(wall,'full-depth-end-cap',(x+side*(w/2-edge/2),y,z),(edge,h,d),wi%4,xmin if side<0 else x+w/2-PANEL_W)

collection=bpy.data.collections.new('Approved opaque wall visuals');scene.collection.children.link(collection)
VISUALS=[]
for name,data in GROUPS.items():
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(data['vertices'],[],data['faces']);mesh.materials.append(material);mesh.update()
    uv=mesh.uv_layers.new(name='Shared2KTrim')
    for poly,values in zip(mesh.polygons,data['uvs']):
        for index,value in zip(poly.loop_indices,values):uv.data[index].uv=value
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
    obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);obj['unity_visual_only']=True;obj['colliders']='None; original runtime boxes remain unchanged';VISUALS.append(obj)

# Editable, non-exported original-box reference collection. No lights/cameras/cycles/rendering exist in this file.
guides=bpy.data.collections.new('Original physical contract - wire guides only');scene.collection.children.link(guides);guides.hide_render=True
for wall in WALLS:
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
    for vertex in bm.verts:
        p=tuple(vertex.co[i]*wall['size'][i]+wall['center'][i] for i in range(3));vertex.co=city_to_blender(p)
    mesh=bpy.data.meshes.new(wall['id']+' contract');bm.to_mesh(mesh);bm.free();obj=bpy.data.objects.new(wall['id']+' ORIGINAL BOX',mesh);guides.objects.link(obj);obj.display_type='WIRE';obj.hide_render=True

wall_reports=[]
for wall in WALLS:
    data=PER_WALL[wall['id']];verts=[Vector(v) for v in data['vertices']];faces=data['faces'];bvh=BVHTree.FromPolygons(verts,faces,all_triangles=False)
    minimum=[min(v[i] for v in verts) for i in range(3)];maximum=[max(v[i] for v in verts) for i in range(3)]
    expected_min=[wall['center'][i]-wall['size'][i]/2 for i in range(3)];expected_max=[wall['center'][i]+wall['size'][i]/2 for i in range(3)]
    delta=max(abs(minimum[i]-expected_min[i]) for i in range(3));delta=max(delta,max(abs(maximum[i]-expected_max[i]) for i in range(3)));assert delta<.0001
    coverage=0;total=0;max_recess=0
    for axis in range(3):
        a,b=[i for i in range(3) if i!=axis]
        for sign in (-1,1):
            for u in np.linspace(.0001,.9999,11):
                for v in np.linspace(.0001,.9999,11):
                    point=list(wall['center']);point[axis]=wall['center'][axis]+sign*(wall['size'][axis]/2+1)
                    point[a]=expected_min[a]+u*wall['size'][a];point[b]=expected_min[b]+v*wall['size'][b]
                    direction=Vector((0,0,0));direction[axis]=-sign
                    hit=bvh.ray_cast(Vector(point),direction,wall['size'][axis]+2)[0];total+=1
                    assert hit is not None,(wall['id'],axis,sign,u,v)
                    coverage+=1
                    if axis==2:max_recess=max(max_recess,abs(hit[axis]-(wall['center'][axis]+sign*wall['size'][axis]/2)))
    assert max_recess<RECESS+.0001
    wall_reports.append(dict(wall,vertices=len(verts),triangles=sum(len(f)-2 for f in faces),bounds_min=minimum,bounds_max=maximum,bounds_max_delta=delta,opaque_axis_rays=total,opaque_axis_hits=coverage,max_surface_recess=max_recess))

all_verts=[];all_faces=[]
for data in PER_WALL.values():
    base=len(all_verts);all_verts.extend(Vector(v) for v in data['vertices']);all_faces.extend(tuple(base+i for i in f) for f in data['faces'])
bvh=BVHTree.FromPolygons(all_verts,all_faces,all_triangles=False);opening_reports=[]
for name,x0,x1,y0,y1 in [('culvert',-26.7,-25.3,.15,1.35),('service',-8.2,-5.8,.15,2.75),('arrival-gangway',-37.5,-34.5,.15,1.35)]:
    z=-37.7 if name=='arrival-gangway' else -18;count=0
    for x in np.linspace(x0+.0001,x1-.0001,13):
        for y in np.linspace(y0+.0001,y1-.0001,13):
            assert bvh.ray_cast(Vector((x,y,z-.8)),Vector((0,0,1)),1.6)[0] is None,(name,x,y)
            count+=1
    opening_reports.append(dict(name=name,x=[x0,x1],y=[y0,y1],width=x1-x0,height=y1-y0,clear_rays=count))

mesh_reports=[];triangles=0
for obj in VISUALS:
    mesh=obj.data;mesh.calc_loop_triangles();triangles+=len(mesh.loop_triangles);areas=[];layer=mesh.uv_layers.active
    for tri in mesh.loop_triangles:
        p=[layer.data[i].uv for i in tri.loops];areas.append(abs((p[1]-p[0]).cross(p[2]-p[0]))*.5)
    assert min(areas)>1e-13,(obj.name,min(areas))
    assert all(math.isfinite(c) and 0<=c<=1 for item in layer.data for c in item.uv)
    mesh_reports.append(dict(name=obj.name,vertices=len(mesh.vertices),triangles=len(mesh.loop_triangles),materials=[m.name for m in mesh.materials],uv_min_area=min(areas),uv_degenerate_triangles=sum(v<=1e-13 for v in areas)))
assert triangles<=12000,triangles

# Correct only export copies at the proven Unity boundary: Blender world-Y reflection + winding reversal.
bpy.ops.object.select_all(action='DESELECT');copies=[]
for obj in VISUALS:
    copy=obj.copy();copy.data=obj.data.copy();scene.collection.objects.link(copy);copy.name=obj.name
    copy.data.transform(Matrix.Diagonal((1,-1,1,1)) @ obj.matrix_world);copy.matrix_world=Matrix.Identity(4);copy.data.flip_normals();copy.data.update();copy.select_set(True);copies.append(copy)
fbx=OUT/'IslandQuarantineWalls.fbx';temporary=STAGE/'work/IslandQuarantineWalls.tmp.fbx'
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.export_scene.fbx(filepath=str(temporary),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
os.replace(temporary,fbx)
for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for obj in VISUALS:obj.select_set(True)
bpy.context.view_layer.objects.active=VISUALS[0]
bpy.ops.file.pack_all()
for image in images:image.filepath='//'+os.path.relpath(image.filepath_raw,SOURCE)
blend=SOURCE/'QuarantineWalls.blend';bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)
assert blend.stat().st_size<100*1024*1024
report=dict(schema=1,status='Ignored staging only; not imported/published or visually rendered',generator_sha256=sha(__file__),blender_version=bpy.app.version_string,cpu_threads=2,render_calls=0,cycles_used=False,gpu_used=False,seed=SEED,
 original_world_origin=[620,0,-220],author_coordinates='Blender=(UnityX,-UnityZ,UnityY)',export_conversion='Temporary mesh reflection on Blender worldY plus reversed winding; observed Unity import maps(BlenderX,BlenderZ,BlenderY)',
 before=dict(visual_boxes=7,triangles=84,materials=['IslandConcrete'],texture_resolution=512,colliders='Existing exact primitive BoxColliders; unchanged by proposal'),
 after=dict(active_meshes=len(VISUALS),triangles=triangles,material_count=1,lights=0,shader_pass_changes=0,new_colliders=0,packed_blend_bytes=blend.stat().st_size),
 walls=wall_reports,openings=opening_reports,meshes=mesh_reports,atlas=ATLAS,pieces=len(PIECES),
 files={str(p.relative_to(STAGE/'staged')):dict(sha256=sha(p),bytes=p.stat().st_size) for p in [fbx,blend]+[TEX/('IslandQuarantine_'+s+'.png') for s in ('A','N','M')]},elapsed_seconds=round(time.monotonic()-START,3))
(SOURCE/'quarantine-wall-manifest.json').write_text(json.dumps(report,indent=2)+'\n');(EVIDENCE/'geometry-uv-bounds.json').write_text(json.dumps(report,indent=2)+'\n')
print('[wall-kit] AUTHOR/EXPORT PASS',triangles,'triangles,2meshes,1material;',round(time.monotonic()-START,2),'seconds; no rendering or publication',flush=True)
