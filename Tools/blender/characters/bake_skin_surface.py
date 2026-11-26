"""Author/bake shared skin response in Blender, without reading an albedo image.

BLENDER_USER_RESOURCES=<MPFB profile> nice -n 15 blender -b -t 2 \
  --python-exit-code 1 --python bake_skin_surface.py -- <Characters root> [--size 2048]

Writes Textures/Skins/Surface/{Adult,Mature,Elder}_{N,M}.png and provenance JSON.
N: Cycles tangent (+X,+Y,+Z), millimetre-scale 3D pore/fine-crease bump.
M: R smoothness, G Cycles local-geometry AO (35 mm), B optical thickness
   (inward BVH ray distance / 30 mm, 0 thin, 1 thick). All channels linear.
The canonical full MPFB body retains the shared body UV layout. No character
FBX, skeleton, garment atlas or source skin photograph is modified.
"""
import argparse
import hashlib
import json
import math
import os
import sys
import time

import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chartools as ct
from skin_profiles import PROFILES, textures_for


def math_node(nt, operation, a, b=None):
    n = nt.nodes.new("ShaderNodeMath")
    n.operation = operation
    for index, value in enumerate((a, b)):
        if value is None:
            continue
        if isinstance(value, (float, int)):
            n.inputs[index].default_value = value
        else:
            nt.links.new(value, n.inputs[index])
    return n.outputs[0]


def gaussian(x, centre, width):
    return math.exp(-((x - centre) / width) ** 2)


def response_attributes(body):
    me = body.data
    me.update()
    tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices],
                               [list(p.vertices) for p in me.polygons])
    top = max(v.co.z for v in me.vertices)
    names = {g.index: g.name.replace("mixamorig:", "") for g in body.vertex_groups}
    layer = me.color_attributes.new("skin_response", "FLOAT_COLOR", "POINT")
    hit_count = 0
    thicknesses = []
    for v in me.vertices:
        x, y, z = v.co
        dz = top - z
        weights = {names.get(g.group, ""): g.weight for g in v.groups}
        head = weights.get("Head", 0.0)
        front = max(0.0, min(1.0, (-y - 0.01) / 0.04)) * head
        nose = gaussian(x, 0.0, 0.022) * gaussian(dz, 0.13, 0.04) * front
        forehead = gaussian(x, 0.0, 0.055) * gaussian(dz, 0.065, 0.026) * front
        lips = gaussian(x, 0.0, 0.032) * gaussian(dz, 0.178, 0.012) * front
        eye_edge = gaussian(abs(x), 0.057, 0.015) * gaussian(dz, 0.112, 0.017) * front
        palm = sum(g.weight for g in v.groups if "Hand" in names.get(g.group, ""))
        smoothness = min(0.57, 0.31 + 0.12 * nose + 0.065 * forehead + 0.22 * lips + 0.025 * palm)
        # A small offset skips the starting face. Only real opposing geometry
        # contributes; open-mouth/missing hits conservatively become thick.
        direction = -v.normal.normalized()
        hit, _, _, distance = tree.ray_cast(v.co + direction * 0.00035, direction, 0.30)
        thickness = min(1.0, (distance + 0.00035) / 0.030) if hit is not None else 1.0
        if hit is not None:
            hit_count += 1
        thicknesses.append(thickness)
        layer.data[v.index].color = (smoothness, thickness,
                                     min(1.0, forehead + eye_edge * 0.8), 1.0)
    return {"vertices": len(me.vertices), "rayHits": hit_count,
            "thicknessNormalizedPercentiles": np.percentile(thicknesses, [0, 10, 50, 90, 100]).tolist()}


