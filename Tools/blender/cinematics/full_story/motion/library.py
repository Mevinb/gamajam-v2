"""UAL -> actual PARASITE actor animation, entirely in armature space.

`MotionLibrary(project).apply_pose(arm, clip, seconds)` sets bone-local quaternion
and location components and returns {target_bone_name: armature_space_matrix}.
No scene.frame_set, depsgraph evaluation, object transforms, mesh edits or GPU
work is performed. Instantiate once and reuse it for every actor/frame.

Source clips use 30fps, with exact manifest loop/clamp timing. 30fps source
samples are lazily cached; arbitrary film times interpolate world quaternions
and hip translation. World rotation deltas relative to source rest are applied
to the target's own rest rotations, then converted to its local basis using
its actual parent hierarchy. This avoids copying incompatible bone-local
Euler/quaternion channels or replacing a real character by a generic rig.

root_motion=False strips only source `root` travel/turn, retaining articulated
pelvis motion (breathing, sway, crouch, collapse). True also transfers the
source root transform, scaled to actor leg length. Already in-place Walk/Run
remain in-place; the caller controls world travel. The target object placement
and any parent transform always belong to the caller.
"""
import sys
sys.dont_write_bytecode = True
import json
import math
import re
from pathlib import Path
import bpy
from mathutils import Matrix, Quaternion, Vector

FPS = 30.0
MAP = {
    'Root': 'root', 'Hips': 'pelvis', 'Spine': 'spine_01',
    'Chest': 'spine_02', 'UpperChest': 'spine_03', 'Neck': 'neck_01', 'Head': 'Head',
}
for side, suffix in (('Left', 'l'), ('Right', 'r')):
    for target, source in (('Shoulder', 'clavicle'), ('UpperArm', 'upperarm'),
                           ('LowerArm', 'lowerarm'), ('Hand', 'hand'),
                           ('UpperLeg', 'thigh'), ('LowerLeg', 'calf'),
                           ('Foot', 'foot'), ('Toes', 'ball')):
        MAP[side + target] = source + '_' + suffix
    for target, source in (('Thumb', 'thumb'), ('Index', 'index'), ('Middle', 'middle'),
                           ('Ring', 'ring'), ('Little', 'pinky')):
        for index, segment in enumerate(('Proximal', 'Intermediate', 'Distal'), 1):
            MAP[side + target + segment] = f'{source}_0{index}_{suffix}'


def _order(arm):
    ordered = []
    def visit(bone):
        ordered.append(bone.name)
        for child in bone.children:
            visit(child)
    for bone in arm.data.bones:
        if bone.parent is None:
            visit(bone)
    return ordered


def _curves(action):
    """One action slot, supporting legacy and Blender 4.4+ layered actions."""
    try:
        return list(action.fcurves)
    except (AttributeError, RuntimeError):
        result = []
        for layer in action.layers:
            for strip in layer.strips:
                for channelbag in getattr(strip, 'channelbags', ()):
                    result.extend(channelbag.fcurves)
        return result


class _Rig:
    def __init__(self, arm):
        self.arm = arm
        self.order = _order(arm)
        self.parents = {b.name: b.parent.name if b.parent else None for b in arm.data.bones}
        self.rest = {b.name: b.matrix_local.copy() for b in arm.data.bones}
        self.rest_q = {n: m.to_quaternion().normalized() for n, m in self.rest.items()}
        self.rel = {n: self.rest[p].inverted() @ self.rest[n] if p else self.rest[n].copy()
                    for n, p in self.parents.items()}
        self.rel_q = {n: m.to_quaternion().normalized() for n, m in self.rel.items()}
        self.rest_q_inv = {n: q.inverted() for n, q in self.rest_q.items()}

    def leg_length(self, names):
        return sum(self.arm.data.bones[n].length for n in names)

    def fk(self, pose):
        world = {}
        for n in self.order:
            q, loc = pose[n]
            basis = q.to_matrix().to_4x4()
            basis.translation = loc
            p = self.parents[n]
            world[n] = (world[p] @ self.rel[n] @ basis) if p else (self.rel[n] @ basis)
        return world


