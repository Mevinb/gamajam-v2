"""New standalone 14-second Northline finale source. Author/audit only: never render or edit runtime.

nice -n 15 blender -b -t 2 --python-exit-code 1 --python author.py -- build
nice -n 15 blender -b -t 2 --python-exit-code 1 --python author.py -- audit
"""
import argparse, hashlib, json, math, random, sys, time
from pathlib import Path
import bpy, bmesh
from mathutils import Matrix, Vector

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
OUT=ROOT/'ArtSource/RealisticFinales/Train';SOURCE=ROOT/'ArtSource/NorthlineCoach'
sys.path.insert(0,str(HERE));sys.path.insert(0,str(ROOT/'Tools/blender/characters'))
from geometry import Batch, empty, curve
import materials
import shell
from cinematic_surfaces import upgrade_character
FPS=24;END=336;ENDPOINT=337;RADIUS=.42;RAIL_X=.7535;RAIL_TOP=.240;WHEEL_Z=RAIL_TOP+RADIUS
VERSION=2
READONLY=[SOURCE/'NorthlineRail.blend',SOURCE/'piece-map.json',SOURCE/'production-manifest.json']
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()


def travel(frame):
    t=(frame-1)/FPS;u=max(0,t-3);v=8/9.5
    return .5*(v/3)*u*u if u<3 else 1.5*v+(u-3)*v


def configure(scene):
    scene.name='Northline realistic departure - eight physical metres'
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=192
    scene.cycles.use_adaptive_sampling=True;scene.cycles.adaptive_threshold=.008
    scene.cycles.use_denoising=True;scene.cycles.denoiser='OPENIMAGEDENOISE'
    scene.cycles.max_bounces=12;scene.cycles.transmission_bounces=10;scene.cycles.transparent_max_bounces=12
    scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
    scene.render.fps=FPS;scene.render.fps_base=1;scene.frame_start=1;scene.frame_end=END
    scene.render.use_motion_blur=True;scene.render.motion_blur_shutter=.5;scene.render.use_compositing=False
    scene.render.film_transparent=False;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB'
    scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=0
    scene.render.filepath=str(ROOT/'Logs/realistic-train-qa/frame-')
    scene['authoring_device']='CPU only, two threads; no render invoked';scene['render_target']='Cycles RTX/OptiX override owned by Root'
    scene['rendered']=False;scene['duration_seconds']=14.;scene['loop_period_frames']=0
    scene['loop_contract']='One-shot departure. Never wrap this train movement; no loop or ending flag is published by the source.'
    scene['runtime_contract']='Presentation source only. No game mission/possession/transit/ending code, flags or colliders changed.'
    scene['source_version']=VERSION


def body(rig):
    with bpy.data.libraries.load(str(SOURCE/'NorthlineRail.blend'),link=False) as (src,dst):
        assert 'RailDepartureCoach' in src.objects;dst.objects=['RailDepartureCoach']
    ob=dst.objects[0];bpy.context.scene.collection.objects.link(ob);ob.name='Northline coach - original body seats and real windows'
    rows=[r for r in json.loads((SOURCE/'piece-map.json').read_text()) if r['group']=='DepartureCoach']
    assert sum(r['source_vertices'] for r in rows)==len(ob.data.vertices)==5952
    retained=('unchanged deck','bright interior ceiling','actual laminated side glass','actual laminated end glass',
              'ceiling passenger handrail','ceiling rail mount','Northline coach seat','canopy underside stiffener')
    remove=set();offset=0;removed={}
    for row in rows:
        count=row['source_vertices']
        if not row['name'].startswith(retained):remove.update(range(offset,offset+count));removed[row['name']]=removed.get(row['name'],0)+1
        offset+=count
    bm=bmesh.new();bm.from_mesh(ob.data);bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[bm.verts[i] for i in sorted(remove)],context='VERTS');bm.to_mesh(ob.data);bm.free();ob.data.update()
    for p in ob.data.polygons:p.use_smooth=p.material_index!=1
    # The 3.2 x 14 m body and seats remain unchanged. Raising the existing body by .45 m
    # gives a .96 m floor above rail top and room for realistic .84 m diameter wheels.
    ob.parent=rig;ob.location.z=.45
    wn=ob.modifiers.new('Manufactured panel and bevel weighted normals','WEIGHTED_NORMAL');wn.keep_sharp=True;wn.weight=60
    opaque=ob.data.materials[0].copy();opaque.name='Original coach trim - dielectric enamel and real steel';ob.data.materials[0]=opaque
    nt=opaque.node_tree;bs=nt.nodes.get('Principled BSDF')
    uv=nt.nodes.new('ShaderNodeTexCoord');sep=nt.nodes.new('ShaderNodeSeparateXYZ');nt.links.new(uv.outputs['UV'],sep.inputs[0])
    painted=nt.nodes.new('ShaderNodeMath');painted.operation='LESS_THAN';painted.inputs[1].default_value=.25;nt.links.new(sep.outputs['Y'],painted.inputs[0])
    coat=nt.nodes.new('ShaderNodeMath');coat.operation='MULTIPLY';coat.inputs[1].default_value=.23;nt.links.new(painted.outputs[0],coat.inputs[0]);nt.links.new(coat.outputs[0],bs.inputs['Coat Weight']);bs.inputs['Coat Roughness'].default_value=.30
    glass=ob.data.materials[1].copy();glass.name='Coach closed 8 mm / 12 mm laminated dielectric glass';ob.data.materials[1]=glass
    gb=glass.node_tree.nodes.get('Principled BSDF');gb.inputs['Base Color'].default_value=(.99,.995,1,1);gb.inputs['Roughness'].default_value=.003;gb.inputs['IOR'].default_value=1.5;gb.inputs['Transmission Weight'].default_value=1.;gb.inputs['Metallic'].default_value=0
    volume=glass.node_tree.nodes.new('ShaderNodeVolumeAbsorption');volume.inputs['Color'].default_value=(.85,.96,.93,1);volume.inputs['Density'].default_value=.01
    glass.node_tree.links.new(volume.outputs[0],glass.node_tree.nodes.get('Material Output').inputs['Volume'])
    floor=materials.weathered('Coach nonslip rubber floor',(.15,.17,.16),.83)
    ceiling=materials.plain('Coach diffuse ivory ceiling',(.72,.73,.68),.74)
    ob.data.materials.append(floor);ob.data.materials.append(ceiling)
    for poly in ob.data.polygons:
        if poly.material_index==1:continue
        z=sum(ob.data.vertices[i].co.z for i in poly.vertices)/len(poly.vertices)
        if z<.77:poly.material_index=2
        elif z>3.04:poly.material_index=3
    return ob,removed


