"""Procedural pose authoring on the UAL rig.

Character-space convenience: V(right, forward, up) in metres -> armature space (x=left, -y=forward, z=up).
Rotation helpers take degrees: pitch>0 bends forward/looks down, yaw>0 turns to the character's right,
roll>0 tilts the top toward the character's right.
"""
import math
from mathutils import Matrix, Quaternion, Vector

FWD = Vector((0, -1, 0)); RIGHT = Vector((-1, 0, 0)); UP = Vector((0, 0, 1)); LEFT = Vector((1, 0, 0))


def V(r=0.0, f=0.0, u=0.0):
    return Vector((-r, -f, u))


def R(pitch=0.0, yaw=0.0, roll=0.0):
    """Character-space rotation matrix (3x3)."""
    Ry = Matrix.Rotation(math.radians(-yaw), 3, 'Z')
    Rp = Matrix.Rotation(math.radians(pitch), 3, 'X')
    Rr = Matrix.Rotation(math.radians(-roll), 3, 'Y')
    return Ry @ Rp @ Rr


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def smoother(x):
    x = max(0.0, min(1.0, x))
    return x * x * x * (x * (x * 6 - 15) + 10)


def lerp(a, b, t):
    return a + (b - a) * t


def keys(t, ks, ease=smooth):
    """Piecewise interpolation of (time, value) keys with easing; values may be floats/Vectors/tuples."""
    if t <= ks[0][0]:
        return ks[0][1]
    for (t0, v0), (t1, v1) in zip(ks, ks[1:]):
        if t <= t1:
            x = ease((t - t0) / max(1e-6, t1 - t0))
            if isinstance(v0, tuple):
                return tuple(lerp(a, b, x) for a, b in zip(v0, v1))
            return lerp(v0, v1, x)
    return ks[-1][1]


def wave(t, T, k=1, phase=0.0):
    """Loop-safe sine: k cycles per clip period T."""
    return math.sin(2 * math.pi * (k * t / T + phase))


def lnoise(t, T, seed=0, harmonics=(1, 2, 3), amp_falloff=0.55):
    """Smooth periodic pseudo-noise in [-1,1]-ish that loops with period T."""
    s = 0.0; a = 1.0; tot = 0.0
    for i, k in enumerate(harmonics):
        ph = math.sin(seed * 12.9898 + k * 78.233) * 43758.5453
        ph -= math.floor(ph)
        s += a * math.sin(2 * math.pi * (k * t / T + ph)); tot += a; a *= amp_falloff
    return s / tot


def slerp_pose(rig, pa, pb, x):
    out = {}
    for n in rig.order:
        qa, la = pa[n]; qb, lb = pb[n]
        if qa.dot(qb) < 0:
            qb = -qb
        out[n] = (qa.slerp(qb, x), la.lerp(lb, x))
    return out


def copy_pose(p):
    return {n: (q.copy(), l.copy()) for n, (q, l) in p.items()}


