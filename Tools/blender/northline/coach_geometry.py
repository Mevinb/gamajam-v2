"""Manufacturing recipe in Unity metres; bpy-independent, no writes on import.

The eight reachable seats intentionally require the eight reviewed compound
colliders in SEATS. They are not a decorative/noncolliding interior promise.
All parts are mounted to the same original coach mesh/transform.
"""
import math

BOUNDS = ((-1.602, .15, -7.0095), (1.602, 3.29, 7.0095))
SEATS = tuple({"name": "Northline coach seat %s %s" % ("west" if side<0 else "east", str(z)),
               "centre": (side*1.09, 1.295, z), "size": (.62, 1.09, 1.44)}
              for side in (-1, 1) for z in (-5.6, -3.6, 3.6, 5.6))
PROTECTED_AISLE = ((-1.43, .75, -1.85), (1.43, 2.65, 1.85))
CENTRAL_PASSAGE = ((-.75, .75, -6.5), (.75, 2.65, 6.5))
TRIANGLE_CAP = 12000


def build(g):
    b, t = g.box, g.tube
    # The exact original deck/canopy/underframe envelope remains authoritative.
    # A real open underframe exposes the bogies rather than covering them with
    # the old solid visual slab. The existing inaccessible underframe collider
    # is intentionally retained by the later integration; deck/pivot are exact.
    for x in (-.67,.67):
        b("underframe C section web", (x,.43,0), (.055,.25,13.8), 2, .006)
        for y in (.2875,.5725):
            b("underframe C section flange", (x,y,0), (.22,.035,13.8), 2, .004)
    for z in (-6.7,-2.6,0,2.6,6.7):
        b("underframe crossmember", (0,.47,z), (2.75,.16,.12), 2, .007)
    b("unchanged deck", (0,.67,0), (3.2,.16,14), 5, .012, 2)
    b("unchanged canopy", (0,3.2,0), (3.2,.18,14), 0, .018, 2)
    b("bright interior ceiling", (0,3.100,0), (2.86,.018,13.42), 1, .003)
    # Actual apertures: no opaque backing is left behind the glass.
    for side in (-1, 1):
        intervals = ((-6.9,-1.24),(1.24,6.9)) if side<0 else ((-6.9,6.9),)
        for lo, hi in intervals:
            b("side lower painted sheet", (side*1.52,1.265,(lo+hi)/2), (.16,1.03,hi-lo), 0, .008)
            b("side upper painted sheet", (side*1.52,2.845,(lo+hi)/2), (.16,.57,hi-lo), 0, .008)
            cursor = lo
            for z in (-5.6,-3.6,3.6,5.6):
                hole_lo, hole_hi = z-.80, z+.80
                if hole_lo < lo or hole_hi > hi:
                    continue
                if hole_lo>cursor:
                    b("side window structural pier", (side*1.52,2.17,(cursor+hole_lo)/2), (.16,.78,hole_lo-cursor), 0, .005)
                cursor=hole_hi
                # Rubber seals are four manufactured strips around real gaps.
                for wz in (z-.79,z+.79):
                    b("window vertical gasket", (side*1.597,2.17,wz), (.008,.76,.03), 4)
                for y in (1.80,2.54):
                    b("window horizontal gasket", (side*1.597,y,z), (.008,.025,1.55), 4)
                b("actual laminated side glass", (side*1.598,2.17,z), (.008,.70,1.55), 2, glass=True)
            if cursor < hi:
                b("side remaining window pier", (side*1.52,2.17,(cursor+hi)/2), (.16,.78,hi-cursor), 0, .005)
    for z in (-6.9,6.9):
        b("end lower panel", (0,1.25,z), (3.2,1.0,.18), 0, .008)
        b("end upper panel", (0,2.81,z), (3.2,.64,.18), 0, .008)
        for x in (-1.44,1.44):
            b("end windscreen pillar", (x,2.12,z), (.32,.74,.18), 0, .005)
        for x in (-1.265,1.265):
            b("end vertical gasket", (x,2.12,z*1.015), (.03,.71,.012), 4)
        for y in (1.765,2.475):
            b("end horizontal gasket", (0,y,z*1.015), (2.50,.03,.012), 4)
        b("actual laminated end glass", (0,2.12,z*1.015), (2.50,.68,.012), 2, glass=True)
    for z in (-1.2,1.2):
        b("unchanged west door jamb", (-1.52,1.94,z), (.16,2.38,.08), 7, .006)
    for z in (-5,-2.5,0,2.5,5):
        b("canopy underside stiffener", (0,3.075,z), (3.0,.07,.08), 1, .004)
    # Recessed below canopy: no new lights, bloom, emissive shader or probe.
    for x in (-.85,.85):
        for z in (-4.4,4.4):
            t("ceiling passenger handrail", (x,2.84,z-1.7), (x,2.84,z+1.7), .017, 2, 10)
            for dz in (-1.4,1.4):
                t("ceiling rail mount", (x,2.84,z+dz), (x,3.05,z+dz), .012, 2, 8)
    for seat in SEATS:
        side=-1 if seat["centre"][0]<0 else 1
        z=seat["centre"][2]
        b(seat["name"]+" seat pan", (side*1.08,1.18,z), (.54,.14,1.36), 6, .035, 2)
        b(seat["name"]+" seat back", (side*1.34,1.485,z), (.10,.61,1.36), 6, .025, 2)
        for dz in (-.49,.49):
            b(seat["name"]+" anchored support", (side*1.16,.955,z+dz), (.28,.41,.09), 2, .006)
    # Markers retain their exact existing locations; the control is a separate
    # original runtime-owned child and is never merged into this visual mesh.
    for z in (-1.2,.4):
        b("unchanged free-body boarding mark", (0,.753,z), (2.65,.006,.08), 7)
    # Conventional longitudinal bogies. Tread/flange/web are distinct profile
    # surfaces; four axles, eight wheels, brakes and visible spring assemblies.
    for bogie_z in (-4.5,4.5):
        for x in (-1.18,1.18):
            b("cast bogie side beam", (x,.45,bogie_z), (.18,.22,2.12), 2, .014)
        for dz in (-.86,.86):
            b("bogie transverse bolster", (0,.46,bogie_z+dz), (2.44,.18,.14), 2, .010)
        for x in (-.98,.98):
            g.spring("coil spring", (x,.40,bogie_z), .073, .17, .012, 3.2, 36, 4)
        for dz in (-.5,.5):
            z=bogie_z+dz
            t("turned axle", (-1.45,.40,z), (1.45,.40,z), .055, 2, 12)
            for x in (-1.45,1.45):
                g.wheel("flanged steel wheel", (x,.40,z), 24)
                side=-1 if x<0 else 1
                t("wheel bearing boss", (x-.09,.40,z), (x+.09,.40,z), .105, 2, 16)
                t("axle end cap", (x+side*.105,.40,z), (x+side*.125,.40,z), .085, 2, 12)
                t("brake disc", (side*1.27,.40,z), (side*1.30,.40,z), .173, 2, 16)
                b("brake caliper", (side*1.235,.52,z), (.12,.13,.24), 2, .008)


