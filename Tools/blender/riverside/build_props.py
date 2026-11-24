"""Original deterministic riverside art, author/export only; no rendering or GPU work.
Unity(X,Y,Z) metre coordinates become Blender(X,-Z,Y). One merged mesh per surface.
"""
import bpy, bmesh, math, os, sys, random, json
from mathutils import Vector, Matrix
import numpy as np
P,STAGE=map(os.path.abspath,sys.argv[sys.argv.index('--')+1:]); OUT=os.path.join(STAGE,'Resources/Riverside'); SOURCE=os.path.join(STAGE,'ArtSource/Riverside')
TEX=os.path.join(OUT,'Textures'); os.makedirs(TEX,exist_ok=True);os.makedirs(SOURCE,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection' and not c.objects:bpy.data.collections.remove(c)
MATS={};RNG=random.Random(70725);ASSETS=[];ACTIVE=None

def v(p):return Vector((p[0],-p[2],p[1]))
def image(name,data):
    h,w,_=data.shape;im=bpy.data.images.new(name,width=w,height=h,alpha=True);im.pixels.foreach_set(data.astype(np.float32).ravel());im.filepath_raw=os.path.join(TEX,name+'.png');im.file_format='PNG';im.save();return im

def material(name,color,rough=.45,metal=0,texture=None,emit=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
    if emit:bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=emit
    if texture:
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=texture;m.node_tree.links.new(n.outputs['Color'],bs.inputs['Base Color'])
    MATS[name]=m;return m

def textures():
    n=512;y,x=np.mgrid[0:n,0:n]/n;noise=np.random.default_rng(70725).normal(0,1,(n,n))
    for name,rgb in [('MarinePaint',(.17,.31,.34)),('Rubber',(.055,.064,.067)),('ClinicEnamel',(.76,.79,.73)),('ClinicFabric',(.66,.74,.70))]:
        if name=='MarinePaint':
            chips=(np.sin(x*math.tau*17+np.sin(y*math.tau*8))+.65*np.sin(y*math.tau*71+x*9)+noise*.5)>1.55
            variation=.92+noise*.025+.035*np.sin(y*math.tau*47);c=variation[:,:,None]*np.array(rgb);c[chips]=np.array((.28,.16,.09));height=chips*.015+noise*.0015;rough=.52+chips*.3+noise*.02;metal=.15+chips*.5
        elif name=='Rubber':
            grain=.7+.12*np.sin(x*math.tau*91)+noise*.11;c=grain[:,:,None]*np.array(rgb);height=noise*.003;rough=.84+noise*.03;metal=np.zeros((n,n))
        elif name=='ClinicFabric':
            weave=.5*np.sin(x*math.tau*181)*np.sin(y*math.tau*181);c=(.94+weave*.04+noise*.02)[:,:,None]*np.array(rgb);height=weave*.008+noise*.001;rough=.86+noise*.01;metal=np.zeros((n,n))
        else:
            c=(.98+noise*.012)[:,:,None]*np.array(rgb);height=noise*.0008;rough=.27+noise*.02;metal=np.zeros((n,n))
        al=np.ones((n,n,4));al[:,:,:3]=np.clip(c,0,1);albedo=image(name+'_A',al)
        dy,dx=np.gradient(height);normal=np.ones((n,n,4));normal[:,:,0]=np.clip(.5-dx*9,0,1);normal[:,:,1]=np.clip(.5-dy*9,0,1);image(name+'_N',normal)
        mask=np.ones((n,n,4));mask[:,:,:3]=metal[:,:,None];mask[:,:,3]=np.clip(1-rough,0,1);image(name+'_M',mask)
        material('River'+name,rgb,float(np.mean(rough)),float(np.mean(metal)),albedo)
    for key,color,rough,metal in [('Steel',(.38,.42,.44),.36,.85),('Timber',(.39,.25,.13),.7,0)]:
        path=os.path.join(P,'Assets/Parasite/Resources/Cinematics/Textures',key+'_A.png');im=bpy.data.images.load(path,check_existing=True);material('Cinema'+key,color,rough,metal,im)
    for name,color,rough,metal in [('Dark',(.035,.045,.05),.45,.65),('Glass',(.065,.14,.18),.08,.35),('Cream',(.76,.77,.68),.4,.1),('Red',(.53,.07,.045),.4,.1),('Yellow',(.82,.56,.12),.45,.2),('Shore',(.29,.27,.21),.94,0),('Stone',(.31,.35,.32),.88,0),('Reed',(.20,.26,.14),.9,0)]:
        material('Cinema'+name,color,rough,metal)
    material('CinemaGlow',(.12,.65,.43),.24,.1,emit=1.4);material('CinemaWarm',(.9,.48,.17),.25,.1,emit=1.2)

def attach(o,name,mat,bevel=0):
    o.name=name
    for collection in list(o.users_collection):collection.objects.unlink(o)
    ACTIVE.objects.link(o);o.data.materials.append(MATS[mat]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        # Avoid collapsed faces when a requested radius reaches half a thin panel/gauge thickness.
        safe_radius=min(bevel,min(o.dimensions)*.4)
        mod=o.modifiers.new('Manufactured edge radii','BEVEL');mod.width=safe_radius;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def box(name,p,d,mat='RiverMarinePaint',bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p));o=bpy.context.object;o.dimensions=(d[0],d[2],d[1]);return attach(o,name,mat,bevel)

def cyl(name,a,b,r,mat='CinemaSteel',vertices=16):
    aa,bb=v(a),v(b);delta=bb-aa;bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=delta.length,location=(aa+bb)/2);o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=delta.to_track_quat('Z','Y');o=attach(o,name,mat,min(.008,r*.12))
    for poly in o.data.polygons:poly.use_smooth=len(poly.vertices)==4
    return o

def ring(name,p,major,minor,mat='CinemaSteel',axis='Y',segments=32):
    bpy.ops.mesh.primitive_torus_add(major_radius=major,minor_radius=minor,major_segments=segments,minor_segments=8,location=v(p));o=bpy.context.object
    if axis=='Z':o.rotation_euler[0]=math.pi*.5
    if axis=='X':o.rotation_euler[1]=math.pi*.5
    o=attach(o,name,mat)
    for poly in o.data.polygons:poly.use_smooth=True
    return o

def mesh(name,points,faces,mat):
    me=bpy.data.meshes.new(name);me.from_pydata([v(p) for p in points],[],faces);me.update();ob=bpy.data.objects.new(name,me);ACTIVE.objects.link(ob);ob.data.materials.append(MATS[mat]);return ob

def text(name,body,p,size,mat='CinemaCream',facing='front'):
    curve=bpy.data.curves.new(name,'FONT');curve.body=body;curve.align_x='CENTER';curve.align_y='CENTER';curve.size=size;curve.extrude=.0007
    o=bpy.data.objects.new(name,curve);ACTIVE.objects.link(o);o.location=v(p);o.rotation_euler=(math.pi*.5,0,0) if facing=='front' else (0,0,0);o.data.materials.append(MATS[mat]);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False);return o

