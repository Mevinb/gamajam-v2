"""UAL (Quaternius Universal Animation Library) rig helpers: FK in armature space, world->basis conversion,
fast keyframe writing. Armature space of the UAL rig: Z up, character faces -Y, character's left = +X."""
import re
import bpy
from mathutils import Matrix, Quaternion, Vector

# Human bones of the UAL rig that we key (fingers keyed too, leaf bones ignored)
LEG = ['thigh', 'calf', 'foot', 'ball']
ARM = ['clavicle', 'upperarm', 'lowerarm', 'hand']
SPINE = ['pelvis', 'spine_01', 'spine_02', 'spine_03', 'neck_01', 'Head']
FINGERS = [f'{f}_0{i}' for f in ('thumb', 'index', 'middle', 'ring', 'pinky') for i in (1, 2, 3)]


def side(names, s):
    return [f'{n}_{s}' for n in names]


class Rig:
    def __init__(self, arm):
        self.arm = arm
        bones = arm.data.bones
        self.order = []
        def walk(b):
            self.order.append(b.name)
            for c in b.children:
                walk(c)
        for b in bones:
            if b.parent is None:
                walk(b)
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}
        self.rest = {b.name: b.matrix_local.copy() for b in bones}
        self.rest_rel = {}
        for n in self.order:
            p = self.parent[n]
            self.rest_rel[n] = (self.rest[p].inverted() @ self.rest[n]) if p else self.rest[n].copy()
        self.keyed = [n for n in self.order if not n.endswith('leaf_l') and not n.endswith('leaf_r')]

    # ---- pose representation: dict name -> (Quaternion rot, Vector loc) in bone-local (basis) space
    def identity_pose(self):
        return {n: (Quaternion(), Vector()) for n in self.order}

    def fk(self, pose):
        W = {}
        for n in self.order:
            q, l = pose.get(n, (Quaternion(), Vector()))
            B = Matrix.Translation(l) @ q.to_matrix().to_4x4()
            p = self.parent[n]
            W[n] = (W[p] @ self.rest_rel[n] @ B) if p else (self.rest_rel[n] @ B)
        return W

    def basis_rot(self, name, world_rot3, W):
        """Basis quaternion so that bone `name` gets armature-space rotation world_rot3 given parent's pose in W."""
        p = self.parent[name]
        P = (W[p] @ self.rest_rel[name]) if p else self.rest_rel[name]
        return (P.to_3x3().normalized().inverted() @ world_rot3).to_quaternion()

    def basis_loc(self, name, head_pos, W):
        p = self.parent[name]
        P = (W[p] @ self.rest_rel[name]) if p else self.rest_rel[name]
        return P.inverted() @ head_pos

    def set_world(self, pose, W, name, rot3=None, head=None):
        """Set bone world rotation/position in pose and update W for this bone only (children must be recomputed)."""
        q, l = pose[name]
        if rot3 is not None:
            q = self.basis_rot(name, rot3, W)
        if head is not None:
            l = self.basis_loc(name, head, W)
        pose[name] = (q, l)
        p = self.parent[name]
        B = Matrix.Translation(l) @ q.to_matrix().to_4x4()
        W[name] = ((W[p] @ self.rest_rel[name]) if p else self.rest_rel[name]) @ B

    def refresh(self, pose, W, start):
        """Recompute W for `start` and all its descendants."""
        idx = self.order.index(start)
        sub = set([start])
        for n in self.order[idx:]:
            if n == start or self.parent[n] in sub:
                sub.add(n)
                q, l = pose[n]
                B = Matrix.Translation(l) @ q.to_matrix().to_4x4()
                p = self.parent[n]
                W[n] = ((W[p] @ self.rest_rel[n]) if p else self.rest_rel[n]) @ B

    def bone_dir_rest(self, name):
        return (self.rest[name].to_3x3() @ Vector((0, 1, 0))).normalized()

    def length(self, name):
        return self.arm.data.bones[name].length


class ActionSampler:
    """Evaluates an imported action's F-curves directly (no depsgraph) -> pose dicts."""
    def __init__(self, rig, action):
        self.rig = rig
        self.action = action
        self.f0, self.f1 = action.frame_range
        cur = {}
        for fc in action.fcurves:
            m = re.match(r'pose\.bones\["([^"]+)"\]\.(rotation_quaternion|location)', fc.data_path)
            if m:
                cur.setdefault(m.group(1), {}).setdefault(m.group(2), {})[fc.array_index] = fc
        self.curves = cur

    @property
    def frames(self):
        return int(round(self.f1 - self.f0))

    def pose(self, frame):
        """frame is absolute action frame (f0..f1)."""
        out = {}
        for n in self.rig.order:
            c = self.curves.get(n, {})
            r = c.get('rotation_quaternion'); l = c.get('location')
            q = Quaternion([r[i].evaluate(frame) if (r and i in r) else (1.0 if i == 0 else 0.0) for i in range(4)]) if r else Quaternion()
            q.normalize()
            v = Vector([l[i].evaluate(frame) if (l and i in l) else 0.0 for i in range(3)]) if l else Vector()
            out[n] = (q, v)
        return out

    def at(self, t, fps=30.0):
        """t in seconds from clip start."""
        return self.pose(self.f0 + t * fps)


def sample_action(rig, action, frame):
    return ActionSampler(rig, action).pose(frame)


def action_frames(action):
    return int(round(action.frame_range[0])), int(round(action.frame_range[1]))


def write_action(rig, name, poses, fps=30, loc_bones=None):
    """Create an action from a list of pose dicts (one per frame starting at frame 1)."""
    arm = rig.arm
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    slot = act.slots[0] if len(act.slots) else None
    if slot is None:
        slot = act.slots.new(id_type='OBJECT', name=arm.name)
    arm.animation_data.action_slot = slot
    n = len(poses)
    frames = [float(i + 1) for i in range(n)]
    if loc_bones is None:
        loc_bones = set(bn for bn in rig.keyed if any(p[bn][1].length > 1e-5 for p in poses)) | {'pelvis'}
    # make quaternions continuous (hemisphere)
    for bn in rig.keyed:
        prev = None
        qs = []
        for p in poses:
            q = p[bn][0].copy()
            if prev is not None and prev.dot(q) < 0:
                q.negate()
            qs.append(q); prev = q
        for i in range(4):
            fc = act.fcurve_ensure_for_datablock(arm, f'pose.bones["{bn}"].rotation_quaternion', index=i)
            fc.keyframe_points.add(n)
            co = []
            for fr, q in zip(frames, qs):
                co += [fr, q[i]]
            fc.keyframe_points.foreach_set('co', co)
            fc.keyframe_points.foreach_set('interpolation', [1] * n)  # LINEAR
            fc.update()
        if bn in loc_bones:
            for i in range(3):
                fc = act.fcurve_ensure_for_datablock(arm, f'pose.bones["{bn}"].location', index=i)
                fc.keyframe_points.add(n)
                co = []
                for fr, p in zip(frames, poses):
                    co += [fr, p[bn][1][i]]
                fc.keyframe_points.foreach_set('co', co)
                fc.keyframe_points.foreach_set('interpolation', [1] * n)
                fc.update()
    act.frame_range = (1, n)
    act.use_frame_range = True
    return act
