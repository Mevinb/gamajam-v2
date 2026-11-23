"""
PARASITE character pipeline — Blender-side library (run inside Blender 4.5 with the MPFB 2 extension).

Stages used by make_character.py:
  build_human()          MPFB human (macros, skin, eyes, brows, lashes, hair, clothes, mixamo_unity rig)
  bake_body_shape()      apply MPFB shape keys, drop helper geometry and MPFB delete groups
  garments.*             procedural lab coat / hi-vis vest / duty belt / badge / lanyard / hard hat
  hidden_surface_removal delete geometry covered by outer layers (no poke-through, fewer triangles)
  rename_bones()         Unity Humanoid names (Hips, Spine, Chest, UpperChest, Neck, Head, LeftUpperArm, ...)
  to_t_pose()            arms horizontal, applied as rest pose (clean Unity avatar)
  atlas.*                bake clothes / hair atlases (Cycles) and tint masks
  make_lods()/export     LOD0/LOD1/LOD2 skinned meshes -> FBX
"""
import bpy, bmesh, math, os, json, re
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

MPFB_DATA = None  # resolved lazily


def log(*a):
    print("[chars]", *a, flush=True)


# ---------------------------------------------------------------- MPFB access

def mpfb_services():
    from bl_ext.user_default.mpfb.services.humanservice import HumanService
    from bl_ext.user_default.mpfb.services.locationservice import LocationService
    return HumanService, LocationService


def mpfb_user_data():
    global MPFB_DATA
    if MPFB_DATA is None:
        _, LocationService = mpfb_services()
        MPFB_DATA = LocationService.get_user_data()
    return MPFB_DATA


def asset_dir(kind, name):
    return os.path.join(mpfb_user_data(), kind, name)


def mhclo_meta(kind, name):
    """z_depth / author / license / name parsed from the .mhclo file."""
    d = asset_dir(kind, name)
    files = [f for f in os.listdir(d) if f.endswith(".mhclo")]
    meta = {"z_depth": 50, "author": "unknown", "license": "CC0", "name": name, "kind": kind}
    if not files:
        return meta
    with open(os.path.join(d, files[0]), encoding="utf8", errors="replace") as fp:
        for line in fp:
            p = line.strip().split(None, 1)
            if len(p) < 2:
                continue
            if p[0] == "z_depth":
                try: meta["z_depth"] = int(float(p[1]))
                except ValueError: pass
            elif p[0] == "author": meta["author"] = p[1]
            elif p[0] == "license": meta["license"] = p[1]
    meta["file"] = os.path.join(kind, name, files[0])
    return meta


def clear_scene():
    """Empty the startup scene without resetting preferences (factory reset would unload MPFB)."""
    mpfb_services()
    for coll in (bpy.data.objects, bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.cameras,
                 bpy.data.lights, bpy.data.images):
        for x in list(coll):
            coll.remove(x)


def mhclo_rel(kind, name):
    d = asset_dir(kind, name)
    files = [f for f in os.listdir(d) if f.endswith(".mhclo")]
    if not files:
        raise IOError("no mhclo in " + d)
    return name + "/" + files[0]


def build_human(spec):
    HumanService, _ = mpfb_services()
    info = HumanService._create_default_human_info_dict()
    ph = info["phenotype"]
    for k in ("gender", "age", "muscle", "weight", "height", "proportions", "cupsize", "firmness"):
        if k in spec:
            ph[k] = spec[k]

    race = spec.get("race", {"caucasian": 1.0})
    tot = sum(race.values()) or 1.0
    ph["race"] = {r: race.get(r, 0.0) / tot for r in ("asian", "caucasian", "african")}
    info["rig"] = "mixamo_unity"
    info["eyes"] = mhclo_rel("eyes", spec.get("eyes_mesh", "low-poly"))
    if spec.get("brows"): info["eyebrows"] = mhclo_rel("eyebrows", spec["brows"])
    if spec.get("lashes"): info["eyelashes"] = mhclo_rel("eyelashes", spec["lashes"])
    if spec.get("hair"): info["hair"] = mhclo_rel("hair", spec["hair"])
    info["clothes"] = [mhclo_rel("clothes", c["asset"]) for c in spec.get("clothes", [])]
    info["skin_mhmat"] = spec["skin"] + "/" + [f for f in os.listdir(asset_dir("skins", spec["skin"])) if f.endswith(".mhmat")][0]
    info["skin_material_type"] = "MAKESKIN"
    info["name"] = spec["id"]
    if spec.get("targets"):
        info["targets"] = [{"target": t, "value": v} for t, v in spec["targets"].items()]
    s = HumanService.get_default_deserialization_settings()
    s["subdiv_levels"] = 0
    body = HumanService.deserialize_from_dict(info, s)
    objs = {"body": body, "rig": body.parent, "eyes": None, "brows": None, "lashes": None, "hair": None, "clothes": []}
    for o in list(bpy.data.objects):
        if o.type != "MESH" or o == body:
            continue
        t = o.get("MPFB_GEN_object_type", "")
        src = o.get("MPFB_GEN_asset_source", "")
        if t == "Eyes": objs["eyes"] = o
        elif t == "Eyebrows": objs["brows"] = o
        elif t == "Eyelashes": objs["lashes"] = o
        elif t == "Hair": objs["hair"] = o
        elif t in ("Teeth", "Tongue"): bpy.data.objects.remove(o)
        elif t == "Clothes":
            o["asset"] = src.split("/")[0]
            objs["clothes"].append(o)
    # keep spec order for clothes
    order = [c["asset"] for c in spec.get("clothes", [])]
    objs["clothes"].sort(key=lambda o: order.index(o["asset"]) if o["asset"] in order else 99)
    for o in objs["clothes"]:
        c = spec["clothes"][order.index(o["asset"])]
        meta = mhclo_meta("clothes", o["asset"])
        o["z_depth"] = c.get("z", meta["z_depth"])
    return objs


