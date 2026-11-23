using System.Collections.Generic;

namespace Parasite.Missions
{
    /// <summary>
    /// Pure, testable rules of the BLACKOUT mission: three transformers must be cut within one reroute window, and the
    /// grid logs a palm print per isolator, so every transformer needs a different human body.
    /// </summary>
    public static class BlackoutRules
    {
        public const int Required = 3;
        public const float WindowSeconds = 35f;   // from the first cut until the grid reroutes and re-arms every box
        public const float HoldSeconds = 1.5f;    // pulling one isolator
        public const float LightRadius = 24f;     // street lights each transformer feeds
        public const int RespondingGuards = 2;    // nearest guards sent to look at each outage

        public static float Remaining(float windowStart, float now, float window) =>
            windowStart < 0f ? window : System.Math.Max(0f, window - (now - windowStart));

        public static bool Expired(float windowStart, float now, float window) => windowStart >= 0f && now - windowStart > window;

        /// <summary>A body that already pulled one isolator in this window is refused at the others.</summary>
        public static bool CanOperate(int bodyId, ICollection<int> usedBodies) => !usedBodies.Contains(bodyId);

        public static bool Complete(int cut) => cut >= Required;
    }
}
