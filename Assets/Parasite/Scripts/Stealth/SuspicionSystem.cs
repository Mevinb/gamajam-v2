using System;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using Parasite.World;
using UnityEngine;

namespace Parasite.Stealth
{
    /// <summary>
    /// Global 0–100 suspicion. Rises only when an awake NPC actually sees a violation (vision cone + line of sight):
    /// witnessed possession/split, a free amoeba in view, a discovered unconscious host, unauthorized access,
    /// trespassing in a restricted zone. Decays when nothing is observed. Drives guard investigate/pursue.
    /// </summary>
    public class SuspicionSystem : MonoBehaviour
    {
        public static SuspicionSystem Instance { get; private set; }

        public float civilianRange = 16f, guardRange = 24f, fovDegrees = 125f, nearAwareness = 2.2f;
        public float blobSightRange = 13f, unconsciousSightRange = 11f, alertRadius = 38f;

        public float Value { get; private set; }
        public SuspicionLevel Level { get; private set; }
        public Component Suspect { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public float TimeSinceObserved { get; private set; } = 999f;
        public float GraceRemaining => Mathf.Max(0f, graceUntil - Time.time);
        public event Action<SuspicionLevel, SuspicionLevel> LevelChanged;

        float scanT, graceUntil;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- suspect
        public Transform SuspectRoot => Suspect ? Suspect.transform : null;

        public bool HasValidSuspect
        {
            get
            {
                if (!Suspect) return false;
                if (Suspect is HostCharacter h) return h.State == HostState.Possessed;
                if (Suspect is AmoebaController a) return a.isActiveAndEnabled && a.gameObject.activeInHierarchy;
                return false;
            }
        }

        public Vector3 SuspectAimPoint =>
            Suspect is HostCharacter h ? h.ChestPosition :
            Suspect is AmoebaController a ? a.Center : (Suspect ? Suspect.transform.position : LastKnownPosition);

        public void ReportSuspectSeen()
        {
            if (!Suspect) return;
            LastKnownPosition = Suspect.transform.position;
            TimeSinceObserved = 0f;
        }

        public void OnSuspectGone() { Suspect = null; }

        // ---------------------------------------------------------------- vision
        public float RangeFor(HostCharacter w) => w.Profession == Profession.Guard ? guardRange : civilianRange;

        /// <summary>Can this awake NPC see the point? (range, cone, line of sight)</summary>
        public bool CanSee(HostCharacter witness, Vector3 point, Transform targetRoot, float range)
        {
            if (!witness || !witness.IsAwakeNpc) return false;
            Vector3 eye = witness.EyePosition;
            Vector3 d = point - eye;
            float dist = d.magnitude;
            if (dist > range) return false;
            if (dist > nearAwareness)
            {
                Vector3 flat = new Vector3(d.x, 0, d.z);
                if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(witness.transform.forward, flat) > fovDegrees * 0.5f) return false;
            }
            int n = Physics.RaycastNonAlloc(eye, d / Mathf.Max(dist, 0.001f), hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].transform;
                if (t.IsChildOf(witness.transform)) continue;
                if (targetRoot && t.IsChildOf(targetRoot)) continue;
                int layer = t.gameObject.layer;
                if (layer == Layers.Characters || layer == Layers.Amoeba) continue;
                return false;
            }
            return true;
        }

