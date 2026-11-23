using UnityEngine;

namespace Parasite.Missions
{
    public enum TailBand { TooClose, InBand, Far, Lost }

    /// <summary>One frame of tail observation, built by <see cref="TailTarget"/> from the world.</summary>
    public struct TailSample
    {
        public float distance;          // nearest amoeba body to the target (m)
        public bool seenByTarget;       // inside the target's vision cone with line of sight
        public bool followerIsBlob;     // the nearest follower is a free amoeba
        public bool followerMoving;     // walking or running (speed above a casual drift)
        public bool followerRunning;    // sprinting
        public bool targetGlancing;     // the target is deliberately looking back
        public bool ignoreCrowding;     // grace period (e.g. she is still packing up at the café)
    }

    /// <summary>
    /// Pure, testable rules of the THE TAIL mission: a distance band, an alertness meter that rises when the follower
    /// crowds the target, is seen moving while she glances back, or is seen as a free amoeba, and a "lost" timer.
    /// </summary>
    public static class TailRules
    {
        public const float TooClose = 3.5f;          // closer than this always unnerves her
        public const float BandMax = 20f;            // comfortable following distance
        public const float LoseDistance = 26f;       // beyond this the trail goes cold…
        public const float LoseGraceSeconds = 5f;    // …after this long
        public const float StartRange = 20f;         // she only sets off (or resumes) with a follower this close
        public const float GlanceRange = 14f;        // a glance back only catches followers within this
        public const float BlobSightRange = 10f;     // a free amoeba is unmistakable at this range
        public const float SpottedAt = 100f;
        public const float WaryAt = 45f;             // shows a '?' over her head

        public const float CrowdRate = 30f, CrowdSeenBonus = 20f;
        public const float BlobRate = 45f;
        public const float GlanceMovingRate = 35f, GlanceStillRate = 10f, RunningMultiplier = 1.6f;
        public const float RunningSeenRate = 10f;
        public const float DecayRate = 12f;

        public static TailBand Band(float distance) =>
            distance < TooClose ? TailBand.TooClose :
            distance <= BandMax ? TailBand.InBand :
            distance <= LoseDistance ? TailBand.Far : TailBand.Lost;

        /// <summary>Alertness change per second (positive = more suspicious).</summary>
        public static float AlertnessRate(in TailSample s)
        {
            float rate = 0f;
            if (!s.ignoreCrowding && s.distance < TooClose) rate += CrowdRate + (s.seenByTarget ? CrowdSeenBonus : 0f);
            if (s.seenByTarget && s.followerIsBlob && s.distance < BlobSightRange) rate += BlobRate;
            if (s.seenByTarget && s.targetGlancing && s.distance < GlanceRange && !s.followerIsBlob)
                rate += (s.followerMoving ? GlanceMovingRate : GlanceStillRate) * (s.followerRunning ? RunningMultiplier : 1f);
            else if (s.seenByTarget && s.followerRunning && s.distance < 10f) rate += RunningSeenRate;
            return rate > 0f ? rate : -DecayRate;
        }

        public static float Step(float alertness, in TailSample s, float dt) =>
            Mathf.Clamp(alertness + AlertnessRate(s) * dt, 0f, SpottedAt);

        public static bool Spotted(float alertness) => alertness >= SpottedAt;

        /// <summary>Accumulates time spent beyond <see cref="LoseDistance"/>; any closer frame resets it.</summary>
        public static float LostTimer(float timer, float distance, float dt) => distance > LoseDistance ? timer + dt : 0f;

        public static bool Lost(float timer) => timer >= LoseGraceSeconds;

        /// <summary>Seconds of walking between glances back, cycling through a fixed, readable rhythm.</summary>
        public static float GlanceInterval(int glanceIndex) =>
            GlanceRhythm[((glanceIndex % GlanceRhythm.Length) + GlanceRhythm.Length) % GlanceRhythm.Length];

        static readonly float[] GlanceRhythm = { 6.5f, 9f, 7.5f, 10f };
    }
}
