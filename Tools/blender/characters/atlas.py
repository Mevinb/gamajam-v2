"""
Texture atlases for a character (Blender python, Cycles bake on the GPU when available).

Cloth atlas (all clothing + procedural garments, one material):
  <id>_Cloth_A.jpg  albedo (sRGB). Regions with a tint group are stored as normalised grey; the default colour is
                    written to the metadata so the runtime shader reproduces the original look (and can re-tint).
  <id>_Cloth_N.jpg  tangent-space normal map (MikkTSpace, AtlasUV)
  <id>_Cloth_M.png  R = tint group (0 none, 1/3 A, 2/3 B, 1 C), G = metallic, B = smoothness
Hair atlas (hair, brows, lashes, beard, glasses frames; alpha-clipped):
  <id>_Hair_A.png   RGBA, composited (no bake) in a fixed layout: hair top 3/4, bottom row = 4 cells
"""
import bpy, os, math
import numpy as np
import chartools as ct

LUMA = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def setup_cycles(samples=1):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for dev_type in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = dev_type
                prefs.get_devices()
                ok = False
                for d in prefs.devices:
                    d.use = d.type == dev_type
                    ok = ok or d.use
                if ok:
                    sc.cycles.device = "GPU"
                    break
            except Exception:
                continue
    except Exception as e:
        ct.log("GPU setup failed", e)
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.bake.margin = 6
    sc.render.bake.margin_type = "EXTEND"


def new_image(name, size, data=False, alpha=False):
    im = bpy.data.images.get(name)
    if im:
        bpy.data.images.remove(im)
    w, h = (size, size) if isinstance(size, int) else size
    im = bpy.data.images.new(name, w, h, alpha=alpha, float_buffer=data)
    im.colorspace_settings.name = "Non-Color" if data else "sRGB"
    return im


def pixels(im):
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def save_array(arr, path, fmt="PNG", quality=92, colorspace="sRGB"):
    h, w = arr.shape[:2]
    name = os.path.basename(path)
    im = bpy.data.images.new(name, w, h, alpha=arr.shape[2] == 4)
    im.colorspace_settings.name = colorspace
    rgba = arr if arr.shape[2] == 4 else np.concatenate([arr, np.ones((h, w, 1), np.float32)], axis=2)
    im.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    sc = bpy.context.scene
    st = sc.render.image_settings
    st.file_format = fmt
    st.color_mode = "RGBA" if (arr.shape[2] == 4 and fmt == "PNG") else "RGB"
    st.color_depth = "8"
    if fmt == "JPEG":
        st.quality = quality
    if fmt == "PNG":
        st.compression = 90
    st.color_management = "OVERRIDE"
    st.view_settings.view_transform = "Standard"
    st.view_settings.look = "None"
    st.display_settings.display_device = "sRGB"
    im.save_render(path, scene=sc)
    bpy.data.images.remove(im)


# ---------------------------------------------------------------- bake materials

def _uvnode(nt, name="UVMap"):
    n = nt.nodes.new("ShaderNodeUVMap")
    n.uv_map = name
    return n