class Plan:
    """Non-Blender dry plan used for bounds/aisle/slot checks before authoring."""
    def __init__(self):
        self.parts=[]
    def add(self,name,centre,size,band,glass=False,**extra):
        self.parts.append(dict(name=name,centre=centre,size=size,band=band,glass=glass,**extra))
    def box(self,name,centre,size,band,bevel=0,segments=1,glass=False):
        self.add(name,centre,size,band,glass,shape="box",bevel=bevel,segments=segments)
    def tube(self,name,start,end,radius,band,sides=8):
        centre=tuple((a+b)/2 for a,b in zip(start,end))
        # Exact radius extent on perpendicular axes for the axis-aligned tubes.
        size=tuple(abs(a-b)+(0 if a!=b else 2*radius) for a,b in zip(start,end))
        self.add(name,centre,size,band,shape="tube",sides=sides)
    def wheel(self,name,centre,sides):
        self.add(name,centre,(.21,.5,.5),3,shape="wheel",sides=sides)
    def spring(self,name,centre,radius,height,wire,turns,steps,sides):
        self.add(name,centre,(2*(radius+wire),height+2*wire,2*(radius+wire)),2,shape="spring",steps=steps,sides=sides)
    def validate(self):
        for p in self.parts:
            lo=[c-s/2 for c,s in zip(p["centre"],p["size"])]
            hi=[c+s/2 for c,s in zip(p["centre"],p["size"])]
            assert all(BOUNDS[0][i]-.00001<=lo[i]<=hi[i]<=BOUNDS[1][i]+.00001 for i in range(3)),p["name"]
        assert sum(p["glass"] for p in self.parts)==10
        for seat in SEATS:
            lo=[c-s/2 for c,s in zip(seat["centre"],seat["size"])]
            hi=[c+s/2 for c,s in zip(seat["centre"],seat["size"])]
            for aisle in (PROTECTED_AISLE,CENTRAL_PASSAGE):
                assert any(hi[i]<aisle[0][i] or lo[i]>aisle[1][i] for i in range(3)),seat
        return {"parts":len(self.parts),"glass_closed_panes":10,"intentional_seat_colliders":len(SEATS),
                "bounds":BOUNDS,"slot_names":["RailCoachOpaquePBR","RailCoachGlass"],
                "triangle_cap":TRIANGLE_CAP,"aisle_width_metres":1.56,
                "original_central_boarding_and_control_zone":PROTECTED_AISLE}


def plan():
    result=Plan()
    build(result)
    return result.validate()
