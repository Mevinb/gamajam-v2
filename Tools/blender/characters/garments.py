"""
Procedural, skinned garments built on the generated MPFB body (Blender python).
All garments are built in the A rest pose before rename/T-pose; weights come from the body ("WeightSource").
Each garment object carries custom props: z_depth (layer order for hidden-surface removal), kind,
and a 'detail' image + uv domain used by atlas baking (procedural textures).
"""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
import chartools as ct

PRE = "mixamorig:"


# ---------------------------------------------------------------- helpers

def vgroup_weights(src):
    """list of dict(group_name->weight) per vertex of src."""
    names = {vg.index: vg.name for vg in src.vertex_groups}
    out = []
    for v in src.data.vertices:
        out.append({names[g.group]: g.weight for g in v.groups if g.weight > 0.0})
    return out


def region_of(wd):
    torso = arm_l = arm_r = other = 0.0
    for n, w in wd.items():
        if not n.startswith(PRE):
            continue
        b = n[len(PRE):]
        if b in ("Hips", "Spine", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "LeftBreast", "RightBreast",
                 "LeftButtock", "RightButtock"):
            torso += w
        elif b in ("LeftArm", "LeftForeArm"):
            arm_l += w
        elif b in ("RightArm", "RightForeArm"):
            arm_r += w
        else:
            other += w
    m = max(torso, arm_l, arm_r, other)
    if m <= 0:
        return "other"
    if m == torso: return "torso"
    if m == arm_l: return "arm_l"
    if m == arm_r: return "arm_r"
    return "other"


def bvh_of(objs):
    return ct.evaluated_world_mesh_bvh([o for o in objs if o])


ARM_BONES = ("Arm", "ForeArm", "Hand")


def bvh_without_arms(objs):
    """BVH of the given meshes minus faces dominated by arm/hand bones (A-pose arms hang beside the hips)."""
    verts, polys = [], []
    for o in objs:
        if o is None:
            continue
        names = {vg.index: vg.name for vg in o.vertex_groups}
        armish = []
        for v in o.data.vertices:
            best, bw = "", 0.0
            for g in v.groups:
                if g.weight > bw:
                    best, bw = names.get(g.group, ""), g.weight
            b = best[len(PRE):] if best.startswith(PRE) else best
            armish.append(any(k in b for k in ARM_BONES) and "UpLeg" not in b)
        off = len(verts)
        mw = o.matrix_world
        verts.extend([mw @ v.co for v in o.data.vertices])
        polys.extend([[off + i for i in p.vertices] for p in o.data.polygons if not any(armish[i] for i in p.vertices)])
    return BVHTree.FromPolygons(verts, polys, all_triangles=False, epsilon=0.0) if polys else None


def new_obj(name, me, z_depth, kind):
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o["z_depth"] = z_depth
    o["kind"] = kind
    return o


def smooth_scalar(bm, values, iters=4):
    vals = list(values)
    for _ in range(iters):
        nv = vals[:]
        for v in bm.verts:
            nb = [e.other_vert(v).index for e in v.link_edges]
            if nb:
                nv[v.index] = 0.5 * vals[v.index] + 0.5 * sum(vals[i] for i in nb) / len(nb)
        vals = nv
    return vals


def push_out(bm, under_bvh, base_off, extra=0.009, max_probe=0.05, smooth=6):
    """Move verts along normals so they sit outside the 'under' layers (robust to isolated far hits)."""
    bm.normal_update()
    need = []
    for v in bm.verts:
        h = 0.0
        if under_bvh:
            n = v.normal
            loc, nor, idx, dist = under_bvh.ray_cast(v.co + n * 0.0005, n, max_probe)
            if loc is not None:
                h = dist
        need.append(max(base_off, h + extra))
    sm = smooth_scalar(bm, need, smooth)
    final = [max(a, min(b, a + 0.015)) for a, b in zip(sm, need)]
    for v in bm.verts:
        v.co = v.co + v.normal * final[v.index]
    return final


def loop_uvs_cylinder(bm, uv, faces, center_xy, radius, axis_front_neg_y=True):
    """u = angle*radius (front = 0, +u = wearer's left/+X), v = z. Seam at the back."""
    cx, cy = center_xy
    for f in faces:
        ths = []
        for l in f.loops:
            x, y = l.vert.co.x - cx, l.vert.co.y - cy
            ths.append(math.atan2(x, -y))
        if max(ths) - min(ths) > math.pi:
            ths = [t + 2 * math.pi if t < 0 else t for t in ths]
        for l, t in zip(f.loops, ths):
            l[uv].uv = (t * radius, l.vert.co.z)


def loop_uvs_limb(bm, uv, faces, a, b, radius, u_offset):
    """Cylindrical around axis a->b; seam underneath. u = angle*radius + u_offset, v = distance along axis."""
    ax = (b - a)
    L = ax.length
    ax.normalize()
    up = Vector((0, 0, 1))
    ref = (up - ax * up.dot(ax)).normalized()
    side = ax.cross(ref)
    for f in faces:
        ths = []
        ts = []
        for l in f.loops:
            p = l.vert.co - a
            t = p.dot(ax)
            q = p - ax * t
            ths.append(math.atan2(q.dot(side), q.dot(ref)))
            ts.append(t)
        if max(ths) - min(ths) > math.pi:
            ths = [t + 2 * math.pi if t < 0 else t for t in ths]
        for l, th, t in zip(f.loops, ths, ts):
            l[uv].uv = (u_offset + th * radius, t)