def new_asset(name):
    global ACTIVE
    ACTIVE=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(ACTIVE)

def finish_asset(name):
    bpy.context.view_layer.update()
    for o in list(ACTIVE.objects):
        if o.type!='MESH':continue
        # UVs preserve meter-scale wear across assembled panels, instead of stretching one scratch over a vessel.
        layer=o.data.uv_layers.active or o.data.uv_layers.new(name='Surface metres')
        for poly in o.data.polygons:
            axis=max(range(3),key=lambda i:abs(poly.normal[i]));axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                co=o.matrix_world @ o.data.vertices[o.data.loops[li].vertex_index].co;layer.data[li].uv=(co[axes[0]],co[axes[1]])
    for mat in MATS.values():
        group=[o for o in ACTIVE.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat]
        if not group:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in group:o.select_set(True)
        bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();o=bpy.context.object;o.name=mat.name+'_Surface';bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    for o in ACTIVE.objects:
        # Freeze one triangulation for every engine, and remove zero-area font tessellation remnants.
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
        def triangle_area(face):
            a,b,c=[vertex.co for vertex in face.verts]
            return (b-a).cross(c-a).length*.5
        degenerate=[face for face in bm.faces if triangle_area(face)<=1e-9]
        if degenerate:bmesh.ops.delete(bm,geom=degenerate,context='FACES_ONLY')
        loose_edges=[edge for edge in bm.edges if not edge.link_faces]
        if loose_edges:bmesh.ops.delete(bm,geom=loose_edges,context='EDGES')
        loose_vertices=[vertex for vertex in bm.verts if not vertex.link_edges]
        if loose_vertices:bmesh.ops.delete(bm,geom=loose_vertices,context='VERTS')
        bm.to_mesh(o.data);bm.free();o.data.update()
    # Unity's verified FBX import preserves X/Y but reflects authored Z. Compensate only at
    # the export boundary: retain the original Blender authoring coordinates and source films.
    # Temporary meshes avoid a negative runtime scale and leave the editable lineup untouched.
    export_collection=bpy.data.collections.new(name+'__UnityExport')
    bpy.context.scene.collection.children.link(export_collection)
    bpy.ops.object.select_all(action='DESELECT')
    export_objects=[]
    for source in list(ACTIVE.objects):
        copy=source.copy();copy.data=source.data.copy();copy.name=source.name+'__UnityExport'
        export_collection.objects.link(copy)
        copy.data.transform(Matrix.Diagonal((1,-1,1,1)) @ source.matrix_world)
        copy.matrix_world=Matrix.Identity(4);copy.data.flip_normals();copy.data.update()
        copy.select_set(True);export_objects.append(copy)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    for copy in export_objects:
        data=copy.data;bpy.data.objects.remove(copy,do_unlink=True);bpy.data.meshes.remove(data)
    bpy.data.collections.remove(export_collection)
    points=[o.matrix_world @ vertex.co for o in ACTIVE.objects for vertex in o.data.vertices]
    lo=[min(c[i] for c in points) for i in range(3)];hi=[max(c[i] for c in points) for i in range(3)]
    # Convert manifest bounds back to Unity axes.
    bounds={'min':[lo[0],lo[2],-hi[1]],'max':[hi[0],hi[2],-lo[1]]}
    tris=sum(sum(len(poly.vertices)-2 for poly in o.data.polygons) for o in ACTIVE.objects)
    slots=sorted(set(m.name for o in ACTIVE.objects for m in o.data.materials))
    info={'asset':name,'triangles':tris,'meshes':len(ACTIVE.objects),'materials':slots,'unity_bounds_metres':bounds};ASSETS.append(info)
    print('[riverside] EXPORTED',json.dumps(info),flush=True)


