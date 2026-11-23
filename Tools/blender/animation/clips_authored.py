"""Hand-authored (procedural, keyed by script) clips on the UAL rig: profession idles, possessed gait, captured,
console, panic, alert, point, gestures. Registered into clips.CLIPS on import.

Conventions: V(right, forward, up) metres; R(pitch, yaw, roll) degrees (pitch>0 bends forward, yaw>0 turns right,
roll>0 tilts right). Feet are re-planted with 2-bone IK after any pelvis/spine change."""
import math
from mathutils import Matrix, Quaternion, Vector
import gait
from clips import clip, ual_loop, breathe, breath_curve, finish_locomotion, lying_pose, FPS
from posing import (V, R, smooth, smoother, keys, wave, lnoise, slerp_pose, copy_pose, lerp, FWD, UP)

ANKLE_H = 0.1034
ARM_BONES = [f'{b}_{s}' for s in ('l', 'r') for b in ('clavicle', 'upperarm', 'lowerarm', 'hand')]
HAND_BONES = {s: [f'{f}_0{i}_{s}' for f in ('thumb', 'index', 'middle', 'ring', 'pinky') for i in (1, 2, 3)] for s in ('l', 'r')}


# ------------------------------------------------------------------ helpers
def yaw_of(P, W, bone):
    f = P.delta_rot(W, bone) @ Vector((0, -1, 0))
    return math.degrees(math.atan2(-f.x, -f.y))  # + = turned to the character's right (toward -X)


def face_forward(P, q, W, chest=True, head=True):
    """Remove the idle's body yaw so pelvis/chest/head face straight forward."""
    P.rotate(q, W, 'pelvis', R(yaw=-yaw_of(P, W, 'pelvis')))
    if chest:
        P.spine(q, W, yaw=-yaw_of(P, W, 'spine_03'))
    if head:
        P.head(q, W, yaw=-yaw_of(P, W, 'Head'))


def level_pelvis(P, q, W):
    l = W['thigh_l'].translation; r = W['thigh_r'].translation
    d = l - r
    roll = math.degrees(math.atan2(d.z, d.x))
    P.rotate(q, W, 'pelvis', R(roll=-roll))


def center_pelvis(P, q, W, dz=0.0, y=None):
    pel = W['pelvis'].translation
    tgt = Vector((0.0, pel.y if y is None else y, pel.z + dz))
    P.move_pelvis(q, W, pos=tgt)


def stance(P, q, W, width=0.30, fwd=0.03, toe_out=8.0, left_fwd=0.0, right_fwd=0.0):
    """Plant both feet symmetric under the pelvis (ankle centres `width` apart)."""
    pel = W['pelvis'].translation
    for s, sg, df in (('l', -1, left_fwd), ('r', 1, right_fwd)):
        tgt = Vector((0, pel.y, 0)) + V(sg * width / 2, fwd + df, 0)
        tgt.z = ANKLE_H
        P.leg(q, W, s, tgt, V(sg * 0.15, 1, 0), P.foot_flat(s, toe_out))


def base_idle(ctx, t, name='Idle_Loop'):
    return copy_pose(ctx.P.clip(name, t))


def arm_to(P, q, W, s, wrist, pole, fingers, palm):
    P.arm(q, W, s, wrist, pole, P.hand_rot(s, fingers.normalized(), palm.normalized()))


def sway(P, q, W, t, T, amp=0.012, seed=0):
    dx = amp * lnoise(t, T, seed, (1, 2))
    dy = amp * 0.5 * lnoise(t, T, seed + 11, (1, 3))
    P.move_pelvis(q, W, delta=V(dx, dy, 0))


def blend_bones(Rg, q, other, bones, w):
    for b in bones:
        qa, la = q[b]; qb, lb = other[b]
        if qa.dot(qb) < 0:
            qb = -qb
        q[b] = (qa.slerp(qb, w), la.lerp(lb, w))


def clamp_feet(P, poses, ankle_min=0.075, ball_min=0.012):
    """Lift any foot that dips below the floor (ankle or ball) with leg IK, keeping its orientation."""
    for q in poses:
        W = P.fk(q)
        for s in ('l', 'r'):
            a = W[f'foot_{s}'].translation; b = W[f'ball_{s}'].translation
            dz = max(ankle_min - a.z, ball_min - b.z, 0.0)
            if dz > 1e-4:
                knee = W[f'calf_{s}'].translation; hip = W[f'thigh_{s}'].translation
                P.leg(q, W, s, a + Vector((0, 0, dz)), knee - (hip + a) * 0.5, W[f'foot_{s}'].to_3x3().normalized())
                W = P.fk(q)
    return poses


