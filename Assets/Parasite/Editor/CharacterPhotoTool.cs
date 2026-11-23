using System.Collections.Generic;
using System.IO;
using Parasite.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using L = Parasite.World.CityLayout;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Renders every character variant (built through the real HumanoidRig, posed by the humanoid Animator if present)
    /// to Logs/characters: per character front | three-quarter | face close-up (studio light), lineups per profession,
    /// and a few shots standing in the generated City (real scene lighting, edit mode).
    /// Batch: Tools/unity.sh chars-photos -executeMethod Parasite.EditorTools.CharacterPhotoTool.CaptureBatch
    /// Optional: -charFilter &lt;substring&gt; (ids), -charNoCity.
    /// </summary>
    public static class CharacterPhotoTool
    {
        static string outDir;

        [MenuItem("Parasite/Characters/Capture Character Photos")]
        public static void Capture()
        {
            var args = System.Environment.GetCommandLineArgs();
            int fi = System.Array.IndexOf(args, "-charFilter");
            string filter = fi >= 0 && fi + 1 < args.Length ? args[fi + 1] : null;
            bool city = System.Array.IndexOf(args, "-charNoCity") < 0;
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "characters"));
            Directory.CreateDirectory(outDir);
            var catalog = Resources.Load<CharacterCatalog>(CharacterCatalog.Resource);
            if (!catalog || catalog.entries.Count == 0) throw new System.InvalidOperationException("Character catalog missing: run CharacterModelImporter.RebuildBatch first.");
            var controller = Resources.Load<RuntimeAnimatorController>(HumanAnimParams.ControllerResource);
            Debug.Log($"[CharPhoto] {catalog.entries.Count} variants, controller {(controller ? controller.name : "MISSING (procedural fallback)")}, GPU {SystemInfo.graphicsDeviceName}");

            // ---------------- studio
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.56f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.37f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.17f, 0.16f);
            RenderSettings.fog = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.35f; key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.45f; fill.color = new Color(0.75f, 0.82f, 1f);
            fill.transform.rotation = Quaternion.Euler(20f, -140f, 0f);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = 0.7f; rim.color = new Color(1f, 0.95f, 0.9f);
            rim.transform.rotation = Quaternion.Euler(25f, 10f, 0f);
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.SetColor("_BaseColor", new Color(0.32f, 0.32f, 0.33f)); floorMat.SetFloat("_Smoothness", 0.15f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(8, 1, 8);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            var backMat = new Material(floorMat);
            backMat.SetColor("_BaseColor", new Color(0.55f, 0.58f, 0.62f));
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.transform.SetPositionAndRotation(new Vector3(0, 5, -6), Quaternion.Euler(0, 180, 0));
            back.transform.localScale = new Vector3(80, 12, 1);
            back.GetComponent<Renderer>().sharedMaterial = backMat;

            var cam = MakeCamera(out var rt, out var tex, 600, 800);
            var groups = new Dictionary<Profession, List<HumanoidRig>>();
            var all = new List<HumanoidRig>();
            foreach (var e in catalog.entries)
            {
                if (e == null || !e.prefab || (filter != null && !e.id.Contains(filter))) continue;
                var rig = Spawn(e, Vector3.zero, 180f);
                Pose(rig, 1.2f);
                Report(rig, e);
                var shots = new List<Texture2D>();
                float h = e.height;
                // front, three-quarter, face
                shots.Add(Shoot(cam, rt, new Vector3(0, h * 0.55f, -3.3f) + Vector3.zero, new Vector3(0, h * 0.5f, 0), 38f));
                shots.Add(Shoot(cam, rt, new Vector3(-2.1f, h * 0.62f, -2.6f), new Vector3(0, h * 0.5f, 0), 38f));
                var head = rig.Head ? rig.Head.position : new Vector3(0, h - 0.12f, 0);
                var faceTarget = head + Vector3.up * 0.06f;
                shots.Add(Shoot(cam, rt, faceTarget + new Vector3(-0.22f, 0.02f, -0.72f), faceTarget, 30f));
                SaveStrip(shots, e.id + ".png");
                foreach (var s in shots) Object.DestroyImmediate(s);
                if (!groups.TryGetValue(e.profession, out var list)) groups[e.profession] = list = new List<HumanoidRig>();
                list.Add(rig);
                all.Add(rig);
                rig.gameObject.SetActive(false);
            }
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(rt);

            // lineups (wide)
            var wide = MakeCamera(out var wrt, out var wtex, 2400, 1000);
            foreach (var kv in groups)
            {
                var list = kv.Value;
                float spacing = 0.95f;
                float x0 = -(list.Count - 1) * spacing * 0.5f;
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].gameObject.SetActive(true);
                    list[i].transform.SetPositionAndRotation(new Vector3(x0 + i * spacing, 0, 0), Quaternion.Euler(0, 180, 0));
                    Pose(list[i], 0.1f);
                }
                float width = Mathf.Max(3f, list.Count * spacing + 0.8f);
                float dist = width * 0.5f / Mathf.Tan(Mathf.Deg2Rad * 18f) * 0.45f + 2.2f;
                var t1 = Shoot(wide, wrt, new Vector3(0, 1.0f, -dist), new Vector3(0, 0.9f, 0), 30f);
                SaveTex(t1, $"lineup_{kv.Key}_front.png"); Object.DestroyImmediate(t1);
                var t2 = Shoot(wide, wrt, new Vector3(-dist * 0.55f, 1.3f, -dist * 0.85f), new Vector3(0, 0.9f, 0), 30f);
                SaveTex(t2, $"lineup_{kv.Key}_34.png"); Object.DestroyImmediate(t2);
                foreach (var r in list) r.gameObject.SetActive(false);
            }
            Object.DestroyImmediate(wide.gameObject);
            Object.DestroyImmediate(wrt);
            foreach (var r in all) if (r) Object.DestroyImmediate(r.gameObject);

            CapturePoses(catalog, filter);
            if (city) CaptureCity(catalog, filter);
        }

        /// <summary>Animation states through the real HumanoidRig.Animate path (Animator or procedural fallback).</summary>
        static void CapturePoses(CharacterCatalog catalog, string filter)
        {
            var ids = new[] { "DrPetrov", "GuardMarcus", "LakeGirl", "MaintGus" };
            (string name, Vector3 vel, HostState st, ActorPose pose, float secs, bool grounded)[] states =
            {
                ("idle", Vector3.zero, HostState.Normal, ActorPose.None, 1.5f, true),
                ("walk", Vector3.forward * HumanAnimParams.WalkClipSpeed, HostState.Normal, ActorPose.None, 1.3f, true),
                ("run", Vector3.forward * HumanAnimParams.RunClipSpeed, HostState.Normal, ActorPose.None, 1.1f, true),
                ("possessed", Vector3.zero, HostState.Possessed, ActorPose.None, 1.0f, true),
                ("airborne", Vector3.forward * 2f, HostState.Possessed, ActorPose.None, 0.6f, false),
                ("sit", Vector3.zero, HostState.Normal, ActorPose.Sit, 3.0f, true),
                ("point", Vector3.zero, HostState.Normal, ActorPose.Point, 2.0f, true),
                ("unconscious", Vector3.zero, HostState.Unconscious, ActorPose.None, 3.0f, true),
                ("captured", Vector3.zero, HostState.Captured, ActorPose.None, 2.0f, true),
            };
            var cam = MakeCamera(out var rt, out var tex, 420, 640);
            foreach (var id in ids)
            {
                if (filter != null && !id.Contains(filter)) continue;
                var e = catalog.FindById(id);
                if (e == null) continue;
                var shots = new List<Texture2D>();
                foreach (var s in states)
                {
                    var rig = Spawn(e, Vector3.zero, 150f);
                    rig.SetPose(s.pose);
                    if (s.st == HostState.Possessed) rig.SetEyesGlow(true);
                    for (float t = 0; t < s.secs; t += 0.1f)
                    {
                        rig.Animate(rig.transform.rotation * s.vel, s.grounded, s.st, 5f, 0.1f);
                        if (rig.UsesAnimator && rig.Animator) rig.Animator.Update(0.1f);
                    }
                    float h = e.height;
                    shots.Add(Shoot(cam, rt, new Vector3(-0.9f, h * 0.62f, -3.0f), new Vector3(0, h * 0.45f, 0), 40f));
                    Object.DestroyImmediate(rig.gameObject);
                }
                SaveStrip(shots, "poses_" + id + ".png");
                foreach (var t in shots) Object.DestroyImmediate(t);
            }
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(rt);
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ city

        static void CaptureCity(CharacterCatalog catalog, string filter)
        {
            EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
            var lamps = Object.FindObjectsByType<FX.LampLights>(FindObjectsSortMode.None);
            FX.LampLights.Budget = 32; FX.LampLights.MaxDistance = 70f;
            var cam = MakeCamera(out var rt, out var tex, 1600, 900);
            cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rigs = new List<HumanoidRig>();
            var spot = L.PlazaSpot;
            int i = 0;
            foreach (var e in catalog.entries)
            {
                if (e == null || !e.prefab || (filter != null && !e.id.Contains(filter))) continue;
                int row = i / 8, col = i % 8;
                var pos = spot + new Vector3(-3.5f + col * 1.0f + (row % 2) * 0.5f, 0, 1.6f * row);
                if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hit, 6f)) pos.y = hit.point.y;
                var rig = Spawn(e, pos, 180f + (col - 4) * 6f);
                Pose(rig, 0.4f + i * 0.13f);
                rigs.Add(rig);
                i++;
            }
            void Shot(string name, Vector3 p, Vector3 look, float fov)
            {
                foreach (var ll in lamps) ll.Select(p, instant: true);
                var t = Shoot(cam, rt, p, look, fov);
                SaveTex(t, name);
                Object.DestroyImmediate(t);
            }
            Shot("city_plaza_group.png", spot + new Vector3(0.2f, 1.7f, -6.5f), spot + new Vector3(0, 1.0f, 1.2f), 50f);
            Shot("city_plaza_close.png", spot + new Vector3(-1.6f, 1.6f, -2.6f), spot + new Vector3(-2.0f, 1.25f, 0f), 45f);
            Shot("city_plaza_thirdperson.png", spot + new Vector3(2.5f, 2.3f, -4.2f), spot + new Vector3(0.5f, 1.2f, 1.5f), 60f);
            foreach (var r in rigs) if (r) Object.DestroyImmediate(r.gameObject);
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(rt);
        }

        // ------------------------------------------------------------------ helpers

        static HumanoidRig Spawn(CharacterCatalog.Entry e, Vector3 pos, float yaw)
        {
            var go = new GameObject("Char_" + e.id);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var rig = go.AddComponent<HumanoidRig>();
            rig.VariantOverride = e.id;
            rig.Build(e.profession, 7, e.id == "DrVale");
            return rig;
        }

        static void Pose(HumanoidRig rig, float seconds)
        {
            var an = rig.Animator;
            if (rig.UsesAnimator && an)
            {
                an.Rebind();
                an.Update(0f);
                for (float t = 0; t < seconds; t += 0.1f) an.Update(0.1f);
            }
            else
            {
                for (int k = 0; k < 30; k++) rig.Animate(Vector3.zero, true, HostState.Normal, 4.6f, 0.05f);
            }
        }

        static void Report(HumanoidRig rig, CharacterCatalog.Entry e)
        {
            int mats = 0;
            var lods = new List<string>();
            foreach (var r in rig.Renderers)
            {
                var smr = r as SkinnedMeshRenderer;
                if (!smr || !smr.sharedMesh) continue;
                long tris = 0;
                for (int s = 0; s < smr.sharedMesh.subMeshCount; s++) tris += smr.sharedMesh.GetIndexCount(s) / 3;
                lods.Add($"{smr.name}:{tris}");
                mats = Mathf.Max(mats, smr.sharedMaterials.Length);
            }
            var an = rig.Animator;
            Debug.Log($"[CharPhoto] {e.id} ({e.profession}) variant={rig.VariantId} human={(an && an.isHuman)} animator={rig.UsesAnimator} head={(rig.Head ? rig.Head.name : "none")} mats={mats} {string.Join(" ", lods)}");
        }

        static Camera MakeCamera(out RenderTexture rt, out Texture2D tex, int w, int h)
        {
            var go = new GameObject("CharPhotoCam");
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.5f, 0.53f, 0.57f);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            tex = null;
            cam.targetTexture = rt;
            return cam;
        }

        static Texture2D Shoot(Camera cam, RenderTexture rt, Vector3 pos, Vector3 look, float fov)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            cam.fieldOfView = fov;
            cam.targetTexture = rt;
            for (int i = 0; i < 2; i++)
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
            }
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return tex;
        }

        static void SaveStrip(List<Texture2D> shots, string name)
        {
            int w = 0, h = 0;
            foreach (var s in shots) { w += s.width; h = Mathf.Max(h, s.height); }
            var strip = new Texture2D(w, h, TextureFormat.RGB24, false);
            int x = 0;
            foreach (var s in shots) { strip.SetPixels(x, 0, s.width, s.height, s.GetPixels()); x += s.width; }
            strip.Apply();
            SaveTex(strip, name);
            Object.DestroyImmediate(strip);
        }

        static void SaveTex(Texture2D t, string name)
        {
            File.WriteAllBytes(Path.Combine(outDir, name), t.EncodeToPNG());
            Debug.Log("[CharPhoto] wrote Logs/characters/" + name);
        }
    }
}