def wheel(batch,side,M):
    # Positive wheel flange is inboard of the 1.435 m inner rail-head gauge.
    profile=[(-.080,.12),(-.080,.408),(-.067,.444),(-.047,.449),(-.036,.421),(.060,.419),(.072,.400),(.072,.12)]
    sides=80;bm=bmesh.new();vv=[]
    for axial,r in profile:
        for i in range(sides):
            a=math.tau*i/sides;vv.append(bm.verts.new((side*(RAIL_X+axial),r*math.cos(a),r*math.sin(a))))
    for j in range(len(profile)):
        for i in range(sides):bm.faces.new((vv[j*sides+i],vv[j*sides+(i+1)%sides],vv[((j+1)%len(profile))*sides+(i+1)%sides],vv[((j+1)%len(profile))*sides+i]))
    batch.append(bm,M['steel'],smooth=True)
    batch.tube((side*.65,0,0),(side*.86,0,0),.13,M['dark'],40)
    batch.tube((side*.515,0,0),(side*.55,0,0),.285,M['steel'],64)
    for n in range(12):
        a=n*math.tau/12;batch.tube((side*.55,.22*math.cos(a),.22*math.sin(a)),(side*.56,.22*math.cos(a),.22*math.sin(a)),.012,M['dark'],6)


def running_gear(rig,M):
    bogies=Batch('Standard gauge bogies - brakes springs air bags and transmission');axles=[]
    for x in (-1.25,1.25):bogies.box((x,0,1.075),(.16,13.8,.17),M['dark'],.012,3)
    for y in (-6.6,-2.5,0,2.5,6.6):bogies.box((0,y,1.055),(2.60,.15,.13),M['dark'],.01)
    for centre in (-4.5,4.5):
        for side in (-1,1):
            x=side*.94
            bogies.box((x,centre,.74),(.18,2.7,.21),M['dark'],.035,3)
            for y in (centre-.83,centre+.83):
                bogies.box((side*.96,y,.66),(.23,.30,.24),M['dark'],.022,3)
                bogies.spring((side*.95,y,.88),.070,.24,.014,M['dark'])
                bogies.tube((side*.53,y+.19,.78),(side*.53,y-.03,.70),.06,M['dark'],16)
                bogies.box((side*.56,y+.23,.76),(.15,.16,.26),M['dark'],.012)
                bogies.tube((side*.98,y,.66),(side*1.09,y,.66),.086,M['steel'],24)
            # Secondary air suspension is visibly between the cast frame and floor bolster.
            bogies.tube((side*.67,centre,.86),(side*.67,centre,1.035),.15,M['rubber'],32)
            bogies.box((side*.67,centre,1.04),(.38,.46,.045),M['dark'],.012)
        bogies.box((0,centre,.84),(1.92,.28,.20),M['dark'],.025,3)
        for y in (centre-.9,centre+.9):
            a=Batch('Rotating wheelset '+str(y));wheel(a,-1,M);wheel(a,1,M)
            a.tube((-.93,0,0),(.93,0,0),.070,M['steel'],32)
            a.tube((-.22,0,0),(.22,0,0),.19,M['dark'],32)
            axle=a.finish(rig);axle.location=(0,y,WHEEL_Z);axle.rotation_mode='XYZ'
            axle['rolling_radius_metres']=RADIUS;axle['wheel_tread_centres_x']=json.dumps([-RAIL_X,RAIL_X])
            fc=axle.driver_add('rotation_euler',0);driver=fc.driver;driver.type='SCRIPTED'
            v=driver.variables.new();v.name='travel';v.type='TRANSFORMS';v.targets[0].id=rig;v.targets[0].transform_type='LOC_Y';v.targets[0].transform_space='LOCAL_SPACE'
            driver.expression='-travel / 0.42';axles.append(axle)
    # Visible auxiliary/motor equipment prevents a hollow toy underframe.
    bogies.box((0,0,.89),(1.10,2.8,.35),M['dark'],.035,3)
    for y in (-1.10,-.88,-.66,-.44,-.22,0,.22,.44,.66,.88,1.10):bogies.box((-.57,y,.89),(.035,.07,.27),M['steel'],.003)
    for y in (-6.99,6.99):
        for x in (-.62,.62):
            bogies.tube((x,y,.91),(x,y+(1 if y>0 else -1)*.26,.91),.080,M['dark'],24)
            bogies.tube((x,y+.22*(1 if y>0 else -1),.91),(x,y+.27*(1 if y>0 else -1),.91),.16,M['dark'],32)
    bogies.finish(rig)
    return axles