class _Action:
    def __init__(self, rig, action, info):
        self.rig = rig
        self.action = action
        self.info = info
        self.f0, self.f1 = [float(v) for v in action.frame_range]
        self.duration = (self.f1 - self.f0) / FPS
        self.last_index = int(round(self.f1 - self.f0))
        self.channels = {}
        self.cache = {}
        pattern = re.compile(r'pose\.bones\["([^"\\]+)"\]\.(rotation_quaternion|location|rotation_euler|scale)$')
        for fc in _curves(action):
            match = pattern.match(fc.data_path)
            if match:
                self.channels.setdefault(match[1], {}).setdefault(match[2], {})[fc.array_index] = fc
        if not any('rotation_quaternion' in c for c in self.channels.values()):
            raise ValueError(f'{action.name}: expected source quaternion animation channels')

    def _sample(self, index):
        if index in self.cache:
            return self.cache[index]
        frame = self.f0 + index
        source_pose = {}
        for n in self.rig.order:
            channels = self.channels.get(n, {})
            rotation = channels.get('rotation_quaternion', {})
            location = channels.get('location', {})
            q = Quaternion(tuple(rotation[i].evaluate(frame) if i in rotation else float(i == 0)
                                 for i in range(4))).normalized()
            loc = Vector(tuple(location[i].evaluate(frame) if i in location else 0.0 for i in range(3)))
            source_pose[n] = (q, loc)
        world = self.rig.fk(source_pose)
        root_delta = world['root'] @ self.rig.rest['root'].inverted()
        root_q = root_delta.to_quaternion().normalized()
        root_inv = root_delta.inverted()
        delta_q = {}
        for n in self.rig.order:
            relative = root_inv @ world[n]
            delta_q[n] = (relative.to_quaternion().normalized() @ self.rig.rest_q_inv[n]).normalized()
        hip_delta = (root_inv @ world['pelvis']).translation - self.rig.rest['pelvis'].translation
        result = (delta_q, hip_delta, root_q, root_delta.translation.copy())
        self.cache[index] = result
        return result

    def at(self, seconds):
        if not math.isfinite(seconds):
            raise ValueError('Clip time must be finite')
        if self.info.get('loop', False) and self.duration > 0:
            seconds = float(seconds) % self.duration
        else:
            seconds = max(0.0, min(self.duration, float(seconds)))
        frame = seconds * FPS
        i0 = min(self.last_index, int(math.floor(frame + 1e-8)))
        i1 = min(self.last_index, i0 + 1)
        weight = max(0.0, min(1.0, frame - i0))
        a = self._sample(i0)
        if i0 == i1 or weight < 1e-8:
            return a
        b = self._sample(i1)
        return ({n: a[0][n].slerp(b[0][n], weight) for n in self.rig.order},
                a[1].lerp(b[1], weight), a[2].slerp(b[2], weight), a[3].lerp(b[3], weight))


