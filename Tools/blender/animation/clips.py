"""Clip definitions. Each clip function returns (poses, meta): a list of pose dicts at 30 fps (for loops the last
pose equals the first) and a dict of extra manifest info."""
import math, os
from collections import namedtuple
from mathutils import Matrix, Quaternion, Vector
import gait, retarget
from posing import (V, R, smooth, smoother, keys, wave, lnoise, slerp_pose, copy_pose, lerp, FWD, UP)

FPS = 30.0
Clip = namedtuple('Clip', 'name fn loop source')
CLIPS = []


def clip(name, loop, source):
    def deco(fn):
        CLIPS.append(Clip(name, fn, loop, source))
        return fn
    return deco


class Context:
    def __init__(self, R_, P, S, src):
        self.R = R_; self.P = P; self.S = S; self.src = src
        idle = S['Idle_Loop'].pose(S['Idle_Loop'].f0)
        W = R_.fk(idle)
        self.ankle_h = (W['foot_l'].translation.z + W['foot_r'].translation.z) / 2
        self.ball_h = (W['ball_l'].translation.z + W['ball_r'].translation.z) / 2
        self.idle0 = idle
        self.cache = {}
        self.built = {}
        self.walk_speed = 0.0

    def ual(self, name, f0=None, f1=None):
        s = self.S[name]
        f0 = s.f0 if f0 is None else f0
        f1 = s.f1 if f1 is None else f1
        return [s.pose(float(f)) for f in range(int(f0), int(f1) + 1)]

    def frames(self, seconds):
        return int(round(seconds * FPS))

    def build(self, seconds, fn):
        """Generate n+1 poses by calling fn(t, T) for t in [0, T]."""
        n = self.frames(seconds)
        T = n / FPS
        return [fn(i / FPS, T) for i in range(n + 1)]

    def build_loop(self, seconds, fn):
        poses = self.build(seconds, fn)
        poses[-1] = copy_pose(poses[0])
        return poses

    def rotate_loop(self, poses, k):
        n = len(poses) - 1
        k %= n
        out = poses[k:n] + poses[:k]
        return out + [copy_pose(out[0])]

    def left_forward_index(self, poses):
        tr = gait.tracks(self.R, poses[:-1], ['foot_l'])['foot_l']
        return min(range(len(tr)), key=lambda i: tr[i].y)  # forward = -Y

    def cmu(self, trial, src_fps=120):
        key = (trial, src_fps)
        if key not in self.cache:
            subj = trial.split('_')[0]
            d = os.path.join(self.src, 'cmu')
            poses, scale = retarget.retarget(self.R, os.path.join(d, subj + '.asf'), os.path.join(d, trial + '.amc'),
                                             0, 10 ** 7, src_fps=src_fps, fps=src_fps, base_pose=self.idle0)
            self.cache[key] = poses
        return self.cache[key]


# ---------------------------------------------------------------- shared helpers
def breathe(P, pose, W, b, amp=1.0, lying=False):
    """b in [-1,1]: +1 full inhale. Chest lift via spine extension + clavicles."""
    s = -1.0 if lying else 1.0
    P.rotate(pose, W, 'spine_02', R(pitch=-0.7 * b * amp * s))
    P.rotate(pose, W, 'spine_03', R(pitch=-0.9 * b * amp * s))
    if not lying:
        P.rotate(pose, W, 'clavicle_l', R(roll=-1.2 * b * amp))
        P.rotate(pose, W, 'clavicle_r', R(roll=1.2 * b * amp))
        P.rotate(pose, W, 'neck_01', R(pitch=1.0 * b * amp))


def breath_curve(t, T, k=1):
    """Breathing 0..1 (quicker inhale, slower exhale), loops k times over T; C1 continuous."""
    x = (k * t / T) % 1.0
    return smooth(x / 0.4) if x < 0.4 else 1.0 - smooth((x - 0.4) / 0.6)


def ual_loop(ctx, name, f0=None, f1=None):
    poses = ctx.ual(name, f0, f1)
    return gait.close_loop(ctx.R, poses)


def finish_locomotion(ctx, poses):
    k = ctx.left_forward_index(poses)
    return ctx.rotate_loop(poses, k)


# ================================================================= LOCOMOTION
@clip('Idle_Neutral', True, 'UAL1 Idle_Loop')
def idle_neutral(ctx):
    return ual_loop(ctx, 'Idle_Loop'), {}


@clip('Walk', True, 'CMU 07_12 (brisk walk), retargeted, in-place, foot-locked')
def walk(ctx):
    P = ctx.P; Rg = ctx.R
    poses = ctx.cmu('07_12')
    st, _ = gait.find_strikes(Rg, poses, 'l', 120)
    i0, i1 = st[0], st[1]
    n = round((i1 - i0) / 120 * FPS)
    cyc = gait.resample(Rg, poses, 120, i0 / 120, i1 / 120, n)
    loop, _, D = gait.make_inplace_loop(Rg, cyc)
    speed = D.length / (n / FPS)
    loop = gait.close_loop(Rg, loop)
    gait.ground(Rg, loop, ctx.ankle_h, ctx.ball_h)
    loop = gait.lock_feet(P, loop, speed, ctx.ankle_h, ctx.ball_h, min_width=0.15)
    # relaxed UAL fingers
    for q in loop:
        W = Rg.fk(q)
        for s in ('l', 'r'):
            P.set_fingers(q, W, s, 'relaxed')
    loop = finish_locomotion(ctx, loop)
    ctx.walk_speed = speed
    return loop, {'speed': round(speed, 4)}