def pantograph(rig,M):
    g=Batch('Roof-mounted diamond pantograph and real contact shoe')
    g.box((0,-1.8,3.91),(1.10,1.50,.12),M['dark'],.02)
    for x in (-.46,.46):
        for y in (-2.25,-1.35):
            g.tube((x,y,3.96),(x,y,4.16),.07,M['ceramic'],24)
        g.tube((x,-2.30,4.16),(x,-1.30,4.77),.025,M['dark'],12)
        g.tube((x,-1.30,4.77),(x,-2.05,5.37),.021,M['dark'],12)
    g.box((0,-2.05,5.39),(1.40,.16,.045),M['dark'],.01)
    g.tube((-.70,-2.05,5.425),(.70,-2.05,5.425),.014,M['copper'],16)
    g.finish(rig)


def gel(rig,name,p,phase):
    mat=materials.plain('Free gel '+name,(.60,.78,.65),.06)
    nt=mat.node_tree;bs=nt.nodes.get('Principled BSDF');bs.inputs['Transmission Weight'].default_value=.65;bs.inputs['IOR'].default_value=1.37
    bs.inputs['Subsurface Weight'].default_value=.10;bs.inputs['Subsurface Scale'].default_value=.003
    vol=nt.nodes.new('ShaderNodeVolumeAbsorption');vol.inputs['Color'].default_value=(.35,.63,.42,1);vol.inputs['Density'].default_value=.22;nt.links.new(vol.outputs[0],nt.nodes.get('Material Output').inputs['Volume'])
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64,ring_count=32,radius=1)
    ob=bpy.context.object;ob.name='FREE BODY '+name;ob.parent=rig;ob.location=p
    for v in ob.data.vertices:
        q=v.co.copy();v.co=(q.x*.235*(1+.06*math.sin(q.y*9)),q.y*.21,q.z*.15)
    for f in ob.data.polygons:f.use_smooth=True
    ob.data.materials.append(mat);ob['passenger_kind']='Free amoeba body. No captive host or human is attached.'
    for f in range(1,ENDPOINT+1,6):
        v=.012*math.sin(math.tau*((f-1)/144+phase));ob.scale=(1+v,1-v*.5,1-v*.5);ob.keyframe_insert('scale',frame=f)
    internal=Batch(name+' internal suspended organelles')
    nucleus=materials.plain(name+' translucent nucleus',(.16,.32,.13),.28)
    for i in range(7):
        a=i*2.399+phase*6;internal.sphere((math.cos(a)*.10,math.sin(a)*.075,.015*math.sin(i)),(.021,.017,.014),nucleus,2)
    inn=internal.finish(ob);inn.location=(0,0,0)
    return ob


RAIL_PROFILE=[(-.075,0),(.075,0),(.075,.017),(.011,.034),(.009,.115),(.036,.135),(.036,.172),(-.036,.172),(-.036,.135),(-.009,.115),(-.011,.034),(-.075,.017)]


def track_segment(M):
    # One real 12 m section is reused by all three tracks and separately exported for future visual use.
    g=Batch('DepotTrackSegment12m');start=-5.7
    for j in range(20):
        y=start+j*.60
        g.box((0,y,-.025),(2.50,.245,.17),M['concrete'],.012,2)
        for side in (-1,1):
            x=side*RAIL_X;g.box((x,y,.064),(.19,.17,.008),M['rubber'],.002,1)
            for offset in (-.11,.11):
                g.box((x+offset,y,.088),(.055,.13,.045),M['rust'],.008,2)
                g.tube((x+offset,y,.11),(x+offset,y,.126),.021,M['dark'],6)
    for side in (-1,1):
        g.extrude(RAIL_PROFILE,12,(side*RAIL_X,0,.068),M['rust'])
        # The separate polished head is only 0.35 mm above the profiled rail's top face.
        g.box((side*RAIL_X,0,RAIL_TOP+.00018),(.068,12,.00035),M['steel'])
        for y in (-5.96,5.96):
            for dx in (-.014,.014):
                g.box((side*RAIL_X+dx,y,.150),(.014,.50,.065),M['rust'],.003)
    ob=g.finish();ob['rail_gauge_inner_faces_metres']=1.435;ob['sleeper_spacing_metres']=.60;ob['rail_head_centres_metres']=2*RAIL_X
    return ob


