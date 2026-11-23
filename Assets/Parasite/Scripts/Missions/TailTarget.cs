using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    public enum TailState { Dormant, Preparing, WaitingForFollower, Walking, Pausing, Glancing, DoublingBack, Dropping, Leaving, Done }
    public enum TailFailure { None, Lost, Spotted, Possessed }

    /// <summary>
    /// THE TAIL: drives a named NPC along a scripted route while the player follows. Crowding her, being seen moving
    /// when she glances back, or being seen as a free amoeba fills her alertness (<see cref="TailRules"/>); at 100% she
    /// makes you and doubles back to the last checkpoint. Falling too far behind for too long loses her the same way.
    /// Reaching the end she leaves a dead drop (<see cref="dropProp"/>) and walks off. Leg checkpoints persist as flags.
    /// </summary>
    public class TailTarget : MonoBehaviour
    {
        public static TailTarget Instance { get; private set; }

        [Header("Target")]
        public string targetName = "Dr. Hana Petrov";
        public float walkSpeed = 2.1f, doubleBackSpeed = 3.4f;
        public float prepareSeconds = 4f, glanceSeconds = 1.8f, dropSeconds = 2.6f, startDelay = 0.8f;
        [Header("Route (the start is the target's spawn point)")]
        public Vector3[] route = new Vector3[0];
        public float[] pauseSeconds = new float[0];
        public int[] checkpointWaypoints = new int[0];
        public string[] checkpointNames = new string[0];
        public string[] checkpointFlags = new string[0];
        public string startName = "café terrace";
        [Header("Dead drop")]
        public MissionProp dropProp;
        public float dropYaw = -90f;
        public Vector3 restPoint;
        public float restYaw = 180f;

        public TailState State { get; private set; } = TailState.Dormant;
        public float Alertness { get; private set; }
        public float Distance { get; private set; } = 999f;
        public TailBand Band => TailRules.Band(Distance);
        public float LostTimer { get; private set; }
        public int Failures { get; private set; }
        public TailFailure LastFailure { get; private set; }
        public HostCharacter Target { get; private set; }
        public bool Glancing => State == TailState.Glancing;
        public bool OnTheMove => State is TailState.Walking or TailState.Pausing or TailState.Glancing;
        public bool Engaged => State is TailState.Preparing or TailState.WaitingForFollower or TailState.Walking or TailState.Pausing
            or TailState.Glancing or TailState.DoublingBack or TailState.Dropping;
        public Vector3 TargetPosition => Target ? Target.transform.position : transform.position;
        public int NextWaypoint => next;
        public int ResumeWaypoint => resumeIndex;
        public Vector3 ResumePosition => resumeIndex < 0 || resumeIndex >= route.Length ? startPos : route[resumeIndex];
        public string ResumeName
        {
            get
            {
                for (int i = 0; i < checkpointWaypoints.Length; i++)
                    if (checkpointWaypoints[i] == resumeIndex && i < checkpointNames.Length) return checkpointNames[i];
                return startName;
            }
        }

        int next, resumeIndex = -1, glanceCount;
        Vector3 startPos, glanceLook;
        Quaternion startRot;
        float stateT, glanceT, followT;
        bool interrupted, waryWarned;

        static readonly (string who, string line)[] StartLine =
        {
            ("DR. PETROV (on the phone)", "I have to go. If anything happens to me, you know where to look."),
        };
        static readonly (string who, string line)[][] CheckpointLines =
        {
            new[] { ("DR. PETROV (on the phone)", "No, not the bus. Vale's people pull the bus-camera footage every morning.") },
            new[] { ("DR. PETROV (on the phone)", "The old storm-drain alley. Same place as always. Don't call me again tonight.") },
        };
        static readonly (string who, string line)[] WaryLine = { ("DR. PETROV", "Hello? …Is somebody following me?") };
        static readonly (string who, string line)[] SpottedLine = { ("DR. PETROV", "Who are you? Stay away from me!") };
        static readonly (string who, string line)[] DropLine = { ("DR. PETROV", "There. If I disappear, at least someone will know what he did.") };

        static bool MissionActive => RunState.Has(MissionFlags.NewsRead) && RunState.Has(MissionFlags.Eavesdropped) &&
                                     !RunState.Has(MissionFlags.TailDone) && !RunState.LabEntered && !RunState.Has(MissionFlags.LabEntered);

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            Target = MissionSpawner.Find(targetName);
            if (!Target) { Debug.LogWarning("[TailTarget] target not found: " + targetName); enabled = false; return; }
            startPos = Target.transform.position; startRot = Target.transform.rotation;
            if (RunState.Has(MissionFlags.TailDone) || RunState.LabEntered)
            {
                if (RunState.Has(MissionFlags.TailDone)) { Warp(restPoint, restYaw); SetRestHome(); }
                State = TailState.Done;
            }
        }

        // ------------------------------------------------------------------ simulation
        void Update()
        {
            var gm = GameManager.Instance;
            if (!Target || (gm && !gm.IsPlaying) || State == TailState.Done) return;
            float dt = Time.deltaTime;

            if (State == TailState.Dormant)
            {
                if (MissionActive) Begin();
                return;
            }
            if (State == TailState.Leaving) return;
            if (!MissionActive && !RunState.Has(MissionFlags.TailDone)) { Abandon(); return; }

            // The player took her body (or knocked her out by leaving it): the route blurs until she comes to.
            if (Target.State != HostState.Normal)
            {
                if (!interrupted)
                {
                    interrupted = true;
                    GameEvents.RaiseToast("Inside Petrov the route blurs — she won't lead you anywhere while you wear her. Release her and pick up the trail.", Palette.Warning);
                }
                return;
            }
            if (interrupted) { interrupted = false; Fail(TailFailure.Possessed); return; }

            switch (State)
            {
                case TailState.Preparing:
                    stateT -= dt;
                    if (stateT <= 0f) { State = TailState.WaitingForFollower; followT = 0f; MissionManager.Say(StartLine); }
                    break;
                case TailState.WaitingForFollower:
                    followT = Distance <= TailRules.StartRange ? followT + dt : 0f;
                    if (followT >= startDelay) { State = TailState.Walking; glanceT = TailRules.GlanceInterval(glanceCount); }
                    break;
                case TailState.Walking:
                    glanceT -= dt;
                    if (glanceT <= 0f) BeginGlance();
                    break;
                case TailState.Glancing:
                    stateT -= dt;
                    if (stateT <= 0f) { State = TailState.Walking; glanceT = TailRules.GlanceInterval(++glanceCount); }
                    break;
                case TailState.Pausing:
                    stateT -= dt;
                    if (stateT <= 0f) BeginGlance();
                    break;
                case TailState.Dropping:
                    stateT -= dt;
                    if (stateT <= 0f) Drop();
                    break;
            }
            if (State is TailState.Preparing or TailState.WaitingForFollower or TailState.Walking or TailState.Pausing or TailState.Glancing)
                Observe(dt);
        }

        void Begin()
        {
            resumeIndex = -1;
            for (int i = 0; i < checkpointFlags.Length && i < checkpointWaypoints.Length; i++)
                if (RunState.Has(checkpointFlags[i])) resumeIndex = Mathf.Max(resumeIndex, checkpointWaypoints[i]);
            next = resumeIndex + 1;
            Alertness = 0f; LostTimer = 0f; glanceCount = 0; waryWarned = false; interrupted = false;
            Target.Brain.RoutineOverride = Drive;
            if (resumeIndex >= 0)
            {
                // Checkpoint reload: she is already waiting further along her route.
                if ((Target.transform.position - ResumePosition).sqrMagnitude > 9f) Warp(ResumePosition, Target.transform.eulerAngles.y);
                State = TailState.WaitingForFollower; followT = 0f;
            }
            else { State = TailState.Preparing; stateT = prepareSeconds; }
        }

        void Abandon()
        {
            // The player went straight into the lab: Petrov simply finishes her evening where she is.
            if (Target.Brain.RoutineOverride == (System.Action<float>)Drive) Target.Brain.RoutineOverride = null;
            Target.Brain.SetHome(Target.transform.position, Target.transform.rotation);
            State = TailState.Done;
        }

        void BeginGlance()
        {
            State = TailState.Glancing;
            stateT = glanceSeconds;
            glanceLook = Target.transform.position - Target.transform.forward * 6f;
        }

        void Observe(float dt)
        {
            var pm = PossessionManager.Instance;
            var ss = SuspicionSystem.Instance;
            if (pm == null || pm.Agents.Count == 0) return;
            AmoebaAgent nearest = null; float best = float.MaxValue;
            foreach (var a in pm.Agents)
            {
                if (a == null || a.InTransit) continue;
                Vector3 d = a.Position - Target.transform.position; d.y = 0f;
                if (d.magnitude < best) { best = d.magnitude; nearest = a; }
            }
            if (nearest == null) return;
            Distance = best;
            bool blob = nearest.Host == null;
            Vector3 point = blob ? nearest.Blob.Center : nearest.Host.ChestPosition;
            Transform root = blob ? nearest.Blob.transform : nearest.Host.transform;
            float speed = blob ? nearest.Blob.Velocity.magnitude : nearest.Host.Velocity.magnitude;
            var sample = new TailSample
            {
                distance = best,
                seenByTarget = ss && ss.CanSee(Target, point, root, TailRules.GlanceRange + 2f),
                followerIsBlob = blob,
                followerMoving = speed > 0.8f,
                followerRunning = speed > 3.6f,
                targetGlancing = State == TailState.Glancing,
                ignoreCrowding = State == TailState.Preparing,
            };
            Alertness = TailRules.Step(Alertness, sample, dt);
            if (Alertness >= TailRules.WaryAt)
            {
                Target.Brain.ShowAlert('?', 0.4f);
                if (!waryWarned) { waryWarned = true; MissionManager.Say(WaryLine); }
            }
            if (TailRules.Spotted(Alertness)) { Fail(TailFailure.Spotted); return; }
            LostTimer = OnTheMove ? TailRules.LostTimer(LostTimer, best, dt) : 0f;
            if (TailRules.Lost(LostTimer)) Fail(TailFailure.Lost);
        }

        void Fail(TailFailure reason)
        {
            Failures++;
            LastFailure = reason;
            Alertness = 0f; LostTimer = 0f; glanceCount = 0; waryWarned = false;
            next = resumeIndex + 1;
            State = TailState.DoublingBack;
            Target.Brain.RoutineOverride = Drive;
            string where = ResumeName;
            switch (reason)
            {
                case TailFailure.Spotted:
                    Target.Brain.ShowAlert('!', 3f);
                    MissionManager.Say(SpottedLine);
                    Sfx.Play(SfxId.AlertCurious, Target.transform.position, 0.7f);
                    GameEvents.RaiseToast($"Petrov made you! She's doubling back to the {where}. Keep 4–20 m and freeze when she glances back.", Palette.Danger);
                    break;
                case TailFailure.Lost:
                    Sfx.Ui(SfxId.Denied, 0.5f);
                    GameEvents.RaiseToast($"You lost Dr. Petrov. She's doubled back to the {where} — pick up her trail there.", Palette.Warning);
                    break;
                default:
                    GameEvents.RaiseToast($"Petrov comes to, rattled, and heads back to the {where}.", Palette.Warning);
                    break;
            }
        }

        void ArriveAt(int index)
        {
            next = index + 1;
            for (int i = 0; i < checkpointWaypoints.Length; i++)
            {
                if (checkpointWaypoints[i] != index) continue;
                resumeIndex = index;
                if (i < checkpointFlags.Length) MissionManager.SetFlag(checkpointFlags[i]);
                if (i < CheckpointLines.Length) MissionManager.Say(CheckpointLines[i]);
                waryWarned = false;
            }
            if (index >= route.Length - 1) { State = TailState.Dropping; stateT = dropSeconds; return; }
            float pause = index < pauseSeconds.Length ? pauseSeconds[index] : 0f;
            if (pause > 0f) { State = TailState.Pausing; stateT = pause; }
        }

        void Drop()
        {
            State = TailState.Leaving;
            Sfx.Play(SfxId.BioSquelch, Target.transform.position, 0.4f);
            MissionManager.Say(DropLine);
            MissionManager.SetFlag(MissionFlags.TailDone);
            GameEvents.RaiseToast("Petrov left something behind the dumpster and walked off. Check the dead drop.", Palette.Amoeba);
        }

        // ------------------------------------------------------------------ movement (HostBrain routine override)
        void Drive(float dt)
        {
            var brain = Target.Brain;
            switch (State)
            {
                case TailState.Walking:
                    if (next >= route.Length) { brain.ScriptStop(); break; }
                    brain.ScriptMove(route[next], walkSpeed);
                    if (brain.ScriptArrived(0.7f) || Flat(Target.transform.position - route[next]) < 0.8f) ArriveAt(next);
                    break;
                case TailState.DoublingBack:
                    brain.ScriptMove(ResumePosition, doubleBackSpeed);
                    if (brain.ScriptArrived(0.8f) || Flat(Target.transform.position - ResumePosition) < 1f)
                    { State = TailState.WaitingForFollower; followT = 0f; Alertness = 0f; }
                    break;
                case TailState.Glancing:
                    brain.ScriptStop();
                    brain.ScriptFace(glanceLook, dt, 9f);
                    break;
                case TailState.Dropping:
                    brain.ScriptStop();
                    brain.ScriptFace(Target.transform.position + Quaternion.Euler(0f, dropYaw, 0f) * Vector3.forward, dt, 6f);
                    break;
                case TailState.Leaving:
                    brain.ScriptMove(restPoint, walkSpeed);
                    if (brain.ScriptArrived(0.7f) || Flat(Target.transform.position - restPoint) < 0.8f)
                    {
                        SetRestHome();
                        State = TailState.Done;
                    }
                    break;
                case TailState.Preparing:
                    brain.ScriptStop();
                    brain.ScriptFace(Target.transform.position + startRot * Vector3.forward, dt, 4f);
                    break;
                default:
                    brain.ScriptStop();
                    break;
            }
        }

        void SetRestHome()
        {
            var rot = Quaternion.Euler(0f, restYaw, 0f);
            Target.Brain.SetHome(restPoint, rot);
            if (Target.Brain.RoutineOverride == (System.Action<float>)Drive) Target.Brain.RoutineOverride = null;
        }

        void Warp(Vector3 position, float yaw)
        {
            if (Target.Nav && Target.Nav.enabled && Target.Nav.isOnNavMesh) Target.Nav.Warp(position);
            else Target.transform.position = position;
            Target.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // ------------------------------------------------------------------ HUD
        /// <summary>Live status line for the objective panel (empty when the tail is not running).</summary>
        public string StatusLine()
        {
            switch (State)
            {
                case TailState.Preparing: return "Petrov is packing up — back off and keep 4–20 m";
                case TailState.WaitingForFollower: return $"Petrov waits at the {ResumeName} — get within {TailRules.StartRange:0} m";
                case TailState.DoublingBack: return $"Petrov is doubling back to the {ResumeName}";
                case TailState.Dropping: return "Petrov stops in the alley… stay back";
                case TailState.Glancing: return $"SHE'S LOOKING BACK — FREEZE · {Distance:0} m · alertness {Alertness:0}%";
                case TailState.Walking:
                case TailState.Pausing:
                    string band = Band switch
                    {
                        TailBand.TooClose => "TOO CLOSE",
                        TailBand.InBand => "in range",
                        TailBand.Far => "FALLING BEHIND",
                        _ => "LOSING HER",
                    };
                    return $"Petrov {Distance:0} m ({band}) · alertness {Alertness:0}%";
                default: return "";
            }
        }
    }
}
