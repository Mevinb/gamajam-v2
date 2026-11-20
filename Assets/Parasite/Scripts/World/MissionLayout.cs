using UnityEngine;
using L = Parasite.World.CityLayout;

namespace Parasite.World
{
    /// <summary>Positions of campaign mission objects (placed by Editor/MissionPlacement.cs). Yaw: the prop's front
    /// (where the player stands) is local -Z rotated by yaw.</summary>
    public static class MissionLayout
    {
        public static readonly Vector3 Newsstand = new(-18.5f, L.Sidewalk, 14f);
        public const float NewsstandYaw = 90f;                     // reader stands west of it, on the plaza
        public static readonly Vector3 Keycard = new(-53.6f, L.Sidewalk, 8.8f);
        public static readonly Vector3 Substation = new(61.2f, L.Sidewalk, 84f);
        public const float SubstationYaw = 90f;                    // faces west into the alley
        public static readonly Vector3 DataTerminalA = new(-22.75f, L.LabFloor, 95f);
        public const float DataTerminalAYaw = -90f;                // faces east into Lab A
        public static readonly Vector3 DataTerminalB = new(6.9f, L.LabFloor, 91f);
        public const float DataTerminalBYaw = -90f;                // faces east into Lab B
        public static readonly Vector3 OfficeDoor = new(0f, L.LabFloor, 88f);
        public static readonly Vector3 ReaderA = new(-3.2f, L.LabFloor + 1.35f, 87.8f);
        public static readonly Vector3 ReaderB = new(3.2f, L.LabFloor + 1.35f, 87.8f);

        // where a player stands to use each (front side)
        public static Vector3 Front(Vector3 pos, float yaw, float dist) => pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -dist);
    }
}
