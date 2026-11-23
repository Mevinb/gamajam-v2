# PARASITE — procedural street trees (Blender 4.5, headless). Recursive branch skeletons inside a crown envelope,
# bark tubes with parallel-transport frames and tiling UVs, leaf-cluster cards (atlas from leaf_clusters.py) with
# crown-spherical normals, wind vertex colours and three LODs. Exports one FBX per species:
#   Tree<Name>.fbx  -> objects Tree<Name>_LOD0/1/2 (Unity builds the LODGroup), materials Bark<Name> + Leaves<Leaf>.
# Vertex colour (linear): R = branch sway weight (0 trunk base .. 1 outer twigs), G = per-branch phase (0..1),
#                         B = leaf flutter weight (0 card base .. 1 card top), A = 1.
# usage: blender -b --python trees.py -- <out dir>
import bpy, math, random, sys, os
from mathutils import Vector, Matrix, Quaternion

OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)

SPECIES = {
    # broad, rounded London-plane-like street tree; trunk forks into scaffold limbs
    "Plane": dict(leaf="Green", seed=11, trunk=6.4, r0=0.21, lean=0.05, taper=0.3,
                  kids=[(7, 0.40, 0.96, 52, 3.6), (6, 0.22, 1.0, 46, 0.52), (5, 0.25, 1.0, 42, 0.5)],
                  env=((0, 0, 5.7), (3.5, 3.5, 3.0)), cards=330, card=(1.05, 1.45), wiggle=(0.08, 0.16, 0.24, 0.3), up=(0.0, 0.05, 0.02, 0.02)),
    # excurrent linden-like tree with a central leader and an ovate crown
    "Linden": dict(leaf="Light", seed=29, trunk=8.2, r0=0.17, lean=0.03, taper=0.15,
                   kids=[(12, 0.28, 0.98, 60, 2.8), (6, 0.22, 1.0, 44, 0.55), (4, 0.3, 1.0, 40, 0.5)],
                   env=((0, 0, 5.5), (2.7, 2.7, 3.4)), cards=340, card=(1.0, 1.35), wiggle=(0.05, 0.14, 0.22, 0.3), up=(0.0, 0.07, 0.04, 0.02)),
    # vase-shaped zelkova/maple: short trunk, 5 upright scaffolds, wide flat-topped crown (autumn foliage)
    "Maple": dict(leaf="Maple", seed=37, trunk=2.6, r0=0.22, lean=0.04, taper=0.7,
                  kids=[(5, 0.8, 1.0, 32, 5.0), (7, 0.25, 1.0, 40, 0.52), (5, 0.3, 1.0, 40, 0.5)],
                  env=((0, 0, 5.9), (3.4, 3.4, 2.8)), cards=290, card=(1.05, 1.45), wiggle=(0.06, 0.12, 0.22, 0.3), up=(0.0, 0.1, 0.04, 0.02)),
}

LODS = [  # tube sides per level (0 = no tube), fraction of cards kept, card scale
    dict(sides=(10, 7, 4), cards=1.0, scale=1.0),
    dict(sides=(7, 5, 3), cards=0.52, scale=1.32),
    dict(sides=(5, 4, 0), cards=0.24, scale=1.85),
]
BARK_TILE = 0.85   # metres per bark texture repeat


class Branch:
    def __init__(self, level, pts, radii, phase, w0, w1):
        self.level, self.pts, self.radii, self.phase, self.w0, self.w1 = level, pts, radii, phase, w0, w1
        self.children = []
        L = [0.0]
        for i in range(1, len(pts)): L.append(L[-1] + (pts[i] - pts[i - 1]).length)
        self.cum = L

    @property
    def length(self): return self.cum[-1]

    def at(self, s):
        """point, direction, radius, sway weight at normalised length s"""
        t = s * self.length
        for i in range(1, len(self.pts)):
            if self.cum[i] >= t or i == len(self.pts) - 1:
                seg = max(1e-6, self.cum[i] - self.cum[i - 1]); f = min(1.0, max(0.0, (t - self.cum[i - 1]) / seg))
                p = self.pts[i - 1].lerp(self.pts[i], f)
                d = (self.pts[i] - self.pts[i - 1]).normalized()
                r = self.radii[i - 1] * (1 - f) + self.radii[i] * f
                return p, d, r, self.w0 + (self.w1 - self.w0) * s
        return self.pts[-1], Vector((0, 0, 1)), self.radii[-1], self.w1


def rand_vec(R):
    v = Vector((R.gauss(0, 1), R.gauss(0, 1), R.gauss(0, 1)))
    return v.normalized() if v.length > 1e-6 else Vector((0, 0, 1))


def env_dist(p, d, C, E):
    """distance along d from p to the crown ellipsoid boundary (C centre, E radii)"""
    q = Vector(((p.x - C.x) / E.x, (p.y - C.y) / E.y, (p.z - C.z) / E.z))
    k = Vector((d.x / E.x, d.y / E.y, d.z / E.z))
    a = k.dot(k); b = 2 * q.dot(k); c = q.dot(q) - 1
    disc = b * b - 4 * a * c
    if disc < 0: return 0.0
    return max(0.0, (-b + math.sqrt(disc)) / (2 * a))