def build_bake_material(o, info, fabric_dir):
    """info: dict with keys diffuse/normal/ao (MPFB textures) or garment props on the object."""
    m = bpy.data.materials.new(o.name + "_bake")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    uv = _uvnode(nt)
    color_socket = None
    normal_socket = None
    is_garment = bool(o.get("kind"))
    if not is_garment:
        if info.get("diffuse"):
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = ct.image(info["diffuse"])
            nt.links.new(uv.outputs[0], t.inputs[0])
            color_socket = t.outputs[0]
            if info.get("ao"):
                a = nt.nodes.new("ShaderNodeTexImage")
                a.image = ct.image(info["ao"])
                a.image.colorspace_settings.name = "Non-Color"
                nt.links.new(uv.outputs[0], a.inputs[0])
                mix = nt.nodes.new("ShaderNodeMix")
                mix.data_type = "RGBA"
                mix.blend_type = "MULTIPLY"
                mix.inputs[0].default_value = 0.6
                nt.links.new(color_socket, mix.inputs[6])
                nt.links.new(a.outputs[0], mix.inputs[7])
                color_socket = mix.outputs[2]
        if info.get("normal"):
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = ct.image(info["normal"])
            t.image.colorspace_settings.name = "Non-Color"
            nt.links.new(uv.outputs[0], t.inputs[0])
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nm.uv_map = "UVMap"
            nt.links.new(t.outputs[0], nm.inputs[1])
            normal_socket = nm.outputs[0]
        rgb = nt.nodes.new("ShaderNodeRGB")
        c = info.get("multiply", (1, 1, 1))
        rgb.outputs[0].default_value = (c[0], c[1], c[2], 1)
        if color_socket is None:
            color_socket = rgb.outputs[0]
        else:
            mul = nt.nodes.new("ShaderNodeMix")
            mul.data_type = "RGBA"
            mul.blend_type = "MULTIPLY"
            mul.inputs[0].default_value = 1.0
            nt.links.new(color_socket, mul.inputs[6])
            nt.links.new(rgb.outputs[0], mul.inputs[7])
            color_socket = mul.outputs[2]
    else:
        base = o.get("base_color", [1, 1, 1])
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (base[0], base[1], base[2], 1)
        color_socket = rgb.outputs[0]
        fabric = o.get("fabric", "")
        scale = 1.0 / max(0.01, float(o.get("fabric_scale", 0.2)))
        mapn = nt.nodes.new("ShaderNodeMapping")
        mapn.inputs["Scale"].default_value = (scale, scale, scale)
        nt.links.new(uv.outputs[0], mapn.inputs[0])
        if fabric:
            alb, nor = fabric_paths(fabric_dir, fabric)
            if alb:
                t = nt.nodes.new("ShaderNodeTexImage")
                t.image = ct.image(alb)
                nt.links.new(mapn.outputs[0], t.inputs[0])
                # normalise fabric luminance around 1 so base_color is preserved
                bw = nt.nodes.new("ShaderNodeRGBToBW")
                nt.links.new(t.outputs[0], bw.inputs[0])
                mavg = nt.nodes.new("ShaderNodeMath")
                mavg.operation = "MULTIPLY"
                mavg.inputs[1].default_value = 1.0 / max(0.05, image_mean_luma(t.image))
                nt.links.new(bw.outputs[0], mavg.inputs[0])
                cl = nt.nodes.new("ShaderNodeMath")
                cl.operation = "MULTIPLY_ADD"  # 0.35*x + 0.65 -> gentle weave contrast
                cl.inputs[1].default_value = 0.35
                cl.inputs[2].default_value = 0.65
                nt.links.new(mavg.outputs[0], cl.inputs[0])
                mul = nt.nodes.new("ShaderNodeMix")
                mul.data_type = "RGBA"
                mul.blend_type = "MULTIPLY"
                mul.inputs[0].default_value = 1.0
                nt.links.new(color_socket, mul.inputs[6])
                nt.links.new(cl.outputs[0], mul.inputs[7])
                color_socket = mul.outputs[2]
            if nor:
                t = nt.nodes.new("ShaderNodeTexImage")
                t.image = ct.image(nor)
                t.image.colorspace_settings.name = "Non-Color"
                nt.links.new(mapn.outputs[0], t.inputs[0])
                nm = nt.nodes.new("ShaderNodeNormalMap")
                nm.uv_map = "UVMap"
                nm.inputs[0].default_value = 0.6
                nt.links.new(t.outputs[0], nm.inputs[1])
                normal_socket = nm.outputs[0]
        bump_s = float(o.get("bump", 0.0))
        if bump_s > 0:
            noise = nt.nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = 9.0
            noise.inputs["Detail"].default_value = 3.0
            nt.links.new(uv.outputs[0], noise.inputs[0])
            bump = nt.nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value = bump_s
            bump.inputs["Distance"].default_value = 0.02
            nt.links.new(noise.outputs[0], bump.inputs["Height"])
            if normal_socket is not None:
                nt.links.new(normal_socket, bump.inputs["Normal"])
            normal_socket = bump.outputs[0]
        det = o.get("detail_image", "")
        dom = list(o.get("detail_domain", []))
        if det and len(dom) == 4 and bpy.data.images.get(det):
            u0, u1, v0, v1 = dom
            mp = nt.nodes.new("ShaderNodeMapping")
            mp.inputs["Location"].default_value = (-u0 / (u1 - u0), -v0 / (v1 - v0), 0)
            mp.inputs["Scale"].default_value = (1 / (u1 - u0), 1 / (v1 - v0), 1)
            nt.links.new(uv.outputs[0], mp.inputs[0])
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = bpy.data.images[det]
            t.image.colorspace_settings.name = "Non-Color"
            t.extension = "CLIP"
            nt.links.new(mp.outputs[0], t.inputs[0])
            # CLIP gives black+alpha 0 outside; mix to white there
            mixw = nt.nodes.new("ShaderNodeMix")
            mixw.data_type = "RGBA"
            mixw.inputs[6].default_value = (1, 1, 1, 1)
            nt.links.new(t.outputs[1], mixw.inputs[0])
            nt.links.new(t.outputs[0], mixw.inputs[7])
            mul = nt.nodes.new("ShaderNodeMix")
            mul.data_type = "RGBA"
            mul.blend_type = "MULTIPLY"
            mul.inputs[0].default_value = 1.0
            nt.links.new(color_socket, mul.inputs[6])
            nt.links.new(mixw.outputs[2], mul.inputs[7])
            color_socket = mul.outputs[2]
    nt.links.new(color_socket, bsdf.inputs["Base Color"])
    if normal_socket is not None:
        nt.links.new(normal_socket, bsdf.inputs["Normal"])
    nt.links.new(color_socket, emit.inputs[0])
    m["color_out"] = "emit"
    o.data.materials.clear()
    o.data.materials.append(m)
    return m