@clip('Run', True, 'UAL1 Jog_Fwd_Loop (in-place; speed from the root-motion version)')
def run(ctx):
    poses = ual_loop(ctx, 'Jog_Fwd_Loop')
    # natural speed: UAL1 _RM version travels 5.0 m in 28 frames
    speed = 5.0 / (28 / FPS)
    return finish_locomotion(ctx, poses), {'speed': round(speed, 4)}


# ================================================================= AIR
@clip('Jump_Takeoff', False, 'UAL1 Jump_Start f3-41')
def jump_takeoff(ctx):
    return ctx.ual('Jump_Start', 3, 41), {}


@clip('Fall_Loop', True, 'UAL1 Jump_Loop')
def fall_loop(ctx):
    return ual_loop(ctx, 'Jump_Loop'), {}


@clip('Land', False, 'UAL1 Jump_Land f1-34, crouch depth softened 35%')
def land(ctx):
    P = ctx.P; Rg = ctx.R
    src = ctx.ual('Jump_Land', 1, 34)
    out = []
    for i, p in enumerate(src):
        w = 0.35 * smooth(i / 3.0) * (1 - smooth((i - 18) / 14.0))
        W0 = Rg.fk(p)
        ank = P.ankles(W0)
        q = slerp_pose(Rg, p, ctx.idle0, w)
        W = Rg.fk(q)
        if i >= 3:
            P.plant(q, W, ank)
        out.append(q)
    return out, {}


# ================================================================= UNCONSCIOUS
@clip('Collapse', False, 'UAL1 Death01')
def collapse(ctx):
    return ctx.ual('Death01'), {}


def lying_pose(ctx):
    s = ctx.S['Death01']
    return s.pose(s.f1)


@clip('Unconscious_Loop', True, 'UAL1 Death01 final pose + authored breathing')
def unconscious(ctx):
    P = ctx.P; base = lying_pose(ctx)
    def f(t, T):
        q = copy_pose(base); W = P.fk(q)
        b = 0.5 - 0.5 * math.cos(2 * math.pi * t / T)
        breathe(P, q, W, b, amp=1.4, lying=True)
        P.rotate(q, W, 'Head', R(roll=0.6 * lnoise(t, T, 3)))
        return q
    return ctx.build_loop(4.0, f), {}


@clip('GetUp', False, 'Death01 final pose -> UAL2 LayToIdle (blended, re-anchored)')
def get_up(ctx):
    P = ctx.P; Rg = ctx.R
    lie = lying_pose(ctx)
    lay = ctx.ual('LayToIdle')
    Wl = Rg.fk(lie); W0 = Rg.fk(lay[0])
    off = Wl['pelvis'].translation - W0['pelvis'].translation
    off.z = 0
    def shifted(p, k):
        q = copy_pose(p); W = Rg.fk(q)
        Rg.set_world(q, W, 'pelvis', head=W['pelvis'].translation + off * k)
        return q
    out = []
    n0 = ctx.frames(0.6)
    for i in range(n0):
        x = smoother(i / n0)
        out.append(slerp_pose(Rg, lie, shifted(lay[0], 1.0), x))
    for i, p in enumerate(lay):
        k = 1.0 - smooth((i - 6) / 18.0)
        out.append(shifted(p, k))
    return out, {}


# ================================================================= POSES
@clip('Sit_Enter', False, 'UAL1 Sitting_Enter')
def sit_enter(ctx):
    return ctx.ual('Sitting_Enter'), {}


@clip('Sit_Exit', False, 'UAL1 Sitting_Exit')
def sit_exit(ctx):
    return ctx.ual('Sitting_Exit'), {}


@clip('Sit_Loop', True, 'UAL1 Sitting_Idle_Loop pose + authored calm breathing/hands in lap')
def sit_loop(ctx):
    P = ctx.P; Rg = ctx.R
    base = ctx.S['Sitting_Idle_Loop'].pose(ctx.S['Sitting_Idle_Loop'].f0)
    def f(t, T):
        q = copy_pose(base); W = P.fk(q)
        ank = P.ankles(W)
        wr = P.wrists(W)
        b = breath_curve(t, T, 2)
        P.spine(q, W, pitch=-3 + 0.8 * lnoise(t, T, 1), yaw=1.2 * lnoise(t, T, 7, (1,)))
        breathe(P, q, W, b, amp=1.1)
        P.head(q, W, pitch=-7 + 1.5 * lnoise(t, T, 2), yaw=4 * lnoise(t, T, 5, (1, 2)), roll=1.5 * lnoise(t, T, 9))
        # hands stay resting on the knees while the chest breathes
        for s, sg in (('l', -1), ('r', 1)):
            P.arm(q, W, s, wr[s][0], V(sg * 0.8, -0.4, -0.2), wr[s][1])
            W = P.fk(q)
        P.plant(q, W, ank)
        return q
    return ctx.build_loop(8.0, f), {}


@clip('Talk_Loop', True, 'UAL1 Idle_Talking_Loop')
def talk(ctx):
    return ual_loop(ctx, 'Idle_Talking_Loop'), {}


@clip('Idle_FoldArms', True, 'UAL2 Idle_FoldArms_Loop')
def idle_fold(ctx):
    return ual_loop(ctx, 'Idle_FoldArms_Loop'), {}
import clips_authored  # noqa: E402,F401  (registers authored clips)