def tracks(M):
    proto=track_segment(M);exports=OUT/'Exports';exports.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT');proto.select_set(True);bpy.context.view_layer.objects.active=proto
    fbx=exports/'DepotTrackSegment12m.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,
        bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,path_mode='STRIP')
    proto.select_set(False)
    for track_x in (0,6.3,12.6):
        for centre_y in range(-42,139,12):
            ob=proto if track_x==0 and centre_y==-42 else bpy.data.objects.new('Main line section' if track_x==0 else 'Depot siding section',proto.data)
            if ob!=proto:bpy.context.scene.collection.objects.link(ob)
            ob.location=(track_x,centre_y,0)
    banks=Batch('Raised granular ballast banks with sloped shoulders')
    section=[(-2.05,-.17),(2.05,-.17),(1.65,-.025),(-1.65,-.025)]
    for x in (0,6.3,12.6):banks.extrude(section,192,(x,48,0),M['ballast'])
    banks.finish()
    # Actual angular stones concentrated in the bogie/camera foreground. Far scans retain fine coverage.
    rng=random.Random(70512);stones=Batch('Individual fractured granite ballast - close and middle distance')
    for i in range(25500):
        x=rng.uniform(-1.85,1.85);y=rng.uniform(-18,60)
        if i<10500:x=rng.uniform(-1.75,-.15);y=rng.uniform(-7,12)
        if i>21000:x+=6.3 if i%2 else 12.6
        z=-.02+(.008*rng.random());sx=rng.uniform(.018,.044);sy=rng.uniform(.020,.048);sz=rng.uniform(.015,.031)
        bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=1,radius=1)
        for v in bm.verts:v.co=Vector((v.co.x*sx*rng.uniform(.88,1.1),v.co.y*sy,v.co.z*sz))
        stones.append(bm,M['stone'+str(i%4)],(x,y,z),Matrix.Rotation(rng.random()*math.tau,3,'Z'))
    stones.finish()
    return {'path':str(fbx.relative_to(ROOT)),'sha256':sha(fbx),'bytes':fbx.stat().st_size,'gauge_metres':1.435}


