#!/usr/bin/env python3
"""Author a metre-scale, collider-free city detail kit and CPU-bake its shared PBR atlas.

Run with Blender 4.5: blender -b -t 2 --python this_file -- [project-root]
The only published outputs are owned EnvironmentRealism paths. No scene file is edited.
"""
import bpy, bmesh, hashlib, json, math, random, sys
from pathlib import Path
from mathutils import Matrix, Vector

P = Path(sys.argv[sys.argv.index('--')+1]).resolve() if '--' in sys.argv else Path(__file__).resolve().parents[3]
SRC = P/'ArtSource/EnvironmentRealism'
DST = P/'Assets/Parasite/Art/EnvironmentRealism'
SRC.mkdir(parents=True, exist_ok=True)
(DST/'Textures').mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 8
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.bake.margin = 8
scene.render.bake.use_clear = False
scene.view_settings.view_transform = 'Standard'
SIZE = 2048
PAD = 12/SIZE

def node(mat, kind, **values):
    n = mat.node_tree.nodes.new(kind)
    for key, value in values.items(): n.inputs[key].default_value = value
    return n

def link(mat, src, out, dst, inp): mat.node_tree.links.new(src.outputs[out], dst.inputs[inp])

# Four procedural material masters are retained in the .blend. Their values are linear,
# with 1 m source coordinates; the exported atlas has no lighting or painted highlights.
masters = []
for i, (name, lo, hi, rough, metal, amplitude) in enumerate([
    ('CutLimestone', (.30,.285,.25,1), (.46,.445,.405,1), .79, 0., .0012),
    ('PowderCoatedSteel', (.115,.145,.145,1), (.19,.215,.21,1), .57, 0., .00022),
    ('OxidisedCastIron', (.027,.034,.037,1), (.065,.069,.064,1), .69, .72, .0007),
    ('MossAndLeaf', (.022,.046,.013,1), (.10,.15,.027,1), .83, 0., .0006),
]):
    m = bpy.data.materials.new('MASTER_'+name); m.use_nodes=True; m.use_fake_user=True
    m.node_tree.nodes.clear()
    uv=node(m,'ShaderNodeTexCoord')
    mapping=node(m,'ShaderNodeVectorMath'); mapping.operation='MULTIPLY_ADD'
    mapping.inputs[1].default_value=(2,2,1); mapping.inputs[2].default_value=(-i%2,-(i//2),0)
    # Each atlas quarter occupies its own unwrapped 0..1 material domain.
    mapping.inputs[2].default_value=(-(i%2),-(i//2),0)
    link(m,uv,'UV',mapping,0)
    coarse=node(m,'ShaderNodeTexNoise',Scale=7.,Detail=4.,Roughness=.72)
    fine=node(m,'ShaderNodeTexNoise',Scale=170.,Detail=2.,Roughness=.7)
    link(m,mapping,'Vector',coarse,'Vector'); link(m,mapping,'Vector',fine,'Vector')
    color=node(m,'ShaderNodeMixRGB'); color.blend_type='MIX'
    color.inputs[1].default_value=lo; color.inputs[2].default_value=hi
    link(m,coarse,'Fac',color,0)
    bump=node(m,'ShaderNodeBump',Strength=.38,Distance=amplitude)
    link(m,fine,'Fac',bump,'Height')
    broad=node(m,'ShaderNodeBump',Strength=.24,Distance=amplitude*2)
    link(m,coarse,'Fac',broad,'Height'); link(m,bump,'Normal',broad,'Normal')
    bsdf=node(m,'ShaderNodeBsdfPrincipled',Roughness=rough,Metallic=metal)
    link(m,color,'Color',bsdf,'Base Color'); link(m,broad,'Normal',bsdf,'Normal')
    out=node(m,'ShaderNodeOutputMaterial'); link(m,bsdf,'BSDF',out,'Surface')
    # Neutral occlusion preserves geometric SSAO / contact shadows without painted-in lighting.
    smooth=node(m,'ShaderNodeMapRange'); smooth.inputs['From Min'].default_value=0
    smooth.inputs['From Max'].default_value=1; smooth.inputs['To Min'].default_value=1-rough-.055
    smooth.inputs['To Max'].default_value=1-rough+.055; link(m,fine,'Fac',smooth,'Value')
    pack=node(m,'ShaderNodeCombineColor'); pack.mode='RGB'
    pack.inputs[0].default_value=metal; pack.inputs[1].default_value=.98; pack.inputs[2].default_value=0
    # alpha cannot be baked by an emission pass; bake smoothness into B then pack after bake.
    link(m,smooth,'Result',pack,2)
    emit=node(m,'ShaderNodeEmission',Strength=1)
    masters.append((m,out,bsdf,color,pack,emit))

verts=[]; faces=[]
for i in range(4):
    x=(i%2)*1.1; y=(i//2)*1.1
    base=len(verts); verts.extend([(x,y,0),(x+1,y,0),(x+1,y+1,0),(x,y+1,0)])
    faces.append(tuple(base+j for j in range(4)))
me=bpy.data.meshes.new('AtlasBakeGrid'); me.from_pydata(verts,[],faces); me.update()
bake=bpy.data.objects.new('AtlasBakeGrid',me); scene.collection.objects.link(bake)
for m,*_ in masters: me.materials.append(m)
uv=me.uv_layers.new(name='UVMap')
for i,p in enumerate(me.polygons):
    p.material_index=i
    for li,xy in zip(p.loop_indices,[(0,0),(1,0),(1,1),(0,1)]):
        uv.data[li].uv=((i%2)*.5+xy[0]*.5,(i//2)*.5+xy[1]*.5)
bpy.context.view_layer.objects.active=bake; bake.select_set(True)

images={}
for channel in ('A','N','M'):
    im=bpy.data.images.new('EnvironmentRealism_'+channel,width=SIZE,height=SIZE,alpha=True,float_buffer=False)
    im.colorspace_settings.name='sRGB' if channel=='A' else 'Non-Color'
    for m,out,bsdf,col,pack,emit in masters:
        tex=node(m,'ShaderNodeTexImage'); tex.image=im; m.node_tree.nodes.active=tex
        if channel=='N':link(m,bsdf,'BSDF',out,'Surface')
        else:
            link(m,col if channel=='A' else pack,'Color',emit,'Color');link(m,emit,'Emission',out,'Surface')
    print('[EnvironmentRealism] CPU bake '+channel,flush=True)
    bpy.ops.object.bake(type='NORMAL' if channel=='N' else 'EMIT',normal_space='TANGENT')
    if channel=='M':
        import numpy as np
        pixels=np.empty(SIZE*SIZE*4,dtype=np.float32);im.pixels.foreach_get(pixels)
        pixels=pixels.reshape((SIZE,SIZE,4));pixels[:,:,3]=pixels[:,:,2];pixels[:,:,2]=0
        im.pixels.foreach_set(pixels.ravel());im.update()
    im.filepath_raw=str(DST/'Textures'/('EnvironmentRealism_'+channel+'.png'))
    im.file_format='PNG';im.save();im.pack();images[channel]=im
for m,out,bsdf,*_ in masters:link(m,bsdf,'BSDF',out,'Surface')
bpy.data.objects.remove(bake,do_unlink=True)

shared=bpy.data.materials.new('EnvironmentRealismShared'); shared.use_nodes=True
nt=shared.node_tree; bsdf=nt.nodes.get('Principled BSDF')
for c in ('A','N','M'):
    tex=nt.nodes.new('ShaderNodeTexImage');tex.image=images[c]
    if c=='A':nt.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    elif c=='N':
        n=nt.nodes.new('ShaderNodeNormalMap');nt.links.new(tex.outputs['Color'],n.inputs['Color']);nt.links.new(n.outputs['Normal'],bsdf.inputs['Normal'])
    else:
        sep=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(tex.outputs['Color'],sep.inputs[0]);nt.links.new(sep.outputs[0],bsdf.inputs['Metallic'])
        inv=nt.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
        nt.links.new(tex.outputs['Alpha'],inv.inputs[1]);nt.links.new(inv.outputs[0],bsdf.inputs['Roughness'])

DATA={}; PIECES=[]; CURRENT=''
# Geometry is specified in Unity axes (X right, Y up, -Z outward), authored in Blender
# metric Z-up, then exported with an explicit axis contract. Never add collider objects.
def unity_to_blender(p):return Vector((p[0],-p[2],p[1]))
def append(bm,name,center,tile):
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();bm.normal_update()
    vertices=[v.co+Vector(center) for v in bm.verts];idx={v:i for i,v in enumerate(bm.verts)}
    low=[min(v[i] for v in vertices) for i in range(3)];high=[max(v[i] for v in vertices) for i in range(3)]
    base=len(DATA['v']);DATA['v'].extend([tuple(unity_to_blender(v)) for v in vertices])
    for f in bm.faces:
        DATA['f'].append(tuple(base+idx[v] for v in f.verts))
        axis=max(range(3),key=lambda i:abs(f.normal[i]));aa,bb=[i for i in range(3) if i!=axis]
        uv=[]
        for v in f.verts:
            p=vertices[idx[v]];u=(p[aa]-low[aa])/max(high[aa]-low[aa],1e-8);w=(p[bb]-low[bb])/max(high[bb]-low[bb],1e-8)
            uv.append(((tile%2)*.5+PAD+u*(.5-2*PAD),(tile//2)*.5+PAD+w*(.5-2*PAD)))
        DATA['uv'].append(uv)
    PIECES.append({'model':CURRENT,'piece':name,'material_tile':tile,'bounds_min':low,'bounds_max':high,'signed_volume':bm.calc_volume(signed=True)})
    bm.free()

def box(name,c,s,tile=0,bevel=.004,segments=2):
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts:v.co=Vector([v.co[i]*s[i] for i in range(3)])
    if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(s)*.22),segments=segments,affect='EDGES')
    append(bm,name,c,tile)

def tube(name,a,b,r,tile=2,sides=12):
    bm=bmesh.new();d=Vector(b)-Vector(a)
    bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=sides,radius1=r,radius2=r,depth=d.length)
    q=Vector((0,0,1)).rotation_difference(d.normalized())
    for v in bm.verts:v.co=q@v.co
    append(bm,name,(Vector(a)+Vector(b))*.5,tile)

def torus(name,c,r,thickness,tile=2):
    bm=bmesh.new();vv=[];n=24;minor=6
    for i in range(n):
        a=2*math.pi*i/n
        for j in range(minor):
            t=2*math.pi*j/minor;rr=r+thickness*math.cos(t)
            vv.append(bm.verts.new((c[0]+rr*math.cos(a),c[1]+thickness*math.sin(t),c[2]+rr*math.sin(a))))
    for i in range(n):
        for j in range(minor):bm.faces.new([vv[i*minor+j],vv[((i+1)%n)*minor+j],vv[((i+1)%n)*minor+(j+1)%minor],vv[i*minor+(j+1)%minor]])
    append(bm,name,(0,0,0),tile)

MODELS={}
def start(name):
    global CURRENT,DATA
    CURRENT=name;DATA={'v':[],'f':[],'uv':[]};MODELS[name]=DATA

# 1 x 1 m aperture: callers scale X/Y to the surveyed 0.62-bay glass envelope.
# Outward depth <=0.19 m, open centre leaves the original interior-mapped glass visible.
start('WindowReveal')
for x in (-.53,.53):
    box('deep stone jamb',(x,0,-.055),(.075,1.14,.15),0,.006)
    box('rebated bronze frame',(x*.948,0,-.111),(.025,1.02,.035),1,.003)
box('recessed lintel',(0,.555,-.055),(1.14,.07,.15),0,.006)
box('sloping projecting sill',(0,-.55,-.095),(1.18,.085,.19),0,.008)
box('drip edge under sill',(0,-.594,-.164),(1.12,.014,.025),2,.002,1)
box('thin vertical mullion',(0,0,-.02),(.022,1.0,.026),1,.002,1)
box('thin transom',(0,.14,-.02),(1.0,.025,.026),1,.002,1)

start('EntranceSurround')
# Decorative replacement occupies the previous non-interactive entrance's envelope.
for x in (-.88,.88):
    box('dressed stone jamb',(x,1.5,-.055),(.14,2.9,.16),0,.008)
    for y in (.38,.97,1.56,2.15,2.74):box('jamb block joint',(x,y,-.142),(.145,.012,.003),2,0)
box('stone lintel',(0,2.92,-.055),(1.9,.18,.18),0,.008)
box('brushed door leaf',(0,1.35,-.099),(1.3,2.4,.055),1,.008)
for x in (-.59,.59):box('door stile',(x,1.35,-.137),(.065,2.33,.035),2,.003)
for y in (.26,1.60,2.49):box('door rail',(0,y,-.14),(1.23,.065,.035),2,.003)
box('nonemissive upper glazed panel',(0,2.06,-.142),(1.04,.72,.014),2,.003)
box('bottom kick plate',(0,.32,-.148),(1.1,.30,.016),2,.003)
for y in (1.03,1.28):tube('pull handle post',(.43,y,-.15),(.43,y,-.20),.014,2,10)
tube('pull handle',(.43,1.03,-.20),(.43,1.28,-.20),.016,2,12)
box('stone canopy',(0,3.0,-.39),(2.08,.12,.78),0,.009)
for x in (-.65,.65):box('canopy support',(x,2.86,-.24),(.10,.19,.37),2,.005)

start('Condenser')
box('sealed folded casing',(0,.56,0),(1.58,.92,1.13),1,.018,3)
box('raised lid',(0,1.04,0),(1.65,.055,1.20),1,.009)
for x in (-.57,.57):box('anti vibration foot',(x,.06,0),(.18,.12,1.14),2,.006)
box('dark radiator recess',(0,.54,-.572),(1.39,.72,.018),2,.004)
for j in range(17):box('radiator louvre',(0,.22+j*.039,-.595),(1.34,.014,.028),1,.002,1)
torus('rolled fan guard rim',(.0,1.087,0),.40,.016)
for x in (-.30,-.22,-.14,-.06,.02,.10,.18,.26):
    z=math.sqrt(.38*.38-x*x);tube('fan safety grid',(x,1.085,-z),(x,1.085,z),.004,2,6)
for z in (-.28,-.18,-.08,.02,.12,.22,.32):
    x=math.sqrt(.38*.38-z*z);tube('fan safety grid',(-x,1.09,z),(x,1.09,z),.004,2,6)
tube('motor hub',(0,1.063,0),(0,1.10,0),.065,2,16)
for x in (-.70,.70):
    for z in (-.47,.47):tube('captive lid fastener',(x,1.07,z),(x,1.083,z),.016,2,6)
for z in (-.24,.24):tube('refrigerant connection',(.795,.30,z),(1.02,.30,z),.025,2,10)

start('CurbStone')
box('worn dressed curb',(0,.081,0),(.997,.162,.318),0,.012,3)

start('DrainGrate')
for x in (-.29,.29):box('cast outer frame',(x,.012,0),(.04,.024,.64),2,.004)
for z in (-.29,.29):box('cast outer frame',(0,.012,z),(.54,.024,.04),2,.004)
box('black drainage cavity',(0,-.008,0),(.56,.012,.56),2,.002)
for x in (-.23,-.1725,-.115,-.0575,0,.0575,.115,.1725,.23):box('cast slotted bar',(x,.016,0),(.022,.032,.55),2,.004)
for x in (-.26,.26):
    for z in (-.26,.26):tube('recessed frame anchor',(x,.024,z),(x,.033,z),.012,2,6)

start('FracturedStone')
bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=2,radius=1)
rng=random.Random(912)
for v in bm.verts:
    q=v.co.normalized();r=1+rng.uniform(-.10,.10)+.09*math.sin(q.x*9+q.z*4)
    v.co=Vector((q.x*.42*r,max(-.05,q.y*.23*r+.11),q.z*.33*r))
append(bm,'fractured weathered stone',(0,0,0),0)

start('GrassClump')
rng=random.Random(130)
for i in range(29):
    a=rng.random()*2*math.pi;rr=rng.random()*.12;h=rng.uniform(.12,.32);w=rng.uniform(.006,.014)
    lean=Vector((math.cos(a)*h*.40,0,math.sin(a)*h*.40));base=Vector((math.cos(a)*rr,0,math.sin(a)*rr))
    right=Vector((-math.sin(a)*w,0,math.cos(a)*w))
    # A folded, double-sided blade has actual changing normals, no alpha-card rectangle.
    pts=[base-right,base,base+right,base+lean*.45+Vector((0,h*.57,0))-right*.55,
         base+lean*.45+Vector((0,h*.57+.003,0)),base+lean*.45+Vector((0,h*.57,0))+right*.55,
         base+lean+Vector((0,h,0))]
    for face in ((0,1,4,3),(1,2,5,4),(3,4,6),(4,5,6)):
        ind=len(DATA['v']);DATA['v'].extend(tuple(unity_to_blender(pts[j])) for j in face)
        DATA['f'].append(tuple(ind+j for j in range(len(face))))
        DATA['uv'].append([(.5+PAD+.18*(j%2),.5+PAD+.44*(pts[k].y/h)) for j,k in enumerate(face)])
        # Separate back vertices survive FBX's triangulation; coincident reverse-wound
        # polygons sharing the same indices are otherwise deduplicated by its bmesh pass.
        back=len(DATA['v']);DATA['v'].extend(tuple(unity_to_blender(pts[j])) for j in face)
        DATA['f'].append(tuple(back+j for j in reversed(range(len(face)))))
        DATA['uv'].append(list(reversed(DATA['uv'][-1])))

report={'version':1,'authoring':'Blender 4.5 LTS; deterministic bmesh solids; CPU Cycles 8-sample procedural atlas bake',
        'axes':'Unity X-right/Y-up/-Z front -> Blender X-right/Z-up/+Y front; FBX -Z forward Y-up; metres',
        'physics':'All exports are visual only, without colliders, lights, cameras, animation or gameplay scripts.',
        'atlas':{'size':SIZE,'tiles':['cut limestone','powder coated steel','oxidised cast iron','moss and leaf'],
                 'channels':{'A':'sRGB unlit base colour','N':'linear tangent-space normal OpenGL +Y','M':'linear R metallic, G neutral 0.98 occlusion, B unused, A smoothness'},
                 'compressed_full_mips_estimate_MiB':16},'models':{},'pieces':PIECES}

for name,d in MODELS.items():
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(d['v'],[],d['f']);mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for poly,coords in zip(mesh.polygons,d['uv']):
        for li,coord in zip(poly.loop_indices,coords):uv.data[li].uv=coord
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);mesh.materials.append(shared)
    # Flat source face normals preserve dressed stone facets; bevel strips round highlights.
    for poly in mesh.polygons:poly.use_smooth=False
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    export=DST/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,
        add_leaf_bones=False,bake_anim=False,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',path_mode='STRIP',
        use_custom_props=False,mesh_smooth_type='FACE',use_triangles=True)
    mesh.calc_loop_triangles()
    # Inverse source-axis conversion back to published game coordinates.
    vv=[Vector((v.co.x,v.co.z,-v.co.y)) for v in mesh.vertices]
    report['models'][name]={'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices),
        'bounds_min':[min(v[i] for v in vv) for i in range(3)],'bounds_max':[max(v[i] for v in vv) for i in range(3)],
        'export':str(export.relative_to(P)),'bytes':export.stat().st_size,'sha256':hashlib.sha256(export.read_bytes()).hexdigest()}
    print('[EnvironmentRealism] '+name+' '+str(report['models'][name]['triangles'])+' triangles',flush=True)

for channel,im in images.items():
    path=Path(im.filepath_raw);report['atlas'][channel]={'path':str(path.relative_to(P)),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    im.filepath='//../../Assets/Parasite/Art/EnvironmentRealism/Textures/'+path.name
source=SRC/'EnvironmentRealism.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
report['packed_source']={'path':str(source.relative_to(P)),'bytes':source.stat().st_size,'sha256':hashlib.sha256(source.read_bytes()).hexdigest()}
(SRC/'production-manifest.json').write_text(json.dumps(report,indent=2)+'\n')
print('[EnvironmentRealism] COMPLETE source and seven exports; no Unity import/render claimed',flush=True)