        HostCharacter FirstWitness(Vector3 point, Transform targetRoot, Transform ignore, float rangeScale = 1f)
        {
            HostCharacter best = null; float bestD = float.MaxValue;
            foreach (var w in HostCharacter.All)
            {
                if (!w || !w.IsAwakeNpc) continue;
                if (targetRoot && w.transform == targetRoot) continue;
                if (ignore && w.transform == ignore) continue;
                if (!CanSee(w, point, targetRoot, RangeFor(w) * rangeScale)) continue;
                float d = (w.transform.position - point).sqrMagnitude;
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        // ---------------------------------------------------------------- reporting
        /// <summary>Instant violation. Only counts if witnessed. Returns true if someone saw it.</summary>
        public bool ReportViolation(ViolationType type, Vector3 position, Component suspect, float scale = 1f, Transform ignoreWitness = null)
        {
            var targetRoot = suspect ? suspect.transform : null;
            var witness = FirstWitness(position, targetRoot, ignoreWitness);
            if (!witness) return false;
            float amount = SuspicionRules.BaseAmount(type) * scale;
            // closer witnesses are more certain
            float dist = Vector3.Distance(witness.EyePosition, position);
            amount *= Mathf.Lerp(1.2f, 0.7f, Mathf.InverseLerp(2f, RangeFor(witness), dist));
            Raise(amount, position, suspect, witness, type);
            return true;
        }

        void Raise(float amount, Vector3 position, Component suspect, HostCharacter witness, ViolationType type)
        {
            if (Time.time < graceUntil) return;
            Value = SuspicionRules.Add(Value, amount);
            if (Value > RunState.PeakSuspicion) RunState.PeakSuspicion = Value;
            TimeSinceObserved = 0f;
            LastKnownPosition = position;
            if (suspect) Suspect = suspect;

            if (witness && witness.Brain)
                witness.Brain.Notice(position, 2.5f, Value >= SuspicionRules.InvestigatingAt ? '!' : '?');

            if (!SuspicionRules.IsContinuous(type))
            {
                string who = witness ? witness.DisplayName : "Someone";
                string what = type switch
                {
                    ViolationType.WitnessedPossession => "saw you take over a body!",
                    ViolationType.WitnessedSplit => "saw you split!",
                    ViolationType.UnconsciousHostFound => "found an unconscious body.",
                    ViolationType.UnauthorizedAccess => "saw you fail an access check.",
                    _ => "noticed something.",
                };
                GameEvents.RaiseToast($"{who} {what}", Palette.Warning);
            }

            if (Level >= SuspicionLevel.Curious || type == ViolationType.UnconsciousHostFound) AlertGuards(position);
            UpdateLevel();
        }

        /// <summary>Guards near the point go investigate.</summary>
        public void AlertGuards(Vector3 position)
        {
            foreach (var h in HostCharacter.All)
            {
                if (!h || !h.IsAwakeNpc || h.Brain is not GuardAI g) continue;
                if ((h.transform.position - position).sqrMagnitude > alertRadius * alertRadius) continue;
                g.OrderInvestigate(position);
            }
        }

        /// <summary>Amoeba forced out of a captured host: brief grace + nearby guards stunned = safe escape window.</summary>
        public void OnAmoebaEjected(AmoebaController blob, Vector3 position)
        {
            Suspect = blob;
            LastKnownPosition = position;
            Value = Mathf.Min(Value, SuspicionRules.PursuingAt - 12f);
            graceUntil = Time.time + 3f;
            foreach (var h in HostCharacter.All)
                if (h && h.IsAwakeNpc && h.Brain is GuardAI g && (h.transform.position - position).sqrMagnitude < 14f * 14f)
                    g.Stun(2.5f);
            UpdateLevel();
        }

        /// <summary>For tests.</summary>
        public void SetValue(float v) { Value = Mathf.Clamp(v, 0, SuspicionRules.Max); UpdateLevel(); }

        // ---------------------------------------------------------------- continuous scan
        void Update()
        {
            float dt = Time.deltaTime;
            TimeSinceObserved += dt;
            scanT -= dt;
            if (scanT <= 0f)
            {
                const float interval = 0.2f;
                scanT = interval;
                Scan(interval);
            }
            Value = SuspicionRules.Decay(Value, TimeSinceObserved, dt);
            if (Suspect && !HasValidSuspect && Suspect is HostCharacter) Suspect = null; // player left that body
            UpdateLevel();
            Fx.SetDanger(Mathf.InverseLerp(SuspicionRules.CuriousAt, SuspicionRules.Max, Value));
        }

        void Scan(float interval)
        {
            var pm = PossessionManager.Instance;
            if (pm != null)
            {
                foreach (var a in pm.Agents)
                {
                    if (a.InTransit) continue;
                    if (a.Host == null && a.Blob && a.Blob.gameObject.activeInHierarchy)
                    {
                        // free amoeba in view
                        var w = FirstWitness(a.Blob.Center, a.Blob.transform, null, blobSightRange / civilianRange);
                        if (w)
                        {
                            float prox = Mathf.Lerp(1.6f, 0.5f, Mathf.InverseLerp(1.5f, blobSightRange, Vector3.Distance(w.EyePosition, a.Blob.Center)));
                            Raise(SuspicionRules.BaseAmount(ViolationType.BlobSighted) * interval * prox, a.Blob.Center, a.Blob, w, ViolationType.BlobSighted);
                        }
                    }
                    else if (a.Host != null && a.Host.State == HostState.Possessed)
                    {
                        // trespassing: in a restricted zone without the permission
                        var z = RestrictedZone.At(a.Host.transform.position);
                        if (z && !AccessRules.Has(a.Host.Permissions, z.required))
                        {
                            var w = FirstWitness(a.Host.ChestPosition, a.Host.transform, null, 0.8f);
                            if (w) Raise(SuspicionRules.BaseAmount(ViolationType.Trespassing) * interval, a.Host.ChestPosition, a.Host, w, ViolationType.Trespassing);
                        }
                    }
                }
            }

            // discovered unconscious bodies (once per knockout)
            foreach (var h in HostCharacter.All)
            {
                if (!h || h.State != HostState.Unconscious || h.KnockoutDiscovered) continue;
                var w = FirstWitness(h.transform.position + Vector3.up * 0.3f, h.transform, null, unconsciousSightRange / civilianRange);
                if (!w) continue;
                h.KnockoutDiscovered = true;
                Raise(SuspicionRules.BaseAmount(ViolationType.UnconsciousHostFound), h.transform.position, null, w, ViolationType.UnconsciousHostFound);
            }

            // keep last-known position fresh while any guard sees the suspect
            if (HasValidSuspect && Level >= SuspicionLevel.Investigating)
            {
                foreach (var h in HostCharacter.All)
                {
                    if (!h || !h.IsAwakeNpc || h.Brain is not GuardAI) continue;
                    if (CanSee(h, SuspectAimPoint, SuspectRoot, guardRange)) { ReportSuspectSeen(); break; }
                }
            }
        }

        void UpdateLevel()
        {
            var nl = SuspicionRules.LevelFor(Value);
            if (nl == Level) return;
            var old = Level;
            Level = nl;
            if (nl > old)
            {
                if (nl == SuspicionLevel.Pursuing) { Sfx.Ui(SfxId.AlertPursuit); GameEvents.RaiseToast("PURSUIT! Guards are hunting you — break line of sight.", Palette.Danger); }
                else if (nl == SuspicionLevel.Investigating) { Sfx.Ui(SfxId.AlertCurious); GameEvents.RaiseToast("Guards are investigating.", Palette.Warning); }
                else if (nl == SuspicionLevel.Curious) Sfx.Ui(SfxId.AlertCurious, 0.5f);
            }
            else if (nl == SuspicionLevel.Calm) GameEvents.RaiseToast("Things have calmed down.", Palette.Info);
            LevelChanged?.Invoke(old, nl);
        }
    }
}