def depot(M):
    site=Batch('Platform dressed coping tactile edge and service yard')
    site.box((-4.35,8,.49),(5.15,58,1.38),M['concrete'],.018,3)
    site.box((0,40,-.25),(130,300,.15),M['asphalt'])
    for y in range(-20,38):
        site.box((-1.94,y+.5,1.165),(.32,.997,.06),M['concrete'],.009,2)
        site.box((-2.28,y+.5,1.186),(.35,.997,.012),M['yellow'],.002,1)
        # Real tactile domes, not a flat warning stripe.
        for i in range(4):
            for j in range(11):site.tube((-2.405+i*.083,y+.06+j*.084,1.192),(-2.405+i*.083,y+.06+j*.084,1.198),.012,M['yellow'],8)
    for y in (-10,8,26):
        site.box((-5.5,y,1.53),(.52,2.3,.12),M['wood'],.015)
        for dy in (-.85,.85):site.box((-5.5,y+dy,1.37),(.48,.10,.40),M['dark'],.006)
    site.finish()
    # A recessed masonry depot with repeated sawtooth roofs, clerestory glass and manufacturing detail.
    arch=Batch('Masonry workshop with sawtooth roofs and glazed north lights')
    for lo,hi in [(-22,-8),(0,12),(20,36),(44,58),(66,88)]:
        arch.box((-13, (lo+hi)/2,3.5),(.38,hi-lo,7),M['brick'],.012)
    for y in (-22,88):arch.box((-22.5,y,3.5),(19.4,.4,7),M['brick'],.012)
    arch.box((-32,33,3.5),(.40,110,7),M['brick'],.012)
    for y in range(-22,89,8):
        arch.box((-12.70,y,3.55),(.48,.48,7.1),M['concrete'],.012)
        arch.box((-12.65,y,1.04),(.52,.56,.18),M['concrete'],.007)
    for centre in (-4,16,40,62):
        # Openings retain visible depth: roller leaf is recessed behind brick jambs.
        arch.box((-13.22,centre,3.25 if centre==16 else 2.60),(.10,7.8,3.5 if centre==16 else 4.95),M['roof'],.01)
        if centre==16:
            arch.box((-19.0,centre,2.55),(.15,7.8,5.0),M['dark'])
            arch.box((-16.0,centre,1.13),(6.0,7.8,.10),M['concrete'])
        for z in range(1,27):
            if centre!=16 or .20+z*.18>1.5:arch.box((-13.13,centre,.20+z*.18),(.033,7.70,.025),M['dark'],.003)
        arch.box((-12.67,centre,5.18),(.30,8.4,.28),M['concrete'],.008)
        for y in (centre-3.9,centre+3.9):arch.box((-12.70,y,2.65),(.30,.20,5.1),M['concrete'],.006)
    for y in range(-18,84,12):
        # Roof has a shallow pitch and a vertical, framed glass clerestory.
        rise=1.65;span=9.2;angle=math.atan2(rise,span)
        for x in (-17.9,-27.2):
            arch.box((x,y+4,7.0+rise*.5),(span,11.9,.09),M['roof'],.008,2,Matrix.Rotation(angle,3,'Y'))
            for yy in [y-1.8+i*.42 for i in range(29)]:
                arch.box((x,yy,7.0+rise*.5+.045),(span,.045,.035),M['roof'],.003,1,Matrix.Rotation(angle,3,'Y'))
            arch.box((x+span*.5,y+4,7.64),(.012,11.4,1.30),M['glass'],.002)
            for yy in range(y-2,y+11,2):arch.box((x+span*.5,yy,7.64),(.05,.04,1.4),M['paint'],.003)
    # Platform canopy has steel columns, actual I beams, gutter and down pipes.
    for y in range(-17,37,9):
        for x in (-6.2,-2.9):
            arch.box((x,y,3.1),(.12,.16,3.75),M['paint'],.006)
            arch.box((x,y,1.31),(.30,.34,.09),M['dark'],.006)
        arch.box((-4.55,y,4.91),(4.6,.12,.20),M['paint'],.006)
    arch.box((-4.55,8,5.04),(5.35,58,.10),M['roof'],.008)
    for x in (-7.18,-1.91):arch.box((x,8,5.10),(.10,58,.14),M['paint'],.008)
    arch.box((-4.7,8,4.96),(.12,58,.18),M['paint'],.006)
    arch.finish()
    fittings=Batch('Depot service details pipes signage ducts pallets and cabinets')
    for y in (-16,2,20,34):
        fittings.tube((-12.48,y,.1),(-12.48,y,7.4),.06,M['roof'],16)
        fittings.box((-10.8,y,1.85),(1.2,.6,1.3),M['paint'],.018,3)
        for z in (1.45,1.57,1.69,1.81):fittings.box((-10.18,y,z),(.018,.43,.035),M['dark'],.003)
        for i in range(5):fittings.box((-9.6,y+i*.20,1.40),(.7,.08,.035),M['wood'],.003)
        for x in (-9.8,-9.4):fittings.box((x,y+.4,1.30),(.10,.95,.12),M['wood'],.005)
    for x in (-6,-3):
        for y in (-14,3,20,34):
            fittings.box((x,y,4.83),(.24,1.2,.07),M['lamp'],.008)
            light=bpy.data.lights.new('Platform practical 40 W','AREA');light.energy=40;light.shape='RECTANGLE';light.size=.2;light.size_y=1.1;light.color=(1,.78,.52)
            ob=bpy.data.objects.new(light.name,light);bpy.context.scene.collection.objects.link(ob);ob.location=(x,y,4.78)
    fittings.finish()
    # Distant buildings/chimneys/stacked materials give an actual horizon behind the track.
    distance=Batch('Industrial middle distance - multiple scales and silhouettes')
    for x,y,w,d,h in [(32,5,17,34,9),(42,58,24,34,12),(20,115,25,28,10),(-36,122,36,34,13),(-53,45,12,28,8)]:
        distance.box((x,y,h/2),(w,d,h),M['brick'],.025)
        distance.box((x,y,h+.14),(w+.3,d+.3,.28),M['roof'],.02)
        for yy in range(int(y-d/2)+2,int(y+d/2)-1,4):
            distance.box((x-w/2-.02,yy,h*.64),(.015,1.8,1.25),M['glass'],.002)
            distance.box((x-w/2-.05,yy,h*.64),(.04,.045,1.32),M['paint'],.002)
        for xx in (-w*.3,w*.3):distance.tube((x+xx,y,h),(x+xx,y,h+1),.22,M['dark'],16)
    for x,y,h in [(35,83,26),(-48,108,23)]:
        distance.tube((x,y,0),(x,y,h),.75,M['brick'],24);distance.tube((x,y,h-.3),(x,y,h+.15),.85,M['dark'],24)
    for i in range(13):
        x=24+(i%4)*2.9;y=50+(i//4)*6
        distance.box((x,y,.65),(2.3,4.8,1.3),M['roof'],.035)
        for yy in (-1.4,1.4):distance.box((x,y+yy,1.32),(2.35,.10,.04),M['dark'],.003)
    distance.finish()
    for label,p,rot,size in [('NORTHLINE  /  DEPOT 07',(-12.42,7.8,5.8),(math.pi/2,0,-math.pi/2),.34),
                             ('DEPARTURE',(-4.6,-11.9,4.20),(math.pi/2,0,0),.25)]:
        cu=bpy.data.curves.new(label,'FONT');cu.body=label;cu.size=size;cu.extrude=.002;cu.align_x='CENTER'
        ob=bpy.data.objects.new(label,cu);bpy.context.scene.collection.objects.link(ob);ob.location=p;ob.rotation_euler=rot;cu.materials.append(M['concrete'])


def catenary(M):
    hardware=Batch('Catenary portals insulators droppers and cable anchors')
    for y in (-25,5,35,65,95,125):
        for x in (-2.8,16.8):
            hardware.box((x,y,3.1),(.19,.23,6.2),M['paint'],.008)
            hardware.box((x,y,.18),(.70,.65,.36),M['concrete'],.012)
            hardware.box((x,y,6.12),(.28,.32,.08),M['dark'],.006)
        hardware.box((7,y,6.18),(19.8,.13,.22),M['paint'],.006)
        for x in (0,6.3,12.6):
            hardware.tube((x,y,5.95),(x,y,6.15),.045,M['ceramic'],24)
            for z in (5.97,6.02,6.07,6.12):hardware.tube((x,y,z),(x,y,z+.025),.095,M['ceramic'],24)
            hardware.tube((x-.45,y,5.94),(x,y,5.43),.013,M['copper'],12)
    hardware.finish()
    for x in (0,6.3,12.6):
        curve('Continuous contact wire',[(x,y,5.43) for y in range(-50,150,5)],.007,M['copper'])
        for y0 in range(-25,126,30):
            points=[(x,y0+u*30,5.94-.23*math.sin(math.pi*u)) for u in [i/20 for i in range(21)]]
            curve('Catenary messenger sag',points,.009,M['copper'])
            for dy in (6,12,18,24):
                z=5.94-.23*math.sin(math.pi*dy/30);curve('Actual messenger dropper',[(x,y0+dy,5.43),(x,y0+dy,z)],.003,M['copper'])


def workers():
    sys.path.insert(0,str(ROOT/'Tools/blender/cinematics/full_story/motion'))
    from library import MotionLibrary
    library=MotionLibrary(ROOT);result=[]
    for cid,p,phase in [('MaintEli',(-3.4,-5,1.18),0),('GuardMarcus',(-5.0,20,1.18),.7)]:
        source=Path.home()/'Data/parasite-work/characters/out'/(cid+'.blend')
        with bpy.data.libraries.load(str(source),link=False) as (src,dst):
            assert 'Armature' in src.objects and cid+'_LOD0' in src.objects;dst.objects=['Armature',cid+'_LOD0']
        for ob in dst.objects:bpy.context.scene.collection.objects.link(ob)
        rig=next(ob for ob in dst.objects if ob.type=='ARMATURE');mesh=next(ob for ob in dst.objects if ob.type=='MESH')
        rig.name='Platform worker '+cid;mesh.name='Platform worker mesh '+cid;rig.animation_data_clear()
        upgrade_character(mesh,cid,ROOT)
        for frame in list(range(1,ENDPOINT+1,8))+[ENDPOINT]:
            library.apply_pose(rig,'Idle_Shift' if cid=='MaintEli' else 'Idle_GuardBelt',(frame-1)/FPS+phase,root_motion=False)
            for bone in rig.pose.bones:
                bone.keyframe_insert('rotation_quaternion',frame=frame);bone.keyframe_insert('location',frame=frame)
        rig.location=p;rig.rotation_euler.z=math.pi/2
        bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
        ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());data=ev.to_mesh();low=min((ev.matrix_world@v.co).z for v in data.vertices);ev.to_mesh_clear();rig.location.z+=p[2]-low
        rig['role']='Background platform worker, never a passenger aboard';result.append({'cid':cid,'source':str(source),'source_sha256':sha(source),'grounded_z':p[2]})
    # No unneeded source rig/action library goes into this new compact film.
    source_arm=library.source_armature;source_arm.use_fake_user=False
    bpy.data.objects.remove(source_arm,do_unlink=True)
    return result


