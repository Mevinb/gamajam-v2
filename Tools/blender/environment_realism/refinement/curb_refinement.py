"""Curb-only inward edge wear and metre-scaled atlas UVs. Blender API only; no I/O."""
import math
from mathutils import Vector
LENGTH, HEIGHT, DEPTH = .997, .162, .318
ATLAS_SIZE, PAD_PIXELS = 2048, 12

def refine_curb(mesh):
    old=[v.co.copy() for v in mesh.vertices]
    # Keep the existing closed92-triangle cage. Small inward edge losses vary by
    # physical position; bottom bearing plane and at least one extreme peraxis stay.
    wear=[]
    for i,v in enumerate(mesh.vertices):
        x,y,z=v.co.x,v.co.z,-v.co.y
        top=max(0,min(1,(y-(HEIGHT-.017))/.017))
        edge=max(0,min(1,(abs(z)-(DEPTH*.5-.020))/.020))
        # Irregular end/corner wear, bounded below2mm. No high-frequency geometry.
        pattern=.5+.5*math.sin(x*31.7+z*46.3+.72)*math.cos(x*17.3-z*29.1)
        amount=.0014*top*edge*pattern
        if i%5==0:amount=0.0  # Unworn measured datum vertices retain the envelope.
        if amount:
            inward=Vector((-x, -(y-HEIGHT*.5), -z)).normalized()
            p=Vector((x,y,z))+inward*amount
            v.co=(p.x,-p.z,p.y)
        wear.append((v.co-old[i]).length)
    mesh.update()
    uv=mesh.uv_layers.active
    assert uv and len(mesh.uv_layers)==1
    pad=PAD_PIXELS/ATLAS_SIZE;scale=.5-2*pad  # One atlas-quadrant equals onephysicalmetre.
    for face in mesh.polygons:
        normal=Vector((face.normal.x,face.normal.z,-face.normal.y))
        axis=max(range(3),key=lambda k:abs(normal[k]))
        for li in face.loop_indices:
            p=mesh.vertices[mesh.loops[li].vertex_index].co
            x,y,z=p.x,p.z,-p.y
            if axis==0:a,b=z+DEPTH*.5,y
            elif axis==1:a,b=x+LENGTH*.5,z+DEPTH*.5
            else:a,b=x+LENGTH*.5,y
            uv.data[li].uv=(pad+a*scale,pad+b*scale)
    mesh.update()
    return {'maximumVertexInsetMetres':max(wear),'movedVertices':sum(v>0 for v in wear),'uvDomain':'Originalcut-limestonequarter0;1000effectivepixelspermetre onallplanaraxes; noface-normalizedstretch','geometryRule':'Samevertices/edges/faceindices/materialslots;inward-onlyedgewear<1.4mm;noextratrianglesorobjects'}
