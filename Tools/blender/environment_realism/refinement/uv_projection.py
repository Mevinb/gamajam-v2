"""UV-only limestone projection. No I/O, object creation or geometry mutation."""
from mathutils import Vector

PAD = 12 / 2048
SPAN = .5 - 2 * PAD
TARGETS = ('WindowReveal', 'EntranceSurround', 'FracturedStone')

def unity(v):
    return Vector((v.x, v.z, -v.y))

def correct(mesh):
    assert len(mesh.uv_layers) == 1
    uv = mesh.uv_layers.active
    parent = list(range(len(mesh.vertices)))
    def find(v):
        while parent[v] != v:
            parent[v] = parent[parent[v]]
            v = parent[v]
        return v
    for edge in mesh.edges:
        a,b = edge.vertices
        parent[find(b)] = find(a)
    components = {}
    for face in mesh.polygons:
        components.setdefault(find(face.vertices[0]), []).append(face)
    result = []
    for faces in components.values():
        stone = [all(0 <= uv.data[i].uv.x < .5 and 0 <= uv.data[i].uv.y < .5 for i in f.loop_indices) for f in faces]
        assert len(set(stone)) == 1, 'A connected source piece spans atlas roles'
        if not stone[0]:
            continue
        vertices = sorted({v for f in faces for v in f.vertices})
        points = {v:unity(mesh.vertices[v].co) for v in vertices}
        low = Vector(tuple(min(p[i] for p in points.values()) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in points.values()) for i in range(3)))
        physical_tile = max(1., *(high-low))
        factor = SPAN / physical_tile
        for face in faces:
            normal = unity(face.normal)
            axis = max(range(3), key=lambda i:abs(normal[i]))
            aa,bb = [i for i in range(3) if i != axis]
            for loop in face.loop_indices:
                point = points[mesh.loops[loop].vertex_index]
                value = (PAD + (point[aa]-low[aa])*factor, PAD + (point[bb]-low[bb])*factor)
                assert all(PAD-1e-8 <= v <= .5-PAD+1e-8 for v in value)
                uv.data[loop].uv = value
        result.append({'vertices':vertices,'polygons':[f.index for f in faces],'boundsUnityMetres':[list(low),list(high)],'physicalTileSizeMetres':physical_tile,'projectedTexelsPerMetre':2048*factor,'paddingPixels':12,'projection':'Dominant face-normal axis; same physical scale on both remaining axes; component minimum anchor'})
    assert result
    return result
