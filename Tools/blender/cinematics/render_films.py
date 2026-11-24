"""Build and render real Cycles/OptiX establishing films. GPU-only; fails if RTX OptiX is absent.
blender -b --python render_films.py -- <project> <opening|ending> <probe|render>
"""
import bpy, math, sys, os, json
bpy.context.preferences.filepaths.save_version = 0
from mathutils import Vector, Matrix
P, FILM, MODE = sys.argv[sys.argv.index('--')+1:]
OUT=os.path.join(P,'Logs','cinematic-films',FILM);os.makedirs(OUT,exist_ok=True)
SOURCE=os.path.join(P,'ArtSource','Cinematics')
RESOURCE=os.path.join(P,'Assets','Parasite','Resources','Cinematics')

def v(p):return Vector((p[0],-p[2],p[1]))
def append(path):
    with bpy.data.libraries.load(path,link=False) as (a,b):b.objects=a.objects
    result=[]
    for o in b.objects:
        if o:bpy.context.scene.collection.objects.link(o);result.append(o)
    return result

def material(name,color,rough=.4,metal=0,emit=0,transmission=0):
    m=bpy.data.materials.new(name);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=rough;b.inputs['Metallic'].default_value=metal
    b.inputs['Emission Color'].default_value=(*color,1);b.inputs['Emission Strength'].default_value=emit;b.inputs['Transmission Weight'].default_value=transmission
    return m

def sphere(name,p,size,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64,ring_count=32,radius=1,location=v(p));o=bpy.context.object;o.name=name;o.scale=size;o.data.materials.append(mat)
    for poly in o.data.polygons:poly.use_smooth=True
    return o

def light(name,p,target,energy,color,size=3):
    d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=v(p);o.rotation_euler=(v(target)-o.location).to_track_quat('-Z','Y').to_euler()
    return o

def camera(start,end,target,lens=44,focus=2):
    d=bpy.data.cameras.new('Authored dolly camera');d.lens=lens;d.sensor_width=36;d.dof.use_dof=True;d.dof.aperture_fstop=5.6;d.dof.focus_distance=focus
    o=bpy.data.objects.new('Authored dolly camera',d);bpy.context.collection.objects.link(o);bpy.context.scene.camera=o
    for frame,p in [(1,start),(72,end)]:
        o.location=v(p);o.rotation_euler=(v(target)-o.location).to_track_quat('-Z','Y').to_euler();o.keyframe_insert('location',frame=frame);o.keyframe_insert('rotation_euler',frame=frame)
    return o

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='GPU';sc.cycles.samples=32;sc.cycles.use_denoising=True;sc.cycles.use_adaptive_sampling=True;sc.cycles.adaptive_threshold=.04
sc.cycles.max_bounces=6;sc.cycles.transparent_max_bounces=6;sc.cycles.transmission_bounces=4
prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
rtx=[d for d in prefs.devices if d.type=='OPTIX' and '4050' in d.name]
if not rtx:raise RuntimeError('RTX4050 OptiX device required; refusing CPU fallback')
for d in prefs.devices:d.use=d in rtx
print('[film] DEVICE',[(d.name,d.type,d.use) for d in prefs.devices],flush=True)
sc.render.resolution_x=1280;sc.render.resolution_y=720;sc.render.resolution_percentage=100
sc.render.fps=24;sc.frame_start=1;sc.frame_end=72;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGB';sc.view_settings.view_transform='AgX'
sc.render.film_transparent=False;sc.render.use_file_extension=True
world=bpy.data.worlds.new('Filmic atmosphere');sc.world=world;world.use_nodes=True
background=world.node_tree.nodes.get('Background');background.inputs[1].default_value=.18

