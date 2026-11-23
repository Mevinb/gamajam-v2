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

        // Expanded campaign (Missions 9-11)
        public static readonly Vector3 SubVaultDiversion = new(6.9f, L.LabFloor, 76.5f);
        public static readonly Vector3 BlackSiteVault = new(-21.5f, L.LabFloor, 101.2f);
        public static readonly Vector3 BackupGenerator = new(58.5f, L.Sidewalk, 74.0f);
        public static readonly Vector3 GridOverride = new(-4.5f, L.Sidewalk, 61.0f);
        public static readonly Vector3 MasterUplink = new(-36.0f, L.Sidewalk, 15.0f);

        // THE TAIL — Dr. Petrov's walk from the café terrace to her dead drop in the storm-drain alley.
        public static readonly Vector3[] TailRoute =
        {
            new(-45f, L.Sidewalk, -6f),        // across the plaza
            new(-26f, L.Sidewalk, -20.3f),     // Market St bus stop (checkpoint 1, pause)
            new(-27f, L.Sidewalk, -36.2f),     // jaywalks Market St to the south sidewalk
            new(-33f, L.Sidewalk, -41.5f),     // storm-drain alley mouth (checkpoint 2, pause)
            new(-34.5f, L.Sidewalk, -54.1f),   // the dead drop, behind the dumpster
        };
        public static readonly float[] TailPauses = { 0f, 3f, 0f, 2f, 0f };
        public static readonly Vector3 DeadDrop = new(-35.5f, L.Sidewalk + 0.55f, -54.1f);   // envelope in a wall junction box
        public static readonly Vector3 PetrovRest = new(-28.2f, L.Sidewalk, -20.3f);          // waits for the night bus afterwards

        // HUNTER PROTOCOL — Protocol Hound team, security-office scanner arch and lockdown breaker.
        public static readonly Vector3 HunterEntry = new(0f, L.LabFloor, 74.8f);
        public static readonly Vector3 HunterExit = new(0f, L.Sidewalk, 66.5f);
        public static readonly Vector3[] HunterCorridorRoute = { new(-20f, L.LabFloor, 86f), new(20.5f, L.LabFloor, 86f) };
        public static readonly Vector3[] HunterLobbyRoute = { new(0f, L.LabFloor, 76.8f), new(-6f, L.LabFloor, 81.6f), new(4.5f, L.LabFloor, 80.5f) };
        public static readonly Vector3 ScanGate = new(16.75f, L.LabFloor, 84f);          // the security office doorway
        public static readonly Vector3 HoundBreaker = new(22.75f, L.LabFloor + 1.2f, 76f); // on the security office's east wall, 10 m from the arch
        public static readonly Vector3 DutyOfficer = new(17.5f, L.LabFloor, 75.4f);
        public const float DutyOfficerYaw = 180f;                                          // watching the monitors, back to the door

        // BLACKOUT — the three transformers that feed Vale's plaza triangulation grid (front faces the player).
        public static readonly (string name, Vector3 pos, float yaw)[] Transformers =
        {
            ("TRANSFORMER P-1", new(-57.5f, L.Sidewalk, 18f), -90f),
            ("TRANSFORMER P-2", new(-14.5f, L.Sidewalk, -15f), 90f),
            ("TRANSFORMER P-3", new(-44f, L.Sidewalk, 36.5f), 0f),
        };
        public static readonly (string name, Profession profession, Vector3 pos)[] BlackoutExtras =
        {
            ("Iris Novak", Profession.Civilian, new(-41.5f, L.Sidewalk, 33.5f)),
            ("Ben Ortiz", Profession.Civilian, new(-18f, L.Sidewalk, -12.5f)),
            ("Sam Reyes", Profession.Maintenance, new(-55f, L.Sidewalk, 21.5f)),
        };

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