def upright(ctx, t, width=0.32, toe_out=9.0, dz=0.0, chest=True, head=True):
    """UAL idle at time t, squared up: facing forward, level hips, symmetric stance (feet planted)."""
    P = ctx.P
    q = base_idle(ctx, t); W = P.fk(q)
    face_forward(P, q, W, chest=chest, head=head)
    level_pelvis(P, q, W)
    center_pelvis(P, q, W, dz)
    return q, W


def finish_stance(P, q, W, width=0.32, toe_out=9.0, fwd=0.02):
    stance(P, q, W, width=width, fwd=fwd, toe_out=toe_out)


# ================================================================= CIVILIAN IDLES
@clip('Idle_Shift', True, 'authored: slow weight shift between legs (UAL Idle_Loop upper-body base)')
def idle_shift(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.012, chest=False)
        x = t / T
        side = math.sin(2 * math.pi * x)               # +1 weight on right leg, -1 on left
        shift = 0.045 * math.copysign(abs(side) ** 0.6, side)
        P.move_pelvis(q, W, delta=V(shift, 0.0, -0.012 * abs(side)))
        P.rotate(q, W, 'pelvis', R(roll=-3.5 * side, yaw=2.5 * side))
        P.spine(q, W, roll=4.0 * side, yaw=-3.0 * side, pitch=1.0)
        breathe(P, q, W, breath_curve(t, T, 3), 0.9)
        P.head(q, W, roll=-1.5 * side, yaw=8 * lnoise(t, T, 4, (1, 2)), pitch=2 * lnoise(t, T, 9))
        stance(P, q, W, width=0.30, fwd=0.02, toe_out=10, left_fwd=0.06, right_fwd=-0.02)
        return q
    return ctx.build_loop(7.0, f), {}


@clip('Idle_LookAround', True, 'authored: idle glancing around and checking a wristwatch')
def idle_lookaround(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.005)
        sway(P, q, W, t, T, 0.01, 21)
        yaw = keys(t, [(0, 0), (1.0, 0), (1.6, 38), (2.9, 38), (3.6, -30), (4.8, -30), (5.4, 0), (T, 0)], smoother)
        watch = keys(t, [(0, 0), (5.6, 0), (6.1, 1), (7.3, 1), (7.8, 0), (T, 0)], smoother)
        P.spine(q, W, yaw=yaw * 0.3, pitch=1.5 * watch)
        breathe(P, q, W, breath_curve(t, T, 3), 0.9)
        P.head(q, W, yaw=yaw * 0.7, pitch=-2 + 26 * watch + 2 * lnoise(t, T, 3))
        finish_stance(P, q, W, 0.30, 9)
        if watch > 0.001:
            W = P.fk(q)
            ik = copy_pose(q)
            Wk = P.fk(ik)
            sh = Wk['upperarm_l'].translation
            arm_to(P, ik, Wk, 'l', sh + V(0.10, 0.30, -0.24), V(-1, -0.2, -0.6), V(1, 0.35, 0.2), V(0, -0.2, -1))
            blend_bones(P.rig, q, ik, ['clavicle_l', 'upperarm_l', 'lowerarm_l', 'hand_l'], watch)
            P.rig.refresh(q, W, 'clavicle_l')
        return q
    return ctx.build_loop(9.0, f), {}


# ================================================================= PROFESSION IDLES
@clip('Idle_Guard', True, 'authored: parade rest (UAL Idle_Loop upper-body base)')
def idle_guard(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.025)
        sway(P, q, W, t, T, 0.007, 3)
        P.spine(q, W, pitch=-3)
        P.rotate(q, W, 'clavicle_l', R(yaw=-6)); P.rotate(q, W, 'clavicle_r', R(yaw=6))
        breathe(P, q, W, breath_curve(t, T, 2), 0.8)
        P.head(q, W, pitch=-2 + 2 * lnoise(t, T, 5), yaw=12 * lnoise(t, T, 2, (1,)) + 3 * lnoise(t, T, 8, (2, 3)))
        finish_stance(P, q, W, 0.36, 10)
        pel = P.point(W, 'pelvis')
        arm_to(P, q, W, 'r', pel + V(0.07, -0.19, 0.13), V(1, -0.5, -0.3), V(-1, 0, -0.35), V(0, -1, 0.1))
        W = P.fk(q)
        rw = W['hand_r'].translation
        arm_to(P, q, W, 'l', rw + V(-0.07, -0.025, -0.01), V(-1, -0.5, -0.3), V(1, 0, -0.5), V(0, 1, 0))
        P.curl_all(q, W, 'l', 25)
        return q
    return ctx.build_loop(5.0, f), {}