def ferry():
    new_asset('RiversideFerry')
    # A compact working ferry: welded V-chine hull, rounded deck edges, pilothouse, fenders and shore intake.
    sections=[(-4,.7,2.3),(-3.5,.9,2.85),(-2,.95,3), (1.9,.95,3),(3,.8,2.6),(4,.65,1.7)]
    points=[]
    for z,bottom,top in sections:
        points.extend([(-top,.38,z),(-bottom,-.75,z),(bottom,-.75,z),(top,.38,z)])
    faces=[]
    for j in range(len(sections)-1):
        for k in range(3):faces.append((j*4+k,j*4+k+1,(j+1)*4+k+1,(j+1)*4+k))
    faces.extend([(3,2,1,0),(20,21,22,23)])
    mesh('Welded double chine hull',points,faces,'RiverMarinePaint')
    box('Recessed walking deck',(0,.35,-.05),(5.85,.12,7.7),'CinemaTimber',.04)
    for x in [-2.55,2.55]:
        for z in [-3,-1,1,2.6]:
            cyl('Rail stanchion',(x,.43,z),(x,1.3,z),.028)
        for y in [.88,1.3]:cyl('Continuous handrail',(x,y,-3.4),(x,y,2.8),.026)
        box('Hull rubbing strip',(x*1.14,.24,-.15),(.10,.13,6.8),'CinemaDark',.04)
    # Pilothouse respects the old cabin collider: width3.5, depth2.5, height2.2.
    box('Pilothouse rear bulkhead',(0,1.45,-3.1),(3.5,2.2,.16),'CinemaCream',.05)
    for x in [-1.68,1.68]:
        box('Pilothouse side lower',(x,.98,-2),(.14,1.3,2.35),'CinemaCream',.035)
        box('Pilothouse window surround',(x,1.95,-2),(.15,.78,2.35),'CinemaDark',.035)
        box('Pilothouse side glass',(x*1.005,1.95,-2),(.03,.63,2.1),'CinemaGlass',.025)
        for z in [-3.08,-2,-.91]:box('Window side mullion',(x*1.013,1.95,z),(.045,.76,.055),'CinemaCream',.008)
    box('Pilothouse front lower',(0,.95,-.77),(3.5,1.2,.14),'CinemaCream',.04)
    box('Pilothouse front glass',(0,1.91,-.71),(3.2,.73,.055),'CinemaGlass',.035)
    for x in [-1.65,-.55,.55,1.65]:box('Windscreen mullion',(x,1.93,-.66),(.06,.82,.06),'CinemaCream',.01)
    for x in [-.55,.55]:cyl('Wiper blade',(x-.17,1.62,-.62),(x+.13,2.18,-.62),.016,'CinemaDark',8)
    box('Pilothouse overhanging roof',(0,2.64,-2),(3.85,.16,2.85),'RiverMarinePaint',.065)
    for x in [-1.05,1.05]:
        cyl('Vent roof pedestal',(x,2.72,-2.4),(x,3.0,-2.4),.09)
        cyl('Vent cowling',(x,3.0,-2.4),(x,3.08,-2.4),.18,'CinemaCream',24)
    cyl('Navigation mast',(0,2.72,-2.5),(0,3.8,-2.5),.04)
    cyl('Radio whip',(0,3.8,-2.5),(.1,4.5,-2.5),.009,'CinemaDark',8)
    box('Navigation lamp',(0,3.8,-2.5),(.18,.12,.15),'CinemaWarm',.025)
    for side in [-1,1]:
        for z in [-2.8,-.8,1.2]:
            ring('Tire fender',(side*2.91,-.04,z),.34,.09,'RiverRubber',axis='X')
            cyl('Fender lanyard',(side*2.64,.58,z),(side*2.91,.25,z),.015,'CinemaCream',8)
    ring('Rescue lifebuoy',(1.77,1.05,-1.4),.26,.065,'CinemaRed',axis='X')
    for x in [-2.1,2.1]:
        for z in [-3.3,2.8]:
            cyl('Mooring cleat base',(x,.42,z),(x,.53,z),.055)
            cyl('Mooring cleat horn',(x-.15,.54,z),(x+.15,.54,z),.045)
    # Intake faces the existing waypoint at localz4.2; open grille gaps retain visual clarity.
    box('Intake flange',(0,.17,4.05),(1.65,.46,.16),'CinemaSteel',.03)
    box('Intake shadow',(0,.17,4.15),(1.4,.29,.02),'CinemaDark',0)
    for x in [-.6,-.4,-.2,0,.2,.4,.6]:box('Intake grille bar',(x,.17,4.18),(.035,.28,.035),'CinemaSteel',.005)
    box('Intake status lamp',(.72,.32,4.18),(.06,.06,.03),'CinemaGlow',.008)
    text('Ferry registration','RIVERSIDE / 07',(0,2.43,-.64),.17)
    text('Ferry safety notice','CREW ONLY',(0,.98,-.66),.13,'CinemaDark')
    finish_asset('RiversideFerry')


