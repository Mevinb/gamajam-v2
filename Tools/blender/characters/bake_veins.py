"""
Bake the shared possession vein mask in MakeHuman body UV space (all characters share the base-mesh UV layout).
  blender -b --python bake_veins.py -- <out_dir>      -> <out_dir>/Textures/Skin_VeinMask.png
Veins = distorted Voronoi cell edges evaluated in 3D (no UV seams), weighted per body region (neck, face sides,
forearms, hands strongest), so the glow reads as veins crawling out from the collar and sleeves.
"""
import bpy, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chartools as ct
import atlas

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir = argv[0]
size = 2048

ct.clear_scene()
HumanService, _ = ct.mpfb_services()
info = HumanService._create_default_human_info_dict()
info["rig"] = "mixamo_unity"
info["name"] = "VeinBase"
s = HumanService.get_default_deserialization_settings()
s["subdiv_levels"] = 0
body = HumanService.deserialize_from_dict(info, s)
ct.apply_shape_keys(body)
ct.remove_modifiers(body)
ct.delete_verts(body, [not b for b in ct.group_mask(body, "body")])

# region weights from bone weights
REGION = {"Neck": 1.0, "Head": 0.75, "LeftHand": 0.85, "RightHand": 0.85, "LeftForeArm": 1.0, "RightForeArm": 1.0,
          "LeftArm": 0.55, "RightArm": 0.55, "Spine2": 0.55, "LeftShoulder": 0.5, "RightShoulder": 0.5, "Spine1": 0.3}
names = {vg.index: vg.name.replace("mixamorig:", "") for vg in body.vertex_groups}
me = body.data
col = me.color_attributes.new("region", "FLOAT_COLOR", "POINT")
head_z = max(v.co.z for v in me.vertices)
for v in me.vertices:
    w = 0.0
    tot = 0.0
    for g in v.groups:
        n = names.get(g.group, "")
        if n in REGION:
            w += REGION[n] * g.weight
        if n.startswith("Left") or n.startswith("Right") or n in ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head"):
            tot += g.weight
    # fingers count like hands
    for g in v.groups:
        n = names.get(g.group, "")
        if "Hand" in n and n not in REGION:
            w += 0.6 * g.weight
    # keep the centre of the face (eyes / nose / lips) clean; veins on jaw, temples, cheeks
    if v.co.z > head_z - 0.25:
        fx = abs(v.co.x)
        front = v.co.y < -0.05
        if front and fx < 0.035 and v.co.z > head_z - 0.2:
            w *= 0.15
        if v.co.z > head_z - 0.08:  # scalp
            w *= 0.3
    col.data[v.index].color = (w, w, w, 1.0)

m = bpy.data.materials.new("veins")
m.use_nodes = True
nt = m.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
em = nt.nodes.new("ShaderNodeEmission")
geo = nt.nodes.new("ShaderNodeNewGeometry")
noise = nt.nodes.new("ShaderNodeTexNoise")
noise.inputs["Scale"].default_value = 18.0
noise.inputs["Detail"].default_value = 3.0
nt.links.new(geo.outputs["Position"], noise.inputs["Vector"])
mix = nt.nodes.new("ShaderNodeVectorMath")
mix.operation = "MULTIPLY_ADD"
mix.inputs[1].default_value = (0.012, 0.012, 0.012)
nt.links.new(noise.outputs["Color"], mix.inputs[0])
nt.links.new(geo.outputs["Position"], mix.inputs[2])


def veins(scale, width):
    vo = nt.nodes.new("ShaderNodeTexVoronoi")
    vo.feature = "DISTANCE_TO_EDGE"
    vo.inputs["Scale"].default_value = scale
    vo.inputs["Randomness"].default_value = 1.0
    nt.links.new(mix.outputs[0], vo.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    ramp.inputs["From Min"].default_value = 0.0
    ramp.inputs["From Max"].default_value = width
    ramp.inputs["To Min"].default_value = 1.0
    ramp.inputs["To Max"].default_value = 0.0
    nt.links.new(vo.outputs["Distance"], ramp.inputs["Value"])
    return ramp.outputs["Result"]


big = veins(9.0, 0.035)
small = veins(26.0, 0.03)
mx = nt.nodes.new("ShaderNodeMath")
mx.operation = "MULTIPLY_ADD"
mx.inputs[1].default_value = 0.55
nt.links.new(small, mx.inputs[0])
nt.links.new(big, mx.inputs[2])
attr = nt.nodes.new("ShaderNodeAttribute")
attr.attribute_name = "region"
mul = nt.nodes.new("ShaderNodeMath")
mul.operation = "MULTIPLY"
mul.use_clamp = True
nt.links.new(mx.outputs[0], mul.inputs[0])
nt.links.new(attr.outputs["Fac"], mul.inputs[1])
nt.links.new(mul.outputs[0], em.inputs[0])
nt.links.new(em.outputs[0], out.inputs[0])
body.data.materials.clear()
body.data.materials.append(m)
im = atlas.new_image("veins", size, data=True)
t = nt.nodes.new("ShaderNodeTexImage")
t.image = im
nt.nodes.active = t
atlas.setup_cycles(1)
ct.set_active(body)
bpy.ops.object.bake(type="EMIT", use_clear=True, margin=8)
px = atlas.pixels(im)[..., :3].copy()
os.makedirs(os.path.join(out_dir, "Textures"), exist_ok=True)
atlas.save_array(px, os.path.join(out_dir, "Textures", "Skin_VeinMask.png"), "PNG", colorspace="Non-Color")
print("[chars] vein mask written", px.mean())
