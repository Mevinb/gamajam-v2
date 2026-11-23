using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Characters
{
    /// <summary>
    /// Bone-driven fallback animation for the skinned humans, used when the humanoid AnimatorController is missing:
    /// idle breathing, walk/run swing, airborne tuck, lying (Unconscious), kneeling (Captured), sitting and pointing.
    /// Rotations are expressed in model axes (X right, Y up, Z forward) relative to the T-pose rest, so they are
    /// independent of how each bone's local axes were authored.
    /// </summary>
    public sealed class ProceduralHumanPose
    {
        readonly Transform model;
        readonly Dictionary<HumanBodyBones, Transform> bones = new();
        readonly Dictionary<Transform, Quaternion> restLocal = new();
        readonly Dictionary<Transform, Quaternion> restModel = new();
        readonly Vector3 hipsRestModelPos;
        float phase, lieT, kneelT, sitT, pointT, airT, speedSmoothed;

        public bool Valid { get; }

        public ProceduralHumanPose(Transform model, Animator animator, float phaseSeed)
        {
            this.model = model;
            phase = phaseSeed;
            if (!animator || !animator.isHuman) return;
            foreach (var b in new[]
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
                HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightFoot,
            })
            {
                var t = animator.GetBoneTransform(b);
                if (!t) continue;
                bones[b] = t;
                restLocal[t] = t.localRotation;
                restModel[t] = Quaternion.Inverse(model.rotation) * t.rotation;
            }
            if (bones.TryGetValue(HumanBodyBones.Hips, out var hips)) hipsRestModelPos = model.InverseTransformPoint(hips.position);
            Valid = bones.ContainsKey(HumanBodyBones.Hips) && bones.ContainsKey(HumanBodyBones.LeftUpperLeg) && bones.ContainsKey(HumanBodyBones.LeftUpperArm);
        }

        public Transform Bone(HumanBodyBones b) => bones.TryGetValue(b, out var t) ? t : null;

        /// <summary>Rotate a bone by D (model axes, rest frame) relative to its T-pose rest.</summary>
        void Pose(HumanBodyBones b, Quaternion d)
        {
            if (!bones.TryGetValue(b, out var t)) return;
            var r = restModel[t];
            t.localRotation = restLocal[t] * (Quaternion.Inverse(r) * d * r);
        }

        static Quaternion X(float deg) => Quaternion.AngleAxis(deg, Vector3.right);
        static Quaternion Y(float deg) => Quaternion.AngleAxis(deg, Vector3.up);
        static Quaternion Z(float deg) => Quaternion.AngleAxis(deg, Vector3.forward);

        /// <summary>Write a full procedural pose. Returns the body lie factor (0..1) so the caller can tilt the model root.</summary>
        public float Evaluate(Vector3 velocity, bool grounded, HostState state, ActorPose pose, float runSpeed, float dt, float time)
        {
            if (!Valid) return 0f;
            float target = new Vector3(velocity.x, 0, velocity.z).magnitude;
            speedSmoothed = Mathf.Lerp(speedSmoothed, target, 1f - Mathf.Exp(-10f * dt));
            float s = speedSmoothed;
            float run01 = Mathf.InverseLerp(2.2f, Mathf.Max(2.3f, runSpeed), s);
            bool lying = state == HostState.Unconscious;
            bool kneeling = state == HostState.Captured;
            lieT = Mathf.MoveTowards(lieT, lying ? 1f : 0f, dt * (lying ? 2.2f : 1.3f));
            kneelT = Mathf.MoveTowards(kneelT, kneeling ? 1f : 0f, dt * 3f);
            airT = Mathf.MoveTowards(airT, grounded ? 0f : 1f, dt * 6f);
            sitT = Mathf.MoveTowards(sitT, pose == ActorPose.Sit ? 1f : 0f, dt * 3f);
            pointT = Mathf.MoveTowards(pointT, pose == ActorPose.Point ? 1f : 0f, dt * 4f);

            float stride = Mathf.Lerp(1.25f, 2.1f, run01);
            phase += (s / stride) * Mathf.PI * 2f * dt;
            float amp = Mathf.Clamp01(s / 1.6f);
            float legSwing = Mathf.Lerp(26f, 44f, run01) * amp;
            float armSwing = Mathf.Lerp(18f, 48f, run01) * amp;
            float sinP = Mathf.Sin(phase), cosP = Mathf.Cos(phase);
            float lLeg = sinP * legSwing, rLeg = -sinP * legSwing;
            float lKnee = Mathf.Max(0, -cosP) * Mathf.Lerp(35f, 85f, run01) * amp + 3f;
            float rKnee = Mathf.Max(0, cosP) * Mathf.Lerp(35f, 85f, run01) * amp + 3f;
            float breathe = Mathf.Sin(time * 1.7f + phase * 0.1f) * 1.2f;
            float lean = Mathf.Lerp(2f, 10f, run01) * amp;
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.035f * amp * (1 + run01);
            lLeg = Mathf.Lerp(lLeg, -35f, airT); rLeg = Mathf.Lerp(rLeg, 10f, airT);
            lKnee = Mathf.Lerp(lKnee, 70f, airT); rKnee = Mathf.Lerp(rKnee, 30f, airT);
            float lArm = -sinP * armSwing, rArm = sinP * armSwing;
            float armDown = 76f - airT * 25f;
            float elbow = Mathf.Lerp(10f, 80f, run01) * Mathf.Max(amp, 0.2f) + 8f;
            float lElbow = elbow, rElbow = elbow;

            if (kneelT > 0f)
            {
                lLeg = Mathf.Lerp(lLeg, -85f, kneelT); rLeg = Mathf.Lerp(rLeg, 5f, kneelT);
                lKnee = Mathf.Lerp(lKnee, 88f, kneelT); rKnee = Mathf.Lerp(rKnee, 100f, kneelT);
                lArm = Mathf.Lerp(lArm, 30f, kneelT); rArm = Mathf.Lerp(rArm, 30f, kneelT);
                lElbow = Mathf.Lerp(lElbow, 40f, kneelT); rElbow = Mathf.Lerp(rElbow, 40f, kneelT);
                lean = Mathf.Lerp(lean, 14f, kneelT);
            }
            if (sitT > 0f)
            {
                lLeg = Mathf.Lerp(lLeg, -88f, sitT); rLeg = Mathf.Lerp(rLeg, -84f, sitT);
                lKnee = Mathf.Lerp(lKnee, 86f, sitT); rKnee = Mathf.Lerp(rKnee, 80f, sitT);
                lArm = Mathf.Lerp(lArm, -22f, sitT); rArm = Mathf.Lerp(rArm, -20f, sitT);
                lElbow = Mathf.Lerp(lElbow, 45f, sitT); rElbow = Mathf.Lerp(rElbow, 42f, sitT);
                lean = Mathf.Lerp(lean, 6f, sitT);
            }

            Pose(HumanBodyBones.LeftUpperLeg, X(lLeg));
            Pose(HumanBodyBones.RightUpperLeg, X(rLeg));
            Pose(HumanBodyBones.LeftLowerLeg, X(lKnee));
            Pose(HumanBodyBones.RightLowerLeg, X(rKnee));
            Pose(HumanBodyBones.LeftFoot, X(-Mathf.Min(lKnee, 60f) * 0.3f));
            Pose(HumanBodyBones.RightFoot, X(-Mathf.Min(rKnee, 60f) * 0.3f));
            Pose(HumanBodyBones.LeftUpperArm, X(lArm) * Z(armDown));
            float rDown = Mathf.Lerp(armDown, 66f, pointT);
            float rFwd = Mathf.Lerp(rArm, -70f + Mathf.Sin(time * 16f) * 3f, pointT);
            Pose(HumanBodyBones.RightUpperArm, X(rFwd) * Z(-rDown));
            Pose(HumanBodyBones.LeftLowerArm, Y(lElbow));
            Pose(HumanBodyBones.RightLowerArm, Y(-Mathf.Lerp(rElbow, 8f, pointT)));
            Pose(HumanBodyBones.Spine, X((lean + breathe) * 0.5f) * Y(Mathf.Sin(phase) * 4f * amp));
            Pose(HumanBodyBones.Chest, X((lean + breathe) * 0.5f));

            if (bones.TryGetValue(HumanBodyBones.Hips, out var hips))
            {
                float drop = kneelT * 0.42f + sitT * 0.40f;
                hips.position = model.TransformPoint(hipsRestModelPos + Vector3.up * (bob - drop) + Vector3.back * (sitT * 0.04f));
            }
            return lieT * lieT * (3f - 2f * lieT);
        }
    }
}
