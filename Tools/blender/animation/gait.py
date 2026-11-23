"""Gait-cycle processing: cycle detection, resampling to an exact loop, in-place conversion, loop closure,
ground alignment and stance-foot locking (2-bone IK)."""
import math
from mathutils import Matrix, Quaternion, Vector
from posing import Poser, copy_pose, slerp_pose, V, FWD

FPS = 30.0


def tracks(rig, poses, names):
    out = {n: [] for n in names}
    for p in poses:
        W = rig.fk(p)
        for n in names:
            out[n].append(W[n].translation.copy())
    return out


def find_strikes(rig, poses, foot, fps, thr_lo=0.6, thr_hi=1.2):
    """Times (frame indices) where the foot's horizontal speed falls below thr_lo after exceeding thr_hi."""
    tr = tracks(rig, poses, [f'foot_{foot}'])[f'foot_{foot}']
    sp = [0.0] + [((tr[i] - tr[i - 1]).to_2d().length * fps) for i in range(1, len(tr))]
    strikes = []
    armed = False
    for i, s in enumerate(sp):
        if s > thr_hi:
            armed = True
        elif armed and s < thr_lo:
            strikes.append(i); armed = False
    return strikes, sp


def resample(rig, poses, src_fps, t0, t1, n):
    """Resample poses (list at src_fps) between t0..t1 seconds into n+1 frames (last == t1)."""
    out = []
    for i in range(n + 1):
        t = t0 + (t1 - t0) * i / n
        x = t * src_fps
        i0 = int(math.floor(x)); a = x - i0
        i0 = max(0, min(len(poses) - 1, i0)); i1 = min(len(poses) - 1, i0 + 1)
        out.append(slerp_pose(rig, poses[i0], poses[i1], a) if a > 1e-6 else copy_pose(poses[i0]))
    return out


def make_inplace_loop(rig, cyc):
    """cyc: n+1 poses with root motion, last pose == first pose of next cycle. Returns (n+1 poses in place, speed m/s,
    displacement Vector). Facing aligned so travel is along FWD."""
    n = len(cyc) - 1
    T = n / FPS
    W0 = rig.fk(cyc[0]); Wn = rig.fk(cyc[n])
    D = (Wn['pelvis'].translation - W0['pelvis'].translation); D.z = 0
    ang = math.atan2(D.x, -D.y)  # angle of D from FWD (-Y) toward +X
    Rz = Matrix.Rotation(ang, 3, 'Z')  # rotate D onto FWD? check sign below
    if (Rz @ D).normalized().dot(FWD) < 0.99:
        Rz = Matrix.Rotation(-ang, 3, 'Z')
    origin = W0['pelvis'].translation.copy(); origin.z = 0
    speed = D.length / T
    out = []
    for i, p in enumerate(cyc):
        q = copy_pose(p)
        W = rig.fk(q)
        pw = W['pelvis']
        head = Rz @ (pw.translation - origin)
        head -= FWD * (speed * i / FPS)  # remove travel
        rig.set_world(q, W, 'pelvis', rot3=Rz @ pw.to_3x3().normalized(), head=head)
        out.append(q)
    # center laterally / longitudinally around 0
    tr = tracks(rig, out, ['pelvis'])['pelvis']
    cx = sum(v.x for v in tr) / len(tr); cy = sum(v.y for v in tr) / len(tr)
    for q in out:
        W = rig.fk(q)
        rig.set_world(q, W, 'pelvis', head=W['pelvis'].translation - Vector((cx, cy, 0)))
    return out, speed, D


def close_loop(rig, poses):
    """Distribute the mismatch between last and first pose linearly over the cycle; last pose := first pose."""
    n = len(poses) - 1
    first, last = poses[0], poses[n]
    out = []
    for i, p in enumerate(poses):
        x = i / n
        q = {}
        for b in rig.order:
            qa, la = p[b]
            q0, l0 = first[b]; qn, ln = last[b]
            if q0.dot(qn) < 0:
                q0 = -q0
            c = q0 @ qn.inverted()  # c @ qn == q0
            cq = Quaternion().slerp(c, x)
            q[b] = ((cq @ qa).normalized(), la + (l0 - ln) * x)
        out.append(q)
    out[n] = copy_pose(out[0])
    return out


