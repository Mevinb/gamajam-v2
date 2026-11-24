"""Deterministic, meter-scale PARASITE film sets, authored and exported with Blender.
Run via build_sets.sh. Own procedural meshes/textures; no downloaded assets.
Unity coordinates in this script are converted to Blender Z-up at construction.
"""
import bpy, math, os, sys, random, json
bpy.context.preferences.filepaths.save_version = 0
from mathutils import Vector, Matrix
import numpy as np

ARGS = sys.argv[sys.argv.index('--') + 1:]
OUT, SOURCE = map(os.path.abspath, ARGS[:2])
os.makedirs(OUT, exist_ok=True)
os.makedirs(SOURCE, exist_ok=True)
TEX = os.path.join(OUT, 'Textures'); os.makedirs(TEX, exist_ok=True)
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
FINAL_SOURCE = os.path.join(PROJECT, 'ArtSource', 'Cinematics')
FINAL_TEXTURES = os.path.join(PROJECT, 'Assets', 'Parasite', 'Resources', 'Cinematics', 'Textures')
RNG = random.Random(70194)
MATS = {}

def v(p): return Vector((p[0], -p[2], p[1]))
def image(name, data):
    h, w, _ = data.shape
    im = bpy.data.images.new(name, width=w, height=h, alpha=True)
    im.pixels.foreach_set(data.astype(np.float32).ravel())
    im.filepath_raw = os.path.join(TEX, name + '.png'); im.file_format = 'PNG'; im.save()
    return im

def textures():
    n = 1024
    y, x = np.mgrid[0:n, 0:n] / n
    noise = np.random.default_rng(70194).normal(0, 1, (n,n))
    for kind, rgb in [('Timber', (.42,.28,.15)), ('Steel',(.36,.40,.42)), ('Shore',(.32,.29,.23)), ('Stone',(.39,.41,.38))]:
        if kind == 'Timber':
            grain = np.sin(x*math.tau*37 + np.sin(y*math.tau*2)*1.3 + np.sin(x*math.tau*3)*2)
            grain += .35*np.sin(x*math.tau*157 + np.sin(y*math.tau)*6)
            knot = np.sqrt(((x-.42)*2.2)**2 + ((y-.6)*.55)**2)
            height = grain*.11 + .035*np.sin(knot*280) * np.exp(-knot*12) + noise*.015
            c = .9 + grain*.13 + noise*.035
            rough = .66 + .1*grain
        elif kind == 'Steel':
            height = .007*np.sin(x*math.tau*490) + noise*.007
            c = .96 + .025*np.sin(x*math.tau*490) + noise*.028
            rough = .34 + noise*.045
        else:
            # Tileable filtered noise creates mineral/gravel grain without a visible woven sine lattice.
            frequency = np.fft.fftfreq(n)
            radius = frequency[:,None]**2 + frequency[None,:]**2
            broad = np.fft.ifft2(np.fft.fft2(noise)*np.exp(-radius*1200)).real
            fine = np.fft.ifft2(np.fft.fft2(noise)*np.exp(-radius*45)).real
            broad /= max(broad.std(), 1e-6); fine /= max(fine.std(), 1e-6)
            height = broad*.025 + fine*.014 + noise*.008
            c = .92 + broad*.06 + fine*.035 + noise*.02
            rough = .88 + broad*.03
        color = np.ones((n,n,4)); color[:,:,:3] = np.clip(c[:,:,None] * np.array(rgb),0,1)
        albedo = image(kind + '_A', color)
        dy, dx = np.gradient(height)
        normal = np.ones((n,n,4)); normal[:,:,0] = np.clip(.5 - dx*12,0,1); normal[:,:,1] = np.clip(.5-dy*12,0,1); normal[:,:,2] = 1
        image(kind + '_N', normal)
        packed = np.ones((n,n,4)); packed[:,:,:3] = .8 if kind=='Steel' else 0; packed[:,:,3] = np.clip(1-rough,0,1)
        image(kind + '_M', packed)
        mat = bpy.data.materials.new('Cinema'+kind); mat.use_nodes = True
        bs = mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value = .35 if kind=='Steel' else .7
        bs.inputs['Metallic'].default_value = .8 if kind=='Steel' else 0
        node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = albedo
        mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
        MATS[kind] = mat
    for name, color, metallic, rough in [('Dark',(.045,.065,.07),.7,.4),('Paint',(.3,.39,.4),.25,.42),('Reed',(.23,.28,.12),0,.83),('Tip',(.15,.09,.04),0,.95),('Glow',(.15,.85,.6),.15,.2),('Warm',(.95,.54,.18),.1,.22)]:
        mat = bpy.data.materials.new('Cinema'+name); mat.diffuse_color=(*color,1); mat.use_nodes=True
        bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Metallic'].default_value=metallic; bs.inputs['Roughness'].default_value=rough
        if name in ('Glow','Warm'):
            bs.inputs['Emission Color'].default_value=(*color,1); bs.inputs['Emission Strength'].default_value=2
        MATS[name]=mat


