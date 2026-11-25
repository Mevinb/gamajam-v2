using UnityEngine;

namespace Parasite.Missions
{
    public enum TidePhase { Open, Warning, Surge }
    public static class TideRules
    {
        public const float OpenSeconds = 6, WarningSeconds = 2, SurgeSeconds = 4;
        public const float Period = OpenSeconds + WarningSeconds + SurgeSeconds;
        public static float Advance(float age, float delta, bool playing) => playing ? age + Mathf.Max(0, delta) : age;
        public static TidePhase Phase(float age)
        {
            float t = Mathf.Repeat(age, Period);
            return t < OpenSeconds ? TidePhase.Open : t < OpenSeconds + WarningSeconds ? TidePhase.Warning : TidePhase.Surge;
        }
        public static float Remaining(float age)
        {
            float t = Mathf.Repeat(age, Period);
            return Phase(age) == TidePhase.Open ? OpenSeconds - t : Phase(age) == TidePhase.Warning ? OpenSeconds + WarningSeconds - t : Period - t;
        }
        public static bool ReachedExit(Vector3 localPosition, bool enteredFromOutside) => enteredFromOutside && localPosition.z >= -16.2f;
    }
}
