# PARASITE — leaf-cluster card atlas renderer (Blender 4.5, Cycles).
# Builds small procedural twigs carrying photo leaves (ambientCG LeafSet atlases, CC0) and renders each twig
# orthographically into one cell of a 2x2 card atlas: linear albedo (emission, with baked AO/depth shading)
# and a tangent-space normal pass (camera-space normals + leaf vein normal maps). Output: EXR per cell, then
# post_atlas.py assembles + dilates them into PNGs.
# usage: blender -b --python leaf_clusters.py -- <src dir> <work dir> [cell resolution]
import bpy, bmesh, json, math, random, sys, os
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, WORK = argv[0], argv[1]
RES = int(argv[2]) if len(argv) > 2 else 1024
RECTS = json.load(open(os.path.join(WORK, "leaf_rects.json")))

# atlas name: leaf set, stem side in the source atlas, leaf length range (card units), twig colour (linear)
SETS = {
    "Green":  ("LeafSet024", "bottom", (0.12, 0.16), (0.16, 0.11, 0.06)),
    "Light":  ("LeafSet014", "bottom", (0.13, 0.175), (0.15, 0.11, 0.07)),
    "Yellow": ("LeafSet007", "left",   (0.13, 0.17), (0.17, 0.12, 0.06)),
    "Maple":  ("LeafSet027", "bottom", (0.13, 0.17), (0.2, 0.09, 0.05)),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type = 'OPTIX'
        prefs.get_devices()
        for d in prefs.devices: d.use = d.type == 'OPTIX'
        sc.cycles.device = 'GPU'
    except Exception as e:
        print("GPU unavailable, CPU render:", e)
    sc.cycles.samples = 96
    sc.cycles.use_denoising = False
    sc.cycles.transparent_max_bounces = 64
    sc.cycles.max_bounces = 2
    sc.cycles.filter_width = 1.0
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.render.resolution_percentage = 100
    st = sc.render.image_settings
    st.file_format = 'OPEN_EXR'; st.color_depth = '32'; st.color_mode = 'RGBA'; st.exr_codec = 'ZIP'
    cam = bpy.data.cameras.new("Cam"); cam.type = 'ORTHO'; cam.ortho_scale = 1.0
    cam.clip_start = 0.1; cam.clip_end = 10.0
    co = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co); sc.camera = co
    co.location = (0.0, 0.5, 5.0); co.rotation_euler = (0.0, 0.0, 0.0)   # looks down -Z, +Y up
    return sc


def node(nt, kind, loc=(0, 0), **props):
    n = nt.nodes.new(kind); n.location = loc
    for k, v in props.items(): setattr(n, k, v)
    return n