def finish(obj,name,mat,bevel=0):
    obj.name=name; obj.data.materials.clear(); obj.data.materials.append(MATS[mat])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Machined edge radii','BEVEL'); mod.width=min(bevel, min(obj.dimensions)*.4); mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons: p.use_smooth = len(p.vertices)<5 and name.startswith(('Pipe','Tank','Ring','Bolt','Rock','Reed','Cable','Bench'))
    return obj

def box(name,p,d,mat='Steel',bevel=.014):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p)); o=bpy.context.object; o.dimensions=(d[0],d[2],d[1]); return finish(o,name,mat,bevel)

def cyl(name,a,b,r,mat='Steel',vertices=16):
    aa,bb=v(a),v(b); delta=bb-aa
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=delta.length,location=(aa+bb)/2)
    o=bpy.context.object; o.rotation_mode='QUATERNION'; o.rotation_quaternion=delta.to_track_quat('Z','Y'); return finish(o,name,mat,.004 if r>.04 else 0)

def ring(name,p,major,minor,mat='Steel'):
    bpy.ops.mesh.primitive_torus_add(major_radius=major,minor_radius=minor,major_segments=64,minor_segments=10,location=v(p)); return finish(bpy.context.object,name,mat)

def rock(name,p,size):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=v(p));o=bpy.context.object
    for vert in o.data.vertices:
        vert.co*=RNG.uniform(.82,1.16)
    o.scale=(size[0],size[2],size[1]);o.rotation_euler[2]=RNG.random()*math.tau;return finish(o,name,'Stone')

def mesh(name,points,faces,mat):
    me=bpy.data.meshes.new(name);me.from_pydata([v(p) for p in points],[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);ob.data.materials.append(MATS[mat]);return ob

def clear():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)

def uv_and_join():
    # Project UVs in world meters before merging: texture grain remains coherent across individual slats/panels.
    for ob in list(bpy.context.scene.objects):
        if ob.type != 'MESH': continue
        if not ob.data.uv_layers: ob.data.uv_layers.new(name='Surface meters')
        layer=ob.data.uv_layers.active.data
        for poly in ob.data.polygons:
            normal=poly.normal; axis=max(range(3),key=lambda i:abs(normal[i]))
            axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                co=ob.matrix_world @ ob.data.vertices[ob.data.loops[li].vertex_index].co
                layer[li].uv=(co[axes[0]],co[axes[1]])
    for mat in MATS.values():
        objects=[ob for ob in bpy.context.scene.objects if ob.type=='MESH' and ob.data.materials and ob.data.materials[0]==mat]
        if not objects:continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:ob.select_set(True)
        bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
        bpy.context.object.name=mat.name+'_Mesh'
        # Keep one meter-space origin per assembled prop.
        bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