# ---------------------------------------------------------------- mesh helpers

def set_active(o):
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    for x in bpy.context.selected_objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def remove_modifiers(o, keep_types=("ARMATURE",)):
    for m in list(o.modifiers):
        if m.type not in keep_types:
            o.modifiers.remove(m)


def apply_shape_keys(o):
    if o.data.shape_keys:
        set_active(o)
        bpy.ops.object.shape_key_remove(all=True, apply_mix=True)


def group_mask(o, group_name, threshold=0.5):
    vg = o.vertex_groups.get(group_name)
    res = [False] * len(o.data.vertices)
    if not vg:
        return res
    gi = vg.index
    for v in o.data.vertices:
        for g in v.groups:
            if g.group == gi and g.weight >= threshold:
                res[v.index] = True
                break
    return res


def delete_verts(o, mask):
    """Delete vertices where mask[i] is True."""
    if not any(mask):
        return
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if mask[v.index]], context="VERTS")
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


def delete_faces_all_hidden(o, hidden, min_hidden_fraction=1.0):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    kill = []
    for f in bm.faces:
        n = sum(1 for v in f.verts if hidden[v.index])
        if n >= len(f.verts) * min_hidden_fraction - 1e-6:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    return len(kill)


def drop_vertex_groups(o, keep_prefix="mixamorig:"):
    for vg in list(o.vertex_groups):
        if not vg.name.startswith(keep_prefix):
            o.vertex_groups.remove(vg)


def bake_body_shape(objs, full_copy_name="WeightSource"):
    """Apply targets, drop helpers. Returns a full-body copy (with weights, before delete groups) used as weight source."""
    body = objs["body"]
    apply_shape_keys(body)
    remove_modifiers(body)
    helpers = [not b for b in group_mask(body, "body")]
    delete_verts(body, helpers)
    src = body.copy()
    src.data = body.data.copy()
    src.name = full_copy_name
    bpy.context.collection.objects.link(src)
    src.modifiers.clear()
    src.parent = None
    src.matrix_world = body.matrix_world.copy()
    src.hide_render = True
    # MPFB delete groups (body parts covered by clothes / shoes)
    kill = [False] * len(body.data.vertices)
    for o in objs["clothes"]:
        dg = o.get("MPFB_MC_delete_group")
        if dg:
            m = group_mask(body, dg)
            kill = [a or b for a, b in zip(kill, m)]
    delete_verts(body, kill)
    for o in [body, objs["eyes"], objs["brows"], objs["lashes"], objs["hair"]] + objs["clothes"]:
        if o:
            remove_modifiers(o)
            apply_shape_keys(o)
    return src


def evaluated_world_mesh_bvh(objs):
    """BVH of evaluated meshes in world space."""
    verts, polys = [], []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        off = len(verts)
        mw = o.matrix_world
        verts.extend([mw @ v.co for v in me.vertices])
        polys.extend([[off + i for i in p.vertices] for p in me.polygons])
        e.to_mesh_clear()
    if not polys:
        return None
    return BVHTree.FromPolygons(verts, polys, all_triangles=False, epsilon=0.0)


def _ray_dirs(n):
    n = n.normalized()
    a = Vector((1, 0, 0)) if abs(n.x) < 0.9 else Vector((0, 1, 0))
    t1 = n.cross(a).normalized()
    t2 = n.cross(t1).normalized()
    dirs = [n]
    for t in (t1, -t1, t2, -t2):
        dirs.append((n + t * 0.75).normalized())
    return dirs


