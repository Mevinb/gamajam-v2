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
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Parasite.Testing
{
    /// <summary>
    /// Automated acceptance scenarios driven through the real game systems with simulated input.
    /// Used by PlayMode tests and by the standalone build:
    ///   Parasite.x86_64 -autotest [-autotestOut file]      → runs all scenarios, writes a report, quits (exit code 0/1)
    ///   Parasite.x86_64 -benchmark [-autotestOut file]     → walks the city for 40 s, logs FPS + memory, screenshots, quits
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
            bool auto = args.Contains("-autotest"), bench = args.Contains("-benchmark");
            if (!auto && !bench) return;
            var go = new GameObject("AutoTestRunner");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<AutoTest>();
            r.StartCoroutine(bench ? r.BenchmarkMain() : r.AutoMain());
        }

        static string OutPath()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autotestOut");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "autotest_report.txt");
        }

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
            Application.Quit(failed == 0 ? 0 : 1);
        }

        // ================================================================ scenarios
        public static IEnumerator RunAll()
        {
            Running = true;
            Results.Clear();
            Write($"PARASITE autotest — {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {SystemInfo.graphicsDeviceName} / {SystemInfo.operatingSystem}");
            var scenarios = new (string, Func<IEnumerator>)[]
            {
                ("01 Start, move, camera, jump", ScenarioMove),
                ("02 Aim + E possesses host within 5 m (camera + WASD follow)", ScenarioPossess),
                ("03 Q release: host unconscious 8 s then recovers", ScenarioRelease),
                ("04 V split max two, Tab switch, inactive stays put", ScenarioSplit),
                ("05 Civilian denied at restricted doors", ScenarioCivilianDenied),
                ("06 Route A: guard badge → Main Gate → lab → Dr. Vale", () => ScenarioRoute(EntryRoute.MainGate)),
                ("07 Route B: scientist → Staff Terminal → lab → Dr. Vale", () => ScenarioRoute(EntryRoute.StaffTerminal)),
                ("08 Route C: maintenance → Service Tunnel → lab → Dr. Vale", () => ScenarioRoute(EntryRoute.ServiceTunnel)),
                ("09 Suspicion only from observed violations; decays", ScenarioSuspicion),
                ("10 Guard pursues + captures; amoeba ejects and survives", ScenarioCapture),
                ("11 Pause + restart", ScenarioPauseRestart),
            };
            foreach (var (name, fn) in scenarios)
            {
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
            while (true)
            {
                object cur;
                try { if (!e.MoveNext()) yield break; cur = e.Current; }
                catch (Exception ex) { current.pass = false; current.detail = "EXCEPTION: " + ex.Message; Debug.LogException(ex); yield break; }
                yield return cur;
                if (!current.pass) yield break;
            }
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
        }

        static PossessionManager PM => PossessionManager.Instance;
        static PlayerCamera Cam => PlayerCamera.Instance;

        static IEnumerator Press(Action set, Action clear)
        {
            set(); yield return null; clear(); yield return null;
        }

        static IEnumerator PressInteract() => Press(() => GameInput.SimInteract = true, () => GameInput.SimInteract = false);

        /// <summary>Point the camera (yaw) at a world point from the active body.</summary>
        static void AimAt(Vector3 p)
        {
            var from = PM.ActiveBody.Root.position;
            var d = p - from; d.y = 0;
            if (d.sqrMagnitude > 0.001f) Cam.SetYaw(Quaternion.LookRotation(d).eulerAngles.y);
        }

        static IEnumerator AimSettle(Vector3 p)
        {
            for (int i = 0; i < 20; i++) { AimAt(p); yield return null; }
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
            }
        }

        static void TeleportNpc(HostCharacter h, Vector3 pos, float yaw)
        {
            if (h.Nav && h.Nav.enabled) h.Nav.Warp(pos); else h.transform.position = pos;
            h.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        /// <summary>Walk the active body along a NavMesh path using simulated WASD (camera yaw fixed at 0).</summary>
        static IEnumerator WalkTo(Vector3 target, float timeout, Action<bool> done, float tolerance = 0.9f)
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
                if ((pos - lastPos).magnitude < 0.02f) { stuckT += Time.deltaTime; if (stuckT > 0.8f) { GameInput.SimJump = true; stuckT = 0f; } }
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
            yield return AimSettle(target.ChestPosition);
            var tg = PM.ComputeTarget();
            Check(tg.kind == TargetKind.Possess && tg.host == target, $"aim target = {tg.kind} {(tg.host ? tg.host.DisplayName : "-")}");
            if (!current.pass) yield break;
            yield return PressInteract();
            yield return new WaitForSeconds(PossessionManager.LaunchSeconds + 0.4f);
            Check(PM.Active.Host == target && target.IsPossessed, $"possessed {target.DisplayName}");
            yield return new WaitForSeconds(0.5f);
            float camDist = Vector3.Distance(Cam.Cam.transform.position, target.CameraTarget.position);
            Check(camDist < 7f, $"camera follows host ({camDist:0.0} m)");
            Vector3 p0 = target.transform.position;
            Cam.SetYaw(0);
            GameInput.SimMove = new Vector2(1, 0);
            yield return new WaitForSeconds(1.0f);
            GameInput.SimMove = Vector2.zero;
            float moved = Flat(target.transform.position - p0).magnitude;
            Check(moved > 1f, $"possessed {target.DisplayName} via aim+E, camera {camDist:0.0} m, WASD moved host {moved:0.0} m");
            // far host is not possessable
            var far = NearestHost(target.transform.position + Vector3.forward * 1000f, h => h.CanBePossessed);
            if (far && Vector3.Distance(far.transform.position, target.transform.position) > 8f)
            {
                yield return AimSettle(far.ChestPosition);
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
            yield return WalkTo(CityLayout.ValeOffice, 60f, r => ok = r, 1.4f);
            Check(ok, "walked to Dr. Vale's office");
            var vale = FindFirstObjectByType<DrValeInteraction>();
            Check(vale != null, "Dr. Vale exists");
            if (!vale) yield break;
            yield return AimSettle(vale.InteractPoint);
            bool ended = false; EndingData data = default;
            GameEvents.Ending += d => { ended = true; data = d; };
            var t3 = PM.ComputeTarget();
            Check(t3.kind == TargetKind.Interact && t3.interactable == (IInteractable)vale, $"Vale prompt: {t3.prompt}");
            yield return PressInteract();
            Time.timeScale = 4f;
            for (float t = 0; t < 60f && !ended; t += Time.unscaledDeltaTime) yield return null;
            Time.timeScale = 1f;
            Check(ended && data.route == route, $"{prof} → {door.doorName} → lab → Dr. Vale → ending shown (route {data.route}, {data.timeSeconds:0}s)");
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
            yield return LoadCity();
            GameInput.Simulated = true;
            Write($"PARASITE benchmark — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Write($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB)  CPU: {SystemInfo.processorType} x{SystemInfo.processorCount}  RAM: {SystemInfo.systemMemorySize} MB");
            Write($"Resolution: {Screen.width}x{Screen.height} fullscreen={Screen.fullScreenMode} vSync={QualitySettings.vSyncCount} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            var waypoints = new[] { CityLayout.PlayerSpawn, CityLayout.MainGateOutside, CityLayout.StaffTerminalPos, CityLayout.TunnelDoorOutside, CityLayout.PlayerSpawn };
            string shotDir = Path.GetDirectoryName(OutPath());
            var frameTimes = new List<float>();
            int shot = 0;
            yield return new WaitForSeconds(2f);
            float total = 0;
            foreach (var wp in waypoints)
            {
                bool done = false;
                var walk = WalkTo(wp, 14f, r => done = true);
                while (walk.MoveNext())
                {
                    frameTimes.Add(Time.unscaledDeltaTime);
                    total += Time.unscaledDeltaTime;
                    yield return walk.Current;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"screenshot_{shot++}.png"));
                yield return null; yield return null;
                _ = done;
            }
            frameTimes.Sort();
            float avg = frameTimes.Average();
            float p99 = frameTimes[Mathf.Clamp((int)(frameTimes.Count * 0.99f), 0, frameTimes.Count - 1)];
            float worst = frameTimes[^1];
            Write($"Frames: {frameTimes.Count} over {total:0.0}s  avg {1f / avg:0.0} FPS ({avg * 1000f:0.00} ms)  1% low {1f / p99:0.0} FPS ({p99 * 1000f:0.0} ms)  worst {worst * 1000f:0.0} ms");
            Write($"Memory: allocated {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB, reserved {Profiler.GetTotalReservedMemoryLong() / 1048576} MB, mono heap {Profiler.GetMonoUsedSizeLong() / 1048576} MB, gfx driver {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576} MB");
            Write($"Scene: {HostCharacter.All.Count} NPCs, {FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length} renderers, {FindObjectsByType<Light>(FindObjectsSortMode.None).Length} lights");
            File.WriteAllText(OutPath(), Log.ToString());
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }
    }
}
