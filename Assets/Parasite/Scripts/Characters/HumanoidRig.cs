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
        readonly List<Material> eyeGlowMats = new();
        readonly List<Material> veinGlowMats = new();
        Material eyeMat;
        bool eyesGlowOn;
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
        static readonly Color[] IrisColors =
        {
            new(0.12f, 0.28f, 0.58f), // deep blue
            new(0.48f, 0.35f, 0.16f), // warm hazel
            new(0.15f, 0.42f, 0.22f), // forest green
            new(0.24f, 0.14f, 0.08f), // dark brown
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

        enum Fabric { None, Jersey, Poplin, Wool, Melange, Denim, Leather }

        Material M(Color c, float smooth = 0.3f, float metal = 0f)
        {
            var m = MatFactory.LitUnique(c, smooth, metal);
            unique.Add(m);
            return m;
        }

        Material F(Color tint, Fabric f, float smooth = 0.2f, float tiling = 3f)
        {
            string a = f switch
            {
                Fabric.Jersey => "cotton_jersey_grey", Fabric.Poplin => "stretch_poplin_grey", Fabric.Wool => "caban_grey",
                Fabric.Melange => "jersey_melange_grey", Fabric.Denim => "denim_fabric_albedo", Fabric.Leather => "brown_leather_albedo", _ => null,
            };
            string n = f switch
            {
                Fabric.Jersey => "cotton_jersey_normal", Fabric.Poplin => "stretch_poplin_normal", Fabric.Wool => "caban_normal",
                Fabric.Melange => "jersey_melange_normal", Fabric.Denim => "denim_fabric_normal", Fabric.Leather => "brown_leather_normal", _ => null,
            };
            var alb = a != null ? MatFactory.Tex("Textures/Clothes/" + a) : null;
            var nor = n != null ? MatFactory.Tex("Textures/Clothes/" + n) : null;
            var m = MatFactory.LitUnique(tint, smooth, 0f, alb, nor, tiling, 0.8f);
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

            var skinC = Pick(SkinTones);
            var skin = M(skinC, 0.42f);
            var hairC = isDrVale ? new Color(0.78f, 0.78f, 0.80f) : Pick(HairColors);
            var hair = F(hairC * 1.15f, Fabric.Wool, 0.35f, 5f);
            var hairHighlight = M(hairC * 1.25f + new Color(0.06f, 0.05f, 0.03f), 0.45f);
            Color shirtC = Pick(ShirtColors), pantsC = Pick(PantsColors);
            var shoes = F(new Color(0.28f, 0.26f, 0.25f), Fabric.Leather, 0.55f, 2f);
            Material shirt, pants;
            switch (profession)
            {
                case Profession.Guard:
                    shirt = F(new Color(0.16f, 0.22f, 0.40f), Fabric.Poplin); pants = F(new Color(0.12f, 0.13f, 0.17f), Fabric.Wool); break;
                case Profession.Scientist:
                    shirt = F(Pick(new[] { new Color(0.62f, 0.72f, 0.86f), new Color(0.45f, 0.48f, 0.52f), new Color(0.7f, 0.28f, 0.32f) }), Fabric.Poplin);
                    pants = F(new Color(0.25f, 0.26f, 0.3f), Fabric.Wool); break;
                case Profession.Maintenance:
                    shirt = F(new Color(0.24f, 0.34f, 0.55f), Fabric.Wool); pants = F(new Color(0.24f, 0.34f, 0.55f), Fabric.Wool); break;
                default:
                    shirt = F(shirtC * 1.25f, rnd.NextDouble() < 0.5 ? Fabric.Jersey : Fabric.Melange);
                    pants = rnd.NextDouble() < 0.6 ? F(Color.Lerp(Color.white, pantsC * 2f, 0.3f), Fabric.Denim, 0.15f, 2.5f) : F(pantsC * 1.4f, Fabric.Wool);
                    break;
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
            Prim.Make(PrimitiveType.Capsule, spine, new Vector3(0, 0.16f, 0), new Vector3(0.33f, 0.2f, 0.21f), shirt);
            Prim.Make(PrimitiveType.Sphere, spine, new Vector3(0, 0.33f, 0), new Vector3(0.40f, 0.30f, 0.245f), shirt);
            Prim.Make(PrimitiveType.Sphere, spine, new Vector3(-0.19f, 0.41f, 0), new Vector3(0.14f, 0.13f, 0.15f), shirt);
            Prim.Make(PrimitiveType.Sphere, spine, new Vector3(0.19f, 0.41f, 0), new Vector3(0.14f, 0.13f, 0.15f), shirt);
            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(0, 0.02f, 0), new Vector3(0.09f, 0.05f, 0.09f), skin);
            var headGo = Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.11f, 0.0f), new Vector3(0.20f, 0.24f, 0.22f), skin);

            // --- sculpted facial features ---
            // Nose: bridge with tip and nostril flares
            Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.106f, 0.106f), new Vector3(0.018f, 0.048f, 0.024f), skin, false, null, Quaternion.Euler(-18f, 0, 0));
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.082f, 0.122f), new Vector3(0.024f, 0.020f, 0.022f), skin);
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(-0.015f, 0.078f, 0.114f), new Vector3(0.014f, 0.012f, 0.014f), skin);
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0.015f, 0.078f, 0.114f), new Vector3(0.014f, 0.012f, 0.014f), skin);
            // Jaw & chin
            Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.032f, 0.088f), new Vector3(0.085f, 0.06f, 0.075f), skin);

            // Defined lips with natural skin-tone tinting
            var upperLipMat = M(Color.Lerp(skinC, new Color(0.56f, 0.22f, 0.24f), 0.45f), 0.5f);
            var lowerLipMat = M(Color.Lerp(skinC, new Color(0.65f, 0.26f, 0.28f), 0.50f), 0.55f);
            Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.055f, 0.108f), new Vector3(0.044f, 0.008f, 0.014f), upperLipMat, false, null, Quaternion.Euler(-6f, 0, 0));
            Prim.Make(PrimitiveType.Capsule, Head, new Vector3(0, 0.044f, 0.106f), new Vector3(0.040f, 0.010f, 0.015f), lowerLipMat, false, null, Quaternion.Euler(0, 0, 90f));

            // Ears with inner concha detail
            var earInner = M(skinC * 0.70f, 0.25f);
            foreach (float ex in new[] { -0.105f, 0.105f })
            {
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex, 0.112f, -0.005f), new Vector3(0.024f, 0.068f, 0.044f), skin, false, null, Quaternion.Euler(0, ex < 0 ? -12f : 12f, ex < 0 ? -6f : 6f));
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex * 0.98f, 0.114f, 0.002f), new Vector3(0.016f, 0.036f, 0.022f), earInner);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex, 0.084f, -0.006f), new Vector3(0.018f, 0.020f, 0.022f), skin);
            }

            // Realistic layered eyes: sclera, chosen iris color, central dark pupil, and specular highlight
            var sclera = M(new Color(0.94f, 0.94f, 0.92f), 0.75f);
            var irisC = Pick(IrisColors);
            var irisMat = M(irisC, 0.88f);
            eyeMat = M(new Color(0.04f, 0.04f, 0.05f), 0.95f); // central dark pupil
            var specular = M(new Color(0.98f, 0.98f, 1f), 0.99f);
            eyeGlowMats.Add(irisMat);

            var brow = M(hairC * 0.75f, 0.25f);
            foreach (float ex in new[] { -0.045f, 0.045f })
            {
                // Sclera base
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex, 0.13f, 0.094f), new Vector3(0.034f, 0.026f, 0.02f), sclera);
                // Colored iris
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex, 0.13f, 0.102f), new Vector3(0.020f, 0.020f, 0.008f), irisMat);
                // Central dark pupil
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex, 0.13f, 0.105f), new Vector3(0.010f, 0.010f, 0.006f), eyeMat);
                // High-gloss specular highlight
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(ex + 0.004f, 0.134f, 0.107f), new Vector3(0.0035f, 0.0035f, 0.003f), specular);
                // Upper eyelid crease
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(ex, 0.144f, 0.098f), new Vector3(0.036f, 0.006f, 0.016f), skin);

                // Arched eyebrows (inner arch + outer tapering tail)
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(ex * 0.65f, 0.156f, 0.103f), new Vector3(0.024f, 0.008f, 0.012f), brow, false, null, Quaternion.Euler(0, 0, ex < 0 ? -4f : 4f));
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(ex * 1.25f, 0.152f, 0.096f), new Vector3(0.026f, 0.007f, 0.012f), brow, false, null, Quaternion.Euler(0, ex < 0 ? 12f : -12f, ex < 0 ? -15f : 15f));
            }

            // --- Amoeba Possession Feedback: subtle emerald energy filaments/veins on neck and wrists ---
            var veinMat = M(new Color(0.20f, 0.38f, 0.25f), 0.6f);
            veinGlowMats.Add(veinMat);
            // Neck filaments
            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(-0.032f, 0.022f, 0.026f), new Vector3(0.005f, 0.045f, 0.005f), veinMat, false, null, Quaternion.Euler(12f, 0, -10f));
            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(0.032f, 0.022f, 0.026f), new Vector3(0.005f, 0.045f, 0.005f), veinMat, false, null, Quaternion.Euler(12f, 0, 10f));
            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(-0.042f, 0.020f, -0.010f), new Vector3(0.005f, 0.042f, 0.005f), veinMat, false, null, Quaternion.Euler(-8f, 0, -6f));
            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(0.042f, 0.020f, -0.010f), new Vector3(0.005f, 0.042f, 0.005f), veinMat, false, null, Quaternion.Euler(-8f, 0, 6f));
            // Wrist filaments
            Prim.Make(PrimitiveType.Cylinder, lForearm, new Vector3(0, -0.255f, 0), new Vector3(0.088f, 0.012f, 0.065f), veinMat);
            Prim.Make(PrimitiveType.Cube, lForearm, new Vector3(0, -0.245f, 0.032f), new Vector3(0.032f, 0.028f, 0.005f), veinMat, false, null, Quaternion.Euler(0, 0, 15f));
            Prim.Make(PrimitiveType.Cylinder, rForearm, new Vector3(0, -0.255f, 0), new Vector3(0.088f, 0.012f, 0.065f), veinMat);
            Prim.Make(PrimitiveType.Cube, rForearm, new Vector3(0, -0.245f, 0.032f), new Vector3(0.032f, 0.028f, 0.005f), veinMat, false, null, Quaternion.Euler(0, 0, -15f));

            // --- Volumetric Hair Styles ---
            int hairStyle = isDrVale ? 0 : rnd.Next(profession == Profession.Civilian ? 5 : 4);
            if (hairStyle == 0) // styled side-part
            {
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.175f, -0.01f), new Vector3(0.218f, 0.145f, 0.23f), hair);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0.025f, 0.19f, 0.01f), new Vector3(0.185f, 0.125f, 0.19f), hair);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.115f, -0.075f), new Vector3(0.205f, 0.145f, 0.11f), hair);
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(0.035f, 0.182f, 0.088f), new Vector3(0.145f, 0.045f, 0.075f), hair, false, null, Quaternion.Euler(0, 0, -8f));
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(0.045f, 0.205f, 0.03f), new Vector3(0.085f, 0.018f, 0.135f), hairHighlight, false, null, Quaternion.Euler(0, 0, -6f));
            }
            else if (hairStyle == 1) // voluminous crop
            {
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.17f, -0.01f), new Vector3(0.215f, 0.14f, 0.225f), hair);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.208f, 0.015f), new Vector3(0.195f, 0.105f, 0.19f), hair);
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.188f, 0.092f), new Vector3(0.165f, 0.042f, 0.06f), hair);
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.224f, 0.02f), new Vector3(0.105f, 0.02f, 0.12f), hairHighlight);
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(-0.095f, 0.145f, -0.01f), new Vector3(0.03f, 0.09f, 0.17f), hair);
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(0.095f, 0.145f, -0.01f), new Vector3(0.03f, 0.09f, 0.17f), hair);
            }
            else if (hairStyle == 2) // parted wavy hair
            {
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.175f, -0.01f), new Vector3(0.225f, 0.15f, 0.23f), hair);
                Prim.Make(PrimitiveType.Capsule, Head, new Vector3(-0.095f, 0.11f, 0.01f), new Vector3(0.065f, 0.18f, 0.16f), hair, false, null, Quaternion.Euler(6f, 0, -8f));
                Prim.Make(PrimitiveType.Capsule, Head, new Vector3(0.095f, 0.11f, 0.01f), new Vector3(0.065f, 0.18f, 0.16f), hair, false, null, Quaternion.Euler(6f, 0, 8f));
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.075f, -0.095f), new Vector3(0.21f, 0.16f, 0.10f), hair);
                Prim.Make(PrimitiveType.Capsule, Head, new Vector3(-0.08f, 0.145f, 0.055f), new Vector3(0.035f, 0.07f, 0.045f), hairHighlight, false, null, Quaternion.Euler(0, 0, -10f));
                Prim.Make(PrimitiveType.Capsule, Head, new Vector3(0.08f, 0.145f, 0.055f), new Vector3(0.035f, 0.07f, 0.045f), hairHighlight, false, null, Quaternion.Euler(0, 0, 10f));
            }
            else if (hairStyle == 3) // ponytail / bun
            {
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.172f, -0.01f), new Vector3(0.215f, 0.14f, 0.22f), hair);
                Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.202f, -0.01f), new Vector3(0.12f, 0.018f, 0.14f), hairHighlight);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.142f, -0.125f), new Vector3(0.08f, 0.08f, 0.07f), hair);
                Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.142f, -0.112f), new Vector3(0.068f, 0.016f, 0.068f), M(new Color(0.15f, 0.15f, 0.18f), 0.3f), false, null, Quaternion.Euler(90, 0, 0));
                Prim.Make(PrimitiveType.Capsule, Head, new Vector3(0, 0.065f, -0.145f), new Vector3(0.052f, 0.14f, 0.052f), hair, false, null, Quaternion.Euler(16f, 0, 0));
            }
            else // civilian beanie / cap option
            {
                var beanieMat = F(Pick(ShirtColors), Fabric.Wool, 0.35f, 4f);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.205f, -0.01f), new Vector3(0.235f, 0.15f, 0.245f), beanieMat);
                Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.165f, 0.005f), new Vector3(0.245f, 0.045f, 0.255f), beanieMat);
                Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.115f, -0.08f), new Vector3(0.19f, 0.08f, 0.07f), hair);
            }

            // --- belt with metallic chrome buckle on pants for all characters ---
            var beltMat = F(new Color(0.12f, 0.10f, 0.09f), Fabric.Leather, 0.45f, 2f);
            var chromeMat = M(new Color(0.85f, 0.86f, 0.88f), 0.92f, 0.95f);
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.052f, 0), new Vector3(0.345f, 0.038f, 0.212f), beltMat);
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.052f, 0.108f), new Vector3(0.065f, 0.042f, 0.012f), chromeMat);
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.052f, 0.113f), new Vector3(0.038f, 0.024f, 0.006f), beltMat);
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.052f, 0.115f), new Vector3(0.006f, 0.028f, 0.005f), chromeMat);
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(-0.13f, 0.052f, 0.107f), new Vector3(0.012f, 0.044f, 0.008f), pants);
            Prim.Make(PrimitiveType.Cube, hips, new Vector3(0.13f, 0.052f, 0.107f), new Vector3(0.012f, 0.044f, 0.008f), pants);

            // --- limbs ---
            foreach (var (thigh, shin) in new[] { (lThigh, lShin), (rThigh, rShin) })
            {
                Prim.Make(PrimitiveType.Capsule, thigh, new Vector3(0, -0.22f, 0), new Vector3(0.15f, 0.23f, 0.15f), pants);
                Prim.Make(PrimitiveType.Capsule, shin, new Vector3(0, -0.22f, 0), new Vector3(0.12f, 0.23f, 0.12f), pants);
                Prim.Make(PrimitiveType.Capsule, shin, new Vector3(0, -0.465f, 0.05f), new Vector3(0.11f, 0.135f, 0.075f), shoes, false, null, Quaternion.Euler(90, 0, 0));
                Prim.Make(PrimitiveType.Cube, shin, new Vector3(0, -0.495f, 0.05f), new Vector3(0.115f, 0.02f, 0.27f), M(new Color(0.05f, 0.05f, 0.05f), 0.3f));
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
                    var capMat = F(new Color(0.09f, 0.11f, 0.16f), Fabric.Wool, 0.3f);
                    var goldMat = M(new Color(0.88f, 0.72f, 0.22f), 0.90f, 0.95f);
                    // Visor cap with gold piping and crest
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.225f, 0), new Vector3(0.235f, 0.052f, 0.245f), capMat);
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.192f, 0.135f), new Vector3(0.205f, 0.015f, 0.11f), M(new Color(0.03f, 0.03f, 0.03f), 0.90f), false, null, Quaternion.Euler(14f, 0, 0));
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.206f, 0.115f), new Vector3(0.21f, 0.012f, 0.014f), goldMat);
                    Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.232f, 0.120f), new Vector3(0.036f, 0.038f, 0.012f), goldMat);

                    // Security badge on chest pocket
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.095f, 0.36f, 0.125f), new Vector3(0.075f, 0.085f, 0.012f), shirt);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.095f, 0.385f, 0.132f), new Vector3(0.048f, 0.055f, 0.008f), goldMat);
                    Prim.Make(PrimitiveType.Sphere, spine, new Vector3(-0.095f, 0.385f, 0.137f), new Vector3(0.018f, 0.018f, 0.008f), M(new Color(0.95f, 0.9f, 0.5f), 0.95f, 1f));

                    // Shoulder-mounted radio/walkie-talkie with antenna
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.165f, 0.475f, 0.015f), new Vector3(0.045f, 0.078f, 0.038f), M(new Color(0.12f, 0.12f, 0.14f), 0.5f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.165f, 0.478f, 0.035f), new Vector3(0.032f, 0.035f, 0.004f), M(new Color(0.06f, 0.06f, 0.06f), 0.2f));
                    Prim.Make(PrimitiveType.Cylinder, spine, new Vector3(-0.165f, 0.565f, 0.015f), new Vector3(0.006f, 0.11f, 0.006f), M(new Color(0.06f, 0.06f, 0.06f), 0.3f));
                    Prim.Make(PrimitiveType.Cylinder, spine, new Vector3(-0.15f, 0.52f, 0.015f), new Vector3(0.012f, 0.016f, 0.012f), M(new Color(0.2f, 0.2f, 0.22f), 0.5f));

                    // Tactical duty belt with utility pouch, holster and accessories
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.052f, 0), new Vector3(0.36f, 0.055f, 0.225f), M(new Color(0.04f, 0.04f, 0.05f), 0.5f));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0.185f, -0.03f, 0.02f), new Vector3(0.055f, 0.13f, 0.085f), M(new Color(0.08f, 0.08f, 0.09f), 0.4f));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0.188f, 0.045f, 0.04f), new Vector3(0.026f, 0.06f, 0.036f), M(new Color(0.15f, 0.15f, 0.16f), 0.6f), false, null, Quaternion.Euler(22f, 0, 0));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(-0.182f, 0.025f, 0.02f), new Vector3(0.045f, 0.075f, 0.075f), M(new Color(0.08f, 0.08f, 0.09f), 0.4f));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0.07f, 0.055f, -0.115f), new Vector3(0.055f, 0.062f, 0.035f), M(new Color(0.08f, 0.08f, 0.09f), 0.4f));
                    Prim.Make(PrimitiveType.Cylinder, hips, new Vector3(-0.08f, 0.03f, -0.115f), new Vector3(0.028f, 0.11f, 0.028f), M(new Color(0.08f, 0.08f, 0.09f), 0.4f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.32f, 0.118f), new Vector3(0.02f, 0.3f, 0.005f), M(new Color(0.05f, 0.05f, 0.07f)));
                    break;
                }
                case Profession.Scientist:
                {
                    var coat = F(new Color(1f, 1f, 1f) * 1.08f, Fabric.Poplin, 0.25f, 2.5f);
                    var darkLapelMat = F(new Color(0.18f, 0.20f, 0.24f), Fabric.Wool, 0.35f);
                    var goldMat = M(new Color(0.88f, 0.72f, 0.22f), 0.90f, 0.95f);
                    var lapelMat = isDrVale ? darkLapelMat : coat;

                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.24f, 0), new Vector3(0.42f, 0.52f, 0.25f), coat);
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, -0.18f, 0), new Vector3(0.40f, 0.36f, 0.24f), coat);
                    foreach (var upper in new[] { lUpperArm, rUpperArm })
                        Prim.Make(PrimitiveType.Capsule, upper, new Vector3(0, -0.14f, 0), new Vector3(0.125f, 0.165f, 0.125f), coat);
                    foreach (var fore in new[] { lForearm, rForearm })
                        Prim.Make(PrimitiveType.Capsule, fore, new Vector3(0, -0.12f, 0), new Vector3(0.11f, 0.14f, 0.11f), coat);

                    // Fold-over lapels and collar
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.065f, 0.38f, 0.128f), new Vector3(0.052f, 0.17f, 0.012f), lapelMat, false, null, Quaternion.Euler(0, 0, -18f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.065f, 0.38f, 0.128f), new Vector3(0.052f, 0.17f, 0.012f), lapelMat, false, null, Quaternion.Euler(0, 0, 18f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.485f, 0.02f), new Vector3(0.24f, 0.045f, 0.21f), lapelMat);

                    // Breast pocket with clipped ID badge and pen
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.11f, 0.33f, 0.132f), new Vector3(0.065f, 0.075f, 0.008f), coat);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.118f, 0.33f, 0.138f), new Vector3(0.046f, 0.062f, 0.005f), M(new Color(0.95f, 0.95f, 0.98f), 0.6f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.118f, 0.364f, 0.141f), new Vector3(0.016f, 0.012f, 0.008f), chromeMat);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.118f, 0.352f, 0.141f), new Vector3(0.042f, 0.012f, 0.003f), M(isDrVale ? new Color(0.75f, 0.12f, 0.15f) : new Color(0.12f, 0.42f, 0.85f), 0.5f));
                    Prim.Make(PrimitiveType.Cylinder, spine, new Vector3(0.082f, 0.345f, 0.138f), new Vector3(0.007f, 0.045f, 0.007f), isDrVale ? goldMat : M(new Color(0.1f, 0.1f, 0.15f), 0.8f));
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.082f, 0.342f, 0.143f), new Vector3(0.004f, 0.024f, 0.004f), isDrVale ? goldMat : chromeMat);

                    // Spectacles on face
                    var specMat = isDrVale ? goldMat : M(new Color(0.75f, 0.76f, 0.78f), 0.9f, 0.95f);
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.134f, 0.114f), new Vector3(0.022f, 0.005f, 0.008f), specMat);
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(-0.045f, 0.13f, 0.113f), new Vector3(0.044f, 0.004f, 0.044f), specMat, false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0.045f, 0.13f, 0.113f), new Vector3(0.044f, 0.004f, 0.044f), specMat, false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(-0.045f, 0.13f, 0.113f), new Vector3(0.040f, 0.002f, 0.040f), M(new Color(0.9f, 0.95f, 1f), 0.98f), false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0.045f, 0.13f, 0.113f), new Vector3(0.040f, 0.002f, 0.040f), M(new Color(0.9f, 0.95f, 1f), 0.98f), false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(-0.092f, 0.134f, 0.05f), new Vector3(0.005f, 0.005f, 0.115f), specMat);
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0.092f, 0.134f, 0.05f), new Vector3(0.005f, 0.005f, 0.115f), specMat);

                    // Wrist watch
                    Prim.Make(PrimitiveType.Cylinder, lForearm, new Vector3(0, -0.215f, 0), new Vector3(0.10f, 0.022f, 0.10f), F(new Color(0.14f, 0.11f, 0.09f), Fabric.Leather, 0.55f));
                    Prim.Make(PrimitiveType.Cylinder, lForearm, new Vector3(0, -0.215f, 0.053f), new Vector3(0.034f, 0.006f, 0.034f), chromeMat, false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cylinder, lForearm, new Vector3(0, -0.215f, 0.056f), new Vector3(0.026f, 0.002f, 0.026f), M(new Color(0.92f, 0.92f, 0.94f), 0.9f), false, null, Quaternion.Euler(90, 0, 0));

                    if (isDrVale)
                    {
                        // Dr. Vale formal burgundy tie & tie clip
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.285f, 0.128f), new Vector3(0.046f, 0.26f, 0.012f), F(new Color(0.42f, 0.06f, 0.12f), Fabric.Poplin, 0.45f));
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.325f, 0.135f), new Vector3(0.038f, 0.006f, 0.005f), goldMat);
                        Prim.Make(PrimitiveType.Sphere, spine, new Vector3(0, 0.415f, 0.130f), new Vector3(0.036f, 0.032f, 0.024f), F(new Color(0.38f, 0.05f, 0.10f), Fabric.Poplin));
                    }
                    break;
                }
                case Profession.Maintenance:
                {
                    var vest = F(new Color(1.2f, 0.5f, 0.06f), Fabric.Melange, 0.3f);
                    var stripe = M(new Color(0.92f, 0.94f, 0.96f), 0.95f, 0.35f);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.27f, 0), new Vector3(0.41f, 0.42f, 0.245f), vest);
                    // High-visibility reflective silver safety stripes across chest, back, and shoulders
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.18f, 0), new Vector3(0.418f, 0.042f, 0.252f), stripe);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.30f, 0), new Vector3(0.418f, 0.042f, 0.252f), stripe);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.11f, 0.38f, 0), new Vector3(0.042f, 0.09f, 0.254f), stripe);
                    Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.11f, 0.38f, 0), new Vector3(0.042f, 0.09f, 0.254f), stripe);

                    // Upper sleeve reflective armbands
                    Prim.Make(PrimitiveType.Cylinder, lUpperArm, new Vector3(0, -0.09f, 0), new Vector3(0.118f, 0.035f, 0.118f), stripe);
                    Prim.Make(PrimitiveType.Cylinder, rUpperArm, new Vector3(0, -0.09f, 0), new Vector3(0.118f, 0.035f, 0.118f), stripe);

                    // Safety hard hat with front headlamp/reflector
                    var hat = M(new Color(1f, 0.82f, 0.1f), 0.65f);
                    Prim.Make(PrimitiveType.Sphere, Head, new Vector3(0, 0.20f, 0), new Vector3(0.255f, 0.15f, 0.275f), hat);
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.168f, 0.02f), new Vector3(0.295f, 0.012f, 0.315f), hat);
                    Prim.Make(PrimitiveType.Cube, Head, new Vector3(0, 0.205f, 0.138f), new Vector3(0.048f, 0.032f, 0.022f), M(new Color(0.12f, 0.12f, 0.14f), 0.5f));
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.205f, 0.150f), new Vector3(0.028f, 0.005f, 0.028f), M(new Color(0.98f, 0.98f, 0.85f), 0.98f), false, null, Quaternion.Euler(90, 0, 0));
                    Prim.Make(PrimitiveType.Cylinder, Head, new Vector3(0, 0.205f, 0.005f), new Vector3(0.26f, 0.018f, 0.28f), M(new Color(0.1f, 0.1f, 0.12f), 0.4f));

                    // Utility belt loop with metal wrench handle and tool pouch
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0, 0.03f, 0), new Vector3(0.36f, 0.07f, 0.22f), M(new Color(0.3f, 0.2f, 0.1f), 0.4f));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(-0.18f, 0.03f, 0.02f), new Vector3(0.02f, 0.06f, 0.05f), beltMat);
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(-0.185f, -0.06f, 0.02f), new Vector3(0.015f, 0.16f, 0.02f), chromeMat, false, null, Quaternion.Euler(0, 0, -8f));
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(-0.192f, 0.025f, 0.02f), new Vector3(0.042f, 0.032f, 0.022f), chromeMat);
                    Prim.Make(PrimitiveType.Cube, hips, new Vector3(0.175f, -0.02f, 0.04f), new Vector3(0.06f, 0.11f, 0.08f), F(new Color(0.28f, 0.18f, 0.1f), Fabric.Leather, 0.4f));

                    // Work boots with rubber sole tread
                    foreach (var shin in new[] { lShin, rShin })
                    {
                        Prim.Make(PrimitiveType.Cube, shin, new Vector3(0, -0.505f, 0.05f), new Vector3(0.122f, 0.02f, 0.275f), M(new Color(0.08f, 0.08f, 0.08f), 0.2f));
                        foreach (float tz in new[] { -0.06f, -0.02f, 0.05f, 0.12f })
                            Prim.Make(PrimitiveType.Cube, shin, new Vector3(0, -0.518f, 0.05f + tz), new Vector3(0.118f, 0.008f, 0.022f), M(new Color(0.05f, 0.05f, 0.05f), 0.15f));
                    }
                    break;
                }
                default:
                {
                    bool hasJacket = rnd.NextDouble() < 0.45;
                    if (hasJacket)
                    {
                        var jacket = rnd.NextDouble() < 0.5 ? F(new Color(0.9f, 0.85f, 0.8f), Fabric.Leather, 0.45f, 2f)
                                                            : F(Pick(new[] { new Color(0.22f, 0.24f, 0.3f), new Color(0.45f, 0.47f, 0.4f), new Color(0.35f, 0.22f, 0.15f) }), Fabric.Wool);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.27f, -0.005f), new Vector3(0.43f, 0.46f, 0.25f), jacket);
                        // Jacket collar and chrome zipper
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.46f, 0), new Vector3(0.25f, 0.06f, 0.23f), jacket);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.26f, 0.128f), new Vector3(0.012f, 0.38f, 0.008f), chromeMat);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.36f, 0.134f), new Vector3(0.014f, 0.022f, 0.006f), chromeMat);
                    }
                    else
                    {
                        int acc = rnd.Next(3);
                        if (acc == 1) // Hoodie
                        {
                            var hoodieMat = F(shirtC * 1.1f, Fabric.Jersey, 0.3f);
                            Prim.Make(PrimitiveType.Sphere, spine, new Vector3(0, 0.44f, -0.09f), new Vector3(0.25f, 0.14f, 0.18f), hoodieMat);
                            Prim.Make(PrimitiveType.Cylinder, spine, new Vector3(-0.04f, 0.36f, 0.128f), new Vector3(0.005f, 0.12f, 0.005f), M(new Color(0.9f, 0.9f, 0.9f), 0.3f));
                            Prim.Make(PrimitiveType.Cylinder, spine, new Vector3(0.04f, 0.36f, 0.128f), new Vector3(0.005f, 0.12f, 0.005f), M(new Color(0.9f, 0.9f, 0.9f), 0.3f));
                        }
                        else if (acc == 2) // Scarf
                        {
                            var scarfMat = F(Pick(ShirtColors), Fabric.Wool, 0.3f);
                            Prim.Make(PrimitiveType.Cylinder, neck, new Vector3(0, 0.025f, 0), new Vector3(0.16f, 0.055f, 0.16f), scarfMat);
                            Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.05f, 0.35f, 0.132f), new Vector3(0.065f, 0.20f, 0.018f), scarfMat, false, null, Quaternion.Euler(0, 0, 8f));
                        }
                    }

                    // Backpack with dual shoulder straps
                    if (rnd.NextDouble() < 0.4)
                    {
                        var packMat = M(Pick(ShirtColors), 0.35f);
                        var strapMat = M(new Color(0.12f, 0.12f, 0.14f), 0.4f);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.28f, -0.18f), new Vector3(0.28f, 0.34f, 0.15f), packMat);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0, 0.22f, -0.27f), new Vector3(0.22f, 0.18f, 0.06f), packMat);
                        Prim.Make(PrimitiveType.Cylinder, spine, new Vector3(0, 0.465f, -0.16f), new Vector3(0.065f, 0.012f, 0.04f), strapMat);
                        // Dual shoulder straps
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.11f, 0.39f, 0.01f), new Vector3(0.038f, 0.065f, 0.26f), strapMat);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(-0.11f, 0.25f, 0.124f), new Vector3(0.034f, 0.22f, 0.012f), strapMat);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.11f, 0.39f, 0.01f), new Vector3(0.038f, 0.065f, 0.26f), strapMat);
                        Prim.Make(PrimitiveType.Cube, spine, new Vector3(0.11f, 0.25f, 0.124f), new Vector3(0.034f, 0.22f, 0.012f), strapMat);
                    }

                    // Sneakers with contrasting soles
                    var soleMat = M(new Color(0.92f, 0.92f, 0.90f), 0.45f);
                    foreach (var shin in new[] { lShin, rShin })
                    {
                        Prim.Make(PrimitiveType.Cube, shin, new Vector3(0, -0.495f, 0.05f), new Vector3(0.12f, 0.025f, 0.28f), soleMat);
                        Prim.Make(PrimitiveType.Sphere, shin, new Vector3(0, -0.478f, 0.165f), new Vector3(0.088f, 0.038f, 0.055f), soleMat);
                    }
                    break;
                }
            }

            Renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in Renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.gameObject.layer = gameObject.layer;
            }

            // Exclude dedicated glow materials from HostGlow's general pulsing loop so eye & vein glow remain pure
            var hostMats = new List<Material>();
            foreach (var m in unique)
            {
                if (m && m != eyeMat && !eyeGlowMats.Contains(m) && !veinGlowMats.Contains(m))
                    hostMats.Add(m);
            }
            UniqueMaterials = hostMats.ToArray();
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
            eyesGlowOn = on;
            ApplyGlowState();
        }

        void ApplyGlowState()
        {
            Color eyeEmission = eyesGlowOn ? Palette.Amoeba * 4f : Color.black;
            Color veinEmission = eyesGlowOn ? Palette.Amoeba * 2.8f : Color.black;
            if (eyeMat) MatFactory.SetEmission(eyeMat, eyeEmission);
            for (int i = 0; i < eyeGlowMats.Count; i++)
                if (eyeGlowMats[i]) MatFactory.SetEmission(eyeGlowMats[i], eyeEmission);
            for (int i = 0; i < veinGlowMats.Count; i++)
                if (veinGlowMats[i]) MatFactory.SetEmission(veinGlowMats[i], veinEmission);
        }

        void OnDestroy()
        {
            for (int i = 0; i < unique.Count; i++)
            {
                if (unique[i])
                {
                    if (Application.isPlaying) Destroy(unique[i]);
                    else DestroyImmediate(unique[i]);
                }
            }
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

            // possessed: occasional unnatural head twitch & maintain glow
            if (state == HostState.Possessed)
            {
                twitchT -= dt;
                if (twitchT < -0.18f) twitchT = Random.Range(2.5f, 6f);
                if (eyesGlowOn) ApplyGlowState();
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