def fabric_paths(fabric_dir, fabric):
    names = {
        "stretch_poplin": ("stretch_poplin_grey.jpg", "stretch_poplin_normal.jpg"),
        "cotton_jersey": ("cotton_jersey_grey.jpg", "cotton_jersey_normal.jpg"),
        "jersey_melange": ("jersey_melange_grey.jpg", "jersey_melange_normal.jpg"),
        "caban": ("caban_grey.jpg", "caban_normal.jpg"),
        "denim": ("denim_fabric_albedo.jpg", "denim_fabric_normal.jpg"),
        "brown_leather": ("brown_leather_albedo.jpg", "brown_leather_normal.jpg"),
    }.get(fabric)
    if not names:
        return None, None
    a, n = (os.path.join(fabric_dir, x) for x in names)
    return (a if os.path.exists(a) else None), (n if os.path.exists(n) else None)


_luma_cache = {}


def image_mean_luma(im):
    if im.name in _luma_cache:
        return _luma_cache[im.name]
    px = pixels(im)
    lin = srgb_to_lin(px[..., :3]) if im.colorspace_settings.name == "sRGB" else px[..., :3]
    v = float((lin @ LUMA).mean())
    _luma_cache[im.name] = v
    return v


def link_output(m, mode, value=None, attr=None):
    """mode: 'emit' (colour), 'value' (constant), 'attr' (face attribute), 'bsdf' (normal bake)."""
    nt = m.node_tree
    out = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
    emit = [n for n in nt.nodes if n.type == "EMISSION"][0]
    bsdf = [n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"][0]
    for l in list(out.inputs[0].links):
        nt.links.remove(l)
    tmp = nt.nodes.get("__val")
    if tmp: nt.nodes.remove(tmp)
    tmp = nt.nodes.get("__emit2")
    if tmp: nt.nodes.remove(tmp)
    if mode == "emit":
        nt.links.new(emit.outputs[0], out.inputs[0])
    elif mode == "bsdf":
        nt.links.new(bsdf.outputs[0], out.inputs[0])
    else:
        e2 = nt.nodes.new("ShaderNodeEmission")
        e2.name = "__emit2"
        if mode == "value":
            e2.inputs[0].default_value = (value, value, value, 1)
        elif mode == "attr":
            a = nt.nodes.new("ShaderNodeAttribute")
            a.name = "__val"
            a.attribute_type = "GEOMETRY"
            a.attribute_name = attr
            nt.links.new(a.outputs["Fac"], e2.inputs[0])
        nt.links.new(e2.outputs[0], out.inputs[0])


def set_bake_target(objs, im):
    for o in objs:
        m = o.data.materials[0]
        nt = m.node_tree
        n = nt.nodes.get("__target") or nt.nodes.new("ShaderNodeTexImage")
        n.name = "__target"
        n.image = im
        nt.nodes.active = n


def do_bake(objs, kind):
    ct.set_active(objs[0])
    for o in objs:
        o.select_set(True)
    if kind == "NORMAL":
        bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", use_clear=True, margin=6)
    else:
        bpy.ops.object.bake(type="EMIT", use_clear=True, margin=6)


def pack_atlas_uv(objs, margin=0.006):
    for o in objs:
        me = o.data
        src = me.uv_layers.get("UVMap") or (me.uv_layers[0] if me.uv_layers else None)
        if src is None:
            raise RuntimeError("object without UVs: " + o.name)
        if src.name != "UVMap":
            src.name = "UVMap"
        at = me.uv_layers.get("AtlasUV") or me.uv_layers.new(name="AtlasUV")
        n = len(me.loops)
        buf = np.empty(n * 2, dtype=np.float32)
        me.uv_layers["UVMap"].data.foreach_get("uv", buf)
        at.data.foreach_set("uv", buf)
        me.uv_layers.active = at
        at.active_render = True
    ct.set_active(objs[0])
    for o in objs:
        o.select_set(True)
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.reveal()
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    # garments with importance weights: scale islands per object (face of coat etc. stay sharp)
    bpy.ops.uv.pack_islands(rotate=True, margin=margin, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")


def scale_object_uv_importance(objs):
    """Pre-scale each object's AtlasUV around its centre by its 'uv_weight' (default 1) before packing."""
    for o in objs:
        w = float(o.get("uv_weight", 1.0))
        if abs(w - 1.0) < 1e-3:
            continue
        me = o.data
        at = me.uv_layers["AtlasUV"]
        n = len(me.loops)
        buf = np.empty(n * 2, dtype=np.float32)
        at.data.foreach_get("uv", buf)
        buf *= math.sqrt(w)
        at.data.foreach_set("uv", buf)


def bake_cloth_atlas(objs, infos, out_dir, char_id, fabric_dir, size=2048, mask_size=1024):
    """objs: cloth objects; infos: per-object dict (textures, tint rules resolved into face attr 'tint_group')."""
    setup_cycles(1)
    for o, info in zip(objs, infos):
        build_bake_material(o, info, fabric_dir)
    pack_atlas_uv(objs)
    # albedo
    im = new_image(char_id + "_alb", size)
    set_bake_target(objs, im)
    for o in objs: link_output(o.data.materials[0], "emit")
    do_bake(objs, "EMIT")
    alb = pixels(im).copy()
    # smoothness / metallic / tint group
    dat = new_image(char_id + "_dat", mask_size, data=True)
    set_bake_target(objs, dat)
    for o in objs: link_output(o.data.materials[0], "value", value=float(o.get("smoothness", 0.25)))
    do_bake(objs, "EMIT")
    smooth = pixels(dat)[..., 0].copy()
    for o in objs: link_output(o.data.materials[0], "value", value=float(o.get("metallic", 0.0)))
    do_bake(objs, "EMIT")
    metal = pixels(dat)[..., 0].copy()
    for o in objs: link_output(o.data.materials[0], "attr", attr="tint_group")
    do_bake(objs, "EMIT")
    group = pixels(dat)[..., 0].copy()
    # normal
    bpy.context.scene.cycles.samples = 2
    nim = new_image(char_id + "_nrm", size, data=True)
    set_bake_target(objs, nim)
    for o in objs: link_output(o.data.materials[0], "bsdf")
    do_bake(objs, "NORMAL")
    nrm = pixels(nim).copy()
    # ---- tint normalisation (work in linear)
    lin = srgb_to_lin(alb[..., :3])
    gi_small = np.clip(np.rint(group * 3), 0, 3).astype(np.int32)
    # upsample group to albedo res (nearest)
    rep = size // mask_size
    gi = np.repeat(np.repeat(gi_small, rep, axis=0), rep, axis=1)
    tints = {}
    for g, key in ((1, "A"), (2, "B"), (3, "C")):
        sel = gi == g
        if sel.sum() < 16:
            continue
        mean = lin[sel].mean(axis=0)
        lum = lin[sel] @ LUMA
        ml = float(max(1e-4, (mean @ LUMA)))
        target = 0.55
        grey = np.clip(lum / ml * target, 0, 1)
        lin[sel] = grey[:, None] * np.ones(3, np.float32)
        tints[key] = [float(x) for x in (mean / target)]
    alb_out = lin_to_srgb(lin)
    # flip vertically: blender pixel rows start at bottom; save_render handles orientation itself
    save_array(alb_out.astype(np.float32), os.path.join(out_dir, char_id + "_Cloth_A.jpg"), "JPEG", 92)
    save_array(nrm[..., :3], os.path.join(out_dir, char_id + "_Cloth_N.jpg"), "JPEG", 95, colorspace="Non-Color")
    mask = np.stack([group, metal, smooth], axis=-1).astype(np.float32)
    save_array(mask, os.path.join(out_dir, char_id + "_Cloth_M.png"), "PNG", colorspace="Non-Color")
    for o in objs:
        me = o.data
        me.uv_layers.remove(me.uv_layers["UVMap"])
        me.uv_layers["AtlasUV"].name = "UVMap"
    return tints


# ---------------------------------------------------------------- hair atlas (composite)

HAIR_LAYOUT = {
    "hair": (0.0, 0.25, 1.0, 1.0),
    "brows": (0.0, 0.0, 0.25, 0.25),
    "lashes": (0.25, 0.0, 0.5, 0.25),
    "beard": (0.5, 0.0, 0.75, 0.25),
    "glasses": (0.75, 0.0, 1.0, 0.25),
}


def load_rgba(path, w, h):
    im = bpy.data.images.load(path, check_existing=False)
    im.colorspace_settings.name = "sRGB"
    if im.size[0] != w or im.size[1] != h:
        im.scale(w, h)
    a = pixels(im).copy()
    bpy.data.images.remove(im)
    return a


def recolor(rgba, color, strength=1.0, keep_dark=False):
    """Recolour a hair texture to 'color' (sRGB) keeping its luminance structure."""
    lin = srgb_to_lin(rgba[..., :3])
    lum = lin @ LUMA
    a = rgba[..., 3]
    sel = a > 0.3
    ml = float(lum[sel].mean()) if sel.any() else 0.2
    c = srgb_to_lin(np.array(color[:3], np.float32))
    cl = float(c @ LUMA) + 1e-4
    if keep_dark:
        out = lum[..., None] * (c / cl) * 0.35
    else:
        out = (lum / max(1e-4, ml))[..., None] * c
    out = lin * (1 - strength) + out * strength
    return np.concatenate([lin_to_srgb(out), a[..., None]], axis=-1)


def remap_uv(o, rect):
    x0, y0, x1, y1 = rect
    me = o.data
    uvl = me.uv_layers.get("UVMap") or me.uv_layers[0]
    uvl.name = "UVMap"
    n = len(me.loops)
    buf = np.empty(n * 2, dtype=np.float32)
    uvl.data.foreach_get("uv", buf)
    u = buf[0::2]
    v = buf[1::2]
    if u.min() < -0.01 or u.max() > 1.01 or v.min() < -0.01 or v.max() > 1.01:
        ct.log("WARN uv outside 0..1 on", o.name, u.min(), u.max(), v.min(), v.max())
    u = np.clip(u, 0, 1)
    v = np.clip(v, 0, 1)
    buf[0::2] = x0 + u * (x1 - x0)
    buf[1::2] = y0 + v * (y1 - y0)
    uvl.data.foreach_set("uv", buf)
    for extra in [l for l in me.uv_layers if l.name != "UVMap"]:
        me.uv_layers.remove(extra)


def build_hair_atlas(parts, out_dir, char_id, hair_color, size=2048):
    """parts: dict kind -> (obj, texture path). Writes <id>_Hair_A.png and remaps UVs."""
    atlas = np.zeros((size, size, 4), np.float32)
    for kind, (o, tex) in parts.items():
        if o is None or not tex:
            continue
        x0, y0, x1, y1 = HAIR_LAYOUT[kind]
        w, h = int((x1 - x0) * size), int((y1 - y0) * size)
        rgba = load_rgba(tex, w, h)
        if kind in ("hair", "brows", "beard"):
            rgba = recolor(rgba, hair_color, 1.0)
            if kind == "brows":
                rgba[..., :3] *= 0.8
        elif kind == "lashes":
            rgba = recolor(rgba, (0.05, 0.04, 0.035), 0.9, keep_dark=True)
        atlas[int(y0 * size):int(y0 * size) + h, int(x0 * size):int(x0 * size) + w] = rgba
        remap_uv(o, HAIR_LAYOUT[kind])
    # dilate colour into transparent texels so alpha-clipped edges do not fringe dark
    rgb = atlas[..., :3].copy()
    a = atlas[..., 3]
    known = a > 0.05
    for _ in range(8):
        acc = np.zeros_like(rgb)
        cnt = np.zeros(a.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            sh = np.roll(np.roll(rgb, dy, 0), dx, 1)
            kn = np.roll(np.roll(known, dy, 0), dx, 1)
            acc += sh * kn[..., None]
            cnt += kn
        grow = (~known) & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        known = known | grow
    atlas[..., :3] = rgb
    save_array(atlas, os.path.join(out_dir, char_id + "_Hair_A.png"), "PNG")