if FILM=='opening':
    append(os.path.join(SOURCE,'ContainmentLaboratory.blend'))
    background.inputs[0].default_value=(.10,.18,.22,1)
    blob=material('Specimen translucent cytoplasm',(.04,.45,.10),.12,0,.04,.48)
    b=blob.node_tree.nodes.get('Principled BSDF');b.inputs['IOR'].default_value=1.36;b.inputs['Subsurface Weight'].default_value=.2;b.inputs['Subsurface Radius'].default_value=(.2,1,.25)
    subject=sphere('Specimen 07',(0,1.28,0),(.24,.20,.23),blob)
    # Organic silhouette around the nucleus, animated breathing rather than a rigid sphere.
    for vert in subject.data.vertices:
        co=vert.co;co*=1+.05*math.sin(co.x*13+co.y*7)*math.sin(co.z*9)
    for frame,scale in [(1,(.24,.20,.23)),(36,(.252,.208,.239)),(72,(.24,.20,.23))]:subject.scale=scale;subject.keyframe_insert('scale',frame=frame)
    core=material('Living nucleus',(.38,.9,.12),.2,0,1.5)
    sphere('Specimen nucleus',(.01,1.29,-.02),(.071,.065,.065),core)
    for p,s in [((-.1,1.34,.03),.025),((.11,1.21,-.02),.034),((-.07,1.18,-.04),.02)]:sphere('Cytoplasmic organelle',p,(s,s,s),core)
    # Real dielectric vessel wall with a physically traced rim, kept thin for clear specimen silhouette.
    glass=material('Optical borosilicate',(.8,.94,.95),.035,0,0,1);glass.node_tree.nodes.get('Principled BSDF').inputs['IOR'].default_value=1.46
    bpy.ops.mesh.primitive_cylinder_add(vertices=96,radius=.445,depth=1.82,end_fill_type='NOTHING',location=v((0,1.4,0)))
    vessel=bpy.context.object;vessel.name='Ray traced containment glass';vessel.data.materials.append(glass)
    sol=vessel.modifiers.new('Four millimeter vessel wall','SOLIDIFY');sol.thickness=.004
    for poly in vessel.data.polygons:poly.use_smooth=True
    light('Clinical soft key',(-2.2,3,-2),(0,1.3,0),230,(.65,.83,1),2.4)
    light('Specimen backlight',(0,2.1,1.9),(0,1.3,0),95,(.15,1,.43),1.6)
    light('Warm laboratory edge',(3,2.3,-1),(0,1.3,0),65,(1,.54,.23),1.7)
    camera((.15,1.43,-1.72),(-.15,1.40,-1.5),(0,1.29,0),50,1.65)
