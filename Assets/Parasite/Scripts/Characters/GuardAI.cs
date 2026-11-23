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
        float lookAroundT, lostT, stunT, sightCheckT, baseInvestigateYaw;
        bool seesSuspect;
        GameObject flashlightGo;

        void TransitionTo(GuardState next)
        {
            if (GState == next) return;
            var prev = GState;
            GState = next;
            UpdateFlashlight();
            if (next == GuardState.Investigate && prev != GuardState.Investigate)
                Sfx.Play(SfxId.AlertCurious, transform.position, 0.6f);
            else if (next == GuardState.Pursue && prev != GuardState.Pursue)
                Sfx.Play(SfxId.AlertPursuit, transform.position, 0.75f);
        }

        void UpdateFlashlight()
        {
            bool active = (GState == GuardState.Investigate || GState == GuardState.Pursue) && host && host.IsAwakeNpc && host.State == HostState.Normal;
            if (!flashlightGo && active)
            {
                flashlightGo = new GameObject("SearchFlashlight");
                flashlightGo.transform.SetParent(transform, false);
                flashlightGo.transform.localPosition = new Vector3(0.2f, 1.15f, 0.25f);
                flashlightGo.transform.localRotation = Quaternion.identity;
                var light = flashlightGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 48f;
                light.innerSpotAngle = 26f;
                light.range = 20f;
                light.intensity = 3.5f;
                light.color = new Color(0.95f, 0.97f, 1.0f);
                light.shadows = LightShadows.None;
            }
            if (flashlightGo) flashlightGo.SetActive(active);
        }

        void OnDisable()
        {
            if (flashlightGo) flashlightGo.SetActive(false);
        }

        void OnDestroy()
        {
            if (flashlightGo) Destroy(flashlightGo);
        }

        public void OrderInvestigate(Vector3 point)
        {
            if (GState == GuardState.Pursue || GState == GuardState.Stunned) return;
            investigatePoint = point;
            lookAroundT = 0f;
            TransitionTo(GuardState.Investigate);
            SetIcon('?', 6f);
        }

        public void Stun(float seconds)
        {
            if (!host || !host.IsAwakeNpc) return;
            stunT = Mathf.Max(stunT, seconds);
            TransitionTo(GuardState.Stunned);
            SetIcon('?', seconds);
        }

        public override void OnPossessed() { base.OnPossessed(); TransitionTo(GuardState.Patrol); stunT = 0f; UpdateFlashlight(); }
        public override void OnRecovered() { base.OnRecovered(); TransitionTo(GuardState.Patrol); UpdateFlashlight(); }

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
                    investigatePoint = ss != null ? ss.LastKnownPosition : transform.position;
                    lookAroundT = 0f;
                    TransitionTo(GuardState.Investigate);
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
                if (GState != GuardState.Pursue) { lostT = 0f; TransitionTo(GuardState.Pursue); }
            }
            else if (GState == GuardState.Pursue)
            {
                investigatePoint = ss != null ? ss.LastKnownPosition : transform.position;
                lookAroundT = 0f;
                TransitionTo(GuardState.Investigate);
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
                investigatePoint = ss.LastKnownPosition;
                lookAroundT = 0f;
                TransitionTo(GuardState.Investigate);
            }
        }

        void TickInvestigate(float dt)
        {
            SetIcon('?', 0.5f);
            if (lookAroundT <= 0f)
            {
                GoTo(investigatePoint, (host.Profile.walkSpeed + host.Profile.runSpeed) * 0.5f);
                host.Rig.LookAt(investigatePoint + Vector3.up);
                if (Arrived(1.2f) || (!NavReady))
                {
                    lookAroundT = 0.001f;
                    baseInvestigateYaw = transform.eulerAngles.y;
                }
            }
            else
            {
                Stop();
                lookAroundT += dt;
                float scanAngle = Mathf.Sin(lookAroundT * 1.8f) * 50f;
                float targetBodyYaw = baseInvestigateYaw + scanAngle * 0.35f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetBodyYaw, 0), 1f - Mathf.Exp(-6f * dt));
                Vector3 lookTarget = transform.position + Vector3.up * 1.5f + (Quaternion.Euler(0, baseInvestigateYaw + scanAngle, 0) * Vector3.forward) * 10f;
                host.Rig.LookAt(lookTarget);

                if (lookAroundT > 4.8f)
                {
                    host.Rig.LookAt(null);
                    TransitionTo(GuardState.Patrol);
                    AlertIcon = ' ';
                }
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