def floodgate():
    new_asset('FloodgateMachinery')
    box('Pump skid base',(0,.13,0),(3.4,.26,2),'CinemaDark',.04)
    for x in [-1.3,1.3]:
        box('Skid runner',(x,.25,0),(.15,.2,2.1),'CinemaSteel')
        for z in [-.75,.75]:cyl('Anchor bolt',(x,.35,z),(x,.41,z),.04,'CinemaDark',6)
    cyl('Motor body',(-1,.72,0),(.45,.72,0),.34,'RiverMarinePaint',32)
    for i in range(14):ring('Cooling fin',(-.9+i*.085,.72,0),.34,.017,'CinemaDark',axis='X')
    cyl('Pump impeller housing',(.45,.72,0),(1.15,.72,0),.43,'CinemaSteel',40)
    for i in range(10):
        a=i*math.tau/10;cyl('Pump flange bolt',(1.16,.72+math.sin(a)*.35,math.cos(a)*.35),(1.2,.72+math.sin(a)*.35,math.cos(a)*.35),.03,'CinemaDark',6)
    cyl('Inlet pipe',(1,.72,0),(1,.72,1.05),.2)
    cyl('Vertical outlet',(1,.85,0),(1,2.45,0),.15)
    ring('Outlet coupling',(1,1.25,0),.20,.035)
    cyl('Actuator stem',(1,1.65,0),(1.6,1.65,0),.04)
    ring('Isolation handwheel',(1.67,1.65,0),.27,.026,'CinemaRed',axis='X')
    for i in range(4):
        a=i*math.tau/4;cyl('Valve wheel spoke',(1.67,1.65,0),(1.67,1.65+math.sin(a)*.25,math.cos(a)*.25),.014,'CinemaRed',8)
    cyl('Pressure gauge neck',(.9,1.8,0),(.9,2.05,0),.03)
    cyl('Pressure gauge case',(.9,2.1,-.04),(.9,2.1,-.16),.13)
    cyl('Gauge white face',(.9,2.1,-.165),(.9,2.1,-.18),.105,'CinemaCream',32)
    cyl('Gauge needle',(.9,2.1,-.187),(.96,2.16,-.187),.007,'CinemaDark',8)
    box('Motor electrical box',(-.6,1.17,0),(.5,.24,.4),'RiverMarinePaint')
    for z in [-.95,.95]:box('Safety stripe',(0,.28,z),(2.6,.025,.08),'CinemaYellow',0)
    text('Pump ID','VALE / RIVER INTAKE',(-.3,.48,-1.03),.12)
    finish_asset('FloodgateMachinery')


