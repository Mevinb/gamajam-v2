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

        [MenuItem("Parasite/Debug/Capture Photos")]
        public static void Capture()
        {
            var args = System.Environment.GetCommandLineArgs();
            int fi = System.Array.IndexOf(args, "-photoFilter");
            string filter = fi >= 0 && fi + 1 < args.Length ? args[fi + 1] : null;
            var scene = EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single);
            int missing = 0, meshCount = 0;
            long triangles = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh) continue;
                    meshCount++;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        triangles += mf.sharedMesh.GetIndexCount(s) / 3;
                }
            }
            Debug.Log($"[Parasite] Photo audit: {meshCount} mesh instances, {triangles} triangles, {missing} missing scripts; " +
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
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
            foreach (var (name, pos, look) in Shots)
            {
                if (filter != null && !name.Contains(filter)) continue;
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(look - pos);
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
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
