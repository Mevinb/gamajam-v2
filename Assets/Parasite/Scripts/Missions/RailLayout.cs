using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Metre-scale Northline contract shared by geometry, mechanics and later physical acceptance.</summary>
    public static class RailLayout
    {
        public const string RootName = "Northline Railway Works";
        public static readonly Vector3 Origin = new(1500, 0, -220);
        public static Vector3 At(float x, float y, float z) => Origin + new Vector3(x, y, z);
        public static Vector3 World(Vector3 local) => Origin + local;
        public static readonly Vector3 Spawn = At(-24, .15f, -34);
        public static readonly Vector3 CrossingControl = At(-8, .15f, -26);
        public static readonly Vector3 CrossingReturnControl = At(-8, .15f, -6);
        public static readonly Vector3 NorthCheckpoint = At(-8, .15f, -8);
        public static readonly Vector3 ManifestControl = At(-31, .15f, 5);
        public static readonly Vector3[] PointControls = { At(-31, .15f, 12), At(-31, .15f, 19) };
        public static readonly Vector3 DispatchControl = At(-31, .15f, 24);
        public static readonly Vector3 FreightDock = At(-22, .15f, 6);
        public const float FreightReturnZ = 20.5f; // Full cradle stays south of the solid signal wall starting at z22.
        public static readonly Vector3 MedicalBay = At(28, .15f, 8);
        public static readonly Vector3 RelayBay = At(28, .15f, 18);
        public static Vector3 RequiredBay => RailCampaign.Peace ? MedicalBay : RelayBay;
        public static readonly Vector3 SignalCheckpoint = At(-12, .15f, 29);
        public static readonly Vector3 Crank = At(-10, .15f, 34);
        public static readonly Vector3 Signal = At(8, .15f, 34);
        public const float CoachWidth = 3.2f, CoachLength = 14f, CoachDeckY = .75f;
        public static readonly Vector3 CoachStart = At(8, 0, 45);
        public static readonly Vector3 Departure = At(8, CoachDeckY, 45);
        public static readonly Vector3 DepartureControlLocal = new(1.15f, CoachDeckY, -.2f);
        public static readonly Vector3 BoardingRampFoot = At(3, .15f, 45);
        public static readonly Vector3 BoardingRampLanding = At(6.8f, CoachDeckY, 45);
        public static readonly Vector3[] BoardingStops = { At(7.25f, CoachDeckY, 43.8f), At(8, CoachDeckY, 45.4f) };
        public static Bounds Crossing => new(At(0, 1.6f, -16), new Vector3(8, 3.2f, 8));
        public static Bounds CrankPad => new(Crank + Vector3.up * 1f, new Vector3(2.4f, 2f, 2.4f));
        public static Bounds SignalPad => new(Signal + Vector3.up * .7f, new Vector3(2.6f, 1.4f, 2.6f));
        public static Bounds Boarding => new(Departure + Vector3.up * .7f, new Vector3(2.8f, 1.4f, 12.8f));
        public static readonly Vector3[] CulvertRoute = { At(-4, .15f, 34), At(-1, .15f, 34), At(1, .15f, 34), At(4, .15f, 34) };
    }
}