def leaf_materials(leafset):
    img_c = bpy.data.images.load(os.path.join(SRC, f"{leafset}_Color.png"))
    img_o = bpy.data.images.load(os.path.join(SRC, f"{leafset}_Opacity.png")); img_o.colorspace_settings.name = 'Non-Color'
    img_n = bpy.data.images.load(os.path.join(SRC, f"{leafset}_NormalGL.png")); img_n.colorspace_settings.name = 'Non-Color'
    mats = {}
    for mode in ("albedo", "normal"):
        m = bpy.data.materials.new(f"Leaf_{mode}"); m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        out = node(nt, 'ShaderNodeOutputMaterial', (900, 0))
        uv = node(nt, 'ShaderNodeUVMap', (-900, 0)); uv.uv_map = "UVMap"
        tc = node(nt, 'ShaderNodeTexImage', (-700, 200), image=img_c)
        to = node(nt, 'ShaderNodeTexImage', (-700, -100), image=img_o)
        for t in (tc, to): nt.links.new(uv.outputs[0], t.inputs[0])
        thr = node(nt, 'ShaderNodeMath', (-450, -100), operation='GREATER_THAN'); thr.inputs[1].default_value = 0.5
        nt.links.new(to.outputs[0], thr.inputs[0])
        transp = node(nt, 'ShaderNodeBsdfTransparent', (300, -200))
        mix = node(nt, 'ShaderNodeMixShader', (700, 0))
        nt.links.new(thr.outputs[0], mix.inputs[0]); nt.links.new(transp.outputs[0], mix.inputs[1])
        em = node(nt, 'ShaderNodeEmission', (500, 100))
        nt.links.new(em.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
        if mode == "albedo":
            # per-leaf brightness/hue jitter, ambient occlusion and depth darkening baked into the card
            attr = node(nt, 'ShaderNodeAttribute', (-700, 450)); attr.attribute_name = "rand"
            hsv = node(nt, 'ShaderNodeHueSaturation', (-400, 300))
            hue = node(nt, 'ShaderNodeMapRange', (-550, 500)); hue.inputs[3].default_value = 0.47; hue.inputs[4].default_value = 0.53
            nt.links.new(attr.outputs['Fac'], hue.inputs[0]); nt.links.new(hue.outputs[0], hsv.inputs['Hue'])
            val = node(nt, 'ShaderNodeMapRange', (-550, 650)); val.inputs[3].default_value = 0.8; val.inputs[4].default_value = 1.12
            nt.links.new(attr.outputs['Fac'], val.inputs[0]); nt.links.new(val.outputs[0], hsv.inputs['Value'])
            nt.links.new(tc.outputs[0], hsv.inputs['Color'])
            ao = node(nt, 'ShaderNodeAmbientOcclusion', (-400, 0)); ao.only_local = True; ao.samples = 16
            ao.inputs['Distance'].default_value = 0.09
            aom = node(nt, 'ShaderNodeMapRange', (-200, 0)); aom.inputs[3].default_value = 0.5; aom.inputs[4].default_value = 1.0
            nt.links.new(ao.outputs['AO'], aom.inputs[0])
            geo = node(nt, 'ShaderNodeNewGeometry', (-700, -400))
            sep = node(nt, 'ShaderNodeSeparateXYZ', (-500, -400)); nt.links.new(geo.outputs['Position'], sep.inputs[0])
            dep = node(nt, 'ShaderNodeMapRange', (-300, -400))
            dep.inputs[1].default_value = -0.2; dep.inputs[2].default_value = 0.2; dep.inputs[3].default_value = 0.78; dep.inputs[4].default_value = 1.0
            nt.links.new(sep.outputs['Z'], dep.inputs[0])
            m1 = node(nt, 'ShaderNodeVectorMath', (0, 200), operation='SCALE'); nt.links.new(hsv.outputs[0], m1.inputs[0]); nt.links.new(aom.outputs[0], m1.inputs['Scale'])
            m2 = node(nt, 'ShaderNodeVectorMath', (200, 200), operation='SCALE'); nt.links.new(m1.outputs[0], m2.inputs[0]); nt.links.new(dep.outputs[0], m2.inputs['Scale'])
            nt.links.new(m2.outputs[0], em.inputs['Color'])
        else:
            tn = node(nt, 'ShaderNodeTexImage', (-700, 450), image=img_n); nt.links.new(uv.outputs[0], tn.inputs[0])
            nm = node(nt, 'ShaderNodeNormalMap', (-450, 450)); nm.uv_map = "UVMap"; nm.inputs['Strength'].default_value = 0.7
            nt.links.new(tn.outputs[0], nm.inputs['Color'])
            camspace_into(nt, nm.outputs[0], em)
        mats[mode] = m
    return mats


def camspace_into(nt, normal_socket, em):
    """emission = camera-space normal (flipped towards the camera) * 0.5 + 0.5"""
    vt = node(nt, 'ShaderNodeVectorTransform', (-250, 450)); vt.vector_type = 'NORMAL'; vt.convert_from = 'WORLD'; vt.convert_to = 'CAMERA'
    nt.links.new(normal_socket, vt.inputs[0])
    sep = node(nt, 'ShaderNodeSeparateXYZ', (-50, 600)); nt.links.new(vt.outputs[0], sep.inputs[0])
    sg = node(nt, 'ShaderNodeMath', (100, 600), operation='SIGN'); nt.links.new(sep.outputs['Z'], sg.inputs[0])
    fl = node(nt, 'ShaderNodeVectorMath', (150, 450), operation='SCALE'); nt.links.new(vt.outputs[0], fl.inputs[0]); nt.links.new(sg.outputs[0], fl.inputs['Scale'])
    nr = node(nt, 'ShaderNodeVectorMath', (250, 450), operation='NORMALIZE'); nt.links.new(fl.outputs[0], nr.inputs[0])
    ma = node(nt, 'ShaderNodeVectorMath', (350, 450), operation='MULTIPLY_ADD')
    ma.inputs[1].default_value = (0.5, 0.5, 0.5); ma.inputs[2].default_value = (0.5, 0.5, 0.5)
    nt.links.new(nr.outputs[0], ma.inputs[0]); nt.links.new(ma.outputs[0], em.inputs['Color'])


def stem_materials(color):
    mats = {}
    for mode in ("albedo", "normal"):
        m = bpy.data.materials.new(f"Stem_{mode}"); m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        out = node(nt, 'ShaderNodeOutputMaterial', (600, 0))
        em = node(nt, 'ShaderNodeEmission', (400, 0)); nt.links.new(em.outputs[0], out.inputs[0])
        if mode == "albedo": em.inputs['Color'].default_value = (*color, 1.0)
        else:
            geo = node(nt, 'ShaderNodeNewGeometry', (-450, 450))
            camspace_into(nt, geo.outputs['Normal'], em)
        mats[mode] = m
    return mats


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.rnd = self.bm.faces.layers.float.new("rand")

    def tube(self, pts, radii, sides=6):
        rings = []
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            a = d.orthogonal().normalized(); b = d.cross(a).normalized()
            rings.append([self.bm.verts.new(p + (a * math.cos(k / sides * 2 * math.pi) + b * math.sin(k / sides * 2 * math.pi)) * radii[i]) for k in range(sides)])
        for i in range(len(rings) - 1):
            for k in range(sides):
                f = self.bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
                f.material_index = 1

    def leaf(self, base, u, n, length, aspect, rect, img, stem, fold, droop, rnd):
        s = n.cross(u).normalized()
        n = u.cross(s).normalized()
        w = length * aspect
        W, H = img
        x0, y0, x1, y1 = rect
        grid = []
        for r in (0.0, 0.5, 1.0):
            row = []
            for c in (0.0, 0.5, 1.0):
                side = (c - 0.5) * 2.0                       # -1 .. 1 across the blade
                p = base + u * (length * r) + s * (side * w * 0.5 * math.cos(fold)) + n * (abs(side) * w * 0.5 * math.sin(fold)) - n * (droop * length * r * r)
                if stem == "bottom": px, py = x0 + c * (x1 - x0), y1 - r * (y1 - y0)
                else: px, py = x0 + r * (x1 - x0), y0 + c * (y1 - y0)
                row.append((self.bm.verts.new(p), (px / W, 1.0 - py / H)))
            grid.append(row)
        for r in range(2):
            for c in range(2):
                quad = (grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c])
                f = self.bm.faces.new([q[0] for q in quad])
                f.material_index = 0
                f[self.rnd] = rnd
                for loop, q in zip(f.loops, quad): loop[self.uv].uv = q[1]

    def finish(self, name, mats):
        # fit the cluster into the card: base at (0, 0), x within +-0.48, y within 0..0.97
        xs = [abs(v.co.x) for v in self.bm.verts]; ys = [v.co.y for v in self.bm.verts]
        k = min(0.47 / max(xs), 0.965 / max(ys))
        for v in self.bm.verts: v.co *= k
        me = bpy.data.meshes.new(name); self.bm.to_mesh(me); self.bm.free()
        for m in mats: me.materials.append(m)
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
        return ob