@clip('Idle_GuardBelt', True, 'authored: guard, thumbs hooked in the belt, scanning')
def idle_guard_belt(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.018, chest=False)
        sway(P, q, W, t, T, 0.014, 31)
        P.spine(q, W, pitch=-2, yaw=5 * lnoise(t, T, 32, (1,)))
        breathe(P, q, W, breath_curve(t, T, 2), 0.9)
        look = keys(t, [(0, 0), (1.2, 0), (2.0, -28), (3.0, -28), (3.8, 0), (T, 0)], smoother)
        P.head(q, W, yaw=look + 5 * lnoise(t, T, 33), pitch=-1 + 2 * lnoise(t, T, 34))
        finish_stance(P, q, W, 0.38, 12)
        pel = P.point(W, 'pelvis')
        for s, sg in (('l', -1), ('r', 1)):
            arm_to(P, q, W, s, pel + V(sg * 0.15, 0.12, 0.11), V(sg * 1, -0.6, -0.2), V(-sg * 0.35, 0.3, -1), V(sg * 0.2, -1, 0))
            W = P.fk(q)
            P.curl_all(q, W, s, 18)
        return q
    return ctx.build_loop(6.0, f), {}


@clip('Idle_Clipboard', True, 'authored: scientist reading/writing on a held clipboard')
def idle_clipboard(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.008, chest=False)
        sway(P, q, W, t, T, 0.008, 41)
        up = keys(t, [(0, 0), (3.6, 0), (4.0, 1), (5.2, 1), (5.6, 0), (T, 0)], smoother)
        P.spine(q, W, pitch=7 - 5 * up)
        breathe(P, q, W, breath_curve(t, T, 2), 0.8)
        P.head(q, W, pitch=24 - 26 * up + 2 * lnoise(t, T, 42), yaw=-6 + 10 * up * lnoise(t, T, 43, (1,)))
        finish_stance(P, q, W, 0.30, 9)
        W = P.fk(q)
        chest = P.point(W, 'spine_03')
        board = chest + V(0.02, 0.27, -0.22)
        # left forearm cradles the board, hand gripping its left edge
        arm_to(P, q, W, 'l', board + V(-0.09, 0.02, 0.0), V(-1, -0.6, -0.9), V(0.3, 0.6, 0.55), V(0.95, 0, -0.25))
        W = P.fk(q)
        P.curl_all(q, W, 'l', 35)
        write = (1 - up)
        wx = 0.03 * math.sin(2 * math.pi * t * 1.3) * write
        wy = 0.012 * math.sin(2 * math.pi * t * 4.1) * write
        arm_to(P, q, W, 'r', board + V(0.06 + wx, -0.03 + wy, 0.05 + 0.03 * up), V(1, -0.5, -0.8), V(-0.4, 0.8, -0.35), V(0.1, 0.2, -1))
        W = P.fk(q)
        P.curl_all(q, W, 'r', 30)
        return q
    return ctx.build_loop(7.0, f), {}


@clip('Idle_HandsOnHips', True, 'authored: hands on hips (maintenance), UAL Idle_Loop base')
def idle_hips(ctx):
    P = ctx.P
    def f(t, T):
        q = base_idle(ctx, t); W = P.fk(q)
        ank = P.ankles(W)
        face_forward(P, q, W, chest=False)
        sway(P, q, W, t, T, 0.015, 4)
        P.spine(q, W, pitch=-4 + 1.5 * lnoise(t, T, 2), yaw=6 * lnoise(t, T, 3, (1,)))
        breathe(P, q, W, breath_curve(t, T, 2), 1.0)
        P.head(q, W, pitch=3 * lnoise(t, T, 6), yaw=14 * lnoise(t, T, 7, (1, 2)))
        P.plant(q, W, ank)
        pel = P.point(W, 'pelvis')
        for s, sg in (('l', -1), ('r', 1)):
            arm_to(P, q, W, s, pel + V(sg * 0.21, -0.01, 0.10), V(sg * 1, -0.35, 0.05), V(0, 0.55, -1), V(-sg, 0, 0))
            W = P.fk(q)
        return q
    return ctx.build_loop(5.0, f), {}