def hidden_mask(inner, outers, max_dist=0.045, need_all=True):
    bvh = evaluated_world_mesh_bvh(outers)
    if bvh is None:
        return [False] * len(inner.data.vertices)
    mw = inner.matrix_world
    nm = mw.to_3x3().inverted().transposed()
    res = []
    for v in inner.data.vertices:
        co = mw @ v.co
        n = (nm @ v.normal).normalized()
        dirs = _ray_dirs(n)
        hits = 0
        for d in dirs:
            loc, nor, idx, dist = bvh.ray_cast(co + d * 0.0008, d, max_dist)
            if loc is not None:
                hits += 1
        res.append(hits == len(dirs) if need_all else hits >= len(dirs) - 1)
    return res


def hidden_surface_removal(layers, max_dist=0.045):
    """layers: list of (obj, z_depth). Removes faces of an object fully covered by higher-z objects."""
    layers = [(o, z) for o, z in layers if o is not None]
    total = 0
    for o, z in layers:
        outers = [p for p, pz in layers if pz > z and p is not o]
        if not outers:
            continue
        hm = hidden_mask(o, outers, max_dist)
        n = delete_faces_all_hidden(o, hm)
        total += n
        log("HSR", o.name, "removed", n, "faces; outers:", [p.name for p in outers])
    return total


def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


# ---------------------------------------------------------------- weights

def transfer_weights(target, source, max_influences=4):
    """Nearest-face-interpolated weight transfer from source (body copy with mixamorig groups) to target."""
    set_active(target)
    for vg in list(target.vertex_groups):
        target.vertex_groups.remove(vg)
    for vg in source.vertex_groups:
        if vg.name.startswith("mixamorig:"):
            target.vertex_groups.new(name=vg.name)
    m = target.modifiers.new("WT", "DATA_TRANSFER")
    m.object = source
    m.use_vert_data = True
    m.data_types_verts = {"VGROUP_WEIGHTS"}
    m.vert_mapping = "POLYINTERP_NEAREST"
    m.layers_vgroup_select_src = "ALL"
    m.layers_vgroup_select_dst = "NAME"
    bpy.ops.object.modifier_apply(modifier=m.name)


def clean_weights(o, limit=4):
    set_active(o)
    if not o.vertex_groups:
        return
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT") if False else None
    bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.01)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=limit)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)


def ensure_armature(o, rig):
    o.parent = rig
    o.matrix_parent_inverse = Matrix.Identity(4)
    for m in list(o.modifiers):
        if m.type == "ARMATURE":
            o.modifiers.remove(m)
    m = o.modifiers.new("Armature", "ARMATURE")
    m.object = rig


# ---------------------------------------------------------------- skeleton

BONE_MAP = {
    "Root": "Root", "Hips": "Hips", "Spine": "Spine", "Spine1": "Chest", "Spine2": "UpperChest",
    "Neck": "Neck", "Head": "Head", "Jaw": "Jaw", "LeftEye": "LeftEye", "RightEye": "RightEye",
}
for _s in ("Left", "Right"):
    BONE_MAP.update({
        _s + "Shoulder": _s + "Shoulder", _s + "Arm": _s + "UpperArm", _s + "ForeArm": _s + "LowerArm", _s + "Hand": _s + "Hand",
        _s + "UpLeg": _s + "UpperLeg", _s + "Leg": _s + "LowerLeg", _s + "Foot": _s + "Foot", _s + "ToeBase": _s + "Toes",
    })
    for _f, _u in (("Thumb", "Thumb"), ("Index", "Index"), ("Middle", "Middle"), ("Ring", "Ring"), ("Pinky", "Little")):
        for _i, _seg in ((1, "Proximal"), (2, "Intermediate"), (3, "Distal")):
            BONE_MAP[f"{_s}Hand{_f}{_i}"] = f"{_s}{_u}{_seg}"

MERGE_INTO_PARENT = ("LeftBreast", "RightBreast", "LeftButtock", "RightButtock",
                     "LeftOrbicularisTop", "RightOrbicularisTop", "LeftOrbicularisBottom", "RightOrbicularisBottom")


def merge_bone_weights(meshes, src, dst):
    for o in meshes:
        s = o.vertex_groups.get(src)
        if not s:
            continue
        d = o.vertex_groups.get(dst) or o.vertex_groups.new(name=dst)
        si, = [s.index]
        for v in o.data.vertices:
            w = 0.0
            for g in v.groups:
                if g.group == si:
                    w = g.weight
            if w > 0:
                cur = 0.0
                for g in v.groups:
                    if g.group == d.index:
                        cur = g.weight
                d.add([v.index], cur + w, "REPLACE")
        o.vertex_groups.remove(s)


