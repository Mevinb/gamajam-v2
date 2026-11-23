"""
Build one PARASITE character and export it for Unity.

  blender -b --python make_character.py -- <CharacterId> <out_dir> <fabric_dir> [--blend]

Outputs into <out_dir>:
  Models/<Id>.fbx            Armature (Unity Humanoid names, T-pose) + <Id>_LOD0/_LOD1/_LOD2 skinned meshes
  Models/<Id>.json           metadata (profession, names, materials/textures, tints, palettes, triangle counts, assets)
  Textures/<Id>_Cloth_A.jpg / _Cloth_N.jpg / _Cloth_M.png / <Id>_Hair_A.png
  Textures/Skins/<skin>.jpg, Textures/Eyes/<colour>.png (shared)
"""
import bpy, sys, os, json, time, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chartools as ct
import garments as gm
import atlas
import specs

LOD0_BUDGET = 20000
LOD1_TARGET = 7000
LOD2_TARGET = 2500
BODY_BUDGET = 8500
HAND_BUDGET = 1300
HEAD_BUDGET = 5200
LIMB_BUDGET = 1800


def srgb_to_lin(c):
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def assign_tint_groups(o, rule, waist_z):
    """Face float attribute 'tint_group' (0 none, 1 A, 2 B, 3 C)."""
    me = o.data
    attr = me.attributes.get("tint_group") or me.attributes.new("tint_group", "FLOAT", "FACE")
    vals = [0.0] * len(me.polygons)
    code = {"A": 1.0, "B": 2.0, "C": 3.0}
    if isinstance(rule, str) and rule in code:
        vals = [code[rule] / 3.0] * len(me.polygons)
    elif isinstance(rule, dict):
        # connected components -> above / below waist by centroid
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        comp = [-1] * len(bm.faces)
        cid = 0
        for f in bm.faces:
            if comp[f.index] >= 0:
                continue
            stack = [f]
            comp[f.index] = cid
            while stack:
                x = stack.pop()
                for e in x.edges:
                    for g in e.link_faces:
                        if comp[g.index] < 0:
                            comp[g.index] = cid
                            stack.append(g)
            cid += 1
        cz = {}
        for f in bm.faces:
            c = f.calc_center_median()
            s = cz.setdefault(comp[f.index], [0.0, 0])
            s[0] += c.z
            s[1] += 1
        for f in bm.faces:
            s = cz[comp[f.index]]
            z = s[0] / s[1]
            g = rule.get("upper") if z > waist_z else rule.get("lower")
            vals[f.index] = code.get(g, 0.0) / 3.0
        bm.free()
    attr.data.foreach_set("value", vals)


def body_regions(body):
    """Per-vertex region: hand / face (front of the head: eyes, nose, mouth) / head / limb."""
    names = {vg.index: vg.name for vg in body.vertex_groups}
    me = body.data
    head_vs = [v.co for v in me.vertices]
    reg = []
    zmax = max(v.co.z for v in me.vertices)
    for v in me.vertices:
        best, bw = "", 0.0
        for g in v.groups:
            n = names.get(g.group, "")
            if g.weight > bw and n.startswith("mixamorig:"):
                best, bw = n[10:], g.weight
        if "Hand" in best:
            reg.append("hand")
        elif best in ("Head", "Neck", "Jaw", "LeftEye", "RightEye"):
            face = best != "Neck" and v.co.y < -0.02 and v.co.z > zmax - 0.24 and abs(v.co.x) < 0.065
            reg.append("face" if face else "head")
        else:
            reg.append("limb")
    return reg