# ================================================================= POSSESSED
def stiff_arms(P, q, W, t, T, lean=1.0):
    """Arms hanging nearly straight, slightly forward, like a puppet."""
    pel = P.point(W, 'pelvis')
    for s, sg in (('l', -1), ('r', 1)):
        sh = W[f'upperarm_{s}'].translation
        tgt = Vector((sh.x, sh.y, 0)) + V(sg * 0.035, 0.07 * lean, 0)
        tgt.z = sh.z - 0.535
        arm_to(P, q, W, s, tgt, V(sg * 0.4, -1, 0), V(0, 0.15, -1), V(-sg, 0.1, 0))
        W = P.fk(q)
    return W


@clip('Idle_Possessed', True, 'authored: uncanny stiff idle with twitches (possessed host)')
def idle_possessed(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.022)
        P.move_pelvis(q, W, delta=V(0.012 * wave(t, T, 1), 0, 0))
        P.spine(q, W, pitch=4, roll=-2.5)
        P.rotate(q, W, 'clavicle_l', R(roll=-5)); P.rotate(q, W, 'clavicle_r', R(roll=2))
        breathe(P, q, W, breath_curve(t, T, 1), 0.6)
        jerk = keys(t, [(0, 0), (2.0, 0), (2.07, 1), (2.6, 0.75), (3.6, 0), (T, 0)], smooth)
        hitch = keys(t, [(0, 0), (4.6, 0), (4.68, 1), (5.2, 0), (T, 0)], smooth)
        P.head(q, W, roll=9 + 7 * jerk, pitch=6 - 4 * jerk + 1.5 * lnoise(t, T, 51), yaw=4 * lnoise(t, T, 52, (1,)) - 6 * jerk)
        P.rotate(q, W, 'clavicle_r', R(roll=-6 * hitch))
        finish_stance(P, q, W, 0.30, 5)
        W = stiff_arms(P, q, W, t, T)
        spasm = keys(t, [(0, 0), (3.4, 0), (3.47, 1), (3.9, 0), (T, 0)], smooth)
        P.curl_all(q, W, 'r', 10 + 45 * spasm)
        P.curl_all(q, W, 'l', 12)
        return q
    return ctx.build_loop(6.0, f), {}


def possessed_overlay(ctx, src, arm_w=0.65, roll=7.0, lurch=1.0):
    P = ctx.P; Rg = ctx.R
    n = len(src) - 1
    T = n / FPS
    out = []
    for i, p in enumerate(src):
        t = i / FPS
        x = i / n
        q = copy_pose(p); W = P.fk(q)
        ank = P.ankles(W)
        # lurch on the right-foot contact (half way through the cycle)
        pulse = math.exp(-((x - 0.56) / 0.09) ** 2) * lurch
        P.move_pelvis(q, W, delta=V(0.012 * pulse, 0, -0.018 * pulse), rot3=R(roll=4.5 * pulse))
        P.spine(q, W, roll=-3.5 * pulse, pitch=3)
        P.head(q, W, roll=roll + 2 * pulse, pitch=5 - 3 * pulse)
        P.plant(q, W, ank)
        stiff = copy_pose(q); Ws = P.fk(stiff)
        stiff_arms(P, stiff, Ws, t, T, lean=0.3)
        blend_bones(Rg, q, stiff, ['upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r'], arm_w)
        P.rig.refresh(q, W, 'pelvis')
        P.curl_all(q, W, 'l', 14); P.curl_all(q, W, 'r', 22)
        out.append(q)
    out[-1] = copy_pose(out[0])
    return out


@clip('Walk_Possessed', True, 'Walk + authored stiff arms, head tilt and right-step lurch (same timing/speed as Walk)')
def walk_possessed(ctx):
    src = ctx.built['Walk']
    return possessed_overlay(ctx, src, 0.65, 7.0, 1.0), {'speed': round(ctx.walk_speed, 4)}


@clip('Run_Possessed', True, 'Run + authored damped arm swing and head tilt (same timing/speed as Run)')
def run_possessed(ctx):
    src = ctx.built['Run']
    return possessed_overlay(ctx, src, 0.35, 5.0, 0.4), {'speed': round(5.0 / (28 / FPS), 4)}


# ================================================================= CAPTURED
def kneel_geometry(P, q, W):
    """Pelvis placement for an upright double kneel; returns per-side (knee, ankle) targets."""
    out = {}
    for s, sg in (('l', -1), ('r', 1)):
        H = W[f'thigh_{s}'].translation
        K = Vector((H.x - sg * 0.0 * 0, H.y - 0.10, 0.075))
        dz = math.sqrt(max(0.0, 0.40 ** 2 - (H.y - K.y) ** 2))
        out[s] = (H, K, dz)
    return out