def mast():
    new_asset('RadioMast')
    for p in [(-.55,0,-.42),(.55,0,-.42),(0,0,.54)]:
        cyl('Lattice tower leg',p,(p[0]*.35,16,p[2]*.35),.045)
        box('Tower concrete anchor',(p[0],.12,p[2]),(.55,.24,.55),'CinemaStone',.025)
    for j in range(12):
        y=.3+j*1.3;s0=1-y/25;s1=1-(y+1.3)/25
        points=[(-.55,-.42),(.55,-.42),(0,.54)]
        for k in range(3):
            a,b=points[k],points[(k+1)%3]
            cyl('Tower diagonal brace',(a[0]*s0,y,a[1]*s0),(b[0]*s1,y+1.3,b[1]*s1),.018)
            cyl('Tower cross brace',(a[0]*s0,y,a[1]*s0),(b[0]*s0,y,b[1]*s0),.021)
    for y in [3,5,7,9,11,13]:
        cyl('Ladder rung',(-.2,y,-.48),(.2,y,-.48),.018)
    cyl('Ladder left stile',(-.2,.3,-.48),(-.2,14,-.48),.022)
    cyl('Ladder right stile',(.2,.3,-.48),(.2,14,-.48),.022)
    for y in [6.5,10.1,13.8]:
        cyl('Antenna mounting boom',(-1.1,y,0),(1.1,y,0),.028)
        for x in [-.85,.85]:
            cyl('Dipole upper',(x,y,0),(x,y+.52,0),.012)
            cyl('Dipole lower',(x,y-.52,0),(x,y,0),.012)
    ring('Microwave dish rim',(.65,9,.1),.43,.025,axis='X')
    # Closed shallow concave dish, reduced to a lathe mesh.
    points=[(.65,9,.1)];faces=[]
    for j in range(1,6):
        r=.43*j/5
        for i in range(32):
            a=i*math.tau/32;points.append((.65+.17*(j/5)**2,9+math.sin(a)*r,.1+math.cos(a)*r))
    for i in range(32):faces.append((0,1+i,1+(i+1)%32))
    for j in range(4):
        for i in range(32):faces.append((1+j*32+i,1+j*32+(i+1)%32,1+(j+1)*32+(i+1)%32,1+(j+1)*32+i))
    mesh('Parabolic microwave dish',points,faces,'CinemaCream')
    box('Aviation obstruction beacon',(0,16.13,0),(.16,.22,.16),'CinemaRed',.035)
    finish_asset('RadioMast')