def decimate_group(o, ratio, group, factor):
    if ratio >= 0.999:
        return
    ct.set_active(o)
    m = o.modifiers.new("Dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = max(0.05, ratio)
    m.use_collapse_triangulate = True
    m.vertex_group = group
    m.vertex_group_factor = factor
    while o.modifiers.find("Dec") > 0:
        bpy.ops.object.modifier_move_up(modifier="Dec")
    bpy.ops.object.modifier_apply(modifier="Dec")


def remove_mouth_interior(body):
    """Delete faces whose UVs fall inside MPFB's inside-mouth mask (closed mouths never show it)."""
    import numpy as np
    from bl_ext.user_default.mpfb.services.locationservice import LocationService
    path = os.path.join(LocationService.get_mpfb_data("textures"), "mpfb_inside-mouth.jpg")
    if not os.path.exists(path):
        ct.log("WARN inside-mouth mask not found")
        return
    im = bpy.data.images.load(path, check_existing=True)
    w, h = im.size
    px = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(px)
    mask = px.reshape(h, w, 4)[..., 0] > 0.5
    me = body.data
    uv = me.uv_layers.active.data
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active
    kill = []
    for f in bm.faces:
        inside = True
        for l in f.loops:
            u, v = l[uvl].uv
            x = min(w - 1, max(0, int(u * w)))
            y = min(h - 1, max(0, int(v * h)))
            if not mask[y, x]:
                inside = False
                break
        if inside:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    me.update()
    ct.log("mouth interior faces removed", len(kill))


def decimate(o, ratio, protect=None, factor=10.0):
    if ratio >= 0.999 or len(o.data.polygons) < 50:
        return
    ct.set_active(o)
    m = o.modifiers.new("Dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = max(0.02, ratio)
    m.use_collapse_triangulate = True
    if protect and o.vertex_groups.get(protect):
        m.vertex_group = protect
        m.vertex_group_factor = factor
        m.invert_vertex_group = True  # decimate weights mark *cheap* verts; invert so protected verts are costly
    # keep armature last so decimate applies to rest geometry
    while o.modifiers.find("Dec") > 0:
        bpy.ops.object.modifier_move_up(modifier="Dec")
    bpy.ops.object.modifier_apply(modifier="Dec")


def duplicate(o, suffix):
    c = o.copy()
    c.data = o.data.copy()
    c.name = o.name + suffix
    bpy.context.collection.objects.link(c)
    return c


def join(objs, name):
    objs = [o for o in objs if o is not None]
    ct.set_active(objs[0])
    for o in objs:
        o.select_set(True)
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    return o


def simple_material(name, color=(0.8, 0.8, 0.8)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes.get("Principled BSDF")
    if bs:
        bs.inputs["Base Color"].default_value = (*color, 1.0)
    return m


def set_material(o, mat):
    o.data.materials.clear()
    o.data.materials.append(mat)
    for p in o.data.polygons:
        p.material_index = 0


def convert_image(src, dst, fmt="JPEG", quality=92, max_size=2048):
    if os.path.exists(dst):
        return
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im = bpy.data.images.load(src, check_existing=False)
    if max(im.size) > max_size:
        im.scale(max_size, max_size)
    arr = atlas.pixels(im).copy()
    bpy.data.images.remove(im)
    atlas.save_array(arr if fmt == "PNG" else arr[..., :3], dst, fmt, quality)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    cid, out_dir, fabric_dir = argv[0], argv[1], argv[2]
    save_blend = "--blend" in argv
    t0 = time.time()
    spec = specs.get(cid)
    models = os.path.join(out_dir, "Models")
    texdir = os.path.join(out_dir, "Textures")
    os.makedirs(models, exist_ok=True)
    os.makedirs(texdir, exist_ok=True)

    ct.clear_scene()
    full = dict(spec)
    clothes = [dict(c) for c in spec.get("clothes", [])]
    if spec.get("beard"):
        clothes.append(dict(asset=spec["beard"], hairlike="beard", z=40))
    if spec.get("glasses"):
        clothes.append(dict(asset=spec["glasses"], hairlike="glasses", z=200))
    full["clothes"] = clothes
    objs = ct.build_human(full)
    rig, body = objs["rig"], objs["body"]
    texinfo = {}
    for o in [objs["eyes"], objs["brows"], objs["lashes"], objs["hair"]] + objs["clothes"]:
        if o:
            texinfo[o.name] = ct.textures_of(o)
    cspec = {c["asset"]: c for c in clothes}
    hairlike = {}
    cloth = []
    for o in objs["clothes"]:
        c = cspec[o["asset"]]
        if c.get("hairlike"):
            hairlike[c["hairlike"]] = o
        else:
            cloth.append(o)

    src = ct.bake_body_shape(objs)
    lm = gm.body_landmarks(rig)
    waist_z = (lm["l_hip"].z + lm["r_hip"].z) * 0.5 + 0.10

    # ---------------- procedural garments
    gars = []
    G = spec.get("garments", [])
    coat = None
    if "coat" in G:
        coat = gm.lab_coat(src, rig, cloth, vale=spec.get("coat_vale", False))
        gars.append(coat)
    if "vest" in G:
        gars.append(gm.hivis_vest(src, rig, cloth, color=spec.get("vest_color", (1.0, 0.5, 0.05))))
    if "belt" in G:
        gars.append(gm.duty_belt(src, rig, cloth))
    if "badge" in G:
        gars.append(gm.chest_badge(src, rig, cloth))
    if "lanyard" in G:
        gars.append(gm.lanyard(src, rig, cloth + ([coat] if coat else [])))
    if "hardhat" in G:
        gars.append(gm.hard_hat(rig, [], color=spec.get("hat_color", (1.0, 0.78, 0.06))))

    # ---------------- layering: tuck inner clothes under coats / vests, then hidden surface removal
    for g in gars:
        if g.get("kind") in ("coat", "vest"):
            gm.tuck_under([o for o in cloth if int(o["z_depth"]) < 100], g, z_max=lm["neck"].z - 0.07)
    # ---------------- hidden surface removal
    layers = [(body, 0)] + [(o, int(o["z_depth"])) for o in cloth] + [(g, int(g["z_depth"])) for g in gars]
    ct.hidden_surface_removal(layers)
    hats = [o for o in cloth if int(o["z_depth"]) >= 100] + [g for g in gars if g.get("kind") == "hat"]
    if objs["hair"] and hats:
        hm = ct.hidden_mask(objs["hair"], hats, 0.05)
        ct.delete_faces_all_hidden(objs["hair"], hm)

    # ---------------- LOD0 budget: per-item decimation, then global
    # body: drop the (never visible) mouth interior, then decimate region by region so every visible part keeps
    # enough detail: hands -> HAND_BUDGET, head/neck -> HEAD_BUDGET (face front protected), rest proportionally.
    bt = ct.tri_count(body)
    remove_mouth_interior(body)
    region = body_regions(body)
    def region_tris(names):
        return sum(len(p.vertices) - 2 for p in body.data.polygons if region[p.vertices[0]] in names)
    for names, budget in ((("hand",), HAND_BUDGET), (("head", "face"), HEAD_BUDGET), (("limb",), LIMB_BUDGET)):
        cur = region_tris(names)
        if cur <= budget:
            continue
        total_now = ct.tri_count(body)
        weights = {"hand": 0.0, "head": 0.0, "face": 0.0, "limb": 0.0}
        for n in names:
            weights[n] = 1.0
        if "face" in names:
            weights["face"] = 0.55
        vg = body.vertex_groups.get("dec_pass") or body.vertex_groups.new(name="dec_pass")
        for v in body.data.vertices:
            vg.add([v.index], weights[region[v.index]], "REPLACE")
        decimate_group(body, (total_now - (cur - budget)) / total_now, "dec_pass", 30.0)
        region = body_regions(body)
    ct.log("body tris", bt, "->", ct.tri_count(body))
    for o in cloth:
        r = cspec[o["asset"]].get("decimate")
        if r:
            decimate(o, r)
        name = o["asset"].lower()
        cap = 1500 if any(k in name for k in ("shoe", "boot", "flat", "sneaker")) else 6000
        if ct.tri_count(o) > cap:
            decimate(o, cap / ct.tri_count(o))
    hair_parts = [objs["hair"], objs["brows"], objs["lashes"]] + list(hairlike.values())
    all_parts = [body, objs["eyes"]] + hair_parts + cloth + gars
    total = sum(ct.tri_count(o) for o in all_parts if o)
    ct.log("LOD0 tris before budget", total, {o.name: ct.tri_count(o) for o in all_parts if o})
    if total > LOD0_BUDGET:
        reducible = [o for o in cloth + [objs["hair"]] if o and ct.tri_count(o) > 800]  # garments were reduced at build
        red_tris = sum(ct.tri_count(o) for o in reducible)
        fixed = total - red_tris
        r = max(0.25, (LOD0_BUDGET - fixed) / max(1, red_tris))
        for o in reducible:
            decimate(o, r)
        total = sum(ct.tri_count(o) for o in all_parts if o)
        if total > LOD0_BUDGET:
            decimate(body, max(0.6, 1.0 - (total - LOD0_BUDGET) / ct.tri_count(body)), protect="dec_protect", factor=8.0)
    for o in all_parts:
        if o:
            ct.drop_vertex_groups(o)
            for p in o.data.polygons:
                p.use_smooth = True

    # ---------------- tint groups
    for o in cloth:
        c = cspec[o["asset"]]
        assign_tint_groups(o, c.get("tint") or c.get("split"), waist_z)
    for g in gars:
        assign_tint_groups(g, g.get("tint") or None, waist_z)

    # ---------------- skeleton: Unity names + T-pose
    bpy.data.objects.remove(src)
    meshes = [o for o in all_parts if o]
    ct.rename_bones(rig, meshes)
    ct.to_t_pose(rig, meshes)
    height = max((body.matrix_world @ v.co).z for v in body.data.vertices)
    target = ct.target_height(spec)
    ct.scale_character(rig, meshes, target / height)
    height = max((body.matrix_world @ v.co).z for v in body.data.vertices)

    # ---------------- atlases + materials
    cloth_objs = cloth + gars
    infos = [texinfo.get(o.name, {}) for o in cloth_objs]
    tints = atlas.bake_cloth_atlas(cloth_objs, infos, texdir, cid, fabric_dir)
    for c in clothes:
        if c.get("hairlike"):
            continue
        if c.get("color") and c.get("tint"):
            tints[c["tint"]] = [x / 0.55 for x in srgb_to_lin(c["color"])]
        for g in ("A", "B", "C"):
            if c.get("color_" + g):
                tints[g] = [x / 0.55 for x in srgb_to_lin(c["color_" + g])]
    hair_parts_map = {
        "hair": (objs["hair"], texinfo.get(objs["hair"].name, {}).get("diffuse") if objs["hair"] else None),
        "brows": (objs["brows"], texinfo.get(objs["brows"].name, {}).get("diffuse") if objs["brows"] else None),
        "lashes": (objs["lashes"], texinfo.get(objs["lashes"].name, {}).get("diffuse") if objs["lashes"] else None),
    }
    for kind, o in hairlike.items():
        hair_parts_map[kind] = (o, texinfo.get(o.name, {}).get("diffuse"))
    atlas.build_hair_atlas(hair_parts_map, texdir, cid, spec.get("hair_color", (0.1, 0.08, 0.06)))

    m_skin, m_eyes = simple_material("Skin", (0.8, 0.6, 0.5)), simple_material("Eyes", (1, 1, 1))
    m_hair, m_cloth = simple_material("Hair", (0.1, 0.08, 0.06)), simple_material("Cloth", (0.5, 0.5, 0.5))
    set_material(body, m_skin)
    if objs["eyes"]:
        set_material(objs["eyes"], m_eyes)
    for o in hair_parts:
        if o:
            set_material(o, m_hair)
    for o in cloth_objs:
        set_material(o, m_cloth)
    # only one UV map everywhere (FBX/Unity uv0)
    for o in meshes:
        for l in [l for l in o.data.uv_layers if l.name != "UVMap"]:
            o.data.uv_layers.remove(l)
        if o.data.uv_layers:
            o.data.uv_layers.active = o.data.uv_layers["UVMap"]

    # shared skin / eye textures
    sk = ct.skin_texture(spec["skin"])
    convert_image(sk["diffuse"], os.path.join(texdir, "Skins", spec["skin"] + ".jpg"), "JPEG", 92)
    convert_image(ct.eye_texture(spec.get("eyes", "brown")), os.path.join(texdir, "Eyes", spec.get("eyes", "brown") + ".png"), "PNG", max_size=1024)

    # ---------------- LODs
    lod_sets = []
    base = {"body": body, "eyes": objs["eyes"], "hair": objs["hair"], "brows": objs["brows"], "lashes": objs["lashes"]}
    base.update({"x_" + k: v for k, v in hairlike.items()})
    for i, o in enumerate(cloth_objs):
        base["cloth%02d" % i] = o
    T = {k: ct.tri_count(o) for k, o in base.items() if o}
    lods = [dict(base)]
    for li, target, keep in ((1, LOD1_TARGET, ("body", "eyes", "hair", "brows", "x_beard")), (2, LOD2_TARGET, ("body", "eyes", "hair"))):
        d = {}
        fixed = 0
        for k, o in base.items():
            if o is None:
                continue
            if not (k in keep or k.startswith("cloth")):
                continue
            d[k] = duplicate(o, "_L%d" % li)
        hair_r = 0.5 if li == 1 else 0.22
        if "hair" in d:
            decimate(d["hair"], hair_r)
        if "x_beard" in d:
            decimate(d["x_beard"], 0.5)
        fixed = sum(ct.tri_count(o) for k, o in d.items() if not (k == "body" or k.startswith("cloth")))
        flex = [o for k, o in d.items() if k == "body" or k.startswith("cloth")]
        ft = sum(ct.tri_count(o) for o in flex)
        r = max(0.04, (target - fixed) / max(1, ft))
        for o in flex:
            decimate(o, r)
        lods.append(d)
    finals = []
    for li, d in enumerate(lods):
        parts = [o for o in d.values() if o]
        j = join(parts, "%s_LOD%d" % (cid, li))
        ct.clean_weights(j)
        ct.ensure_armature(j, rig)
        finals.append(j)
    tris = [ct.tri_count(o) for o in finals]
    ct.log("LOD tris", tris)
    for o in finals:  # sanity: decimation / garment bugs show up as very long edges
        me = o.data
        long_edges = sum(1 for e in me.edges if (me.vertices[e.vertices[0]].co - me.vertices[e.vertices[1]].co).length > 0.3)
        if long_edges:
            ct.log("WARN", o.name, "has", long_edges, "edges longer than 0.3 m")

    # ---------------- export
    for o in bpy.data.objects:
        o.select_set(False)
    rig.select_set(True)
    for o in finals:
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx = os.path.join(models, cid + ".fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z",
                             axis_up="Y", use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=True,
                             use_armature_deform_only=True, add_leaf_bones=False, primary_bone_axis="Y",
                             secondary_bone_axis="X", bake_anim=False, path_mode="STRIP", embed_textures=False,
                             use_custom_props=False)
    assets = []
    for kind, name in (("skins", spec["skin"]), ("eyebrows", spec.get("brows")), ("eyelashes", spec.get("lashes")),
                       ("hair", spec.get("hair")), ("eyes", spec.get("eyes_mesh", "low-poly")),
                       ("material", "eyes " + spec.get("eyes", "brown"))):
        if name:
            assets.append({"kind": kind, "name": name})
    for c in clothes:
        assets.append({"kind": "clothes", "name": c["asset"]})
    meta = {
        "id": cid, "profession": spec["profession"], "names": spec.get("names", []), "unique": bool(spec.get("unique")),
        "gender": spec.get("gender", 0.5), "age": spec.get("age", 0.5), "height": round(height, 3),
        "skin": spec["skin"], "skinTint": list(spec.get("skin_tint", (1, 1, 1))), "eyes": spec.get("eyes", "brown"),
        "hairColor": list(spec.get("hair_color", (0.1, 0.08, 0.06))),
        "tints": tints, "palettes": spec.get("palettes", {}), "lodTris": tris, "assets": assets,
        "textures": {"skin": "Skins/%s.jpg" % spec["skin"], "eyes": "Eyes/%s.png" % spec.get("eyes", "brown"),
                     "clothA": cid + "_Cloth_A.jpg", "clothN": cid + "_Cloth_N.jpg", "clothM": cid + "_Cloth_M.png",
                     "hairA": cid + "_Hair_A.png"},
    }
    with open(os.path.join(models, cid + ".json"), "w") as fp:
        json.dump(meta, fp, indent=1)
    if save_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, cid + ".blend"))
    ct.log("DONE", cid, "tris", tris, "height %.3f" % height, "%.1fs" % (time.time() - t0))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback
        traceback.print_exc()
        sys.exit(1)