def kneel_pose(ctx, base, breath=0.0, look=(0.0, 0.0, 0.0)):
    """Double kneel, upright torso, hands interlaced behind the head."""
    P = ctx.P
    q = copy_pose(base); W = P.fk(q)
    face_forward(P, q, W)
    level_pelvis(P, q, W)
    # pelvis height so the thighs (0.40 m) reach knees on the floor 10 cm forward of the hips
    hipz = 0.075 + math.sqrt(0.40 ** 2 - 0.10 ** 2)
    pel = W['pelvis'].translation
    hip_c = (W['thigh_l'].translation + W['thigh_r'].translation) * 0.5
    P.move_pelvis(q, W, pos=Vector((0.0, 0.05, pel.z + (hipz - hip_c.z))))
    P.rotate(q, W, 'pelvis', R(pitch=4))
    P.spine(q, W, pitch=-4)
    breathe(P, q, W, breath, 1.2)
    P.head(q, W, pitch=14 + look[0], yaw=look[1], roll=look[2])
    kneel_legs(P, q, W)
    hands_behind_head(P, q, W)
    return q


def kneel_legs(P, q, W, which=('l', 'r'), mix=1.0):
    for s, sg in (('l', -1), ('r', 1)):
        if s not in which:
            continue
        H = W[f'thigh_{s}'].translation
        K = Vector((H.x - sg * 0.02, H.y - 0.10, 0.075))
        A = K + Vector((0, 0.40, 0.102))
        A.x = K.x
        pole = K - (H + A) * 0.5
        fr = P.foot_rot(s, V(0, -0.35, -1), V(0, 1, 0))
        P.leg(q, W, s, A, pole, fr)


def hands_behind_head(P, q, W):
    hd = W['Head'].translation
    for s, sg in (('l', -1), ('r', 1)):
        wrist = P.point(W, 'Head', V(sg * 0.105, -0.085, 0.11))
        arm_to(P, q, W, s, wrist, V(sg * 1, 0.35, 0.25), V(-sg * 0.8, -0.45, 0.35), V(-sg * 0.5, 1, 0))
        W = P.fk(q)
        P.curl_all(q, W, s, 30, thumb=0.2)
    return W


@clip('Captured_Loop', True, 'authored: double kneel, hands behind head, nervous breathing')
def captured_loop(ctx):
    base = ctx.idle0
    def f(t, T):
        b = breath_curve(t, T, 3)
        look = (4 * lnoise(t, T, 61), 9 * lnoise(t, T, 62, (1, 2)), 3 * lnoise(t, T, 63))
        return kneel_pose(ctx, base, b, look)
    return ctx.build_loop(6.0, f), {}


@clip('Captured_Enter', False, 'authored: raise hands behind head and drop to both knees')
def captured_enter(ctx):
    P = ctx.P
    final = ctx.built['Captured_Loop'][0]
    Wf = P.fk(final)
    start = base_idle(ctx, 0.0)
    Ws = P.fk(start)
    face_forward(P, start, Ws)
    ank0 = P.ankles(Ws)
    ankF = P.ankles(Wf)
    T = 1.4
    def f(t, T_):
        # body descent
        x_body = smoother((t - 0.2) / 1.15)
        q = slerp_pose(P.rig, start, final, x_body)
        W = P.fk(q)
        pel_s = Ws['pelvis'].translation; pel_f = Wf['pelvis'].translation
        z = keys(t, [(0, pel_s.z), (0.25, pel_s.z - 0.02), (0.85, 0.66), (1.35, pel_f.z)], smooth)
        y = keys(t, [(0, pel_s.y), (0.85, pel_s.y + 0.04), (1.35, pel_f.y)], smooth)
        P.move_pelvis(q, W, pos=Vector((pel_s.x * (1 - x_body), y, z)))
        # right foot steps back first, then the left knee follows
        for s, t0, t1, lift in (('r', 0.2, 0.8, 0.12), ('l', 0.8, 1.35, 0.08)):
            u = smooth((t - t0) / (t1 - t0))
            a0, r0 = ank0[s]; a1, r1 = ankF[s]
            pos = a0.lerp(a1, u) + Vector((0, 0, lift * math.sin(math.pi * u)))
            if t < t0:
                pos = a0
            rot = r0.to_quaternion().slerp(r1.to_quaternion(), u).to_matrix()
            Hs = W[f'thigh_{s}'].translation
            pole = V(0, 1, -0.6 * u)
            P.leg(q, W, s, pos, pole, rot)
            W = P.fk(q)
        # hands: rise in front of the face, then behind the head
        w = smoother(t / 0.6)
        if w < 1:
            W = P.fk(q)
            ik = copy_pose(q); Wk = P.fk(ik)
            hands_behind_head(P, ik, Wk)
            mid = copy_pose(q); Wm = P.fk(mid)
            for s, sg in (('l', -1), ('r', 1)):
                wrist = P.point(Wm, 'Head', V(sg * 0.2, 0.18, 0.05))
                arm_to(P, mid, Wm, s, wrist, V(sg * 1, -0.2, -0.8), V(0, 0.2, 1), V(0, 1, 0))
                Wm = P.fk(mid)
            arm_src = slerp_pose(P.rig, start, mid, smooth(w / 0.5)) if w < 0.5 else slerp_pose(P.rig, mid, ik, smooth((w - 0.5) / 0.5))
            blend_bones(P.rig, q, arm_src, ARM_BONES + HAND_BONES['l'] + HAND_BONES['r'], 1.0)
            P.rig.refresh(q, W, 'pelvis')
        else:
            W = P.fk(q)
            hands_behind_head(P, q, W)
        return q
    return clamp_feet(P, ctx.build(T, f)), {}


