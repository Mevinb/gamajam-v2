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


def passenger(spec, lod):
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
