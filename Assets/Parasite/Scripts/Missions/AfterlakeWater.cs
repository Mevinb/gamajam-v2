using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>Visual-only river, ferry berth and distant Blender banks; never contributes navigation colliders.</summary>
    public sealed class AfterlakeWater : MonoBehaviour
    {
        readonly List<Material> owned = new();
        Material river, berth;
        ReflectionProbe probe;
        RenderTexture reflectionTexture;
        Mesh surfaceMesh;
        float cleanWater;
        const int WaterLayer = 4;

        public static AfterlakeWater Create(Transform parent)
        {
            var go = new GameObject("Riverside water and horizon"); go.transform.SetParent(parent, false);
            var water = go.AddComponent<AfterlakeWater>();
            water.Build();
            return water;
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("Shaders/RiverWater");
            if (!shader) throw new System.InvalidOperationException("Riverside river water shader is missing.");
            river = new Material(shader) { name = "Riverside tidal water" }; owned.Add(river);
            river.SetFloat("_ShoreZ", -38.2f);
            cleanWater = RunState.Has(AfterlakeCampaign.Floodgate) ? 1f : 0f;
            river.SetFloat("_CleanWater", cleanWater);
            Surface("Tidal river", new Vector3(230, -0.3f, -87), new Vector3(380, 1, 106), river);

            // A real notch in the quay exposes the hull. The established deck connects by a sloped physical gangway.
            berth = new Material(river) { name = "South ferry berth water" }; owned.Add(berth);
            berth.SetFloat("_ShoreZ", -24.15f);
            Surface("Ferry berth water", new Vector3(270, -0.3f, -31), new Vector3(10, 1, 14), berth);

            var waterBounds = new List<Bounds>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>()) if (renderer.gameObject.layer == WaterLayer) waterBounds.Add(renderer.bounds);
            var cutout = gameObject.AddComponent<SkylineWaterCutout>();
            if (!cutout.ApplyToSkyline(waterBounds)) Debug.LogWarning("[AfterlakeWater] Distant ground left unchanged: " + cutout.LastFailure);

            var banks = Resources.Load<GameObject>("Riverside/RiversideBanks");
            if (banks)
            {
                var instance = Instantiate(banks, transform, false);
                instance.name = "Distant riverbanks (Blender)";
                instance.transform.localPosition = new Vector3(230, 0, -125);
                var surfaces = new Dictionary<string, Material>();
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        string key = materials[i] ? materials[i].name : "CinemaShore";
                        if (!surfaces.TryGetValue(key, out var material))
                        {
                            material = BankMaterial(key); surfaces.Add(key, material); owned.Add(material);
                        }
                        materials[i] = material;
                    }
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            var probeObject = new GameObject("Riverside local reflection probe"); probeObject.transform.SetParent(transform, false);
            probeObject.transform.localPosition = new Vector3(230, 3, -41);
            probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = GraphicsPreset.Current >= 2 ? 256 : 128;
            probe.size = new Vector3(430, 70, 260); probe.center = new Vector3(0, 12, -45);
            probe.blendDistance = 15; probe.importance = 3; probe.boxProjection = false;
            probe.hdr = true; probe.nearClipPlane = 0.15f; probe.farClipPlane = 220;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = ~((1 << WaterLayer) | (1 << 2) | (1 << 30));
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) probe.enabled = false;
        }

        void Surface(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name) { layer = WaterLayer }; go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            if (!surfaceMesh)
            {
                surfaceMesh = new Mesh { name = "River surface quad" };
                surfaceMesh.vertices = new[] { new Vector3(-.5f,0,-.5f), new Vector3(-.5f,0,.5f), new Vector3(.5f,0,.5f), new Vector3(.5f,0,-.5f) };
                surfaceMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                surfaceMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                surfaceMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                surfaceMesh.RecalculateBounds(); surfaceMesh.UploadMeshData(true);
            }
            var mesh = go.AddComponent<MeshFilter>(); mesh.sharedMesh = surfaceMesh;
            go.transform.localScale = new Vector3(size.x, 1, size.z);
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        static Material BankMaterial(string name)
        {
            bool reed = name.Contains("Reed");
            string texture = name.Contains("Stone") ? "Stone" : "Shore";
            var material = MatFactory.LitUnique(reed ? new Color(0.21f, 0.27f, 0.17f) : new Color(0.73f, 0.8f, 0.75f), reed ? 0.08f : 0.16f);
            material.name = name + " riverbank";
            if (reed) return material;
            var albedo = Resources.Load<Texture2D>(CinematicArt.ResourceRoot + "Textures/" + texture + "_A");
            var normal = Resources.Load<Texture2D>(CinematicArt.ResourceRoot + "Textures/" + texture + "_N");
            var mask = Resources.Load<Texture2D>(CinematicArt.ResourceRoot + "Textures/" + texture + "_M");
            if (albedo) material.SetTexture("_BaseMap", albedo);
            if (normal) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
            if (mask) { material.SetTexture("_MetallicGlossMap", mask); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            material.SetTextureScale("_BaseMap", new Vector2(6, 6));
            return material;
        }

        IEnumerator Start()
        {
            // Capture once after the district's buildings, equipment and NPCs exist; never recapture every frame.
            yield return null;
            if (!probe || !probe.enabled) yield break;
            reflectionTexture = new RenderTexture(probe.resolution, probe.resolution, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "Riverside local HDR reflection", dimension = TextureDimension.Cube,
                useMipMap = true, autoGenerateMips = false, hideFlags = HideFlags.DontSave,
            };
            reflectionTexture.Create();
            probe.realtimeTexture = reflectionTexture;
            probe.RenderProbe(reflectionTexture);
        }

        void Update()
        {
            float target = RunState.Has(AfterlakeCampaign.Floodgate) ? 1f : 0f;
            if (Mathf.Approximately(cleanWater, target)) return;
            cleanWater = Mathf.MoveTowards(cleanWater, target, Time.deltaTime * 0.12f);
            if (river) river.SetFloat("_CleanWater", cleanWater);
            if (berth) berth.SetFloat("_CleanWater", cleanWater);
        }

        void OnDestroy()
        {
            if (probe) { probe.enabled = false; probe.realtimeTexture = null; }
            if (reflectionTexture)
            {
                reflectionTexture.Release();
                if (Application.isPlaying) Destroy(reflectionTexture); else DestroyImmediate(reflectionTexture);
            }
            if (surfaceMesh) { if (Application.isPlaying) Destroy(surfaceMesh); else DestroyImmediate(surfaceMesh); }
            foreach (var material in owned)
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