class Poser:
    def __init__(self, rig, samplers):
        self.rig = rig
        self.S = samplers
        self.restW = rig.fk(rig.identity_pose())
        self.rest_rot = {n: self.restW[n].to_3x3().normalized() for n in rig.order}
        # hinge axes in the upper bone's local frame, measured from strongly bent UAL poses
        self.hinge = {}
        sit = samplers['Sitting_Idle_Loop'].pose(samplers['Sitting_Idle_Loop'].f0)
        fold = samplers['Idle_FoldArms_Loop'].pose(samplers['Idle_FoldArms_Loop'].f0)
        for s in ('l', 'r'):
            self.hinge[f'thigh_{s}'] = self._measure_hinge(sit, f'thigh_{s}', f'calf_{s}', f'foot_{s}')
            self.hinge[f'upperarm_{s}'] = self._measure_hinge(fold, f'upperarm_{s}', f'lowerarm_{s}', f'hand_{s}')
        self.fingers = {}
        idle = samplers['Idle_Loop'].pose(samplers['Idle_Loop'].f0)
        self.finger_presets = {'relaxed': idle}
        # flexion sign of the finger hinge (local X) per side, measured from the relaxed (curled) idle fingers
        self.flex_sign = {}
        for s_ in ('l', 'r'):
            q = idle[f'middle_02_{s_}'][0]
            axis, ang = q.to_axis_angle()
            self.flex_sign[s_] = 1.0 if axis.x * ang >= 0 else -1.0
        # palm normal at rest (sign chosen to point down in the T-pose)
        self.palm_rest = {}
        for s in ('l', 'r'):
            W = self.restW
            h = W[f'hand_{s}'].translation
            a = W[f'index_01_{s}'].translation - h; b = W[f'pinky_01_{s}'].translation - h
            n = a.cross(b).normalized()
            if n.z > 0:
                n = -n
            self.palm_rest[s] = n

    def _measure_hinge(self, pose, upper, lower, end):
        W = self.rig.fk(pose)
        u = (W[lower].translation - W[upper].translation).normalized()
        l = (W[end].translation - W[lower].translation).normalized()
        h = u.cross(l).normalized()
        return W[upper].to_3x3().normalized().inverted() @ h

    # ------------------------------------------------------------ sampling
    def clip(self, name, t, loop=True):
        s = self.S[name]
        dur = (s.f1 - s.f0) / 30.0
        if loop and dur > 0:
            t = t % dur
        else:
            t = max(0.0, min(dur, t))
        return s.at(t)

    def clip_dur(self, name):
        s = self.S[name]
        return (s.f1 - s.f0) / 30.0

    # ------------------------------------------------------------ basic ops
    def fk(self, pose):
        return self.rig.fk(pose)

    def rotate(self, pose, W, bone, rot3):
        """Rotate bone in world (armature) space about its head; descendants follow."""
        cur = W[bone].to_3x3().normalized()
        self.rig.set_world(pose, W, bone, rot3=rot3 @ cur)
        self.rig.refresh(pose, W, bone)

    def set_rot(self, pose, W, bone, rot3):
        self.rig.set_world(pose, W, bone, rot3=rot3)
        self.rig.refresh(pose, W, bone)

    def move_pelvis(self, pose, W, delta=None, pos=None, rot3=None):
        cur = W['pelvis']
        head = pos if pos is not None else cur.translation + (delta if delta is not None else Vector())
        r = (rot3 @ cur.to_3x3().normalized()) if rot3 is not None else cur.to_3x3().normalized()
        self.rig.set_world(pose, W, 'pelvis', rot3=r, head=head)
        self.rig.refresh(pose, W, 'pelvis')

    def spine(self, pose, W, pitch=0, yaw=0, roll=0, w=(0.3, 0.35, 0.35)):
        for b, k in zip(('spine_01', 'spine_02', 'spine_03'), w):
            self.rotate(pose, W, b, R(pitch * k, yaw * k, roll * k))

    def head(self, pose, W, pitch=0, yaw=0, roll=0, w=(0.4, 0.6)):
        for b, k in zip(('neck_01', 'Head'), w):
            self.rotate(pose, W, b, R(pitch * k, yaw * k, roll * k))

    def point(self, W, bone, offset=Vector()):
        """Point attached to a bone: offset given in rest-pose armature directions, rotates with the bone."""
        delta = W[bone].to_3x3().normalized() @ self.rest_rot[bone].inverted()
        return W[bone].translation + delta @ offset

    def delta_rot(self, W, bone):
        return W[bone].to_3x3().normalized() @ self.rest_rot[bone].inverted()

    # ------------------------------------------------------------ IK
    def limb(self, pose, W, upper, lower, end, target, pole, end_rot=None):
        rig = self.rig
        if end_rot is None:
            end_rot = W[end].to_3x3().normalized()
        S = W[upper].translation
        a = (W[lower].translation - S).length
        b = (W[end].translation - W[lower].translation).length
        d = target - S
        dl = max(abs(a - b) + 1e-4, min(d.length, (a + b) * 0.9995))
        dn = d.normalized()
        p = pole - dn * pole.dot(dn)
        if p.length < 1e-6:
            p = (W[lower].translation - S) - dn * (W[lower].translation - S).dot(dn)
        p.normalize()
        cosA = max(-1.0, min(1.0, (a * a + dl * dl - b * b) / (2 * a * dl)))
        sinA = math.sqrt(max(0.0, 1 - cosA * cosA))
        E = S + dn * (a * cosA) + p * (a * sinA)
        T = S + dn * dl
        ud = (E - S).normalized(); ld = (T - E).normalized()
        h = ud.cross(ld)
        if h.length < 1e-4:
            h = p.cross(dn)
        h.normalize()
        hl = self.hinge[upper].copy()
        yl = Vector((0, 1, 0))
        hl = (hl - yl * hl.dot(yl)).normalized()
        Ml = Matrix((yl, hl, yl.cross(hl))).transposed()
        hw = (h - ud * h.dot(ud)).normalized()
        Mw = Matrix((ud, hw, ud.cross(hw))).transposed()
        self.set_rot(pose, W, upper, Mw @ Ml.transposed())
        cur = W[lower].to_3x3().normalized()
        cd = (cur @ yl).normalized()
        self.set_rot(pose, W, lower, cd.rotation_difference(ld).to_matrix() @ cur)
        self.set_rot(pose, W, end, end_rot)

    def arm(self, pose, W, s, target, pole, hand_rot=None):
        self.limb(pose, W, f'upperarm_{s}', f'lowerarm_{s}', f'hand_{s}', target, pole, hand_rot)

    def leg(self, pose, W, s, target, pole=None, foot_rot=None):
        if pole is None:
            pole = V(0.12 if s == 'r' else -0.12, 1.0, 0.0)
        self.limb(pose, W, f'thigh_{s}', f'calf_{s}', f'foot_{s}', target, pole, foot_rot)

    # ------------------------------------------------------------ orientations
    def hand_rot(self, s, fingers_dir, palm_dir):
        """World rotation for hand `s` whose fingers point along fingers_dir with the palm facing palm_dir."""
        f0 = self.rig.bone_dir_rest(f'hand_{s}'); n0 = self.palm_rest[s]
        n0 = (n0 - f0 * n0.dot(f0)).normalized()
        f1 = fingers_dir.normalized(); n1 = (palm_dir - f1 * palm_dir.dot(f1)).normalized()
        M0 = Matrix((f0, n0, f0.cross(n0))).transposed()
        M1 = Matrix((f1, n1, f1.cross(n1))).transposed()
        return M1 @ M0.transposed() @ self.rest_rot[f'hand_{s}']

    def foot_flat(self, s, out_deg=0.0, pitch=0.0):
        """Rest-like flat foot, toe turned outward by out_deg."""
        yaw = out_deg if s == 'r' else -out_deg
        return R(pitch=pitch, yaw=yaw) @ self.rest_rot[f'foot_{s}']

    def set_fingers(self, pose, W, s, preset, weight=1.0):
        src = self.finger_presets[preset] if isinstance(preset, str) else preset
        for f in ('thumb', 'index', 'middle', 'ring', 'pinky'):
            for i in (1, 2, 3):
                n = f'{f}_0{i}_{s}'
                if n in pose:
                    q = src[n][0]
                    if weight < 1:
                        q0 = pose[n][0]
                        if q0.dot(q) < 0:
                            q = -q
                        q = q0.slerp(q, weight)
                    pose[n] = (q.copy(), pose[n][1])
        self.rig.refresh(pose, W, f'hand_{s}')

    def curl(self, pose, W, s, finger, deg, joints=(1, 2, 3)):
        """Curl one finger by deg about its local hinge (local X)."""
        for i in joints:
            n = f'{finger}_0{i}_{s}'
            q, l = pose[n]
            pose[n] = ((q @ Quaternion(Vector((1, 0, 0)), math.radians(deg * self.flex_sign[s]))), l)
        self.rig.refresh(pose, W, f'hand_{s}')

    def curl_all(self, pose, W, s, deg, thumb=0.4):
        for f in ('index', 'middle', 'ring', 'pinky'):
            self.curl(pose, W, s, f, deg)
        if thumb:
            self.curl(pose, W, s, 'thumb', deg * thumb, joints=(2, 3))

    def foot_rot(self, s, toe_dir, top_dir):
        """World rotation for foot `s` pointing (ankle->ball) along toe_dir with the instep facing top_dir."""
        d0 = self.rig.bone_dir_rest(f'foot_{s}')
        n0 = (UP - d0 * UP.dot(d0)).normalized()
        d1 = toe_dir.normalized(); n1 = (top_dir - d1 * top_dir.dot(d1)).normalized()
        M0 = Matrix((d0, n0, d0.cross(n0))).transposed()
        M1 = Matrix((d1, n1, d1.cross(n1))).transposed()
        return M1 @ M0.transposed() @ self.rest_rot[f'foot_{s}']

    def ankles(self, W):
        return {s: (W[f'foot_{s}'].translation.copy(), W[f'foot_{s}'].to_3x3().normalized()) for s in ('l', 'r')}

    def wrists(self, W):
        return {s: (W[f'hand_{s}'].translation.copy(), W[f'hand_{s}'].to_3x3().normalized()) for s in ('l', 'r')}

    def plant(self, pose, W, ank, poles=None):
        for s in ('l', 'r'):
            p, r = ank[s]
            self.leg(pose, W, s, p, None if not poles else poles.get(s), r)