def lighting():
    world=bpy.data.worlds.new('Belfast photographed dusk - sole sun direction');world.use_nodes=True;nt=world.node_tree;nt.nodes.clear()
    out=nt.nodes.new('ShaderNodeOutputWorld');bg=nt.nodes.new('ShaderNodeBackground');bg.inputs['Strength'].default_value=.75
    env=nt.nodes.new('ShaderNodeTexEnvironment');path=ROOT/'Assets/Parasite/Art/City/sky/belfast_sunset_puresky_4k.hdr';env.image=bpy.data.images.load(str(path),check_existing=True)
    nt.links.new(env.outputs['Color'],bg.inputs['Color']);nt.links.new(bg.outputs[0],out.inputs['Surface']);bpy.context.scene.world=world
    # No independent rotating sun: its direction, colour, reflections and shadows all come from one HDRI.
    world['sun_coherence']='Photographed HDRI is the only outdoor illuminant; practical lamps are local 40 W fixtures.'
    return {'path':str(path.relative_to(ROOT)),'sha256':sha(path),'strength':.75,'independent_sun_lights':0}


def animation(rig):
    for frame in range(1,ENDPOINT+1):
        rig.location.y=travel(frame);rig.keyframe_insert('location',index=1,frame=frame)
    for fc in rig.animation_data.action.fcurves:
        for key in fc.keyframe_points:key.interpolation='LINEAR'
    rig['endpoint_travel_metres']=8.;rig['motion_direction']='Blender +Y';rig['rolling_relation']='axle rotation X = -train travel Y / .42'


