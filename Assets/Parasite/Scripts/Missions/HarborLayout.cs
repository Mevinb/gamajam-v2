using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Shared metre-scale contract for the harbor's physical layout, authored visuals and acceptance paths.</summary>
    public static class HarborLayout
    {
        public const string RootName = "Harbor Canal Works";
        public static readonly Vector3 Origin = new(1100, 0, -220);
        public static Vector3 At(float x, float y, float z) => Origin + new Vector3(x, y, z);
        public static Vector3 World(Vector3 local) => Origin + local;

        public static readonly Vector3 Spawn = At(-29, .15f, -34);
        public static readonly Vector3 PumpCheckpoint = At(-18, .15f, -23);
        public static readonly Vector3 LockCheckpoint = At(-10, .15f, 5);
        public static readonly Vector3 ManifestCheckpoint = At(3, .15f, 27);
        public static readonly Vector3 GateLocal = new(-10, 1.9f, 0);
        public static readonly Vector3 GateSize = new(4, 3.5f, .25f);
        public static readonly Vector3 GateOpenOffset = new(0, 4, 0);
        public static readonly Vector3 GaugeLocal = new(-18, 1.6f, -14);
        public static readonly Vector3[] ValveLocal =
        {
            new(-24, .15f, -17), new(-18, .15f, -17), new(-12, .15f, -17),
        };

        // Crates remain inside a physically fenced, non-walkable work area. Arrays are configuration data, never mutated.
        public static readonly Vector3[] CargoLoopLocal =
        {
            new(14, .55f, 10), new(14, .55f, 22), new(26, .55f, 22), new(26, .55f, 10),
        };
        public static readonly Vector3[] CargoBayLocal =
        {
            new(30, .55f, 29), new(32, .55f, 29), new(30, .55f, 33), new(32, .55f, 33),
        };
        public static readonly Vector3 ScanLocal = new(14, 1.05f, 16);
        public static readonly Vector3 ScannerLocal = new(7, .15f, 16);
        public static readonly Vector3 BrakeLocal = new(7, .15f, 10);
        public static Bounds ScanBounds => new(World(ScanLocal), new Vector3(2, 2, 4.4f));
        public static Bounds CargoWorkBounds => new(At(22.5f, 1.5f, 22.5f), new Vector3(25, 3, 29));
        public const float CargoSpeed = 2.2f;

        public static readonly Vector3[] RelayLocal = { new(-8, .15f, 34), new(8, .15f, 34) };
        public static Bounds RelayPad(int index) => new(World(RelayLocal[index]) + Vector3.up * .7f, new Vector3(2.4f, 1.4f, 2.4f));
        public static Vector3 RelaySightPoint(int index) => World(RelayLocal[index]) + Vector3.up * .8f;
    }
}