def grow(sp, R, C, E, start, d, length, r_base, level, w_base, w_tip):
    seg = (0.55, 0.45, 0.32)[min(level, 2)]
    n = max(3, int(length / seg) + 1)
    pts = [start.copy()]
    for i in range(n):
        d = (d + rand_vec(R) * sp["wiggle"][level] + Vector((0, 0, 1)) * sp["up"][level]).normalized()
        if level > 0:
            # keep branches inside the crown envelope: steer back towards the centre near the boundary
            p = pts[-1]
            q = Vector(((p.x - C.x) / E.x, (p.y - C.y) / E.y, (p.z - C.z) / E.z))
            if q.length > 0.92: d = (d - q.normalized() * 0.35).normalized()
        pts.append(pts[-1] + d * (length / n))
    tip = sp.get("taper", 0.22) if level == 0 else 0.18
    radii = [r_base * (1 - (1 - tip) * (i / n) ** 0.85) for i in range(n + 1)]
    if level == 0:   # root flare
        radii = [r * (1 + 0.55 * math.exp(-(pts[i].z) / 0.3)) for i, r in enumerate(radii)]
    br = Branch(level, pts, radii, R.random(), w_base, w_tip)
    if level < 3:
        cnt, s0, s1, ang, ln = sp["kids"][level]
        roll = R.uniform(0, 360)
        for j in range(cnt):
            s = s0 + (s1 - s0) * (j + R.uniform(0.15, 0.85)) / cnt
            p, pd, pr, pw = br.at(s)
            roll += 137.5 + R.uniform(-25, 25)
            perp = pd.orthogonal().normalized()
            axis = (Quaternion(pd, math.radians(roll)) @ perp).normalized()
            cd = (Quaternion(axis, math.radians(ang + R.uniform(-12, 12))) @ pd).normalized()
            if level == 0: want = ln * R.uniform(0.82, 1.12)
            else: want = (br.length * (1 - s) + 0.6) * ln * R.uniform(0.8, 1.15)
            room = env_dist(p, cd, C, E)
            L = min(want, room * 1.05) if level > 0 else min(want, max(room * 1.1, 1.2))
            if L < 0.25: continue
            cr = max(0.012, pr * ((0.78 if sp.get("taper", 0) > 0.5 else 0.62) if level == 0 else 0.55) * (L / max(want, 1e-3)) ** 0.3)
            wt = (0.45, 0.8, 1.0)[level]
            br.children.append(grow(sp, R, C, E, p - cd * pr * 0.4, cd, L, cr, level + 1, pw, wt))
    return br


def all_branches(b):
    yield b
    for c in b.children: yield from all_branches(c)


class MeshData:
    def __init__(self):
        self.v, self.faces, self.fmat, self.luv, self.lcol, self.lnor = [], [], [], [], [], []

    def add_face(self, idx, uvs, cols, nors, mat):
        self.faces.append(idx); self.fmat.append(mat)
        self.luv += uvs; self.lcol += cols; self.lnor += nors


def tube(md, br, sides):
    if sides <= 0 or len(br.pts) < 2: return
    nrm = br.pts[1] - br.pts[0]; nrm.normalize()
    ref = nrm.orthogonal().normalized()
    u_tiles = max(1, round(2 * math.pi * br.radii[0] / BARK_TILE))
    rings = []
    prev_d = nrm
    for i, p in enumerate(br.pts):
        d = (br.pts[min(i + 1, len(br.pts) - 1)] - br.pts[max(i - 1, 0)]).normalized()
        # parallel transport of the reference vector
        q = prev_d.rotation_difference(d); ref = (q @ ref).normalized(); prev_d = d
        b = d.cross(ref).normalized(); a = b.cross(d).normalized()
        ring = []
        for k in range(sides + 1):
            t = k / sides * 2 * math.pi
            n = a * math.cos(t) + b * math.sin(t)
            md.v.append(p + n * br.radii[i])
            ring.append((len(md.v) - 1, n, k / sides * u_tiles))
        rings.append(ring)
    for i in range(len(rings) - 1):
        s0, s1 = br.cum[i] / br.length, br.cum[i + 1] / br.length
        v0, v1 = br.cum[i] / BARK_TILE, br.cum[i + 1] / BARK_TILE
        w0, w1 = br.w0 + (br.w1 - br.w0) * s0, br.w0 + (br.w1 - br.w0) * s1
        if br.level == 0:  # trunk barely sways
            w0, w1 = 0.06 * s0, 0.06 * s1
        for k in range(sides):
            A, B, Cc, D = rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]
            md.add_face([A[0], B[0], Cc[0], D[0]],
                        [(A[2], v0), (B[2], v0), (Cc[2], v1), (D[2], v1)],
                        [(w0, br.phase, 0, 1), (w0, br.phase, 0, 1), (w1, br.phase, 0, 1), (w1, br.phase, 0, 1)],
                        [A[1], B[1], Cc[1], D[1]], 0)