def rename_bones(rig, meshes):
    pre = "mixamorig:"
    # merge helper bones into their parents first
    for b in MERGE_INTO_PARENT:
        bone = rig.data.bones.get(pre + b)
        if bone:
            merge_bone_weights(meshes, bone.name, bone.parent.name)
    set_active(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for b in MERGE_INTO_PARENT:
        eb = rig.data.edit_bones.get(pre + b)
        if eb:
            rig.data.edit_bones.remove(eb)
    bpy.ops.object.mode_set(mode="OBJECT")
    for bone in rig.data.bones:
        short = bone.name[len(pre):] if bone.name.startswith(pre) else bone.name
        new = BONE_MAP.get(short)
        if new is None:
            log("WARN unmapped bone", bone.name)
            continue
        old = bone.name
        bone.name = new
        for o in meshes:  # bpy bone rename does not always follow for unparented meshes
            vg = o.vertex_groups.get(old)
            if vg:
                vg.name = new
    rig.name = "Armature"
    rig.data.name = "Armature"


def to_t_pose(rig, meshes):
    """Rotate arms horizontal and apply as rest pose (meshes baked to match)."""
    set_active(rig)
    bpy.ops.object.mode_set(mode="POSE")
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        for b in (side + "UpperArm", side + "LowerArm", side + "Hand"):
            bpy.context.view_layer.update()
            pb = rig.pose.bones[b]
            head, tail = pb.head.copy(), pb.tail.copy()
            cur = (tail - head).normalized()
            tgt = Vector((sign, 0.0, 0.0))
            q = cur.rotation_difference(tgt)
            M = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head)
            pb.matrix = M @ pb.matrix
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in meshes:
        set_active(o)
        mod = None
        for m in o.modifiers:
            if m.type == "ARMATURE":
                mod = m
        if mod is None:
            mod = o.modifiers.new("Armature", "ARMATURE")
            mod.object = rig
        bpy.ops.object.modifier_apply(modifier=mod.name)
    set_active(rig)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in meshes:
        ensure_armature(o, rig)


def bone_head(rig, name):
    return rig.matrix_world @ rig.data.bones[name].head_local


def bone_tail(rig, name):
    return rig.matrix_world @ rig.data.bones[name].tail_local


# ---------------------------------------------------------------- materials (for baking / preview)

def image(path):
    path = os.path.abspath(path)
    for im in bpy.data.images:
        if im.filepath and os.path.abspath(bpy.path.abspath(im.filepath)) == path:
            return im
    return bpy.data.images.load(path)


def textures_of(o):
    """{'diffuse': path, 'normal': path, 'ao': path} from an MPFB MakeSkin material."""
    res = {}
    if not o.material_slots or not o.material_slots[0].material:
        return res
    m = o.material_slots[0].material
    if not m.node_tree:
        return res
    for n in m.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            p = os.path.abspath(bpy.path.abspath(n.image.filepath))
            if n.name == "diffuseTexture": res["diffuse"] = p
            elif n.name == "normalmapTexture": res["normal"] = p
            elif n.name == "aomapTexture": res["ao"] = p
            elif n.name == "bumpmapTexture": res["bump"] = p
    return res


def skin_texture(skin_name):
    d = asset_dir("skins", skin_name)
    mh = [f for f in os.listdir(d) if f.endswith(".mhmat")][0]
    res = {}
    with open(os.path.join(d, mh), encoding="utf8", errors="replace") as fp:
        for line in fp:
            p = line.strip().split(None, 1)
            if len(p) == 2 and p[0] in ("diffuseTexture", "normalmapTexture"):
                res["diffuse" if p[0] == "diffuseTexture" else "normal"] = os.path.join(d, p[1])
    return res


def eye_texture(color):
    return os.path.join(mpfb_user_data(), "eyes", "materials", color + "_eye.png")


def target_height(spec):
    """Standing height in metres (top of head): explicit height_m, else from the relative height macro."""
    if spec.get("height_m"):
        return float(spec["height_m"])
    base = 1.76 if spec.get("gender", 0.5) >= 0.5 else 1.63
    return base + (float(spec.get("height", 0.5)) - 0.5) * 0.30


def scale_character(rig, meshes, s):
    """Uniformly scale rest skeleton + meshes about the world origin (feet stay on the ground)."""
    if abs(s - 1.0) < 1e-4:
        return
    assert (rig.matrix_world.to_translation().length < 1e-4), "rig expected at origin"
    S = Matrix.Scale(s, 4)
    for o in meshes:
        o.data.transform(o.matrix_world.inverted() @ S @ o.matrix_world)
        o.data.update()
    set_active(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in rig.data.edit_bones:
        eb.head = eb.head * s
        eb.tail = eb.tail * s
    bpy.ops.object.mode_set(mode="OBJECT")