def cameras(scene,rig):
    shots=[(1,84,'01 - 28 mm industrial departure establishing',28,5.6,(12,-17,4.1),(10,-14,3.6),(0,-1,2),(0,0,2),False),
           (85,144,'02 - 50 mm running gear and real ballast',50,5.6,(2.15,-5.8,.57),(2.0,-4.8,.60),(.74,-4.5,.66),(.74,-3.9,.67),True),
           (145,216,'03 - 50 mm two free bodies behind real glass',50,8.0,(-2.8,4.4,3.65),(-2.65,4.0,3.60),(-.10,3.55,1.44),(-.10,3.55,1.44),True),
           (217,END,'04 - 38 mm depart through the layered depot',38,8.0,(12,-17,5.5),(14,-23,6.5),(0,6,2.0),(0,13,2.0),False)]
    records=[]
    for first,last,name,lens,fstop,a,b,ta,tb,follow in shots:
        data=bpy.data.cameras.new(name);data.lens=lens;data.sensor_width=36;data.clip_start=.04;data.clip_end=600;data.dof.use_dof=True;data.dof.aperture_fstop=fstop
        ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob)
        focus=empty(name+' focus')
        if follow:ob.parent=rig;focus.parent=rig
        data.dof.focus_object=focus
        for frame,p,t in ((first,a,ta),(last,b,tb)):
            ob.location=p;ob.rotation_euler=(Vector(t)-Vector(p)).to_track_quat('-Z','Y').to_euler();focus.location=t
            ob.keyframe_insert('location',frame=frame);ob.keyframe_insert('rotation_euler',frame=frame);focus.keyframe_insert('location',frame=frame)
        marker=scene.timeline_markers.new(name,frame=first);marker.camera=ob
        records.append({'first':first,'last':last,'camera':name,'lens_mm':lens,'fstop':fstop,'follows_actual_train':follow})
    scene.camera=bpy.data.objects[shots[0][2]]
    return records