def material(body, settings):
    m = bpy.data.materials.new("SkinSurfaceAuthoring")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bs.inputs["Base Color"].default_value = (0.34, 0.18, 0.12, 1.0)
    nt.links.new(bs.outputs[0], out.inputs["Surface"])
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "skin_response"
    channels = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], channels.inputs[0])

    pores = nt.nodes.new("ShaderNodeTexVoronoi")
    pores.inputs["Scale"].default_value = 650.0  # 1.54 mm cell spacing in metre-space
    pores.inputs["Randomness"].default_value = 1.0
    nt.links.new(geo.outputs["Position"], pores.inputs["Vector"])
    pit = nt.nodes.new("ShaderNodeMapRange")
    pit.interpolation_type = "SMOOTHSTEP"
    pit.clamp = True
    pit.inputs["From Min"].default_value = 0.025
    pit.inputs["From Max"].default_value = 0.22
    pit.inputs["To Min"].default_value = -1.0
    pit.inputs["To Max"].default_value = 0.0
    nt.links.new(pores.outputs["Distance"], pit.inputs["Value"])

    grain = nt.nodes.new("ShaderNodeTexNoise")
    grain.inputs["Scale"].default_value = 1300.0
    grain.inputs["Detail"].default_value = 2.0
    grain.inputs["Roughness"].default_value = 0.72
    nt.links.new(geo.outputs["Position"], grain.inputs["Vector"])
    small = math_node(nt, "MULTIPLY", grain.outputs["Fac"], 0.16)
    height = math_node(nt, "ADD", pit.outputs["Result"], small)
    relief = math_node(nt, "MULTIPLY", height, settings["pore_depth"])

    # Forehead/crow-foot crease field is limited by authored anatomical weights.
    # Warp the bands in 3D; UV seams never determine their direction or depth.
    xyz = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], xyz.inputs[0])
    fine = nt.nodes.new("ShaderNodeTexNoise")
    fine.inputs["Scale"].default_value = 95.0
    fine.inputs["Detail"].default_value = 2.0
    nt.links.new(geo.outputs["Position"], fine.inputs["Vector"])
    phase = math_node(nt, "ADD", math_node(nt, "MULTIPLY", xyz.outputs["Z"], 1150.0),
                      math_node(nt, "MULTIPLY", fine.outputs["Fac"], 2.3))
    band = math_node(nt, "POWER", math_node(nt, "ABSOLUTE", math_node(nt, "SINE", phase)), 12.0)
    crease = math_node(nt, "MULTIPLY", band, channels.outputs["Blue"])
    crease = math_node(nt, "MULTIPLY", crease, -settings["crease_depth"])
    height_m = math_node(nt, "ADD", relief, crease)
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.7
    bump.inputs["Distance"].default_value = 1.0  # height field is already in metres
    nt.links.new(height_m, bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bs.inputs["Normal"])

    smooth_noise = math_node(nt, "MULTIPLY_ADD", grain.outputs["Fac"], 0.10)
    # MULTIPLY_ADD third operand defaults to 0: shift explicitly to 0.95.
    smooth_noise.node.inputs[2].default_value = 0.95
    smooth = math_node(nt, "MULTIPLY", channels.outputs["Red"], smooth_noise)
    bs.inputs["Roughness"].default_value = 0.65
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.inputs["Distance"].default_value = 0.035
    ao.samples = 16
    ao.only_local = True
    packed = nt.nodes.new("ShaderNodeCombineColor")
    nt.links.new(smooth, packed.inputs["Red"])
    nt.links.new(ao.outputs["AO"], packed.inputs["Green"])
    nt.links.new(channels.outputs["Green"], packed.inputs["Blue"])
    emission = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(packed.outputs[0], emission.inputs[0])
    target = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = target
    body.data.materials.clear()
    body.data.materials.append(m)
    for p in body.data.polygons:
        p.use_smooth = True
    return out, bs, emission, target


