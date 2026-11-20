using Parasite.FX;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Characters
{
    public enum GuardState { Patrol, Investigate, Pursue, Stunned }

    /// <summary>Patrol → Investigate → Pursue → Capture, on the NavMesh. No combat.</summary>
    public class GuardAI : HostBrain
    {
        public const float CaptureRange = 1.45f;
        public const float SightRange = 24f;
        public const float LoseTrackSeconds = 6f;

        public GuardState GState { get; private set; } = GuardState.Patrol;
        Vector3 investigatePoint;
        float lookAroundT, lostT, stunT, sightCheckT;
        bool seesSuspect;

        public void OrderInvestigate(Vector3 point)
        {
            if (GState == GuardState.Pursue || GState == GuardState.Stunned) return;
            investigatePoint = point;
            lookAroundT = 0f;
            if (GState != GuardState.Investigate) Sfx.Play(SfxId.AlertCurious, transform.position, 0.5f);
            GState = GuardState.Investigate;
            SetIcon('?', 6f);
        }

        public void Stun(float seconds)
        {
            if (!host || !host.IsAwakeNpc) return;
            stunT = Mathf.Max(stunT, seconds);
            GState = GuardState.Stunned;
            SetIcon('?', seconds);
        }

        public override void OnPossessed() { base.OnPossessed(); GState = GuardState.Patrol; stunT = 0f; }
        public override void OnRecovered() { base.OnRecovered(); GState = GuardState.Patrol; }

        public override void Tick(float dt)
        {
            if (alertIconT > 0f) { alertIconT -= dt; if (alertIconT <= 0f) AlertIcon = ' '; }
            var ss = SuspicionSystem.Instance;

            if (GState == GuardState.Stunned)
            {
                stunT -= dt;
                Stop();
                if (stunT <= 0f)
                {
                    GState = GuardState.Investigate;
                    investigatePoint = ss != null ? ss.LastKnownPosition : transform.position;
                    lookAroundT = 0f;
                }
                return;
            }

            // Sight of the current suspect (throttled).
            sightCheckT -= dt;
            if (sightCheckT <= 0f)
            {
                sightCheckT = 0.15f;
                seesSuspect = ss != null && ss.HasValidSuspect && ss.CanSee(host, ss.SuspectAimPoint, ss.SuspectRoot, SightRange);
            }

            if (ss != null && ss.Level == SuspicionLevel.Pursuing && ss.HasValidSuspect && (seesSuspect || GState == GuardState.Pursue))
            {
                if (GState != GuardState.Pursue) { GState = GuardState.Pursue; lostT = 0f; Sfx.Play(SfxId.AlertPursuit, transform.position, 0.7f); }
            }
            else if (GState == GuardState.Pursue)
            {
                GState = GuardState.Investigate;
                investigatePoint = ss != null ? ss.LastKnownPosition : transform.position;
                lookAroundT = 0f;
            }

            switch (GState)
            {
                case GuardState.Pursue: TickPursue(ss, dt); break;
                case GuardState.Investigate: TickInvestigate(dt); break;
                default: base.Tick(dt); break;
            }
        }

        void TickPursue(SuspicionSystem ss, float dt)
        {
            SetIcon('!', 1f);
            if (seesSuspect) { lostT = 0f; ss.ReportSuspectSeen(); }
            else lostT += dt;

            var target = seesSuspect ? ss.SuspectRoot.position : ss.LastKnownPosition;
            GoTo(target, host.Profile.runSpeed);
            host.Rig.LookAt(target + Vector3.up);

            if (seesSuspect && (ss.SuspectRoot.position - transform.position).sqrMagnitude < CaptureRange * CaptureRange)
                TryCapture(ss);
            else if (lostT > LoseTrackSeconds)
            {
                GState = GuardState.Investigate;
                investigatePoint = ss.LastKnownPosition;
                lookAroundT = 0f;
            }
        }

        void TickInvestigate(float dt)
        {
            SetIcon('?', 0.5f);
            if (lookAroundT <= 0f)
            {
                GoTo(investigatePoint, (host.Profile.walkSpeed + host.Profile.runSpeed) * 0.5f);
                host.Rig.LookAt(investigatePoint + Vector3.up);
                if (Arrived(1.2f) || (!NavReady)) lookAroundT = 0.001f;
            }
            else
            {
                Stop();
                lookAroundT += dt;
                transform.Rotate(0, Mathf.Sin(lookAroundT * 1.6f) * 70f * dt, 0);
                if (lookAroundT > 4.5f) { GState = GuardState.Patrol; AlertIcon = ' '; }
            }
        }

        void TryCapture(SuspicionSystem ss)
        {
            var pm = PossessionManager.Instance;
            if (pm == null) return;
            if (ss.SuspectRoot.TryGetComponent<HostCharacter>(out var h) && h.State == HostState.Possessed)
            {
                h.Capture();
                pm.OnHostCaptured(h, this);
            }
            else if (ss.SuspectRoot.TryGetComponent<AmoebaController>(out var blob) && blob.isActiveAndEnabled)
            {
                pm.ContainBlob(blob.Agent, this);
            }
            Stun(2.5f);
        }
    }
}