def ground(rig, poses, ankle_h, ball_h):
    """Shift pelvis vertically so the lowest foot contact matches the reference ankle/ball heights."""
    tr = tracks(rig, poses, ['foot_l', 'foot_r', 'ball_l', 'ball_r'])
    lows = []
    for i in range(len(poses)):
        lows.append(min(tr['foot_l'][i].z - ankle_h, tr['foot_r'][i].z - ankle_h,
                        tr['ball_l'][i].z - ball_h, tr['ball_r'][i].z - ball_h))
    lows.sort()
    dz = -lows[len(lows) // 4]  # robust: lower quartile of per-frame lowest contact
    for q in poses:
        W = rig.fk(q)
        rig.set_world(q, W, 'pelvis', head=W['pelvis'].translation + Vector((0, 0, dz)))
    return dz


def lock_feet(poser, poses, speed, ankle_h, ball_h, min_width=0.0, contact_tol=0.03, report=None):
    """Stance-foot locking for an in-place loop travelling along FWD at `speed`.
    Contact point (ankle if heel down else ball) must move backward at exactly `speed` while in contact.
    Returns corrected poses. Also enforces a minimum lateral ankle separation (min_width)."""
    rig = poser.rig
    n = len(poses) - 1
    tr = tracks(rig, poses, ['foot_l', 'foot_r', 'ball_l', 'ball_r', 'pelvis'])
    back = -FWD  # +Y
    corr = {'l': [Vector() for _ in range(n + 1)], 'r': [Vector() for _ in range(n + 1)]}
    for s in ('l', 'r'):
        A = tr[f'foot_{s}']; B = tr[f'ball_{s}']
        # contact classification per frame (cyclic)
        contact = []
        for i in range(n):
            heel_down = A[i].z - ankle_h < contact_tol
            ball_down = B[i].z - ball_h < contact_tol
            # world-space horizontal speed of the lower point
            j = (i + 1) % n
            pt_i = A[i] if heel_down else B[i]
            pt_j = A[j] if heel_down else B[j]
            w = (pt_j - pt_i) - back * (speed / FPS)
            wsp = w.to_2d().length * FPS
            contact.append(('A' if heel_down else 'B') if ((heel_down or ball_down) and wsp < 1.0) else None)
        # find contiguous runs cyclically
        if all(c is None for c in contact):
            continue
        start = next(i for i in range(n) if contact[i] is not None and contact[i - 1] is None)
        delta = [Vector() for _ in range(n)]
        i = start; k = 0
        cur = Vector(); anchor = None; anchor_kind = None; anchor_t = 0
        acc = Vector()
        while k < n:
            c = contact[i]
            if c is not None:
                pt = (A[i] if c == 'A' else B[i]) + acc
                if anchor is None or c != anchor_kind:
                    anchor = pt.copy(); anchor_kind = c; anchor_t = k
                tgt = anchor + back * (speed * (k - anchor_t) / FPS)
                d = tgt - pt
                d.z = 0
                acc = acc + d
                delta[i] = acc.copy()
            else:
                acc = acc * 0.6  # decay correction during swing
                delta[i] = acc.copy()
            i = (i + 1) % n; k += 1
        # smooth deltas cyclically
        sm = []
        for i in range(n):
            sm.append((delta[i - 1] + delta[i] * 2 + delta[(i + 1) % n]) / 4)
        for i in range(n):
            corr[s][i] = sm[i]
        corr[s][n] = sm[0]
        if report is not None:
            report[s] = ''.join(c or '.' for c in contact)
    # lateral minimum width
    if min_width > 0:
        for i in range(n + 1):
            al = tr['foot_l'][i] + corr['l'][i]; ar = tr['foot_r'][i] + corr['r'][i]
            px = tr['pelvis'][i].x
            # left = +X
            needl = (px + min_width / 2) - al.x
            needr = ar.x - (px - min_width / 2)
            if needl > 0:
                corr['l'][i].x += needl
            if needr > 0:
                corr['r'][i].x -= needr
    out = []
    for i, p in enumerate(poses):
        q = copy_pose(p)
        W = rig.fk(q)
        for s in ('l', 'r'):
            if corr[s][i].length > 1e-5:
                knee = W[f'calf_{s}'].translation; hip = W[f'thigh_{s}'].translation; ank = W[f'foot_{s}'].translation
                pole = knee - (hip + ank) * 0.5
                poser.leg(q, W, s, ank + corr[s][i], pole, W[f'foot_{s}'].to_3x3().normalized())
        out.append(q)
    return out


def slip_report(rig, poses, speed, ankle_h, ball_h, tol=0.03):
    """Average world-space slip (m/s) of contact points."""
    n = len(poses) - 1
    tr = tracks(rig, poses, ['foot_l', 'foot_r', 'ball_l', 'ball_r'])
    tot = 0.0; cnt = 0; worst = 0.0
    for s in ('l', 'r'):
        A = tr[f'foot_{s}']; B = tr[f'ball_{s}']
        for i in range(n):
            hd = A[i].z - ankle_h < tol; bd = B[i].z - ball_h < tol
            if not (hd or bd):
                continue
            P0 = A[i] if hd else B[i]; P1 = A[i + 1] if hd else B[i + 1]
            w = (P1 - P0) - (-FWD) * (speed / FPS)
            v = w.to_2d().length * FPS
            tot += v; cnt += 1; worst = max(worst, v)
    return tot / max(1, cnt), worst, cnt