else:
    shore_objects=append(os.path.join(SOURCE,'LakesideShore.blend'))
    for o in shore_objects:
        if 'Reed' in o.name or 'Tip' in o.name:o.scale.x=.68
    bench=append(os.path.join(SOURCE,'LakesideBench.blend'))
    for o in bench:o.location+=v((-1.7,0,.05));o.rotation_euler[2]=math.radians(-15)
    chars=os.path.join(os.path.expanduser('~'),'Data','parasite-work','characters','out')
    girl=append(os.path.join(chars,'LakeGirl.blend'))
    rig=next(o for o in girl if o.type=='ARMATURE')
    for o in girl:
        if o.type=='MESH':o.hide_render=not o.name.endswith('_LOD0')
    meta=json.load(open(os.path.join(chars,'Models','LakeGirl.json')))
    # Rebind source character maps (same maps used by the in-engine model).
    for name,kind in [('Skin','skin'),('Eyes','eyes'),('Hair','hairA'),('Cloth','clothA')]:
        m=bpy.data.materials.get(name)
        if not m:continue
        m.use_nodes=True;nt=m.node_tree;nt.nodes.clear();out=nt.nodes.new('ShaderNodeOutputMaterial');bs=nt.nodes.new('ShaderNodeBsdfPrincipled');nt.links.new(bs.outputs[0],out.inputs[0]);tx=nt.nodes.new('ShaderNodeTexImage');tx.image=bpy.data.images.load(os.path.join(chars,'Textures',meta['textures'][kind]),check_existing=True);nt.links.new(tx.outputs[0],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.62
        if name=='Hair':nt.links.new(tx.outputs['Alpha'],bs.inputs['Alpha'])
        if name=='Skin':bs.inputs['Subsurface Weight'].default_value=.12
    # Authored sit, all rotations in armature world space, descendants inherit from their parents.
    def turn(name,angle,axis):
        pb=rig.pose.bones[name];bpy.context.view_layer.update();head=pb.head.copy();pb.matrix=Matrix.Translation(head)@Matrix.Rotation(math.radians(angle),4,axis)@Matrix.Translation(-head)@pb.matrix;bpy.context.view_layer.update()
    for side in ['Left','Right']:
        turn(side+'UpperLeg',-72,'X');turn(side+'LowerLeg',72,'X')
        turn(side+'UpperArm',68 if side=='Left' else -68,'Y');turn(side+'LowerArm',-65,'X')
    rig.location=v((-1.7,-.22,.11));rig.rotation_euler[2]=math.radians(-15)
    # Bind every mesh to the same placement; meshes parent to the rig and already follow its transform.
    water=material('Ray traced sunset water',(.045,.11,.14),.12,.2,0,.4)
    bs=water.node_tree.nodes.get('Principled BSDF');bs.inputs['IOR'].default_value=1.333
    noise=water.node_tree.nodes.new('ShaderNodeTexNoise');noise.noise_dimensions='4D';noise.inputs['Scale'].default_value=13;noise.inputs['Detail'].default_value=3
    noise.inputs['W'].default_value=0;noise.inputs['W'].keyframe_insert('default_value',frame=1);noise.inputs['W'].default_value=.13;noise.inputs['W'].keyframe_insert('default_value',frame=72)
    bump=water.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.18;bump.inputs['Distance'].default_value=.06
    water.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);water.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    bpy.ops.mesh.primitive_plane_add(size=2,location=v((0,-.035,65)));o=bpy.context.object;o.name='Physically traced lake';o.scale=(150,150,1);o.data.materials.append(water)
    # Physical low-sun atmosphere supplies warm multibounce sky illumination and a visible sun.
    sky=world.node_tree.nodes.new('ShaderNodeTexSky');sky.sky_type='NISHITA';sky.sun_elevation=math.radians(3.2);sky.sun_rotation=math.radians(-100);sky.altitude=.1;sky.air_density=1;sky.dust_density=2.4;sky.ozone_density=1;sky.sun_intensity=.65
    world.node_tree.links.new(sky.outputs['Color'],background.inputs['Color']);background.inputs['Strength'].default_value=.2
    sun=bpy.data.lights.new('Golden hour sunlight','SUN');sun.energy=1.5;sun.angle=math.radians(1.5);sun.color=(1,.64,.34)
    so=bpy.data.objects.new('Golden hour sunlight',sun);bpy.context.collection.objects.link(so);so.rotation_euler=(v((-1.7,.8,0))-v((14,12,80))).to_track_quat('-Z','Y').to_euler()
    glow=material('Visible setting sun',(1,.42,.12),.3,0,6)
    sphere('Distant warm sun',(14,11,80),(2.1,2.1,2.1),glow)
    light('Cool shore fill',(-4,4,-5),(-1.7,.7,0),65,(.58,.72,1),6)
    camera((.9,1.80,-7),(0.5,1.76,-6.8),(-.5,1.05,12),32,8.5)

# Match the runtime normal/metallic/smoothness maps in the physically traced film source.
for m in bpy.data.materials:
    if not m.name.startswith('Cinema') or not m.use_nodes:continue
    kind=m.name.split('.')[0].replace('Cinema','')
    path=os.path.join(RESOURCE,'Textures',kind+'_N.png')
    if not os.path.isfile(path):continue
    bs=m.node_tree.nodes.get('Principled BSDF')
    if not bs:continue
    normal=m.node_tree.nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(path,check_existing=True);normal.image.colorspace_settings.name='Non-Color'
    nm=m.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.65
    m.node_tree.links.new(normal.outputs['Color'],nm.inputs['Color']);m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])

# Preserve source animation/cameras/lights, packed source images for a portable editable film scene.
bpy.ops.file.pack_all();sc.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,('OpeningFilm' if FILM=='opening' else 'EndingFilm')+'.blend'),compress=True)
sc.render.filepath=os.path.join(OUT,'frame-')
if MODE=='probe':
    sc.cycles.samples=24;sc.render.filepath=os.path.join(OUT,'probe.png');bpy.ops.render.render(write_still=True)
else:
    sc.render.use_persistent_data=True;bpy.ops.render.render(animation=True)
print('[film] COMPLETE',FILM,MODE,flush=True)