def build():
    OUT.mkdir(parents=True,exist_ok=True);before={str(p.relative_to(ROOT)):sha(p) for p in READONLY}
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene;configure(scene);M=materials.make(ROOT)
    rig=empty('ACTUAL TRAIN RIG - visual only');coach,removed=body(rig)
    axles=running_gear(rig,M);shell.add(rig,M);shell.interior(rig,M);pantograph(rig,M)
    gel(rig,'A',(-.32,3.10,1.352),0);gel(rig,'B',(.32,3.90,1.352),.37)
    print('[realistic-train] coach and standard gauge bogies authored',flush=True)
    export=tracks(M);depot(M);catenary(M)
    print('[realistic-train] depot track ballast architecture and catenary authored',flush=True)
    characters=workers();environment=lighting();animation(rig);shots=cameras(scene,rig)
    scene['rail_gauge_metres']=1.435;scene['wheel_radius_metres']=RADIUS;scene['rail_top_metres']=RAIL_TOP;scene['coach_deck_top_metres']=1.20
    scene['old_running_gear_removed']=json.dumps(removed);scene['passenger_count']=2;scene['captive_humans_aboard']=0
    scene['gpu_qa_frames']=json.dumps([1,60,100,144,168,204,256,336])
    scene.frame_set(1)
    for _ in range(2):bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
    bpy.ops.file.pack_all();path=OUT/('Northline_RealisticDeparture_v'+str(VERSION)+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    assert path.stat().st_size<200*1024*1024,'New packed source exceeds 200 MiB'
    assert before=={str(p.relative_to(ROOT)):sha(p) for p in READONLY},'Original coach source changed'
    receipt={'schema':1,'version':VERSION,'status':'AUTHORED_SOURCE_ONLY_UNRENDERED','path':str(path.relative_to(ROOT)),'sha256':sha(path),'bytes':path.stat().st_size,
        'fps':FPS,'frames':[1,END],'duration_seconds':14,'endpoint_frame':ENDPOINT,'travel_metres':8,'rail_gauge_metres':1.435,
        'wheel_radius_metres':RADIUS,'wheel_tread_centres_x':[-RAIL_X,RAIL_X],'rail_top_metres':RAIL_TOP,'wheel_centres_z':WHEEL_Z,
        'body_source_unchanged_read_only':before,'removed_old_running_gear':removed,'body_deck_raised_metres':.45,
        'body_size_preserved_metres':[3.204,14.019],'coach_floor_above_rail_top_metres':.960,
        'free_gel_passengers':2,'captive_humans_aboard':0,'background_platform_workers':characters,'environment':environment,
        'shots':shots,'gpu_qa_frames':[1,60,100,144,168,204,256,336],'samples':192,'adaptive_threshold':.008,'motion_blur_shutter':.5,
        'loop':'One-shot only; no wrap. Gel/idle secondary motion does not grant completion.','reusable_track_export':export,
        'rendered':False,'runtime_integration':'None. Protected runtime physical departure and flags are unchanged.'}
    (OUT/('train-authoring-v'+str(VERSION)+'.json')).write_text(json.dumps(receipt,indent=2)+'\n')
    print('[realistic-train] AUTHORED packed source '+str(path.stat().st_size)+' B, no render or runtime integration',flush=True)


def audit():
    path=OUT/('Northline_RealisticDeparture_v'+str(VERSION)+'.blend');receipt=json.loads((OUT/('train-authoring-v'+str(VERSION)+'.json')).read_text());assert sha(path)==receipt['sha256']
    bpy.ops.wm.open_mainfile(filepath=str(path),load_ui=False,use_scripts=False);scene=bpy.context.scene
    assert (scene.frame_start,scene.frame_end,scene.render.fps)==(1,END,FPS)
    assert scene.cycles.samples==192 and abs(scene.cycles.adaptive_threshold-.008)<1e-7
    assert scene.render.use_motion_blur and scene.render.motion_blur_shutter==.5 and scene.view_settings.view_transform=='AgX'
    assert len([o for o in scene.objects if o.name.startswith('FREE BODY ')])==2
    rig=bpy.data.objects['ACTUAL TRAIN RIG - visual only'];axles=[o for o in scene.objects if o.name.startswith('Rotating wheelset')]
    assert len(axles)==4 and all(o.parent==rig for o in axles)
    unpacks=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not unpacks,unpacks
    checks=[];last=-1
    for frame in range(1,ENDPOINT+1):
        scene.frame_set(frame);bpy.context.view_layer.update();distance=rig.location.y
        assert distance>=last-1e-6 and abs(distance-travel(frame))<1e-5;last=distance
        for axle in axles:assert abs(axle.rotation_euler.x+distance/RADIUS)<1e-4,(frame,axle.name)
        if frame in receipt['gpu_qa_frames']+[ENDPOINT]:checks.append({'frame':frame,'train_y':distance,'wheel_rotation_x':axles[0].rotation_euler.x,'camera':scene.camera.name})
    assert abs(last-8)<1e-5
    # Prove the window camera sees both independent gel bodies through actual glass,
    # rather than reporting their existence behind an opaque side panel or seat back.
    from bpy_extras.object_utils import world_to_camera_view
    routes=[]
    for frame in (145,168,192,216):
        scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();camera=scene.camera
        for target in [o for o in scene.objects if o.name.startswith('FREE BODY ')]:
            destination=target.matrix_world.translation;origin=camera.matrix_world.translation.copy();direction=(destination-origin).normalized();passed=[];reached=False
            projected=world_to_camera_view(scene,camera,destination)
            assert .025<projected.x<.975 and .025<projected.y<.975 and projected.z>0,(frame,target.name,'out of camera',list(projected))
            for step in range(16):
                distance=(destination-origin).length
                hit,point,normal,face,ob,matrix=scene.ray_cast(deps,origin,direction,distance=distance+.01)
                if not hit:break
                if ob.original==target:reached=True;break
                material=ob.data.materials[ob.data.polygons[face].material_index] if ob.type=='MESH' and face<len(ob.data.polygons) else None
                principled=material.node_tree.nodes.get('Principled BSDF') if material and material.use_nodes else None
                transparent=principled and principled.inputs['Transmission Weight'].default_value>.9
                assert transparent,(frame,target.name,'opaque occluder',ob.name,face)
                passed.append(material.name);origin=point+direction*.0001
            assert reached and any('glass' in n.lower() for n in passed),(frame,target.name,'glass route',passed)
            routes.append({'frame':frame,'target':target.name,'screen':[projected.x,projected.y],'transparent_surfaces':passed,'reached':reached})
    # Rail head width .072 m; centres at +/-.7535 -> inner faces +/-.7175.
    assert abs(2*(RAIL_X-.036)-1.435)<1e-9
    source={name:sha(ROOT/name) for name in receipt['body_source_unchanged_read_only']};assert source==receipt['body_source_unchanged_read_only']
    scene.frame_set(1)
    count=sum(len(o.data.polygons) for o in scene.objects if o.type=='MESH')
    lamps=[ob for ob in scene.objects if ob.type=='LIGHT' and ob.name.startswith('Coach ceiling 18 W')]
    assert len(lamps)==4 and all(ob.parent==rig and ob.data.type=='AREA' and ob.data.energy==18 for ob in lamps)
    glass=bpy.data.materials['Coach closed 8 mm / 12 mm laminated dielectric glass'];gb=glass.node_tree.nodes.get('Principled BSDF')
    assert gb.inputs['Roughness'].default_value<.004 and gb.inputs['Transmission Weight'].default_value==1
    camera=bpy.data.objects['03 - 50 mm two free bodies behind real glass'];assert camera.data.dof.aperture_fstop==8
    scene.frame_set(168);focus=camera.data.dof.focus_object;focus_distance=(focus.matrix_world.translation-camera.matrix_world.translation).length
    result={'passed':True,'version':VERSION,'scope':'CPU Blender source audit only; no rendered appearance, encoding, Unity/native acceptance or performance claim',
        'packed_source_sha256':sha(path),'packed_source_bytes':path.stat().st_size,'frame_count':END,'duration_seconds':14,'samples':192,
        'rail_gauge_inner_faces_metres':1.435,'rail_top_metres':RAIL_TOP,'wheel_tread_radius_metres':RADIUS,'framewise_rolling_checks':ENDPOINT*len(axles),
        'free_gel_bodies':2,'captive_humans_aboard':0,'unpacked_images':unpacks,'scene_object_count':len(scene.objects),
        'mesh_polygons_with_instances':count,'qa_frame_transforms':checks,'window_camera_ray_checks':routes,
        'interior_strip_lights':len(lamps),'interior_radiant_watts':sum(ob.data.energy for ob in lamps),
        'window_glass_roughness':gb.inputs['Roughness'].default_value,'window_lens_mm':camera.data.lens,'window_fstop':camera.data.dof.aperture_fstop,
        'window_focus_distance_frame168_metres':focus_distance,'brick_course_metres':.075,
        'old_source_hashes_preserved':True,'runtime_modified':False}
    (OUT/('train-source-audit-v'+str(VERSION)+'.json')).write_text(json.dumps(result,indent=2)+'\n');print('[realistic-train] AUDIT PASS '+str(path.stat().st_size)+' B, no render',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['build','audit'])
    opts=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    build() if opts.mode=='build' else audit()
