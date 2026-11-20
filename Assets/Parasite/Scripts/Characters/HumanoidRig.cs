using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Characters
{
    /// <summary>
    /// Procedural human body built from primitives with profession outfits, plus procedural animation
    /// (walk/run cycle, idle breathing, airborne, unconscious, captured). Placeholder for imported rigged humans.
    /// </summary>
    public class HumanoidRig : MonoBehaviour
    {
        public Transform Model { get; private set; }
        public Transform Head { get; private set; }
        public Renderer[] Renderers { get; private set; }
        public Material[] UniqueMaterials { get; private set; }

        Transform hips, spine, neck, lThigh, rThigh, lShin, rShin, lUpperArm, rUpperArm, lForearm, rForearm;
        readonly List<Material> unique = new();
        Material eyeMat;
        float phase, lieT, kneelT, speedSmoothed, airT, twitchT;
        float height = 1f;
        Vector3 lookDir;
        bool hasLook;

        static readonly Color[] SkinTones =
        {
            new(0.96f, 0.80f, 0.69f), new(0.89f, 0.69f, 0.55f), new(0.78f, 0.57f, 0.42f),
            new(0.62f, 0.43f, 0.30f), new(0.45f, 0.31f, 0.22f), new(0.33f, 0.22f, 0.16f),
        };
        static readonly Color[] HairColors =
        {
            new(0.08f, 0.06f, 0.05f), new(0.22f, 0.14f, 0.08f), new(0.45f, 0.30f, 0.16f),
            new(0.75f, 0.60f, 0.38f), new(0.55f, 0.55f, 0.56f), new(0.12f, 0.10f, 0.10f),
        };
        static readonly Color[] ShirtColors =
        {
            new(0.55f, 0.12f, 0.12f), new(0.15f, 0.35f, 0.55f), new(0.82f, 0.78f, 0.70f), new(0.20f, 0.42f, 0.28f),
            new(0.35f, 0.30f, 0.45f), new(0.85f, 0.55f, 0.20f), new(0.12f, 0.12f, 0.14f), new(0.62f, 0.62f, 0.66f),
        };
        static readonly Color[] PantsColors =
        {
            new(0.13f, 0.18f, 0.30f), new(0.10f, 0.10f, 0.11f), new(0.35f, 0.30f, 0.24f), new(0.25f, 0.27f, 0.30f),
        };

        Material M(Color c, float smooth = 0.3f, float metal = 0f)
        {
            var m = MatFactory.LitUnique(c, smooth, metal);
            unique.Add(m);
            return m;
        }

        public void Build(Profession profession, int seed, bool isDrVale)
        {
            var rnd = new System.Random(seed * 7919 + (int)profession * 31);
            float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            T Pick<T>(T[] arr) => arr[rnd.Next(arr.Length)];

            height = Range(0.94f, 1.06f) * (profession == Profession.Guard ? 1.03f : 1f);
            float girth = Range(0.92f, 1.12f) * (profession == Profession.Guard ? 1.08f : 1f);

            Model = new GameObject("Model").transform;
            Model.SetParent(transform, false);
            Model.localScale = new Vector3(girth, height, girth);

            var skin = M(Pick(SkinTones), 0.45f);
            var hair = M(isDrVale ? new Color(0.78f, 0.78f, 0.80f) : Pick(HairColors), 0.25f);
            Color shirtC = Pick(ShirtColors), pantsC = Pick(PantsColors);
            var shoes = M(new Color(0.06f, 0.05f, 0.05f), 0.55f);
            Material shirt, pants;
            switch (profession)
            {
                case Profession.Guard:
                    shirt = M(new Color(0.10f, 0.14f, 0.26f), 0.25f); pants = M(new Color(0.07f, 0.08f, 0.11f), 0.3f); break;
                case Profession.Scientist:
                    shirt = M(Pick(new[] { new Color(0.55f, 0.65f, 0.78f), new Color(0.35f, 0.38f, 0.42f), new Color(0.6f, 0.2f, 0.25f) }), 0.3f);
                    pants = M(new Color(0.17f, 0.18f, 0.21f), 0.3f); break;
                case Profession.Maintenance:
                    shirt = M(new Color(0.16f, 0.24f, 0.40f), 0.2f); pants = M(new Color(0.16f, 0.24f, 0.40f), 0.2f); break;
                default:
                    shirt = M(shirtC, 0.3f); pants = M(pantsC, 0.25f); break;
            }

            // --- skeleton (pivots) ---
            hips = Pivot("Hips", Model, new Vector3(0, 0.95f, 0));
            spine = Pivot("Spine", hips, new Vector3(0, 0.05f, 0));
            neck = Pivot("Neck", spine, new Vector3(0, 0.50f, 0));
            Head = Pivot("Head", neck, new Vector3(0, 0.09f, 0));
            lThigh = Pivot("LThigh", hips, new Vector3(-0.10f, -0.02f, 0));
            rThigh = Pivot("RThigh", hips, new Vector3(0.10f, -0.02f, 0));
            lShin = Pivot("LShin", lThigh, new Vector3(0, -0.44f, 0));
            rShin = Pivot("RShin", rThigh, new Vector3(0, -0.44f, 0));
            lUpperArm = Pivot("LUpperArm", spine, new Vector3(-0.22f, 0.43f, 0));
            rUpperArm = Pivot("RUpperArm", spine, new Vector3(0.22f, 0.43f, 0));
            lForearm = Pivot("LForearm", lUpperArm, new Vector3(0, -0.29f, 0));
            rForearm = Pivot("RForearm", rUpperArm, new Vector3(0, -0.29f, 0));

            // --- body meshes ---
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, -0.02f, 0), new Vector3(0.33f, 0.16f, 0.2f), pants);
            Prim.Make(PrimitiveType.Capsule, spine, new Vector3(0, 0.26f, 0), new Vector3(0.38f, 0.27f, 0.23f), shirt);
            Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.36f, 0), new Vector3(0.40f, 0.2f, 0.22f), shirt);
            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(0, 0.02f, 0), new Vector3(0.09f, 0.05f, 0.09f), skin);
            var headGo = Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.11f, 0.0f), new Vector3(0.20f, 0.24f, 0.22f), skin);
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.06f, 0.10f), new Vector3(0.06f, 0.07f, 0.05f), skin); // nose/jaw
            eyeMat = M(new Color(0.08f, 0.08f, 0.1f), 0.9f);
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(-0.045f, 0.13f, 0.098f), Vector3.one * 0.03f, eyeMat);
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0.045f, 0.13f, 0.098f), Vector3.one * 0.03f, eyeMat);
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(-0.105f, 0.11f, 0), new Vector3(0.03f, 0.06f, 0.04f), skin); // ears
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0.105f, 0.11f, 0), new Vector3(0.03f, 0.06f, 0.04f), skin);

            // hair (style by seed)
            int hairStyle = rnd.Next(4);
            if (isDrVale) hairStyle = 1;
            if (hairStyle == 0) Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.17f, -0.01f), new Vector3(0.215f, 0.16f, 0.23f), hair);
            else if (hairStyle == 1) { Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.18f, -0.02f), new Vector3(0.22f, 0.13f, 0.22f), hair); Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.12f, -0.07f), new Vector3(0.21f, 0.15f, 0.12f), hair); }
            else if (hairStyle == 2) { Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.15f, -0.03f), new Vector3(0.23f, 0.22f, 0.22f), hair); Prim.Make(PrimitiveType.Capsule, Head, new Vector3(0, 0.02f, -0.1f), new Vector3(0.18f, 0.12f, 0.08f), hair); }
            else Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.2f, -0.01f), new Vector3(0.2f, 0.09f, 0.21f), hair);

            // limbs
            foreach (var (thigh, shin) in new[] { (lThigh, lShin), (rThigh, rShin) })
            {
                Prim.Make(PrimitiveType.Capsule, thigh, new Vector3(0, -0.22f, 0), new Vector3(0.15f, 0.23f, 0.15f), pants);
                Prim.Make(PrimitiveType.Capsule, shin, new Vector3(0, -0.22f, 0), new Vector3(0.12f, 0.23f, 0.12f), pants);
                Prim.Make(PrimitiveType.Cube, shin, new Vector3(0, -0.46f, 0.05f), new Vector3(0.11f, 0.07f, 0.26f), shoes);
            }
            bool shortSleeves = profession == Profession.Civilian && rnd.NextDouble() < 0.5;
            foreach (var (upper, fore) in new[] { (lUpperArm, lForearm), (rUpperArm, rForearm) })
            {
                Prim.Make(PrimitiveType.Capsule, upper, new Vector3(0, -0.14f, 0), new Vector3(0.11f, 0.16f, 0.11f), shirt);
                Prim.Make(PrimitiveType.Capsule, fore, new Vector3(0, -0.13f, 0), new Vector3(0.095f, 0.15f, 0.095f), shortSleeves ? skin : shirt);
                Prim.Make(PrimitiveType.Sphere, fore, new Vector3(0, -0.29f, 0), new Vector3(0.085f, 0.1f, 0.06f), skin);
            }

            // --- outfits ---
            switch (profession)
            {
                case Profession.Guard:
                {
                    var cap = M(new Color(0.05f, 0.06f, 0.09f), 0.35f);
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.22f, 0), new Vector3(0.23f, 0.05f, 0.24f), cap);
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.19f, 0.13f), new Vector3(0.2f, 0.015f, 0.1f), cap);
                    var gold = M(new Color(0.85f, 0.65f, 0.2f), 0.8f, 1f);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.1f, 0.38f, 0.115f), new Vector3(0.06f, 0.07f, 0.01f), gold);
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.05f, 0), new Vector3(0.35f, 0.05f, 0.215f), M(new Color(0.03f, 0.03f, 0.03f), 0.6f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.32f, 0.118f), new Vector3(0.02f, 0.3f, 0.005f), M(new Color(0.05f, 0.05f, 0.07f)));
                    break;
                }
                case Profession.Scientist:
                {
                    var coat = M(new Color(0.93f, 0.94f, 0.95f), 0.25f);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.24f, 0), new Vector3(0.42f, 0.52f, 0.25f), coat);
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, -0.18f, 0), new Vector3(0.40f, 0.36f, 0.24f), coat);
                    foreach (var upper in new[] { lUpperArm, rUpperArm })
                        Prim.Make(PrimitiveType.Capsule, upper, new Vector3(0, -0.14f, 0), new Vector3(0.125f, 0.165f, 0.125f), coat);
                    foreach (var fore in new[] { lForearm, rForearm })
                        Prim.Make(PrimitiveType.Capsule, fore, new Vector3(0, -0.12f, 0), new Vector3(0.11f, 0.14f, 0.11f), coat);
                    var frame = M(new Color(0.05f, 0.05f, 0.05f), 0.7f, 0.5f);
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.135f, 0.112f), new Vector3(0.17f, 0.012f, 0.01f), frame);
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(-0.045f, 0.13f, 0.113f), new Vector3(0.05f, 0.005f, 0.05f), frame, false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0.045f, 0.13f, 0.113f), new Vector3(0.05f, 0.005f, 0.05f), frame, false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.1f, 0.33f, 0.13f), new Vector3(0.06f, 0.08f, 0.005f), M(new Color(0.2f, 0.5f, 0.9f), 0.5f));
                    if (isDrVale)
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.3f, 0.12f), new Vector3(0.045f, 0.28f, 0.01f), M(new Color(0.45f, 0.05f, 0.1f), 0.5f));
                    break;
                }
                case Profession.Maintenance:
                {
                    var vest = M(new Color(1f, 0.45f, 0.05f), 0.3f);
                    var stripe = M(new Color(0.85f, 0.85f, 0.75f), 0.9f, 0.2f);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.27f, 0), new Vector3(0.41f, 0.42f, 0.245f), vest);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.18f, 0), new Vector3(0.415f, 0.04f, 0.25f), stripe);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.30f, 0), new Vector3(0.415f, 0.04f, 0.25f), stripe);
                    var hat = M(new Color(1f, 0.82f, 0.1f), 0.6f);
                    Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.2f, 0), new Vector3(0.25f, 0.15f, 0.27f), hat);
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.17f, 0.02f), new Vector3(0.29f, 0.01f, 0.31f), hat);
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.03f, 0), new Vector3(0.36f, 0.07f, 0.22f), M(new Color(0.3f, 0.2f, 0.1f), 0.4f));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0.17f, -0.03f, 0.06f), new Vector3(0.06f, 0.1f, 0.08f), M(new Color(0.2f, 0.2f, 0.22f), 0.6f, 0.8f));
                    break;
                }
                default:
                    if (rnd.NextDouble() < 0.4) // jacket
                    {
                        var jacket = M(Pick(new[] { new Color(0.2f, 0.15f, 0.1f), new Color(0.1f, 0.12f, 0.15f), new Color(0.4f, 0.42f, 0.35f) }), 0.35f);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.27f, -0.005f), new Vector3(0.43f, 0.46f, 0.25f), jacket);
                    }
                    if (rnd.NextDouble() < 0.35) // backpack
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.27f, -0.18f), new Vector3(0.28f, 0.34f, 0.13f), M(Pick(ShirtColors), 0.2f));
                    break;
            }

            Renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in Renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.gameObject.layer = gameObject.layer;
            }
            UniqueMaterials = unique.ToArray();
            phase = Range(0f, 10f);
            _ = headGo;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public void SetEyesGlow(bool on)
        {
            if (!eyeMat) return;
            MatFactory.SetEmission(eyeMat, on ? Palette.Amoeba * 4f : Color.black);
        }

        /// <summary>World-space direction for the head to look toward (null clears).</summary>
        public void LookAt(Vector3? worldPoint)
        {
            hasLook = worldPoint.HasValue;
            if (hasLook) lookDir = worldPoint.Value - (Head ? Head.position : transform.position);
        }

        /// <summary>Drive the procedural animation. velocity in world space.</summary>
        public void Animate(Vector3 velocity, bool grounded, HostState state, float runSpeed, float dt)
        {
            if (!Model) return;
            float target = new Vector3(velocity.x, 0, velocity.z).magnitude;
            speedSmoothed = Mathf.Lerp(speedSmoothed, target, 1f - Mathf.Exp(-10f * dt));
            float s = speedSmoothed;
            float run01 = Mathf.InverseLerp(2.2f, Mathf.Max(2.3f, runSpeed), s);

            bool lying = state == HostState.Unconscious;
            bool kneeling = state == HostState.Captured;
            lieT = Mathf.MoveTowards(lieT, lying ? 1f : 0f, dt * (lying ? 2.2f : 1.3f));
            kneelT = Mathf.MoveTowards(kneelT, kneeling ? 1f : 0f, dt * 3f);
            airT = Mathf.MoveTowards(airT, grounded ? 0f : 1f, dt * 6f);

            // gait
            float stride = Mathf.Lerp(1.25f, 2.1f, run01);
            phase += (s / stride) * Mathf.PI * 2f * dt;
            float amp = Mathf.Clamp01(s / 1.6f);
            float legSwing = Mathf.Lerp(28f, 48f, run01) * amp;
            float armSwing = Mathf.Lerp(22f, 55f, run01) * amp;
            float sinP = Mathf.Sin(phase), cosP = Mathf.Cos(phase);

            float lLeg = sinP * legSwing, rLeg = -sinP * legSwing;
            float lKnee = Mathf.Max(0, -cosP) * Mathf.Lerp(35f, 85f, run01) * amp + 4f;
            float rKnee = Mathf.Max(0, cosP) * Mathf.Lerp(35f, 85f, run01) * amp + 4f;
            float breathe = Mathf.Sin(Time.time * 1.7f + phase * 0.1f) * 1.2f;
            float lean = Mathf.Lerp(2f, 12f, run01) * amp;
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.04f * amp * (1 + run01);

            // airborne pose
            lLeg = Mathf.Lerp(lLeg, -35f, airT); rLeg = Mathf.Lerp(rLeg, 10f, airT);
            lKnee = Mathf.Lerp(lKnee, 70f, airT); rKnee = Mathf.Lerp(rKnee, 30f, airT);

            float lArm = -sinP * armSwing, rArm = sinP * armSwing;
            float armOut = 6f + airT * 25f;
            float elbow = Mathf.Lerp(8f, 75f, run01) * Mathf.Max(amp, 0.2f) + 8f;

            // captured: hands behind back, kneel
            if (kneelT > 0f)
            {
                lLeg = Mathf.Lerp(lLeg, -80f, kneelT); rLeg = Mathf.Lerp(rLeg, 5f, kneelT);
                lKnee = Mathf.Lerp(lKnee, 85f, kneelT); rKnee = Mathf.Lerp(rKnee, 95f, kneelT);
                lArm = Mathf.Lerp(lArm, 35f, kneelT); rArm = Mathf.Lerp(rArm, 35f, kneelT);
                armOut = Mathf.Lerp(armOut, -12f, kneelT); elbow = Mathf.Lerp(elbow, 60f, kneelT);
                lean = Mathf.Lerp(lean, 18f, kneelT);
            }

            lThigh.localRotation = Quaternion.Euler(lLeg, 0, 0);
            rThigh.localRotation = Quaternion.Euler(rLeg, 0, 0);
            lShin.localRotation = Quaternion.Euler(lKnee, 0, 0);
            rShin.localRotation = Quaternion.Euler(rKnee, 0, 0);
            lUpperArm.localRotation = Quaternion.Euler(lArm, 0, -armOut);
            rUpperArm.localRotation = Quaternion.Euler(rArm, 0, armOut);
            lForearm.localRotation = Quaternion.Euler(-elbow, 0, 0);
            rForearm.localRotation = Quaternion.Euler(-elbow, 0, 0);
            spine.localRotation = Quaternion.Euler(lean + breathe, Mathf.Sin(phase) * 6f * amp, 0);

            // possessed: occasional unnatural head twitch
            if (state == HostState.Possessed)
            {
                twitchT -= dt;
                if (twitchT < -0.18f) twitchT = Random.Range(2.5f, 6f);
            }
            Quaternion headRot = Quaternion.identity;
            if (hasLook && lookDir.sqrMagnitude > 0.01f && state == HostState.Normal)
            {
                var local = Quaternion.Inverse(spine.rotation) * Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                var e = local.eulerAngles;
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(0, e.y), -70f, 70f), pitch = Mathf.Clamp(Mathf.DeltaAngle(0, e.x), -30f, 30f);
                headRot = Quaternion.Euler(pitch, yaw, 0);
            }
            if (state == HostState.Possessed && twitchT < 0f) headRot *= Quaternion.Euler(0, 0, 25f * Mathf.Sin(Time.time * 60f));
            neck.localRotation = Quaternion.Slerp(neck.localRotation, headRot, 1f - Mathf.Exp(-8f * dt));

            // whole-body: lie down / kneel offsets
            float kneelDrop = kneelT * 0.42f;
            hips.localPosition = new Vector3(0, 0.95f + bob - kneelDrop, 0);
            float lieEase = lieT * lieT * (3f - 2f * lieT);
            Model.localRotation = Quaternion.Euler(-90f * lieEase, 0, 0);
            Model.localPosition = new Vector3(0, 0.12f * lieEase, 0);
        }
    }
}