@clip('Captured_Exit', False, 'Captured_Enter reversed')
def captured_exit(ctx):
    return list(reversed([copy_pose(p) for p in ctx.built['Captured_Enter']])), {}


@clip('Captured_Collapse', False, 'authored: kneel -> fall back into UAL1 Death01 (from its falling phase)')
def captured_collapse(ctx):
    P = ctx.P
    kneel = ctx.built['Captured_Loop'][0]
    death = ctx.S['Death01']
    f0, f1 = int(death.f0), int(death.f1)
    fc = f0
    for fr in range(f0, f1 + 1):
        if P.fk(death.pose(float(fr)))['pelvis'].translation.z < 0.50:
            fc = fr
            break
    blend = ctx.frames(0.4)
    out = []
    n = (f1 - fc) + blend
    for i in range(n + 1):
        fr = min(f1, fc + max(0, i - blend // 2))
        d = death.pose(float(fr))
        x = smoother(i / blend) if i < blend else 1.0
        out.append(slerp_pose(P.rig, kneel, d, x))
    return clamp_feet(P, out), {'death01_from_frame': fc}


# ================================================================= HELD POSES
@clip('Console_Loop', True, 'authored: standing at a terminal, typing and using a mouse')
def console_loop(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=0.0)
        sway(P, q, W, t, T, 0.006, 71)
        P.rotate(q, W, 'pelvis', R(pitch=4))
        P.spine(q, W, pitch=9)
        breathe(P, q, W, breath_curve(t, T, 2), 0.7)
        look = keys(t, [(0, 0), (2.4, 0), (2.7, 1), (3.6, 1), (3.9, 0), (T, 0)], smooth)
        P.head(q, W, pitch=14 + 2 * lnoise(t, T, 72), yaw=-4 + 9 * look)
        finish_stance(P, q, W, 0.28, 8)
        W = P.fk(q)
        pel = P.point(W, 'pelvis')
        kb = Vector((0, pel.y, 0)) + V(0, 0.40, 1.02)
        mouse = keys(t, [(0, 0), (2.3, 0), (2.6, 1), (3.7, 1), (4.0, 0), (T, 0)], smooth)
        for s, sg, seed in (('l', -1, 73), ('r', 1, 74)):
            tap = 0.007 * max(0.0, math.sin(2 * math.pi * (t * 4.6 + (0.37 if s == 'r' else 0.0))))
            dx = 0.015 * lnoise(t, T, seed, (2, 5, 7))
            dy = 0.010 * lnoise(t, T, seed + 3, (3, 4))
            off = V(sg * 0.15 + dx + (0.14 * mouse if s == 'r' else 0), dy - (0.03 * mouse if s == 'r' else 0), -tap)
            arm_to(P, q, W, s, kb + off, V(sg * 1, -0.6, -0.9), V(-sg * 0.25, 1, -0.25), V(0, 0.1, -1))
            W = P.fk(q)
            P.curl_all(q, W, s, -14 + (9 * math.sin(2 * math.pi * t * 5.3 + sg) if not (s == 'r' and mouse > 0.5) else 6), thumb=0.2)
        return q
    return ctx.build_loop(6.0, f), {}


def cower_pose(ctx, t, T):
    P = ctx.P
    q, W = upright(ctx, t, dz=0.0)
    trem = lambda k, seed: 1.2 * math.sin(2 * math.pi * (k * t / T) + seed)
    P.move_pelvis(q, W, delta=V(0.008 * trem(24, 1), 0.10, -0.30))
    P.rotate(q, W, 'pelvis', R(pitch=22))
    peek = keys(t, [(0, 0), (1.1, 0), (1.4, 1), (2.0, 1), (2.3, 0), (T, 0)], smooth)
    P.spine(q, W, pitch=24 - 8 * peek + trem(24, 2), roll=trem(23, 3) * 0.6)
    P.rotate(q, W, 'clavicle_l', R(roll=-12)); P.rotate(q, W, 'clavicle_r', R(roll=12))
    P.head(q, W, pitch=28 - 30 * peek, yaw=22 * peek)
    stance(P, q, W, width=0.40, fwd=0.10, toe_out=16)
    W = P.fk(q)
    for s, sg, dz in (('l', -1, 0.12), ('r', 1, 0.06)):
        wrist = P.point(W, 'Head', V(-sg * 0.06, 0.20, dz + 0.04))
        arm_to(P, q, W, s, wrist, V(sg * 1, 0.1, -1), V(-sg * 1, 0.2, 0.55), V(0, 1, 0.3))
        W = P.fk(q)
        P.curl_all(q, W, s, 20)
    return q


@clip('Panic_Loop', True, 'authored: cowering crouch, arms shielding the head, trembling, peeking')
def panic_loop(ctx):
    return ctx.build_loop(3.0, lambda t, T: cower_pose(ctx, t, T)), {}


@clip('Panic_Flee_Loop', True, 'authored upper body over Run: arms up by the head, glancing back (used on an upper-body mask)')
def panic_flee(ctx):
    P = ctx.P
    run = ctx.built['Run']
    n = len(run) - 1
    def f(t, T):
        i = int(round(t * FPS)) % n
        q = copy_pose(run[i]); W = P.fk(q)
        back = keys(t, [(0, 0), (0.5, 0), (0.75, 1), (1.2, 1), (1.45, 0), (T, 0)], smooth)
        P.spine(q, W, pitch=10, yaw=-14 * back)
        P.head(q, W, yaw=-48 * back, pitch=-4)
        swing = math.sin(2 * math.pi * t / (n / FPS))
        for s, sg in (('l', -1), ('r', 1)):
            wrist = P.point(W, 'Head', V(sg * 0.17, 0.10 + 0.06 * swing * sg, -0.02 + 0.04 * swing * sg))
            arm_to(P, q, W, s, wrist, V(sg * 1, -0.3, -1), V(-sg * 0.2, 0.3, 1), V(-sg * 0.6, 1, 0))
            W = P.fk(q)
            P.curl_all(q, W, s, 25)
        return q
    return ctx.build_loop(2 * n / FPS, f), {}


@clip('Alert_Loop', True, 'authored: guard investigating, scanning left/right, hand at the holster')
def alert_loop(ctx):
    P = ctx.P
    def f(t, T):
        q, W = upright(ctx, t, dz=-0.03)
        P.rotate(q, W, 'pelvis', R(pitch=5))
        yaw = keys(t, [(0, 0), (0.6, 0), (1.3, 42), (2.4, 42), (3.4, -40), (4.5, -40), (5.3, 0), (T, 0)], smoother)
        tilt = keys(t, [(0, 0), (1.3, 0), (1.7, 6), (2.4, 0), (3.4, 0), (3.8, -4), (4.5, 0), (T, 0)], smooth)
        P.spine(q, W, pitch=6, yaw=yaw * 0.35)
        breathe(P, q, W, breath_curve(t, T, 3), 0.9)
        P.head(q, W, yaw=yaw * 0.65, pitch=-3 + tilt, roll=0.3 * tilt)
        stance(P, q, W, width=0.40, fwd=0.04, toe_out=14, left_fwd=0.08, right_fwd=-0.06)
        W = P.fk(q)
        pel = P.point(W, 'pelvis')
        arm_to(P, q, W, 'r', pel + V(0.22, 0.02, 0.03), V(1, -0.6, 0.1), V(0, 0.25, -1), V(-1, 0, 0))
        W = P.fk(q)
        P.curl_all(q, W, 'r', 30)
        arm_to(P, q, W, 'l', pel + V(-0.20, 0.26, 0.17), V(-1, -0.5, -0.6), V(0.3, 0.8, -0.4), V(0.4, 0, -1))
        W = P.fk(q)
        P.curl_all(q, W, 'l', 10)
        return q
    return ctx.build_loop(6.0, f), {}


@clip('Point_Loop', True, 'authored: point forward with the right arm')
def point_loop(ctx):
    P = ctx.P
    def f(t, T):
        q = base_idle(ctx, t); W = P.fk(q)
        ank = P.ankles(W)
        face_forward(P, q, W, chest=False)
        P.spine(q, W, yaw=12, pitch=-2)
        breathe(P, q, W, breath_curve(t, T, 1), 0.8)
        P.head(q, W, yaw=6, pitch=-3)
        P.plant(q, W, ank)
        sh = W['upperarm_r'].translation
        pump = 0.025 * max(0.0, math.sin(2 * math.pi * t / T * 2)) ** 3
        tgt = sh + V(0.19, 0.46 + pump, 0.08 + pump * 0.5)
        arm_to(P, q, W, 'r', tgt, V(1, -0.2, -1), V(0.35, 1, 0.15), V(0.25, 0, -1))
        P.set_fingers(q, W, 'r', 'relaxed')
        for fn in ('middle', 'ring', 'pinky'):
            P.curl(q, W, 'r', fn, 60)
        P.curl(q, W, 'r', 'index', -12)
        return q
    return ctx.build_loop(3.0, f), {}


# ================================================================= GESTURES (arms + head layer)
def gesture(ctx, T, wrist_fn, extra=None):
    """Blend from the relaxed idle arms into IK targets and back. wrist_fn(t) -> {side: (pos, pole, fingers, palm)}."""
    P = ctx.P
    def f(t, T_):
        q = base_idle(ctx, t); W = P.fk(q)
        w = smoother(t / 0.45) * (1 - smoother((t - (T - 0.6)) / 0.6))
        if extra:
            extra(q, W, t, w)
        W = P.fk(q)
        ik = copy_pose(q); Wk = P.fk(ik)
        for s, (pos, pole, fing, palm) in wrist_fn(t, Wk).items():
            arm_to(P, ik, Wk, s, pos, pole, fing, palm)
            Wk = P.fk(ik)
            P.curl_all(ik, Wk, s, -30, thumb=0.5)
        blend_bones(P.rig, q, ik, ARM_BONES + HAND_BONES['l'] + HAND_BONES['r'], w)
        return q
    return ctx.build(T, f)


@clip('Gesture_Explain', False, 'authored: two-handed explaining gesture (palms up, open outward, beat)')
def gesture_explain(ctx):
    P = ctx.P
    def wrists(t, W):
        pel = P.point(W, 'pelvis')
        spread = keys(t, [(0, 0.16), (0.5, 0.16), (0.95, 0.30), (1.3, 0.27), (1.6, 0.29)], smooth)
        lift = keys(t, [(0, 0.12), (0.5, 0.18), (1.1, 0.20), (1.25, 0.16), (1.45, 0.20)], smooth)
        out = {}
        for s, sg in (('l', -1), ('r', 1)):
            out[s] = (pel + V(sg * spread, 0.30, lift), V(sg * 1, -0.3, -1), V(-sg * 0.25, 1, 0.1), V(sg * 0.2, 0, 1))
        return out
    def extra(q, W, t, w):
        P.head(q, W, pitch=-4 * w * math.sin(math.pi * min(1, t / 1.6)), yaw=4 * w)
    return gesture(ctx, 2.2, wrists, extra), {}


@clip('Gesture_Shrug', False, 'authored: shrug (shoulders up, palms up, head tilt)')
def gesture_shrug(ctx):
    P = ctx.P
    def wrists(t, W):
        pel = P.point(W, 'pelvis')
        out = {}
        for s, sg in (('l', -1), ('r', 1)):
            out[s] = (pel + V(sg * 0.34, 0.16, 0.12), V(sg * 0.6, -1, -0.6), V(-sg * 0.1, 1, 0.15), V(sg * 0.35, 0, 1))
        return out
    def extra(q, W, t, w):
        up = keys(t, [(0, 0), (0.35, 0), (0.6, 1), (1.0, 1), (1.3, 0)], smooth)
        P.rotate(q, W, 'clavicle_l', R(roll=-14 * up)); P.rotate(q, W, 'clavicle_r', R(roll=14 * up))
        P.head(q, W, roll=9 * w, pitch=-3 * w)
    return gesture(ctx, 1.8, wrists, extra), {}