def export(name):
    uv_and_join()
    # Portable paths resolve from the final source directory, never from the disposable staging folder.
    for im in bpy.data.images:
        if im.filepath:
            im.filepath = bpy.path.relpath(os.path.join(FINAL_TEXTURES, os.path.basename(im.filepath)), start=FINAL_SOURCE)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,name+'.blend'),compress=True,relative_remap=False)
    # Unity's FBX handedness conversion preserves X/Y but reverses this author's +Z convention.
    # Reflect Blender world Y only at the export boundary (with face winding corrected).
    # The saved .blend remains in its native film-authoring coordinates; packed movie scenes stay untouched.
    reflection = Matrix.Diagonal((1.0, -1.0, 1.0, 1.0))
    for ob in bpy.context.scene.objects:
        if ob.type != 'MESH': continue
        ob.data.transform(reflection @ ob.matrix_world)
        ob.matrix_world = Matrix.Identity(4)
        ob.data.flip_normals()
        ob.data.update()
    bpy.ops.object.select_all(action='SELECT')
    temporary = os.path.join(SOURCE, '.' + name + '.export.fbx')
    bpy.ops.export_scene.fbx(filepath=temporary,use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    os.replace(temporary, os.path.join(OUT,name+'.fbx'))
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH')
    print('[cinematics] EXPORTED',name,'triangles',tris,flush=True)
    return tris


def laboratory():
    clear()
    for ix in range(-5,6):
        for iz in range(-4,4):box('Floor service tile',(ix*1.05,-.085,iz*1.05), (1.026,.14,1.026),'Paint',.012)
    for ix in range(-4,5):
        box('Back insulated wall',(ix*1.23,2.25,3.08),(1.20,4.5,.18),'Paint',.02)
        box('Wall column',(ix*1.23+.56,2.25,2.93),(.07,4.5,.12),'Dark',.015)
        box('Wall light bar',(ix*1.23,3.73,2.88),(.73,.045,.05),'Glow',.008)
        for k in range(3):box('Vent fin',(ix*1.23, .5+k*.13,2.96),(.62,.028,.09),'Steel',.005)
    box('Side wall',(-5.58,2.25,0),(.16,4.5,6),'Paint')
    box('Ceiling service spine',(0,4.4,.8),(10,.18,.6),'Dark')
    for x in [-3,-1.5,1.5,3]:
        box('Suspended lamp casing',(x,3.9,.1),(.65,.12,2.4),'Dark')
        box('Suspended cold diffuser',(x,3.83,.1),(.48,.025,2.15),'Glow')
    # Hero containment vessel: bolts, pressure rings, glass mounting struts and specimen illumination.
    cyl('Tank tapered foundation',(0,.05,0),(0,.31,0),.61,'Dark',64)
    cyl('Tank lower plate',(0,.3,0),(0,.41,0),.57,'Steel',64)
    cyl('Tank upper cap',(0,2.38,0),(0,2.57,0),.56,'Steel',64)
    cyl('Tank lid inlay',(0,2.57,0),(0,2.63,0),.43,'Dark',64)
    for y in [.41,.49,2.31,2.39]:ring('Ring containment seal',(0,y,0),.478,.026,'Steel')
    for i in range(12):
        a=i*math.tau/12; x,z=math.sin(a)*.53,math.cos(a)*.53
        cyl('Bolt base fastener',(x,.405,z),(x,.43,z),.023,'Dark',6)
        cyl('Bolt lid fastener',(x,2.56,z),(x,2.59,z),.023,'Dark',6)
    for a in [math.pi*.30,math.pi*.70,math.pi*1.30,math.pi*1.70]:
        x,z=math.sin(a)*.49,math.cos(a)*.49
        cyl('Tank vertical stay',(x,.45,z),(x,2.34,z),.025,'Steel',12)
    ring('Ring illuminated lower track',(0,.5,0),.435,.012,'Glow')
    ring('Ring illuminated upper track',(0,2.29,0),.435,.012,'Glow')
    # Laboratory workstations sit clear of the historical actor positions.
    for x in [-3.1,3.15]:
        box('Workstation cabinet',(x,.48,1.82),(1.55,.92,.72),'Paint')
        box('Workstation countertop',(x,.99,1.82),(1.69,.09,.81),'Steel',.025)
        for k in range(3):
            box('Storage drawer',(x,.25+k*.26,1.44),(1.39,.2,.035),'Dark',.009)
            box('Drawer handle',(x,.3+k*.26,1.40),(.44,.025,.035),'Steel',.006)
        box('Monitor pedestal',(x,1.25,2),(.12,.45,.12),'Dark')
        box('Monitor housing',(x,1.6,1.98),(.77,.48,.08),'Dark',.025)
        box('Monitor screen',(x,1.6,1.925),(.68,.38,.012),'Glow',.006)
        for k in range(5):box('Monitor trace',(x-.23+k*.11,1.6+RNG.uniform(-.08,.08),1.913),(.045,RNG.uniform(.03,.16),.008),'Dark',0)
    for x in [-4.3,4.3]:
        cyl('Tank auxiliary cylinder',(x,.08,2),(x,1.67,2),.23,'Steel',32)
        ring('Ring auxiliary seam',(x,1.4,2),.24,.018,'Dark')
        cyl('Pipe supply neck',(x,1.67,2),(x,2.1,2),.04,'Dark')
        cyl('Pipe wall service',(x,2.1,2),(x,2.1,2.9),.04,'Steel')
    # Floor cable troughs and overhead pipe clamps provide depth in lateral camera moves.
    for z in [-2.55,2.55]:
        box('Floor channel',(0,-.001,z),(10.4,.012,.14),'Dark',0)
        for i in range(50):box('Floor channel grille',(-5+i*.2,.007,z),(.018,.012,.15),'Steel',0)
    for x in [-4.6,4.6]:
        cyl('Pipe overhead run',(x,3.35,-2.8),(x,3.35,2.9),.068,'Steel',16)
        for z in [-2,0,2]:ring('Ring spare coupling',(x,3.35,z),.09,.018,'Dark')
    export('ContainmentLaboratory')


def conduit():
    clear()
    # Open front, corrugated metallic rectangular conduit; coordinates match historical escape movement.
    box('Service conduit floor',(0,-.34,5),(1.28,.08,12),'Steel')
    box('Service conduit ceiling',(0,.52,5),(1.28,.08,12),'Dark')
    for x in [-.65,.65]:box('Service conduit side',(x,.08,5),(.08,.9,12),'Steel')
    for i in range(29):
        z=-.6+i*.4
        for x in [-.59,.59]:box('Service conduit rib',(x,.08,z),(.035,.83,.045),'Dark',.005)
        box('Service conduit ceiling rib',(0,.46,z),(1.17,.035,.045),'Dark',.005)
        box('Service conduit floor rib',(0,-.29,z),(1.17,.025,.045),'Dark',.003)
    for i in range(6):box('Service conduit guide lamp',(.53,.24,i*2),(.025,.08,.12),'Warm',.004)
    export('ServiceConduit')


def bench():
    clear()
    # Slats preserve the original seat contact height (0.52m) for the authored humanoid sitting pose.
    for iz in range(5):box('Bench seasoned seat slat',(0,.485,-.23+iz*.115),(1.78,.07,.098),'Timber',.015)
    for iy in range(4):box('Bench seasoned back slat',(0,.68+iy*.115,-.28),(1.78,.09,.065),'Timber',.012)
    for x in [-.61,.61]:
        for z in [-.19,.18]:cyl('Bench cast iron foot',(x,.02,z),(x,.45,z),.035,'Dark',12)
        cyl('Bench seat support',(x,.44,-.28),(x,.44,.27),.037,'Dark',12)
        cyl('Bench back support',(x,.3,-.3),(x,1.08,-.3),.032,'Dark',12)
        cyl('Bench arm upright',(x*1.32,.48,.13),(x*1.32,.78,.13),.025,'Dark',12)
        box('Bench timber arm',(x*1.32,.79,-.035),(.08,.045,.42),'Timber',.012)
        for y in [.69,.8,.92,1.03]:
            cyl('Bolt bench back',(x,y,-.32),(x,y,-.23),.012,'Steel',8)
    export('LakesideBench')


def shoreline():
    clear()
    points=[]; faces=[]; nx,nz=100,42
    for j in range(nz+1):
        t=j/nz
        for i in range(nx+1):
            x=-35+i*70/nx; shore=.85+.22*math.sin(x*.8)+.16*math.sin(x*2.1)
            z=-25*(1-t)+shore*t
            h=.03 + .012*math.sin(x*.6)*math.sin(z*.9) + .01*math.cos(z*.6)
            if z>-.2:h-=.11*((z+.2)/(shore+.2))**2
            if z< -2:h+=.015*math.sin(x*1.7+z*.8)
            points.append((x,h,z))
    for j in range(nz):
        for i in range(nx):
            k=j*(nx+1)+i;faces.append((k,k+nx+1,k+nx+2,k+1))
    mesh('Weathered shoreline',points,faces,'Shore')
    for i in range(115):
        x=RNG.uniform(-30,30); z=RNG.uniform(.35,1.1)+.16*math.sin(x*1.5);s=RNG.uniform(.07,.24)
        rock('Rock water rounded',(x,-.01,z),(s*1.5,s*.55,s))
    for side in [-1,1]:
        for i in range(56):
            x=side*RNG.uniform(4.2,24);z=RNG.uniform(.1,1.9);h=RNG.uniform(.45,1.2);lean=RNG.uniform(-.13,.13)
            cyl('Reed tapered stalk',(x,-.04,z),(x+lean,h,z),.008,'Reed',6)
            if i%3==0:cyl('Reed seed head',(x+lean,h*.78,z),(x+lean,h,z),.025,'Tip',8)
            for sign in [-1,1]:
                mesh('Reed blade',[(x,h*.35,z),(x+sign*.18,h*.65,z+.04),(x+sign*.23,h*.73,z+.03),(x+sign*.03,h*.43,z)],[(0,1,2,3)],'Reed')
    # Plank-by-plank jetty, weathered beams, pilings, mooring rings and rope rail.
    for i in range(27):box('Jetty weathered plank',(6,.16,.4+i*.34),(2.4,.13,.31),'Timber',.018)
    for x in [5.1,6.9]:box('Jetty support beam',(x,-.02,4.6),(.16,.19,9.7),'Dark')
    for z in [.6,3.8,7,9.4]:
        for x in [4.84,7.16]:
            cyl('Jetty timber pile',(x,-.6,z),(x,.76,z),.09,'Timber',14)
            ring('Ring mooring band',(x,.6,z),.097,.012,'Dark')
        if z<9:
            cyl('Cable jetty rope',(7.16,.64,z),(7.16,.55,z+1.6),.016,'Timber',8)
            cyl('Cable jetty rope',(7.16,.55,z+1.6),(7.16,.64,z+3.2),.016,'Timber',8)
    # Angular eroded landforms replace spherical horizon silhouettes.
    for ridge in range(3):
        pp=[];ff=[]
        for i in range(81):
            x=-125+i*3.2;top=4+ridge*2+3*math.sin(i*.18+ridge)+1.6*math.sin(i*.57)
            pp += [(x,-2,83+ridge*11),(x,top,86+ridge*11),(x,-2,91+ridge*11)]
        for i in range(80):
            k=i*3;ff.extend([(k,k+1,k+4,k+3),(k+1,k+2,k+5,k+4)])
        mesh('Distant sculpted headland',pp,ff,'Stone')
    export('LakesideShore')

textures(); laboratory(); conduit(); bench(); shoreline()
with open(os.path.join(SOURCE,'manifest.json'),'w') as f:
    json.dump({'generator':'Tools/blender/cinematics/build_sets.py','seed':70194,'units':'meters','assets':['ContainmentLaboratory','ServiceConduit','LakesideBench','LakesideShore'],'texture_resolution':1024,'licence':'Original PARASITE project artwork'},f,indent=2)
