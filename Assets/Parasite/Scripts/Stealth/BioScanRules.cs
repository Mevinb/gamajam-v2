using UnityEngine;

namespace Parasite.Stealth
{
    /// <summary>
    /// Pure, testable rules of Vale's Protocol Hound bio-scanners. Scanners read the parasite signature of POSSESSED
    /// humans only (a free amoeba has no host to read); a full meter "burns" that body, so leaving it breaks the trail.
    /// </summary>
    public static class BioScanRules
    {
        public const float Range = 7.5f;            // hunter scanner reach (m)
        public const float HalfAngle = 24f;         // cone half-angle (deg)
        public const float BaseRate = 0.55f;        // meter per second at mid range
        public const float NearBoost = 1.7f, FarFactor = 0.45f;
        public const float DecayPerSecond = 0.3f;   // the read fades once out of the cone
        public const float AlertAt = 0.5f;          // hunters call it out
        public const float GateRate = 50f;          // the doorway arch reads instantly

        public static bool Detectable(HostState state, bool isFreeAmoeba) => !isFreeAmoeba && state == HostState.Possessed;

        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 point, float range, float halfAngle)
        {
            Vector3 d = point - origin;
            float dist = d.magnitude;
            if (dist > range) return false;
            if (dist < 0.6f) return true;
            Vector3 flatF = new Vector3(forward.x, 0f, forward.z), flatD = new Vector3(d.x, 0f, d.z);
            if (flatF.sqrMagnitude < 1e-4f || flatD.sqrMagnitude < 1e-4f) return true;
            return Vector3.Angle(flatF, flatD) <= halfAngle && Mathf.Abs(d.y) < 3f;
        }

        /// <summary>Meter gain per second for a body at <paramref name="distance"/> inside the cone.</summary>
        public static float ScanRate(float distance, float range) =>
            BaseRate * Mathf.Lerp(NearBoost, FarFactor, Mathf.InverseLerp(1f, range, distance));

        public static float Step(float meter, bool scanned, float rate, float dt) =>
            Mathf.Clamp01(scanned ? meter + rate * dt : meter - DecayPerSecond * dt);

        public static bool Burned(float meter) => meter >= 1f;
    }
}
