using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Parasite.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Renders contact sheets of the humanoid clips (Art/Characters/Animations) on the preview mannequins and on any
    /// humanoid character models found in Art/Characters/Models or Resources/Characters, plus a scripted run of the
    /// shared AnimatorController, to Logs/animation/*.png, and logs numeric checks ([AnimCheck] lines).
    /// Batch: Tools/unity.sh anim-preview -executeMethod Parasite.EditorTools.AnimationPreviewTool.CaptureBatch
    ///   [-animClips Walk,Run] [-animModels substring] [-animNoScenario] [-animNoClips] [-animCols 8]
    /// Sheet layout: one row per (model, view) — views: right side, front, front-left three-quarter; columns are evenly
    /// spaced normalized times (first == 0, last == 1). A red bar on the floor marks the character's +Z (forward).
    /// </summary>
    public static class AnimationPreviewTool
    {
        const int TileW = 240, TileH = 300;
        static Material groundMat, bodyMat, markMat;
        static readonly StringBuilder Summary = new();

        class Model
        {
            public string label;
            public GameObject go;
            public Animator animator;
            public float humanScale;
            public List<(SkinnedMeshRenderer smr, MeshFilter mf, Mesh mesh)> proxies = new();

            /// <summary>Edit-mode batch rendering does not refresh skinning after a manual graph evaluation, so every
            /// skinned mesh is baked into a static proxy mesh before each render.</summary>
            public void Bake()
            {
                foreach (var (smr, mf, mesh) in proxies)
                {
                    smr.BakeMesh(mesh, true);
                    mf.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    mesh.RecalculateBounds();
                }
            }
        }

        [MenuItem("Parasite/Animation/Capture Animation Previews")]
        public static void Capture()
        {
            var args = System.Environment.GetCommandLineArgs();
            string Arg(string k) { int i = System.Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            bool Has(string k) => System.Array.IndexOf(args, k) >= 0;
            var clipFilter = Arg("-animClips")?.Split(',').Where(s => s.Length > 0).ToHashSet();
            string modelFilter = Arg("-animModels");
            int cols = int.TryParse(Arg("-animCols"), out var cc) ? cc : 8;
            Summary.Clear();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "animation"));
            Directory.CreateDirectory(outDir);
            SetupStage(out var cam, out var rt);
            var models = LoadModels(modelFilter);
            if (models.Count == 0) throw new System.Exception("[Anim] no humanoid preview models found");
            foreach (var m in models) Log($"model {m.label}: humanScale {m.humanScale:0.000}");

            if (!Has("-animNoClips"))
            {
                foreach (var path in Directory.GetFiles(HumanAnimationImporter.Folder, "*.fbx").OrderBy(p => p))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (clipFilter != null && !clipFilter.Contains(name)) continue;
                    var clip = HumanAnimatorBuilder.LoadClip(name);
                    if (!clip) continue;
                    ClipSheet(clip, models, cam, rt, cols, outDir);
                }
            }
            if (!Has("-animNoScenario"))
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanAnimatorBuilder.ControllerPath);
                if (ctrl) Scenario(ctrl, models[0], cam, rt, outDir);
                else Debug.LogWarning("[Anim] controller missing; scenario skipped");
            }
            File.WriteAllText(Path.Combine(outDir, "checks.txt"), Summary.ToString());
            foreach (var m in models) Object.DestroyImmediate(m.go);
            Object.DestroyImmediate(rt);
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Log(string s) { Debug.Log("[AnimCheck] " + s); Summary.AppendLine(s); }

        // ------------------------------------------------------------------ stage
        static void SetupStage(out Camera cam, out RenderTexture rt)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var tex = new Texture2D(512, 512, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                {
                    bool line = x % 256 < 4 || y % 256 < 4;
                    bool checker = ((x / 256) + (y / 256)) % 2 == 0;
                    float v = line ? 0.18f : checker ? 0.55f : 0.47f;
                    tex.SetPixel(x, y, new Color(v, v * 1.0f, v * 0.96f));
                }
            tex.Apply();
            groundMat = new Material(shader) { mainTexture = tex };
            groundMat.SetTexture("_BaseMap", tex);
            groundMat.SetTextureScale("_BaseMap", new Vector2(10, 10));
            groundMat.SetFloat("_Smoothness", 0.1f);
            bodyMat = new Material(shader);
            bodyMat.SetColor("_BaseColor", new Color(0.78f, 0.68f, 0.55f));
            bodyMat.SetFloat("_Smoothness", 0.35f);
            markMat = new Material(shader);
            markMat.SetColor("_BaseColor", new Color(0.85f, 0.1f, 0.1f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.localScale = new Vector3(2, 1, 2); // 20 m, 0.5 m checker
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "ForwardMark"; mark.transform.position = new Vector3(0, 0.003f, 0.35f); mark.transform.localScale = new Vector3(0.03f, 0.005f, 0.7f);
            mark.GetComponent<Renderer>().sharedMaterial = markMat;
            Object.DestroyImmediate(mark.GetComponent<Collider>());

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.6f; light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

            var camGo = new GameObject("PreviewCam");
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 32f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.20f, 0.22f, 0.27f);
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.renderShadows = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            rt = new RenderTexture(TileW, TileH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
        }

        static List<Model> LoadModels(string filter)
        {
            var paths = new List<string>();
            if (Directory.Exists(HumanAnimationImporter.PreviewFolder))
                paths.AddRange(Directory.GetFiles(HumanAnimationImporter.PreviewFolder, "*.fbx").OrderBy(p => p));
            foreach (var dir in new[] { "Assets/Parasite/Art/Characters/Models", "Assets/Parasite/Resources/Characters" })
                if (Directory.Exists(dir))
                    paths.AddRange(Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                        .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) || p.EndsWith(".prefab"))
                        .OrderBy(p => p));
            var list = new List<Model>();
            int x = 0;
            foreach (var raw in paths)
            {
                string p = raw.Replace('\\', '/');
                if (filter != null && !p.Contains(filter) && !p.Contains("/Preview/")) continue;
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!src) continue;
                var srcAnim = src.GetComponentInChildren<Animator>();
                Avatar av = srcAnim ? srcAnim.avatar : null;
                if (!av) av = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().FirstOrDefault();
                if (!av || !av.isHuman || !av.isValid) continue;
                if (!src.GetComponentInChildren<Renderer>()) continue;
                var go = (GameObject)Object.Instantiate(src);
                go.name = Path.GetFileNameWithoutExtension(p);
                go.transform.position = new Vector3(100 + 10 * x++, 0, 0); // parked away from the stage
                var anim = go.GetComponentInChildren<Animator>();
                if (!anim) anim = go.AddComponent<Animator>();
                anim.avatar = av;
                anim.applyRootMotion = false;
                anim.runtimeAnimatorController = null;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                bool preview = p.Contains("/Preview/");
                var model = new Model { label = go.name, go = go, animator = anim, humanScale = anim.humanScale };
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (preview) r.sharedMaterials = Enumerable.Repeat(bodyMat, r.sharedMaterials.Length).ToArray();
                    if (r is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = true;
                        var proxy = new GameObject(smr.name + "_baked");
                        proxy.transform.SetParent(go.transform, true);
                        var mf = proxy.AddComponent<MeshFilter>();
                        var mr = proxy.AddComponent<MeshRenderer>();
                        mr.sharedMaterials = smr.sharedMaterials;
                        var mesh = new Mesh { name = smr.name + "_baked" };
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        mf.sharedMesh = mesh;
                        smr.forceRenderingOff = true;
                        model.proxies.Add((smr, mf, mesh));
                    }
                }
                list.Add(model);
                if (list.Count >= 4) break;
            }
            return list;
        }

        // ------------------------------------------------------------------ rendering
        static readonly (string name, Vector3 dir)[] Views =
        {
            ("side", new Vector3(1, 0.12f, 0)), ("front", new Vector3(0.18f, 0.12f, 1)), ("q", new Vector3(-0.75f, 0.32f, 0.75f)),
        };

        static Texture2D Grab(Camera cam, RenderTexture rt)
        {
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = null;
            return t;
        }

        static void Place(Camera cam, Bounds b, Vector3 dir)
        {
            float radius = Mathf.Max(0.75f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.95f;
            var c = b.center;
            cam.transform.position = c + dir.normalized * dist;
            cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position);
        }

        static void Blit(Texture2D sheet, Texture2D tile, int col, int row, int rows)
        {
            sheet.SetPixels(col * TileW, (rows - 1 - row) * TileH, TileW, TileH, tile.GetPixels());
            Object.DestroyImmediate(tile);
        }

        static Transform Bone(Model m, HumanBodyBones b) => m.animator.GetBoneTransform(b);

        // ------------------------------------------------------------------ clip sheets + checks
        static void ClipSheet(AnimationClip clip, List<Model> models, Camera cam, RenderTexture rt, int cols, string outDir)
        {
            int rows = models.Count * Views.Length;
            var sheet = new Texture2D(cols * TileW, rows * TileH, TextureFormat.RGB24, false);
            for (int mi = 0; mi < models.Count; mi++)
            {
                var m = models[mi];
                var root = m.go.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var graph = PlayableGraph.Create("AnimPreview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", m.animator);
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(true);
                output.SetSourcePlayable(cp);
                void PoseAt(float t) { cp.SetTime(t); graph.Evaluate(0f); }

                // numeric pass
                int n = 60;
                var b = new Bounds();
                bool init = false;
                float minFoot = 9, maxHips = -9, minHips = 9;
                for (int i = 0; i <= n; i++)
                {
                    PoseAt(clip.length * i / n);
                    foreach (var hb in new[] { HumanBodyBones.Head, HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                                 HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
                    {
                        var tr = Bone(m, hb);
                        if (!tr) continue;
                        if (!init) { b = new Bounds(tr.position, Vector3.zero); init = true; }
                        b.Encapsulate(tr.position);
                    }
                    var hips = Bone(m, HumanBodyBones.Hips).position;
                    minHips = Mathf.Min(minHips, hips.y); maxHips = Mathf.Max(maxHips, hips.y);
                    minFoot = Mathf.Min(minFoot, Bone(m, HumanBodyBones.LeftFoot).position.y, Bone(m, HumanBodyBones.RightFoot).position.y);
                }
                b.Encapsulate(new Vector3(b.center.x, 0, b.center.z));
                b.Expand(new Vector3(0.25f, 0.15f, 0.25f));
                PoseAt(0);
                var head0 = Bone(m, HumanBodyBones.Head).position; var hips0 = Bone(m, HumanBodyBones.Hips).position;
                PoseAt(clip.length);
                var head1 = Bone(m, HumanBodyBones.Head).position; var hips1 = Bone(m, HumanBodyBones.Hips).position;
                string extra = "";
                if (clip.name == "Walk" || clip.name == "Run" || clip.name.StartsWith("Walk_") || clip.name.StartsWith("Run_"))
                    extra = " " + MeasureStride(m, clip, PoseAt);
                if (clip.name == "Sit_Loop")
                {
                    PoseAt(0);
                    float thighY = 0, thighZ = 0;
                    foreach (var (u, l) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg) })
                    {
                        var mid = (Bone(m, u).position + Bone(m, l).position) * 0.5f;
                        thighY += mid.y * 0.5f; thighZ += mid.z * 0.5f;
                    }
                    extra = $" seat: thigh-bone midpoint y {thighY:0.000} z {thighZ:+0.00;-0.00} (seat surface ~ y-0.08)";
                }
                Log($"{clip.name,-18} on {m.label,-16} len {clip.length:0.00}s hips y [{minHips:0.00},{maxHips:0.00}] minFootY {minFoot:0.000} " +
                    $"start hips {hips0.x:+0.00;-0.00},{hips0.y:0.00},{hips0.z:+0.00;-0.00} head {head0.x:+0.00;-0.00},{head0.y:0.00},{head0.z:+0.00;-0.00} " +
                    $"end hips {hips1.x:+0.00;-0.00},{hips1.y:0.00},{hips1.z:+0.00;-0.00} head {head1.x:+0.00;-0.00},{head1.y:0.00},{head1.z:+0.00;-0.00}{extra}");

                for (int v = 0; v < Views.Length; v++)
                {
                    Place(cam, b, Views[v].dir);
                    for (int i = 0; i < cols; i++)
                    {
                        PoseAt(clip.length * i / Mathf.Max(1, cols - 1));
                        m.Bake();
                        Blit(sheet, Grab(cam, rt), i, mi * Views.Length + v, rows);
                    }
                }
                graph.Destroy();
                root.position = new Vector3(100 + 10 * mi, 0, 0);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, clip.name + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        /// <summary>Stance-foot backward speed (m/s) = the clip's natural ground speed on this avatar.</summary>
        static string MeasureStride(Model m, AnimationClip clip, System.Action<float> poseAt)
        {
            int n = 240;
            float dt = clip.length / n;
            var speeds = new List<float>();
            foreach (var fb in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                var ys = new float[2 * n]; var zs = new float[2 * n];
                for (int i = 0; i < 2 * n; i++) { poseAt((i % n) * dt); var p = Bone(m, fb).position; ys[i] = p.y; zs[i] = p.z; }
                float minY = ys.Min();
                // longest contiguous low run (cyclic)
                int bestStart = -1, bestLen = 0;
                for (int i = 0; i < n; i++)
                {
                    if (ys[i] >= minY + 0.025f || (i > 0 && ys[i - 1] < minY + 0.025f) || (i == 0 && ys[n - 1] < minY + 0.025f)) continue;
                    int len = 0;
                    while (len < n && ys[(i + len) % n] < minY + 0.025f) len++;
                    if (len > bestLen) { bestLen = len; bestStart = i; }
                }
                if (bestLen < 6) continue;
                int a = bestStart + bestLen / 4, b = bestStart + bestLen * 3 / 4;
                float za = Sample(zs, a, n), zb = Sample(zs, b, n);
                speeds.Add(-(zb - za) / ((b - a) * dt));
            }
            if (speeds.Count == 0) return "stance n/a";
            float med = speeds.Average();
            float expected = (clip.name.StartsWith("Run") ? HumanAnimParams.RunClipSpeed : HumanAnimParams.WalkClipSpeed) *
                             m.humanScale / HumanAnimParams.ReferenceHumanScale;
            return $"stanceSpeed {med:0.00} m/s (expected {expected:0.00}; L/R {string.Join("/", speeds.Select(x => x.ToString("0.00")))})";
        }

        static float Sample(float[] zs, int i, int n)
        {
            // z of a cyclic in-place clip: positions repeat every n samples (feet move back during stance only)
            return zs[i % n];
        }

        // ------------------------------------------------------------------ controller scenario
        struct Step { public float t; public System.Action<AnimatorControllerPlayable> set; public string expect; public string label; }

        static void Scenario(AnimatorController ctrl, Model m, Camera cam, RenderTexture rt, string outDir)
        {
            var root = m.go.transform;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var graph = PlayableGraph.Create("AnimScenario");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "out", m.animator);
            var acp = AnimatorControllerPlayable.Create(graph, ctrl);
            output.SetSourcePlayable(acp);
            float hs = m.humanScale;
            void Move(AnimatorControllerPlayable p, float speed)
            {
                p.SetFloat(HumanAnimParams.Speed, speed);
                p.SetFloat(HumanAnimParams.LocomotionRate, HumanAnimParams.LocomotionRateFor(speed, hs));
            }
            var steps = new List<Step>
            {
                new() { t = 0.0f, label = "idle civilian", expect = "Idle_Civilian", set = p => { Move(p, 0); p.SetFloat(HumanAnimParams.Variant, 0.1f); } },
                new() { t = 1.5f, label = "walk 1.9", expect = "Move", set = p => Move(p, 1.9f) },
                new() { t = 3.5f, label = "run 5.2", expect = "Move", set = p => Move(p, 5.2f) },
                new() { t = 5.0f, label = "stop", expect = "Idle_Civilian", set = p => Move(p, 0) },
                new() { t = 6.0f, label = "jump", expect = "Jump_Takeoff", set = p => p.SetBool(HumanAnimParams.Grounded, false) },
                new() { t = 6.9f, label = "land", expect = "Land", set = p => p.SetBool(HumanAnimParams.Grounded, true) },
                new() { t = 8.0f, label = "knocked out", expect = "Collapse", set = p => p.SetInteger(HumanAnimParams.State, 2) },
                new() { t = 11.5f, label = "lying", expect = "Unconscious_Loop", set = p => { } },
                new() { t = 13.0f, label = "recover", expect = "GetUp", set = p => p.SetInteger(HumanAnimParams.State, 0) },
                new() { t = 15.5f, label = "guard idle", expect = "Idle_Guard", set = p => p.SetInteger(HumanAnimParams.Profession, 1) },
                new() { t = 17.0f, label = "alert", expect = "Alert_Loop", set = p => p.SetInteger(HumanAnimParams.Pose, (int)ActorPose.Alert) },
                new() { t = 19.0f, label = "point", expect = "Point_Loop", set = p => p.SetInteger(HumanAnimParams.Pose, (int)ActorPose.Point) },
                new() { t = 21.0f, label = "pose none", expect = "Idle_Guard", set = p => p.SetInteger(HumanAnimParams.Pose, 0) },
                new() { t = 22.0f, label = "scientist sit", expect = "Sit_Enter", set = p => { p.SetInteger(HumanAnimParams.Profession, 2); p.SetInteger(HumanAnimParams.Pose, (int)ActorPose.Sit); } },
                new() { t = 24.5f, label = "seated", expect = "Sit_Loop", set = p => { } },
                new() { t = 26.0f, label = "stand", expect = "Sit_Exit", set = p => p.SetInteger(HumanAnimParams.Pose, 0) },
                new() { t = 28.0f, label = "talk + gesture", expect = "Talk_Loop", set = p => { p.SetInteger(HumanAnimParams.Pose, (int)ActorPose.Talk); p.SetTrigger(HumanAnimParams.Gesture); } },
                new() { t = 30.0f, label = "console", expect = "Console_Loop", set = p => p.SetInteger(HumanAnimParams.Pose, (int)ActorPose.UseConsole) },
                new() { t = 32.0f, label = "panic", expect = "Panic_Loop", set = p => p.SetInteger(HumanAnimParams.Pose, (int)ActorPose.Panic) },
                new() { t = 34.0f, label = "panic flee", expect = "Move_Panic", set = p => Move(p, 5.0f) },
                new() { t = 36.0f, label = "possessed idle", expect = "Idle_Possessed", set = p => { p.SetInteger(HumanAnimParams.Pose, 0); Move(p, 0); p.SetInteger(HumanAnimParams.State, 1); } },
                new() { t = 37.5f, label = "possessed walk", expect = "Move_Possessed", set = p => Move(p, 2.4f) },
                new() { t = 39.5f, label = "captured", expect = "Captured_Enter", set = p => { Move(p, 0); p.SetInteger(HumanAnimParams.State, 3); } },
                new() { t = 41.5f, label = "kneeling", expect = "Captured_Loop", set = p => { } },
                new() { t = 43.0f, label = "ejected", expect = "Captured_Collapse", set = p => p.SetInteger(HumanAnimParams.State, 2) },
                new() { t = 46.0f, label = "lying", expect = "Unconscious_Loop", set = p => { } },
                new() { t = 47.0f, label = "recover", expect = "GetUp", set = p => { p.SetInteger(HumanAnimParams.State, 0); p.SetInteger(HumanAnimParams.Profession, 3); } },
                new() { t = 50.0f, label = "maintenance idle", expect = "Idle_Maintenance", set = p => { } },
                new() { t = 51.5f, label = "end", expect = "Idle_Maintenance", set = p => { } },
            };
            var stateNames = CollectStateNames(ctrl);
            float dt = 1f / 30f, end = steps[^1].t;
            int frames = Mathf.CeilToInt(end / dt);
            int captureEvery = 9; // 0.3 s
            int cols = 12;
            var tiles = new List<Texture2D>();
            var log = new StringBuilder();
            int si = 0, fails = 0;
            var pending = new List<(Step s, float deadline)>();
            var b = new Bounds(new Vector3(0, 0.85f, -0.45f), new Vector3(1.4f, 1.9f, 2.4f));
            Place(cam, b, Views[2].dir);
            for (int f = 0; f <= frames; f++)
            {
                float t = f * dt;
                while (si < steps.Count && steps[si].t <= t + 1e-4f)
                {
                    steps[si].set(acp);
                    pending.Add((steps[si], steps[si].t + 0.6f));
                    si++;
                }
                graph.Evaluate(f == 0 ? 0f : dt);
                var info = acp.GetCurrentAnimatorStateInfo(0);
                string cur = stateNames.TryGetValue(info.fullPathHash, out var nm) ? nm : info.fullPathHash.ToString();
                bool inTrans = acp.IsInTransition(0);
                string next = inTrans && stateNames.TryGetValue(acp.GetNextAnimatorStateInfo(0).fullPathHash, out var nn) ? nn : "";
                for (int k = pending.Count - 1; k >= 0; k--)
                {
                    var (s, deadline) = pending[k];
                    if (cur == s.expect || next == s.expect)
                    {
                        log.AppendLine($"  ok   {s.t,5:0.0}s {s.label,-18} -> {s.expect} after {t - s.t:0.00}s");
                        pending.RemoveAt(k);
                    }
                    else if (t > deadline)
                    {
                        log.AppendLine($"  FAIL {s.t,5:0.0}s {s.label,-18} expected {s.expect}, got {cur}{(inTrans ? " -> " + next : "")}");
                        fails++; pending.RemoveAt(k);
                    }
                }
                if (f % captureEvery == 0) { m.Bake(); tiles.Add(Grab(cam, rt)); }
                if (f == 36) // 1.2 s into the civilian idle: arms must hang below the chest (no layer overriding them)
                {
                    float lh = Bone(m, HumanBodyBones.LeftHand).position.y, rh = Bone(m, HumanBodyBones.RightHand).position.y;
                    float hips = Bone(m, HumanBodyBones.Hips).position.y;
                    bool ok = lh < hips + 0.15f && rh < hips + 0.15f;
                    if (!ok) fails++;
                    log.AppendLine($"  {(ok ? "ok  " : "FAIL")}  1.2s idle hands y L {lh:0.00} R {rh:0.00} (hips {hips:0.00})");
                }
            }
            graph.Destroy();
            Log($"scenario on {m.label}: {steps.Count - fails}/{steps.Count} expected states reached{(fails == 0 ? "" : " — FAILURES")}\n{log}");
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * TileW, rows * TileH, TextureFormat.RGB24, false);
            for (int i = 0; i < tiles.Count; i++) Blit(sheet, tiles[i], i % cols, i / cols, rows);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "_scenario.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            root.position = new Vector3(100, 0, 0);
        }

        static Dictionary<int, string> CollectStateNames(AnimatorController c)
        {
            var d = new Dictionary<int, string>();
            foreach (var l in c.layers)
                foreach (var s in l.stateMachine.states)
                    d[Animator.StringToHash(l.name + "." + s.state.name)] = s.state.name;
            return d;
        }
    }
}
