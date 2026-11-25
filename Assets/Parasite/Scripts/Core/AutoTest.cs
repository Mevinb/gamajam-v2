using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Parasite.Testing
{
    /// <summary>
    /// Automated acceptance scenarios driven through the real game systems with simulated input.
    /// Used by PlayMode tests and by the standalone build:
    ///   Parasite.x86_64 -autotest [-autotestOut file]      → runs all scenarios, writes a report, quits (exit code 0/1)
    ///   Parasite.x86_64 -benchmark [-autotestOut file]     → walks the city, logs uncapped FPS + route coverage, screenshots, quits
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public struct Result { public string name; public bool pass; public string detail; public float seconds; }

        public static readonly List<Result> Results = new();
        public static readonly StringBuilder Log = new();
        public static bool Running { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            bool auto = args.Contains("-autotest"), bench = args.Contains("-benchmark"), shortcut = args.Contains("-shortcuttest");
            if (!auto && !bench && !shortcut) return;
            var go = new GameObject("AutoTestRunner");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<AutoTest>();
            r.StartCoroutine(shortcut ? r.ShortcutMain() : bench ? r.BenchmarkMain() : r.AutoMain());
        }

        static string OutPath()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autotestOut");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "autotest_report.txt");
        }

        /// <summary>Diagnostic detail for a running scenario (not a PASS/FAIL line).</summary>
        static void Note(string line) => Debug.Log("[AutoTest-note] " + (current.name ?? "") + " :: " + line);

        static void Write(string line)
        {
            Log.AppendLine(line);
            Debug.Log("[AutoTest] " + line);
        }

        IEnumerator AutoMain()
        {
            yield return RunAll();
            int failed = Results.Count(r => !r.pass);
            Write($"SUMMARY: {Results.Count - failed}/{Results.Count} passed");
            File.WriteAllText(OutPath(), Log.ToString());
            yield return null;
            CinematicFilmLifetime.Quit(failed == 0 ? 0 : 1);
        }

        // Built-player input regression mode. These are queued Input System events, not native OS key injection.
        IEnumerator ShortcutMain()
        {
            Running = true; Results.Clear(); Log.Clear();
            Application.runInBackground = true;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldUpdate = InputSystem.settings.updateMode;
            var keyboard = InputSystem.AddDevice<Keyboard>("Standalone shortcut regression keyboard");
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            Write($"PARASITE shortcut input regression — {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {SystemInfo.graphicsDeviceName}");
            Write("Queued KeyboardState events in the built player; GameInput.Simulated=false. This does not claim native OS key injection.");
            try
            {
                foreach (string phase in new[] { "Title", "Opening", "PausedSettings", "ValeDialogue" })
                {
                    current = new Result { name = "Global shortcut from " + phase, pass = true };
                    yield return Guard(ShortcutPresentationPhaseCase(keyboard, phase));
                    Results.Add(current);
                    Write($"{(current.pass ? "PASS" : "FAIL")} {current.name}: {current.detail}");
                }
                foreach (var control in new[] { Key.LeftCtrl, Key.RightCtrl })
                foreach (bool letterFirst in new[] { false, true })
                foreach (bool possessed in new[] { false, true })
                {
                    current = new Result { name = $"{control} {(letterFirst ? "M-first" : "Ctrl-first")} {(possessed ? "host" : "free")}", pass = true };
                    yield return Guard(ShortcutKeyboardCase(keyboard, control, letterFirst, possessed, false));
                    Results.Add(current);
                    Write($"{(current.pass ? "PASS" : "FAIL")} {current.name}: {current.detail}");
                }
                foreach (var control in new[] { Key.LeftCtrl, Key.RightCtrl })
                {
                    current = new Result { name = $"{control} fast down/up in one input update", pass = true };
                    yield return Guard(ShortcutKeyboardCase(keyboard, control, false, false, true));
                    Results.Add(current);
                    Write($"{(current.pass ? "PASS" : "FAIL")} {current.name}: {current.detail}");
                }
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.updateMode = oldUpdate;
                ClearSim(); GameInput.Simulated = false; Running = false;
            }
            int failed = Results.Count(result => !result.pass);
            Write($"SUMMARY: {Results.Count - failed}/{Results.Count} passed");
            string report = Path.GetFullPath(OutPath());
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, Log.ToString());
            yield return null;
            CinematicFilmLifetime.Quit(failed == 0 ? 0 : 1);
        }

        static IEnumerator ShortcutKeyboardCase(Keyboard keyboard, Key control, bool letterFirst, bool possessed, bool fastTap)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            yield return LoadCity();
            GameInput.Simulated = false;
            if (possessed)
            {
                var host = NearestHost(PM.Active.Position, h => h.CanBePossessed && h.Profession == Profession.Civilian);
                Check(host, "an ordinary civilian host is available");
                if (!host) yield break;
                PM.ForcePossessImmediate(host);
            }
            var gm = GameManager.Instance;
            var originalBlob = PM.Active.Blob;
            int captures = RunState.Captures, resets = RunState.Resets;
            int performed = 0;
            void OnChord(InputAction.CallbackContext ctx) { if (ctx.control.device == keyboard) performed++; }
            GameInput.PresentationEnding.performed += OnChord;
            try
            {
                if (!fastTap)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(letterFirst ? Key.M : control));
                    yield return null; yield return null; yield return null;
                    Check(gm.IsPlaying && !MissionManager.Instance.EndingStarted, "a lone first key does not activate presentation");
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(control, Key.M));
                if (fastTap) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (float t = 0; t < 3f && !MissionManager.Instance.EndingStarted; t += Time.unscaledDeltaTime) yield return null;
                Check(performed > 0, "the named PresentationEnding action received the actual queued chord");
                Check(MissionManager.Instance.EndingStarted && gm.State == GameState.Ending && Time.timeScale == 0f,
                    "queued chord starts the real protected drain sequence");
                Check(PM.Active.Host == null && PM.Active.Blob == originalBlob && Vector3.Distance(PM.Active.Position, MissionLayout.Extraction) < 0.5f,
                    "free and possessed forms reach extraction using the same amoeba");
                Check(!RunState.Has(MissionFlags.Escaped), "shortcut leaves the final outcome unchosen");
                Vector3 position = PM.Active.Position;
                yield return new WaitForSecondsRealtime(0.25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(control));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(control, Key.M));
                yield return null; yield return null;
                Check(GameManager.Instance == gm && PM.Active.Position == position && RunState.Captures == captures && RunState.Resets == resets,
                    "held/repeated chord does not reload, recapture or relocate the player");
                Check(!RunState.Has(MissionFlags.Escaped), "repeated input never chooses an ending");
            }
            finally { GameInput.PresentationEnding.performed -= OnChord; }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        /// <summary>Shared phase fixtures for queued-key PlayMode and built-player shortcut regression.</summary>
        public static IEnumerator PreparePresentationPhase(string phase)
        {
            ClearSim(); GameInput.Simulated = false; Time.timeScale = 1f;
            bool title = phase.StartsWith("Title") || phase == "BeginTransition" || phase.StartsWith("Opening");
            if (title)
            {
                if (phase != "Title" || SceneManager.GetActiveScene().name != GameManager.MenuScene)
                { SceneManager.LoadScene(GameManager.MenuScene); yield return null; }
                if (phase == "Title") yield break;
                yield return new WaitForSecondsRealtime(0.7f);
                var menu = FindFirstObjectByType<UI.MainMenuController>();
                if (!menu) throw new InvalidOperationException("Presentation fixture has no title menu.");
                if (phase == "TitleControls" || phase == "TitleSettings")
                { PresentationFixtureClick(menu.transform, phase == "TitleControls" ? "Btn_Controls" : "Btn_Settings"); yield break; }
                PresentationFixtureClick(menu.transform, "Btn_Begin Infiltration");
                if (phase == "BeginTransition") { yield return new WaitForSecondsRealtime(0.1f); yield break; }
                float deadline = Time.realtimeSinceStartup + 20f;
                while ((!OpeningCinematic.Instance || !OpeningCinematic.Instance.Running) && Time.realtimeSinceStartup < deadline) yield return null;
                if (!OpeningCinematic.Instance || !OpeningCinematic.Instance.Running)
                    throw new InvalidOperationException("Requested opening did not start in presentation phase fixture.");
                if (phase == "OpeningFinish")
                {
                    var opening = OpeningCinematic.Instance; opening.PlaybackSpeed = 80f;
                    while (opening && opening.Running && opening.ElapsedSeconds < OpeningCinematic.Duration && Time.realtimeSinceStartup < deadline) yield return null;
                }
                else yield return new WaitForSecondsRealtime(0.3f);
                yield break;
            }
            yield return LoadCity();
            GameInput.Simulated = false;
            var gm = GameManager.Instance;
            if (phase.StartsWith("Paused"))
            {
                gm.SetPaused(true); yield return null;
                if (phase == "PausedSettings") PresentationFixtureClick(UI.HUDController.Instance.transform.Find("HUD Canvas/Pause"), "Btn_Settings");
                else if (phase == "PausedControls") PresentationFixtureClick(UI.HUDController.Instance.transform.Find("HUD Canvas/Pause"), "Btn_Controls");
            }
            else if (phase == "ValeDialogue")
            {
                MissionManager.SetFlag(MissionFlags.Research); MissionManager.SetFlag(MissionFlags.OfficeOpen);
                var vale = FindFirstObjectByType<DrValeInteraction>();
                vale.Interact(ActiveContext());
                yield return null;
                if (!MissionManager.Instance.DialoguePlaying) throw new InvalidOperationException("Vale reveal fixture failed to start.");
            }
            else if (phase == "ResetPending") { gm.ResetToCheckpoint("Presentation regression reset setup"); yield return null; }
            else if (phase != "Normal") throw new ArgumentException("Unknown presentation fixture phase: " + phase);
        }

        static void PresentationFixtureClick(Transform root, string buttonName)
        {
            var button = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == buttonName);
            if (!button.gameObject.activeInHierarchy || !button.IsInteractable()) throw new InvalidOperationException(buttonName + " is not actionable.");
            button.onClick.Invoke();
        }

        static IEnumerator ShortcutPresentationPhaseCase(Keyboard keyboard, string phase)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            yield return PreparePresentationPhase(phase);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.M));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.M, Key.RightCtrl));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((!GameManager.Instance || !MissionManager.Instance || !MissionManager.Instance.EndingStarted) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.Instance && MissionManager.Instance && MissionManager.Instance.EndingStarted,
                phase + " queued chord reaches the real final escape");
            if (!current.pass) yield break;
            var gm = GameManager.Instance;
            var director = CityDirector.Instance;
            float stableUntil = Time.realtimeSinceStartup + 4.8f, nextPulse = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < stableUntil)
            {
                bool pulse = Time.realtimeSinceStartup >= nextPulse;
                InputSystem.QueueStateEvent(keyboard, pulse ? new KeyboardState(Key.E) : new KeyboardState());
                if (pulse) nextPulse = Time.realtimeSinceStartup + 0.65f;
                yield return null;
                Check(gm && gm == GameManager.Instance && director == CityDirector.Instance && gm.State == GameState.Ending && Time.timeScale == 0f,
                    phase + " remains protected after old intro/reveal/reload timing");
                if (!current.pass) yield break;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            Check(!OpeningCinematic.Instance && !MissionManager.Instance.DialoguePlaying && !RunState.Has(MissionFlags.Escaped),
                phase + " has no stale intro/reveal and no automatically chosen outcome");
            Check(FindObjectsByType<UI.LakesideEpilogue>(FindObjectsSortMode.None).Length <= 1,
                phase + " creates no duplicate lakeside controller");
            if (UI.LakesideEpilogue.Instance) Check(UI.LakesideEpilogue.Instance.Outcome == EndingOutcome.Unchosen,
                phase + " preserves explicit ending choice");
        }

        // ================================================================ scenarios
        public static IEnumerator RunAll()
        {
            Running = true;
            Results.Clear();
            Log.Clear();
            Write($"PARASITE autotest — {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {SystemInfo.graphicsDeviceName} / {SystemInfo.operatingSystem}");
            var scenarios = new (string, Func<IEnumerator>)[]
            {
                ("01 Start, move, camera, jump", ScenarioMove),
                ("02 Aim + E possesses host within 5 m (camera + WASD follow)", ScenarioPossess),
                ("03 Q release: host unconscious 8 s then recovers", ScenarioRelease),
                ("04 V split max two, Tab switch, inactive stays put", ScenarioSplit),
                ("05 Civilian denied at restricted doors", ScenarioCivilianDenied),
                ("06 Route A: leads → tail → guard badge → Main Gate → research → Vale → hunters → evidence → containment → blackout → escape", () => ScenarioRoute(EntryRoute.MainGate)),
                ("07 Route B: leads → tail → scientist + keycard → Staff Terminal → research → Vale → hunters → evidence → containment → blackout → escape", () => ScenarioRoute(EntryRoute.StaffTerminal)),
                ("08 Route C: leads → tail → maintenance + substation → Service Tunnel → research → Vale → hunters → evidence → containment → blackout → escape", () => ScenarioRoute(EntryRoute.ServiceTunnel)),
                ("09 Suspicion only from observed violations; decays", ScenarioSuspicion),
                ("12 Missions 1-2: alley exit, first host, newsstand, eavesdrop", ScenarioEarlyMissions),
                ("13 CCTV spots a free amoeba; maintenance F-ability cuts camera power; scientist calms", ScenarioCameraAbilities),
                ("14 The Tail: losing Petrov or crowding her sends her back to the last checkpoint; progress survives a reload", ScenarioTail),
                ("15 Hunter Protocol: bio-scanner burns a worn body, leaving it breaks the trail, the arch passes only a free amoeba", ScenarioHunters),
                ("16 Blackout: palm-print lockout, grid reroute on timeout, three-body chain, blackout persists", ScenarioBlackout),
                ("10 Guard pursues + captures; amoeba ejects and survives", ScenarioCapture),
                ("11 Pause + restart", ScenarioPauseRestart),
                ("17 Peace: explicit lake choice → Continue → riverside missions → checkpoint recovery → ferry", () => AfterlakeAcceptance.RunChapter(EndingOutcome.Peace)),
                ("18 Dominion: explicit lake choice → Continue → branch relay → checkpoint recovery → ferry", () => AfterlakeAcceptance.RunChapter(EndingOutcome.Dominion)),
                ("19 River falls: active/inactive bodies recover once; story/NPC fixtures are excluded", FallRecoveryAcceptance.RunAll),
                ("20 MissionProp holds: original owner/state/authorization and pause semantics", MissionPropAcceptance.RunAll),
                ("21 Island first slice: explicit ferry handoff, both entry routes and fair recovery", IslandEntryAcceptance.RunAll),
                ("22 Rendered river/berth visibility: actual skyline occlusion before/after, unchanged footing and shaded views", () => WaterVisibilityAcceptance.Run()),
                ("23 Island rescue branches: moving people, carried signal, all-free assembly and protected launch", IslandRescueAcceptance.RunBranches),
                ("24 Island rescue recovery: capture, visited cover, partial roster, signal custody and ally reporting", IslandRescueAcceptance.RunRecovery),
                ("25 Harbor Peace: pressure lock, moving antidote manifest, two-free-body relay and checkpoint recovery", () => HarborAcceptance.RunChapter(EndingOutcome.Peace)),
                ("26 Harbor Dominion: pressure lock, moving relay manifest, two-free-body relay and checkpoint recovery", () => HarborAcceptance.RunChapter(EndingOutcome.Dominion)),
                ("27 Harbor cargo recovery: two actual in-flight reroutes, separate physical restore and bay arrivals", HarborAcceptance.TwoInflightCargoReroutesRecoverWithoutOverlap),
            };
            // Optional iteration aid: -autotestFilter 06,14 runs only scenarios whose number prefix matches.
            var args = Environment.GetCommandLineArgs();
            int fi = Array.IndexOf(args, "-autotestFilter");
            var only = fi >= 0 && fi + 1 < args.Length ? args[fi + 1].Split(',') : null;
            foreach (var (name, fn) in scenarios)
            {
                if (only != null && !only.Any(o => name.StartsWith(o.Trim()))) continue;
                yield return LoadCity();
                current = new Result { name = name, pass = true };
                float t0 = Time.realtimeSinceStartup;
                yield return Guard(fn());
                current.seconds = Time.realtimeSinceStartup - t0;
                Results.Add(current);
                Write($"{(current.pass ? "PASS" : "FAIL")}  {name}  ({current.seconds:0.0}s)  {current.detail}");
                GameInput.Simulated = false;
                Time.timeScale = 1f;
            }
            Running = false;
        }

        static Result current;

        static void Check(bool cond, string what)
        {
            if (!cond && current.pass) { current.pass = false; current.detail = "FAILED: " + what; }
            else if (cond && current.pass) current.detail = what;
        }

        /// <summary>Runs a scenario, catching exceptions as failures.</summary>
        static IEnumerator Guard(IEnumerator e)
        {
            try
            {
                while (true)
                {
                    object cur;
                    try { if (!e.MoveNext()) yield break; cur = e.Current; }
                    catch (Exception ex) { current.pass = false; current.detail = "EXCEPTION: " + ex.Message; Debug.LogException(ex); yield break; }
                    if (cur is IEnumerator nested) yield return Guard(nested);
                    else yield return cur;
                    if (!current.pass) yield break;
                }
            }
            finally { (e as IDisposable)?.Dispose(); }
        }

        public static IEnumerator LoadCity()
        {
            RunState.NewGame();
            Time.timeScale = 1f;
            GameInput.Simulated = false;
            SceneManager.LoadScene(GameManager.CityScene);
            yield return null;
            float t = 0;
            while ((CityDirector.Instance == null || !CityDirector.Instance.Ready) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);
            GameInput.Simulated = true;
            ClearSim();
        }

        static void ClearSim()
        {
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false; GameInput.SimJump = false;
            GameInput.SimInteract = false; GameInput.SimLeave = false; GameInput.SimSplit = false; GameInput.SimSwitch = false;
            GameInput.SimAbility = false;
        }

        static PossessionManager PM => PossessionManager.Instance;
        static PlayerCamera Cam => PlayerCamera.Instance;

        static IEnumerator Press(Action set, Action clear)
        {
            set(); yield return null; clear(); yield return null;
        }

        static IEnumerator PressInteract() => Press(() => GameInput.SimInteract = true, () => GameInput.SimInteract = false);

        /// <summary>Look at a world point through the actual camera, including its settled boom/collision position.</summary>
        static void AimAt(Vector3 p)
        {
            Vector3 d = p - Cam.Position;
            if (d.sqrMagnitude <= 0.001f) return;
            float planar = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            if (planar > 0.001f) Cam.SetYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            Cam.SetPitch(-Mathf.Atan2(d.y, planar) * Mathf.Rad2Deg);
        }

        static IEnumerator AimSettle(Vector3 p)
        {
            // Yaw/pitch move the boom, and nearby geometry can shorten it. Recompute from its real
            // position each frame, within the existing camera clamps and unchanged .8s aim budget.
            for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime) { AimAt(p); yield return null; }
        }

        /// <summary>Track the intended host's actual moving chest through the normal camera controls.</summary>
        public static IEnumerator AimSettle(HostCharacter target)
        {
            for (float t = 0; t < 0.8f && target; t += Time.unscaledDeltaTime)
            { AimAt(target.ChestPosition); yield return null; }
        }

        /// <summary>Normal retreat within the original security-room leg budget when an investigating officer crowds the blob.</summary>
        public static IEnumerator EnsureHunterAimSpacing(HostCharacter officer, float approachDeadline)
        {
            if (!officer || PM == null || PM.Active == null || !PM.Active.IsFree)
                throw new InvalidOperationException("Hunter aiming requires its actual free amoeba and officer.");
            float spacing = Flat(PM.Active.Position - officer.transform.position).magnitude;
            if (spacing >= 2.6f) yield break;
            float remaining = approachDeadline - Time.realtimeSinceStartup;
            if (remaining <= 0f) throw new InvalidOperationException("The original hunter approach budget expired before physical retreat.");
            // This is the same clear north aisle used to enter the office. Stay on the inside of the arch;
            // a player can back away from a close chest rather than changing the camera's upward clamp.
            Vector3 retreat = new(17.3f, CityLayout.LabFloor, 83.8f);
            if (!PathComplete(PM.Active.Position, retreat))
                throw new InvalidOperationException("The hunter retreat must have a complete path to the original security-room aisle.");
            Vector3 before = PM.Active.Position;
            NoteHunterTarget("crowded officer before physical retreat", officer, PM.ComputeTarget());
            bool reached = false;
            yield return WalkTo(retreat, remaining, value => reached = value, .15f);
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
            spacing = officer ? Flat(PM.Active.Position - officer.transform.position).magnitude : 0f;
            Note("hunter physical retreat from=" + before.ToString("F4") + " to=" + PM.Active.Position.ToString("F4")
                + " target=" + retreat.ToString("F4") + " reached=" + reached + " spacing=" + spacing.ToString("F4")
                + " originalBudgetRemaining=" + (approachDeadline - Time.realtimeSinceStartup).ToString("F4"));
            if (!reached || spacing < 2.6f || Time.realtimeSinceStartup > approachDeadline)
                throw new InvalidOperationException("Normal hunter retreat did not restore legal aiming space inside the original approach budget.");
        }

        static void NoteHunterTarget(string phase, HostCharacter officer, TargetInfo target)
        {
            var active = PM.Active; var body = PM.ActiveBody;
            if (active == null || body == null || !officer || !Cam) return;
            Vector3 eye = body.EyePosition, chest = officer.ChestPosition, ray = chest - eye;
            var rayHits = Physics.RaycastAll(eye, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance).Select(h => h.collider.name + " parent=" + (h.collider.transform.parent ? h.collider.transform.parent.name : "-")
                    + " type=" + h.collider.GetType().Name + " layer=" + h.collider.gameObject.layer + " distance=" + h.distance.ToString("F4")
                    + " self=" + h.collider.transform.IsChildOf(body.Root) + " target=" + h.collider.transform.IsChildOf(officer.transform));
            Note("hunter-target phase=" + phase + " body=" + body.Root.name + " free=" + active.IsFree
                + " bodyAt=" + body.Root.position.ToString("F4") + " eye=" + eye.ToString("F4")
                + " cameraAt=" + Cam.Position.ToString("F4") + " cameraForward=" + Cam.Forward.ToString("F4")
                + " yaw=" + Cam.Yaw.ToString("F4") + " pitch=" + Cam.Pitch.ToString("F4")
                + " officerAt=" + officer.transform.position.ToString("F4") + " chest=" + chest.ToString("F4")
                + " officerState=" + officer.State + " possessable=" + officer.CanBePossessed + " officerPossessor=" + (officer.Possessor != null)
                + " officerBrain=" + (officer.Brain is GuardAI guard ? guard.GState.ToString() : "not guard")
                + " officerVelocity=" + officer.Velocity.ToString("F4")
                + " aimAngle=" + Vector3.Angle(Cam.Forward, chest - Cam.Position).ToString("F4") + " cone=" + PossessionManager.AimConeDegrees
                + " eyeDistance=" + ray.magnitude.ToString("F4") + " range=" + PossessionManager.PossessRange
                + " los=" + PM.LineOfSight(eye, chest, body.Root, officer.transform) + " transit=" + active.InTransit
                + " inputLocked=" + PM.InputLocked + " targetKind=" + target.kind + " targetHost=" + (target.host ? target.host.DisplayName : "-")
                + " prompt=" + target.prompt + " rayHits=[" + string.Join("; ", rayHits) + "]");
        }

        static HostCharacter NearestHost(Vector3 p, Func<HostCharacter, bool> pred) =>
            HostCharacter.All.Where(h => h && pred(h)).OrderBy(h => (h.transform.position - p).sqrMagnitude).FirstOrDefault();

        static void TeleportBody(IControllable body, Vector3 pos, float yaw)
        {
            if (body is AmoebaController a) { a.Place(pos); a.transform.rotation = Quaternion.Euler(0, yaw, 0); }
            else if (body is HostCharacter h)
            {
                bool en = h.Controller.enabled;
                h.Controller.enabled = false;
                h.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
                h.Controller.enabled = en;
                Physics.SyncTransforms();
            }
        }

        static void TeleportNpc(HostCharacter h, Vector3 pos, float yaw)
        {
            if (h.Nav && h.Nav.enabled) h.Nav.Warp(pos); else h.transform.position = pos;
            h.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        /// <summary>Walk the active body along a NavMesh path using simulated WASD (camera yaw fixed at 0).</summary>
        public static IEnumerator WalkTo(Vector3 target, float timeout, Action<bool> done, float tolerance = 0.9f)
        {
            Cam.SetYaw(0f);
            var path = new NavMeshPath();
            var body = PM.ActiveBody;
            float t = 0f, repath = 0f;
            Vector3[] corners = Array.Empty<Vector3>();
            int ci = 0;
            Vector3 lastPos = body.Root.position; float stuckT = 0f;
            while (t < timeout)
            {
                body = PM.ActiveBody;
                if (body == null) break;
                Vector3 pos = body.Root.position;
                if (Flat(pos - target).magnitude < tolerance) { GameInput.SimMove = Vector2.zero; done(true); yield break; }
                repath -= Time.deltaTime;
                if (repath <= 0f)
                {
                    repath = 1.5f;
                    NavMesh.SamplePosition(pos, out var a, 3f, NavMesh.AllAreas);
                    NavMesh.SamplePosition(target, out var b, 3f, NavMesh.AllAreas);
                    if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 0)
                    { corners = path.corners; ci = 0; }
                    else { corners = new[] { target }; ci = 0; }
                }
                while (ci < corners.Length - 1 && Flat(corners[ci] - pos).magnitude < 0.5f) ci++;
                Vector3 dir = Flat((ci < corners.Length ? corners[ci] : target) - pos).normalized;
                GameInput.SimMove = new Vector2(dir.x, dir.z);
                GameInput.SimSprint = true;
                Cam.SetYaw(0f);
                // Measure planar metres per simulated second: an uncapped high frame rate is not a stall.
                float progressSpeed = Time.deltaTime > 0f ? Flat(pos - lastPos).magnitude / Time.deltaTime : 0f;
                if (Time.deltaTime > 0f && progressSpeed < 0.15f)
                { stuckT += Time.deltaTime; if (stuckT > 0.8f) { GameInput.SimJump = true; stuckT = 0f; } }
                else { stuckT = 0f; GameInput.SimJump = false; }
                lastPos = pos;
                t += Time.deltaTime;
                yield return null;
                GameInput.SimJump = false;
            }
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
            done(false);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        static void Calm()
        {
            SuspicionSystem.Instance?.SetValue(0f);
            SuspicionSystem.Instance?.OnSuspectGone();
        }

        // ---------------------------------------------------------------- 01
        static IEnumerator ScenarioMove()
        {
            Check(PM != null && PM.Agents.Count == 1 && PM.Active.IsFree, "free amoeba spawned");
            Check(Cam != null && Cam.Cam != null, "camera exists");
            var blob = PM.Active.Blob;
            Vector3 p0 = blob.transform.position;
            Cam.SetYaw(blob.transform.eulerAngles.y);
            GameInput.SimMove = new Vector2(0, 1);
            yield return new WaitForSeconds(1.2f);
            GameInput.SimMove = Vector2.zero;
            float moved = Flat(blob.transform.position - p0).magnitude;
            Check(moved > 1.5f, $"blob moved {moved:0.0} m with W");
            float yaw0 = Cam.Cam.transform.eulerAngles.y;
            Cam.SetYaw(yaw0 + 90f);
            yield return new WaitForSeconds(0.4f);
            float dyaw = Mathf.Abs(Mathf.DeltaAngle(yaw0, Cam.Cam.transform.eulerAngles.y));
            Check(dyaw > 45f, $"camera turned {dyaw:0}°");
            float y0 = blob.transform.position.y, maxY = y0;
            GameInput.SimJump = true; yield return null; GameInput.SimJump = false;
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { maxY = Mathf.Max(maxY, blob.transform.position.y); yield return null; }
            Check(maxY - y0 > 0.25f, $"moved {moved:0.0} m, camera turned {dyaw:0}°, hop {maxY - y0:0.00} m");
        }

        // ---------------------------------------------------------------- 02
        static IEnumerator ScenarioPossess()
        {
            Calm();
            var target = NearestHost(PM.Active.Position, h => h.CanBePossessed && h.Profession == Profession.Civilian)
                         ?? NearestHost(PM.Active.Position, h => h.CanBePossessed);
            Check(target != null, "found a host");
            if (!target) yield break;
            // stand 3.5 m in front of the host, aim at it
            Vector3 hp = target.transform.position;
            Vector3 spot = PossessionManager.FindFreeSpot(hp + target.transform.forward * 3.5f, Vector3.zero);
            TeleportBody(PM.ActiveBody, spot, 0);
            target.Brain.Notice(spot, 3f);
            yield return AimSettle(target);
            var tg = PM.ComputeTarget();
            float ang = Vector3.Angle(Cam.Forward, target.ChestPosition - Cam.Position);
            float dst = Vector3.Distance(PM.ActiveBody.EyePosition, target.ChestPosition);
            bool los = PM.LineOfSight(PM.ActiveBody.EyePosition, target.ChestPosition, PM.ActiveBody.Root, target.transform);
            Check(tg.kind == TargetKind.Possess && tg.host == target, $"aim target = {tg.kind} {(tg.host ? tg.host.DisplayName : "-")} (angle {ang:0.0}°, dist {dst:0.0} m, LOS {los}, canPossess {target.CanBePossessed})");
            if (!current.pass) yield break;
            yield return PressInteract();
            yield return new WaitForSeconds(PossessionManager.LaunchSeconds + 0.4f);
            Check(PM.Active.Host == target && target.IsPossessed, $"possessed {target.DisplayName}");
            yield return new WaitForSeconds(0.5f);
            float camDist = Vector3.Distance(Cam.Cam.transform.position, target.CameraTarget.position);
            Check(camDist < 7f, $"camera follows host ({camDist:0.0} m)");
            Vector3 p0 = target.transform.position;
            Vector3 free = Vector3.forward; float bestFree = -1f;
            for (int i = 0; i < 16; i++)
            {
                var d = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                float len = NavMesh.Raycast(p0, p0 + d * 6f, out var nh, NavMesh.AllAreas) ? nh.distance : 6f;
                if (len > bestFree) { bestFree = len; free = d; }
            }
            Cam.SetYaw(0);
            GameInput.SimMove = new Vector2(free.x, free.z);
            yield return new WaitForSeconds(1.0f);
            GameInput.SimMove = Vector2.zero;
            float moved = Flat(target.transform.position - p0).magnitude;
            Check(moved > 1f, $"possessed {target.DisplayName} via aim+E, camera {camDist:0.0} m, WASD moved host {moved:0.0} m");
            // far host is not possessable
            var far = NearestHost(target.transform.position + Vector3.forward * 1000f, h => h.CanBePossessed);
            if (far && Vector3.Distance(far.transform.position, target.transform.position) > 8f)
            {
                yield return AimSettle(far);
                var t2 = PM.ComputeTarget();
                Check(!(t2.kind == TargetKind.Possess && t2.host == far), "far host (>5 m) not targetable");
            }
        }

        // ---------------------------------------------------------------- 03
        static IEnumerator ScenarioRelease()
        {
            Calm();
            var host = NearestHost(PM.Active.Position, h => h.CanBePossessed && h.Profession != Profession.Guard);
            PM.ForcePossessImmediate(host);
            yield return new WaitForSeconds(0.3f);
            yield return Press(() => GameInput.SimLeave = true, () => GameInput.SimLeave = false);
            Check(host.State == HostState.Unconscious, "host unconscious after Q");
            Check(PM.Active.IsFree && PM.Active.Blob.gameObject.activeSelf, "amoeba is free");
            float t = 0; bool recovered = false;
            while (t < HostCharacter.UnconsciousSeconds + 2f)
            {
                t += Time.deltaTime;
                if (host.State == HostState.Normal) { recovered = true; break; }
                yield return null;
            }
            Check(recovered && t > HostCharacter.UnconsciousSeconds - 0.3f, $"recovered after {t:0.0} s");
            yield return new WaitForSeconds(1.5f);
            Check(host.Nav && host.Nav.enabled && host.Nav.isOnNavMesh, $"host unconscious {t:0.0} s, recovered, AI resumed on NavMesh");
        }

        // ---------------------------------------------------------------- 04
        static IEnumerator ScenarioSplit()
        {
            Calm();
            yield return Press(() => GameInput.SimSplit = true, () => GameInput.SimSplit = false);
            Check(PM.Agents.Count == 2, "split → 2 amoebas");
            yield return Press(() => GameInput.SimSplit = true, () => GameInput.SimSplit = false);
            Check(PM.Agents.Count == 2, "second split refused (max 2)");
            int before = PM.ActiveIndex;
            var inactive = PM.Agents[1 - before];
            Vector3 inactivePos = inactive.Position;
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            Check(PM.ActiveIndex != before, "Tab switched active amoeba");
            var other = PM.Agents[before];
            Vector3 otherPos = other.Position;
            Vector3 p0 = PM.Active.Position;
            Cam.SetYaw(0);
            GameInput.SimMove = new Vector2(0, 1);
            yield return new WaitForSeconds(1f);
            GameInput.SimMove = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            float moved = Flat(PM.Active.Position - p0).magnitude;
            float drift = Flat(other.Position - otherPos).magnitude;
            Check(moved > 1f, $"active moved {moved:0.0} m");
            Check(drift < 0.1f, $"inactive drift {drift:0.00} m");
            float camDist = Vector3.Distance(Cam.Cam.transform.position, PM.ActiveBody.CameraTarget.position);
            Check(camDist < 6f, $"2 amoebas max, Tab switch OK, active moved {moved:0.0} m, inactive drift {drift:0.00} m, camera on active ({camDist:0.0} m)");
            // split from a host too
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            _ = inactivePos;
        }

        // ---------------------------------------------------------------- doors
        static AccessDoor Door(EntryRoute r) => FindObjectsByType<AccessDoor>(FindObjectsSortMode.None).FirstOrDefault(d => d.route == r);

        static Vector3 OutsideOf(EntryRoute r) => r switch
        {
            EntryRoute.MainGate => CityLayout.MainGateOutside,
            EntryRoute.StaffTerminal => CityLayout.StaffTerminalPos,
            _ => CityLayout.TunnelDoorOutside,
        };

        static Vector3 InsideOf(EntryRoute r) => r switch
        {
            EntryRoute.MainGate => CityLayout.MainGateInside,
            EntryRoute.StaffTerminal => CityLayout.StaffDoorInside,
            _ => CityLayout.TunnelExitInside,
        };

        static Profession ProfessionFor(EntryRoute r) => r switch
        {
            EntryRoute.MainGate => Profession.Guard,
            EntryRoute.StaffTerminal => Profession.Scientist,
            _ => Profession.Maintenance,
        };

        static bool PathComplete(Vector3 a, Vector3 b)
        {
            var p = new NavMeshPath();
            if (!NavMesh.SamplePosition(a, out var ha, 3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out var hb, 3f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, p) && p.status == NavMeshPathStatus.PathComplete;
        }

        static MissionProp PropFor(string flag) => FindObjectsByType<MissionProp>(FindObjectsSortMode.None).FirstOrDefault(p => p.flag == flag);

        /// <summary>Teleport the active host in front of a mission prop, aim, press E and wait.</summary>
        static IEnumerator UseProp(string flag, float wait)
        {
            var prop = PropFor(flag);
            Check(prop != null, $"mission prop '{flag}' exists");
            if (!prop) yield break;
            Vector3 ip = prop.InteractPoint;
            Vector3 stand = new(ip.x, ip.y - 1.2f, ip.z);
            Vector3 back = Flat(stand - prop.transform.position);
            if (back.sqrMagnitude > 0.01f) stand += back.normalized * 0.35f;
            if (NavMesh.SamplePosition(stand, out var nh, 1.5f, NavMesh.AllAreas)) stand = nh.position;
            TeleportBody(PM.ActiveBody, stand, 0);
            Calm();
            yield return AimSettle(ip);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Interact && tg.interactable == (IInteractable)prop && tg.allowed, $"{prop.displayName}: prompt '{tg.prompt}'");
            if (!current.pass) yield break;
            yield return PressInteract();
            for (float t = 0; t < wait + 1f && !RunState.Has(flag); t += Time.deltaTime) yield return null;
            Check(RunState.Has(flag), $"{prop.displayName} done");
        }

        /// <summary>Split, put the second amoeba in another staff host, badge both readers within the window.</summary>
        static IEnumerator DualAuth()
        {
            var readers = FindObjectsByType<DualAuthReader>(FindObjectsSortMode.None).OrderBy(r => r.readerName).ToArray();
            Check(readers.Length == 2, "two readers exist");
            if (!current.pass) yield break;
            // one host can't do both
            yield return UseReader(readers[0]);
            yield return UseReaderExpectDenied(readers[1]);
            yield return new WaitForSeconds(readers[0].window + 0.3f);
            int first = PM.ActiveIndex;
            yield return Press(() => GameInput.SimSplit = true, () => GameInput.SimSplit = false);
            Check(PM.Agents.Count == 2, "split for the second badge");
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            var second = NearestHost(CityLayout.LabCorridor, h => h.CanBePossessed && AccessRules.Has(h.Permissions, AccessPermission.LabInterior));
            Check(second != null, "second staff host available in the lab");
            if (!current.pass) yield break;
            PM.ForcePossessImmediate(second);
            yield return UseReader(readers[1]);
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            Check(PM.ActiveIndex == first, "switched back to the first amoeba");
            yield return UseReader(readers[0]);
            for (float t = 0; t < 1.5f && !RunState.Has(MissionFlags.OfficeOpen); t += Time.deltaTime) yield return null;
        }

        static IEnumerator UseReader(DualAuthReader r)
        {
            Vector3 stand = new(r.transform.position.x, CityLayout.LabFloor, r.transform.position.z - 1.1f);
            TeleportBody(PM.ActiveBody, stand, 0);
            Calm();
            yield return AimSettle(r.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Interact && tg.interactable == (IInteractable)r && tg.allowed, $"{r.readerName}: '{tg.prompt}'");
            if (!current.pass) yield break;
            yield return PressInteract();
        }

        static IEnumerator UseReaderExpectDenied(DualAuthReader r)
        {
            Vector3 stand = new(r.transform.position.x, CityLayout.LabFloor, r.transform.position.z - 1.1f);
            TeleportBody(PM.ActiveBody, stand, 0);
            yield return AimSettle(r.InteractPoint);
            var ctx = new InteractContext { agent = PM.Active, host = PM.Active.Host, permissions = PM.Active.Host.Permissions, position = stand };
            Check(!r.CanInteract(ctx, out var why), $"same amoeba can't badge both readers ({why})");
        }

        // ---------------------------------------------------------------- 13
        static IEnumerator ScenarioCameraAbilities()
        {
            var cam = SecurityCamera.All.FirstOrDefault(c => c && c.cameraName.Contains("Tunnel"));
            Check(cam != null, "tunnel-alley camera exists");
            if (!cam) yield break;
            Calm();
            // Hold the sweep still so occlusion and power are tested against the same sightline.
            cam.sweepDegrees = 0f;
            yield return null;
            foreach (var h in HostCharacter.All) if (h && h.IsAwakeNpc && Vector3.Distance(h.transform.position, cam.transform.position) < 25f) TeleportNpc(h, CityLayout.PlazaSpot, 0);
            Vector3 seen = cam.head.position + Flat(cam.head.forward).normalized * 6f; seen.y = CityLayout.Sidewalk;
            if (NavMesh.SamplePosition(seen, out var nh, 3f, NavMesh.AllAreas)) seen = nh.position;
            TeleportBody(PM.ActiveBody, seen, 0);
            // A real collider must occlude CCTV; a camera cannot see through walls.
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "AutoTest_CCTV_Occluder";
            cover.transform.position = Vector3.Lerp(cam.head.position, PM.Active.Blob.Center, 0.5f);
            cover.transform.rotation = Quaternion.LookRotation(Flat(cam.head.forward));
            cover.transform.localScale = new Vector3(3f, 5f, 0.5f);
            Physics.SyncTransforms();
            try
            {
                yield return new WaitForSeconds(0.8f);
                Check(SuspicionSystem.Instance.Value < 0.01f && !cam.SeesSomething, "wall blocks the camera's view of the blob");
            }
            finally { Destroy(cover); }
            yield return null;
            float t = 0;
            while (t < 6f && SuspicionSystem.Instance.Value < 3f) { t += Time.deltaTime; yield return null; }
            Check(SuspicionSystem.Instance.Value >= 3f && cam.SeesSomething, $"camera saw the exposed blob (suspicion {SuspicionSystem.Instance.Value:0})");
            var maint = NearestHost(cam.transform.position, h => h.CanBePossessed && h.Profession == Profession.Maintenance);
            Check(maint != null, "maintenance worker available");
            if (!maint) yield break;
            PM.ForcePossessImmediate(maint);
            TeleportBody(PM.ActiveBody, seen, 0);
            yield return Press(() => GameInput.SimAbility = true, () => GameInput.SimAbility = false);
            Check(cam.Disabled, "maintenance 'Cut the power' disabled the camera");
            float cd = AbilitySystem.Instance.CooldownRemaining(Profession.Maintenance);
            Check(cd > 0f, $"ability on cooldown ({cd:0}s)");
            Check(!AbilitySystem.Instance.TryUse(maint), "cooldown rejects a second power cut");
            PM.LeaveHost();
            // Move the unconscious worker outside every camera view so only the free blob is under test.
            TeleportNpc(maint, new Vector3(0, -80f, 0), 0);
            TeleportBody(PM.ActiveBody, seen, 0);
            Calm();
            yield return new WaitForSeconds(0.8f);
            Check(SuspicionSystem.Instance.Value < 0.01f && !cam.SeesSomething, "disabled camera cannot raise suspicion from the exposed blob");
            var sci = NearestHost(PM.Active.Position, h => h.CanBePossessed && h.Profession == Profession.Scientist);
            Check(sci != null, "scientist available");
            if (!sci) yield break;
            PM.ForcePossessImmediate(sci);
            SuspicionSystem.Instance.SetValue(50f);
            yield return Press(() => GameInput.SimAbility = true, () => GameInput.SimAbility = false);
            Check(SuspicionSystem.Instance.Value <= 21f, $"scientist reassurance lowered suspicion to {SuspicionSystem.Instance.Value:0}");
            // Reassurance must not cancel a live pursuit.
            SuspicionSystem.Instance.SetValue(95f);
            float before = SuspicionSystem.Instance.Value;
            Check(!SuspicionSystem.Instance.Reassure(30f) && SuspicionSystem.Instance.Value == before, "scientist cannot reassure guards during pursuit");
            Check(!AbilitySystem.Instance.TryUse(null) && !AbilitySystem.Instance.TryUse(maint), "abilities reject free and inactive hosts");
        }

        // ---------------------------------------------------------------- 12
        static IEnumerator ScenarioEarlyMissions()
        {
            var mm = MissionManager.Instance;
            Check(mm != null && RunState.MissionIndex == 0, "campaign starts at mission 1");
            bool ok = false;
            yield return WalkTo(CityLayout.AlleyMouth + Vector3.forward * 2f, 40f, r => ok = r);
            yield return null;
            Check(RunState.Has(MissionFlags.AlleyExit), "left the storm-drain alley");
            var civ = NearestHost(PM.Active.Position, h => h.CanBePossessed && h.Profession == Profession.Civilian);
            PM.ForcePossessImmediate(civ);
            yield return null; yield return null;
            Check(RunState.MissionIndex == 1, $"mission 1 complete → mission {RunState.MissionIndex + 1}");
            Check(mm.Waypoints.Count >= 1, $"waypoints shown ({mm.Waypoints.Count})");
            yield return UseProp(MissionFlags.NewsRead, 3f);
            var cafe = CityLayout.CafeSpot + new Vector3(2.2f, 0, 0);
            if (NavMesh.SamplePosition(cafe, out var nh, 2f, NavMesh.AllAreas)) cafe = nh.position;
            TeleportBody(PM.ActiveBody, cafe, 0);
            Calm();
            for (float t = 0; t < 14f && !RunState.Has(MissionFlags.Eavesdropped); t += Time.deltaTime) { Calm(); yield return null; }
            Check(RunState.Has(MissionFlags.Eavesdropped), "eavesdropped on Dr. Petrov");
            yield return null;
            Check(RunState.MissionIndex == 2, $"alley exit, first host, newsstand read, eavesdrop done → now mission {RunState.MissionIndex + 1}/{mm.Missions.Count} ({mm.Current?.title})");
        }

        // ---------------------------------------------------------------- 05
        static IEnumerator ScenarioCivilianDenied()
        {
            Calm();
            var civ = NearestHost(CityLayout.MainGateOutside, h => h.CanBePossessed && h.Profession == Profession.Civilian);
            Check(civ != null, "civilian exists");
            if (!civ) yield break;
            PM.ForcePossessImmediate(civ);
            int deniedCount = 0;
            foreach (var r in new[] { EntryRoute.MainGate, EntryRoute.StaffTerminal, EntryRoute.ServiceTunnel })
            {
                TeleportBody(PM.ActiveBody, OutsideOf(r), 0);
                yield return new WaitForSeconds(0.2f);
                IInteractable it = r == EntryRoute.StaffTerminal ? FindFirstObjectByType<StaffTerminal>() : Door(r);
                yield return AimSettle(it.InteractPoint);
                var tg = PM.ComputeTarget();
                Check(tg.kind == TargetKind.Interact && tg.interactable == it, $"{r}: prompt shown ({tg.prompt})");
                Check(!tg.allowed, $"{r}: shown as denied");
                yield return PressInteract();
                yield return new WaitForSeconds(0.3f);
                var d = Door(r);
                Check(d && !d.IsOpen, $"{r}: door stays closed for civilian");
                if (d && !d.IsOpen) deniedCount++;
            }
            Check(deniedCount == 3, "civilian denied at Main Gate, Staff Terminal and Service Tunnel");
        }

        // ---------------------------------------------------------------- 06-08
        static IEnumerator ScenarioRoute(EntryRoute route)
        {
            Calm();
            var prof = ProfessionFor(route);
            var door = Door(route);
            Check(door != null, $"door for {route} exists");
            if (!door) yield break;
            Vector3 outside = OutsideOf(route), inside = InsideOf(route);
            Check(!PathComplete(outside, CityLayout.ValeOffice) || route == EntryRoute.None, "closed door blocks the NavMesh path");
            var host = NearestHost(outside, h => h.CanBePossessed && h.Profession == prof);
            Check(host != null, $"{prof} available");
            if (!host) yield break;
            PM.ForcePossessImmediate(host);
            // Missions 2-3 for real: newsstand + Petrov's call, then tail her to the dead drop.
            yield return CityLeads();
            if (!current.pass) yield break;
            yield return TailPetrov();
            if (!current.pass) yield break;
            Check(RunState.MissionIndex == 3, $"dead drop taken → mission {RunState.MissionIndex + 1} ({MissionManager.Instance.Current?.title})");
            if (route == EntryRoute.StaffTerminal) { yield return UseProp(MissionFlags.Keycard, 0.3f); Check(RunState.Has(MissionFlags.Keycard), "keycard taken"); }
            if (route == EntryRoute.ServiceTunnel) { yield return UseProp(MissionFlags.Power, 3.6f); Check(RunState.Has(MissionFlags.Power), "substation power restored"); }
            if (!current.pass) yield break;
            TeleportBody(PM.ActiveBody, outside, 0);
            yield return new WaitForSeconds(0.3f);
            IInteractable it = route == EntryRoute.StaffTerminal ? FindFirstObjectByType<StaffTerminal>() : door;
            yield return AimSettle(it.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Interact && tg.allowed, $"allowed prompt: {tg.prompt}");
            yield return PressInteract();
            yield return new WaitForSeconds(door.openSeconds + 0.5f);
            Check(door.IsOpen, $"{door.doorName} opened by {prof}");
            Check(RunState.Route == route, $"route recorded = {RunState.Route}");
            Calm();
            bool ok = false;
            yield return WalkTo(inside, 60f, r => ok = r);
            Check(ok, $"walked through {door.doorName} to inside point");
            bool reached = false;
            for (float t = 0; t < 2f && !reached; t += Time.deltaTime) { reached = RunState.LabEntered; yield return null; }
            Check(RunState.LabEntered, "lab entered (LabInteriorZone)");
            Calm();
            Check(!PathComplete(CityLayout.LabCorridor, CityLayout.ValeOffice), "office door closed (two-person rule)");
            yield return UseProp(MissionFlags.DataA, 4.6f);
            yield return UseProp(MissionFlags.DataB, 4.6f);
            Check(RunState.Has(MissionFlags.Research), "research downloaded from Lab A + Lab B");
            if (!current.pass) yield break;
            yield return DualAuth();
            Check(RunState.Has(MissionFlags.OfficeOpen), "two-person rule satisfied with a split amoeba");
            if (!current.pass) yield break;
            yield return new WaitForSeconds(1.2f);
            Calm();
            yield return WalkTo(CityLayout.ValeOffice, 60f, r => ok = r, 0.6f);
            Check(ok, "walked to Dr. Vale's office");
            var vale = FindFirstObjectByType<DrValeInteraction>();
            Check(vale != null, "Dr. Vale exists");
            if (!vale) yield break;
            yield return AimSettle(vale.InteractPoint);
            bool ended = false; EndingData data = default;
            GameEvents.Ending += d => { ended = true; data = d; };
            var t3 = PM.ComputeTarget();
            Check(t3.kind == TargetKind.Interact && t3.interactable == (IInteractable)vale, $"Vale prompt: {t3.prompt}");
            // Initiate the reveal with a real pursuer almost within capture range.
            var storyGuard = NearestHost(PM.Active.Position, h => h.IsAwakeNpc && h.Brain is GuardAI);
            Check(storyGuard != null, "nearby guard available to exercise dialogue safety under pursuit");
            if (!storyGuard) yield break;
            Vector3 guardSpot = PM.Active.Position + Vector3.right * 2.5f;
            if (NavMesh.SamplePosition(guardSpot, out var guardHit, 1f, NavMesh.AllAreas)) guardSpot = guardHit.position;
            TeleportNpc(storyGuard, guardSpot, 270f);
            var ss = SuspicionSystem.Instance;
            ss.ReportDirect(ViolationType.UnauthorizedAccess, PM.ActiveBody.EyePosition,
                PM.Active.Host ? (Component)PM.Active.Host : PM.Active.Blob, 10f, "AutoTest witness");
            ss.SetValue(95f);
            int capturesBeforeStory = RunState.Captures, resetsBeforeStory = RunState.Resets;
            var bodyBeforeStory = PM.ActiveBody;
            yield return PressInteract();
            GameManager.Instance.ResetToCheckpoint("AutoTest: protected Vale story must reject reset requests");
            Vector3 frozenGuard = storyGuard.transform.position;
            yield return new WaitForSecondsRealtime(0.7f);
            Check(GameManager.Instance.State == GameState.Ending && Time.timeScale == 0f &&
                RunState.Captures == capturesBeforeStory && RunState.Resets == resetsBeforeStory && PM.ActiveBody == bodyBeforeStory &&
                Vector3.Distance(storyGuard.transform.position, frozenGuard) < 0.02f,
                "Vale dialogue freezes the nearby pursuer and prevents capture/checkpoint reset while input is locked");
            yield return SkipStoryUntil(() => RunState.Has(MissionFlags.Confronted), 20f);
            Check(RunState.Has(MissionFlags.Confronted) && !ended && !MissionManager.Instance.EndingStarted && GameManager.Instance.IsPlaying && Time.timeScale == 1f,
                "meeting Vale resumes gameplay instead of ending the campaign");
            Check(MissionManager.Instance.Missions.Count == 14 && RunState.MissionIndex == 6, "HUNTER PROTOCOL follows Vale's reveal in the fourteen-mission campaign");
            if (!current.pass) yield break;
            Calm();
            yield return HunterProtocolRoute();
            if (!current.pass) yield break;
            Calm();
            yield return PostValeCampaign();
            // The checkpoint persistence check reloads CityDirector, which intentionally clears event subscribers.
            GameEvents.Ending += d => { ended = true; data = d; };
            yield return SkipStoryUntil(() => UI.LakesideEpilogue.Instance, 15f);
            var epilogue = UI.LakesideEpilogue.Instance;
            Check(epilogue && !ended && GameManager.Instance.State == GameState.Ending && Time.timeScale == 0f,
                "the drain leads to the protected lakeside epilogue, without publishing final statistics");
            if (!epilogue) yield break;
            epilogue.RevealChoices();
            for (float t = 0; t < 5f && !epilogue.ChoicesReady; t += Time.unscaledDeltaTime) yield return null;
            Check(epilogue.ChoicesReady, "both final life choices are presented to the player");
            epilogue.FinishResolution();
            yield return new WaitForSecondsRealtime(0.7f);
            Check(!ended && GameManager.Instance.State == GameState.Ending,
                "waiting or skipping before an explicit choice cannot select an ending or publish statistics");
            var outcome = route == EntryRoute.ServiceTunnel ? EndingOutcome.Dominion : EndingOutcome.Peace;
            epilogue.Choose(outcome);
            // A second input cannot silently replace the player's committed choice.
            epilogue.Choose(outcome == EndingOutcome.Peace ? EndingOutcome.Dominion : EndingOutcome.Peace);
            yield return new WaitForSecondsRealtime(0.9f);
            epilogue.FinishResolution();
            for (float t = 0; t < 5f && !ended; t += Time.unscaledDeltaTime) yield return null;
            Check(ended && data.route == route && data.outcome == outcome && RunState.Has(MissionFlags.Escaped) &&
                GameManager.Instance.State == GameState.Ended && Time.timeScale == 0f,
                $"{prof} → tail → {door.doorName} → Vale reveal → hunters → signed evidence → relay sabotage → shadow uplink → black site vault → protocol epsilon → blackout → master broadcast → drain → lakeside choice {outcome} (route {data.route}, {data.timeSeconds:0}s)");
        }

        // Uses real-time input pulses, so story protection is never bypassed by changing timeScale.
        static IEnumerator SkipStoryUntil(Func<bool> done, float timeout)
        {
            float start = Time.realtimeSinceStartup, nextPress = start + 0.65f;
            while (!done() && Time.realtimeSinceStartup - start < timeout)
            {
                bool press = Time.realtimeSinceStartup >= nextPress;
                GameInput.SimInteract = press;
                if (press) nextPress = Time.realtimeSinceStartup + 0.65f;
                yield return null;
            }
            GameInput.SimInteract = false;
            yield return null;
        }

        static InteractContext ActiveContext() => new()
        {
            agent = PM.Active, host = PM.Active.Host,
            permissions = PM.Active.Host ? PM.Active.Host.Permissions : AccessPermission.None,
            position = PM.Active.Position,
        };

        static void TakeProfession(Profession profession)
        {
            if (PM.Active.Host && PM.Active.Host.Profession == profession) return;
            var h = NearestHost(PM.Active.Position, candidate => candidate.CanBePossessed && candidate.Profession == profession);
            Check(h != null, $"{profession} host available for the post-Vale campaign");
            if (h) PM.ForcePossessImmediate(h);
        }

        static IEnumerator UseCampaignInteractable(IInteractable target, Vector3 groundPosition)
        {
            Vector3 stand = groundPosition + Vector3.back * 1.05f;
            if (NavMesh.SamplePosition(stand, out var nh, 1f, NavMesh.AllAreas)) stand = nh.position;
            TeleportBody(PM.ActiveBody, stand, 0f);
            Calm();
            yield return AimSettle(target.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Interact && tg.interactable == target && tg.allowed, $"post-Vale interaction: {tg.prompt}");
            if (!current.pass) yield break;
            yield return PressInteract();
        }

        static IEnumerator FinishCampaignOperation(CampaignOperation op)
        {
            yield return UseCampaignInteractable(op, op.transform.position);
            for (float t = 0; t < op.seconds + 2f && !RunState.Has(op.flag); t += Time.deltaTime) { Calm(); yield return null; }
            Check(RunState.Has(op.flag), $"{op.displayName} completed through real input and held controls");
        }

        static IEnumerator PostValeCampaign()
        {
            var coolant = CooperativeConsole.Find("coolant");
            var dispatch = CooperativeConsole.Find("dispatch");
            var operations = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None);
            var archive = operations.FirstOrDefault(o => o.flag == MissionFlags.SignedArchive);
            var relay = operations.FirstOrDefault(o => o.flag == MissionFlags.ContainmentOffline);
            var uplink = operations.FirstOrDefault(o => o.flag == MissionFlags.EvidenceBroadcast);
            var exit = FindFirstObjectByType<CampaignExtraction>();
            Check(coolant && dispatch && archive && relay && uplink && exit, "all post-Vale missions have real world interactables");
            if (!current.pass) yield break;
            Check(!exit.CanInteract(ActiveContext(), out _), "cannot escape before broadcasting the evidence");
            Check(PathComplete(CityLayout.ValeOffice, MissionLayout.SignedArchive + Vector3.back) &&
                  PathComplete(CityLayout.ServiceRoom, MissionLayout.CoolantBypass + Vector3.back), "archive and coolant controls are reachable on the lab NavMesh");

            TakeProfession(Profession.Scientist);
            Check(!archive.CanInteract(ActiveContext(), out _), "archive refuses a lone scientist without coolant support");
            TakeProfession(Profession.Maintenance);
            int supportIndex = PM.ActiveIndex;
            yield return UseCampaignInteractable(coolant, coolant.transform.position);
            var coolantHost = PM.Active.Host;
            Vector3 heldPosition = coolantHost.transform.position;
            Check(coolant.Held, "maintenance physically holds the coolant bypass");
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            TakeProfession(Profession.Scientist);
            yield return UseCampaignInteractable(archive, archive.transform.position);
            yield return new WaitForSeconds(0.5f);
            TeleportBody(coolantHost, heldPosition + Vector3.back * 5f, 0f);
            yield return null; yield return null;
            Check(!archive.Busy && !RunState.Has(MissionFlags.SignedArchive), "archive copy interrupts when the second body leaves coolant");
            TeleportBody(coolantHost, heldPosition, 0f);
            yield return FinishCampaignOperation(archive);
            Check(RunState.MissionIndex == 8 && GameManager.Instance.IsPlaying, "signed evidence advances to relay sabotage");

            TakeProfession(Profession.Guard);
            yield return UseCampaignInteractable(dispatch, dispatch.transform.position);
            Check(dispatch.Held, "guard holds a false dispatch at the lobby console");
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            Check(PM.ActiveIndex == supportIndex, "switches back to the independent maintenance body");
            TakeProfession(Profession.Maintenance);
            Check(PathComplete(PM.Active.Position, relay.transform.position + Vector3.back), "containment relay can be reached from the service room");
            yield return FinishCampaignOperation(relay);
            Check(RunState.MissionIndex == 9 && SecurityCamera.All.All(c => !c || c.Disabled), "relay overload permanently disables all CCTV and starts shadow uplink mission");
            Check(FindObjectsByType<AccessDoor>(FindObjectsSortMode.None).Where(d => d.route != EntryRoute.None).All(d => d.IsOpen), "relay overload unlocks all campus escape routes");
            if (!current.pass) yield break;

            // A death/checkpoint reload must preserve story/world progress, without restoring a finished ending.
            var oldDirector = CityDirector.Instance;
            SceneManager.LoadScene(GameManager.CityScene);
            yield return null;
            for (float t = 0; t < 20f && (!CityDirector.Instance || CityDirector.Instance == oldDirector || !CityDirector.Instance.Ready); t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            GameInput.Simulated = true; ClearSim();
            Check(RunState.Has(MissionFlags.SignedArchive) && RunState.Has(MissionFlags.ContainmentOffline) && RunState.MissionIndex == 9,
                "checkpoint reload preserves both completed post-Vale missions");
            Check(SecurityCamera.All.All(c => !c || c.Disabled) && !MissionManager.Instance.EndingStarted,
                "checkpoint reload restores disabled CCTV and ongoing gameplay");
            uplink = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).First(o => o.flag == MissionFlags.EvidenceBroadcast);
            exit = FindFirstObjectByType<CampaignExtraction>();
            TakeProfession(Profession.Civilian);
            SuspicionSystem.Instance.SetValue(95f);
            Check(!uplink.CanInteract(ActiveContext(), out _), "journalist uplink refuses to expose a contact during pursuit");
            Calm();
            yield return FinishCampaignOperation(uplink);
            Check(RunState.Has(MissionFlags.EvidenceBroadcast) && RunState.MissionIndex == 10, "shadow uplink broadcast complete → black site archive");

            // M9: THE BLACK SITE ARCHIVE
            var subvault = CooperativeConsole.Find(MissionFlags.SubVaultDiversion);
            var blacksite = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).First(o => o.flag == MissionFlags.BlackSiteArchive);
            Check(subvault && blacksite, "Mission 9 sub-vault diversion and black site vault exist");
            if (!current.pass) yield break;
            if (PM.Agents.Count < 2)
            {
                yield return Press(() => GameInput.SimSplit = true, () => GameInput.SimSplit = false);
                Check(PM.Agents.Count == 2, "split for dual-amoeba campaign operations");
                if (!current.pass) yield break;
            }
            TakeProfession(Profession.Guard);
            yield return UseCampaignInteractable(subvault, subvault.transform.position);
            Check(subvault.Held, "guard holds sub-vault diversion at lobby console");
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            TakeProfession(Profession.Scientist);
            yield return FinishCampaignOperation(blacksite);
            Check(RunState.Has(MissionFlags.BlackSiteArchive) && RunState.MissionIndex == 11, "black site archive downloaded → protocol epsilon");

            // M10: CONTAINMENT PROTOCOL EPSILON
            var backupGen = CooperativeConsole.Find(MissionFlags.BackupGenerator);
            var gridOverride = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).First(o => o.flag == MissionFlags.GridOverride);
            Check(backupGen && gridOverride, "Mission 10 backup generator and grid override exist");
            if (!current.pass) yield break;
            TakeProfession(Profession.Maintenance);
            yield return UseCampaignInteractable(backupGen, backupGen.transform.position);
            Check(backupGen.Held, "maintenance holds auxiliary backup generator");
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            TakeProfession(Profession.Guard);
            yield return FinishCampaignOperation(gridOverride);
            Check((RunState.Has(MissionFlags.GridOverride) || RunState.Has(MissionFlags.ProtocolEpsilonBreached)) && RunState.MissionIndex == 12, "grid override completed → blackout");

            // M13: BLACKOUT
            yield return BlackoutRoute();
            if (!current.pass) yield break;

            // M11: THE FINAL CONVERGENCE
            var masterUplink = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).First(o => o.flag == MissionFlags.MasterBroadcast);
            exit = FindFirstObjectByType<CampaignExtraction>();
            Check(masterUplink && exit, "Mission 11 master uplink and storm drain extraction exist");
            if (!current.pass) yield break;
            TakeProfession(Profession.Civilian);
            SuspicionSystem.Instance.SetValue(95f);
            Check(!masterUplink.CanInteract(ActiveContext(), out _), "master antenna refuses to broadcast during pursuit");
            Calm();
            yield return FinishCampaignOperation(masterUplink);
            Check(RunState.Has(MissionFlags.MasterBroadcast), "master broadcast completed");
            Check(!exit.CanInteract(ActiveContext(), out _), "a human disguise cannot fit through the final drain");
            PM.LeaveHost();
            TeleportBody(PM.ActiveBody, CityLayout.AlleyMouth + Vector3.back * 3f, 180f);
            Calm();
            bool reached = false;
            yield return WalkTo(MissionLayout.Extraction + Vector3.forward, 30f, ok => reached = ok, 0.7f);
            Check(reached, "walks the final alley to the original storm-drain extraction");
            yield return AimSettle(exit.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.interactable == (IInteractable)exit && tg.allowed, $"final extraction prompt: {tg.prompt}");
            int finalCaptures = RunState.Captures, finalResets = RunState.Resets;
            var finalDirector = CityDirector.Instance;
            yield return PressInteract();
            GameManager.Instance.ResetToCheckpoint("AutoTest: protected finale must reject reset requests");
            yield return new WaitForSecondsRealtime(0.7f);
            Check(MissionManager.Instance.EndingStarted && !RunState.Has(MissionFlags.Escaped) &&
                  GameManager.Instance.State == GameState.Ending && Time.timeScale == 0f &&
                  RunState.Captures == finalCaptures && RunState.Resets == finalResets && CityDirector.Instance == finalDirector,
                  "final dialogue freezes simulation without prematurely committing escape or resetting the checkpoint");
        }


        // ================================================================ THE TAIL · HUNTER PROTOCOL · BLACKOUT
        static readonly string[] CampaignOrder =
        {
            MissionFlags.AlleyExit, MissionFlags.FirstHost, MissionFlags.NewsRead, MissionFlags.Eavesdropped, MissionFlags.TailDone,
            MissionFlags.DeadDrop, MissionFlags.LabEntered, MissionFlags.DataA, MissionFlags.DataB, MissionFlags.OfficeOpen,
            MissionFlags.Confronted, MissionFlags.HunterGate, MissionFlags.HuntersDown, MissionFlags.SignedArchive,
            MissionFlags.ContainmentOffline, MissionFlags.EvidenceBroadcast, MissionFlags.SubVaultDiversion, MissionFlags.BlackSiteArchive,
            MissionFlags.BackupGenerator, MissionFlags.GridOverride, MissionFlags.ProtocolEpsilonBreached,
        };

        /// <summary>Focused-scenario setup only: marks earlier chapters complete so the mechanic under test is current.</summary>
        static void AdvanceCampaignTo(string last)
        {
            foreach (var f in CampaignOrder)
            {
                if (f == MissionFlags.LabEntered) RunState.LabEntered = true;
                MissionManager.SetFlag(f);
                if (f == last) break;
            }
        }

        static IEnumerator ReloadCheckpoint()
        {
            var oldDirector = CityDirector.Instance;
            SceneManager.LoadScene(GameManager.CityScene);
            yield return null;
            for (float t = 0; t < 20f && (!CityDirector.Instance || CityDirector.Instance == oldDirector || !CityDirector.Instance.Ready); t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            GameInput.Simulated = true; ClearSim();
        }

        static Vector3 Ground(Vector3 p, float radius = 1.5f)
        {
            p.y = CityLayout.Sidewalk;
            return NavMesh.SamplePosition(p, out var h, radius, NavMesh.AllAreas) ? h.position : p;
        }

        /// <summary>Mission 2 for real: read the newsstand, then overhear Petrov's call at the café as a host.</summary>
        static IEnumerator CityLeads()
        {
            yield return UseProp(MissionFlags.NewsRead, 3f);
            if (!current.pass) yield break;
            TeleportBody(PM.ActiveBody, Ground(CityLayout.CafeSpot + new Vector3(2.2f, 0, 0), 2f), 0);
            for (float t = 0; t < 14f && !RunState.Has(MissionFlags.Eavesdropped); t += Time.deltaTime) { Calm(); yield return null; }
            Check(RunState.Has(MissionFlags.Eavesdropped), "overheard Dr. Petrov's call at the café");
        }

        /// <summary>Back away from the tail target to a comfortable distance before she sets off.</summary>
        static IEnumerator BackOff(TailTarget tail, float distance)
        {
            Vector3 away = Flat(PM.Active.Position - tail.TargetPosition);
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            Vector3 spot = Ground(tail.TargetPosition + away.normalized * distance, 3f);
            bool ok = false;
            yield return WalkTo(spot, 10f, r => ok = r, 1f);
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
        }

        /// <summary>
        /// Follow the tail target with simulated WASD along NavMesh paths, like a careful player: close in when she gets
        /// past ~11 m, stop at ~7.5 m, freeze whenever she glances back or pauses, and step away if she comes too close.
        /// </summary>
        static IEnumerator FollowTail(Func<bool> until, float timeout, Action<bool> done, float near = 7.5f, float far = 11f)
        {
            var tail = TailTarget.Instance;
            var path = new NavMeshPath();
            Vector3[] corners = Array.Empty<Vector3>();
            int ci = 0; float repath = 0f, stuckT = 0f;
            bool moving = false;
            Vector3 lastPos = PM.ActiveBody.Root.position;
            Cam.SetYaw(0f);
            for (float t = 0; t < timeout; t += Time.deltaTime)
            {
                if (until()) { GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false; done(true); yield break; }
                Calm();
                var body = PM.ActiveBody;
                Vector3 pos = body.Root.position, target = tail.TargetPosition;
                float d = Flat(target - pos).magnitude;
                Cam.SetYaw(0f);
                if (d < TailRules.TooClose + 1f && tail.State != TailState.DoublingBack)
                {
                    Vector3 back = Flat(pos - target).normalized;
                    GameInput.SimMove = new Vector2(back.x, back.z); GameInput.SimSprint = false;
                    moving = false;
                    yield return null;
                    continue;
                }
                bool freeze = tail.Glancing || tail.State == TailState.Preparing || tail.State == TailState.Dropping ||
                              (tail.State == TailState.Pausing && d < far + 2f);
                if (freeze || d < near) moving = false;
                else if (d > far) moving = true;
                if (!moving)
                {
                    GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
                    stuckT = 0f; lastPos = pos;
                    yield return null;
                    continue;
                }
                repath -= Time.deltaTime;
                if (repath <= 0f)
                {
                    repath = 0.5f;
                    NavMesh.SamplePosition(pos, out var a, 3f, NavMesh.AllAreas);
                    NavMesh.SamplePosition(target, out var b, 3f, NavMesh.AllAreas);
                    if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 0) { corners = path.corners; ci = 0; }
                    else { corners = new[] { target }; ci = 0; }
                }
                while (ci < corners.Length - 1 && Flat(corners[ci] - pos).magnitude < 0.5f) ci++;
                Vector3 dir = Flat((ci < corners.Length ? corners[ci] : target) - pos).normalized;
                GameInput.SimMove = new Vector2(dir.x, dir.z);
                GameInput.SimSprint = d > 17f;
                if ((pos - lastPos).magnitude < 0.02f) { stuckT += Time.deltaTime; if (stuckT > 0.8f) { GameInput.SimJump = true; stuckT = 0f; } }
                else stuckT = 0f;
                lastPos = pos;
                yield return null;
                GameInput.SimJump = false;
            }
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
            done(false);
        }

        /// <summary>
        /// Real aim + [E] possession of a nearby human: approach to ~2.6 m on the side away from <paramref name="avoid"/>
        /// (an interactable whose prompt would otherwise take priority), aim at the chest and press E.
        /// </summary>
        static IEnumerator PossessNearby(HostCharacter target, Vector3 avoid)
        {
            for (int attempt = 0; attempt < 4 && target && PM.Active.Host != target; attempt++)
            {
                Vector3 away = Flat(target.transform.position - avoid);
                if (away.sqrMagnitude < 0.01f) away = Flat(PM.Active.Position - target.transform.position);
                Vector3 stand = Ground(target.transform.position + away.normalized * 2.6f, 2f);
                bool ok = false;
                yield return WalkTo(stand, 10f, r => ok = r, 0.5f);
                yield return AimSettle(target);
                var tg = PM.ComputeTarget();
                if (tg.kind != TargetKind.Possess || tg.host != target) { Note($"possess attempt {attempt}: {tg.kind} '{tg.prompt}'"); continue; }
                yield return PressInteract();
                for (float t = 0; t < 2f && PM.Active.Host != target; t += Time.deltaTime) yield return null;
            }
            Check(target && PM.Active.Host == target, $"possessed {(target ? target.DisplayName : "?")} with aim + E");
        }

        /// <summary>WalkTo through a sequence of points (e.g. the centre of a doorway first).</summary>
        static IEnumerator WalkVia(Action<bool> done, params Vector3[] points)
        {
            bool ok = true;
            foreach (var p in points)
            {
                bool leg = false;
                yield return WalkTo(p, 15f, r => leg = r, 0.6f);
                if (!leg) { ok = false; break; }
            }
            done(ok);
        }

        /// <summary>Chase a moving point along NavMesh paths (sprinting), holding position once within <paramref name="stop"/> m.</summary>
        static IEnumerator Approach(Func<Vector3> target, float stop, float timeout, Func<bool> until)
        {
            var path = new NavMeshPath();
            Vector3[] corners = Array.Empty<Vector3>();
            int ci = 0; float repath = 0f, stuckT = 0f;
            Vector3 lastPos = PM.ActiveBody.Root.position;
            for (float t = 0; t < timeout && !until(); t += Time.deltaTime)
            {
                Calm();
                Cam.SetYaw(0f);
                Vector3 pos = PM.ActiveBody.Root.position, goal = target();
                if (Flat(goal - pos).magnitude <= stop) { GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false; yield return null; continue; }
                repath -= Time.deltaTime;
                if (repath <= 0f)
                {
                    repath = 0.4f;
                    NavMesh.SamplePosition(pos, out var a, 3f, NavMesh.AllAreas);
                    NavMesh.SamplePosition(goal, out var b, 3f, NavMesh.AllAreas);
                    if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 0) { corners = path.corners; ci = 0; }
                    else { corners = new[] { goal }; ci = 0; }
                }
                while (ci < corners.Length - 1 && Flat(corners[ci] - pos).magnitude < 0.5f) ci++;
                Vector3 dir = Flat((ci < corners.Length ? corners[ci] : goal) - pos).normalized;
                GameInput.SimMove = new Vector2(dir.x, dir.z);
                GameInput.SimSprint = true;
                if ((pos - lastPos).magnitude < 0.02f) { stuckT += Time.deltaTime; if (stuckT > 0.8f) { GameInput.SimJump = true; stuckT = 0f; } }
                else stuckT = 0f;
                lastPos = pos;
                yield return null;
                GameInput.SimJump = false;
            }
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
        }

        /// <summary>THE TAIL for real: follow Petrov from the café to the alley, then take her envelope from the dead drop.</summary>
        static IEnumerator TailPetrov()
        {
            var tail = TailTarget.Instance;
            Check(tail && tail.Target, "Petrov's tail controller and its target exist");
            if (!current.pass) yield break;
            for (float t = 0; t < 2f && tail.State == TailState.Dormant; t += Time.deltaTime) yield return null;
            Check(tail.State == TailState.Preparing && RunState.MissionIndex == 2, $"THE TAIL starts as mission 3 once both leads are in ({tail.State})");
            if (!current.pass) yield break;
            yield return BackOff(tail, 9f);
            int failures = tail.Failures;
            float peak = 0f;
            bool done = false;
            yield return FollowTail(() => { peak = Mathf.Max(peak, tail.Alertness); return RunState.Has(MissionFlags.TailDone); }, 150f, ok => done = ok);
            Check(done && RunState.Has(MissionFlags.TailLeg1) && RunState.Has(MissionFlags.TailLeg2),
                $"tailed Petrov via the bus stop and the alley mouth to her dead drop ({tail.State}, {tail.Distance:0} m)");
            Check(tail.Failures == failures, $"Petrov never made the careful follower (peak alertness {peak:0}%)");
            if (!current.pass) yield break;
            var drop = PropFor(MissionFlags.DeadDrop);
            for (float t = 0; t < 8f && Flat(tail.TargetPosition - MissionLayout.DeadDrop).magnitude < 6f; t += Time.deltaTime) { Calm(); yield return null; }
            Check(drop && drop.Revealed && drop.IsAvailable, "her envelope appears in the junction box once she walks off");
            if (!current.pass) yield break;
            bool reached = false;
            yield return WalkTo(Ground(new Vector3(-34.5f, 0f, MissionLayout.DeadDrop.z)), 25f, ok => reached = ok, 0.6f);
            Check(reached, "walked up to the dead drop behind the dumpster");
            yield return AimSettle(drop.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Interact && tg.interactable == (IInteractable)drop && tg.allowed, $"dead drop prompt: {tg.prompt}");
            if (!current.pass) yield break;
            yield return PressInteract();
            for (float t = 0; t < 1f && !RunState.Has(MissionFlags.DeadDrop); t += Time.deltaTime) yield return null;
            Check(RunState.Has(MissionFlags.DeadDrop), $"took Petrov's envelope (tail peak alertness {peak:0}%)");
            Note($"tail complete: peak alertness {peak:0}%, failures {tail.Failures - failures}");
        }

        // ---------------------------------------------------------------- 14
        static IEnumerator ScenarioTail()
        {
            var tail = TailTarget.Instance;
            Check(tail && tail.Target && tail.State == TailState.Dormant, "Petrov stays at the café until the tail begins");
            if (!current.pass) yield break;
            var civ = NearestHost(CityLayout.CafeSpot + new Vector3(10f, 0, 0), h => h.CanBePossessed && h.Profession == Profession.Civilian && h != tail.Target);
            Check(civ != null, "a civilian follower is available");
            if (!civ) yield break;
            PM.ForcePossessImmediate(civ);
            TeleportBody(PM.ActiveBody, Ground(new Vector3(-46.2f, 0f, 7f)), 270f);
            // Prerequisites only (mission 2 is covered by scenario 12): both leads are in.
            MissionManager.SetFlag(MissionFlags.NewsRead);
            MissionManager.SetFlag(MissionFlags.Eavesdropped);
            yield return null; yield return null;
            Check(RunState.MissionIndex == 2 && tail.State == TailState.Preparing, $"THE TAIL is mission 3; Petrov packs up first ({tail.State})");
            bool ok = false;
            yield return FollowTail(() => RunState.Has(MissionFlags.TailLeg1), 70f, r => ok = r);
            Check(ok && tail.Failures == 0 && tail.ResumeWaypoint == 1, $"followed her unnoticed to the Market St bus stop checkpoint ({tail.State})");
            if (!current.pass) yield break;

            // Failure 1: wander off. Beyond 26 m for 5 s the trail goes cold and she doubles back to the bus stop.
            yield return WalkTo(Ground(new Vector3(-40f, 0f, 2f)), 20f, r => ok = r, 1.5f);
            GameInput.SimMove = Vector2.zero;
            for (float t = 0; t < 30f && tail.Failures == 0; t += Time.deltaTime) { Calm(); yield return null; }
            Check(tail.Failures == 1 && tail.LastFailure == TailFailure.Lost && tail.ResumeWaypoint == 1, $"losing her counts as a failure ({tail.LastFailure})");
            for (float t = 0; t < 25f && tail.State != TailState.WaitingForFollower; t += Time.deltaTime) { Calm(); yield return null; }
            Check(tail.State == TailState.WaitingForFollower && Flat(tail.TargetPosition - tail.ResumePosition).magnitude < 1.6f,
                "she doubled back and waits at the last checkpoint (bus stop)");
            if (!current.pass) yield break;

            // Failure 2: crowd her. Staying at arm's length fills her alertness until she makes you.
            yield return Approach(() => tail.TargetPosition, 1.4f, 30f, () => tail.Failures >= 2);
            Check(tail.Failures == 2 && tail.LastFailure == TailFailure.Spotted, $"crowding her gets the follower spotted ({tail.LastFailure}, {tail.Failures})");
            Check(!RunState.Has(MissionFlags.TailDone) && RunState.MissionIndex == 2, "a failed tail keeps the mission open (retry from the checkpoint)");
            if (!current.pass) yield break;

            // A checkpoint reload resumes her at the furthest checkpoint reached, waiting for the follower.
            yield return ReloadCheckpoint();
            tail = TailTarget.Instance;
            for (float t = 0; t < 2f && tail && tail.State == TailState.Dormant; t += Time.deltaTime) yield return null;
            Check(tail && tail.State == TailState.WaitingForFollower && tail.ResumeWaypoint == 1 &&
                  Flat(tail.TargetPosition - MissionLayout.TailRoute[1]).magnitude < 2f && RunState.Has(MissionFlags.TailLeg1),
                  $"reload: leg-1 progress persists, Petrov waits at the bus stop; 2 failures (lost, spotted) each sent her back");
        }

        // ---------------------------------------------------------------- HUNTER PROTOCOL
        static IEnumerator HunterProtocolRoute()
        {
            var squad = HunterSquad.Instance;
            for (float t = 0; t < 1f && (!squad || squad.Hunters.Count < 2); t += Time.deltaTime) yield return null;
            Check(squad && squad.Hunters.Count == 2 && squad.Officer && squad.Hunters.All(h => squad.ScannerOf(h) && squad.ScannerOf(h).Active),
                "Protocol Hound deployed two scanner hunters and a duty officer");
            if (!current.pass) yield break;
            var coolant = CooperativeConsole.Find("coolant");
            Check(coolant && !coolant.CanInteract(ActiveContext(), out _), "the coolant co-op stays locked while Protocol Hound runs");
            int burns = BioScanTracker.BurnCount;
            bool ok = false;
            // Park the other amoeba's body deep in Vale's office, out of every scanner sweep.
            if (PM.Agents.Count == 2)
            {
                int me = PM.ActiveIndex;
                yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
                if (PM.Active.Host)
                {
                    var parked = PM.Active.Host;
                    Vector3 from = parked.transform.position, park = new Vector3(3.6f, CityLayout.LabFloor, 96.6f);
                    // Line up on the office doorway's centre first, like a player would, then walk in.
                    yield return WalkVia(r => ok = r, new Vector3(0f, CityLayout.LabFloor, 86.9f), new Vector3(0f, CityLayout.LabFloor, 90.2f), park);
                    if (!ok) Note($"park failed: {parked.DisplayName} ({parked.State}) from {from} now {parked.transform.position}, path {PathComplete(parked.transform.position, park)}, active host {(PM.Active.Host ? PM.Active.Host.DisplayName : "none")}");
                    Check(ok, "parked the second body in Vale's office, away from the scanner sweep");
                }
                yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
                Check(PM.ActiveIndex == me, "switched back to the first amoeba");
                if (!current.pass) yield break;
            }
            // Time the corridor crossing: wait until Hunter Kade's sweep heads away to the far west end.
            var kade = squad.Hunters.FirstOrDefault(h => h && h.DisplayName == "Hunter Kade");
            bool gap = false;
            for (float t = 0; t < 70f && kade && !gap; t += Time.deltaTime)
            {
                Calm();
                gap = kade.transform.position.x < -8f && kade.Velocity.x < -0.3f;
                yield return null;
            }
            Check(gap, "waited for a gap in Hunter Kade's corridor sweep");
            Note($"hunter gap found at Kade x={(kade ? kade.transform.position.x : 0f):0.0}");
            if (!current.pass) yield break;
            yield return WalkVia(r => ok = r, new Vector3(0f, CityLayout.LabFloor, 90.2f), new Vector3(0f, CityLayout.LabFloor, 86.9f),
                new Vector3(16.75f, CityLayout.LabFloor, 86.4f));
            Check(ok && BioScanTracker.BurnCount == burns, "reached the security office doorway without a scanner burn");
            if (!current.pass) yield break;
            // Leave the body outside the arch and slip through as a free amoeba.
            yield return Press(() => GameInput.SimLeave = true, () => GameInput.SimLeave = false);
            yield return null;
            Check(PM.Active.IsFree, "left the host outside the bio-scan arch");
            float aimApproachDeadline = Time.realtimeSinceStartup + 15f;
            yield return WalkTo(new Vector3(17.3f, CityLayout.LabFloor, 79.7f), 15f, r => ok = r, 0.6f);
            Check(ok && RunState.Has(MissionFlags.HunterGate) && BioScanTracker.BurnCount == burns, "a free amoeba crosses the arch unread");
            if (!current.pass) yield break;
            var officer = squad.Officer;
            yield return EnsureHunterAimSpacing(officer, aimApproachDeadline);
            NoteHunterTarget("full route before camera aim", officer, PM.ComputeTarget());
            yield return AimSettle(officer);
            var tg = PM.ComputeTarget();
            NoteHunterTarget("full route after settled yaw/pitch aim", officer, tg);
            Check(tg.kind == TargetKind.Possess && tg.host == officer, $"duty officer targetable from behind: {tg.prompt}");
            if (!current.pass) yield break;
            yield return PressInteract();
            for (float t = 0; t < 2f && PM.Active.Host != officer; t += Time.deltaTime) yield return null;
            Check(PM.Active.Host == officer, "possessed Officer Duarte without crossing the arch in a body");
            if (!current.pass) yield break;
            Calm();
            yield return UseProp(MissionFlags.HuntersDown, 4f);
            yield return null;
            Check(RunState.Has(MissionFlags.HuntersDown) && RunState.MissionIndex == 7 && squad.StoodDown &&
                  BioScanner.All.All(sc => !sc.Active) && !ScanGate.All.Any(g => g.Live) && BioScanTracker.BurnCount == burns,
                  "the officer's badge released Protocol Hound with no body burned → THE PART HE HID");
        }

        /// <summary>
        /// Test housekeeping after a deliberate burn: clear suspicion and put the officer and hunters back on their posts
        /// (a burn alerts every guard nearby), so the next step starts from a known state.
        /// </summary>
        static IEnumerator CalmLab(HunterSquad squad)
        {
            Calm();
            yield return null;
            Calm();
            var posts = new[] { MissionLayout.HunterCorridorRoute[0], MissionLayout.HunterLobbyRoute[0] };
            float[] yaws = { 270f, 0f };
            for (int i = 0; i < squad.Hunters.Count && i < posts.Length; i++)
            {
                var h = squad.Hunters[i];
                if (!h || !h.IsAwakeNpc) continue;
                TeleportNpc(h, posts[i], yaws[i]);
                if (h.Brain is GuardAI g) g.OrderInvestigate(posts[i]);
            }
            var o = squad.Officer;
            if (o && o.IsAwakeNpc)
            {
                TeleportNpc(o, MissionLayout.DutyOfficer, MissionLayout.DutyOfficerYaw);
                if (o.Brain is GuardAI g) g.OrderInvestigate(MissionLayout.DutyOfficer);
            }
            yield return null;
            Calm();
        }

        // ---------------------------------------------------------------- 15
        static IEnumerator ScenarioHunters()
        {
            // Prerequisites only: earlier chapters complete up to Vale's reveal (each is covered by the route scenarios).
            AdvanceCampaignTo(MissionFlags.Confronted);
            yield return null; yield return null;
            var squad = HunterSquad.Instance;
            Check(RunState.MissionIndex == 6 && squad && squad.Hunters.Count == 2 && squad.Officer, $"HUNTER PROTOCOL deployed after the reveal (mission {RunState.MissionIndex + 1})");
            if (!current.pass) yield break;
            var kade = squad.Hunters.First(h => h.DisplayName == "Hunter Kade");
            var scanner = squad.ScannerOf(kade);
            Check(scanner && scanner.Active, "Hunter Kade carries a live bio-scanner");
            var archive = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).First(o => o.flag == MissionFlags.SignedArchive);
            var ss = SuspicionSystem.Instance;
            bool ok = false;
            yield return new WaitForSeconds(0.5f);

            // 1) A possessed body in the cone fills the meter and is burned: pursuit locks onto that body.
            var sci = NearestHost(CityLayout.LabCorridor, h => h.CanBePossessed && h.Profession == Profession.Scientist && !squad.Hunters.Contains(h));
            Check(sci != null, "a scientist host is available");
            if (!sci) yield break;
            PM.ForcePossessImmediate(sci);
            Check(!archive.CanInteract(ActiveContext(), out _), "the signed archive stays locked until the hunters are down");
            TeleportNpc(kade, Ground(new Vector3(-9f, 0f, 86f)), 90f);
            TeleportBody(PM.ActiveBody, Ground(new Vector3(-4.6f, 0f, 86f)), 270f);
            Calm();
            float rose = 0f;
            for (float t = 0; t < 6f && !BioScanTracker.IsBurned(sci); t += Time.deltaTime)
            {
                rose = Mathf.Max(rose, BioScanTracker.Meter(sci));
                Vector3 front = kade.transform.position + kade.transform.forward * 3.5f;
                if (Flat(front - sci.transform.position).magnitude > 1.5f) TeleportBody(PM.ActiveBody, Ground(front), 270f);
                yield return null;
            }
            Check(BioScanTracker.IsBurned(sci) && ss.Level == SuspicionLevel.Pursuing && ss.Suspect == (Component)sci,
                $"the scanner read rose ({rose * 100f:0}%) and burned {sci.DisplayName}: pursuit locks onto that body");
            if (!current.pass) yield break;
            // 2) Leaving the burned body breaks the trail.
            yield return Press(() => GameInput.SimLeave = true, () => GameInput.SimLeave = false);
            yield return null;
            Check(PM.Active.IsFree && ss.Suspect != (Component)sci && BioScanTracker.IsBurned(sci), "leaving the burned body [Q] breaks the trail (it stays burned)");
            TeleportBody(PM.ActiveBody, Ground(new Vector3(12.5f, 0f, 86.6f)), 90f);
            yield return CalmLab(squad);
            if (!current.pass) yield break;

            // 3) The doorway arch burns any possessed body on the spot.
            var worker = NearestHost(CityLayout.ServiceRoom, h => h.CanBePossessed && !squad.Hunters.Contains(h) && h != squad.Officer && h != sci);
            Check(worker != null, "a second staff body is available");
            if (!worker) yield break;
            PM.ForcePossessImmediate(worker);
            var gate = ScanGate.All.First();
            TeleportBody(PM.ActiveBody, Ground(new Vector3(16.75f, 0f, 86.5f)), 180f);
            Calm();
            int gateBurns = gate.Burns;
            for (float t = 0; t < 3f && !BioScanTracker.IsBurned(worker); t += Time.deltaTime)
            {
                Cam.SetYaw(180f);
                GameInput.SimMove = new Vector2(0f, 1f); // camera faces south: walk into the doorway
                yield return null;
            }
            GameInput.SimMove = Vector2.zero;
            Check(BioScanTracker.IsBurned(worker) && gate.Burns == gateBurns + 1, $"the arch burned {worker.DisplayName} in the doorway");
            Cam.SetYaw(0f);
            yield return WalkTo(new Vector3(16.75f, CityLayout.LabFloor, 86.6f), 6f, r => ok = r, 0.6f);
            yield return Press(() => GameInput.SimLeave = true, () => GameInput.SimLeave = false);
            yield return null;
            yield return CalmLab(squad);
            if (!current.pass) yield break;

            // 4) A free amoeba walks through unread.
            int burns = BioScanTracker.BurnCount;
            bool ok2 = false;
            float aimApproachDeadline = Time.realtimeSinceStartup;
            for (int i = 0; i < 4 && !ok2; i++)
            {
                aimApproachDeadline = Time.realtimeSinceStartup + 12f;
                yield return WalkTo(new Vector3(17.3f, CityLayout.LabFloor, 79.7f), 12f, r => ok2 = r, 0.6f); Calm();
            }
            Check(ok2 && BioScanTracker.BurnCount == burns && RunState.Has(MissionFlags.HunterGate), "a free amoeba slips through the arch unread");
            if (!current.pass) yield break;

            // 5) Possess the duty officer from behind and release the lockdown with her badge.
            var officer = squad.Officer;
            yield return EnsureHunterAimSpacing(officer, aimApproachDeadline);
            NoteHunterTarget("focused protocol before camera aim", officer, PM.ComputeTarget());
            yield return AimSettle(officer);
            var tg = PM.ComputeTarget();
            NoteHunterTarget("focused protocol after settled yaw/pitch aim", officer, tg);
            Check(tg.kind == TargetKind.Possess && tg.host == officer, $"duty officer targetable: {tg.prompt}");
            if (!current.pass) yield break;
            yield return PressInteract();
            for (float t = 0; t < 2f && PM.Active.Host != officer; t += Time.deltaTime) yield return null;
            Check(PM.Active.Host == officer, "possessed the duty officer");
            if (!current.pass) yield break;
            Calm();
            yield return UseProp(MissionFlags.HuntersDown, 4f);
            yield return null;
            Check(RunState.MissionIndex == 7 && squad.StoodDown && BioScanner.All.All(sc => !sc.Active) && !gate.Live,
                "breaker pulled: scanners and arch dark, hunters recalled → THE PART HE HID");
            Check(archive.requiredFlag == MissionFlags.HuntersDown && RunState.Has(archive.requiredFlag), "the signed-archive co-op unlocks after the hunters stand down");
            if (!current.pass) yield break;
            yield return ReloadCheckpoint();
            Check(RunState.MissionIndex == 7 && HunterSquad.Instance && !HunterSquad.Instance.Deployed &&
                  !HostCharacter.All.Any(h => h && h.DisplayName.StartsWith("Hunter ")) && BioScanner.All.Count == 0,
                  "reload: Protocol Hound stays down (no hunters redeployed); burned bodies, trail breaks and the arch verified");
        }

        // ---------------------------------------------------------------- BLACKOUT
        static Vector3 FrontOf(BlackoutSwitch s)
        {
            Vector3 ip = s.InteractPoint, c = s.transform.position;
            Vector3 outward = Flat(ip - c).normalized;
            return Ground(new Vector3(ip.x, 0f, ip.z) + outward * 0.45f, 1.2f);
        }

        static IEnumerator CutTransformer(BlackoutSwitch s, bool teleport = true)
        {
            if (teleport) TeleportBody(PM.ActiveBody, FrontOf(s), 0f);
            Calm();
            yield return AimSettle(s.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Interact && tg.interactable == (IInteractable)s && tg.allowed, $"{s.switchName}: {tg.prompt}");
            if (!current.pass) yield break;
            yield return PressInteract();
            for (float t = 0; t < BlackoutRules.HoldSeconds + 1.5f && !s.Cut; t += Time.deltaTime) { Calm(); yield return null; }
            Check(s.Cut, $"{s.switchName} isolator pulled through real input");
        }

        static IEnumerator BlackoutRoute()
        {
            var grid = BlackoutGrid.Instance;
            var master = FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).FirstOrDefault(o => o.flag == MissionFlags.MasterBroadcast);
            Check(grid && grid.Live && grid.switches.Length == 3 && master, "BLACKOUT: three live transformers and the master antenna exist");
            if (!current.pass) yield break;
            Check(!master.CanInteract(ActiveContext(), out _), "the master antenna refuses to broadcast before the blackout");
            if (PM.Agents.Count < 2)
            {
                yield return Press(() => GameInput.SimSplit = true, () => GameInput.SimSplit = false);
                Check(PM.Agents.Count == 2, "split for the body chain");
                if (!current.pass) yield break;
            }
            BlackoutSwitch s1 = grid.switches[0], s2 = grid.switches[1], s3 = grid.switches[2];
            // Amoeba A: the first body cuts P-1 and opens the reroute window.
            TakeProfession(Profession.Civilian);
            var bodyA = PM.Active.Host;
            yield return CutTransformer(s1);
            if (!current.pass) yield break;
            float windowStart = Time.time;
            Check(grid.WindowOpen && grid.DarkLights >= 0, $"P-1 dark: {grid.Remaining:0}s reroute window open");
            // Run A to P-3 under the clock: the grid refuses its logged palm print, so it leaves the body there.
            bool ok = false;
            yield return WalkTo(FrontOf(s3), 20f, r => ok = r, 0.8f);
            Check(ok, "body A ran to transformer P-3 under the clock");
            yield return AimSettle(s3.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.interactable == (IInteractable)s3 && !tg.allowed, $"P-3 refuses body A's logged palm print: {tg.prompt}");
            if (!current.pass) yield break;
            yield return Press(() => GameInput.SimLeave = true, () => GameInput.SimLeave = false);
            yield return null;
            // Amoeba B: a different body cuts P-2.
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            TakeProfession(Profession.Civilian);
            Check(PM.Active.Host && PM.Active.Host != bodyA, "amoeba B wears a second body");
            yield return CutTransformer(s2);
            if (!current.pass) yield break;
            // Back to A: hop into a third body beside P-3 and finish inside the window.
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            var third = NearestHost(PM.Active.Position, h => h.CanBePossessed && h != bodyA);
            Check(third != null, "a third body is near P-3");
            if (!third) yield break;
            yield return PossessNearby(third, s3.InteractPoint);
            Check(PM.Active.Host == third, $"hopped into {third.DisplayName}");
            yield return CutTransformer(s3);
            float elapsed = Time.time - windowStart;
            Note($"blackout chain finished in {elapsed:0.0}s, {grid.DarkLights} lights dark, third body {third.DisplayName}");
            Check(RunState.Has(MissionFlags.DistrictBlackout) && elapsed < grid.windowSeconds && RunState.MissionIndex == 13 && grid.DarkLights > 0,
                $"three different bodies blacked out the plaza in {elapsed:0.0}s ({grid.DarkLights} lights dark) → THE FINAL CONVERGENCE");
        }

        // ---------------------------------------------------------------- 16
        static IEnumerator ScenarioBlackout()
        {
            // Prerequisites only: every earlier chapter complete (each is covered by the route scenarios).
            AdvanceCampaignTo(MissionFlags.ProtocolEpsilonBreached);
            yield return null; yield return null;
            var grid = BlackoutGrid.Instance;
            Check(RunState.MissionIndex == 12 && grid && grid.Live && grid.switches.Length == 3, $"BLACKOUT is mission 13 (now {RunState.MissionIndex + 1})");
            if (!current.pass) yield break;
            yield return new WaitForSeconds(0.3f);
            BlackoutSwitch s1 = grid.switches[0], s2 = grid.switches[1], s3 = grid.switches[2];
            var civA = NearestHost(s1.transform.position, h => h.CanBePossessed && h.Profession == Profession.Civilian);
            PM.ForcePossessImmediate(civA);
            yield return CutTransformer(s1);
            if (!current.pass) yield break;
            int dark = grid.DarkLights;
            Check(grid.WindowOpen && grid.CutCount == 1, $"first cut opens the {grid.windowSeconds:0}s reroute window ({dark} street lights dark)");
            // The same body is refused at the next isolator.
            TeleportBody(PM.ActiveBody, FrontOf(s2), 0f);
            yield return AimSettle(s2.InteractPoint);
            var tg = PM.ComputeTarget();
            Check(tg.interactable == (IInteractable)s2 && !tg.allowed && !s2.CanInteract(ActiveContext(), out _), $"palm-print lockout: {tg.prompt}");
            if (!current.pass) yield break;
            // Failure: let the window expire. The grid reroutes, every isolator re-arms and the lights return.
            float wait = grid.Remaining + 2f;
            for (float t = 0; t < wait && grid.Reroutes == 0; t += Time.deltaTime) { Calm(); yield return null; }
            Check(grid.Reroutes == 1 && !s1.Cut && grid.CutCount == 0 && !grid.WindowOpen && grid.DarkLights == 0 && !RunState.Has(MissionFlags.DistrictBlackout),
                "timeout: the grid rerouted, all transformers live again and the lights restored");
            Check(s2.CanInteract(ActiveContext(), out _), "the palm-print log clears with the reroute");
            if (!current.pass) yield break;
            // Success: a three-body chain (split + a hop) inside one window.
            yield return Press(() => GameInput.SimSplit = true, () => GameInput.SimSplit = false);
            Check(PM.Agents.Count == 2, "split for the chain");
            yield return CutTransformer(s2);
            yield return Press(() => GameInput.SimSwitch = true, () => GameInput.SimSwitch = false);
            var civB = NearestHost(s1.transform.position, h => h.CanBePossessed && h != civA);
            PM.ForcePossessImmediate(civB);
            yield return CutTransformer(s1);
            var civC = NearestHost(s3.transform.position, h => h.CanBePossessed && h != civA && h != civB);
            PM.ForcePossessImmediate(civC);
            yield return CutTransformer(s3);
            Check(RunState.Has(MissionFlags.DistrictBlackout) && RunState.MissionIndex == 13 && grid.DarkLights > 0, "three bodies inside one window → district blackout");
            if (!current.pass) yield break;
            yield return ReloadCheckpoint();
            grid = BlackoutGrid.Instance;
            Check(grid && grid.switches.All(sw => sw.Cut) && !grid.Live && grid.DarkLights > 0 && RunState.MissionIndex == 13,
                $"reload: blackout persists ({grid.DarkLights} lights dark); lockout, reroute-on-timeout and the chain verified");
        }

        // ---------------------------------------------------------------- 09
        static IEnumerator ScenarioSuspicion()
        {
            var ss = SuspicionSystem.Instance;
            Calm();
            // unobserved: a violation far below the map cannot be seen
            bool seen = ss.ReportViolation(ViolationType.WitnessedPossession, new Vector3(0, -80, 0), null);
            Check(!seen && ss.Value < 0.01f, "unobserved violation ignored");
            // observed: put a civilian 3 m in front of the free blob, facing it
            var blob = PM.Active.Blob;
            var civ = NearestHost(blob.transform.position, h => h.IsAwakeNpc && h.Profession == Profession.Civilian);
            Vector3 spot = PossessionManager.FindFreeSpot(blob.transform.position, blob.transform.forward);
            Vector3 npcPos = blob.transform.position + (spot - blob.transform.position).normalized * 3f;
            if (NavMesh.SamplePosition(npcPos, out var hit, 2f, NavMesh.AllAreas)) npcPos = hit.position;
            TeleportNpc(civ, npcPos, Quaternion.LookRotation(Flat(blob.transform.position - npcPos)).eulerAngles.y);
            civ.Brain.Notice(blob.Center, 4f);
            float t = 0;
            while (t < 3f && ss.Value < 5f) { t += Time.deltaTime; yield return null; }
            float peak = ss.Value;
            Check(peak >= 5f, $"blob sighted by {civ.DisplayName} → suspicion {peak:0}");
            // hide the blob: move far from everyone (out of sight) and check decay
            TeleportBody(PM.ActiveBody, CityLayout.PlayerSpawn, 0);
            foreach (var h in HostCharacter.All) if (h && h.IsAwakeNpc && Vector3.Distance(h.transform.position, CityLayout.PlayerSpawn) < 20f) TeleportNpc(h, h.transform.position + Vector3.forward * 40f, 0);
            float v0 = ss.Value;
            yield return new WaitForSeconds(SuspicionRules.DecayDelay + 2.5f);
            Check(ss.Value < v0, $"unobserved: no rise; observed: rose to {peak:0}; decayed {v0:0} → {ss.Value:0}");
        }

        // ---------------------------------------------------------------- 10
        static IEnumerator ScenarioCapture()
        {
            Calm();
            var guard = HostCharacter.All.FirstOrDefault(h => h && h.IsAwakeNpc && h.Brain is GuardAI);
            Check(guard != null, "guard exists");
            if (!guard) yield break;
            var victim = NearestHost(guard.transform.position, h => h.CanBePossessed && h != guard && h.Profession != Profession.Guard);
            PM.ForcePossessImmediate(victim);
            Vector3 front = guard.transform.position + guard.transform.forward * 4f;
            if (NavMesh.SamplePosition(front, out var hit, 3f, NavMesh.AllAreas)) front = hit.position;
            TeleportBody(PM.ActiveBody, front, 0);
            yield return null;
            var ss = SuspicionSystem.Instance;
            // a witnessed violation by the possessed host near the guard → raise to pursuit
            ss.SetValue(90f);
            ss.ReportViolation(ViolationType.UnauthorizedAccess, victim.ChestPosition, victim);
            ss.SetValue(95f);
            float t = 0; bool captured = false;
            var agent = PM.Active;
            while (t < 12f && !captured) { t += Time.deltaTime; captured = agent.CapturedPendingEject || victim.State == HostState.Captured; ss.SetValue(Mathf.Max(ss.Value, 90f)); yield return null; }
            Check(captured, $"guard {guard.DisplayName} captured the host after {t:0.0}s ({(guard.Brain as GuardAI).GState})");
            if (!captured) yield break;
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Eject, "eject prompt shown");
            yield return PressInteract();
            yield return new WaitForSeconds(0.3f);
            Check(PM.Agents.Count >= 1 && PM.Active.IsFree && PM.Active.Blob.gameObject.activeSelf, "amoeba ejected in free form");
            Check(victim.State == HostState.Unconscious, "captured host left unconscious");
            Check(GameManager.Instance.State == GameState.Playing, "game continues");
            Check(ss.GraceRemaining > 0f, $"captured after {t:0.0}s, E ejected, escape window {ss.GraceRemaining:0.0}s, game continues");
        }

        // ---------------------------------------------------------------- 11
        static IEnumerator ScenarioPauseRestart()
        {
            var gm = GameManager.Instance;
            gm.SetPaused(true);
            yield return null;
            Check(gm.State == GameState.Paused && Time.timeScale == 0f, "paused");
            gm.SetPaused(false);
            yield return null;
            Check(gm.State == GameState.Playing && Time.timeScale == 1f, "resumed");
            var oldDirector = CityDirector.Instance;
            gm.RestartGame();
            float t = 0;
            while ((CityDirector.Instance == null || CityDirector.Instance == oldDirector || !CityDirector.Instance.Ready) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            Check(CityDirector.Instance != null && CityDirector.Instance != oldDirector && PM != null && PM.Agents.Count == 1, "pause/resume OK, restart reloaded the city with a fresh amoeba");
        }

        // ================================================================ benchmark
        IEnumerator BenchmarkMain()
        {
            Log.Clear();
            string reportPath = Path.GetFullPath(OutPath());
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var arguments = Environment.GetCommandLineArgs();
            string Option(string flag)
            {
                int index = Array.IndexOf(arguments, flag);
                return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
            }
            string district = Option("-benchmarkDistrict") ?? "city";
            string preset = Option("-benchmarkPreset");
            int requestedPreset = GraphicsPreset.Current;
            if (preset != null)
            {
                requestedPreset = Array.FindIndex(GraphicsPreset.Names, name => name.Equals(preset, StringComparison.OrdinalIgnoreCase));
                if (requestedPreset < 0 && (!int.TryParse(preset, out requestedPreset) || requestedPreset < 0 || requestedPreset >= GraphicsPreset.Names.Length))
                    requestedPreset = -1;
            }
            bool riverside = district.Equals("riverside", StringComparison.OrdinalIgnoreCase);
            bool island = district.Equals("island", StringComparison.OrdinalIgnoreCase);
            if ((!riverside && !island && !district.Equals("city", StringComparison.OrdinalIgnoreCase)) || requestedPreset < 0 ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Write("ERROR: benchmark requires a graphics device, -benchmarkPreset Low/Medium/High/Ultra (or 0-3), and -benchmarkDistrict city/riverside/island.");
                File.WriteAllText(reportPath, Log.ToString());
                CinematicFilmLifetime.Quit(2);
                yield break;
            }
            yield return LoadCity();
            if (riverside || island)
            {
                // Diagnostic chapter setup only, never mission proof or a saved campaign/graphics preference.
                RunState.RecordLakesideOutcome(EndingOutcome.Peace);
                RunState.Flags.Add(MissionFlags.Escaped);
                RunState.BeginAfterlake();
                if (island)
                {
                    RunState.Flags.Add(AfterlakeCampaign.Departed);
                    if (!RunState.BeginIsland())
                    {
                        Write("ERROR: island benchmark chapter setup was rejected.");
                        File.WriteAllText(reportPath, Log.ToString()); CinematicFilmLifetime.Quit(2); yield break;
                    }
                    RunState.Flags.Add(IslandCampaign.ArrivalSeen); // Exclude protected travel presentation from performance samples.
                    RunState.Flags.Add(IslandCampaign.ServiceOpen); // Authorized Maintenance service route; never the blob-only tunnel.
                    RunState.Flags.Add(IslandCampaign.InnerRefuge); // Populate the real holding roster; delivery/boarding remain incomplete.
                }
                SceneManager.LoadScene(GameManager.CityScene);
                yield return null;
                float readyDeadline = Time.realtimeSinceStartup + 30f;
                while ((!CityDirector.Instance || !CityDirector.Instance.Ready) && Time.realtimeSinceStartup < readyDeadline) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            GraphicsPreset.Apply(requestedPreset, save: false);
            GameInput.Simulated = true;
            // A legal staff host lets the tour exercise the live city without manufacturing a blob chase.
            var tourSpawn = island ? IslandCampaign.Spawn : riverside ? AfterlakeCampaign.Spawn : CityLayout.PlayerSpawn;
            var tourHost = island ? MissionSpawner.Find("Island jetty engineer") :
                NearestHost(tourSpawn, h => h.CanBePossessed && h.Profession == Profession.Scientist);
            if (island && (!CityDirector.Instance || !CityDirector.Instance.Ready || !tourHost || !tourHost.CanBePossessed ||
                tourHost.Profession != Profession.Maintenance || (tourHost.Permissions & AccessPermission.ServiceTunnel) == 0))
            {
                Write("ERROR: island benchmark requires its actual ready district and unoccupied Maintenance/ServiceTunnel jetty engineer.");
                File.WriteAllText(reportPath, Log.ToString()); CinematicFilmLifetime.Quit(2); yield break;
            }
            if (tourHost)
            {
                PM.ForcePossessImmediate(tourHost);
                TeleportBody(tourHost, tourSpawn, 0f);
            }
            var tourDirector = CityDirector.Instance;
            int tourCaptures = RunState.Captures;
            // Measure available headroom independently of the desktop refresh rate.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Write($"PARASITE benchmark — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Write($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB)  CPU: {SystemInfo.processorType} x{SystemInfo.processorCount}  RAM: {SystemInfo.systemMemorySize} MB");
            Write($"Resolution: {Screen.width}x{Screen.height} fullscreen={Screen.fullScreenMode} vSync={QualitySettings.vSyncCount} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Write($"Preset: {GraphicsPreset.Names[GraphicsPreset.Current]}, render scale {(pipeline ? pipeline.renderScale : 1f):0.00}; touring as {(tourHost ? island ? "maintenance" : "scientist" : "free amoeba")}; traffic and NPC AI enabled; startup and screenshot encoding excluded from samples");
            Write($"District: {(island ? "island (Peace chapter benchmark setup)" : riverside ? "riverside (Peace chapter fixture)" : "city")}; preset override is not saved.");
            if (island) Write("Setup only: completed river travel, arrival seen, human service gate open and inner-refuge prerequisite seeded; real holding NPCs active, no deliveries/boarding or mission acceptance claimed.");
            var waypoints = island
                ? new[] { tourSpawn,
                    IslandCampaign.At(-29.5f,.15f,-32), IslandCampaign.At(-22,.15f,-32), // Exterior wall views, south of the low culvert.
                    IslandCampaign.At(-7,.15f,-21), IslandCampaign.At(-7,.15f,-14.5f), // Actual human service opening.
                    IslandCampaign.At(-22,.15f,-5), IslandCampaign.At(-20,.15f,0), IslandCampaign.At(-20,.15f,6), IslandCampaign.At(-20,.15f,0),
                    IslandCampaign.At(14,.15f,-13), IslandCampaign.At(20,.15f,-9), // Holding overview and clear eastern approach.
                    IslandCampaign.At(17,.15f,5), IslandCampaign.At(27,.15f,10), // Handoff architecture from the south.
                    IslandCampaign.At(18,.15f,16.3f), IslandCampaign.At(16,.15f,18), IslandCampaign.At(16,.15f,21), // Supply front and clear west/north exit.
                    IslandCampaign.At(34,.15f,19), IslandCampaign.At(34,.15f,35), IslandCampaign.At(34,.15f,38), IslandCampaign.At(34,.55f,44.5f) }
                : riverside
                ? new[] { tourSpawn, AfterlakeCampaign.Receiver + Vector3.back * 3f, AfterlakeCampaign.FloodgatePanel + Vector3.back * 3f,
                    AfterlakeCampaign.Analysis + Vector3.back * 3f, AfterlakeCampaign.Radio + Vector3.back * 3f,
                    AfterlakeCampaign.Ferry + Vector3.back * 2f, tourSpawn }
                : new[] { tourSpawn, CityLayout.MainGateOutside, CityLayout.StaffTerminalPos, CityLayout.TunnelDoorOutside, tourSpawn };
            string shotDir = Path.GetDirectoryName(reportPath);
            Directory.CreateDirectory(shotDir);
            var frameTimes = new List<float>();
            int shot = 0, reachedCount = 0;
            bool interrupted = false;
            yield return new WaitForSeconds(2f);
            using var benchmarkProfile = arguments.Contains("-benchmarkProfile") ? new BenchmarkProfile() : null;
            float total = 0;
            foreach (var wp in waypoints)
            {
                bool reached = false;
                int firstFrame = frameTimes.Count;
                if (!PM || PM.ActiveBody == null)
                { interrupted = true; Write("ERROR: gameplay lost the touring body; samples are incomplete"); break; }
                var touristBody = PM.ActiveBody;
                var plannedPath = new NavMeshPath();
                bool sampledStart = NavMesh.SamplePosition(touristBody.Root.position, out var pathStart, 3f, NavMesh.AllAreas);
                bool sampledTarget = NavMesh.SamplePosition(wp, out var pathTarget, 3f, NavMesh.AllAreas);
                bool calculated = sampledStart && sampledTarget && NavMesh.CalculatePath(pathStart.position, pathTarget.position, NavMesh.AllAreas, plannedPath);
                string pathStatus = calculated ? plannedPath.status.ToString() : "not calculated";
                float targetPlanarOffset = sampledTarget ? Flat(pathTarget.position - wp).magnitude : float.PositiveInfinity;
                // Keep the original waypoint. A proxy outside the arrival tolerance is not route coverage.
                if (!calculated || plannedPath.status != NavMeshPathStatus.PathComplete || targetPlanarOffset > 0.9f)
                {
                    Write($"Waypoint {shot}: PATH FAILED {wp}; body {touristBody.Root.position}; sampled start={sampledStart}, target={sampledTarget}; path={pathStatus}; target planar offset={targetPlanarOffset:0.00}m");
                    Write("ERROR: route INCOMPLETE — benchmark requires a complete path to the original waypoint; no target was moved or skipped.");
                    break;
                }
                float sprintSpeed = touristBody is HostCharacter human ? human.Profile.runSpeed :
                    touristBody is AmoebaController blob ? blob.sprintSpeed : 0f;
                if (sprintSpeed <= 0f || float.IsNaN(sprintSpeed) || float.IsInfinity(sprintSpeed))
                {
                    Write($"ERROR: route INCOMPLETE — touring body's sprint speed {sprintSpeed} cannot define a travel budget.");
                    break;
                }
                float pathLength = 0f;
                var plannedCorners = plannedPath.corners;
                for (int point = 1; point < plannedCorners.Length; point++)
                    pathLength += Vector3.Distance(plannedCorners[point - 1], plannedCorners[point]);
                float travelBudget = Mathf.Max(35f, pathLength / sprintSpeed * 1.5f + 10f);
                Write($"Waypoint {shot} plan: {pathStatus}, {pathLength:0.00}m at {sprintSpeed:0.00}m/s, budget {travelBudget:0.0}s; original target {wp}; target planar offset {targetPlanarOffset:0.00}m");
                benchmarkProfile?.BeginLeg();
                var walk = WalkTo(wp, travelBudget, r => reached = r);
                while (true)
                {
                    if (CityDirector.Instance != tourDirector || !PM || PM.ActiveBody == null ||
                        !GameManager.Instance || !GameManager.Instance.IsPlaying || RunState.Captures != tourCaptures ||
                        (tourHost && PM.Active.Host != tourHost))
                    { interrupted = true; break; }
                    if (!walk.MoveNext()) break;
                    if (Time.unscaledDeltaTime > 0f)
                    {
                        frameTimes.Add(Time.unscaledDeltaTime);
                        total += Time.unscaledDeltaTime;
                        benchmarkProfile?.Sample();
                    }
                    yield return walk.Current;
                }
                (walk as IDisposable)?.Dispose();
                benchmarkProfile?.EndLeg();
                if (interrupted) { Write("ERROR: gameplay reset/capture interrupted the tour; samples are incomplete"); break; }
                if (reached) reachedCount++;
                Write($"Waypoint {shot}: {(reached ? "REACHED" : "TIMEOUT")} {wp}; body {PM.ActiveBody.Root.position}; sampled {frameTimes.Count - firstFrame} frames");
                ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"screenshot_{shot++}.png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            ClearSim();
            if (benchmarkProfile != null)
            {
                string profilePath = Path.ChangeExtension(reportPath,"profile.json");
                benchmarkProfile.Write(profilePath,district,GraphicsPreset.Names[GraphicsPreset.Current],frameTimes.Count,reachedCount,waypoints.Length);
                Write("Profiler: optional actual-marker sidecar " + profilePath + "; first profile sample per leg excluded; baseline frame statistics unchanged.");
                benchmarkProfile.Dispose();
            }
            if (frameTimes.Count == 0)
            {
                Write("ERROR: benchmark collected no frames");
                File.WriteAllText(reportPath, Log.ToString());
                CinematicFilmLifetime.Quit(1);
                yield break;
            }
            frameTimes.Sort();
            float avg = frameTimes.Average();
            float p99 = frameTimes[Mathf.Clamp((int)(frameTimes.Count * 0.99f), 0, frameTimes.Count - 1)];
            float worst = frameTimes[^1];
            Write($"Frames: {frameTimes.Count} over {total:0.0}s  avg {1f / avg:0.0} FPS ({avg * 1000f:0.00} ms)  p99 {p99 * 1000f:0.00} ms ({1f / p99:0.0} FPS)  worst {worst * 1000f:0.0} ms");
            Write($"Memory: allocated {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB, reserved {Profiler.GetTotalReservedMemoryLong() / 1048576} MB, mono heap {Profiler.GetMonoUsedSizeLong() / 1048576} MB, gfx driver {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576} MB");
            Write($"Scene: {HostCharacter.All.Count} NPCs, {FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length} renderers, {FindObjectsByType<Light>(FindObjectsSortMode.None).Length} lights");
            bool complete = !interrupted && reachedCount == waypoints.Length;
            Write($"Final vSync={QualitySettings.vSyncCount}; route {(interrupted ? "INTERRUPTED" : complete ? "COMPLETE" : "INCOMPLETE")}, {reachedCount}/{waypoints.Length} waypoints reached");
            File.WriteAllText(reportPath, Log.ToString());
            yield return new WaitForSecondsRealtime(1f);
            CinematicFilmLifetime.Quit(complete ? 0 : 1);
        }
    }
}