def bezier(a, c, b, t): return a * (1 - t) ** 2 + c * (2 * (1 - t) * t) + b * t * t


def cluster(R, leafset, stem_side, lrange):
    info = RECTS[leafset]; rects = info["rects"]; img = info["size"]
    B = Builder()
    def leaf_at(p, d, scale=1.0):
        rect = R.choice(rects)
        rw, rh = rect[2] - rect[0], rect[3] - rect[1]
        aspect = rw / rh if stem_side == "bottom" else rh / rw
        n = Vector((R.uniform(-0.55, 0.55), R.uniform(-0.35, 0.35), 1.0)).normalized()
        if abs(n.dot(d)) > 0.9: n = Vector((0, 0, 1))
        B.leaf(p, d, n, R.uniform(*lrange) * scale, aspect, rect, img, stem_side, math.radians(R.uniform(8, 28)), R.uniform(0.05, 0.25), R.random())
    a = Vector((0, 0, 0)); b = Vector((R.uniform(-0.14, 0.14), R.uniform(0.78, 0.9), R.uniform(-0.08, 0.08)))
    c = Vector((R.uniform(-0.12, 0.12), 0.45, R.uniform(-0.1, 0.1)))
    pts = [bezier(a, c, b, i / 9) for i in range(10)]
    B.tube(pts, [0.011 * (1 - i / 11) + 0.003 for i in range(10)])
    nt = R.randint(6, 8)
    side = R.choice((-1, 1))
    for j in range(nt):
        t = 0.14 + 0.72 * (j + R.uniform(-0.3, 0.3)) / max(1, nt - 1)
        t = min(max(t, 0.1), 0.9)
        p0 = bezier(a, c, b, t); d0 = (bezier(a, c, b, t + 0.02) - p0).normalized()
        ang = math.radians(R.uniform(38, 64)) * side; side = -side
        d = Matrix.Rotation(ang, 3, 'Z') @ d0
        d = (d + Vector((0, 0, R.uniform(-0.45, 0.45)))).normalized()
        ln = 0.34 * (1 - t * 0.55) * R.uniform(0.8, 1.1)
        tp = [p0 + d * (ln * i / 5) + Vector((0, -0.02, 0)) * (i / 5) ** 2 for i in range(6)]
        B.tube(tp, [0.006 * (1 - i / 7) + 0.0015 for i in range(6)], sides=5)
        nl = R.randint(4, 6); ls = R.choice((-1, 1))
        for i in range(nl):
            s = 0.28 + 0.72 * (i + 0.5) / nl
            p = tp[0].lerp(tp[-1], s)
            dl = Matrix.Rotation(math.radians(R.uniform(30, 62)) * ls, 3, 'Z') @ d; ls = -ls
            leaf_at(p, (dl + Vector((0, 0, R.uniform(-0.35, 0.35)))).normalized())
        leaf_at(tp[-1], d, 1.08)
    for i in range(3):
        t = 0.72 + 0.09 * i
        p = bezier(a, c, b, t); d0 = (bezier(a, c, b, t + 0.02) - p).normalized()
        leaf_at(p, (Matrix.Rotation(math.radians(R.uniform(25, 50)) * (1 if i % 2 else -1), 3, 'Z') @ d0).normalized())
    leaf_at(b, (b - bezier(a, c, b, 0.95)).normalized(), 1.12)
    return B


def main():
    out_dir = os.path.join(WORK, "clusters"); os.makedirs(out_dir, exist_ok=True)
    for name, (leafset, stem_side, lrange, twig) in SETS.items():
        sc = reset()
        lm = leaf_materials(leafset); sm = stem_materials(twig)
        for cell in range(4):
            for o in list(sc.collection.objects):
                if o.type == 'MESH': bpy.data.objects.remove(o)
            R = random.Random(sum(map(ord, name)) * 31 + cell * 977)
            ob = cluster(R, leafset, stem_side, lrange).finish(f"{name}_{cell}", [lm["albedo"], sm["albedo"]])
            for mode in ("albedo", "normal"):
                ob.data.materials[0] = lm[mode]; ob.data.materials[1] = sm[mode]
                sc.render.filepath = os.path.join(out_dir, f"{name}_{cell}_{mode}.exr")
                bpy.ops.render.render(write_still=True)
                print("[clusters] rendered", sc.render.filepath)


main()
