# Preview render of the exported trees (Cycles): LOD0 of each species side by side + LOD1/LOD2 of the first.
# usage: blender -b --python preview_trees.py -- <fbx dir> <atlas dir> <bark src dir> <out png>
import bpy, sys, os, math
argv = sys.argv[sys.argv.index("--") + 1:]
FBX, ATLAS, SRC, OUT = argv[:4]
BARK = {"Plane": "bark_platanus", "Linden": "bark_brown_02", "Maple": "japanese_zelkova_bark"}

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
try:
    p = bpy.context.preferences.addons['cycles'].preferences; p.compute_device_type = 'OPTIX'; p.get_devices()
    for d in p.devices: d.use = d.type == 'OPTIX'
    sc.cycles.device = 'GPU'
except Exception as e: print(e)
sc.cycles.samples = 64; sc.render.resolution_x = 1600; sc.render.resolution_y = 700
sc.view_settings.view_transform = 'AgX'


def mat_bark(name, bark):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(SRC, bark + "_diff_1k.jpg"))
    nt.links.new(t.outputs[0], b.inputs['Base Color']); b.inputs['Roughness'].default_value = 0.85
    return m


def mat_leaf(name, atlas):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(ATLAS, atlas + "_albedo.png"))
    nt.links.new(t.outputs[0], b.inputs['Base Color'])
    thr = nt.nodes.new('ShaderNodeMath'); thr.operation = 'GREATER_THAN'; thr.inputs[1].default_value = 0.4
    nt.links.new(t.outputs[1], thr.inputs[0]); nt.links.new(thr.outputs[0], b.inputs['Alpha'])
    b.inputs['Roughness'].default_value = 0.6
    b.inputs['Subsurface Weight'].default_value = 0.0
    try: b.inputs['Transmission Weight'].default_value = 0.15
    except KeyError: pass
    return m


x = -9.0
first = True
for sp in ("Plane", "Linden", "Maple"):
    bpy.ops.import_scene.fbx(filepath=os.path.join(FBX, f"Tree{sp}.fbx"))
    for ob in [o for o in sc.objects if o.name.startswith(f"Tree{sp}_LOD")]:
        lod = int(ob.name.split("LOD")[1][:1])
        for i, slot in enumerate(ob.material_slots):
            n = slot.material.name
            if n.startswith("Bark"): slot.material = bpy.data.materials.get("P" + n) or mat_bark("P" + n, BARK[sp])
            else: slot.material = bpy.data.materials.get("P" + n) or mat_leaf("P" + n, n)
        if lod == 0: ob.location = (x, 0, 0)
        elif first: ob.location = (x + 8.0 * lod + 18.0, 9.0, 0)
        else: ob.hide_render = True
    x += 9.0
    first = False
bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, 0))
g = bpy.data.materials.new("G"); g.use_nodes = True; g.node_tree.nodes["Principled BSDF"].inputs['Base Color'].default_value = (0.25, 0.25, 0.25, 1)
sc.objects["Plane"].data.materials.append(g)
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 3.0; sun.angle = math.radians(3)
so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(30))
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.55, 0.75, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
cam = bpy.data.cameras.new("C"); cam.lens = 35
co = bpy.data.objects.new("C", cam); sc.collection.objects.link(co); sc.camera = co
co.location = (4, -30, 6.5); co.rotation_euler = (math.radians(84), 0, math.radians(4))
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
