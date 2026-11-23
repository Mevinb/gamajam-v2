using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parasite.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Generates Resources/Characters/HumanLocomotion.controller (the shared humanoid AnimatorController) from the clips
    /// in Art/Characters/Animations. Reproducible; the controller file keeps its GUID across rebuilds.
    /// Batch: Tools/unity.sh anim-build -executeMethod Parasite.EditorTools.HumanAnimatorBuilder.BuildBatch
    ///
    /// Base layer (IK pass): Idle_{Civilian,Guard,Scientist,Maintenance,Possessed} (Variant-selected idle trees) and
    /// Move / Move_Possessed (1D Speed tree Walk/Run, speed multiplier LocomotionRate); air (Jump_Takeoff, Fall_Loop,
    /// Land); unconscious (Collapse, Unconscious_Loop, GetUp); captured (Captured_Enter, Captured_Loop,
    /// Captured_Collapse, Captured_Exit); held poses (Sit_Enter/Sit_Loop/Sit_Exit, Talk_Loop, Console_Loop, Panic_Loop,
    /// Alert_Loop, Point_Loop; Move_Panic = Walk/Panic_Flee_Loop while Pose==Panic and moving). "Back to locomotion"
    /// transitions go to the Exit node; the Entry node then picks the idle by State/Profession.
    /// Layer "Gesture" (additive, arms+head mask): one-shot gestures on the Gesture trigger; its default state is empty.
    /// </summary>
    public static class HumanAnimatorBuilder
    {
        public const string ControllerPath = "Assets/Parasite/Resources/Characters/HumanLocomotion.controller";
        const string UpperMaskPath = "Assets/Parasite/Resources/Characters/AnimUpperBodyMask.mask"; // obsolete, deleted on build
        public const string ArmsMaskPath = "Assets/Parasite/Resources/Characters/AnimArmsHeadMask.mask";
        const string TempPath = "Assets/Parasite/Resources/Characters/HumanLocomotion.build.controller";

        const string P_Speed = "Speed", P_Rate = "LocomotionRate", P_Grounded = "Grounded", P_State = "State",
            P_Pose = "Pose", P_Variant = "Variant", P_Profession = "Profession", P_Gesture = "Gesture";

        /// <summary>Clip used when a clip is missing (keeps the controller valid while clips are being authored).</summary>
        static readonly Dictionary<string, string> Fallback = new()
        {
            { "Idle_Shift", "Idle_Neutral" }, { "Idle_LookAround", "Idle_Neutral" }, { "Idle_Guard", "Idle_Neutral" },
            { "Idle_GuardBelt", "Idle_Neutral" }, { "Idle_Clipboard", "Idle_FoldArms" }, { "Idle_HandsOnHips", "Idle_Neutral" },
            { "Idle_Possessed", "Idle_Neutral" }, { "Walk_Possessed", "Walk" }, { "Run_Possessed", "Run" },
            { "Captured_Enter", "Idle_Neutral" }, { "Captured_Loop", "Idle_Neutral" }, { "Captured_Exit", "Idle_Neutral" },
            { "Captured_Collapse", "Collapse" }, { "Console_Loop", "Talk_Loop" }, { "Panic_Loop", "Idle_Neutral" },
            { "Panic_Flee_Loop", "Run" }, { "Alert_Loop", "Idle_Neutral" }, { "Point_Loop", "Talk_Loop" },
            { "Gesture_Explain", "Talk_Loop" }, { "Gesture_Shrug", "Talk_Loop" },
        };

        static readonly List<string> Missing = new();
        static readonly Dictionary<string, AnimationClip> Cache = new();

        [MenuItem("Parasite/Animation/Build Human Animator")]
        public static void Build()
        {
            Missing.Clear();
            Cache.Clear();
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            var arms = Mask(ArmsMaskPath, false);
            AssetDatabase.DeleteAsset(UpperMaskPath);

            AssetDatabase.DeleteAsset(TempPath);
            var c = AnimatorController.CreateAnimatorControllerAtPath(TempPath);
            c.AddParameter(new AnimatorControllerParameter { name = P_Speed, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            c.AddParameter(new AnimatorControllerParameter { name = P_Rate, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            c.AddParameter(new AnimatorControllerParameter { name = P_Grounded, type = AnimatorControllerParameterType.Bool, defaultBool = true });
            c.AddParameter(new AnimatorControllerParameter { name = P_State, type = AnimatorControllerParameterType.Int, defaultInt = 0 });
            c.AddParameter(new AnimatorControllerParameter { name = P_Pose, type = AnimatorControllerParameterType.Int, defaultInt = 0 });
            c.AddParameter(new AnimatorControllerParameter { name = P_Variant, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            c.AddParameter(new AnimatorControllerParameter { name = P_Profession, type = AnimatorControllerParameterType.Int, defaultInt = 0 });
            c.AddParameter(new AnimatorControllerParameter { name = P_Gesture, type = AnimatorControllerParameterType.Trigger });

            var layers = c.layers;
            layers[0].name = "Base Layer";
            layers[0].iKPass = true;
            c.layers = layers;
            BuildBase(c, c.layers[0].stateMachine);
            BuildGesture(c, arms);

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            // Overwrite the real controller file so its GUID (and .meta) stay stable across rebuilds.
            string full = Path.GetFullPath(ControllerPath), temp = Path.GetFullPath(TempPath);
            File.Copy(temp, full, true);
            AssetDatabase.DeleteAsset(TempPath);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            var result = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Report(result);
        }

        public static void BuildBatch()
        {
            try { Build(); EditorApplication.Exit(Missing.Count == 0 ? 0 : 0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ clips & masks
        public static AnimationClip LoadClip(string name)
        {
            string path = HumanAnimationImporter.Folder + name + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(a => !a.name.StartsWith("__preview__"));
        }

        static AnimationClip Clip(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var clip = LoadClip(name);
            if (!clip)
            {
                if (Fallback.TryGetValue(name, out var fb))
                {
                    Missing.Add(name);
                    Debug.LogWarning($"[Anim] clip {name} missing, using {fb}");
                    clip = Clip(fb);
                }
                else throw new System.IO.FileNotFoundException($"[Anim] required clip missing: {name}");
            }
            Cache[name] = clip;
            return clip;
        }

        static AvatarMask Mask(string path, bool withSpine)
        {
            var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            bool create = !m;
            if (create) m = new AvatarMask();
            for (var p = AvatarMaskBodyPart.Root; p < AvatarMaskBodyPart.LastBodyPart; p++) m.SetHumanoidBodyPartActive(p, false);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, withSpine);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            m.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            m.transformCount = 0;
            if (create) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ helpers
        static AnimatorState S(AnimatorStateMachine sm, string name, Motion motion, Vector2 pos, bool footIK = true, bool rate = false)
        {
            var s = sm.AddState(name, new Vector3(pos.x, pos.y, 0));
            s.motion = motion;
            s.writeDefaultValues = false;
            s.iKOnFeet = footIK;
            if (rate) { s.speedParameterActive = true; s.speedParameter = P_Rate; }
            return s;
        }

        struct Cond { public AnimatorConditionMode m; public float v; public string p; }
        static Cond If(string p, AnimatorConditionMode m, float v = 0) => new Cond { p = p, m = m, v = v };
        static Cond Eq(string p, int v) => If(p, AnimatorConditionMode.Equals, v);
        static Cond Ne(string p, int v) => If(p, AnimatorConditionMode.NotEqual, v);
        static Cond Lt(string p, float v) => If(p, AnimatorConditionMode.Less, v);
        static Cond Gt(string p, float v) => If(p, AnimatorConditionMode.Greater, v);
        static Cond True(string p) => If(p, AnimatorConditionMode.If);
        static Cond False(string p) => If(p, AnimatorConditionMode.IfNot);

        static AnimatorStateTransition Setup(AnimatorStateTransition t, float dur, float exit, Cond[] conds,
            TransitionInterruptionSource intr = TransitionInterruptionSource.None)
        {
            t.hasFixedDuration = true;
            t.duration = dur;
            t.hasExitTime = exit >= 0f;
            t.exitTime = exit >= 0f ? exit : 0f;
            t.offset = 0f;
            t.interruptionSource = intr;
            t.canTransitionToSelf = false;
            foreach (var k in conds) t.AddCondition(k.m, k.v, k.p);
            return t;
        }

        static AnimatorStateTransition T(AnimatorState a, AnimatorState b, float dur, float exit, params Cond[] conds) =>
            Setup(a.AddTransition(b), dur, exit, conds);

        static AnimatorStateTransition TI(AnimatorState a, AnimatorState b, float dur, float exit, TransitionInterruptionSource intr, params Cond[] conds) =>
            Setup(a.AddTransition(b), dur, exit, conds, intr);

        static AnimatorStateTransition ToLoco(AnimatorState a, float dur, float exit, params Cond[] conds) =>
            Setup(a.AddExitTransition(), dur, exit, conds);

        static BlendTree Tree(AnimatorController c, string name, string param)
        {
            var t = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = param, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(t, c);
            return t;
        }

        /// <summary>Variant-selected idle: each clip owns an equal slice of 0..1 (duplicate thresholds keep blends tiny).</summary>
        static Motion IdleTree(AnimatorController c, string name, params string[] clipNames)
        {
            if (clipNames.Length == 1) return Clip(clipNames[0]);
            var t = Tree(c, name, P_Variant);
            int n = clipNames.Length;
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n, b = (float)(i + 1) / n;
                t.AddChild(Clip(clipNames[i]), i == 0 ? 0f : a + 0.01f);
                t.AddChild(Clip(clipNames[i]), i == n - 1 ? 1f : b - 0.01f);
            }
            return t;
        }

        static Motion MoveTree(AnimatorController c, string name, string walk, string run)
        {
            var t = Tree(c, name, P_Speed);
            t.AddChild(Clip(walk), HumanAnimParams.WalkClipSpeed);
            t.AddChild(Clip(run), HumanAnimParams.RunClipSpeed);
            return t;
        }

        // ------------------------------------------------------------------ base layer
        static void BuildBase(AnimatorController c, AnimatorStateMachine sm)
        {
            sm.entryPosition = new Vector3(-420, 0); sm.exitPosition = new Vector3(-420, 400); sm.anyStatePosition = new Vector3(-420, -120);
            // ---- states (transitions are added afterwards: Unity evaluates them in creation order, so the State
            //      overrides (unconscious / captured) are added first and win over everything else)
            var idleCiv = S(sm, "Idle_Civilian", IdleTree(c, "IdleCivilianTree", "Idle_Neutral", "Idle_Shift", "Idle_LookAround"), new Vector2(-150, 0));
            var idleGuard = S(sm, "Idle_Guard", IdleTree(c, "IdleGuardTree", "Idle_Guard", "Idle_GuardBelt"), new Vector2(-150, 70));
            var idleSci = S(sm, "Idle_Scientist", IdleTree(c, "IdleScientistTree", "Idle_FoldArms", "Idle_Clipboard"), new Vector2(-150, 140));
            var idleMaint = S(sm, "Idle_Maintenance", IdleTree(c, "IdleMaintenanceTree", "Idle_HandsOnHips", "Idle_Shift"), new Vector2(-150, 210));
            var idlePos = S(sm, "Idle_Possessed", Clip("Idle_Possessed"), new Vector2(-150, 300));
            var move = S(sm, "Move", MoveTree(c, "MoveTree", "Walk", "Run"), new Vector2(150, 100), true, true);
            var movePos = S(sm, "Move_Possessed", MoveTree(c, "MovePossessedTree", "Walk_Possessed", "Run_Possessed"), new Vector2(150, 300), true, true);
            var movePanic = S(sm, "Move_Panic", MoveTree(c, "MovePanicTree", "Walk", "Panic_Flee_Loop"), new Vector2(150, 200), true, true);
            var idles = new[] { idleCiv, idleGuard, idleSci, idleMaint };
            var loco = new List<AnimatorState>(idles) { idlePos, move, movePos, movePanic };

            var takeoff = S(sm, "Jump_Takeoff", Clip("Jump_Takeoff"), new Vector2(450, -150), false);
            var fall = S(sm, "Fall_Loop", Clip("Fall_Loop"), new Vector2(700, -150), false);
            var land = S(sm, "Land", Clip("Land"), new Vector2(700, -60), true);

            var collapse = S(sm, "Collapse", Clip("Collapse"), new Vector2(450, 450), false);
            var unconscious = S(sm, "Unconscious_Loop", Clip("Unconscious_Loop"), new Vector2(700, 450), false);
            var getUp = S(sm, "GetUp", Clip("GetUp"), new Vector2(950, 450), false);

            var capEnter = S(sm, "Captured_Enter", Clip("Captured_Enter"), new Vector2(450, 600), false);
            var capLoop = S(sm, "Captured_Loop", Clip("Captured_Loop"), new Vector2(700, 600), false);
            var capCollapse = S(sm, "Captured_Collapse", Clip("Captured_Collapse"), new Vector2(700, 520), false);
            var capExit = S(sm, "Captured_Exit", Clip("Captured_Exit"), new Vector2(950, 600), false);

            var sitEnter = S(sm, "Sit_Enter", Clip("Sit_Enter"), new Vector2(-150, 520), true);
            var sitLoop = S(sm, "Sit_Loop", Clip("Sit_Loop"), new Vector2(-150, 600), true);
            var sitExit = S(sm, "Sit_Exit", Clip("Sit_Exit"), new Vector2(-150, 680), true);
            var held = new List<AnimatorState> { sitEnter, sitLoop, sitExit };
            var poseDefs = new (ActorPose pose, string clip, Vector2 pos)[]
            {
                (ActorPose.Talk, "Talk_Loop", new Vector2(150, 520)), (ActorPose.UseConsole, "Console_Loop", new Vector2(150, 590)),
                (ActorPose.Panic, "Panic_Loop", new Vector2(150, 660)), (ActorPose.Alert, "Alert_Loop", new Vector2(150, 730)),
                (ActorPose.Point, "Point_Loop", new Vector2(150, 800)),
            };
            var poseStates = new List<(ActorPose pose, AnimatorState state)>();
            foreach (var (pose, clip, pos) in poseDefs)
            {
                var ps = S(sm, clip, Clip(clip), pos, true);
                poseStates.Add((pose, ps));
                held.Add(ps);
            }
            sm.defaultState = idleCiv;

            // ---- 1. State overrides (highest priority)
            var standing = new List<AnimatorState>(loco) { takeoff, fall, land, capExit };
            standing.AddRange(held);
            foreach (var s in standing)
            {
                T(s, collapse, 0.2f, -1, Eq(P_State, 2));
                T(s, capEnter, 0.2f, -1, Eq(P_State, 3));
            }
            T(getUp, collapse, 0.3f, -1, Eq(P_State, 2));
            T(getUp, capEnter, 0.25f, -1, Eq(P_State, 3));
            T(capEnter, capCollapse, 0.3f, -1, Eq(P_State, 2));
            T(capLoop, capCollapse, 0.2f, -1, Eq(P_State, 2));

            // ---- 2. airborne (only for awake / possessed hosts)
            foreach (var l in loco) TI(l, takeoff, 0.12f, -1, TransitionInterruptionSource.Destination, False(P_Grounded), Lt(P_State, 2));
            T(takeoff, land, 0.1f, -1, True(P_Grounded));
            T(takeoff, fall, 0.25f, 0.92f);
            T(fall, land, 0.08f, -1, True(P_Grounded));
            T(land, takeoff, 0.1f, -1, False(P_Grounded));
            T(land, move, 0.2f, 0.2f, Gt(P_Speed, 1.2f), Eq(P_State, 0));
            T(land, movePos, 0.2f, 0.2f, Gt(P_Speed, 1.2f), Eq(P_State, 1));
            ToLoco(land, 0.25f, 0.55f);

            // ---- 3. held poses (entered from locomotion while slow; released when the pose changes or the actor moves)
            foreach (var l in loco) T(l, sitEnter, 0.3f, -1, Eq(P_Pose, (int)ActorPose.Sit), Lt(P_State, 2), Lt(P_Speed, HumanAnimParams.PoseMaxSpeed));
            foreach (var (pose, ps) in poseStates)
            {
                float fadeIn = pose == ActorPose.Panic ? 0.2f : 0.3f;
                foreach (var l in loco) T(l, ps, fadeIn, -1, Eq(P_Pose, (int)pose), Lt(P_State, 2), Lt(P_Speed, HumanAnimParams.PoseMaxSpeed));
            }
            // direct pose-to-pose switches (no detour through the idle), then release to locomotion
            foreach (var (pa, sa) in poseStates)
            {
                foreach (var (pb, sb) in poseStates)
                    if (pa != pb) T(sa, sb, 0.3f, -1, Eq(P_Pose, (int)pb), Lt(P_Speed, HumanAnimParams.PoseMaxSpeed));
                T(sa, sitEnter, 0.3f, -1, Eq(P_Pose, (int)ActorPose.Sit), Lt(P_Speed, HumanAnimParams.PoseMaxSpeed));
                T(sa, movePanic, 0.25f, -1, Gt(P_Speed, HumanAnimParams.PoseBreakSpeed), Eq(P_State, 0), Eq(P_Pose, (int)ActorPose.Panic));
                T(sa, move, 0.25f, -1, Gt(P_Speed, HumanAnimParams.PoseBreakSpeed), Eq(P_State, 0));
                T(sa, movePos, 0.25f, -1, Gt(P_Speed, HumanAnimParams.PoseBreakSpeed), Eq(P_State, 1));
                ToLoco(sa, 0.3f, -1, Ne(P_Pose, (int)pa));
            }
            T(sitEnter, sitExit, 0.3f, 0.5f, Ne(P_Pose, (int)ActorPose.Sit));
            T(sitEnter, sitLoop, 0.2f, 0.95f);
            T(sitLoop, sitExit, 0.3f, -1, Ne(P_Pose, (int)ActorPose.Sit));
            ToLoco(sitExit, 0.3f, 0.9f);

            // ---- 4. locomotion. Entry picks the idle by State / Profession (Exit transitions come back through here).
            sm.AddEntryTransition(idlePos).AddCondition(AnimatorConditionMode.Equals, 1, P_State);
            for (int k = 1; k < 4; k++) sm.AddEntryTransition(idles[k]).AddCondition(AnimatorConditionMode.Equals, k, P_Profession);
            float enter = HumanAnimParams.MoveEnterSpeed, leave = HumanAnimParams.MoveExitSpeed;
            for (int k = 0; k < 4; k++)
            {
                var i = idles[k];
                T(i, idlePos, 0.4f, -1, Eq(P_State, 1));
                T(i, movePanic, 0.2f, -1, Gt(P_Speed, enter), Eq(P_State, 0), Eq(P_Pose, (int)ActorPose.Panic));
                T(i, move, 0.2f, -1, Gt(P_Speed, enter), Eq(P_State, 0));
                for (int j = 0; j < 4; j++) if (j != k) T(i, idles[j], 0.3f, -1, Eq(P_Profession, j), Eq(P_State, 0));
            }
            T(move, movePos, 0.35f, -1, Eq(P_State, 1));
            T(move, movePanic, 0.3f, -1, Eq(P_Pose, (int)ActorPose.Panic), Eq(P_State, 0));
            T(movePanic, movePos, 0.35f, -1, Eq(P_State, 1));
            T(movePanic, move, 0.3f, -1, Ne(P_Pose, (int)ActorPose.Panic));
            for (int k = 0; k < 4; k++) T(movePanic, idles[k], 0.25f, -1, Lt(P_Speed, leave), Eq(P_State, 0), Eq(P_Profession, k), Ne(P_Pose, (int)ActorPose.Panic));
            for (int k = 0; k < 4; k++) T(move, idles[k], 0.25f, -1, Lt(P_Speed, leave), Eq(P_State, 0), Eq(P_Profession, k));
            T(move, idleCiv, 0.25f, -1, Lt(P_Speed, leave), Eq(P_State, 0), Gt(P_Profession, 3.5f));
            T(movePos, move, 0.35f, -1, Eq(P_State, 0));
            T(movePos, idlePos, 0.25f, -1, Lt(P_Speed, leave), Eq(P_State, 1));
            T(idlePos, movePos, 0.2f, -1, Gt(P_Speed, enter), Eq(P_State, 1));
            for (int k = 0; k < 4; k++) T(idlePos, idles[k], 0.5f, -1, Eq(P_State, 0), Eq(P_Profession, k));
            T(idlePos, idleCiv, 0.5f, -1, Eq(P_State, 0), Gt(P_Profession, 3.5f));

            // ---- 5. unconscious chain
            T(collapse, getUp, 0.3f, 0.75f, Ne(P_State, 2));
            T(collapse, unconscious, 0.12f, 0.97f);
            T(unconscious, getUp, 0.25f, -1, Ne(P_State, 2));
            ToLoco(getUp, 0.25f, 0.9f);

            // ---- 6. captured chain (possessed host grabbed by a guard; ejection knocks the host out)
            T(capEnter, capLoop, 0.15f, 0.97f);
            T(capLoop, capExit, 0.25f, -1, Lt(P_State, 2));
            T(capCollapse, getUp, 0.3f, 0.8f, Ne(P_State, 2));
            T(capCollapse, unconscious, 0.15f, 0.97f);
            ToLoco(capExit, 0.25f, 0.92f);
        }

        // ------------------------------------------------------------------ extra layers
        static void BuildGesture(AnimatorController c, AvatarMask mask)
        {
            c.AddLayer(new AnimatorControllerLayer
            {
                name = "Gesture", defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Additive, avatarMask = mask,
                stateMachine = NewSM(c, "Gesture"),
            });
            var sm = c.layers[c.layers.Length - 1].stateMachine;
            var empty = sm.AddState("Empty", new Vector3(0, 0)); empty.writeDefaultValues = false;
            var a = S(sm, "Gesture_Explain", Clip("Gesture_Explain"), new Vector2(300, -60), false);
            var b = S(sm, "Gesture_Shrug", Clip("Gesture_Shrug"), new Vector2(300, 60), false);
            sm.defaultState = empty;
            T(empty, a, 0.25f, -1, True(P_Gesture), Lt(P_Variant, 0.5f), Lt(P_State, 2));
            T(empty, b, 0.25f, -1, True(P_Gesture), Gt(P_Variant, 0.4999f), Lt(P_State, 2));
            T(a, empty, 0.35f, 0.85f);
            T(b, empty, 0.35f, 0.85f);
            T(a, empty, 0.2f, -1, Gt(P_State, 1));
            T(b, empty, 0.2f, -1, Gt(P_State, 1));
        }

        static AnimatorStateMachine NewSM(AnimatorController c, string name)
        {
            var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, c);
            return sm;
        }

        // ------------------------------------------------------------------ report
        static void Report(AnimatorController c)
        {
            if (!c) throw new System.Exception("[Anim] controller failed to load after build");
            int states = 0, transitions = 0;
            foreach (var l in c.layers)
            {
                foreach (var s in l.stateMachine.states) { states++; transitions += s.state.transitions.Length; }
                transitions += l.stateMachine.entryTransitions.Length;
            }
            Debug.Log($"[Anim] Built {ControllerPath}: {c.layers.Length} layers, {states} states, {transitions} transitions, " +
                      $"{c.parameters.Length} parameters; walk {HumanAnimParams.WalkClipSpeed} m/s, run {HumanAnimParams.RunClipSpeed} m/s.");
            foreach (var path in Directory.GetFiles(HumanAnimationImporter.Folder, "*.fbx").OrderBy(p => p))
            {
                string p = path.Replace('\\', '/');
                var clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
                var avatar = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().FirstOrDefault();
                if (!clip) { Debug.LogWarning($"[Anim] {p}: no clip"); continue; }
                Debug.Log($"[Anim] clip {clip.name,-18} {clip.length,6:0.000}s loop={clip.isLooping} human={clip.isHumanMotion} " +
                          $"avatar={(avatar ? (avatar.isValid && avatar.isHuman ? "ok" : "INVALID") : "none")}");
            }
            if (Missing.Count > 0) Debug.LogWarning($"[Anim] {Missing.Count} clips missing (fallbacks used): {string.Join(", ", Missing.Distinct())}");
            else Debug.Log("[Anim] all clips present.");
        }
    }
}