def console(kind):
    name=kind+'Console';new_asset(name)
    mat='RiverClinicEnamel' if kind=='Clinic' else 'RiverMarinePaint'
    box('Equipment plinth',(0,.2,0),(1.04,.14,.5),'CinemaDark',.025)
    box('Pressed steel housing',(0,.82,0),(1.1,1.12,.54),mat,.045)
    box('Instrument bezel',(0,1.19,-.258),(1.02,.52,.06),'CinemaDark',.025)
    # Central aperture is deliberately empty: the game's existing animated Screen stays visible.
    for x in [-.47,.47]:box('Screen bezel side',(x,1.2,-.333),(.055,.43,.035),'CinemaSteel',.009)
    for y in [.985,1.415]:box('Screen bezel edge',(0,y,-.333),(.96,.035,.035),'CinemaSteel',.006)
    for y in [.38,.45,.52,.59]:box('Vent louvre',(0,y,-.282),(.78,.022,.025),'CinemaDark',.005)
    for x in [-.45,.45]:
        for y in [.32,.88,1.36]:cyl('Panel screw',(x,y,-.303),(x,y,-.32),.014,'CinemaSteel',8)
    if kind=='Industrial':
        for x in [-.31,0,.31]:
            cyl('Selector dial',(x,.77,-.29),(x,.77,-.35),.062,'CinemaDark',24)
            box('Dial marker',(x,.81,-.358),(.012,.055,.013),'CinemaCream',.003)
        box('Emergency stop surround',(.41,.74,-.31),(.13,.13,.025),'CinemaYellow',.015)
        cyl('Emergency mushroom stop',(.41,.74,-.33),(.41,.74,-.38),.045,'CinemaRed',24)
        text('Panel legend','ISOLATE / MANUAL / AUTO',(0,.92,-.348),.04)
    elif kind=='Radio':
        for x in [-.31,0,.31]:cyl('Tuning knob',(x,.77,-.29),(x,.77,-.36),.052,'CinemaDark',24)
        for i in range(7):box('Speaker grille',(-.28+i*.09,.58,-.296),(.035,.12,.018),'CinemaDark',.003)
        cyl('Handset left grip',(-.43,.74,.1),(-.43,1.05,.1),.045,'CinemaDark',16)
        for y in [.74,1.05]:box('Handset earpiece',(-.43,y,.1),(.13,.08,.1),'CinemaDark',.02)
        for i in range(12):ring('Handset coiled lead',(-.49,.32+i*.025,.08),.045,.01,'CinemaDark',axis='Y',segments=12)
        text('Radio equipment ID','RIVERSIDE / RELAY 07',(0,.92,-.348),.045)
    else:
        box('Analyzer cartridge drawer',(0,.77,-.3),(.75,.19,.035),'CinemaDark',.015)
        box('Analyzer pull tab',(0,.77,-.34),(.28,.035,.04),'CinemaSteel',.006)
        for i in range(3):
            x=-.33+i*.12;cyl('Sample tube holder',(x,1.41,.12),(x,1.48,.12),.045,'CinemaDark',16)
            cyl('Sealed specimen vial',(x,1.48,.12),(x,1.66,.12),.03,'CinemaGlass',20)
            cyl('Vial cap',(x,1.66,.12),(x,1.7,.12),.034,'CinemaRed',16)
        text('Analyzer equipment ID','WATER / BIOASSAY',(0,.92,-.348),.045)
    finish_asset(name)


def screen():
    new_asset('ClinicScreen')
    for x in [-.13,.13]:
        for z in [-2.17,2.17]:cyl('Screen leg',(x,.16,z),(x,.52,z),.022)
    for z in [-2.17,0,2.17]:cyl('Screen vertical frame',(0,.3,z),(0,2.02,z),.025)
    for y in [.38,2.02]:cyl('Screen horizontal frame',(0,y,-2.17),(0,y,2.17),.025)
    for iz in range(28):
        z=-2.1+iz*.15;box('Curtain stitched pleat',(.018*math.sin(iz*2),1.22,z),(.022,1.54,.17),'RiverClinicFabric',.004)
    finish_asset('ClinicScreen')


def banks():
    new_asset('RiversideBanks')
    points=[];faces=[];nx,nz=100,24
    for j in range(nz+1):
        t=j/nz
        for i in range(nx+1):
            x=-180+i*360/nx;edge=2.1*math.sin(x*.039)+1.3*math.sin(x*.091)
            z=edge-t*72
            height=-.3+3.5*t+7.5*t*t*(.5+.5*math.sin(x*.023+1.7))+.4*math.sin(x*.17+t*6)*t
            points.append((x,height,z))
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    land=mesh('Layered meandering riverbank',points,faces,'CinemaShore')
    land.data.materials.append(MATS['CinemaStone']);land.data.materials.append(MATS['CinemaReed'])
    for poly in land.data.polygons:
        co=poly.center;poly.material_index=2 if poly.index//nx<3 else 1 if poly.index//nx>17 else 0
    finish_asset('RiversideBanks')

textures();ferry();floodgate();mast();console('Industrial');console('Radio');console('Clinic');screen();banks()
# In the editable source, park each complete prop on a spaced authoring lineup after exports.
for index,asset in enumerate(ASSETS):
    collection=bpy.data.collections[asset['asset']]
    if asset['asset']=='RiversideBanks':continue
    for obj in collection.objects:obj.location.x+=index*10
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'RiversideProps.blend'),compress=True)
with open(os.path.join(SOURCE,'asset_manifest.json'),'w') as f:json.dump({'seed':70725,'coordinates':'Unity metres X right, Y up, Z forward; FBX export compensates Unity import with Blender world-Y reflection and corrected winding','colliders':'None. Preserve the existing tested Unity collision objects and interaction points.','assets':ASSETS},f,indent=2)
print('[riverside] SOURCE_SAVED',flush=True)
