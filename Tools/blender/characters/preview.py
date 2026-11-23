"""
Quick Cycles preview of a built character (reads the .blend + json written by make_character.py --blend).
  blender -b <out>/<Id>.blend --python preview.py -- <out_dir> <Id> <png_prefix> [lod]
Renders front / three-quarter / face close-up, reproducing the Unity shader's tint logic.
"""
import bpy, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir, cid, prefix = argv[0], argv[1], argv[2]
lod = int(argv[3]) if len(argv) > 3 else 0
meta = json.load(open(os.path.join(out_dir, "Models", cid + ".json")))
tex = os.path.join(out_dir, "Textures")


def img(rel, data=False):
    im = bpy.data.images.load(os.path.join(tex, rel), check_existing=True)
    im.colorspace_settings.name = "Non-Color" if data else "sRGB"
    return im


def lin(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(name, kind):
    m = bpy.data.materials[name]
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], out.inputs[0])
    t = nt.nodes.new("ShaderNodeTexImage")
    if kind == "Skin":
        t.image = img(meta["textures"]["skin"])
        mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1
        st = meta.get("skinTint", [1, 1, 1])
        mul.inputs[7].default_value = (*lin(st), 1)
        nt.links.new(t.outputs[0], mul.inputs[6]); nt.links.new(mul.outputs[2], bs.inputs["Base Color"])
        bs.inputs["Roughness"].default_value = 0.55
        bs.inputs["Subsurface Weight"].default_value = 0.15
        bs.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
        bs.inputs["Subsurface Scale"].default_value = 0.01
    elif kind == "Eyes":
        t.image = img(meta["textures"]["eyes"])
        nt.links.new(t.outputs[0], bs.inputs["Base Color"])
        bs.inputs["Roughness"].default_value = 0.1
    elif kind == "Hair":
        t.image = img(meta["textures"]["hairA"])
        nt.links.new(t.outputs[0], bs.inputs["Base Color"])
        nt.links.new(t.outputs[1], bs.inputs["Alpha"])
        bs.inputs["Roughness"].default_value = 0.45
        m.blend_method = "CLIP" if hasattr(m, "blend_method") else None
    elif kind == "Cloth":
        t.image = img(meta["textures"]["clothA"])
        mk = nt.nodes.new("ShaderNodeTexImage"); mk.image = img(meta["textures"]["clothM"], True); mk.interpolation = "Closest"
        sep = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(mk.outputs[0], sep.inputs[0])
        col = t.outputs[0]
        for gi, key in ((1, "A"), (2, "B"), (3, "C")):
            tint = meta["tints"].get(key)
            if not tint:
                continue
            # weight = 1 if round(r*3)==gi
            m1 = nt.nodes.new("ShaderNodeMath"); m1.operation = "MULTIPLY"; m1.inputs[1].default_value = 3
            nt.links.new(sep.outputs[0], m1.inputs[0])
            m2 = nt.nodes.new("ShaderNodeMath"); m2.operation = "SUBTRACT"; m2.inputs[1].default_value = gi
            nt.links.new(m1.outputs[0], m2.inputs[0])
            m3 = nt.nodes.new("ShaderNodeMath"); m3.operation = "ABSOLUTE"; nt.links.new(m2.outputs[0], m3.inputs[0])
            m4 = nt.nodes.new("ShaderNodeMath"); m4.operation = "LESS_THAN"; m4.inputs[1].default_value = 0.5
            nt.links.new(m3.outputs[0], m4.inputs[0])
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            nt.links.new(m4.outputs[0], mix.inputs[0]); nt.links.new(col, mix.inputs[6])
            mix.inputs[7].default_value = (tint[0], tint[1], tint[2], 1)
            col = mix.outputs[2]
        nt.links.new(col, bs.inputs["Base Color"])
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
        nt.links.new(sep.outputs[2], inv.inputs[1]); nt.links.new(inv.outputs[0], bs.inputs["Roughness"])
        nt.links.new(sep.outputs[1], bs.inputs["Metallic"])
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = img(meta["textures"]["clothN"], True)
        nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(n.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    nt.links.new(nt.nodes.new("ShaderNodeUVMap").outputs[0], t.inputs[0])


for name in ("Skin", "Eyes", "Hair", "Cloth"):
    if name in bpy.data.materials:
        mat(name, name)
for o in bpy.data.objects:
    if o.type == "MESH":
        o.hide_render = not o.name.endswith("_LOD%d" % lod)

sc = bpy.context.scene
sc.render.engine = "CYCLES"
prefs = bpy.context.preferences.addons["cycles"].preferences
try:
    prefs.compute_device_type = "OPTIX"; prefs.get_devices()
    for d in prefs.devices: d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
except Exception as e:
    print("gpu fail", e)
sc.cycles.samples = 48
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 900, 1100
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_management = "FOLLOW_SCENE"
sc.view_settings.view_transform = "AgX" if "AgX" in [v for v in ("AgX",)] else "Standard"
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.68, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.45
for n, (loc, en) in {"key": ((2.0, -3.0, 3.0), 320), "fill": ((-3, -2, 1.8), 110), "rim": ((0.5, 3, 3), 220)}.items():
    l = bpy.data.lights.new(n, "AREA"); l.energy = en; l.size = 2
    ob = bpy.data.objects.new(n, l); sc.collection.objects.link(ob); ob.location = loc
    ob.rotation_euler = (Vector((0, 0, 1.1)) - ob.location).to_track_quat("-Z", "Y").to_euler()
h = meta.get("height", 1.7)
cams = [("front", (0, -4.0, h * 0.55), (0, 0, h * 0.5), 38), ("q34", (2.4, -3.1, h * 0.62), (0, 0, h * 0.5), 38),
        ("back", (-1.2, 3.6, h * 0.6), (0, 0, h * 0.5), 38), ("face", (0.32, -0.85, h - 0.1), (0, 0, h - 0.12), 55)]
for name, loc, tgt, lens in cams:
    cd = bpy.data.cameras.new(name); cd.lens = lens
    c = bpy.data.objects.new(name, cd); sc.collection.objects.link(c); c.location = loc
    c.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = c
    sc.render.filepath = prefix + "_" + name + ".png"
    bpy.ops.render.render(write_still=True)
