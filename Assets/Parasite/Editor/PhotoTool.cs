using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using L = Parasite.World.CityLayout;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Renders preset viewpoints of City.unity (edit mode, environment only) to Logs/photos/*.png.
    /// Batch: Unity -batchmode -projectPath . -executeMethod Parasite.EditorTools.PhotoTool.CaptureBatch -logFile -
    /// Optional: -photoFilter <substring> renders only matching shots.
    /// </summary>
    public static class PhotoTool
    {
        public static readonly (string name, Vector3 pos, Vector3 lookAt)[] Shots =
        {
            ("01_spawn_alley", L.PlayerSpawn + new Vector3(0, 1.8f, -4f), L.PlayerSpawn + new Vector3(0, 1.5f, 12f)),
            ("02_market_st", new Vector3(-20, 1.8f, -28), new Vector3(30, 4f, -28)),
            ("03_avenue_north", new Vector3(0, 1.8f, -40), new Vector3(0, 6f, 20)),
            ("04_plaza", new Vector3(-50, 2.2f, -5), new Vector3(-30, 2f, 12)),
            ("05_plaza_shops", new Vector3(-36, 1.8f, 0), new Vector3(-62, 4f, 5)),
            ("06_campus_rd", new Vector3(-40, 1.8f, 46), new Vector3(30, 4f, 46)),
            ("07_main_gate", L.MainGateOutside + new Vector3(-3, 1.8f, -9f), L.MainGateOutside + new Vector3(0, 3f, 6f)),
            ("08_staff_entrance", L.StaffTerminalPos + new Vector3(-6, 1.8f, -4), L.StaffTerminalPos + new Vector3(2, 1.2f, 0)),
            ("09_tunnel_alley", L.TunnelDoorOutside + new Vector3(3, 1.8f, -10), L.TunnelDoorOutside + new Vector3(-1, 1.5f, 0)),
            ("10_lab_lobby", L.MainGateInside + new Vector3(0, 1.7f, -2), L.MainGateInside + new Vector3(0, 1.5f, 10)),
            ("11_lab_corridor", L.LabCorridor + new Vector3(-12, 1.7f, 0), L.LabCorridor + new Vector3(10, 1.4f, 0)),
            ("12_vale_office", L.ValeOffice + new Vector3(0, 1.7f, -3), L.DrVale + new Vector3(0, 1.4f, 0)),
            ("13_tunnel", L.TunnelMid + new Vector3(8, 1.6f, 0), L.TunnelMid + new Vector3(-6, 1.2f, 0)),
            ("14_aerial", new Vector3(-80, 60, -110), new Vector3(0, 0, 20)),
            ("15_street_level_detail", new Vector3(-28, 1.6f, -18), new Vector3(-40, 3f, -17)),
        };

        static readonly (string name, Vector3 at, Vector3 cam)[] AmoebaShots =
        {
            // (start of the slide, camera); the blob slides ~3.5 m towards +z, leaving its trail behind
            ("16_amoeba_alley", L.PlayerSpawn + new Vector3(0.4f, 0f, 7f), L.PlayerSpawn + new Vector3(1.4f, 1.05f, 11.6f)),
            ("17_amoeba_street", new Vector3(-20.5f, 0f, -30.8f), new Vector3(-21.6f, 1.15f, -26.6f)),
        };

        [MenuItem("Parasite/Debug/Capture Photos")]
        public static void Capture()
        {
            var args = System.Environment.GetCommandLineArgs();
            int fi = System.Array.IndexOf(args, "-photoFilter");
            string filter = fi >= 0 && fi + 1 < args.Length ? args[fi + 1] : null;
            var scene = EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single);
            int missing = 0, meshCount = 0;
            long triangles = 0, lowerLods = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh) continue;
                    long tris = 0;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += mf.sharedMesh.GetIndexCount(s) / 3;
                    // LODGroup children beyond LOD0 never render together with LOD0: report them separately
                    if (mf.GetComponentInParent<LODGroup>() && System.Text.RegularExpressions.Regex.IsMatch(mf.name, "_LOD[1-9]$")) { lowerLods += tris; continue; }
                    meshCount++;
                    triangles += tris;
                }
            }
            Debug.Log($"[Parasite] Photo audit: {meshCount} mesh instances, {triangles} triangles (LOD0; +{lowerLods} in lower LODs), {missing} missing scripts; " +
                      $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                      $"GPU {SystemInfo.graphicsDeviceName}, pipeline {GraphicsSettings.currentRenderPipeline}.");
            if (missing != 0) throw new System.InvalidOperationException($"City scene has {missing} missing scripts; regenerate the scenes before capturing.");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "photos"));
            Directory.CreateDirectory(dir);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("PhotoCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.08f; cam.farClipPlane = 900f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, hideFlags = HideFlags.DontSave };
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
            foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
            // photos show the High preset's runtime lamp-light budget and light rain on the water
            var lamps = Object.FindObjectsByType<FX.LampLights>(FindObjectsSortMode.None);
            FX.LampLights.Budget = 32; FX.LampLights.MaxDistance = 70f;
            Shader.SetGlobalFloat("_ParasiteRain", 0.5f);
            Shader.DisableKeyword("_PARASITE_SSR_OFF");
            void Shoot(string name, Vector3 pos, Vector3 look)
            {
                if (!rt || !rt.IsCreated())
                {
                    rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, hideFlags = HideFlags.DontSave };
                    rt.Create();
                }
                if (!tex) tex = new Texture2D(1600, 900, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(look - pos);
                foreach (var ll in lamps) ll.Select(pos, instant: true);
                cam.targetTexture = rt;
                for (int i = 0; i < 2; i++)
                {
                    var req = new RenderPipeline.StandardRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                    else cam.Render();
                }
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Debug.Log("[Parasite] photo " + name);
            }
            foreach (var (name, pos, look) in Shots)
                if (filter == null || name.Contains(filter)) Shoot(name, pos, look);
            // free amoeba close-ups: a real AmoebaVisual stepped through a short slide in edit mode (trail, squash, pods)
            foreach (var (name, at, camPos) in AmoebaShots)
            {
                if (filter != null && !name.Contains(filter)) continue;
                var blob = new GameObject("PhotoAmoeba");
                int layer = LayerMask.NameToLayer("Amoeba");
                if (layer >= 0) blob.layer = layer;
                var vis = blob.AddComponent<FX.AmoebaVisual>();
                blob.transform.position = at - Vector3.forward * 1.8f;
                vis.Build(0.38f);
                float t = 20f, dt = 1f / 30f;
                for (int i = 0; i < 54; i++)
                {
                    float speed = i < 40 ? 2.4f : 0.6f;
                    blob.transform.position += Vector3.forward * (speed * dt);
                    vis.SetMotion(Vector3.forward * speed, true);
                    FX.SlimeTrail.ClockOverride = t;
                    vis.Step(dt, t);
                    t += dt;
                }
                FX.SlimeTrail.ClockOverride = -1f;
                Shoot(name, camPos, blob.transform.position + Vector3.up * 0.32f);
                Object.DestroyImmediate(blob);
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            // title-menu backdrop (Bootstrap scene), from its own main camera
            if (filter == null || "00_menu_backdrop".Contains(filter))
            {
                EditorSceneManager.OpenScene(CityBuilder.BootstrapPath, OpenSceneMode.Single);
                var main = Camera.main;
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
                go = new GameObject("PhotoCam");
                cam = go.AddComponent<Camera>();
                cam.fieldOfView = main ? main.fieldOfView : 55f; cam.nearClipPlane = 0.08f; cam.farClipPlane = 900f;
                data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.renderShadows = true;
                foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
                lamps = Object.FindObjectsByType<FX.LampLights>(FindObjectsSortMode.None);
                var mp = main ? main.transform.position : new Vector3(1.2f, 2.1f, -22f);
                Shoot("00_menu_backdrop", mp, mp + (main ? main.transform.forward : Vector3.forward) * 10f);
                cam.targetTexture = null;
                Object.DestroyImmediate(go);
            }
            if (rt) Object.DestroyImmediate(rt);
            if (tex) Object.DestroyImmediate(tex);
        }

        /// <summary>Regenerate both scenes, then capture (one Unity session instead of two queued batch runs).</summary>
        public static void BuildAndCaptureBatch()
        {
            try { CityBuilder.BuildAll(); Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
