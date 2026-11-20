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

        // Post-Vale campaign: control points stay on existing walkable floors.
        public static readonly Vector3 CoolantBypass = new(20.2f, L.LabFloor, 94f);
        public static readonly Vector3 SignedArchive = new(-3.4f, L.LabFloor, 98.5f);
        public static readonly Vector3 DispatchOverride = new(6.9f, L.LabFloor, 81f);
        public static readonly Vector3 ContainmentRelay = new(57.8f, L.Sidewalk, 94f);
        public static readonly Vector3 JournalistUplink = new(-24f, L.Sidewalk, 15.5f);
        public static readonly Vector3 Extraction = L.StormDrain + Vector3.forward * 1.4f;

        // security cameras: name, mount position, yaw of the view centre, range
        public static readonly (string name, Vector3 pos, float yaw, float range)[] Cameras =
        {
            ("Forecourt", new(-9f, 4.2f, 71.6f), 180f, 16f),
            ("Lobby", new(10.5f, 3.95f, 73.2f), -40f, 14f),
            ("Corridor", new(-22.9f, 3.95f, 86f), 90f, 16f),
            ("Staff Lane", new(-24.4f, 4.2f, 76f), -90f, 15f),
            ("Tunnel Alley", new(55f, 4.2f, 84f), 10f, 14f),
        };

        // where a player stands to use each (front side)
        public static Vector3 Front(Vector3 pos, float yaw, float dist) => pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -dist);
    }
}
