"""Original manufactured road vehicles, authored in Blender (Unity metres).

CPU only. Generates five vehicle FBX assemblies plus a taxi sign, three LODs, a packed source
blend and one shared 2K surface-detail atlas. No scene, physics or mission edits.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
MATERIALS = ("VehiclePaint", "VehicleTrim", "VehicleRubber", "VehicleAlloy",
             "VehicleInterior", "VehicleHeadlamp", "VehicleTaillamp",
             "VehicleAmber", "VehiclePlate", "VehicleGlass")
SPECS = {
    "Sedan": dict(length=4.4, width=1.85, belt=.91, roof=1.45, radius=.34, wheelbase=2.7,
                  cab_rear=-1.30, cab_front=.78, roof_rear=-.98, roof_front=.28,seat_front=.045),
    "Hatch": dict(length=4.05, width=1.76, belt=.94, roof=1.49, radius=.31, wheelbase=2.55,
                  cab_rear=-1.76, cab_front=.66, roof_rear=-1.38, roof_front=-.02),
    "Suv": dict(length=4.65, width=1.9, belt=1.10, roof=1.76, radius=.37, wheelbase=2.8,
                cab_rear=-2.03, cab_front=.77, roof_rear=-1.66, roof_front=.02),
    "Van": dict(length=4.9, width=1.85, belt=1.08, roof=1.85, radius=.34, wheelbase=3.2,
                cab_rear=-2.45, cab_front=1.45, roof_rear=-2.36, roof_front=.81),
    "BoxTruck": dict(length=8.1, width=2.45, belt=1.97, roof=2.91, radius=.48, wheelbase=6.1,
                     cab_rear=1.78, cab_front=3.92, roof_rear=1.94, roof_front=3.24),
}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def bc(p):
    return Vector((p[0], -p[2], p[1]))


class Geometry:
    def __init__(self):
        self.v, self.f, self.m, self.smooth = [], [], [], []

    def mesh(self, vertices, faces, material, smooth=False):
        offset = len(self.v)
        self.v.extend(tuple(bc(p)) for p in vertices)
        self.f.extend(tuple(offset + i for i in f) for f in faces)
        self.m.extend([material] * len(faces) if isinstance(material,int) else material)
        self.smooth.extend([smooth] * len(faces))

    def box(self, centre, size, material, bevel=.01, rotation=None, segments=2):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        transform = Matrix.Identity(3) if rotation is None else rotation
        for v in bm.verts:
            p = transform @ Vector(tuple(v.co[i] * size[i] for i in range(3))) + Vector(centre)
            v.co = p
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * .43),
                            segments=segments, affect="EDGES", profile=.5)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.verts.ensure_lookup_table()
        self.mesh([v.co[:] for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces], material)
        bm.free()

    def path(self, points, radius, material, sides=6, closed=False):
        points = [Vector(p) for p in points]
        vs, fs = [], []
        for i, p in enumerate(points):
            tangent = points[(i + 1) % len(points)] - points[i - 1] if closed else points[min(i + 1, len(points)-1)] - points[max(i-1, 0)]
            tangent.normalize()
            axis = tangent.cross(Vector((0, 1, 0)))
            if axis.length < .01:
                axis = tangent.cross(Vector((0, 0, 1)))
            axis.normalize()
            other = tangent.cross(axis).normalized()
            for j in range(sides):
                angle = 2 * math.pi * j / sides
                vs.append(p + radius * (axis * math.cos(angle) + other * math.sin(angle)))
        for i in range(len(points) if closed else len(points)-1):
            for j in range(sides):
                fs.append((i*sides+j, i*sides+(j+1)%sides,
                           ((i+1)%len(points))*sides+(j+1)%sides, ((i+1)%len(points))*sides+j))
        if not closed:
            fs.extend((tuple(range(sides-1, -1, -1)), tuple((len(points)-1)*sides+j for j in range(sides))))
        self.mesh(vs, fs, material, True)

    def surface(self, fn, nu, nv, material, reverse=False, thickness=0, inner_material=None):
        vertices = [fn(i/nu, j/nv) for j in range(nv+1) for i in range(nu+1)]
        faces = []
        for j in range(nv):
            for i in range(nu):
                a = j*(nu+1)+i
                face = (a, a+1, a+nu+2, a+nu+1)
                faces.append(tuple(reversed(face)) if reverse else face)
        if thickness:
            count=len(vertices);top_faces=list(faces)
            vertices += [(p[0],p[1]-thickness,p[2]) for p in vertices]
            faces += [tuple(count+i for i in reversed(face)) for face in top_faces]
            edges={}
            for face in top_faces:
                for a,b in zip(face,(*face[1:],face[0])):
                    key=tuple(sorted((a,b)));edges.setdefault(key,[]).append((a,b))
            sides=[(b,a,a+count,b+count) for group in edges.values() if len(group)==1 for a,b in group]
            faces += sides
            materials=[material]*len(top_faces)+[material if inner_material is None else inner_material]*len(top_faces)+[material]*len(sides)
            self.mesh(vertices,faces,materials,True)
            self.smooth[-len(sides):]=[False]*len(sides)
        else:self.mesh(vertices, faces, material, True)

    def text(self, value, centre, size, material, rear=False):
        curve=bpy.data.curves.new("Manufactured registration lettering","FONT")
        curve.body=value;curve.size=size;curve.align_x="CENTER";curve.align_y="CENTER";curve.extrude=.0003;curve.resolution_u=2
        ob=bpy.data.objects.new("Temporary registration type",curve);bpy.context.scene.collection.objects.link(ob)
        evaluated=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
        vertices=[(centre[0]+(-v.co.x if rear else v.co.x),centre[1]+v.co.y,centre[2]+(-v.co.z if rear else v.co.z)) for v in mesh.vertices]
        faces=[tuple(reversed(p.vertices)) if rear else tuple(p.vertices) for p in mesh.polygons]
        self.mesh(vertices,faces,material)
        evaluated.to_mesh_clear();bpy.data.objects.remove(ob,do_unlink=True);bpy.data.curves.remove(curve)

    def object(self, name, materials):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.v, [], self.f)
        mesh.update()
        for material in materials:
            mesh.materials.append(material)
        for p, mat, smooth in zip(mesh.polygons, self.m, self.smooth):
            p.material_index = mat
            p.use_smooth = smooth
        # Metre-based tri-planar UVs retain repeat scale on each manufactured part.
        uv = mesh.uv_layers.new(name="SurfaceDetailMetres")
        for p in mesh.polygons:
            n = p.normal
            axis = max(range(3), key=lambda k: abs(n[k]))
            for li in p.loop_indices:
                v = mesh.vertices[mesh.loops[li].vertex_index].co
                uv.data[li].uv = (v.y, v.z) if axis == 0 else (v.x, v.z) if axis == 1 else (v.x, v.y)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        ob = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def blend(a, b, t):
    return tuple(a[i]*(1-t)+b[i]*t for i in range(3))


def _baseline_passenger(spec, lod):
    g, glass = Geometry(), Geometry()
    s = spec
    hw, hl = s["width"]*.5, s["length"]*.5
    r, belt, roof = s["radius"], s["belt"], s["roof"]
    cr, cf, rr, rf = (s[k] for k in ("cab_rear", "cab_front", "roof_rear", "roof_front"))
    van = s is SPECS["Van"]
    suv = s is SPECS["Suv"]
    wz = s.get("arch_axles", [-s["wheelbase"]*.5, s["wheelbase"]*.5])
    sill = .27 if not suv else .39
    n = (112, 64, 38)[lod]
    # Include exact arch endpoints and crowns, instead of sampling through tyres.
    zs = sorted(set([-hl+i*s["length"]/n for i in range(n+1)] + [cr, cf] +
                    [z + d*(r+.055) for z in wz for d in (-1, 0, 1)]))
    rings = []
    for z in zs:
        end = abs(z)/hl
        w = hw*(1-.052*end**8)
        top = belt-.055*end**8
        bottom = sill
        for axle in wz:
            delta = abs(z-axle)
            if delta < r+.055:
                bottom = max(bottom, r+math.sqrt((r+.055)**2-delta**2))
        bottom = min(bottom, top-.06)
        rings.append([(-w+.045,bottom,z),(-w,bottom+.045,z),(-w+.012,top-.12,z),
                      (-w+.09,top,z),(w-.09,top,z),(w-.012,top-.12,z),(w,bottom+.045,z),(w-.045,bottom,z)])
    vertices = [p for row in rings for p in row]
    faces = []
    for i in range(len(rings)-1):
        for j in range(8):
            if j == 3:
                continue  # Separate crowned hood/deck; cabin aperture stays genuinely open.
            faces.append((i*8+j, i*8+(j+1)%8, (i+1)*8+(j+1)%8, (i+1)*8+j))
    faces += [tuple(range(7,-1,-1)), tuple((len(rings)-1)*8+j for j in range(8))]
    g.mesh(vertices, faces, 0, True)
    for z0,z1 in ((-hl,cr),(cf,hl)):
        def sheet(u,v):
            z=z0+(z1-z0)*v;end=abs(z)/hl
            w=hw*(1-.052*end**8)-.09
            return ((u*2-1)*w,belt-.055*end**8+.025*math.sin(math.pi*u),z)
        g.surface(sheet,(20,12,6)[lod],(30,16,7)[lod],0,reverse=True)
    g.box((0,.33,(cr+cf)/2), (s["width"]-.25,.06,cf-cr), 4, .01)
    # Pressed roof with a real crown; no opaque shell behind windscreen.
    def roof_fn(u,v):
        x = (u*2-1)*(hw-.23)
        z = rr+(rf-rr)*v
        return (x, roof+.025*math.sin(math.pi*u)+.012*math.sin(math.pi*v), z)
    # A real closed roof includes sheet metal, edge returns and a dark inner
    # headliner. Its underside must be visible through the glass from below.
    g.surface(roof_fn, (20,12,6)[lod], (20,12,5)[lod], 0, reverse=True,thickness=.018,inner_material=4)
    # Windows, gaskets, structural pillars and drip rails are separate geometry.
    def windscreen(rear=False):
        basez, topz = (cr,rr) if rear else (cf,rf)
        sign = -1 if rear else 1
        def fn(u,v):
            w = (hw-.105)*(1-v)+(hw-.235)*v
            return ((u*2-1)*w, belt+.009+(roof-belt-.019)*v+.025*math.sin(math.pi*u),
                    basez+(topz-basez)*v+sign*.027*math.sin(math.pi*u)*math.sin(math.pi*v))
        glass.surface(fn, (18,10,5)[lod], (10,6,3)[lod], 9, reverse=rear)
        for u in (0,1):
            g.path([fn(u,i/10) for i in range(11)], .028 if not van else .041, 0, (8,6,4)[lod])
        for v in (0,1):
            g.path([fn(i/16,v) for i in range(17)], .013, 1, (6,5,4)[lod])
    windscreen()
    if not van:
        windscreen(True)
    else:
        def backpoint(u,v):
            return ((u*2-1)*((hw-.09)*(1-v)+(hw-.23)*v),
                    belt+.035+(roof-belt-.055)*v+.025*math.sin(math.pi*u)*v,cr+.035+(rr-cr-.035)*v)
        g.surface(backpoint,(16,10,5)[lod],(10,6,3)[lod],0)
        def rear_lower_seal(u,v):
            lower=((u*2-1)*(hw*.948-.09),belt-.055,-hl)
            return blend(lower,backpoint(u,0),v)
        def rear_upper_seal(u,v):
            low=backpoint(u,1)
            return (low[0],low[1]+.020*v,low[2])
        g.surface(rear_lower_seal,(20,12,6)[lod],2,0)
        g.surface(rear_upper_seal,(20,12,6)[lod],1,0)
        g.path([(0,.48,-hl-.007),(0,belt+.005,-hl-.007),backpoint(.5,.06),backpoint(.5,.96)],.004,1)
        for side in (-1,1):
            u=.04 if side<0 else .96
            g.path([backpoint(u,v) for v in (.02,.25,.5,.75,.96)],.003,1,4)
            for v in (.02,.96):g.path([backpoint(i/16,v) for i in range(1,16)],.003,1,4)
            if lod<2:
                u=.32 if side<0 else .68
                g.path([backpoint(u,v) for v in (.10,.35,.6,.84)],.009,3,6)
    bz = .15 if van else rr+(rf-rr)*.48
    for side in (-1,1):
        # Side perimeter follows glass surface, with soft transverse curvature.
        def side_fn(u,v):
            rearbase = cr+.035 if van else cr+.07
            frontbase = cf-.055
            reartop, fronttop = rr if van else rr+.03, rf-.035
            return (side*((hw-.09)*(1-v)+(hw-.23)*v+.015*math.sin(math.pi*u)*math.sin(math.pi*v)),
                    belt+.035+(roof-belt-.055)*v,
                    (rearbase*(1-u)+frontbase*u)*(1-v)+(reartop*(1-u)+fronttop*u)*v)
        # Van has a real front cabin and pressed opaque cargo sides.
        if van:
            rear_u = (bz-cr)/(cf-cr)
            g.surface(lambda u,v: side_fn(u*rear_u,v),(18,10,5)[lod],(10,6,3)[lod],0,reverse=side>0)
        else:
            rear_u = 0
        # Weather seals are backed by opaque strips right down to the lower
        # shell and up to the crowned roof. No daylight slit remains behind a
        # decorative gasket, including the tapered cargo panels.
        intervals=[(0,rear_u,0),(rear_u,1,1)] if van else [(0,1,1)]
        for u0,u1,material in intervals:
            def lower_seal(u,v):
                x,_,z=side_fn(u0+(u1-u0)*u,0)
                bottom=belt-.055*(abs(z)/hl)**8-.001
                lower_x=side*(hw*(1-.052*(abs(z)/hl)**8)-.09)
                return (lower_x+(x-lower_x)*v,bottom+(belt+.035-bottom)*v,z)
            def upper_seal(u,v):
                x,y,z=side_fn(u0+(u1-u0)*u,1)
                edge=roof+.012*math.sin(math.pi*max(0,min(1,(z-rr)/(rf-rr))))+.001
                return (x,y+(edge-y)*v,z)
            g.surface(lower_seal,(20,12,6)[lod],1,material,reverse=side>0)
            g.surface(upper_seal,(20,12,6)[lod],1,material,reverse=side>0)
        if van:
            z=cr+.035
            a=(side*(hw*.948-.09),belt-.055,-hl)
            b=(side*(hw*(1-.052*(abs(z)/hl)**8)-.09),belt-.055*(abs(z)/hl)**8-.001,z)
            g.mesh([a,b,side_fn(0,0)],[(0,1,2) if side>0 else (2,1,0)],0)
        for u0,u1 in ((rear_u,.53),(.57,1)) if not van else ((rear_u,1),):
            if u1 <= u0:
                continue
            glass.surface(lambda u,v: side_fn(u0+(u1-u0)*u,v), (12,7,4)[lod], (6,4,2)[lod],9,reverse=side>0)
            for u in (u0,u1):
                g.path([side_fn(u,i/6) for i in range(7)],.010,1,(6,5,4)[lod])
        for v in (0,1):
            g.path([side_fn(i/20,v) for i in range(21)],.013,1,(6,5,4)[lod])
        if not van:
            for u in (.55,):
                g.path([side_fn(u,i/8) for i in range(9)], .034,1,(8,6,4)[lod])
        # Door shut-lines, belt moulding, rocker, and restrained polished trim.
        g.path([(side*(hw-.003),sill+.05,-hl+.20),(side*(hw-.003),sill+.05,hl-.20)],.021,1)
        if lod < 2:
            seams = [cr+.12,bz,cf-.12] if not van else [cr+.12,bz,cf-.14]
            for z in seams:
                if any(abs(z-a)<r+.09 for a in wz):
                    low = r+math.sqrt(max(0,(r+.075)**2-min(abs(z-a) for a in wz)**2))
                else:
                    low = sill+.06
                g.path([(side*(hw+.003),low,z),(side*(hw+.003),belt-.045,z)],.0028,1,4)
            for z in ([bz+.31,cr+.45] if not van else [cf-.48,-.28]):
                g.box((side*(hw+.009),belt-.10,z),(.018,.043,.15),1,.012)
                g.box((side*(hw+.024),belt-.092,z),(.029,.020,.12),3,.008)
        # Side mirror shell, mount and rear-facing reflective glass.
        mx,mz = side*(hw+.055),cf-.20
        g.path([(side*(hw-.03),belt+.095,mz-.04),(mx,belt+.10,mz)],.018,1)
        g.box((mx,belt+.16,mz),(.16,.095,.205),0,.035,segments=3 if lod==0 else 2)
        g.box((mx,belt+.16,mz-.099),(.13,.071,.009),3,.003)
        for axle in wz:
            points = [(side*(hw+.006),r+(r+.058)*math.sin(a),axle+(r+.058)*math.cos(a))
                      for a in [math.pi*i/(24 if lod==0 else 14) for i in range((24 if lod==0 else 14)+1)]]
            g.path(points,.017 if suv else .010,1 if suv else 0,(7,5,4)[lod])
        # Front and rear lamps have dark recesses and separate lenses/projectors.
        fx = side*(hw-.26)
        for z,mat in ((hl-.013,5),(-hl+.013,6)):
            g.box((fx,belt-.145,z),(.34,.125,.045),1,.028)
            if z>0 and lod<2:
                g.box((fx,belt-.142,z+.025),(.30,.088,.012),3,.020)
                for dx in (-.072,.072):
                    g.path([(fx+dx,belt-.145,hl+.024),(fx+dx,belt-.145,hl+.032)],.037,1,(20,12)[lod])
                    g.path([(fx+dx+.031*math.cos(a),belt-.145+.031*math.sin(a),hl+.033)
                            for a in [2*math.pi*i/(24 if lod==0 else 12) for i in range(24 if lod==0 else 12)]],.004,3,5,True)
                    g.path([(fx+dx,belt-.145,hl+.034),(fx+dx,belt-.145,hl+.038)],.022,5,(20,12)[lod])
                glass.box((fx,belt-.142,hl+.046),(.30,.088,.003),9,.018)
            else:
                g.box((fx,belt-.142,z+(1 if z>0 else -1)*.025),(.30,.088,.016),mat,.020)
        if lod<2:
            g.path([(fx-.115,belt-.10,hl+.02),(fx+.115,belt-.10,hl+.02)],.009,5)
            g.box((side*(hw-.1),belt-.148,hl+.012),(.042,.07,.020),7,.008)
    # Sculpted bumpers, actual recessed radiator opening, mesh slats and plates.
    for direction in (-1,1):
        z=direction*(hl-.018)
        g.box((0,sill+.105,z),(s["width"]-.07,.23,.105),0,.042,segments=3 if lod==0 else 2)
        g.box((0,sill+.065,z+direction*.054),(s["width"]-.18,.075,.045),1,.015)
        g.box((0,sill+.20,z+direction*.061),(.48,.108,.015),8,.009)
        if lod<2:g.text("PN 7134",(0,sill+.20,z+direction*.071),.066,1,rear=direction<0)
        if lod<2:
            for x in (-.204,.204):
                g.box((x,sill+.20,z+direction*.072),(.010,.01,.004),1,.001)
    g.box((0,belt-.18,hl+.015),(.67,.16,.042),1,.027)
    for y in ([belt-.245,belt-.18,belt-.115] if lod<2 else [belt-.18]):
        g.box((0,y,hl+.039),(.63,.012,.008),3,.003)
    # Fine hood and tail seams follow separate sheet-metal panels.
    if lod<2:
        for z in (cf+.015,cr-.015):
            g.path([(-hw+.16,belt+.002,z),(0,belt+.009,z),(hw-.16,belt+.002,z)],.003,1,4)
        for x in (-hw+.19,hw-.19):
            g.path([(x,belt+.007,cf+.07),(x*.93,belt-.013,hl-.14)],.0024,1,4)
        # Wipers are visible geometry on top of the front laminated pane.
        for x in (-.40,.22):
            g.path([(x,belt+.045,cf+.007),(x+.10,belt+.14,cf-.16),(x+.31,belt+.21,cf-.29)],.006,1,5)
    # Cabin contains shaped cushions, backs, headrests, dashboard and steering.
    seat_base=.51 if not suv else .67
    front_z=s.get("seat_front",rf+.025)
    for z in (front_z, max(cr+.43,front_z-.84)) if not van else (front_z,):
        for x in (-.40,.40):
            g.box((x,seat_base,z),(.48,.13,.48),4,.055,segments=3 if lod==0 else 2)
            g.box((x,seat_base+.24,z-.19),(.46,.48,.115),4,.04)
            g.box((x,seat_base+.53,z-.20),(.23,.16,.10),4,.035)
    g.box((0,belt-.035,cf-.15),(s["width"]-.27,.16,.32),4,.045)
    g.box((0,seat_base-.03,front_z+.04),(.17,.30,.73),4,.025)
    g.path([(-.40,belt-.04,cf-.28),(-.40,belt+.06,cf-.47)],.027,4)
    centre=Vector((-.40,belt+.09,cf-.46))
    g.path([centre+Vector((.145*math.cos(a),.145*math.sin(a),0))
            for a in [2*math.pi*i/(24 if lod==0 else 14) for i in range(24 if lod==0 else 14)]],.015,4,6,True)
    for x in (-.40,0):
        g.box((x,belt-.02,cf-.318),(.21,.08,.009),1,.008)
    if van:
        # Pressing ribs and cargo-door hardware, without fake windows.
        for x in (-.59,-.29,0,.29,.59):
            g.path([(x,roof+.009,cr+.15),(x,roof+.014,rf-.15)],.012,0,5)
        for side in (-1,1):
            g.box((side*(hw+.001),.98,-.74),(.016,.012,2.1),3,.004)
            for y in (.68,1.49):
                if y<belt:
                    centre=(side*(hw*.948-.03),y,-hl-.018)
                else:
                    t=(y-belt)/(roof-belt)
                    width=(hw-.10)*(1-t)+(hw-.23)*t
                    centre=(side*(width-.022),y,cr+.035+(rr-cr-.035)*t-.018)
                g.box(centre,(.055,.09,.038),3,.006)
    if suv:
        for side in (-1,1):
            g.path([(side*(hw-.27),roof+.06,rr+.16),(side*(hw-.27),roof+.06,rf-.15)],.023,3)
    return g, glass, [(side*(hw-.10),r,z) for side in (-1,1) for z in wz]


# Van-only primary-form revision. Unity-space metres; no new material slots.
# This fragment is assembled into the staged full generator. Not executed.

def van_bezier(a, b, c, d, t):
    s=1-t
    return tuple(s*s*s*a[i]+3*s*s*t*b[i]+3*s*t*t*c[i]+t*t*t*d[i] for i in range(len(a)))


def van_smooth(t):
    t=max(0.0,min(1.0,t))
    return t*t*(3-2*t)


def van_upper_profile(t):
    """Continuous wall tumblehome, rolled roof corner and shallow centre crown.
    Returns positive half-width,height. Peak1.875 + longitudinal crown.012
    retains the existing1.887m maximum, with a visible corner roll below it.
    """
    if t<=.55:
        return van_bezier((.870,1.115),(.870,1.350),(.850,1.620),(.820,1.730),t/.55)
    if t<=.75:
        return van_bezier((.820,1.730),(.820,1.800),(.760,1.840),(.660,1.855),(t-.55)/.20)
    return van_bezier((.660,1.855),(.540,1.870),(.240,1.875),(0,1.875),(t-.75)/.25)


def van_front_z(t):
    return 1.780-.400*van_smooth(t/.60)


def van_rear_z(t):
    return -2.450+.090*t**1.2


def van_roof_cross(q, along):
    t=.60+.40*(1-abs(q));x,y=van_upper_profile(t)
    z=van_rear_z(t)+(1.380-van_rear_z(t))*along
    return ((-1 if q<0 else 1)*x,y+.012*math.sin(math.pi*along)*t*t,z)


def van_lower_top(z):
    return 1.080+.035*van_smooth((z-1.30)/.48)-.110*van_smooth((z-1.78)/.67)


def van_lower_width(y,z):
    t=max(0,min(1,(y-.27)/.81))
    return .925-.045*(1-van_smooth(t/.25))-.055*van_smooth((t-.45)/.55)-.035*(abs(z)/2.45)**8


def van_closed_shoulder(geometry, fn, nu, nv, side):
    """Keep the exterior shoulder exact and give its disconnected sheet a closed inward backing."""
    outer=[fn(i/nu,j/nv) for j in range(nv+1) for i in range(nu+1)]
    count=len(outer)
    # +Y enters the body at the horizontal front endpoint, where an X-only
    # backing would have a coplanar/degenerate cap. The side strip stays inside
    # the old vehicle envelope; every exterior point remains unchanged.
    vertices=outer+[(p[0]-side*.012,p[1]+.004,p[2]) for p in outer]
    outside=[]
    for j in range(nv):
        for i in range(nu):
            a=j*(nu+1)+i;face=(a,a+1,a+nu+2,a+nu+1)
            outside.append(tuple(reversed(face)) if side>0 else face)
    inside=[tuple(count+i for i in reversed(face)) for face in outside]
    edges={}
    for face in outside:
        for a,b in zip(face,(*face[1:],face[0])):
            edges.setdefault(tuple(sorted((a,b))),[]).append((a,b))
    caps=[(b,a,a+count,b+count) for uses in edges.values() if len(uses)==1 for a,b in uses]
    geometry.mesh(vertices,outside+inside+caps,[0]*len(outside)+[4]*len(inside)+[0]*len(caps),True)
    geometry.smooth[-len(caps):]=[False]*len(caps)


def van_candidate(spec,lod):
    g,glass=Geometry(),Geometry();s=spec;hw=s['width']*.5;hl=s['length']*.5;r=s['radius'];belt=s['belt']
    axles=[-s['wheelbase']*.5,s['wheelbase']*.5]
    nz,ny=(96,60,32)[lod],(12,8,5)[lod]
    zs=sorted(set([-hl+i*s['length']/nz for i in range(nz+1)]+[.42,1.30,1.78]+[z+d*(r+.055) for z in axles for d in (-1,0,1)]))
    def arch_bottom(z):
        y=.27
        for axle in axles:
            delta=abs(z-axle)
            if delta<r+.055:y=max(y,r+math.sqrt((r+.055)**2-delta*delta))
        return min(y,van_lower_top(z)-.065)
    # Nonconstant manufactured cross-section: tucked sill, door barrel and
    # shoulder turn. Wheel-arch openings remain at original axle coordinates.
    for side in (-1,1):
        vertices=[]
        for z in zs:
            low,top=arch_bottom(z),van_lower_top(z)
            for j in range(ny+1):
                y=low+(top-low)*j/ny
                vertices.append((side*van_lower_width(y,z),y,z))
        faces=[]
        for i in range(len(zs)-1):
            for j in range(ny):
                a=i*(ny+1)+j;f=(a,a+ny+1,a+ny+2,a+1)
                faces.append(tuple(reversed(f)) if side>0 else f)
        g.mesh(vertices,faces,0,True)
    # Closed lower front/rear sections; the fascia sits in front of these
    # backings, with real inset returns rather than floating lamp rectangles.
    for direction in (-1,1):
        z=direction*hl;low=arch_bottom(z);top=van_lower_top(z)
        points=[(-van_lower_width(low,z),low,z),(van_lower_width(low,z),low,z)]
        points += [(van_lower_width(low+(top-low)*j/ny,z),low+(top-low)*j/ny,z) for j in range(1,ny+1)]
        points += [(-van_lower_width(low+(top-low)*j/ny,z),low+(top-low)*j/ny,z) for j in range(ny,-1,-1)]
        g.mesh(points,[tuple(range(len(points))) if direction>0 else tuple(range(len(points)-1,-1,-1))],0)
    g.box((0,.33,-.30),(1.35,.06,4.20),4,.01)
    # Continuous roof sheet and headliner;75mm of rolled corner/crown is
    # expressed downward inside the unchanged height envelope, not by scaling.
    g.surface(lambda u,v:van_roof_cross(u*2-1,v),(32,20,10)[lod],(16,12,8)[lod],0,reverse=True,thickness=.018,inner_material=4)
    for side in (-1,1):
        def cargo(u,v):
            t=.60*v;x,y=van_upper_profile(t);rear=van_rear_z(t);front=.420-.06*van_smooth(v)
            z=rear+(front-rear)*u
            # The wall's longitudinal crown is finite-scale shape, not noise.
            return (side*(x+.018*math.sin(math.pi*u)*math.sin(math.pi*v)),y,z)
        g.surface(cargo,(20,16,10)[lod],(16,10,5)[lod],0,reverse=side>0)
        # Closed shoulder shell joins the old wheel envelope to the canopy.
        # The outer grid is unchanged; inward backing fixes open-sheet recalc orientation.
        def shoulder(u,v):
            z=-hl+(1.78+hl)*u;low=van_lower_top(z);x0=van_lower_width(low,z)
            return (side*(x0+(.870-x0)*v),low+(1.115-low)*v,z)
        van_closed_shoulder(g,shoulder,(48,28,14)[lod],2,side)
        # Actual front side-glass aperture. All surrounding returns are opaque.
        def side_window(u,v):
            t=.60*v;x,y=van_upper_profile(t)
            back=.420-.060*van_smooth(v);front=van_front_z(t)
            return (side*(x-.018),y+.014*(1-v)-.014*v,back+(front-back)*u)
        glass.surface(side_window,(14,9,5)[lod],(10,6,3)[lod],9,reverse=side>0)
        # Side-window perimeter follows its curved surface and is fully backed.
        for u in (0,1):
            def jamb(a,b):
                inner=side_window(u,a);t=.60*a;x,y=van_upper_profile(t)
                outer=(side*x,y,(.420-.060*van_smooth(a)) if u==0 else van_front_z(t))
                return blend(inner,outer,b)
            g.surface(jamb,(14,9,5)[lod],2,0,reverse=side*(2*u-1)<0)
            g.path([side_window(u,i/16) for i in range(17)],.009,1,(6,5,4)[lod])
        for v in (0,1):
            def window_return(u,w):
                inner=side_window(u,v);t=.60*v;x,y=van_upper_profile(t)
                back=.420-.060*van_smooth(v);front=van_front_z(t)
                return blend(inner,(side*x,y,back+(front-back)*u),w)
            g.surface(window_return,(14,9,5)[lod],2,0,reverse=side*(2*v-1)>0)
            g.path([side_window(i/16,v) for i in range(17)],.009,1,(6,5,4)[lod])
        # Full-body door shut lines follow the curved panel instead of sitting
        # on a fixed x plane. Only the original functional handle is retained.
        if lod<2:
            for z in (.42,1.72):
                low=arch_bottom(z)+.035;top=van_lower_top(z)-.025
                g.path([(side*(van_lower_width(low+(top-low)*i/14,z)+.0015),low+(top-low)*i/14,z) for i in range(15)],.0028,1,4)
            z=1.34;y=1.00
            g.box((side*(van_lower_width(y,z)+.009),y,z),(.018,.040,.15),1,.012)
        # Original mirror outer extent remains exactly ±1.06m.
        mx=side*(hw+.055);mz=1.25
        g.path([(side*(hw-.03),belt+.095,mz-.04),(mx,belt+.10,mz)],.018,1)
        g.box((mx,belt+.16,mz),(.16,.095,.205),0,.035,segments=3 if lod==0 else 2)
        g.box((mx,belt+.16,mz-.099),(.13,.071,.009),3,.003)
        for axle in axles:
            points=[(side*(van_lower_width(r+(r+.058)*math.sin(a),axle+(r+.058)*math.cos(a))+.003),r+(r+.058)*math.sin(a),axle+(r+.058)*math.cos(a)) for a in [math.pi*i/(28 if lod==0 else 16) for i in range((28 if lod==0 else 16)+1)]]
            g.path(points,.010,0,(7,5,4)[lod])
    # Bowed front windscreen, thick curved A-pillars and an actual header return.
    def wind(u,v):
        q=u*2-1;t=.60+.40*(1-abs(q));tx,ty=van_upper_profile(t)
        top=(((-1 if q<0 else 1)*tx*.94),ty-.014,1.380)
        base=(q*.815,1.115+.025*(1-q*q),1.780)
        x,y,z=blend(base,top,v)
        return (x,y,z+.025*(1-q*q)*math.sin(math.pi*v))
    glass.surface(wind,(24,14,7)[lod],(14,9,5)[lod],9)
    for side in (-1,1):
        u=0 if side<0 else 1
        def pillar(v,w):
            inner=wind(u,v);t=.60*v;x,y=van_upper_profile(t);outer=(side*x,y,van_front_z(t))
            return blend(inner,outer,w)
        # Two skins/edge returns form a closed section around the aperture.
        g.surface(pillar,(22,14,7)[lod],3,0,reverse=side>0)
        g.path([wind(u,i/18) for i in range(19)],.011,1,(7,5,4)[lod])
        inner=[wind(u,i/18) for i in range(19)]
        g.path([(p[0],p[1]-.016,p[2]-.022) for p in inner],.022,4,(7,5,4)[lod])
    def header(u,v):return blend(wind(u,1),van_roof_cross(u*2-1,1),v)
    g.surface(header,(32,18,9)[lod],3,0)
    g.path([wind(i/24,1) for i in range(25)],.011,1,(7,5,4)[lod])
    # Van-specific short crowned bonnet and cowl, not the sedan's long deck.
    def bonnet(u,v):
        q=u*2-1;z=1.78+(hl-1.78)*v;width=van_lower_width(van_lower_top(z),z);x=q*width
        # At the cowl the crown follows the actual narrower windscreen base;
        # the two flat outer shoulders continue beneath the A-pillars.
        crown_width=.815+(width-.815)*van_smooth(v/.25)
        return (x,van_lower_top(z)+.025*max(0,1-(x/crown_width)**2),z)
    g.surface(bonnet,(24,14,7)[lod],(24,14,7)[lod],0,reverse=True)
    def bonnet_front_return(u,v):
        top=bonnet(u,1)
        return blend((top[0],van_lower_top(hl),hl),top,v)
    g.surface(bonnet_front_return,(24,14,7)[lod],2,0)
    g.path([wind(i/20,0) for i in range(21)],.011,1,(6,5,4)[lod])
    # Rounded complete rear upper skin matches the same roof/wall profiles.
    def rear(u,v):
        q=u*2-1;t=.60*v;x,y=van_upper_profile(t);rx,ry,rz=van_roof_cross(q,0)
        x0=q*x;x=x0+(rx-q*van_upper_profile(.60)[0])*v*v
        return (x,y+(ry-van_upper_profile(.60)[1])*v*v,van_rear_z(t)+(rz-van_rear_z(.60))*v*v)
    g.surface(rear,(28,18,9)[lod],(18,12,6)[lod],0,reverse=True)
    def rear_belt_return(u,v):
        q=u*2-1;low=(q*van_lower_width(van_lower_top(-hl),-hl),van_lower_top(-hl),-hl)
        return blend(low,rear(u,0),v)
    g.surface(rear_belt_return,(28,18,9)[lod],2,0,reverse=True)
    g.path([rear(.5,i/20) for i in range(21)],.003,1,4)
    # Primary fascia volumes: broad recessed intake and rounded wrap bumpers.
    for direction in (-1,1):
        def bumper(u,v):
            q=u*2-1
            return (q*(hw-.055),.29+.27*v,direction*(hl-.018+.060*(1-q*q)*math.sin(math.pi*v)))
        g.surface(bumper,(36,22,12)[lod],(8,5,3)[lod],0,reverse=direction<0)
        # Lower returned lip preserves the original ±2.5085m end extent.
        g.box((0,.335,direction*(hl+.036)),(s['width']-.18,.075,.045),1,.015)
        g.box((0,.47,direction*(hl+.043)),(.48,.108,.015),8,.009)
    # Recessed opening has an opaque backing and separate side/top returns.
    g.box((0,.79,hl+.007),(1.02,.23,.018),1,.038,segments=3 if lod==0 else 2)
    for y in (.715,.79,.865):g.box((0,y,hl+.023),(.94,.018,.014),3,.006)
    for side in (-1,1):
        # Lamp pockets wrap onto the front quarter; lenses share the authored
        # glass surface. Housing and reflector are separate physical depths.
        def lamp(u,v,offset):
            x=side*(.535+.345*u);y=.900+.135*v-.018*u*v
            z=hl-.008-.075*u*u+offset
            return (x,y,z)
        g.surface(lambda u,v:lamp(u,v,.007),(12,8,4)[lod],(6,4,2)[lod],1,reverse=side<0)
        g.surface(lambda u,v:lamp(.06+.88*u,.10+.80*v,.020),(12,8,4)[lod],(6,4,2)[lod],5,reverse=side<0)
        g.surface(lambda u,v:lamp(.88+.10*u,.12+.76*v,.022),(4,3,2)[lod],(6,4,2)[lod],7,reverse=side<0)
        glass.surface(lambda u,v:lamp(.035+.93*u,.055+.89*v,.029),(14,9,5)[lod],(7,5,3)[lod],9,reverse=side<0)
        # Simple opaque end returns enclose the pocket; no floating greebles.
        for u in (0,1):
            def lamp_u_return(v,w):return blend(lamp(u,v,.007),lamp(u,v,.029),w)
            g.surface(lamp_u_return,6,2,1,reverse=side*(2*u-1)<0)
        for v in (0,1):
            def lamp_v_return(u,w):return blend(lamp(u,v,.007),lamp(u,v,.029),w)
            g.surface(lamp_v_return,10,2,1,reverse=side*(2*v-1)>0)
        g.box((side*.79,.91,-hl-.025),(.13,.25,.025),6,.025)
    # Cab seating/dashboard/bulkhead establishes real glazing depth. Existing
    # seat-front z=.835 is retained; no driver/agent or decorative cargo added.
    for x in (-.40,.40):
        g.box((x,.64,.835),(.48,.14,.48),4,.055,segments=3 if lod==0 else 2)
        g.box((x,.90,.645),(.46,.50,.115),4,.045)
        g.box((x,1.21,.635),(.23,.16,.10),4,.035)
    g.box((0,1.055,1.62),(s['width']-.27,.16,.32),4,.045)
    g.path([(-.40,1.04,1.50),(-.40,1.15,1.32)],.027,4)
    centre=Vector((-.40,1.15,1.32))
    g.path([centre+Vector((.145*math.cos(a),.145*math.sin(a),0)) for a in [2*math.pi*i/(24 if lod==0 else 14) for i in range(24 if lod==0 else 14)]],.015,4,6,True)
    profile=[(-.83,.34,.38),(.83,.34,.38)]
    profile += [(max(0,van_upper_profile(i/20)[0]-.035),van_upper_profile(i/20)[1]-.020,.38) for i in range(21)]
    profile += [(-max(0,van_upper_profile(i/20)[0]-.035),van_upper_profile(i/20)[1]-.020,.38) for i in range(19,-1,-1)]
    g.mesh(profile,[tuple(range(len(profile)))],4)
    return g,glass,[(side*(hw-.10),r,z) for side in (-1,1) for z in axles]


def passenger(spec,lod):
    # Identity test keeps the shortened BoxTruck cab and all other bodies on
    # their byte-identical existing generation path.
    return van_candidate(spec,lod) if spec is SPECS['Van'] else _baseline_passenger(spec,lod)


def wheel(radius,lod,centre):
    g=Geometry()
    count=(56,32,16)[lod]
    # Continuous revolved tyre profile: rounded sidewalls and shoulders, not cylinders.
    profile=[(-.112,.62),(-.119,.79),(-.110,.94),(-.078,.995),(-.044,1),(.044,1),(.078,.995),(.110,.94),(.119,.79),(.112,.62)]
    vs=[(centre[0]+x,centre[1]+radius*rr*math.sin(2*math.pi*i/count),centre[2]+radius*rr*math.cos(2*math.pi*i/count))
        for x,rr in profile for i in range(count)]
    fs=[(j*count+i,j*count+(i+1)%count,((j+1)%len(profile))*count+(i+1)%count,((j+1)%len(profile))*count+i)
        for j in range(len(profile)) for i in range(count)]
    g.mesh(vs,fs,2,True)
    side=1 if centre[0]>0 else -1
    x=centre[0]+side*.115
    for rad,rr,mat in ((radius*.64,.012,3),(radius*.60,.009,1),(radius*.20,.020,3)):
        g.path([(x,centre[1]+rad*math.sin(2*math.pi*i/count),centre[2]+rad*math.cos(2*math.pi*i/count)) for i in range(count)],rr,mat,(8,6,4)[lod],True)
    g.path([(centre[0]-.065,centre[1],centre[2]),(centre[0]+.065,centre[1],centre[2])],radius*.54,3,count)
    for i in range(5):
        angle=2*math.pi*i/5
        g.path([(x+side*.004,centre[1]+radius*.16*math.sin(angle),centre[2]+radius*.16*math.cos(angle)),
                (x+side*.005,centre[1]+radius*.61*math.sin(angle+.08),centre[2]+radius*.61*math.cos(angle+.08))],radius*.055,3,(8,6,4)[lod])
        if lod==0:
            a=angle+.27
            g.path([(x+side*.012,centre[1]+radius*.13*math.sin(a),centre[2]+radius*.13*math.cos(a)),
                    (x+side*.020,centre[1]+radius*.13*math.sin(a),centre[2]+radius*.13*math.cos(a))],.011,3,6)
    if lod==0:
        # Fine circumferential tread grooves and diagonal sipes remain geometry at near LOD.
        for dx in (-.050,0,.050):
            g.path([(centre[0]+dx,centre[1]+radius*1.002*math.sin(2*math.pi*i/count),centre[2]+radius*1.002*math.cos(2*math.pi*i/count))
                    for i in range(count)],.003,1,4,True)
        for i in range(36):
            a=2*math.pi*i/36
            g.path([(centre[0]-.085,centre[1]+radius*.998*math.sin(a),centre[2]+radius*.998*math.cos(a)),
                    (centre[0]+.085,centre[1]+radius*.998*math.sin(a+.040),centre[2]+radius*.998*math.cos(a+.040))],.0027,1,4)
    return g


def box_truck(lod):
    s=SPECS["BoxTruck"]
    # The shaped van cab is shortened and moved over the existing truck chassis.
    cab=dict(SPECS["Van"],length=2.2,width=2.36,belt=1.97,roof=2.91,radius=.48,wheelbase=1.3,
             cab_rear=-1.05,cab_front=1.05,roof_rear=-.9,roof_front=.40,arch_axles=[.2])
    g,gl,_=passenger(cab,lod)
    for data in (g,gl):
        data.v=[(p[0],p[1]-2.9,p[2]) for p in data.v]
    # Cargo box has rolled edges, recessed loading door, latch bars, sill and skirt.
    g.box((0,2.25,-1.0),(2.45,2.70,5.80),8,.035)
    for side in (-1,1):
        g.box((side*1.228,2.23,-1.0),(.014,2.53,5.58),8,.006)
        for y in (.92,3.58):
            g.box((side*1.229,y,-1.0),(.025,.045,5.82),3,.008)
        for z in (-3.87,1.87):
            g.box((side*1.229,2.25,z),(.035,2.69,.035),3,.006)
        for z in (-3.1,-1.7,-.3,1.1):
            g.box((side*1.245,.91,z),(.025,.075,.13),7,.009)
        g.box((side*1.10,.58,-.9),(.12,.16,6.8),1,.014)
    for side in (-1,1):
        g.box((side*.59,2.25,-3.915),(1.15,2.52,.024),8,.017)
        g.path([(side*.36,1.07,-3.94),(side*.36,3.45,-3.94)],.014,3,6)
        for y in (1.27,3.18):
            g.box((side*1.15,y,-3.95),(.16,.10,.045),3,.008)
        g.box((side*.96,.89,-3.96),(.26,.14,.035),6,.01)
    g.box((0,.78,-3.98),(2.3,.12,.12),3,.015)
    g.box((0,.63,0),(1.7,.24,7.3),1,.02)
    centres=[(side*1.05,.48,z) for side in (-1,1) for z in (-3.0,-1.9,3.1)]
    return g,gl,centres


def textures(output):
    # Deterministic physically small detail, shared across every vehicle and LOD.
    size=2048
    yy,xx=np.mgrid[0:size,0:size].astype(np.float32)
    rng=np.random.default_rng(2731)
    grain=rng.normal(0,1,(size,size)).astype(np.float32)
    wav=(np.sin(xx*.093+np.sin(yy*.028))*.35+np.sin(yy*.077)*.25)
    height=(grain*.12+wav*.08)
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.12
    dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.12
    normals=np.dstack((-dx,-dy,np.ones_like(dx)))
    normals/=np.linalg.norm(normals,axis=2)[:,:,None]
    atlas={"VehicleDetail_A":np.dstack([np.clip(.965+grain*.007+wav*.005,0,1)]*3+[np.ones_like(xx)]),
           "VehicleDetail_N":np.dstack(((normals+1)*.5,np.ones_like(xx))),
           "VehicleDetail_M":np.dstack((np.ones_like(xx),np.clip(.97+grain*.005,0,1),np.zeros_like(xx),np.clip(.94+grain*.014+wav*.03,0,1)))}
    images={}
    for name,array in atlas.items():
        image=bpy.data.images.new(name,width=size,height=size,alpha=True)
        image.colorspace_settings.name="sRGB" if name.endswith("_A") else "Non-Color"
        image.pixels.foreach_set(array.astype(np.float32).ravel())
        image.filepath_raw=str(output/"Textures"/(name+".png"));image.file_format="PNG";image.save();image.pack()
        images[name[-1]]=image
    return images


def materials(images):
    values=[((.16,.24,.31,1),.22,.38),((.018,.021,.024,1),.37,0),((.027,.029,.030,1),.70,0),
            ((.56,.58,.61,1),.22,.96),((.051,.054,.057,1),.81,0),((.80,.85,.81,1),.18,.20),
            ((.26,.006,.008,1),.25,.05),((.65,.18,.004,1),.3,0),((.79,.80,.77,1),.44,0),((.80,.91,.90,1),.08,0)]
    result=[]
    for name,(color,roughness,metallic) in zip(MATERIALS,values):
        material=bpy.data.materials.new(name);material.use_nodes=True
        material.diffuse_color=color
        nodes=material.node_tree.nodes;links=material.node_tree.links;bsdf=nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value=color;bsdf.inputs["Roughness"].default_value=roughness;bsdf.inputs["Metallic"].default_value=metallic
        if name=="VehicleGlass":
            bsdf.inputs["Transmission Weight"].default_value=1;bsdf.inputs["IOR"].default_value=1.5
        else:
            normal=nodes.new("ShaderNodeTexImage");normal.image=images["N"]
            mapping=nodes.new("ShaderNodeMapping");coord=nodes.new("ShaderNodeTexCoord")
            mapping.inputs["Scale"].default_value=(2,2,2);links.new(coord.outputs["UV"],mapping.inputs["Vector"]);links.new(mapping.outputs["Vector"],normal.inputs["Vector"])
            bump=nodes.new("ShaderNodeNormalMap");bump.inputs["Strength"].default_value=.08 if name=="VehiclePaint" else .35
            links.new(normal.outputs["Color"],bump.inputs["Color"]);links.new(bump.outputs["Normal"],bsdf.inputs["Normal"])
        if name=="VehiclePaint":
            bsdf.inputs["Coat Weight"].default_value=.85;bsdf.inputs["Coat Roughness"].default_value=.14
        if name in ("VehicleHeadlamp","VehicleTaillamp","VehicleAmber"):
            bsdf.inputs["Emission Color"].default_value=color;bsdf.inputs["Emission Strength"].default_value=.35
        result.append(material)
    return result


def van_lamp_roundrect(cx,cy,width,height,radius,steps,z):
    points=[]
    for x,y,start in ((width/2-radius,height/2-radius,0),(-width/2+radius,height/2-radius,math.pi/2),(-width/2+radius,-height/2+radius,math.pi),(width/2-radius,-height/2+radius,3*math.pi/2)):
        for i in range(steps+1):
            angle=start+i*math.pi/(2*steps);px=cx+x+radius*math.cos(angle)
            points.append((px,cy+y+radius*math.sin(angle)-.006*((abs(px)-.536)/.320)**2,z))
    return points


def van_lamp_cup(g,side,lod):
    steps=(4,3,1)[lod];cx=.696;cy=.955;n=4*(steps+1)
    # Back contact is 2.5mm ahead of the accepted flat front skin. The closed
    # pocket therefore shows real depth without cutting or moving that skin.
    rings=[van_lamp_roundrect(cx,cy,.320,.110,.013,steps,2.4525),
           van_lamp_roundrect(cx,cy,.320,.110,.013,steps,2.4950),
           van_lamp_roundrect(cx,cy,.308,.098,.010,steps,2.4954),
           van_lamp_roundrect(cx,cy,.304,.094,.009,steps,2.4555)]
    if lod==2:
        rings=[[(cx+x,cy+y-.006*((cx+x-.536)/.320)**2,z) for x,y in ((w/2,h/2),(-w/2,h/2),(-w/2,-h/2),(w/2,-h/2))] for w,h,z in ((.320,.110,2.4525),(.320,.110,2.4950),(.308,.098,2.4954),(.304,.094,2.4555))];n=4
    vertices=[(side*p[0],p[1],p[2]) for ring in rings for p in ring]
    vertices += [(side*cx,cy-.0015,2.4525),(side*cx,cy-.0015,2.4555)]
    faces=[];smooth=[]
    for ring in range(3):
        for i in range(n):faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i));smooth.append(True)
    for i in range(n):
        faces.append((4*n,(i+1)%n,i));smooth.append(False)
        faces.append((4*n+1,3*n+i,3*n+(i+1)%n));smooth.append(False)
    # Mirroring X reverses winding. The standard closed-component recalc later
    # establishes consistent positive volume for both sides.
    g.mesh(vertices,[tuple(reversed(f)) for f in faces] if side<0 else faces,1,True);g.smooth[-len(faces):]=smooth


def van_lamp_revolve(g,centre,profile,segments,materials,smooth=True):
    """Closed cup/optic from an axis-to-axis profile, with unique axis verts."""
    vertices=[];rings=[]
    for radius,z in profile:
        if radius==0:
            rings.append([len(vertices)]);vertices.append((centre[0],centre[1],z))
        else:
            rings.append(list(range(len(vertices),len(vertices)+segments)))
            vertices.extend((centre[0]+radius*math.cos(2*math.pi*i/segments),centre[1]+radius*math.sin(2*math.pi*i/segments),z) for i in range(segments))
    faces=[];mats=[]
    for j,(a,b) in enumerate(zip(rings,rings[1:])):
        for i in range(segments):
            k=(i+1)%segments
            face=(a[0],b[k],b[i]) if len(a)==1 else (a[i],a[k],b[0]) if len(b)==1 else (a[i],a[k],b[k],b[i])
            faces.append(face);mats.append(materials[j])
    g.mesh(vertices,faces,mats,smooth)


def van_lamp_cover(glass,side,lod):
    steps=(4,3,1)[lod];cx=.696;cy=.955;n=4*(steps+1)
    edge=van_lamp_roundrect(cx,cy,.305,.095,.009,steps,0)
    vertices=[(side*cx,cy-.0015,2.5005)]
    rings=[]
    levels=((1.,2.4972),(1.,2.4960)) if lod==2 else ((.55,2.5000),(1.,2.4972),(1.,2.4960),(.55,2.4988))
    for scale,z in levels:
        rings.append(list(range(len(vertices),len(vertices)+n)))
        vertices.extend((side*(cx+(p[0]-cx)*scale),cy-.0015+(p[1]-(cy-.0015))*scale,z) for p in edge)
    back=len(vertices);vertices.append((side*cx,cy-.0015,2.4993));faces=[]
    for i in range(n):faces.append((0,rings[0][i],rings[0][(i+1)%n]))
    for a,b in zip(rings,rings[1:]):
        for i in range(n):faces.append((a[i],b[i],b[(i+1)%n],a[(i+1)%n]))
    for i in range(n):faces.append((back,rings[-1][(i+1)%n],rings[-1][i]))
    start=len(glass.f)
    glass.mesh(vertices,[tuple(reversed(f)) for f in faces] if side<0 else faces,9,True)
    # Keep the thin perimeter wall flat so its radial normal does not bend
    # front/back transmission normals into an artificial prismatic rim.
    wall_start=start+(n if lod==2 else 2*n)
    glass.smooth[wall_start:wall_start+n]=[False]*n


def van_headlamp_parts(geometry_type,lod):
    body,glass=geometry_type(),geometry_type()
    for side in (-1,1):
        van_lamp_cup(body,side,lod)
        # Integrated black barrel, bright rolled rim, parabolic reflector and
        # small emitter: manufactured depth replaces the old luminous panel.
        main=(side*.628,.9545)
        secondary=(side*.756,.9522)
        if lod==2:
            # Distant authored optic keeps the chrome/emitter signature within
            # the established coarsest-LOD ceiling; no second clear optic layer.
            van_lamp_revolve(body,main,[(0,2.482),(.039,2.482),(.039,2.495),(.014,2.495),(0,2.495)],8,[1,3,3,5])
            van_lamp_revolve(body,secondary,[(0,2.482),(.031,2.482),(.031,2.495),(.011,2.495),(0,2.495)],6,[1,3,3,5])
        else:
            profile=[(0,2.4560),(.045,2.4560),(.046,2.4915),(.043,2.4950),(.0345,2.4940),(.026,2.4800),(.014,2.4680),(0,2.4680)]
            van_lamp_revolve(body,main,profile,(24,16)[lod],[1,1,3,3,3,3,5])
            optic=[(0,2.4980),(.020,2.4972),(.0325,2.4950),(.0325,2.4938),(0,2.4938)]
            van_lamp_revolve(glass,main,optic,(20,14)[lod],[9,9,9,9])
            profile=[(0,2.4560),(.035,2.4560),(.037,2.4915),(.034,2.4950),(.029,2.4940),(.019,2.4770),(0,2.4650)]
            van_lamp_revolve(body,secondary,profile,(16,12)[lod],[1,1,3,3,3,5],False)
        # Restrained outer indicator and lower running-light element reuse the
        # existing amber/headlamp surfaces, including their existing emission.
        body.box((side*.837,.949,2.490),(.007,.074,.006),7,0)
        if lod<2:body.box((side*.701,.910,2.490),(.239,.0035,.004),5,0)
        if lod==0:
            for x in (.548,.846):
                van_lamp_revolve(body,(side*x,.984),[(0,2.4915),(.0032,2.4915),(.0032,2.4940),(0,2.4940)],8,[1,3,3],False)
        van_lamp_cover(glass,side,lod)
    return body,glass


def van_lamp_components(mesh):
    parent=list(range(len(mesh.vertices)))
    def find(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    for f in mesh.polygons:
        for i in f.vertices[1:]:parent[find(i)]=find(f.vertices[0])
    groups={}
    for f in mesh.polygons:groups.setdefault(find(f.vertices[0]),[]).append(f.index)
    return list(groups.values())


def van_lamp_old_components(mesh,part):
    selected=[]
    for faces in van_lamp_components(mesh):
        ids=sorted({v for i in faces for v in mesh.polygons[i].vertices});points=[(mesh.vertices[i].co.x,mesh.vertices[i].co.z,-mesh.vertices[i].co.y) for i in ids]
        lo=[min(v[i] for v in points) for i in range(3)];hi=[max(v[i] for v in points) for i in range(3)]
        side=1 if lo[0]>.50 else -1 if hi[0]<-.50 else 0
        if side and min(abs(p[0]) for p in points)>.50 and max(abs(p[0]) for p in points)<.90 and lo[1]>.88 and hi[1]<1.05 and lo[2]>2.35 and hi[2]<2.49:
            roles={mesh.polygons[i].material_index for i in faces}
            assert roles <= ({1,5,7} if part=='Body' else {9}), 'Lamp selector touched an unexpected surface'
            selected.append(dict(side=side,faces=faces,vertices=ids,roles=sorted(roles),bounds=[lo,hi]))
    expected=14 if part=='Body' else 2
    assert len(selected)==expected, 'Expected disconnected old lamp components: '+str((part,len(selected),expected))
    return selected


def van_lamp_mesh_signature(mesh):
    """Dense meaningful buffers; index identity is preserved for whole wheels."""
    mesh.calc_tangents(uvmap=mesh.uv_layers.active.name)
    values=([tuple(v.co) for v in mesh.vertices],[(tuple(f.vertices),f.material_index,f.use_smooth) for f in mesh.polygons],[[tuple(v.uv) for v in layer.data] for layer in mesh.uv_layers],[(tuple(n.vector),tuple(loop.tangent),loop.bitangent_sign) for n,loop in zip(mesh.corner_normals,mesh.loops)])
    return hashlib.sha256(repr(values).encode()).hexdigest()


def van_headlamp_refine(ob,lod,part):
    """Replace only old lamp components after standard LOD; preserve all others."""
    assert part in ('Body','Glass') and not ob.modifiers
    old=ob.data;assert not old.has_custom_normals and len(old.materials)==10
    selected=van_lamp_old_components(old,part);removed_faces={i for r in selected for i in r['faces']};removed_vertices={i for r in selected for i in r['vertices']}
    kept_faces=[f.index for f in old.polygons if f.index not in removed_faces];kept_vertices=[v.index for v in old.vertices if v.index not in removed_vertices];mapping={old_i:new_i for new_i,old_i in enumerate(kept_vertices)}
    old.calc_tangents(uvmap=old.uv_layers.active.name)
    corners=[(i,tuple(old.corner_normals[i].vector),tuple(old.loops[i].tangent),old.loops[i].bitangent_sign) for f in kept_faces for i in old.polygons[f].loop_indices]
    body,glass=van_headlamp_parts(Geometry,lod);geometry=body if part=='Body' else glass
    temp=geometry.object('OwnedVanHeadlamp_'+part+'_LOD'+str(lod),list(old.materials))
    try:
        # The new asset has explicit per-LOD tessellation. Existing accepted LOD
        # surfaces are already simplified, so no global modifier runs again.
        lamp=temp.data;lamp.calc_tangents(uvmap=lamp.uv_layers.active.name)
        positions=[tuple(old.vertices[i].co) for i in kept_vertices]+[tuple(v.co) for v in lamp.vertices]
        faces=[tuple(mapping[v] for v in old.polygons[i].vertices) for i in kept_faces]
        faces += [tuple(len(kept_vertices)+v for v in f.vertices) for f in lamp.polygons]
        mesh=bpy.data.meshes.new(old.name+'_headlamp');mesh.from_pydata(positions,[],faces)
        for material in old.materials:mesh.materials.append(material)
        old_polys=[old.polygons[i] for i in kept_faces];source_polys=old_polys+list(lamp.polygons)
        for f,source in zip(mesh.polygons,source_polys):f.material_index=source.material_index;f.use_smooth=source.use_smooth
        for layer in old.uv_layers:
            target=mesh.uv_layers.new(name=layer.name);source_lamp=lamp.uv_layers.get(layer.name);assert source_lamp
            values=[tuple(layer.data[i].uv) for f in old_polys for i in f.loop_indices]+[tuple(v.uv) for v in source_lamp.data]
            target.data.foreach_set('uv',[v for uv in values for v in uv])
        mesh.update();mesh.calc_tangents(uvmap=mesh.uv_layers.active.name)
        maximum_normal=maximum_tangent=0.
        for new_i,(old_i,normal,tangent,sign) in enumerate(corners):
            maximum_normal=max(maximum_normal,(mesh.corner_normals[new_i].vector-Vector(normal)).length)
            maximum_tangent=max(maximum_tangent,(mesh.loops[new_i].tangent-Vector(tangent)).length)
            assert mesh.loops[new_i].bitangent_sign==sign
        assert maximum_normal<=2e-6 and maximum_tangent<=2e-6,'Non-lamp shading buffers changed'
        for i,old_i in enumerate(kept_faces):
            f=mesh.polygons[i];original=old.polygons[old_i]
            assert tuple(f.vertices)==tuple(mapping[v] for v in original.vertices)
            assert all(tuple(mesh.vertices[n].co)==tuple(old.vertices[o].co) for n,o in zip(f.vertices,original.vertices))
            assert f.material_index==original.material_index and f.use_smooth==original.use_smooth
        old_name=old.name;old.name=old_name+'_retained_source';ob.data=mesh;mesh.name=old_name
        report=dict(part=part,lod=lod,oldLampComponents=len(selected),removedLampTriangles=len(removed_faces),newLampTriangles=len(lamp.polygons),nonLampFacesExact=len(kept_faces),nonLampMaximumNormalDelta=maximum_normal,nonLampMaximumTangentDelta=maximum_tangent,addedVertexStart=len(kept_vertices),addedFaceStart=len(kept_faces),newComponents=[])
        # Each actual added lamp component must be a consistently oriented
        # closed solid after object construction/normal recalc.
        for group in van_lamp_components(lamp):
            used={v for i in group for v in lamp.polygons[i].vertices};edges={};volume=0.
            for i in group:
                f=lamp.polygons[i];ids=tuple(f.vertices);assert len(ids)==3
                a,b,c=(lamp.vertices[v].co for v in ids);assert (b-a).cross(c-a).length/2>=1e-8
                volume+=a.dot(b.cross(c))/6
                for a,b in zip(ids,(*ids[1:],ids[0])):edges.setdefault(tuple(sorted((a,b))),[]).append((a,b))
            assert edges and all(len(v)==2 and v[0]!=v[1] for v in edges.values()),'Open or inconsistent new lamp component'
            assert volume>1e-10 and len(used)-len(edges)+len(group)==2,'New lamp component is not an outward genus0 solid'
            assert {lamp.polygons[i].material_index for i in group} <= ({1,3,5,7} if part=='Body' else {9})
            report['newComponents'].append(dict(triangles=len(group),vertices=len(used),euler=len(used)-len(edges)+len(group),positiveVolumeM3=volume,materials=sorted({lamp.polygons[i].material_index for i in group})))
        if old.users==0:bpy.data.meshes.remove(old)
        return report
    finally:
        data=temp.data;bpy.data.objects.remove(temp,do_unlink=True)
        if data.users==0:bpy.data.meshes.remove(data)


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:]);output=args.output.resolve()
    output.mkdir(parents=True,exist_ok=True);(output/"Exports").mkdir(exist_ok=True);(output/"Textures").mkdir(exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.render.engine="CYCLES";scene.cycles.device="CPU";scene.render.threads_mode="FIXED";scene.render.threads=2
    bpy.context.preferences.filepaths.save_version=0
    scene.unit_settings.system="METRIC";scene.unit_settings.scale_length=1
    mats=materials(textures(output));records=[]
    for name,spec in SPECS.items():
        collection=bpy.data.collections.new("Vehicle"+name);scene.collection.children.link(collection)
        objects=[];lod_records=[];centres=None
        for lod in range(3):
            opaque,glass,centres=box_truck(lod) if name=="BoxTruck" else passenger(spec,lod)
            geometries=[("Body",opaque),("Glass",glass)]+[("Wheel_"+str(i),wheel(spec["radius"],lod,c)) for i,c in enumerate(centres)]
            triangles=0
            for part,geometry in geometries:
                ob=geometry.object("Vehicle"+name+"_"+part+"_LOD"+str(lod),mats)
                if lod:
                    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
                    decimate=ob.modifiers.new("Bounded distance simplification","DECIMATE");decimate.ratio=.65 if lod==1 else .32
                    decimate.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=decimate.name);ob.select_set(False)
                if name=='Van' and part in ('Body','Glass'):van_headlamp_refine(ob,lod,part)
                bm=bmesh.new();bm.from_mesh(ob.data)
                # Font tessellation can leave near-collinear cap triangles that
                # collapse after FBX float conversion. Reject sub-0.01 mm2 faces.
                degenerate=[face for face in bm.faces if face.calc_area()<1e-8]
                if degenerate:bmesh.ops.delete(bm,geom=degenerate,context="FACES_ONLY")
                bm.to_mesh(ob.data);bm.free();ob.data.validate(clean_customdata=False);ob.data.update()
                for owner in list(ob.users_collection):owner.objects.unlink(ob)
                collection.objects.link(ob);ob["vehicle_visual_only"]=True
                ob.data.calc_loop_triangles();triangles+=len(ob.data.loop_triangles)
                objects.append(ob)
            lod_records.append(dict(lod=lod,triangles=triangles))
        bpy.ops.object.select_all(action="DESELECT")
        temp=[]
        for original in objects:
            ob=original.copy();ob.data=original.data.copy();scene.collection.objects.link(ob)
            ob.name=original.name+"_Export";ob.data.transform(Matrix.Diagonal((1,-1,1,1)));ob.data.flip_normals()
            ob.select_set(True);temp.append(ob)
        bpy.context.view_layer.objects.active=temp[0]
        path=output/"Exports"/("Vehicle"+name+".fbx")
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,axis_forward="-Z",axis_up="Y",
            use_mesh_modifiers=True,mesh_smooth_type="FACE",add_leaf_bones=False,bake_anim=False,path_mode="STRIP",use_custom_props=True)
        for ob in temp:bpy.data.objects.remove(ob,do_unlink=True)
        points=[(v.co.x,v.co.z,-v.co.y) for ob in objects if ob.name.endswith("LOD0") for v in ob.data.vertices]
        bounds=[[min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]]
        records.append(dict(name="Vehicle"+name,file=str(path.relative_to(output)),lods=lod_records,
                            wheel_centres=centres,wheel_radius=spec["radius"],bounds_unity=bounds,sha256=sha(path)))
        collection.hide_render=True
    sign=Geometry()
    sign.box((0,1.49,-.30),(.62,.045,.23),1,.018)
    sign.box((0,1.57,-.30),(.56,.13,.19),5,.025,segments=3)
    for x in (-.18,-.06,.06,.18):
        sign.box((x,1.57,-.198),(.045,.075,.006),1,.002)
    sign_object=sign.object("VehicleTaxiSign_Body_LOD0",mats)
    sign_object.data.calc_loop_triangles()
    copy=sign_object.copy();copy.data=sign_object.data.copy();scene.collection.objects.link(copy)
    copy.data.transform(Matrix.Diagonal((1,-1,1,1)));copy.data.flip_normals()
    bpy.ops.object.select_all(action="DESELECT");copy.select_set(True);bpy.context.view_layer.objects.active=copy
    sign_path=output/"Exports/VehicleTaxiSign.fbx"
    bpy.ops.export_scene.fbx(filepath=str(sign_path),use_selection=True,object_types={"MESH"},apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,axis_forward="-Z",axis_up="Y",add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
    bpy.data.objects.remove(copy,do_unlink=True)
    records.append(dict(name="VehicleTaxiSign",file="Exports/VehicleTaxiSign.fbx",lods=[dict(lod=0,triangles=len(sign_object.data.loop_triangles))],
                        wheel_centres=[],wheel_radius=0,bounds_unity=[[-.31,1.4675,-.415],[.31,1.635,-.185]],sha256=sha(sign_path)))
    sign_object.hide_render=True
    bpy.ops.wm.save_as_mainfile(filepath=str(output/"Vehicles.blend"),compress=True)
    files={str(p.relative_to(output)):{"bytes":p.stat().st_size,"sha256":sha(p)} for p in output.rglob("*") if p.is_file() and p.suffix in (".png",".fbx",".blend")}
    manifest=dict(schema=1,status="AUTHORED_CPU_SOURCE_ONLY_UNITY_VISUAL_ACCEPTANCE_PENDING",author_sha256=sha(__file__),
                  materials=list(MATERIALS),models=records,files=files,texture_size=2048,
                  physics="visual only; no exported colliders, rigs, cameras or lights; gameplay colliders remain authoritative",
                  geometry="crowned roofs, wheel arch apertures, separate curved glazing, seat/dashboard/steering cabin, door seams, recessed lighting, revolved tyres and alloys",
                  provenance="Original procedural manufacture in Blender; no third-party meshes or textures",renders=0)
    manifest["textures"]=[dict(name="VehicleDetail_"+suffix,file="Textures/VehicleDetail_"+suffix+".png",sha256=files["Textures/VehicleDetail_"+suffix+".png"]["sha256"]) for suffix in ("A","N","M")]
    (output/"production-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    print(json.dumps({"status":manifest["status"],"models":[{"name":m["name"],"lods":m["lods"]} for m in records],"bytes":sum(v["bytes"] for v in files.values())}))


if __name__=="__main__":main()
