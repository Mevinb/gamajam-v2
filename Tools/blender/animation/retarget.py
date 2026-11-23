"""CMU ASF/AMC -> UAL rig retargeting (world-rotation delta with per-bone rest-direction alignment)."""
import math
from mathutils import Matrix, Quaternion, Vector
import cmu
from rig import Rig

# Y-up/+Z-forward/+X-left (CMU)  ->  Z-up/-Y-forward/+X-left (UAL armature space)
A = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))
AT = A.transposed()

MAP = {  # UAL bone -> CMU bone
    'pelvis': 'root', 'spine_01': 'lowerback', 'spine_02': 'upperback', 'spine_03': 'thorax',
    'neck_01': 'lowerneck', 'Head': 'head',
    'clavicle_l': 'lclavicle', 'upperarm_l': 'lhumerus', 'lowerarm_l': 'lradius', 'hand_l': 'lhand',
    'clavicle_r': 'rclavicle', 'upperarm_r': 'rhumerus', 'lowerarm_r': 'rradius', 'hand_r': 'rhand',
    'thigh_l': 'lfemur', 'calf_l': 'ltibia', 'foot_l': 'lfoot', 'ball_l': 'ltoes',
    'thigh_r': 'rfemur', 'calf_r': 'rtibia', 'foot_r': 'rfoot', 'ball_r': 'rtoes',
}


def retarget(rig: Rig, asf_path, amc_path, start=0, end=None, src_fps=120, fps=30, base_pose=None, hand_weight=0.5):
    """Returns list of pose dicts (30 fps) with root motion kept (pelvis translation in armature space)."""
    bones = cmu.load_asf(asf_path)
    frames = cmu.load_amc(amc_path)
    ordered = cmu.order_bones(bones)
    end = len(frames) - 1 if end is None else min(end, len(frames) - 1)
    # rest alignment per mapped bone
    align = {}
    for t, s in MAP.items():
        if s == 'root':
            align[t] = Matrix.Identity(3)
            continue
        d_s = (A @ bones[s].direction).normalized()
        d_t = rig.bone_dir_rest(t)
        align[t] = d_t.rotation_difference(d_s).to_matrix()
    ual_leg = rig.length('thigh_l') + rig.length('calf_l')
    cmu_leg = bones['lfemur'].length + bones['ltibia'].length
    scale = ual_leg / cmu_leg
    restW = rig.fk(rig.identity_pose())
    hip_c_rest = (restW['thigh_l'].translation + restW['thigh_r'].translation) * 0.5
    pel_off_rest = hip_c_rest - restW['pelvis'].translation
    base = base_pose or rig.identity_pose()
    out = []
    step = src_fps / fps
    t = float(start)
    while t <= end + 1e-6:
        i0 = int(math.floor(t)); a = t - i0
        P0 = cmu.pose_frame(bones, frames[i0], ordered)
        P1 = cmu.pose_frame(bones, frames[min(i0 + 1, end)], ordered) if a > 1e-6 else P0
        pose = {n: (q.copy(), l.copy()) for n, (q, l) in base.items()}
        W = rig.fk(pose)
        for n in rig.order:
            if n not in MAP:
                # keep basis from base pose; refresh this bone's world
                p = rig.parent[n]
                q, l = pose[n]
                B = Matrix.Translation(l) @ q.to_matrix().to_4x4()
                W[n] = ((W[p] @ rig.rest_rel[n]) if p else rig.rest_rel[n]) @ B
                continue
            s = MAP[n]
            G0 = P0[s][0].to_quaternion(); G1 = P1[s][0].to_quaternion()
            G = G0.slerp(G1, a).to_matrix() if a > 1e-6 else P0[s][0]
            Rw = A @ G @ AT @ align[n] @ rig.rest[n].to_3x3()
            if n.startswith('hand_') and hand_weight < 1:
                # damp noisy CMU hand data toward following the forearm with base-pose wrist
                q_full = rig.basis_rot(n, Rw, W)
                q_base = base[n][0]
                q = q_base.slerp(q_full, hand_weight)
                pose[n] = (q, pose[n][1])
                p = rig.parent[n]
                B = q.to_matrix().to_4x4()
                W[n] = (W[p] @ rig.rest_rel[n]) @ Matrix.Translation(pose[n][1]) @ B
                continue
            if n == 'pelvis':
                hc0 = (P0['lfemur'][1] + P0['rfemur'][1]) * 0.5
                hc1 = (P1['lfemur'][1] + P1['rfemur'][1]) * 0.5
                hc = hc0.lerp(hc1, a)
                hip_c = (A @ hc) * scale
                head = hip_c - Rw @ (rig.rest['pelvis'].to_3x3().inverted() @ pel_off_rest)
                rig.set_world(pose, W, n, rot3=Rw, head=head)
            else:
                rig.set_world(pose, W, n, rot3=Rw)
        out.append(pose)
        t += step
    return out, scale
