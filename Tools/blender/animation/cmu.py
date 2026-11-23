"""ASF/AMC (CMU Graphics Lab Motion Capture Database) loader.

Computes per-frame global joint rotations/positions in the CMU frame (Y up, subject faces +Z, subject left = +X),
in metres. Pure python + mathutils (runs inside Blender).
"""
import math
from mathutils import Matrix, Euler, Vector

UNIT = 0.0254 / 0.45  # CMU 'length 0.45' units -> metres


class Bone:
    def __init__(self, name):
        self.name = name
        self.direction = Vector((0, 0, 0))
        self.length = 0.0
        self.C = Matrix.Identity(3)
        self.dof = []
        self.parent = None
        self.children = []


def _euler_mat(angles_deg, order='XYZ'):
    return Euler([math.radians(a) for a in angles_deg], order).to_matrix()


def load_asf(path):
    bones = {'root': Bone('root')}
    lines = [l.strip() for l in open(path) if l.strip() and not l.strip().startswith('#')]
    i = 0
    section = None
    cur = None
    root_order = ['TX', 'TY', 'TZ', 'RX', 'RY', 'RZ']
    root_axis = 'XYZ'
    while i < len(lines):
        l = lines[i]
        if l.startswith(':'):
            section = l.split()[0]
            if section == ':root':
                pass
            i += 1
            continue
        tok = l.split()
        if section == ':root':
            if tok[0] == 'order':
                root_order = [t.upper() for t in tok[1:]]
            elif tok[0] == 'axis':
                root_axis = tok[1].upper()
            elif tok[0] == 'orientation':
                bones['root'].C = _euler_mat([float(x) for x in tok[1:4]], root_axis)
        elif section == ':bonedata':
            if tok[0] == 'begin':
                cur = None
            elif tok[0] == 'name':
                cur = Bone(tok[1]); bones[cur.name] = cur
            elif tok[0] == 'direction':
                cur.direction = Vector([float(x) for x in tok[1:4]])
            elif tok[0] == 'length':
                cur.length = float(tok[1]) * UNIT
            elif tok[0] == 'axis':
                cur.C = _euler_mat([float(x) for x in tok[1:4]], tok[4].upper() if len(tok) > 4 else 'XYZ')
            elif tok[0] == 'dof':
                cur.dof = [t.lower() for t in tok[1:]]
        elif section == ':hierarchy':
            if tok[0] in ('begin', 'end'):
                pass
            else:
                p = bones[tok[0]]
                for c in tok[1:]:
                    bones[c].parent = p
                    p.children.append(bones[c])
        i += 1
    bones['root'].dof = [t.lower() for t in root_order]
    return bones


def load_amc(path):
    frames = []
    cur = None
    for l in open(path):
        l = l.strip()
        if not l or l.startswith('#') or l.startswith(':'):
            continue
        tok = l.split()
        if len(tok) == 1 and tok[0].isdigit():
            cur = {}
            frames.append(cur)
            continue
        cur[tok[0]] = [float(x) for x in tok[1:]]
    return frames


def order_bones(bones):
    out = []
    def walk(b):
        out.append(b)
        for c in b.children:
            walk(c)
    walk(bones['root'])
    return out


def pose_frame(bones, frame, ordered=None):
    """Returns dict name -> (G 3x3 global rotation delta from rest, start Vector, end Vector) in CMU frame (metres)."""
    ordered = ordered or order_bones(bones)
    out = {}
    for b in ordered:
        vals = frame.get(b.name, [])
        if b.name == 'root':
            d = dict(zip(b.dof, vals))
            pos = Vector((d.get('tx', 0), d.get('ty', 0), d.get('tz', 0))) * UNIT
            M = _euler_mat([d.get('rx', 0), d.get('ry', 0), d.get('rz', 0)], 'XYZ')
            G = b.C @ M @ b.C.inverted()
            out['root'] = (G, pos, pos)
            continue
        d = dict(zip(b.dof, vals))
        M = _euler_mat([d.get('rx', 0), d.get('ry', 0), d.get('rz', 0)], 'XYZ')
        Gp, _, pend = out[b.parent.name]
        G = Gp @ b.C @ M @ b.C.inverted()
        start = pend
        end = start + G @ (b.direction * b.length)
        out[b.name] = (G, start, end)
    return out
