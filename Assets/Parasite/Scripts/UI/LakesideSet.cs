using System.Collections.Generic;
using System.Linq;
using Parasite.Characters;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Parasite.UI
{
    /// <summary>Small temporary film set. It never creates a gameplay host or changes campaign statistics.</summary>
    internal sealed class LakesideSet : System.IDisposable
    {
        const int Layer = 30;
        readonly List<Material> materials = new();
        readonly List<Texture2D> textures = new();
        readonly List<GameObject> distantPulses = new();
        (Camera camera, bool enabled)[] cameras;
        (Light light, bool enabled)[] lights;
        bool fog;
        float intent;
        GameObject root;
        Material water, sky;
        HumanoidRig girl;
        public Camera Camera { get; private set; }
        public Transform Girl => girl ? girl.transform : null;
        public static Vector3 Origin => new(0, -400, 0);

        public void Build()
        {
            cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c => (c, c.enabled)).ToArray();
            lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l => (l, l.enabled)).ToArray();
            foreach (var state in cameras) state.camera.enabled = false;
            foreach (var state in lights) state.light.enabled = false;
            fog = RenderSettings.fog; RenderSettings.fog = false;
            root = new GameObject("Lakeside film set") { layer = Layer };
            root.transform.position = Origin;
            var cameraObject = new GameObject("Lakeside Camera") { tag = "MainCamera", layer = Layer };
            cameraObject.transform.SetParent(root.transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.cullingMask = 1 << Layer; Camera.fieldOfView = 49f;
            Camera.nearClipPlane = 0.06f; Camera.farClipPlane = 220f;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.67f, 0.40f, 0.39f);
            var pipeline = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            pipeline.renderPostProcessing = false; pipeline.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Tick(0f, false);

            var gradient = new Texture2D(4, 256, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, name = "Lakeside sunset gradient" };
            for (int y = 0; y < gradient.height; y++)
            {
                float t = y / 255f;
                Color c = t < 0.52f ? Color.Lerp(new Color(0.95f, 0.70f, 0.48f), new Color(0.79f, 0.47f, 0.49f), t / 0.52f)
                    : Color.Lerp(new Color(0.79f, 0.47f, 0.49f), new Color(0.20f, 0.24f, 0.40f), (t - 0.52f) / 0.48f);
                for (int x = 0; x < 4; x++) gradient.SetPixel(x, y, c);
            }
            gradient.Apply(); textures.Add(gradient);
            sky = Unlit(Color.white); sky.SetTexture("_BaseMap", gradient);
            var backdrop = Shape("Sunset sky", PrimitiveType.Quad, Camera.transform.localPosition + Camera.transform.localRotation * Vector3.forward * 150f, new Vector3(280, 160, 1), sky);
            backdrop.transform.localRotation = Camera.transform.localRotation;

            var shader = Resources.Load<Shader>("Shaders/LakesideWater");
            if (!shader) throw new System.InvalidOperationException("Lakeside water shader is missing from Resources/Shaders.");
            water = new Material(shader); materials.Add(water);
            Shape("Lake", PrimitiveType.Plane, new Vector3(0, -0.025f, 48), new Vector3(24, 1, 24), water);
            var hill = Unlit(new Color(0.24f, 0.28f, 0.35f));
            var farHill = Unlit(new Color(0.39f, 0.36f, 0.43f));
            for (int i = 0; i < 7; i++)
                Shape("Distant headland", PrimitiveType.Sphere, new Vector3(-70 + i * 25, -4f + (i % 3) * 0.4f, 93 + (i % 2) * 14), new Vector3(55, 13 + (i % 3) * 3, 26), i % 2 == 0 ? hill : farHill);
            Shape("Setting sun", PrimitiveType.Sphere, new Vector3(16, 4.7f, 70), new Vector3(5.2f, 5.2f, 3), Unlit(new Color(1f, 0.86f, 0.58f)));

            var earth = Lit(new Color(0.22f, 0.21f, 0.17f), 0.12f);
            Shape("Quiet shoreline", PrimitiveType.Cube, new Vector3(0, -0.24f, -10), new Vector3(50, 0.44f, 22), earth);
            var stone = Lit(new Color(0.34f, 0.33f, 0.29f), 0.1f);
            var reeds = Lit(new Color(0.31f, 0.30f, 0.19f), 0.05f);
            for (int i = 0; i < 27; i++)
            {
                float x = -12 + i * 0.9f;
                Shape("Shore pebble", PrimitiveType.Sphere, new Vector3(x, 0.015f, 0.8f + Mathf.Sin(i * 2.3f) * 0.3f), new Vector3(0.4f + (i % 3) * 0.2f, 0.12f, 0.28f), stone);
                if (i > 10 && i < 17) continue;
                var reed = Shape("Reed", PrimitiveType.Cylinder, new Vector3(x, 0.31f, 1.15f), new Vector3(0.025f, 0.31f + (i % 3) * 0.06f, 0.025f), reeds);
                reed.transform.localRotation = Quaternion.Euler(7, 0, (i % 5 - 2) * 5);
            }
            BuildGirl();
            var warm = new GameObject("Sunset key light").AddComponent<Light>();
            warm.transform.SetParent(root.transform, false); warm.type = LightType.Directional;
            warm.transform.rotation = Quaternion.Euler(28, -125, 0); warm.color = new Color(1f, 0.73f, 0.48f); warm.intensity = 2f; warm.cullingMask = 1 << Layer;
            var fill = new GameObject("Lake fill light").AddComponent<Light>();
            fill.transform.SetParent(root.transform, false); fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(24, 45, 0); fill.color = new Color(0.65f, 0.72f, 1f); fill.intensity = 0.7f; fill.cullingMask = 1 << Layer;
            var pulseMaterial = Unlit(new Color(0.38f, 1f, 0.58f));
            for (int i = 0; i < 11; i++)
            {
                var pulse = Shape("Another heartbeat", PrimitiveType.Sphere, new Vector3(-19 + i * 5.3f, 0.2f, 45 + (i % 3) * 8), Vector3.one * 0.1f, pulseMaterial);
                pulse.SetActive(false); distantPulses.Add(pulse);
            }
        }

        void BuildGirl()
        {
            var wood = Lit(new Color(0.28f, 0.15f, 0.10f), 0.18f);
            var bench = new GameObject("Lakeside bench").transform; bench.SetParent(root.transform, false);
            bench.localPosition = new Vector3(-1.7f, 0, 0.05f); bench.localRotation = Quaternion.Euler(0, 15, 0);
            LocalShape("Seat", PrimitiveType.Cube, bench, new Vector3(0, 0.46f, 0), new Vector3(1.6f, 0.12f, 0.48f), wood);
            LocalShape("Back", PrimitiveType.Cube, bench, new Vector3(0, 0.85f, -0.23f), new Vector3(1.6f, 0.18f, 0.06f), wood);
            foreach (float x in new[] { -0.57f, 0.57f }) LocalShape("Leg", PrimitiveType.Cube, bench, new Vector3(x, 0.23f, 0), new Vector3(0.08f, 0.46f, 0.3f), wood);
            var actor = new GameObject("SeatedGirl") { layer = Layer };
            actor.transform.SetParent(root.transform, false); actor.transform.localPosition = new Vector3(-1.7f, 0, 0.12f); actor.transform.localRotation = Quaternion.Euler(0, 15, 0);
            girl = actor.AddComponent<HumanoidRig>();
            girl.Identity = "Lakeside Girl";
            girl.UseUnscaledTime = true; // the epilogue runs while gameplay time is frozen
            girl.AmbientGestures = false;
            girl.Build(Profession.Civilian, 71, false);
            girl.SetPose(ActorPose.Sit);
            girl.FastForward(3f); // start already seated (skip the sit-down transition)
            materials.AddRange(girl.UniqueMaterials);
            // rest the pelvis on the bench: seat top is 0.52 above the bench root; the hips bone sits ~0.1 above contact
            var hips = girl.Animator && girl.Animator.isHuman ? girl.Animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips)
            {
                var seat = bench.TransformPoint(new Vector3(0f, 0.52f + 0.1f, -0.03f));
                var offset = seat - hips.position;
                offset = bench.InverseTransformVector(offset);
                offset.x = 0f; // keep her left/right placement on the bench
                actor.transform.position += bench.TransformVector(offset);
            }
            girl.SetEyesGlow(true);
            foreach (Transform item in actor.GetComponentsInChildren<Transform>()) item.gameObject.layer = Layer;
        }

        public void Tick(float age, bool world)
        {
            if (Camera)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 24f));
                Camera.transform.localPosition = Vector3.Lerp(new Vector3(0.6f, 2f, -6f), new Vector3(0.35f, 1.9f, -5.65f), t);
                Camera.transform.localRotation = Quaternion.LookRotation(new Vector3(3.1f, 1.15f, 18f) - Camera.transform.localPosition);
            }
            intent = Mathf.MoveTowards(intent, world ? 1f : 0f, Time.unscaledDeltaTime * 0.5f);
            if (water) { water.SetFloat("_QuietTime", age); water.SetFloat("_Intent", intent); }
            if (sky) sky.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.64f, 0.48f, 0.62f), intent));
            for (int i = 0; i < distantPulses.Count; i++)
            {
                distantPulses[i].SetActive(world && intent > 0.2f);
                if (world) distantPulses[i].transform.localScale = Vector3.one * (0.07f + 0.06f * Mathf.Sin(age * 1.2f - i * 0.4f));
            }
        }

        Material Lit(Color color, float smoothness) { var material = MatFactory.LitUnique(color, smoothness); materials.Add(material); return material; }
        Material Unlit(Color color) { var material = new Material(MatFactory.Unlit(color)); materials.Add(material); return material; }
        GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material) => LocalShape(name, type, root.transform, position, scale, material);
        static GameObject LocalShape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var shape = Prim.Make(type, parent, position, scale, material, false, name); shape.layer = Layer; return shape;
        }

        public void Dispose()
        {
            if (cameras != null) foreach (var state in cameras) if (state.camera) state.camera.enabled = state.enabled;
            if (lights != null) foreach (var state in lights) if (state.light) state.light.enabled = state.enabled;
            RenderSettings.fog = fog;
            if (root) Object.Destroy(root);
            foreach (var material in materials) if (material) Object.Destroy(material);
            foreach (var texture in textures) if (texture) Object.Destroy(texture);
            materials.Clear(); textures.Clear();
            root = null;
        }
    }
}