def reduce_mesh(o, ratio):
    ct.set_active(o)
    m = o.modifiers.new("Dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = ratio
    m.use_collapse_triangulate = False
    bpy.ops.object.modifier_apply(modifier=m.name)


def make_detail_image(name, w, h):
    img = np.ones((h, w, 4), dtype=np.float32)
    return img


def save_detail(name, arr):
    h, w = arr.shape[:2]
    im = bpy.data.images.get(name) or bpy.data.images.new(name, w, h, alpha=True, float_buffer=False)
    if im.size[0] != w or im.size[1] != h:
        im.scale(w, h)
    im.pixels.foreach_set(arr.astype(np.float32).ravel())
    im.update()
    im.pack()
    return im


class Canvas:
    """Paint in garment-UV metres onto a numpy RGBA image (multiplier on fabric albedo; A unused)."""

    def __init__(self, u0, u1, v0, v1, ppm=900):
        self.u0, self.u1, self.v0, self.v1 = u0, u1, v0, v1
        self.w = int((u1 - u0) * ppm)
        self.h = int((v1 - v0) * ppm)
        self.ppm = ppm
        self.img = np.ones((self.h, self.w, 4), dtype=np.float32)
        yy, xx = np.mgrid[0:self.h, 0:self.w]
        self.U = u0 + (xx + 0.5) / ppm
        self.V = v0 + (yy + 0.5) / ppm

    def blend(self, mask, color, a=1.0):
        m = np.clip(mask, 0, 1)[..., None] * a
        c = np.array(color + (1.0,) if len(color) == 3 else color, dtype=np.float32)
        self.img = self.img * (1 - m) + c * m

    def line(self, p0, p1, width, color, a=1.0, dash=0.0):
        (u0, v0), (u1, v1) = p0, p1
        du, dv = u1 - u0, v1 - v0
        L2 = du * du + dv * dv + 1e-12
        t = np.clip(((self.U - u0) * du + (self.V - v0) * dv) / L2, 0, 1)
        d = np.hypot(self.U - (u0 + t * du), self.V - (v0 + t * dv))
        m = np.clip((width * 0.5 - d) * self.ppm + 0.5, 0, 1)
        if dash > 0:
            s = t * math.sqrt(L2)
            m = m * ((s / dash) % 1.0 < 0.55)
        self.blend(m, color, a)

    def rect(self, u0, v0, u1, v1, color, a=1.0, soft=0.002):
        mu = np.clip((np.minimum(self.U - u0, u1 - self.U)) / soft + 0.5, 0, 1)
        mv = np.clip((np.minimum(self.V - v0, v1 - self.V)) / soft + 0.5, 0, 1)
        self.blend(mu * mv, color, a)

    def outline(self, u0, v0, u1, v1, width, color, a=1.0, dash=0.0):
        self.line((u0, v0), (u1, v0), width, color, a, dash)
        self.line((u1, v0), (u1, v1), width, color, a, dash)
        self.line((u1, v1), (u0, v1), width, color, a, dash)
        self.line((u0, v1), (u0, v0), width, color, a, dash)

    def circle(self, cu, cv, r, color, a=1.0):
        d = np.hypot(self.U - cu, self.V - cv)
        self.blend(np.clip((r - d) * self.ppm + 0.5, 0, 1), color, a)

    def ring(self, cu, cv, r, width, color, a=1.0):
        d = np.abs(np.hypot(self.U - cu, self.V - cv) - r)
        self.blend(np.clip((width * 0.5 - d) * self.ppm + 0.5, 0, 1), color, a)

    def finish(self, name):
        im = save_detail(name, self.img)
        return im, (self.u0, self.u1, self.v0, self.v1)


def attach_detail(o, im, domain, fabric=None, color=(1, 1, 1), smooth=0.25, tint=None, metal=0.0, bump=0.0,
                  fabric_scale=0.22, sleeve_domain=None):
    o["detail_image"] = im.name if im else ""
    o["detail_domain"] = list(domain) if domain else []
    o["fabric"] = fabric or ""
    o["base_color"] = list(color)
    o["smoothness"] = smooth
    o["metallic"] = metal
    o["tint"] = tint or ""
    o["bump"] = bump
    o["fabric_scale"] = fabric_scale


# ---------------------------------------------------------------- torso shell (coat / vest)

def body_landmarks(rig):
    def H(n): return ct.bone_head(rig, PRE + n)
    def T(n): return ct.bone_tail(rig, PRE + n)
    lm = {
        "neck": H("Neck"), "head": H("Head"), "chest": H("Spine2"), "spine1": H("Spine1"), "spine": H("Spine"),
        "hips": H("Hips"), "l_shoulder": H("LeftArm"), "r_shoulder": H("RightArm"),
        "l_elbow": H("LeftForeArm"), "r_elbow": H("RightForeArm"), "l_wrist": H("LeftHand"), "r_wrist": H("RightHand"),
        "l_hip": H("LeftUpLeg"), "r_hip": H("RightUpLeg"), "l_knee": H("LeftLeg"), "r_knee": H("RightLeg"),
        "l_ankle": H("LeftFoot"), "r_ankle": H("RightFoot"),
    }
    return lm


def torso_shell(src, rig, under, *, name, sleeves, v_depth, v_halfwidth, bottom_z, sleeve_t=0.93,
                base_off=0.014, extra=0.009, collar_up=0.0):
    """Extract a shell from the body: torso (+ sleeves) between the neck/V opening and bottom_z, pushed outside 'under'."""
    lm = body_landmarks(rig)
    wds = vgroup_weights(src)
    regions = [region_of(w) for w in wds]
    neck_z = lm["neck"].z + 0.012 + collar_up
    neck_y = lm["neck"].y
    zv = neck_z - v_depth
    slope = v_halfwidth / max(0.01, v_depth)
    me = src.data
    co = [v.co.copy() for v in me.vertices]

    def arm_t(i, side):
        a = lm[side + "_shoulder"]; b = lm[side + "_wrist"]
        ax = b - a
        return (co[i] - a).dot(ax) / ax.length_squared

    keep = []
    for i, v in enumerate(me.vertices):
        r = regions[i]
        p = co[i]
        k = True
        if r in ("arm_l", "arm_r"):
            if not sleeves:
                k = False
            elif arm_t(i, "l" if r == "arm_l" else "r") > sleeve_t:
                k = False
        elif r == "torso":
            if p.z > neck_z:
                k = False
            elif p.z < bottom_z:
                k = False
            elif p.y < neck_y + 0.02 and p.z > zv and abs(p.x) < (p.z - zv) * slope:
                k = False  # V opening at the front
        else:
            # neck/head/hands/legs excluded, but let the shoulders keep neck-side verts below the collar
            k = False
        keep.append(k)

    bm = bmesh.new()
    bm.from_mesh(me)
    for layer in list(bm.loops.layers.uv.values()):  # body UVs are meaningless for the garment
        bm.loops.layers.uv.remove(layer)
    bm.verts.ensure_lookup_table()
    kill = [f for f in bm.faces if not all(keep[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # keep the largest connected component only (drop islands)
    bm.verts.ensure_lookup_table()
    comp = {}
    cid = 0
    for v in bm.verts:
        if v in comp:
            continue
        stack = [v]
        comp[v] = cid
        while stack:
            x = stack.pop()
            for e in x.link_edges:
                y = e.other_vert(x)
                if y not in comp:
                    comp[y] = cid
                    stack.append(y)
        cid += 1
    sizes = {}
    for v, c in comp.items():
        sizes[c] = sizes.get(c, 0) + 1
    big = max(sizes, key=sizes.get)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if comp[v] != big], context="VERTS")
    # remember source index for weights
    bm.verts.ensure_lookup_table()
    src_idx = bm.verts.layers.int.new("src")
    # bmesh kept original order of surviving verts; map via nearest original coordinate (exact match)
    lookup = {}
    for i, c in enumerate(co):
        lookup[(round(c.x, 6), round(c.y, 6), round(c.z, 6))] = i
    for v in bm.verts:
        v[src_idx] = lookup.get((round(v.co.x, 6), round(v.co.y, 6), round(v.co.z, 6)), -1)
    under_bvh = bvh_of(under)
    push_out(bm, under_bvh, base_off, extra)
    straighten_v_edge(bm, neck_y, neck_z, zv, slope)
    return bm, src_idx, lm, wds


def straighten_v_edge(bm, neck_y, neck_z, zv, slope):
    """Snap the front opening's boundary verts onto a clean V (body topology leaves a jagged staircase)."""
    for e in bm.edges:
        if not e.is_boundary:
            continue
        for v in e.verts:
            p = v.co
            if p.y < neck_y + 0.04 and zv - 0.03 < p.z < neck_z + 0.02 and abs(p.x) < 0.16:
                target = max(0.004, (p.z - zv) * slope)
                if abs(abs(p.x) - target) < 0.05:
                    v.co.x = math.copysign(target, p.x if abs(p.x) > 1e-5 else 1.0)


def boundary_loops(bm):
    edges = [e for e in bm.edges if e.is_boundary]
    used = set()
    loops = []
    adj = {}
    for e in edges:
        for v in e.verts:
            adj.setdefault(v, []).append(e)
    for e in edges:
        if e in used:
            continue
        loop = [e.verts[0], e.verts[1]]
        used.add(e)
        cur = e.verts[1]
        while True:
            nxt = None
            for e2 in adj.get(cur, []):
                if e2 not in used:
                    nxt = e2
                    break
            if nxt is None:
                break
            used.add(nxt)
            cur = nxt.other_vert(cur)
            if cur == loop[0]:
                break
            loop.append(cur)
        loops.append(loop)
    return loops


def extrude_skirt(bm, loop, hem_z, under_bvh, rings=6, flare=0.06, min_gap=0.022, src_layer=None):
    """Extrude the bottom boundary loop down to hem_z as a hanging tube (encloses both legs)."""
    cx = sum(v.co.x for v in loop) / len(loop)
    cy = sum(v.co.y for v in loop) / len(loop)
    z0 = sum(v.co.z for v in loop) / len(loop)
    # make the loop run consistently
    ths = [math.atan2(v.co.x - cx, -(v.co.y - cy)) for v in loop]
    radii = [math.hypot(v.co.x - cx, v.co.y - cy) for v in loop]
    prev = list(loop)
    prev_r = radii[:]
    new_rings = []
    for k in range(1, rings + 1):
        t = k / rings
        z = z0 + (hem_z - z0) * t
        ring = []
        rr = []
        for i, v in enumerate(loop):
            th = ths[i]
            d = Vector((math.sin(th), -math.cos(th), 0.0))
            r_body = 0.0
            if under_bvh:
                origin = Vector((cx, cy, z)) + d * 0.9
                # march inward: ignore the hanging arms/hands (A pose) that sit far out beside the hips
                start, budget = origin, 0.9
                while budget > 0:
                    loc, nor, idx, dist = under_bvh.ray_cast(start, -d, budget)
                    if loc is None:
                        break
                    rr_hit = math.hypot(loc.x - cx, loc.y - cy)
                    if rr_hit < max(prev_r) * 1.25 + 0.02:
                        r_body = rr_hit
                        break
                    start = loc - d * 0.002
                    budget -= dist + 0.002
            r = max(prev_r[i], r_body + min_gap) + flare * (1.0 / rings) * (0.6 + 0.4 * abs(math.sin(th)))
            rr.append(r)
            nv = bm.verts.new((cx + d.x * r, cy + d.y * r, z))
            if src_layer is not None:
                nv[src_layer] = -1
            ring.append(nv)
        # light smoothing of radii around the ring
        sm = [(rr[i - 1] + 2 * rr[i] + rr[(i + 1) % len(rr)]) * 0.25 for i in range(len(rr))]
        for i, v in enumerate(ring):
            th = ths[i]
            d = Vector((math.sin(th), -math.cos(th), 0.0))
            v.co = Vector((cx + d.x * sm[i], cy + d.y * sm[i], z))
        n = len(loop)
        for i in range(n):
            a, b = prev[i], prev[(i + 1) % n]
            c, d2 = ring[(i + 1) % n], ring[i]
            try:
                bm.faces.new((a, b, c, d2))
            except ValueError:
                pass
        prev, prev_r = ring, sm
        new_rings.append(ring)
    bm.normal_update()
    return new_rings, (cx, cy, z0)


def assign_weights_from_src(o, bm_src_idx_values, wds, extra_weights=None):
    o.vertex_groups.clear()
    groups = {}
    for i, si in enumerate(bm_src_idx_values):
        wd = wds[si] if si >= 0 else (extra_weights[i] if extra_weights and extra_weights[i] else {})
        for n, w in wd.items():
            if not n.startswith(PRE):
                continue
            g = groups.get(n) or o.vertex_groups.new(name=n)
            groups[n] = g
            g.add([i], w, "REPLACE")


def fill_missing_weights(o, src):
    """Verts without weights get transferred weights from the body (nearest)."""
    missing = [v.index for v in o.data.vertices if not v.groups]
    if not missing:
        return
    tmp = o.copy()
    tmp.data = o.data.copy()
    bpy.context.collection.objects.link(tmp)
    ct.transfer_weights(tmp, src)
    names = {vg.index: vg.name for vg in tmp.vertex_groups}
    for i in missing:
        for g in tmp.data.vertices[i].groups:
            n = names[g.group]
            vg = o.vertex_groups.get(n) or o.vertex_groups.new(name=n)
            vg.add([i], g.weight, "REPLACE")
    bpy.data.objects.remove(tmp)


# ---------------------------------------------------------------- LAB COAT

def lab_coat(src, rig, under, *, name="LabCoat", length=0.95, color=(0.84, 0.85, 0.86), vale=False):
    lm = body_landmarks(rig)
    knee_z = (lm["l_knee"].z + lm["r_knee"].z) * 0.5
    hip_z = (lm["l_hip"].z + lm["r_hip"].z) * 0.5
    crotch_cut = hip_z - 0.035
    bm, src_idx, lm, wds = torso_shell(src, rig, under, name=name, sleeves=True, v_depth=0.25, v_halfwidth=0.085,
                                       bottom_z=crotch_cut, sleeve_t=0.935, base_off=0.016, extra=0.010, collar_up=0.012)
    loops = boundary_loops(bm)
    # bottom loop = lowest mean z among loops not near the arms
    def meanz(l): return sum(v.co.z for v in l) / len(l)
    bottom = min(loops, key=meanz)
    hem_z = knee_z + (hip_z - knee_z) * (1.0 - length) + 0.02
    under_bvh = bvh_without_arms([src] + list(under))
    rings, (cx, cy, z0) = extrude_skirt(bm, bottom, hem_z, under_bvh, rings=7, flare=0.05, src_layer=src_idx)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    ring_verts = set(v for r in rings for v in r)
    bm.verts.ensure_lookup_table()
    bm.verts.index_update()
    # UVs
    uv = bm.loops.layers.uv.new("UVMap")
    torso_faces, la, ra = [], [], []
    for f in bm.faces:
        si = [v[src_idx] for v in f.verts]
        regs = [region_of(wds[i]) if i >= 0 else "torso" for i in si]
        if regs.count("arm_l") * 2 > len(regs): la.append(f)
        elif regs.count("arm_r") * 2 > len(regs): ra.append(f)
        else: torso_faces.append(f)
    R0 = 0.17
    loop_uvs_cylinder(bm, uv, torso_faces, (cx, cy), R0)
    loop_uvs_limb(bm, uv, la, lm["l_shoulder"], lm["l_wrist"], 0.05, 1.0)
    loop_uvs_limb(bm, uv, ra, lm["r_shoulder"], lm["r_wrist"], 0.05, 1.5)
    # per-vertex weights: copy from body for shell verts; skirt verts blend hips/thighs
    srcvals = [v[src_idx] for v in bm.verts]
    extra = [None] * len(bm.verts)
    for v in bm.verts:
        if v in ring_verts:
            t = min(1.0, max(0.0, (z0 - v.co.z) / max(0.01, z0 - hem_z)))
            side = 1.0 / (1.0 + math.exp(-(v.co.x - cx) / 0.035))
            leg = 0.55 * t
            extra[v.index] = {PRE + "Hips": 1.0 - leg, PRE + "LeftUpLeg": leg * side, PRE + "RightUpLeg": leg * (1 - side)}
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = new_obj(name, me, 90, "coat")
    assign_weights_from_src(o, srcvals, wds, extra)
    fill_missing_weights(o, src)
    for p in o.data.polygons:
        p.use_smooth = True
    reduce_mesh(o, 0.42)
    # ----- procedural details in torso UV space (u: -pi*R0..pi*R0, v: z)
    neck_z = lm["neck"].z + 0.024
    zv = neck_z - 0.25
    cv = Canvas(-0.56, 0.56, hem_z - 0.03, neck_z + 0.06, ppm=850)
    seam = (0.70, 0.71, 0.73)
    shadow = (0.80, 0.81, 0.83)
    # front closure (wearer's left panel overlaps)
    cv.line((0.0, hem_z), (0.0, zv), 0.003, (0.55, 0.56, 0.58))
    cv.line((0.028, hem_z + 0.03), (0.028, zv - 0.01), 0.0012, seam, dash=0.006)
    # lapels: shading band along the V edges, notch, roll line
    for s in (1, -1):
        e0 = (s * 0.0, zv)
        e1 = (s * 0.085 * 1.05, neck_z - 0.01)
        cv.line(e0, e1, 0.004, (0.60, 0.61, 0.63))
        cv.line((s * 0.045, zv + 0.02), (s * 0.13, neck_z - 0.06), 0.0015, shadow)
        cv.line((s * 0.13, neck_z - 0.06), (s * 0.115, neck_z - 0.035), 0.003, (0.62, 0.63, 0.65))
    # buttons
    v = zv - 0.04
    while v > hip_z - 0.20:
        cv.circle(0.014, v, 0.0075, (0.86, 0.86, 0.85))
        cv.ring(0.014, v, 0.0075, 0.0016, (0.62, 0.62, 0.63))
        cv.circle(0.012, v + 0.0015, 0.0012, (0.55, 0.55, 0.56))
        cv.circle(0.016, v - 0.0015, 0.0012, (0.55, 0.55, 0.56))
        v -= 0.115
    # hip pockets
    py = hip_z - 0.02
    for s in (1, -1):
        u0, u1 = s * 0.07, s * 0.23
        a, b = min(u0, u1), max(u0, u1)
        cv.rect(a, py - 0.17, b, py, (0.965, 0.965, 0.965), 0.6)
        cv.outline(a, py - 0.17, b, py, 0.0022, (0.68, 0.69, 0.71))
        cv.line((a + 0.006, py - 0.012), (b - 0.006, py - 0.012), 0.0012, seam, dash=0.006)
        cv.line((a, py + 0.002), (b, py + 0.002), 0.004, (0.78, 0.79, 0.81))
    # breast pocket (wearer's left = +u) with pens + logo
    bz = lm["chest"].z + 0.07
    cv.rect(0.07, bz - 0.12, 0.18, bz, (0.965, 0.965, 0.965), 0.6)
    cv.outline(0.07, bz - 0.12, 0.18, bz, 0.0022, (0.68, 0.69, 0.71))
    for pu, col in ((0.095, (0.10, 0.18, 0.55)), (0.11, (0.08, 0.08, 0.09)), (0.155, (0.65, 0.12, 0.12) if vale else (0.15, 0.45, 0.25))):
        cv.rect(pu - 0.0035, bz - 0.01, pu + 0.0035, bz + 0.035, col)
        cv.rect(pu - 0.0045, bz + 0.025, pu + 0.0045, bz + 0.032, (0.75, 0.76, 0.78))
    cv.rect(-0.17, bz - 0.035, -0.085, bz - 0.012, (0.10, 0.42, 0.40))  # Vale Biotech logo patch (wearer's right)
    cv.rect(-0.165, bz - 0.031, -0.135, bz - 0.016, (0.80, 0.95, 0.90))
    # hem + side seams
    cv.line((-0.56, hem_z + 0.025), (0.56, hem_z + 0.025), 0.0012, seam, dash=0.006)
    cv.line((-0.56, hem_z + 0.004), (0.56, hem_z + 0.004), 0.004, shadow)
    for s in (1, -1):
        cv.line((s * 0.30, hem_z), (s * 0.30, neck_z - 0.10), 0.0018, (0.83, 0.84, 0.86))
    cv.line((-0.56, hip_z + 0.06), (0.56, hip_z + 0.06), 0.0016, (0.86, 0.87, 0.88))
    im, dom = cv.finish(name + "_detail")
    attach_detail(o, im, dom, fabric="stretch_poplin", color=color, smooth=0.22, bump=0.35, fabric_scale=0.18)
    return o


# ---------------------------------------------------------------- HI-VIS VEST

def hivis_vest(src, rig, under, *, name="HiVisVest", color=(1.0, 0.62, 0.05)):
    lm = body_landmarks(rig)
    hip_z = (lm["l_hip"].z + lm["r_hip"].z) * 0.5
    bottom = hip_z + 0.06
    bm, src_idx, lm, wds = torso_shell(src, rig, under, name=name, sleeves=False, v_depth=0.22, v_halfwidth=0.09,
                                       bottom_z=bottom, base_off=0.017, extra=0.011)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.verts.index_update()
    uv = bm.loops.layers.uv.new("UVMap")
    cx = 0.0
    cy = sum(v.co.y for v in bm.verts) / max(1, len(bm.verts))
    loop_uvs_cylinder(bm, uv, list(bm.faces), (cx, cy), 0.17)
    srcvals = [v[src_idx] for v in bm.verts]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = new_obj(name, me, 92, "vest")
    assign_weights_from_src(o, srcvals, wds)
    fill_missing_weights(o, src)
    for p in o.data.polygons:
        p.use_smooth = True
    reduce_mesh(o, 0.45)
    neck_z = lm["neck"].z + 0.012
    cv = Canvas(-0.56, 0.56, bottom - 0.03, neck_z + 0.05, ppm=700)
    silver = (0.80 / color[0], 0.80 / color[1], 0.80 / max(color[2], 0.05))  # multiplier -> grey-silver after tint
    # horizontal reflective bands
    for zc in (bottom + 0.07, lm["chest"].z - 0.02):
        cv.rect(-0.56, zc - 0.025, 0.56, zc + 0.025, (0.85, 0.85, 0.85, 1.0))
        cv.line((-0.56, zc - 0.025), (0.56, zc - 0.025), 0.002, (0.6, 0.6, 0.6))
        cv.line((-0.56, zc + 0.025), (0.56, zc + 0.025), 0.002, (0.6, 0.6, 0.6))
    # shoulder braces (front and back)
    for s in (1, -1):
        for base in (0.0, 0.53):
            u = s * 0.095 if base == 0.0 else s * (0.53 - 0.095)
            cv.rect(u - 0.025, lm["chest"].z + 0.03, u + 0.025, neck_z + 0.05, (0.85, 0.85, 0.85, 1.0))
    # zip
    cv.line((0.0, bottom), (0.0, neck_z - 0.22), 0.004, (0.25, 0.25, 0.25))
    im, dom = cv.finish(name + "_detail")
    o["silver_mask"] = True
    attach_detail(o, im, dom, fabric="jersey_melange", color=color, smooth=0.35, bump=0.2, fabric_scale=0.12)
    return o


# ---------------------------------------------------------------- simple rigid accessories

def _finish_rigid(o, bone_weights, z_depth, kind, src=None):
    o["z_depth"] = z_depth
    o["kind"] = kind
    o.vertex_groups.clear()
    if bone_weights == "transfer" and src is not None:
        ct.transfer_weights(o, src)
    else:
        for n, w in bone_weights.items():
            g = o.vertex_groups.new(name=PRE + n)
            g.add([v.index for v in o.data.vertices], w, "REPLACE")
    for p in o.data.polygons:
        p.use_smooth = True


def hard_hat(rig, head_objs, *, name="HardHat", color=(1.0, 0.78, 0.05)):
    lm = body_landmarks(rig)
    # fit to head top: find head extents from body verts weighted to head
    hb = ct.bone_head(rig, PRE + "Head")
    ht = ct.bone_tail(rig, PRE + "Head")
    top = ht.z + 0.035
    bm = bmesh.new()
    seg, rings = 24, 8
    rx, ry, rz = 0.122, 0.142, 0.115
    cz = top - rz
    cyo = hb.y + 0.005
    verts = []
    for j in range(rings + 1):
        phi = (math.pi / 2) * j / rings
        row = []
        for i in range(seg):
            th = 2 * math.pi * i / seg
            x = rx * math.cos(phi) * math.sin(th)
            y = ry * math.cos(phi) * -math.cos(th)
            z = rz * math.sin(phi)
            row.append(bm.verts.new((x, cyo + y, cz + z)))
        verts.append(row)
    for j in range(rings):
        for i in range(seg):
            bm.faces.new((verts[j][i], verts[j][(i + 1) % seg], verts[j + 1][(i + 1) % seg], verts[j + 1][i]))
    # brim: ring outward (longer at the front peak)
    brim = []
    for i in range(seg):
        th = 2 * math.pi * i / seg
        front = max(0.0, -math.cos(th + math.pi))  # 1 at front (th=0)
        ext = 0.018 + 0.045 * (math.cos(th) * 0.5 + 0.5) ** 3
        x = (rx + ext) * math.sin(th)
        y = (ry + ext) * -math.cos(th)
        brim.append(bm.verts.new((x, cyo + y, cz - 0.004)))
    for i in range(seg):
        bm.faces.new((verts[0][i], brim[i], brim[(i + 1) % seg], verts[0][(i + 1) % seg]))
    # ridge on top
    bm.normal_update()
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            l[uv].uv = (l.vert.co.x * 2 + 0.5, (l.vert.co.y - cyo) * 2 + 0.5)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = new_obj(name, me, 100, "hat")
    sol = o.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.006
    sol.offset = 1.0
    ct.set_active(o)
    bpy.ops.object.modifier_apply(modifier=sol.name)
    _finish_rigid(o, {"Head": 1.0}, 100, "hat")
    attach_detail(o, None, None, color=color, smooth=0.55)
    return o


def _box(bm, center, size, rot=None):
    m = Matrix.Translation(center)
    if rot is not None:
        m = m @ rot
    res = bmesh.ops.create_cube(bm, size=1.0, matrix=m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))
    return res["verts"]


def duty_belt(src, rig, under, *, name="DutyBelt", color=(0.05, 0.05, 0.055)):
    """Belt band hugging the trousers at the hip, with buckle, holster, radio pouch and cuff case."""
    lm = body_landmarks(rig)
    hip_z = (lm["l_hip"].z + lm["r_hip"].z) * 0.5
    z = hip_z + 0.075
    bvh = bvh_without_arms([src] + list(under))
    cy = lm["hips"].y
    seg = 40
    bm = bmesh.new()
    rows = []
    for dz in (-0.024, 0.024):
        row = []
        for i in range(seg):
            th = 2 * math.pi * i / seg
            d = Vector((math.sin(th), -math.cos(th), 0))
            o = Vector((0, cy, z + dz)) + d * 0.6
            loc, nor, idx, dist = bvh.ray_cast(o, -d, 0.6)
            r = (0.6 - dist) if loc is not None else 0.16
            row.append(bm.verts.new(Vector((0, cy, z + dz)) + d * (r + 0.008)))
        rows.append(row)
    for i in range(seg):
        bm.faces.new((rows[0][i], rows[0][(i + 1) % seg], rows[1][(i + 1) % seg], rows[1][i]))
    bm.normal_update()
    # accessories (placed on the band surface)
    def on_band(th, out=0.0):
        i = th / (2 * math.pi) * seg
        a = rows[0][int(i) % seg].co
        b = rows[1][int(i) % seg].co
        c = (a + b) * 0.5
        d = Vector((math.sin(th), -math.cos(th), 0))
        return c + d * out, d
    pieces = []
    p, d = on_band(0.0, 0.006)
    pieces.append((p, (0.06, 0.012, 0.045)))                       # buckle
    for th, size in ((math.radians(80), (0.05, 0.07, 0.11)),       # holster (wearer's left? +x)
                     (math.radians(-75), (0.045, 0.05, 0.075)),    # radio pouch
                     (math.radians(-35), (0.04, 0.03, 0.05)),      # cuff case
                     (math.radians(150), (0.05, 0.035, 0.045))):   # back pouch
        p, d = on_band(th, size[1] * 0.5)
        rot = Matrix.Rotation(-th, 4, "Z")
        pieces.append((p + Vector((0, 0, -size[2] * 0.25)), size, rot))
    for pc in pieces:
        if len(pc) == 2:
            _box(bm, pc[0], pc[1])
        else:
            _box(bm, pc[0], pc[1], pc[2])
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            l[uv].uv = (math.atan2(l.vert.co.x, -(l.vert.co.y - cy)) * 0.17, l.vert.co.z)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = new_obj(name, me, 88, "belt")
    _finish_rigid(o, "transfer", 88, "belt", src)
    attach_detail(o, None, None, fabric="brown_leather", color=color, smooth=0.45, bump=0.3, fabric_scale=0.1)
    return o


def chest_badge(src, rig, under, *, name="Badge", side=1.0, gold=True):
    lm = body_landmarks(rig)
    z = lm["chest"].z + 0.075
    x = side * 0.095
    bvh = bvh_of([src] + list(under))
    o0 = Vector((x, lm["chest"].y - 0.5, z))
    loc, nor, idx, dist = bvh.ray_cast(o0, Vector((0, 1, 0)), 1.0)
    if loc is None:
        loc, nor = Vector((x, lm["chest"].y - 0.12, z)), Vector((0, -1, 0))
    bm = bmesh.new()
    # shield shape (flat prism)
    pts = [(-0.028, 0.03), (0.028, 0.03), (0.03, 0.0), (0.016, -0.026), (0.0, -0.036), (-0.016, -0.026), (-0.03, 0.0)]
    n = nor.normalized()
    up = Vector((0, 0, 1))
    t = up.cross(n).normalized()
    u = n.cross(t).normalized()
    front, back = [], []
    for px, py in pts:
        base = loc + t * px + u * py
        back.append(bm.verts.new(base + n * 0.002))
        front.append(bm.verts.new(base + n * 0.006))
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    k = len(pts)
    for i in range(k):
        bm.faces.new((back[i], back[(i + 1) % k], front[(i + 1) % k], front[i]))
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            p = l.vert.co - loc
            l[uv].uv = (p.dot(t) * 10 + 0.5, p.dot(u) * 10 + 0.5)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = new_obj(name, me, 99, "badge")
    _finish_rigid(o, {"Spine2": 1.0}, 99, "badge")
    attach_detail(o, None, None, color=(0.95, 0.75, 0.30) if gold else (0.82, 0.83, 0.86), smooth=0.8, metal=1.0)
    return o


def lanyard(src, rig, outer, *, name="Lanyard", strap_color=(0.10, 0.35, 0.65)):
    """Strap from the back of the neck over the collar down to an ID card at mid-chest, sitting on 'outer'."""
    lm = body_landmarks(rig)
    neck = lm["neck"]
    card_c = Vector((0.0, 0.0, lm["chest"].z - 0.02))
    bvh = bvh_of([src] + list(outer))

    def surf(p, direction=Vector((0, 1, 0))):
        o0 = Vector((p.x, p.y - 0.5, p.z))
        loc, nor, idx, dist = bvh.ray_cast(o0, direction, 1.0)
        return (loc + nor * 0.004) if loc is not None else p

    # path points (one side), mirrored
    bm = bmesh.new()
    width = 0.016
    strips = []
    card_top = None
    for s in (1, -1):
        pts = []
        # around the back of the neck
        for k in range(7):
            a = math.pi * (k / 6.0)  # from back (0) to side (pi/2) to front-ish
            ang = math.pi - a * 0.5
            th = s * ang
            d = Vector((math.sin(th), -math.cos(th), 0))
            p0 = Vector((0, neck.y, neck.z + 0.005)) + d * 0.6
            loc, nor, idx, dist = bvh.ray_cast(p0, -d, 0.6)
            p = (loc + nor * 0.004) if loc is not None else Vector((0, neck.y, neck.z)) + d * 0.08
            pts.append(p)
        # down the chest to the card top
        start = pts[-1]
        for k in range(1, 7):
            t = k / 6.0
            q = start.lerp(Vector((s * 0.012, start.y, card_c.z + 0.05)), t)
            pts.append(surf(q))
        strips.append(pts)
        card_top = pts[-1]
    for pts in strips:
        prev = None
        for i, p in enumerate(pts):
            a = pts[max(0, i - 1)]
            b = pts[min(len(pts) - 1, i + 1)]
            tan = (b - a).normalized()
            n = Vector((p.x, p.y - neck.y, 0)).normalized() if i < 7 else Vector((0, -1, 0))
            side = tan.cross(n).normalized() * width * 0.5
            v1, v2 = bm.verts.new(p - side), bm.verts.new(p + side)
            if prev:
                bm.faces.new((prev[0], prev[1], v2, v1))
            prev = (v1, v2)
    # card: 5.4 x 8.6 cm, hanging plate in front of the chest
    cc = surf(card_c) + Vector((0, -0.006, 0))
    _box(bm, cc, (0.056, 0.004, 0.084))
    _box(bm, cc + Vector((0, -0.002, 0.048)), (0.016, 0.004, 0.012))
    bm.normal_update()
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            p = l.vert.co - cc
            l[uv].uv = (p.x / 0.056 + 0.5, p.z / 0.084 + 0.5)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = new_obj(name, me, 97, "lanyard")
    _finish_rigid(o, {"Spine2": 0.6, "Neck": 0.4}, 97, "lanyard")
    # card face detail: photo, stripe, bars; strap colour elsewhere (UV outside 0..1 -> strap colour)
    cv = Canvas(-0.5, 1.5, -0.5, 1.5, ppm=200)
    cv.rect(-0.5, -0.5, 1.5, 1.5, strap_color)
    cv.rect(0.0, 0.0, 1.0, 1.0, (0.96, 0.96, 0.97))
    cv.rect(0.0, 0.78, 1.0, 1.0, (0.10, 0.42, 0.40))
    cv.rect(0.12, 0.30, 0.52, 0.70, (0.45, 0.40, 0.38))
    for i in range(3):
        cv.rect(0.58, 0.60 - i * 0.1, 0.90, 0.64 - i * 0.1, (0.25, 0.25, 0.28))
    cv.rect(0.12, 0.10, 0.88, 0.18, (0.15, 0.15, 0.16))
    im, dom = cv.finish(name + "_detail")
    attach_detail(o, im, dom, color=(1, 1, 1), smooth=0.5)
    return o


def tuck_under(inners, outer, max_dist=0.035, gap=0.006, z_max=99.0):
    """Pull vertices of inner garments that poke through 'outer' back underneath it (jacket hems under a lab coat...)."""
    bvh = ct.evaluated_world_mesh_bvh([outer])
    if bvh is None:
        return 0
    moved = 0
    for o in inners:
        if o is None:
            continue
        mw = o.matrix_world
        inv = mw.inverted()
        me = o.data
        for v in me.vertices:
            p = mw @ v.co
            if p.z > z_max:  # leave collars / ties in the neck opening alone
                continue
            loc, nor, idx, dist = bvh.find_nearest(p, max_dist)
            if loc is None:
                continue
            if (p - loc).dot(nor) > -gap:
                v.co = inv @ (loc - nor * gap)
                moved += 1
        me.update()
    ct.log("tuck_under", outer.name, "moved", moved, "verts")
    return moved