def card_sites(root, sp, R, C, E):
    """candidate (base, branch direction, sway weight, phase) leaf-card attachment points"""
    sites = []
    for b in all_branches(root):
        if b.level < 1: continue
        if b.level == 1:
            ss = [0.82, 0.97]
        else:
            k = max(2, int(b.length / 0.42))
            ss = [0.3 + 0.7 * (i + 0.5) / k for i in range(k)]
        for s in ss:
            p, d, r, w = b.at(s)
            q = Vector(((p.x - C.x) / E.x, (p.y - C.y) / E.y, (p.z - C.z) / E.z))
            if q.length < 0.42 and R.random() < 0.7: continue      # hidden crown core
            sites.append((p, d, max(w, 0.55), b.phase))
    R.shuffle(sites)
    return sites[:sp["cards"]]


def cards(md, sites, sp, R, C, E, lod):
    n = len(sites)
    keep = sites[:max(8, int(n * lod["cards"]))]
    lo, hi = sp["card"]
    for i, (p, d, w, ph) in enumerate(keep):
        rr = random.Random(i * 7919 + 17)                        # same card shape across LODs
        q = Vector(((p.x - C.x) / E.x, (p.y - C.y) / E.y, (p.z - C.z) / E.z))
        out = Vector((q.x / E.x, q.y / E.y, q.z / E.z)).normalized() if q.length > 1e-4 else Vector((0, 0, 1))
        up = (d * 0.55 + out * 0.65 + Vector((0, 0, 0.35))).normalized()
        right = up.orthogonal().normalized()
        right = (Quaternion(up, rr.uniform(0, math.pi)) @ right).normalized()
        nrm = right.cross(up).normalized()
        if nrm.dot(out) < 0: right = -right; nrm = -nrm
        size = rr.uniform(lo, hi) * lod["scale"]
        base = p - up * size * 0.12
        cell = rr.randrange(4); flip = rr.random() < 0.5
        u0, v0 = (cell % 2) * 0.5, (cell // 2) * 0.5
        corners = [(-0.5, 0.0), (0.5, 0.0), (0.5, 1.0), (-0.5, 1.0)]
        idx, uvs, cols, nors = [], [], [], []
        for cx, cy in corners:
            pos = base + right * (cx * size) + up * (cy * size)
            md.v.append(pos); idx.append(len(md.v) - 1)
            uu = (0.5 - cx) if flip else (cx + 0.5)
            uvs.append((u0 + 0.004 + uu * 0.492, v0 + 0.004 + cy * 0.492))
            sph = Vector(((pos.x - C.x) / E.x ** 2, (pos.y - C.y) / E.y ** 2, (pos.z - C.z + 0.6) / E.z ** 2)).normalized()
            nors.append((nrm * 0.35 + sph * 0.65).normalized())
            cols.append((min(1.0, w + 0.08 * cy), ph, cy, 1.0))
        md.add_face(idx, uvs, cols, nors, 1)


def build_object(name, md, mats):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in md.v], [], md.faces)
    me.update()
    for m in mats: me.materials.append(m)
    me.polygons.foreach_set("material_index", md.fmat)
    uv = me.uv_layers.new(name="UVMap")
    uv.data.foreach_set("uv", [c for t in md.luv for c in t])
    col = me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
    col.data.foreach_set("color", [c for t in md.lcol for c in t])
    me.color_attributes.active_color = col
    me.normals_split_custom_set([tuple(n) for n in md.lnor])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob, len(me.polygons), sum(len(p.vertices) - 2 for p in me.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, sp in SPECIES.items():
        for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o)
        R = random.Random(sp["seed"])
        C = Vector(sp["env"][0]); E = Vector(sp["env"][1])
        d0 = Vector((R.uniform(-1, 1) * sp["lean"], R.uniform(-1, 1) * sp["lean"], 1)).normalized()
        root = grow(sp, R, C, E, Vector((0, 0, 0)), d0, sp["trunk"], sp["r0"], 0, 0.0, 0.1)
        sites = card_sites(root, sp, R, C, E)
        bark = bpy.data.materials.get("Bark" + name) or bpy.data.materials.new("Bark" + name)
        leaves = bpy.data.materials.get("Leaves" + sp["leaf"]) or bpy.data.materials.new("Leaves" + sp["leaf"])
        objs = []
        for li, lod in enumerate(LODS):
            md = MeshData()
            for b in all_branches(root):
                tube(md, b, lod["sides"][min(b.level, 2)] if b.level <= 2 else 0)
            cards(md, sites, sp, R, C, E, lod)
            ob, faces, tris = build_object(f"Tree{name}_LOD{li}", md, [bark, leaves])
            objs.append(ob)
            print(f"[trees] {name} LOD{li}: {tris} triangles, {len(md.v)} verts, cards {int(len(sites) * lod['cards'])}")
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        path = os.path.join(OUT, f"Tree{name}.fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                                 apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                                 mesh_smooth_type='OFF', use_mesh_modifiers=False, colors_type='LINEAR', prioritize_active_color=True,
                                 use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
        print("[trees] exported", path)


main()