class MotionLibrary:
    """Deterministic source animation shared by all target rigs in one Blender process."""
    def __init__(self, project, optional_source_path=None):
        self.project = Path(project).resolve()
        compact = self.project / 'ArtSource/FullCinematics/Motion/UAL_story_source.blend'
        fallback = Path.home() / 'Data/parasite-work/animation/dev.blend'
        if not fallback.exists():
            fallback = Path.home() / 'Data/parasite-work/animation/build.blend'
        self.source_path = Path(optional_source_path) if optional_source_path else (compact if compact.exists() else fallback)
        manifest = self.project / 'ArtSource/FullCinematics/Motion/manifest.json'
        if not manifest.exists():
            manifest = self.project / 'Tools/blender/animation/manifest.json'
        self.manifest = {v['name']: v for v in json.loads(manifest.read_text())}
        with bpy.data.libraries.load(str(self.source_path), link=False) as (src, dst):
            names = [n for n in src.actions if n in self.manifest]
            objects = [n for n in src.objects if 'Armature' in n or 'MotionSource' in n]
            if not objects:
                raise ValueError(f'No source armature in {self.source_path}')
            dst.objects = objects[:]
            dst.actions = names[:]
        source_arm = next((obj for obj in dst.objects if obj and obj.type == 'ARMATURE'
                           and 'pelvis' in obj.data.bones and 'root' in obj.data.bones), None)
        if source_arm is None:
            raise ValueError('Source blend does not contain the UAL humanoid skeleton')
        source_arm.animation_data_clear()
        source_arm.name = 'PARASITE_MotionSource'
        source_arm.use_fake_user = True
        self.source_armature = source_arm
        self._source = _Rig(source_arm)
        self._source_leg = self._source.leg_length(('thigh_l', 'calf_l'))
        self._actions = {name: action for name, action in zip(names, dst.actions)}
        self._clips = {name: _Action(self._source, self._actions[name], self.manifest[name]) for name in names}
        self.available_clip_names = tuple(sorted(self._clips))
        self._targets = {}
        self._last_fk = {}

    def clip_info(self, clip_name):
        """Copy of provenance/timing metadata; duration derives from the exact action range."""
        clip = self._clip(clip_name)
        return dict(clip.info, seconds=clip.duration, fps=FPS,
                    action_frame_start=clip.f0, action_frame_end=clip.f1)

    def _clip(self, name):
        if name not in self._clips:
            raise KeyError(f'Unknown motion {name!r}. Available: {", ".join(self.available_clip_names)}')
        return self._clips[name]

    def _target(self, arm):
        if arm.type != 'ARMATURE':
            raise TypeError('Target must be a Blender armature object')
        key = arm.as_pointer()
        if key not in self._targets:
            rig = _Rig(arm)
            required = ('Hips', 'LeftUpperArm', 'RightUpperArm', 'LeftUpperLeg', 'LeftLowerLeg', 'Head')
            missing = [n for n in required if n not in rig.rest]
            if missing:
                raise ValueError(f'{arm.name}: missing PARASITE humanoid bones {missing}')
            rig.scale = rig.leg_length(('LeftUpperLeg', 'LeftLowerLeg')) / self._source_leg
            rig.mapping = {n: MAP[n] for n in rig.order if n in MAP and MAP[n] in self._source.rest}
            for bone in arm.pose.bones:
                bone.rotation_mode = 'QUATERNION'
            self._targets[key] = rig
        return self._targets[key]

    def _retarget(self, arm, clip_name, seconds, root_motion):
        rig = self._target(arm)
        delta, hip_delta, root_q, root_t = self._clip(clip_name).at(float(seconds))
        if not root_motion:
            root_q, root_t = Quaternion(), Vector()
        else:
            root_t = root_t * rig.scale
        pose, world, world_q = {}, {}, {}
        for n in rig.order:
            parent = rig.parents[n]
            pre = world[parent] @ rig.rel[n] if parent else rig.rel[n]
            pre_q = world_q[parent] @ rig.rel_q[n] if parent else rig.rel_q[n]
            source_name = rig.mapping.get(n)
            desired_q = (root_q @ delta[source_name] @ rig.rest_q[n]).normalized() if source_name else pre_q
            loc = Vector()
            head = pre.translation.copy()
            if n == 'Hips':
                head = root_q @ (rig.rest[n].translation + hip_delta * rig.scale) + root_t
                loc = pre.inverted() @ head
            elif n == 'Root':
                head = root_q @ rig.rest[n].translation + root_t
                loc = pre.inverted() @ head
            q = (pre_q.inverted() @ desired_q).normalized()
            pose[n] = (q, loc)
            matrix = desired_q.to_matrix().to_4x4()
            matrix.translation = head
            world[n], world_q[n] = matrix, desired_q
        return pose, world

    def pose_for(self, target_armature, clip_name, seconds, root_motion=False):
        """Return bone-local {name: (Quaternion, Vector)} without changing the target."""
        return self._retarget(target_armature, clip_name, seconds, root_motion)[0]

    def apply_pose(self, target_armature, clip_name, seconds, root_motion=False):
        """Apply sampled pose, return all target bone FK matrices in armature space."""
        pose, world = self._retarget(target_armature, clip_name, seconds, root_motion)
        for name, (q, loc) in pose.items():
            bone = target_armature.pose.bones[name]
            bone.rotation_quaternion = q
            bone.location = loc
            bone.scale = (1.0, 1.0, 1.0)
        self._last_fk[target_armature.as_pointer()] = world
        return world

    def fk(self, target_armature):
        """Recompute FK from current bone components after caller layers head/hand/jaw keys."""
        rig = self._target(target_armature)
        return rig.fk({b.name: (b.rotation_quaternion.copy(), b.location.copy()) for b in target_armature.pose.bones})

    def source_datablocks(self):
        """Minimal datablocks for bpy.data.libraries.write of an editable compact library."""
        return {self.source_armature, *self._actions.values()}