def bake(body, target, size, kind, path):
    image = bpy.data.images.new(os.path.basename(path), size, size, alpha=False, float_buffer=True)
    image.colorspace_settings.name = "Non-Color"
    target.image = image
    target.id_data.nodes.active = target
    ct.set_active(body)
    bpy.ops.object.bake(type=kind, use_clear=True, margin=12)
    pixels = np.empty(size * size * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    rgb = pixels.reshape(size, size, 4)[..., :3]
    # Image.save writes data textures without a display/view transform.
    image.filepath_raw = path
    image.file_format = "PNG"
    bpy.context.scene.render.image_settings.color_depth = "16"
    image.save()
    result = {"sha256": hashlib.sha256(open(path, "rb").read()).hexdigest(),
              "bytes": os.path.getsize(path), "size": size,
              "channelMin": rgb.min(axis=(0, 1)).tolist(), "channelMax": rgb.max(axis=(0, 1)).tolist(),
              "channelMean": rgb.mean(axis=(0, 1), dtype=np.float64).tolist()}
    bpy.data.images.remove(image)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("characters_root")
    parser.add_argument("--size", type=int, default=2048)
    parser.add_argument("--profiles", nargs="+", default=list(PROFILES))
    parser.add_argument("--update-metadata", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = os.path.join(args.characters_root, "Textures", "Skins", "Surface")
    os.makedirs(output, exist_ok=True)
    receipt = {"schema": 1, "blender": bpy.app.version_string, "device": "CPU", "threads": 2,
               "normalConvention": "MikkTSpace tangent +X +Y +Z; no green flip",
               "maskChannels": {"R": "smoothness", "G": "local AO, radius 0.035 m", "B": "optical thickness / 0.030 m"},
               "albedoInputs": [], "profiles": {}}
    started = time.monotonic()
    for name in args.profiles:
        settings = PROFILES[name]
        ct.clear_scene()
        H, _ = ct.mpfb_services()
        info = H._create_default_human_info_dict()
        info["rig"] = "mixamo_unity"
        info["name"] = "SkinSurface_" + name
        info["phenotype"]["age"] = settings["age"]
        setup = H.get_default_deserialization_settings()
        setup["subdiv_levels"] = 0
        body = H.deserialize_from_dict(info, setup)
        ct.apply_shape_keys(body)
        ct.remove_modifiers(body)
        ct.delete_verts(body, [not b for b in ct.group_mask(body, "body")])
        # All authoring distances and BVH tests are in physical metres.
        body.scale = (1.0, 1.0, 1.0)
        body.location = (0.0, 0.0, 0.0)
        body.parent = None
        body.matrix_world.identity()
        stats = response_attributes(body)
        out, bs, emission, target = material(body, settings)
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.render.threads_mode = "FIXED"
        scene.render.threads = 2
        scene.cycles.samples = 8
        scene.cycles.use_denoising = False
        scene.render.bake.normal_space = "TANGENT"
        scene.render.bake.normal_r = "POS_X"
        scene.render.bake.normal_g = "POS_Y"
        scene.render.bake.normal_b = "POS_Z"
        scene.render.bake.margin_type = "EXTEND"
        normal = bake(body, target, args.size, "NORMAL", os.path.join(output, name + "_N.png"))
        out.id_data.links.new(emission.outputs[0], out.inputs["Surface"])
        mask = bake(body, target, max(256, args.size // 2), "EMIT", os.path.join(output, name + "_M.png"))
        receipt["profiles"][name] = dict(authoring=settings, geometry=stats, normal=normal, mask=mask)
        print("[skin] BAKED", name, json.dumps(receipt["profiles"][name]), flush=True)
    receipt["elapsedSeconds"] = time.monotonic() - started
    with open(os.path.join(output, "SkinSurface.provenance.json"), "w") as f:
        json.dump(receipt, f, indent=2)
        f.write("\n")
    if args.update_metadata:
        import glob
        for path in glob.glob(os.path.join(args.characters_root, "Models", "*.json")):
            with open(path) as f:
                data = json.load(f)
            data["textures"].update(textures_for(data.get("age", 0.5)))
            with open(path, "w") as f:
                json.dump(data, f, indent=1)
                f.write("\n")
    print("[skin] COMPLETE", receipt["elapsedSeconds"], flush=True)


if __name__ == "__main__":
    main()
