using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Renders the set pieces of THE TAIL, HUNTER PROTOCOL and BLACKOUT from City.unity (edit mode) to
    /// Logs/photos/missions_*.png. Batch: -executeMethod Parasite.EditorTools.MissionPhotoTool.CaptureBatch
    /// </summary>
    public static class MissionPhotoTool
    {
        public static readonly (string name, Vector3 pos, Vector3 lookAt)[] Shots =
        {
            ("missions_01_tail_bus_stop", new Vector3(-41f, 2.2f, -7f), new Vector3(-26f, 1.2f, -20.5f)),
            ("missions_02_tail_alley_mouth", new Vector3(-26f, 1.9f, -33f), new Vector3(-33f, 1.2f, -45f)),
            ("missions_03_dead_drop", new Vector3(-32.6f, 1.55f, -51.4f), new Vector3(-35.8f, 0.6f, -54.1f)),
            ("missions_04_scan_arch", new Vector3(12.2f, 1.7f, 87.2f), new Vector3(16.75f, 1.3f, 84f)),
            ("missions_05_security_office", new Vector3(13.2f, 1.8f, 82.6f), new Vector3(22.8f, 1.1f, 76f)),
            ("missions_06_transformer_p1", new Vector3(-52.8f, 1.7f, 15.2f), new Vector3(-57.5f, 1.1f, 18f)),
            ("missions_07_transformer_p2", new Vector3(-19.5f, 1.7f, -12.6f), new Vector3(-14.5f, 1.1f, -15f)),
            ("missions_08_transformer_p3", new Vector3(-46.5f, 1.7f, 31.2f), new Vector3(-44f, 1.1f, 36.5f)),
        };

        [MenuItem("Parasite/Debug/Capture Mission Photos")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single);
            int missing = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missing != 0) throw new System.InvalidOperationException($"City scene has {missing} missing scripts; regenerate the scenes first.");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "photos"));
            Directory.CreateDirectory(dir);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("MissionPhotoCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
            foreach (var (name, pos, look) in Shots)
            {
                go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
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
                Debug.Log("[Parasite] mission photo " + name);
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
