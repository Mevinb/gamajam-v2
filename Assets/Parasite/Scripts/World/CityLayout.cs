using UnityEngine;

namespace Parasite.World
{
    /// <summary>
    /// Key coordinates of the City scene (Y up, metres). CityBuilder places the scene from these values,
    /// so they are always in sync with the built geometry. Street level is y = 0, block/sidewalk level y = 0.15,
    /// the lab floor y = 0.17. Lab interior (LabInteriorZone) = x[-23.5, 23.5] z[72.5, 103.5].
    /// </summary>
    public static class CityLayout
    {
        public const float Street = 0f, Sidewalk = 0.15f, LabFloor = 0.17f;

        public static readonly Bounds District = new(new Vector3(0f, 10f, 10f), new Vector3(180f, 20f, 200f));
        public static readonly Bounds LabInterior = new(new Vector3(0f, 2.2f, 88f), new Vector3(47f, 4.4f, 31f));

        // start: storm drain at the dead end of the south alley, facing north
        public static readonly Vector3 PlayerSpawn = new(-33f, Sidewalk, -82f);
        public static readonly Vector3 StormDrain = new(-33f, Sidewalk, -84.5f);
        public static readonly Vector3 AlleyMouth = new(-33f, Sidewalk, -42f);
        public static readonly Vector3 PlazaSpot = new(-27f, Sidewalk, 9f);
        public static readonly Vector3 Fountain = new(-36f, Sidewalk, 9f);
        public static readonly Vector3 CafeSpot = new(-55.2f, Sidewalk, 7f);
        public static readonly Vector3 ServiceLane = new(43f, Sidewalk, 13f);

        // A) main gate (guard badge) — Campus Rd north sidewalk → forecourt → lab lobby
        public static readonly Vector3 MainGate = new(0f, Sidewalk, 57.5f);
        public static readonly Vector3 MainGateOutside = new(0f, Sidewalk, 55.6f);
        public static readonly Vector3 Forecourt = new(0f, Sidewalk, 64f);
        public static readonly Vector3 MainGateInside = new(0f, LabFloor, 76f);
        public static readonly Vector3 GuardPost = new(2.2f, Sidewalk, 54.2f);
        public static readonly Vector3 CivilianNearGate = new(-14f, Sidewalk, 54.2f);

        // B) staff entrance (scientist at the terminal) — west wall of the lab, staff parking lane
        public static readonly Vector3 StaffDoor = new(-23.75f, Sidewalk, 80f);
        public static readonly Vector3 StaffDoorOutside = new(-26f, Sidewalk, 80f);
        public static readonly Vector3 StaffTerminal = new(-25.4f, Sidewalk, 82.8f);
        public static readonly Vector3 StaffTerminalPos = new(-26.6f, Sidewalk, 82.8f);
        public static readonly Vector3 StaffDoorInside = new(-21.5f, LabFloor, 80f);
        public static readonly Vector3 ScientistSmokeSpot = new(-31f, Sidewalk, 84f);

        // C) service tunnel (maintenance keys) — east service alley → enclosed tunnel → lab service room
        public static readonly Vector3 TunnelDoor = new(53.85f, Sidewalk, 90f);
        public static readonly Vector3 TunnelDoorOutside = new(56f, Sidewalk, 90f);
        public static readonly Vector3 TunnelInsideDoor = new(51.5f, Sidewalk, 90f);
        public static readonly Vector3 TunnelMid = new(39f, Sidewalk, 90f);
        public static readonly Vector3 TunnelExitInside = new(21.5f, LabFloor, 90f);
        public static readonly Vector3 MaintenanceAlleySpot = new(58f, Sidewalk, 80f);

        // lab interior
        public static readonly Vector3 LabLobby = new(0f, LabFloor, 77f);
        public static readonly Vector3 LabCorridor = new(0f, LabFloor, 86f);
        public static readonly Vector3 LabA = new(-15f, LabFloor, 96f);
        public static readonly Vector3 LabB = new(11f, LabFloor, 92f);
        public static readonly Vector3 ServiceRoom = new(20f, LabFloor, 92f);
        public static readonly Vector3 ValeOffice = new(0f, LabFloor, 98f);
        public static readonly Vector3 DrVale = new(0f, LabFloor, 100.6f);
        public const float DrValeYaw = 180f;
    }
}
