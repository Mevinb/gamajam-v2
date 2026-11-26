#!/usr/bin/env python3
"""CPU-only source/export audit and optional studio render, never a Unity acceptance claim."""
import bpy, bmesh, hashlib, json, math, sys
from pathlib import Path
from mathutils import Vector

args=sys.argv[sys.argv.index('--')+1:]
P=Path(args[0]).resolve(); SRC=P/'ArtSource/EnvironmentRealism'
manifest=json.loads((SRC/'production-manifest.json').read_text())
out=P/'Logs/environment-realism-review';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
report={'scope':'Blender CPU source/export verification; no Unity import, navigation, gameplay or GPU timing claim','models':{}}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for name,item in manifest['models'].items():
    path=P/item['export'];assert sha(path)==item['sha256']
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True)
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==1,(name,len(objects))
    obj=objects[0];mesh=obj.data;mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles)==item['triangles'],name
    vv=[obj.matrix_world@v.co for v in mesh.vertices]
    vv=[Vector((v.x,v.z,-v.y)) for v in vv]
    lo=[min(v[i] for v in vv) for i in range(3)];hi=[max(v[i] for v in vv) for i in range(3)]
    error=max(abs(a-b) for actual,expected in ((lo,item['bounds_min']),(hi,item['bounds_max'])) for a,b in zip(actual,expected))
    assert error<.00001,(name,'axis/scale round trip',lo,hi,error)
    assert len(mesh.uv_layers)==1 and len(mesh.materials)==1
    for uv in mesh.uv_layers[0].data:assert 0<=uv.uv.x<=1 and 0<=uv.uv.y<=1
    assert all(o.type=='MESH' for o in bpy.context.scene.objects)
    assert all(abs(v)<100 for p in vv for v in p)
    report['models'][name]={'triangles':len(mesh.loop_triangles),'roundtrip_axis_bound_error_metres':error,
                             'bounds_min':lo,'bounds_max':hi,'material_slots':len(mesh.materials)}
for channel in 'ANM':assert sha(P/manifest['atlas'][channel]['path'])==manifest['atlas'][channel]['sha256']
source=P/manifest['packed_source']['path'];assert sha(source)==manifest['packed_source']['sha256']
bpy.ops.wm.open_mainfile(filepath=str(source))
packed=[]
for im in bpy.data.images:
    if not im.name.startswith('EnvironmentRealism_'):continue
    assert im.packed_file and len(im.packed_file.data)>0
    channel=im.name[-1];assert hashlib.sha256(bytes(im.packed_file.data)).hexdigest()==manifest['atlas'][channel]['sha256']
    packed.append(channel)
assert sorted(packed)==list('AMN')
report['packed_images']=packed
report['total_source_triangles']=sum(x['triangles'] for x in manifest['models'].values())
report['new_asset_bytes']=sum(p.stat().st_size for p in (P/'Assets/Parasite/Art/EnvironmentRealism').rglob('*') if p.is_file())+source.stat().st_size
report['passed']=True
(SRC/'source-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('[EnvironmentRealism] VERIFIED FBX metre axes/bounds, triangle counts, UVs, material slots and packed texture hashes',flush=True)

if '--render' in args:
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24
    scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=1100;scene.render.resolution_y=780;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
    def cv(p):return Vector((p[0],-p[2],p[1]))
    def place(name,p,scale=(1,1,1)):
        obj=bpy.data.objects[name];obj.location=cv(p);obj.scale=(scale[0],scale[2],scale[1]);return obj
    place('WindowReveal',(-2.4,2.3,0),(1.7,1.95,1))
    place('EntranceSurround',(.0,0,0))
    place('Condenser',(2.65,0,-.1))
    place('CurbStone',(-.9,0,-2.7))
    place('DrainGrate',(.8,.02,-2.6))
    place('FracturedStone',(2.5,.01,-2.7))
    place('GrassClump',(2.1,.01,-2.6))
    for i in (-3,-2,-1,1,2,3):
        obj=bpy.data.objects['CurbStone'].copy();obj.data=bpy.data.objects['CurbStone'].data;scene.collection.objects.link(obj)
        obj.location=cv((-.9+i,0,-2.7))
    def plain(name,color,rough):
        mat=bpy.data.materials.new(name);mat.use_nodes=True
        bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*color,1);bsdf.inputs['Roughness'].default_value=rough
        return mat
    def block(name,p,s,mat):
        bpy.ops.mesh.primitive_cube_add(size=1,location=cv(p));obj=bpy.context.object;obj.name=name;obj.scale=(s[0],s[2],s[1]);obj.data.materials.append(mat)
    block('Studio floor',(0,-.08,0),(22,.14,16),plain('Studio neutral stone',(.18,.17,.15),.82))
    # A neutral wall clarifies real jamb/sill relief; it is review staging, never an exported kit piece.
    block('Review wall',(-1.2,1.9,.15),(5.4,3.8,.26),plain('Review matte plaster',(.27,.25,.215),.85))
    world=bpy.data.worlds.new('Studio world');world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.21,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
    for name,p,power,size,color in [('Large soft key',(-3,6,-4),1250,5,(1,.86,.72)),('Cool fill',(5,4,-1),780,4,(.76,.86,1))]:
        data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
        obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=cv(p)
        obj.rotation_euler=(cv((0,1,0))-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('ReviewCamera');camera=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(camera)
    camera.location=cv((8,5.4,-10));camera.rotation_euler=(cv((0,1.4,-.5))-camera.location).to_track_quat('-Z','Y').to_euler();data.lens=49
    scene.camera=camera;scene.render.filepath=str(out/'source-studio.png')
    print('[EnvironmentRealism] CPU studio render; source review only',flush=True)
    bpy.ops.render.render(write_still=True)
    print('[EnvironmentRealism] CPU studio render complete',flush=True)
