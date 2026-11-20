/*
 * PARASITE — City layout (Y up, metres; roads y = 0, blocks/sidewalks y = 0.15, lab floor y = 0.17).
 * All numbers below come from Parasite.World.CityLayout (Assets/Parasite/Scripts/World/CityLayout.cs).
 *
 *   z = 110 ┌──────────────────────────────── back wall (z 107.75–108.25) ──────────────────────────────┐
 *           │ NW blocks │ staff lane │ ███ VALE BIOTECH LAB ███ │  service yard  │door│ alley │ NE block │
 *           │ x<-50     │ x -50..-24 │ x -24..24, z 72..104     │ x 24..50       │hse │ 54..62│ x>62     │
 *           │           │ [B] staff  │ forecourt z 57.75..72    │ tunnel z 88.6–91.4 x 24→53.7 [C]       │
 *   z = 57  │           │   door     └──────[A] main gate x=0 ──┴────────────────┘    │       │          │
 *   z 40–52 ═════════════════════════════ CAMPUS RD (E–W) ═══════════════════════════════════════════════
 *           │ shops (x<-60) │ CITY PLAZA x -60..-12 (fountain -36,9) ║VALE║ shops │ lane z10–16 │ shops │
 *   z -34..-22 ═══════════════════════════ MARKET ST (E–W) ═════════════════════════════════════════════
 *           │ shops │ alley x -36..-30 (storm drain + spawn at south end) │ ║AVE║ x -7..7 │ shops        │
 *   z = -90 └──────────────────────────── road closed / hoarding ──────────────────────────────────────┘
 *
 * PlayerSpawn        (-33, 0.15, -82)  facing north, storm drain at (-33, -84.5)
 * A Main Gate        door at (0, 0.15, 57.5) (panel slides +x into the fence); outside (0, 0.15, 55.6);
 *                    inside = lab lobby (0, 0.17, 76). Gate guard Stationary at (2.2, 0.15, 54.2).
 * B Staff Entrance   door at (-23.75, 0.15, 80) in the lab's west wall (slides +z); terminal kiosk (-25.4, 0.15, 82.8),
 *                    stand at (-26.6, 0.15, 82.8); inside (-21.5, 0.17, 80). Scientist on a smoke break (-31, 0.15, 84).
 * C Service Tunnel   door at (53.85, 0.15, 90) facing the east alley (slides +z); outside (56, 0.15, 90);
 *                    tunnel x 24..53.7, z 88.6..91.4; exits in the lab service room at (21.5, 0.17, 90).
 *                    Maintenance worker wanders at (58, 0.15, 80).
 * Lab interior       x -23.5..23.5, z 72.5..103.5: lobby → corridor (z 84.15..87.85) → Lab A (west), Dr. Vale's
 *                    office (x -6..6, door x -1.2..1.2), Lab B, service room (east).
 * Dr. Vale           (0, 0.17, 100.6) facing the door (yaw 180); ValeOffice point (0, 0.17, 98).
 * Doors and gameplay markers live under "Gameplay" (outside "World") so the runtime NavMesh bake ignores the door
 * panels; AccessDoor carves them out while closed.
 */
using System;
using System.IO;
using Parasite.Missions;
using Parasite.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static Parasite.EditorTools.CityAssets;
using L = Parasite.World.CityLayout;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    public static class CityBuilder
    {
        const string SceneDir = "Assets/Parasite/Scenes";
        public const string BootstrapPath = SceneDir + "/Bootstrap.unity";
        public const string CityPath = SceneDir + "/City.unity";
        const string LightingPath = SettingsDir + "/CityLighting.lighting";
        const float GY = L.Sidewalk, LY = L.LabFloor;
        static readonly Vector3 SunEuler = new(11f, 220f, 0f);   // matches the HDRI sun (azimuth +40° from +Z, ~8° up)
        static readonly Color SunColor = new(1f, 0.74f, 0.52f);

        [MenuItem("Parasite/Setup/Build Scenes")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ProjectSetup.Run();
            FxSetup.Run();
            Prepare();
            Directory.CreateDirectory(SceneDir);
            BuildBootstrap();
            BuildCity();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(CityPath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Parasite] Scenes built: " + BootstrapPath + ", " + CityPath);
        }

        public static void BuildAllBatch()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ================================================================== City
        static void BuildCity()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("World").transform;
            var skyline = new GameObject("Skyline").transform;
            var gameplay = new GameObject("Gameplay").transform;
            var lighting = new GameObject("Lighting").transform;
            var k = new WorldKit(world, 1337);

            Ground(k);
            Streets(k);
            SouthBlock(k);
            MiddleBlock(k);
            NorthBlock(k);
            Lab(k);
            Tunnel(k);
            Boundaries(k);
            k.Root = skyline;
            Skyline(k, new Rect(-104f, -104f, 208f, 228f), new Rect(-200f, -190f, 400f, 400f));
            k.Root = world;
            Gameplay(k, gameplay);
            int renderers = k.Flush();

            Environment(lighting);
            CityLights(lighting);
            Probe(lighting, "Probe_Plaza", new Vector3(-36f, 5f, 9f), new Vector3(54f, 14f, 58f), false);
            Probe(lighting, "Probe_South", new Vector3(-12f, 5f, -60f), new Vector3(64f, 14f, 60f), false);
            Probe(lighting, "Probe_Campus", new Vector3(10f, 5f, 58f), new Vector3(100f, 14f, 30f), false);
            Probe(lighting, "Probe_Lab", new Vector3(0f, 2.3f, 88f), new Vector3(47f, 4.4f, 31f), true);
            Probe(lighting, "Probe_Market", new Vector3(0f, 3f, -28f), new Vector3(180f, 14f, 22f), false);
            Probe(lighting, "Probe_Avenue", new Vector3(0f, 3f, 8f), new Vector3(24f, 14f, 60f), false);
            Probe(lighting, "Probe_Alley", new Vector3(-33f, 2.5f, -64f), new Vector3(6f, 8f, 48f), true);

            new GameObject("CityDirector").AddComponent<CityDirector>();
            MainCamera(L.PlayerSpawn + new Vector3(0f, 2.4f, -4.5f), Quaternion.Euler(15f, 0f, 0f));

            SaveAndBake(scene, CityPath);
            Debug.Log($"[Parasite] City built: {renderers} merged renderers, {k.LastTriangles} triangles, prop triangles {k.PropTriangles}.");
        }

        // ------------------------------------------------------------------ ground & streets
        static void Ground(WorldKit k)
        {
            k.BoxMM(M("Asphalt"), -100f, 100f, -1f, 0f, -100f, 120f);
            var sw = M("Sidewalk");
            Slab(k, sw, -100f, -7f, -100f, -34f, Faces.E | Faces.N);
            Slab(k, sw, 7f, 100f, -100f, -34f, Faces.W | Faces.N);
            Slab(k, sw, -100f, -7f, -22f, 40f, Faces.E | Faces.N | Faces.S);
            Slab(k, sw, 7f, 100f, -22f, 40f, Faces.W | Faces.N | Faces.S);
            Slab(k, sw, -100f, 100f, 52f, 120f, Faces.S);
            k.BoxMM(M("Cobble"), -59.5f, -12.6f, GY - 0.01f, GY + 0.003f, -20.4f, 38.4f, false);   // plaza paving
        }

        static void Slab(WorldKit k, Material m, float x0, float x1, float z0, float z1, Faces curbs)
        {
            k.BoxMM(m, x0, x1, 0f, GY, z0, z1);
            var c = M("Curb");
            const float w = 0.3f, o = 0.02f, h = GY + 0.012f;
            if ((curbs & Faces.E) != 0) k.BoxMM(c, x1 - w, x1 + o, 0f, h, z0, z1, false);
            if ((curbs & Faces.W) != 0) k.BoxMM(c, x0 - o, x0 + w, 0f, h, z0, z1, false);
            if ((curbs & Faces.N) != 0) k.BoxMM(c, x0, x1, 0f, h, z1 - w, z1 + o, false);
            if ((curbs & Faces.S) != 0) k.BoxMM(c, x0, x1, 0f, h, z0 - o, z0 + w, false);
        }

        // Road markings are flat quads 6 mm above the asphalt (never boxes, never overlapping each other or the curbs).
        const float PaintY = 0.006f;

        static void Stripe(WorldKit k, Material m, float x0, float x1, float z0, float z1) =>
            k.FloorQuad(m, new Vector3((x0 + x1) * 0.5f, PaintY, (z0 + z1) * 0.5f), Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0), 0f, new Rect(0, 0, 1, 1));

        static void DashesX(WorldKit k, Material m, float x0, float x1, float z, float w, float dash = 3f, float gap = 3f)
        {
            for (float x = x0; x + dash <= x1; x += dash + gap) Stripe(k, m, x, x + dash, z - w * 0.5f, z + w * 0.5f);
        }

        static void DashesZ(WorldKit k, Material m, float z0, float z1, float x, float w, float dash = 3f, float gap = 3f)
        {
            for (float z = z0; z + dash <= z1; z += dash + gap) Stripe(k, m, x - w * 0.5f, x + w * 0.5f, z, z + dash);
        }

        static void Markings(WorldKit k)
        {
            var yellow = M("PaintYellow"); var white = M("PaintWhite");
            // Vale Avenue (x -7..7): double yellow centre line, lane/parking edge lines, parking ticks, stop lines
            foreach (var (z0, z1) in new[] { (-78.6f, -38.4f), (-17.6f, 35.6f) })
            {
                Stripe(k, yellow, -0.2f, -0.08f, z0, z1); Stripe(k, yellow, 0.08f, 0.2f, z0, z1);
                Stripe(k, white, -4.32f, -4.2f, z0, z1); Stripe(k, white, 4.2f, 4.32f, z0, z1);
                for (float z = z0 + 3f; z < z1 - 1f; z += 6f) { Stripe(k, white, -6.8f, -4.32f, z, z + 0.12f); Stripe(k, white, 4.32f, 6.8f, z, z + 0.12f); }
            }
            Stripe(k, white, 0.3f, 6.8f, -38.4f, -38.0f);     // northbound stop line before Market St
            Stripe(k, white, -6.8f, -0.3f, -18.0f, -17.6f);   // southbound stop line after Market St
            Stripe(k, white, 0.3f, 6.8f, 35.6f, 36.0f);       // northbound stop line before Campus Rd
            // Market St (z -34..-22) and Campus Rd (z 40..52): dashed centre, solid parking edge lines
            foreach (var (zc, e0, e1) in new[] { (-28f, -30.3f, -25.7f), (46f, 42.7f, 49.3f) })
                foreach (var (x0, x1) in new[] { (-88f, -11.6f), (11.6f, 88f) })
                {
                    DashesX(k, white, x0 + 0.5f, x1, zc, 0.15f);
                    Stripe(k, white, x0, x1, e0 - 0.06f, e0 + 0.06f); Stripe(k, white, x0, x1, e1 - 0.06f, e1 + 0.06f);
                }
            // zebra crossings
            foreach (float zc in new[] { -36.5f, -19.5f, 37.5f })
                for (float x = -6.2f; x <= 6.3f; x += 1.1f) Stripe(k, white, x - 0.28f, x + 0.28f, zc - 1.3f, zc + 1.3f);
            foreach (float xc in new[] { -9.5f, 9.5f })
            {
                for (float z = -33.4f; z <= -22.5f; z += 1.1f) Stripe(k, white, xc - 1.3f, xc + 1.3f, z - 0.28f, z + 0.28f);
                for (float z = 40.6f; z <= 51.5f; z += 1.1f) Stripe(k, white, xc - 1.3f, xc + 1.3f, z - 0.28f, z + 0.28f);
            }
        }

        static void RoadSurface(WorldKit k)
        {
            // manholes, storm grates at the curbs, patched / cracked asphalt, puddles (high smoothness) for evening reflections
            foreach (var p in new[] { new Vector3(2.5f, 0f, -60f), new Vector3(-3f, 0f, 10f), new Vector3(40f, 0f, -27f), new Vector3(-50f, 0f, 47f),
                                      new Vector3(30f, 0f, 44f), new Vector3(-2.4f, 0f, -28f), new Vector3(-62f, 0f, -29f), new Vector3(64f, 0f, 47.5f), new Vector3(2.8f, 0f, 24f) })
                CityProps.Manhole(k, p, 0.9f);
            foreach (var (p, alongX) in new[] { (new Vector3(-6.75f, 0f, -55f), false), (new Vector3(6.75f, 0f, -8f), false), (new Vector3(-6.75f, 0f, 20f), false),
                                                (new Vector3(-40f, 0f, -33.75f), true), (new Vector3(36f, 0f, -22.25f), true), (new Vector3(-30f, 0f, 40.25f), true), (new Vector3(44f, 0f, 51.75f), true) })
            {
                var s = alongX ? new Vector3(0.9f, 0.012f, 0.45f) : new Vector3(0.45f, 0.012f, 0.9f);
                k.Box(M("MetalDark"), p + Vector3.up * 0.003f, s);
                for (int i = -3; i <= 3; i++) k.Box(M("Tire"), p + Vector3.up * 0.0095f + (alongX ? new Vector3(i * 0.11f, 0, 0) : new Vector3(0, 0, i * 0.11f)), alongX ? new Vector3(0.05f, 0.002f, 0.36f) : new Vector3(0.36f, 0.002f, 0.05f));
            }
            var rng = new System.Random(77);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 26; i++) CityProps.RoadDamage(k, new Vector3(R(-5.5f, 5.5f), 0f, R(-78f, 50f)), R(2.5f, 5f), R(-20f, 20f) + (rng.Next(2) == 0 ? 0f : 90f));
            for (int i = 0; i < 30; i++) CityProps.RoadDamage(k, new Vector3(R(-86f, 86f), 0f, rng.Next(2) == 0 ? R(-33f, -23f) : R(41f, 51f)), R(2.5f, 5f), R(70f, 110f));
            foreach (var (p, sx, sz) in new[] { (new Vector3(-6f, 0f, -42f), 2.6f, 1.6f), (new Vector3(5.8f, 0f, -14f), 2.2f, 3.4f), (new Vector3(-5.9f, 0f, 14f), 1.8f, 2.6f),
                                               (new Vector3(-20f, 0f, -33.2f), 3.2f, 1.4f), (new Vector3(18f, 0f, -22.9f), 2.6f, 1.3f), (new Vector3(-45f, 0f, -22.9f), 2.8f, 1.2f),
                                               (new Vector3(2f, 0f, -26f), 3.5f, 2.5f), (new Vector3(-36f, 0f, 51.1f), 3f, 1.3f), (new Vector3(20f, 0f, 40.9f), 2.6f, 1.2f),
                                               (new Vector3(-3f, 0f, 44f), 2.4f, 1.8f), (new Vector3(55f, 0f, -33.1f), 3f, 1.2f), (new Vector3(5.9f, 0f, 30f), 1.8f, 2.8f) })
                CityProps.Puddle(k, p, sx, sz, R(0f, 180f));
        }

        static void Streets(WorldKit k)
        {
            Markings(k);
            RoadSurface(k);

            // lamps (curb side, lantern posts)
            foreach (float z in new[] { -80f, -63f, -46f, -12f, 6f, 24f })
            {
                CityProps.Lamp(k, new Vector3(-7.6f, GY, z), Vector3.right);
                CityProps.Lamp(k, new Vector3(7.6f, GY, z + 9f), Vector3.left);
            }
            foreach (float x in new[] { -70f, -50f, -26f, 26f, 50f, 70f }) CityProps.Lamp(k, new Vector3(x, GY, -34.6f), Vector3.forward);
            foreach (float x in new[] { -60f, -42f, 30f, 60f }) CityProps.Lamp(k, new Vector3(x, GY, -21.4f), Vector3.back);
            foreach (float x in new[] { -70f, -48f, -22f, 22f, 48f, 70f }) CityProps.Lamp(k, new Vector3(x, GY, 39.4f), Vector3.forward);
            foreach (float x in new[] { -62f, -36f, -16f, 14f, 32f, 56f, 76f }) CityProps.Lamp(k, new Vector3(x, GY, 52.6f), Vector3.back);

            foreach (float z in new[] { -72f, -55f, -3f, 15f, 31f }) { CityProps.Tree(k, new Vector3(-7.9f, GY, z)); CityProps.Tree(k, new Vector3(7.9f, GY, z - 6f)); }
            foreach (float x in new[] { -80f, -58f, -38f, 38f, 60f, 82f }) CityProps.Tree(k, new Vector3(x, GY, -18f), 0.9f);
            foreach (float x in new[] { -78f, -58f, -34f, 34f, 58f, 80f }) CityProps.Tree(k, new Vector3(x, GY, 36.2f), 0.9f);
            foreach (float x in new[] { -66f, -44f, 18f, 40f, 62f }) CityProps.Tree(k, new Vector3(x, GY, -37.6f), 0.85f);

            // parked cars (blockers) — mixed bodies, a couple of taxis
            CityProps.Car(k, new Vector3(5.4f, 0f, -10f), true, "CarRed");
            CityProps.Car(k, new Vector3(5.4f, 0f, 22f), true, "CarWhite");
            CityProps.Car(k, new Vector3(-5.4f, 0f, -2f), true, "CarTaxi");
            CityProps.Car(k, new Vector3(-5.4f, 0f, 28f), true, "CarBlack");
            CityProps.Car(k, new Vector3(5.4f, 0f, -60f), true, "CarSilver");
            CityProps.Car(k, new Vector3(-5.4f, 0f, -50f), true, "CarRed");
            CityProps.Car(k, new Vector3(-5.4f, 0f, -66f), true, "CarGreen");
            CityProps.Car(k, new Vector3(5.4f, 0f, 4f), true, "CarBeige");
            CityProps.Car(k, new Vector3(-48f, 0f, -24.6f), false, "CarSilver");
            CityProps.Car(k, new Vector3(28f, 0f, -24.6f), false, "CarBlue");
            CityProps.Car(k, new Vector3(58f, 0f, -31.4f), false, "CarWhite");
            CityProps.Car(k, new Vector3(-70f, 0f, -31.4f), false, "CarBlack");
            CityProps.Car(k, new Vector3(-22f, 0f, -31.4f), false, "CarTaxi");
            CityProps.Car(k, new Vector3(44f, 0f, -24.6f), false, "CarBeige");
            CityProps.Car(k, new Vector3(-60f, 0f, 50.4f), false, "CarWhite");
            CityProps.Car(k, new Vector3(28f, 0f, 50.4f), false, "CarRed");
            CityProps.Car(k, new Vector3(70f, 0f, 41.6f), false, "CarSilver");
            CityProps.Car(k, new Vector3(-40f, 0f, 41.6f), false, "CarBlue");
            CityProps.Car(k, new Vector3(52f, 0f, 41.6f), false, "CarGreen");

            foreach (var p in new[] { new Vector3(-10.5f, GY, -42f), new Vector3(10.6f, GY, -16f), new Vector3(-11.3f, GY, 29f), new Vector3(-20f, GY, 54f), new Vector3(22f, GY, 54f), new Vector3(-28f, GY, -37.5f) })
                Bin(k, p);
            // hydrants, utility cabinets, newspaper/planters along the sidewalks (patrol lines at x = ±9.8 and z = 54.8 stay clear)
            foreach (var p in new[] { new Vector3(-7.75f, GY, -60f), new Vector3(7.75f, GY, -28f + 34f), new Vector3(7.75f, GY, 12f), new Vector3(-44f, GY, -34.55f),
                                      new Vector3(36f, GY, -21.45f), new Vector3(-30f, GY, 39.45f), new Vector3(42f, GY, 52.55f), new Vector3(-7.75f, GY, -16f) })
                CityProps.Hydrant(k, p, k.Rand(0f, 360f));
            foreach (var (p, yaw) in new[] { (new Vector3(-11.55f, GY, -46f), 90f), (new Vector3(11.55f, GY, -20f), -90f), (new Vector3(11.55f, GY, 37.6f), -90f),
                                            (new Vector3(-56f, GY, -38.6f), 0f), (new Vector3(66f, GY, -17.4f), 180f), (new Vector3(-74f, GY, 35.4f), 0f) })
                CityProps.UtilityBox(k, p, yaw, k.Chance(0.4f));
            foreach (var (p, yaw) in new[] { (new Vector3(-16f, GY, -38.5f), 0f), (new Vector3(16f, GY, -17.5f), 0f), (new Vector3(-50f, GY, -17.6f), 0f), (new Vector3(62.5f, GY, -38.4f), 0f) })
                CityProps.Planter(k, p, yaw);

            Signpost(k, new Vector3(-11.3f, GY, 33.5f), "VALE BIOTECH  ^", 0f, "NeonGreen");
            Signpost(k, new Vector3(-29f, GY, -35.3f), "VALE BIOTECH  ^  NORTH", 0f, "NeonGreen");
            Signpost(k, new Vector3(9.7f, GY, -38f), "VALE AVENUE", 0f, "NeonCyan");
            Signpost(k, new Vector3(-12.2f, GY, -21.6f), "MARKET ST", 0f, "NeonCyan");
            Signpost(k, new Vector3(12.2f, GY, 39.6f), "CAMPUS RD", 180f, "NeonCyan");
        }

        static void RoadClosed(WorldKit k, Vector3 pos, float yaw)
        {
            k.Board("ROAD CLOSED", pos, yaw, 3.4f, 0.6f, Color.white, M("NeonRed"));
            var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            foreach (float s in new[] { -1.5f, 1.5f })
                k.Box(M("MetalDark"), new Vector3(pos.x, 0f, pos.z) + side * s + Vector3.up * (pos.y * 0.5f), new Vector3(0.08f, pos.y, 0.08f));
        }

        static void Bin(WorldKit k, Vector3 p) => CityProps.TrashCan(k, p);

        static void Signpost(WorldKit k, Vector3 p, string text, float yaw, string neon)
        {
            k.Cylinder(M("MetalDark"), p, 0.06f, 3.6f);
            k.Board(text, p + Vector3.up * 3.3f + Quaternion.Euler(0, yaw, 0) * Vector3.back * 0.1f, yaw, text.Length * 0.2f + 0.6f, 0.45f, Color.white, M(neon));
            k.Solid(p + Vector3.up * 1.2f, new Vector3(0.2f, 2.4f, 0.2f));
        }

        // ------------------------------------------------------------------ south block (start alley)
        static void SouthBlock(WorldKit k)
        {
            CityProps.Building(k, Facade("BrickRed"), -30f, -12f, -100f, -39f, 16f, Faces.E | Faces.N | Faces.W,
                new Shop(Faces.E, 48f, 10f, "24/7 MART", "AwningGreen", "NeonGreen"), new Shop(Faces.N, 9f, 12f, "KITO RAMEN", "AwningRed", "NeonRed"),
                new Shop(Faces.E, 33f, 8f, "PHONE FIX", "AwningNavy", "NeonCyan"), new Shop(Faces.E, 21f, 8f, "BAKERY", "AwningCream", "NeonPink"));
            CityProps.Building(k, Facade("BrickOchre"), -62f, -36f, -100f, -39f, 13f, Faces.N | Faces.E,
                new Shop(Faces.N, 13f, 14f, "LUCKY LAUNDRY", "AwningNavy", "NeonCyan"));
            CityProps.Building(k, Facade("LoftBrown"), -100f, -62f, -100f, -39f, 20f, Faces.N,
                new Shop(Faces.N, 24f, 12f, "PAWN & GOLD", "AwningYellow", "NeonPink"), new Shop(Faces.N, 8f, 8f, "LIQUOR", "AwningBlack", "NeonRed"));
            CityProps.Building(k, Facade("StoneGrey"), 12f, 40f, -100f, -39f, 18f, Faces.W | Faces.N,
                new Shop(Faces.W, 45f, 12f, "HALCYON BOOKS", "AwningGreen", "NeonGreen"), new Shop(Faces.N, 14f, 14f, "NOVA PHARMACY", "AwningNavy", "NeonCyan"),
                new Shop(Faces.W, 30f, 8f, "DRY CLEAN", "AwningCream", "NeonCyan"), new Shop(Faces.W, 16f, 9f, "TACO LOCO", "AwningYellow", "NeonRed"));
            CityProps.Building(k, Facade("BrickCream"), 40f, 64f, -100f, -39f, 14f, Faces.N, new Shop(Faces.N, 12f, 12f, "METRO DINER", "AwningRed", "NeonRed"));
            CityProps.Building(k, Facade("StoneSand"), 64f, 100f, -100f, -39f, 24f, Faces.N,
                new Shop(Faces.N, 12f, 10f, "HOTEL ARDEN", "AwningNavy", "NeonPink"), new Shop(Faces.N, 27f, 8f, "TATTOO", "AwningBlack", "NeonGreen"));

            // alley x -36..-30: dead end + storm drain
            k.BoxMM(M("BrickDark"), -36f, -30f, 0f, 6.5f, -88.5f, -87.5f);
            k.BoxMM(M("Curb"), -36f, -30f, 6.5f, 6.75f, -88.7f, -87.3f, false);
            var drain = L.StormDrain;
            // Flush cast-iron grille: dark slots over the paving, with a thin matte frame and bars.
            k.Box(M("Tire"), drain + Vector3.up * 0.001f, new Vector3(1.4f, 0.002f, 0.9f));
            foreach (float z in new[] { -0.41f, 0.41f })
                k.Box(M("DrainIron"), drain + new Vector3(0f, 0.003f, z), new Vector3(1.4f, 0.006f, 0.08f));
            foreach (float x in new[] { -0.66f, 0.66f })
                k.Box(M("DrainIron"), drain + new Vector3(x, 0.003f, 0f), new Vector3(0.08f, 0.006f, 0.74f));
            for (float x = -0.55f; x <= 0.56f; x += 0.22f)
                k.Box(M("DrainIron"), drain + new Vector3(x, 0.003f, 0f), new Vector3(0.055f, 0.006f, 0.74f));
            CityProps.Puddle(k, new Vector3(-32.1f, GY, -79f), 2.2f, 1.6f, 20f);
            CityProps.Puddle(k, new Vector3(-34f, GY, -64f), 1.6f, 2.6f, 75f);
            CityProps.Puddle(k, new Vector3(-33f, GY, -85.8f), 3f, 1.6f, 0f);
            CityProps.Puddle(k, new Vector3(-31.2f, GY, -50f), 1.4f, 2.2f, 40f);
            CityProps.Dumpster(k, new Vector3(-34.9f, GY, -73f), false);
            CityProps.Dumpster(k, new Vector3(-34.9f, GY, -56f), false);
            CityProps.Crate(k, new Vector3(-30.8f, GY, -67f), new Vector3(1.1f, 1f, 1.1f));
            CityProps.Crate(k, new Vector3(-30.8f, GY + 1f, -67.1f), new Vector3(0.8f, 0.7f, 0.8f));
            CityProps.Crate(k, new Vector3(-31f, GY, -48f), new Vector3(1.2f, 0.8f, 1f), "MetalDark");
            k.Box(M("DoorService"), new Vector3(-30.03f, GY + 1.15f, -76f), new Vector3(0.08f, 2.3f, 1.1f));
            k.Box(M("LampHead"), new Vector3(-30.12f, 2.95f, -76f), new Vector3(0.18f, 0.12f, 0.4f));
            k.Box(M("DoorService"), new Vector3(-35.97f, GY + 1.15f, -62f), new Vector3(0.08f, 2.3f, 1.1f));
            foreach (float z in new[] { -82f, -58f, -44f }) k.Cylinder(M("Pipe"), new Vector3(-30.15f, 0f, z), 0.08f, 15.5f);
            k.Cylinder(M("Pipe"), new Vector3(-35.85f, 0f, -70f), 0.1f, 12.5f);
            k.Pipe(M("Pipe"), new Vector3(-35.8f, 3.6f, -86f), new Vector3(-35.8f, 3.6f, -40f), 0.09f);
            // alley dressing: fire escapes, AC units, wall lamps, shutters, tags, trash
            CityProps.FireEscape(k, new Vector3(-36f, 0f, -100f), Vector3.forward, Vector3.right, 34f, 3.4f, 2, 3.2f, 4.35f);
            CityProps.FireEscape(k, new Vector3(-30f, 0f, -39f), Vector3.back, Vector3.left, 47f, 3.4f, 3, 3.6f, 4.35f);
            foreach (var (p, yaw) in new[] { (new Vector3(-30.05f, 2.7f, -80f), -90f), (new Vector3(-30.05f, 2.9f, -52f), -90f), (new Vector3(-35.95f, 2.6f, -47f), 90f),
                                            (new Vector3(-30.05f, 7.6f, -70f), -90f), (new Vector3(-35.95f, 8.1f, -58f), 90f), (new Vector3(-35.95f, 2.5f, -81f), 90f) })
                CityProps.AirCon(k, p, yaw);
            CityProps.SecurityLight(k, new Vector3(-30.08f, 3.0f, -76f), -90f);
            CityProps.SecurityLight(k, new Vector3(-35.92f, 3.0f, -62f), 90f);
            CityProps.Shutter(k, new Vector3(-30.02f, GY, -60f), -90f, 2.6f);
            CityProps.Shutter(k, new Vector3(-35.98f, GY, -76.5f), 90f, 2.4f);
            foreach (var (p, n) in new[] { (new Vector3(-30f, 1.4f, -84f), Vector3.left), (new Vector3(-36f, 1.6f, -68f), Vector3.right), (new Vector3(-30f, 1.3f, -64f), Vector3.left),
                                          (new Vector3(-36f, 1.2f, -52f), Vector3.right), (new Vector3(-33f, 1.6f, -87.5f), Vector3.back), (new Vector3(-30f, 1.5f, -45f), Vector3.left) })
                CityProps.Graffiti(k, p, n, k.Rand(1.6f, 2.4f));
            CityProps.TrashPile(k, new Vector3(-35f, GY, -70.6f), 4);
            CityProps.TrashPile(k, new Vector3(-31f, GY, -57.5f), 3);
            CityProps.Barrel(k, new Vector3(-35.3f, GY, -79.2f));
            CityProps.Barrel(k, new Vector3(-35.4f, GY, -78.4f));
            CityProps.PlasticCrates(k, new Vector3(-30.7f, GY, -70f), 3);
            CityProps.PlasticCrates(k, new Vector3(-30.7f, GY, -70.6f), 2);
            CityProps.Tyre(k, new Vector3(-35.4f, GY, -52f));
            CityProps.UtilityBox(k, new Vector3(-35.75f, GY, -86.6f), 90f);
            k.Text("STORM DRAIN 7", drain + new Vector3(0f, 0.03f, -0.7f), 0f, 0.12f, new Color(0.6f, 0.6f, 0.55f), null, false).transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // avenue south closure
            CityProps.Barrier(k, new Vector3(-7f, 0f, -79f), new Vector3(7f, 0f, -79f));
            RoadClosed(k, new Vector3(0f, 1.75f, -79.05f), 180f);
            CityProps.Truck(k, new Vector3(0f, 0f, -84f), false, "CarBlue", "CITY WORKS");
            CityProps.Hoarding(k, new Vector3(-12f, 0f, -88.6f), new Vector3(12f, 0f, -88.6f));
        }

        // ------------------------------------------------------------------ middle block (plaza, shops)
        static void MiddleBlock(WorldKit k)
        {
            CityProps.Building(k, Facade("BrickWhite"), -100f, -60f, -17f, 10f, 15f, Faces.E | Faces.S,
                new Shop(Faces.E, 20f, 12f, "CAFE LUMEN", "AwningGreen", "NeonGreen"), new Shop(Faces.S, 26f, 10f, "NOODLE BAR", "AwningRed", "NeonRed"));
            CityProps.Building(k, Facade("BrickRed"), -100f, -60f, 10f, 35f, 19f, Faces.E | Faces.N, new Shop(Faces.E, 14f, 10f, "FLORA", "AwningYellow", "NeonPink"));
            CityProps.Building(k, Facade("LoftCharcoal"), 12f, 48f, -17f, 10f, 22f, Faces.W | Faces.S | Faces.N,
                new Shop(Faces.W, 13f, 14f, "PIXEL ARCADE", "AwningNavy", "NeonPink"), new Shop(Faces.S, 18f, 10f, "QUICK PRINT", "AwningYellow", "NeonCyan"));
            CityProps.Building(k, Facade("BrickWhite"), 12f, 48f, 16f, 35f, 12f, Faces.W | Faces.N | Faces.S,
                new Shop(Faces.W, 10f, 12f, "OSAKA SUSHI", "AwningRed", "NeonRed"), new Shop(Faces.N, 10f, 9f, "BARBER", "AwningCream", "NeonRed"));
            CityProps.Building(k, Facade("StoneSand"), 48f, 100f, -17f, 35f, 18f, Faces.S | Faces.N | Faces.W,
                new Shop(Faces.S, 21f, 14f, "CITY BANK", "AwningNavy", "NeonCyan"));

            // plaza
            var f = L.Fountain;
            foreach (var (a0, a1, b0, b1) in new[] { (-9f, 9f, -9f, -8.6f), (-9f, 9f, 8.6f, 9f), (-9f, -8.6f, -8.6f, 8.6f), (8.6f, 9f, -8.6f, 8.6f) })
                k.BoxMM(M("Stone"), f.x + a0, f.x + a1, GY - 0.01f, GY + 0.008f, f.z + b0, f.z + b1, false);
            k.BoxMM(M("Concrete"), f.x - 3.5f, f.x + 3.5f, 0f, 0.65f, f.z - 3.5f, f.z - 3.1f, false);
            k.BoxMM(M("Concrete"), f.x - 3.5f, f.x + 3.5f, 0f, 0.65f, f.z + 3.1f, f.z + 3.5f, false);
            k.BoxMM(M("Concrete"), f.x - 3.5f, f.x - 3.1f, 0f, 0.65f, f.z - 3.1f, f.z + 3.1f, false);
            k.BoxMM(M("Concrete"), f.x + 3.1f, f.x + 3.5f, 0f, 0.65f, f.z - 3.1f, f.z + 3.1f, false);
            k.BoxMM(M("Water"), f.x - 3.1f, f.x + 3.1f, 0.1f, 0.45f, f.z - 3.1f, f.z + 3.1f, false);
            k.Cylinder(M("Concrete"), new Vector3(f.x, 0.3f, f.z), 0.45f, 1.4f);
            k.Cylinder(M("Concrete"), new Vector3(f.x, 1.6f, f.z), 1.3f, 0.22f);
            k.Cylinder(M("Water"), new Vector3(f.x, 1.7f, f.z), 1.15f, 0.14f);
            k.Cylinder(M("Concrete"), new Vector3(f.x, 1.8f, f.z), 0.18f, 0.9f);
            k.Solid(new Vector3(f.x, 0.45f, f.z), new Vector3(7f, 0.9f, 7f));
            CityProps.Bench(k, new Vector3(f.x, GY, f.z - 6.2f), Vector3.forward);
            CityProps.Bench(k, new Vector3(f.x, GY, f.z + 6.2f), Vector3.back);
            CityProps.Bench(k, new Vector3(f.x - 6.2f, GY, f.z), Vector3.right);
            CityProps.Bench(k, new Vector3(f.x + 6.2f, GY, f.z), Vector3.left);
            foreach (var p in new[] { new Vector3(-53f, GY, -11f), new Vector3(-19f, GY, -11f), new Vector3(-53f, GY, 29f), new Vector3(-19f, GY, 29f), new Vector3(-47f, GY, -1f), new Vector3(-25f, GY, 19f) })
                CityProps.Tree(k, p, k.Rand(0.9f, 1.15f));
            foreach (var p in new[] { new Vector3(-44f, GY, 26f), new Vector3(-27f, GY, -6f) }) CityProps.Bench(k, p, Vector3.forward);

            // news kiosk
            k.BoxMM(M("PlasterTeal"), -21.5f, -18.5f, 0f, 2.6f, 21f, 23f);
            k.BoxMM(M("AwningGreen"), -21.9f, -18.1f, 2.6f, 2.8f, 20.4f, 23.4f, false);
            k.Box(M("ShopGlow"), new Vector3(-21.53f, 1.5f, 22f), new Vector3(0.08f, 1.2f, 1.6f));
            k.Board("NEWS", new Vector3(-22f, 3.1f, 22f), 90f, 1.8f, 0.45f, Color.white, M("NeonCyan"));

            // cafe terrace
            foreach (float z in new[] { -1.5f, 2.5f })
            {
                var t = new Vector3(-57.6f, GY, z);
                k.Cylinder(M("MetalDark"), t, 0.05f, 0.74f);
                k.Cylinder(M("Wood"), t + Vector3.up * 0.74f, 0.42f, 0.04f);
                k.Cylinder(M("MetalDark"), t + Vector3.up * 0.78f, 0.03f, 1.6f);
                k.Cylinder(M("AwningGreen"), t + Vector3.up * 2.3f, 1.25f, 0.08f);
                foreach (float s in new[] { -0.7f, 0.7f }) k.Box(M("MetalDark"), t + new Vector3(0f, 0.45f, s), new Vector3(0.42f, 0.06f, 0.42f));
                k.Solid(t + Vector3.up * 0.4f, new Vector3(0.9f, 0.8f, 0.9f));
            }

            // bus shelter on Market St (north sidewalk)
            k.BoxMM(M("MetalDark"), -33f, -27f, 2.6f, 2.72f, -20.8f, -18.6f, false);
            k.BoxMM(M("GlassClear"), -33f, -27f, GY, 2.6f, -18.75f, -18.6f);
            k.BoxMM(M("GlassClear"), -33f, -32.85f, GY, 2.6f, -20.6f, -18.75f);
            CityProps.Bench(k, new Vector3(-30f, GY, -19.2f), Vector3.back);
            k.Board("BUS 42", new Vector3(-27.2f, 3.05f, -19.6f), 0f, 1.4f, 0.35f, Color.white, M("NeonCyan"));

            // service lane z 10..16 (east)
            CityProps.Dumpster(k, new Vector3(46.6f, GY, 11.2f), true);
            CityProps.Crate(k, new Vector3(46.8f, GY, 15f), new Vector3(1.2f, 1.6f, 0.8f), "MetalDark");
            CityProps.Crate(k, new Vector3(40f, GY, 15.2f), new Vector3(1f, 0.9f, 0.9f));
            CityProps.Puddle(k, new Vector3(36f, GY, 12.5f), 2f, 1.4f, 22f);
            k.Pipe(M("Pipe"), new Vector3(13f, 3.4f, 15.85f), new Vector3(47.8f, 3.4f, 15.85f), 0.1f);
            k.Box(M("LampHead"), new Vector3(47.9f, 3.3f, 13f), new Vector3(0.12f, 0.12f, 0.5f));
            k.Text("SERVICE ACCESS", new Vector3(47.95f, 2.6f, 13f), -90f, 0.25f, new Color(1f, 0.8f, 0.3f), null, false);
        }

        // ------------------------------------------------------------------ north block (campus surroundings)
        static void NorthBlock(WorldKit k)
        {
            CityProps.Building(k, Facade("BrickRed"), -100f, -50f, 57f, 82f, 16f, Faces.S | Faces.E, new Shop(Faces.S, 30f, 12f, "MIDNIGHT BAR", "AwningBlack", "NeonPink"));
            CityProps.Building(k, Facade("LoftBrown"), -100f, -50f, 82f, 107.75f, 22f, Faces.E);
            CityProps.Building(k, Facade("BrickCream"), 62f, 100f, 57f, 107.75f, 20f, Faces.S | Faces.W, new Shop(Faces.S, 14f, 12f, "NIGHT OWL DELI", "AwningGreen", "NeonGreen"));
            k.BoxMM(M("Concrete"), -100f, 100f, 0f, 4.2f, 107.75f, 108.25f);

            var con = M("Concrete");
            k.WallX(con, -24f, 50.25f, 57.25f, 57.75f, 0f, 3.55f, new Vector3(-1.2f, 1.2f, GY + 3f));
            k.BoxMM(M("MetalDark"), -24.05f, 50.3f, 3.55f, 3.65f, 57.2f, 57.8f, false);
            for (float x = -21f; x < 49f; x += 6f)
                if (Mathf.Abs(x) > 3f) k.Box(M("ValeAccent"), new Vector3(x, 1.9f, 57.22f), new Vector3(0.3f, 3f, 0.06f));
            k.WallZ(con, 57.25f, 72f, -24f, -23.5f, 0f, 3.55f);
            k.WallZ(con, 104f, 107.75f, -24f, -23.5f, 0f, 4.2f);
            k.WallZ(con, 57.25f, 88.3f, 49.75f, 50.25f, 0f, 4.2f);
            k.WallZ(con, 91.7f, 107.75f, 49.75f, 50.25f, 0f, 4.2f);

            // main gate frame + booth (outside)
            k.BoxMM(M("MetalDark"), -1.7f, -1.2f, 0f, 4.5f, 57.1f, 57.9f);
            k.BoxMM(M("MetalDark"), 1.2f, 1.7f, 0f, 4.5f, 57.1f, 57.9f);
            k.Board("MAIN GATE — SECURITY", new Vector3(0f, 4.05f, 57.04f), 0f, 4.8f, 0.6f, Color.white, M("NeonRed"));
            k.Box(M("WarningLight"), new Vector3(-1.45f, 4.6f, 57.5f), new Vector3(0.2f, 0.2f, 0.2f));
            k.Box(M("WarningLight"), new Vector3(1.45f, 4.6f, 57.5f), new Vector3(0.2f, 0.2f, 0.2f));
            for (float x = -1.1f; x <= 1.1f; x += 0.55f) k.Box(M("PaintYellow"), new Vector3(x, GY + 0.004f, 56.6f), new Vector3(0.28f, 0.008f, 1.2f));
            k.BoxMM(M("PlasterGrey"), 3f, 5.5f, 0f, 2.95f, 53.6f, 56.6f);
            k.BoxMM(M("MetalDark"), 2.7f, 5.8f, 2.95f, 3.15f, 53.3f, 56.9f, false);
            k.Box(M("WindowCool"), new Vector3(2.97f, 1.8f, 55.1f), new Vector3(0.06f, 1.1f, 2.6f));
            k.Box(M("WindowCool"), new Vector3(4.25f, 1.8f, 53.57f), new Vector3(2.2f, 1.1f, 0.06f));
            k.Box(M("WindowCool"), new Vector3(5.53f, 1.8f, 55.1f), new Vector3(0.06f, 1.1f, 2.6f));
            k.Text("SECURITY", new Vector3(4.25f, 3.05f, 53.27f), 0f, 0.16f, Color.white);
            // monolith sign outside the fence
            k.BoxMM(M("SignBoard"), -11f, -5f, 0f, 2.3f, 55.9f, 56.5f);
            k.Box(M("NeonGreen"), new Vector3(-8f, 0.35f, 55.87f), new Vector3(5.6f, 0.06f, 0.04f));
            k.Sign("VALE BIOTECH", new Vector3(-8f, 1.55f, 55.86f), 0f, 0.7f, 5.8f);
            k.Text("ADVANCED LIFE SCIENCES", new Vector3(-8f, 0.85f, 55.86f), 0f, 0.2f, new Color(0.8f, 0.9f, 0.85f), null, false);

            // forecourt (inside the gate)
            foreach (float s in new[] { -1f, 1f })
            {
                k.BoxMM(con, s * 6f, s * 14f, 0f, 0.6f, 62f, 64f);
                k.BoxMM(M("Grass"), s * 6.2f, s * 13.8f, 0.6f, 0.62f, 62.2f, 63.8f, false);
                CityProps.Tree(k, new Vector3(s * 10f, 0.6f, 63f), 0.8f);
                k.Cylinder(M("Chrome"), new Vector3(s * 18f, GY, 61f), 0.07f, 9f);
                k.Box(M("ValeAccent"), new Vector3(s * 18f + 0.75f, 8.3f, 61f), new Vector3(1.5f, 0.9f, 0.03f));
                k.Solid(new Vector3(s * 18f, 1.5f, 61f), new Vector3(0.3f, 3f, 0.3f));
                k.Cylinder(M("MetalDark"), new Vector3(s * 3.2f, GY, 66f), 0.12f, 0.9f);
                k.Box(M("LabPanel"), new Vector3(s * 3.2f, GY + 0.92f, 66f), new Vector3(0.18f, 0.06f, 0.18f));
            }
            CityProps.SecurityCamera(k, new Vector3(-1.45f, 4.55f, 57.1f), 180f);
            CityProps.SecurityCamera(k, new Vector3(-23.9f, 3.7f, 57.5f), 225f);
            CityProps.SecurityCamera(k, new Vector3(-24.2f, 3.4f, 78.3f), -90f);
            // service yard (inside perimeter, east)
            CityProps.Crate(k, new Vector3(32f, GY, 64f), new Vector3(4f, 2.2f, 3f), "MetalDark");
            k.Cylinder(M("SteelPlate"), new Vector3(30f, GY, 76f), 1.3f, 4.5f);
            k.Cylinder(M("SteelPlate"), new Vector3(34f, GY, 76f), 1.3f, 4.5f);
            k.Solid(new Vector3(32f, 2.3f, 76f), new Vector3(6.8f, 4.6f, 2.8f));
            CityProps.Crate(k, new Vector3(40f, GY, 100f), new Vector3(5f, 2.6f, 4f), "SteelPlate");
            k.Text("VALE BIOTECH — AUTHORISED VEHICLES ONLY", new Vector3(36f, 2.2f, 57.2f), 0f, 0.3f, new Color(1f, 0.85f, 0.4f), null, false);

            // staff lane (public): parking, smoking spot, terminal kiosk
            foreach (var (z, paint) in new[] { (66f, "CarSilver"), (93f, "CarRed"), (99f, "CarWhite") })
                CityProps.Car(k, new Vector3(-46.4f, GY, z), false, paint);
            for (float z = 61f; z < 106f; z += 3f) k.Box(M("PaintWhite"), new Vector3(-46.5f, GY + 0.004f, z), new Vector3(5.5f, 0.008f, 0.12f));
            CityProps.Bench(k, new Vector3(-33.5f, GY, 87.2f), Vector3.back);
            Bin(k, new Vector3(-31.6f, GY, 87.3f));
            CityProps.Lamp(k, new Vector3(-27f, GY, 70f), Vector3.left);
            CityProps.Lamp(k, new Vector3(-27f, GY, 98f), Vector3.left);
            CityProps.Lamp(k, new Vector3(-48.8f, GY, 82f), Vector3.right);
            k.Text("NO SMOKING WITHIN 5 M OF ENTRANCE", new Vector3(-24.04f, 1.6f, 85.5f), 90f, 0.14f, new Color(0.9f, 0.9f, 0.9f), null, false);
            Signpost(k, new Vector3(-37f, GY, 57.4f), "STAFF PARKING", 0f, "NeonCyan");

            // staff entrance canopy + sign + terminal kiosk
            var door = L.StaffDoor;
            k.BoxMM(M("ValeAccent"), -24.12f, -23.98f, GY, GY + 2.82f, door.z - 1.22f, door.z - 1f, false);
            k.BoxMM(M("ValeAccent"), -24.12f, -23.98f, GY, GY + 2.82f, door.z + 1f, door.z + 1.22f, false);
            k.BoxMM(M("ValeAccent"), -24.12f, -23.98f, GY + 2.6f, GY + 2.82f, door.z - 1.22f, door.z + 1.22f, false);
            k.BoxMM(M("MetalDark"), -25.7f, -24f, 3.2f, 3.32f, door.z - 1.8f, door.z + 1.8f, false);
            k.Board("STAFF ENTRANCE", new Vector3(-24.08f, 3.75f, door.z), 90f, 3.6f, 0.55f, Color.white, M("NeonCyan"));
            var t = L.StaffTerminal;
            k.Box(M("MetalDark"), t + new Vector3(0f, 0.52f, 0f), new Vector3(0.45f, 1.04f, 0.4f));
            k.Box(M("SteelPlate"), t + new Vector3(0f, 1.3f, 0f), new Vector3(0.18f, 0.55f, 0.6f));
            k.Box(M("ValeAccent"), t + new Vector3(0f, 1.62f, 0f), new Vector3(0.2f, 0.08f, 0.62f));
            k.Solid(t + Vector3.up * 0.8f, new Vector3(0.6f, 1.6f, 0.7f));
            k.Text("STAFF LOGIN", t + new Vector3(-0.1f, 1.78f, 0f), 90f, 0.12f, new Color(0.6f, 1f, 0.8f), null, false);

            // east service alley (public) + tunnel door house
            k.BoxMM(con, 50.25f, 54f, 0f, 4.15f, 86f, 86.3f);
            k.BoxMM(con, 50.25f, 54f, 0f, 4.15f, 93.7f, 94f);
            k.WallZ(con, 86f, 94f, 53.7f, 54f, 0f, 4.15f, new Vector3(89f, 91f, GY + 2.6f));
            k.BoxMM(con, 50.1f, 54.2f, 4.15f, 4.4f, 85.9f, 94.1f);
            var td = L.TunnelDoor;
            for (float y = 0.3f; y < 2.8f; y += 0.5f)
                foreach (float s in new[] { -1f, 1f })
                    k.Box(M(((int)(y * 2f)) % 2 == 0 ? "PaintYellow" : "SignBoard"), new Vector3(54.03f, GY + y, td.z + s * 1.12f), new Vector3(0.05f, 0.25f, 0.22f));
            k.Board("SERVICE TUNNEL", new Vector3(54.08f, 3.6f, td.z), -90f, 4.2f, 0.6f, Color.white, M("WarningLight"));
            k.Text("MAINTENANCE ONLY", new Vector3(54.05f, 3.02f, td.z), -90f, 0.26f, new Color(1f, 0.8f, 0.25f));
            k.Box(M("WarningLight"), new Vector3(54.1f, 2.9f, td.z - 1.5f), new Vector3(0.15f, 0.2f, 0.15f));
            CityProps.Dumpster(k, new Vector3(60.8f, GY, 100f), false);
            CityProps.Crate(k, new Vector3(60.6f, GY, 70f), new Vector3(1.2f, 1f, 1.2f));
            CityProps.Crate(k, new Vector3(60.7f, GY, 71.4f), new Vector3(1f, 0.8f, 1f));
            k.Pipe(M("Pipe"), new Vector3(61.85f, 3.2f, 58f), new Vector3(61.85f, 3.2f, 107f), 0.12f);
            k.Pipe(M("PipeYellow"), new Vector3(61.85f, 3.6f, 58f), new Vector3(61.85f, 3.6f, 107f), 0.08f);
            CityProps.Puddle(k, new Vector3(57.5f, GY, 84f), 2.2f, 1.6f, -12f);
            CityProps.Lamp(k, new Vector3(61.4f, GY, 76f), Vector3.left);
            CityProps.Hoarding(k, new Vector3(54f, 0f, 104.5f), new Vector3(62f, 0f, 104.5f), 3f);
            foreach (var (p, yaw) in new[] { (new Vector3(61.95f, 2.8f, 66f), -90f), (new Vector3(61.95f, 2.6f, 97f), -90f), (new Vector3(61.95f, 7.4f, 74f), -90f) })
                CityProps.AirCon(k, p, yaw);
            CityProps.SecurityLight(k, new Vector3(54.1f, 3.3f, 92.6f), 90f);
            CityProps.SecurityCamera(k, new Vector3(54.15f, 4.0f, 87.2f), 90f);
            CityProps.Graffiti(k, new Vector3(62f, 1.5f, 70f), Vector3.left, 2.2f);
            CityProps.Graffiti(k, new Vector3(50.25f, 1.6f, 66f), Vector3.right, 2f);
            CityProps.Graffiti(k, new Vector3(62f, 1.3f, 102f), Vector3.left, 1.8f);
            CityProps.TrashPile(k, new Vector3(61f, GY, 98.2f), 3);
            CityProps.Barrel(k, new Vector3(61.2f, GY, 68.6f));
            CityProps.Puddle(k, new Vector3(58.5f, GY, 95f), 1.8f, 2.6f, 10f);
            CityProps.Puddle(k, new Vector3(56.5f, GY, 72f), 1.4f, 2f, 60f);
            CityProps.FireEscape(k, new Vector3(62f, 0f, 107.75f), Vector3.back, Vector3.left, 28f, 3.4f, 4, 3.6f, 4.35f);
        }

        // ------------------------------------------------------------------ Vale Biotech lab
        static void Lab(WorldKit k)
        {
            var white = M("LabWhite"); var glass = M("GlassClear"); var md = M("MetalDark");
            k.BoxMM(M("LabFloor"), -23.5f, 23.5f, GY - 0.05f, LY, 72.5f, 103.5f);
            k.BoxMM(M("LabFloor"), -2f, 2f, GY - 0.05f, LY, 72f, 72.5f, false);
            k.BoxMM(white, -23.5f, 23.5f, 4.35f, 4.55f, 72.5f, 103.5f);
            k.BoxMM(M("Concrete"), -24.4f, 24.4f, 9.15f, 9.5f, 71.6f, 104.4f);

            // shell
            k.WallX(white, -24f, 24f, 72f, 72.5f, 0f, 9.15f, new Vector3(-8f, 8f, 4.35f));
            k.BoxMM(glass, -8f, -2f, GY, 4.35f, 72.17f, 72.33f);
            k.BoxMM(glass, 2f, 8f, GY, 4.35f, 72.17f, 72.33f);
            k.BoxMM(glass, -2f, 2f, 3.35f, 4.35f, 72.17f, 72.33f);
            foreach (float x in new[] { -8f, -6f, -4f, -2f, 2f, 4f, 6f, 8f }) k.Box(md, new Vector3(x, 2.25f, 72.25f), new Vector3(0.1f, 4.2f, 0.22f));
            k.Box(md, new Vector3(0f, 3.35f, 72.25f), new Vector3(4f, 0.1f, 0.22f));
            k.Box(md, new Vector3(0f, 4.3f, 72.25f), new Vector3(16.2f, 0.1f, 0.24f));
            k.WallZ(white, 72f, 104f, -24f, -23.5f, 0f, 9.15f, new Vector3(79f, 81f, GY + 2.6f));
            k.WallZ(white, 72f, 104f, 23.5f, 24f, 0f, 9.15f, new Vector3(88.6f, 91.4f, GY + 2.8f));
            k.WallX(white, -24f, 24f, 103.5f, 104f, 0f, 9.15f);

            // exterior detailing: accent band, window bands, sign, canopy
            Band(k, M("ValeAccent"), -24f, 24f, 72f, 104f, 4.55f, 4.95f, 0.08f);
            Band(k, M("Curb"), -24f, 24f, 72f, 104f, 8.75f, 9.15f, 0.12f);
            // upper storey: photo office facade (one floor per face), bay-aligned
            var labFacade = Facade("StoneGrey");
            foreach (Faces f in new[] { Faces.N, Faces.S, Faces.E, Faces.W })
            {
                var face = CityProps.GetFace(f, -24f, 24f, 72f, 104f);
                int bays = Mathf.RoundToInt(face.length / 3.2f);
                float uPer = (1f / 6f) / (face.length / bays);
                k.WallQuad(labFacade.Mat, face.origin, face.dir, face.normal, 0f, face.length, 4.95f, 8.75f, 0.5f / 6f, uPer, 0.5f / 6f, (1f / 6f) / 3.8f, 0.01f);
            }
            k.Box(M("SignBoard"), new Vector3(0f, 8.1f, 71.92f), new Vector3(11.4f, 1.5f, 0.1f));
            k.Sign("VALE BIOTECH", new Vector3(0f, 8.1f, 71.86f), 0f, 1.4f, 11.2f);
            k.Box(M("NeonGreen"), new Vector3(0f, 7.5f, 71.88f), new Vector3(9.5f, 0.06f, 0.05f));
            k.BoxMM(md, -5f, 5f, 3.95f, 4.15f, 68.4f, 72f, false);
            k.Box(M("LabPanel"), new Vector3(0f, 3.94f, 70.2f), new Vector3(8f, 0.02f, 2.6f));
            foreach (float s in new[] { -1f, 1f })
            {
                k.Cylinder(M("Chrome"), new Vector3(s * 4.6f, GY, 68.8f), 0.12f, 3.8f);
                k.Solid(new Vector3(s * 4.6f, 2f, 68.8f), new Vector3(0.3f, 4f, 0.3f));
            }
            k.Text("RESEARCH CENTRE — VISITORS REPORT TO SECURITY", new Vector3(0f, 4.05f, 68.36f), 0f, 0.13f, Color.white, null, false);

            // interior walls
            k.WallX(white, -23.5f, 23.5f, 83.85f, 84.15f, GY, 4.35f, new Vector3(-18f, -15.5f, GY + 2.6f), new Vector3(-4f, 4f, 3.4f), new Vector3(15.5f, 18f, GY + 2.6f));
            k.WallX(white, -23.5f, 23.5f, 87.85f, 88.15f, GY, 4.35f, new Vector3(-16f, -13f, GY + 2.6f), new Vector3(-1.2f, 1.2f, GY + 2.6f), new Vector3(9.5f, 12f, GY + 2.6f), new Vector3(18f, 20.5f, GY + 2.6f));
            k.WallZ(white, 72.5f, 83.85f, -10.15f, -9.85f, GY, 4.35f);
            k.WallZ(white, 88.15f, 103.5f, -6.15f, -5.85f, GY, 4.35f);
            k.WallZ(white, 88.15f, 103.5f, 5.85f, 6.15f, GY, 4.35f);
            GlassPartition(k, 10f, 72.5f, 83.85f);
            GlassPartition(k, 16f, 88.15f, 103.5f);
            k.Box(M("ValeAccent"), new Vector3(0f, LY + 0.004f, 86f), new Vector3(47f, 0.008f, 0.25f));
            k.Box(M("ValeAccent"), new Vector3(0f, LY + 0.004f, 87f), new Vector3(2.4f, 0.008f, 1.7f));

            // ceiling panels
            foreach (float x in new[] { -21f, -17.5f, -13.5f, -8f, -3f, 3f, 8f, 12f, 19.5f })
            foreach (float z in new[] { 75.5f, 79.5f, 82.5f, 86f, 91f, 95f, 99f, 102.5f })
                k.Box(M("LabPanel"), new Vector3(x, 4.33f, z), new Vector3(1.2f, 0.04f, 0.6f));

            // lobby
            k.BoxMM(M("Wood"), -7.5f, -3.5f, LY, LY + 1.1f, 78f, 79f);
            k.BoxMM(M("LabCounter"), -7.6f, -3.4f, LY + 1.1f, LY + 1.16f, 77.9f, 79.1f, false);
            k.Box(M("ValeAccent"), new Vector3(-5.5f, LY + 0.6f, 77.97f), new Vector3(4f, 0.25f, 0.04f));
            CityProps.Monitor(k, new Vector3(-5.5f, LY + 1.16f, 78.7f), Vector3.forward);
            k.Text("RECEPTION", new Vector3(-5.5f, LY + 0.9f, 77.94f), 0f, 0.2f, Color.white, null, false);
            k.Text("VALE BIOTECH", new Vector3(0f, 3.88f, 83.8f), 0f, 0.45f, new Color(0.45f, 1f, 0.6f));
            k.Box(M("NeonGreen"), new Vector3(0f, 3.55f, 83.82f), new Vector3(4.5f, 0.04f, 0.04f));
            CityProps.Bench(k, new Vector3(8.6f, LY, 76f), Vector3.left);
            CityProps.Bench(k, new Vector3(8.6f, LY, 79.5f), Vector3.left);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Cylinder(M("Concrete"), new Vector3(s * 8.8f, LY, 73.4f), 0.4f, 0.7f);
                k.Sphere(M("Foliage"), new Vector3(s * 8.8f, LY + 1.2f, 73.4f), new Vector3(1f, 1.1f, 1f));
                k.Solid(new Vector3(s * 8.8f, LY + 0.5f, 73.4f), new Vector3(0.85f, 1f, 0.85f));
            }

            // staff room (west front)
            k.BoxMM(M("Locker"), -22.8f, -11.6f, LY, LY + 2.1f, 72.5f, 73.1f);
            for (float x = -22.2f; x < -11.6f; x += 0.6f) k.Box(md, new Vector3(x, LY + 1.05f, 73.11f), new Vector3(0.02f, 2.1f, 0.02f));
            CityProps.Bench(k, new Vector3(-17f, LY, 76.5f), Vector3.back);
            k.BoxMM(M("LabCounter"), -12.6f, -10.15f, LY, LY + 0.92f, 79f, 82.5f);
            k.Box(M("MetalDark"), new Vector3(-10.6f, LY + 1.2f, 81.8f), new Vector3(0.45f, 0.55f, 0.4f));
            k.Text("STAFF ROOM", new Vector3(-16.75f, 3.1f, 84.18f), 180f, 0.22f, new Color(0.2f, 0.25f, 0.3f), null, false);

            // security office (east front)
            k.BoxMM(M("LabCounter"), 13.5f, 21.5f, LY, LY + 0.8f, 73f, 74f);
            foreach (float x in new[] { 14.5f, 16.5f, 18.5f, 20.5f }) CityProps.Monitor(k, new Vector3(x, LY + 0.8f, 73.4f), Vector3.forward);
            k.Box(M("Screen"), new Vector3(17.5f, 2.6f, 72.53f), new Vector3(5f, 1.5f, 0.03f));
            k.Text("SECURITY", new Vector3(16.75f, 3.1f, 84.18f), 180f, 0.22f, new Color(0.2f, 0.25f, 0.3f), null, false);

            // corridor door signs (read from the corridor, facing south)
            var signC = new Color(0.15f, 0.35f, 0.25f);
            k.Text("LAB A — GENETICS", new Vector3(-14.5f, 3.2f, 87.82f), 0f, 0.24f, signC);
            k.Text("DR. E. VALE — DIRECTOR", new Vector3(0f, 3.2f, 87.82f), 0f, 0.24f, signC);
            k.Text("LAB B — CULTURES", new Vector3(10.75f, 3.2f, 87.82f), 0f, 0.24f, signC);
            k.Text("SERVICE ROOM", new Vector3(19.25f, 3.2f, 87.82f), 0f, 0.24f, signC);

            // lab A (west back)
            CityProps.LabBench(k, -20f, -9f, 93f, 94.2f, LY, Vector3.back, 4);
            CityProps.LabBench(k, -20f, -9f, 98f, 99.2f, LY, Vector3.forward, 4);
            foreach (float z in new[] { 92f, 96f, 100f }) CityProps.Tank(k, new Vector3(-22.4f, LY, z), 0.6f, 2.3f);
            CityProps.Tank(k, new Vector3(-8f, LY, 101.8f), 0.7f, 2.5f);
            k.BoxMM(M("SteelPlate"), -20f, -14f, LY, LY + 2.2f, 102.6f, 103.5f);
            k.Box(M("GlassTank"), new Vector3(-17f, LY + 1.5f, 102.55f), new Vector3(5.6f, 1f, 0.05f));

            // Dr. Vale's office (centre back)
            k.Box(M("Rug"), new Vector3(0f, LY + 0.003f, 98f), new Vector3(5f, 0.006f, 4f));
            k.BoxMM(M("Wood"), -2f, 2f, LY, LY + 0.78f, 101.6f, 102.6f);
            CityProps.Monitor(k, new Vector3(-0.8f, LY + 0.78f, 102.2f), Vector3.back);
            CityProps.Monitor(k, new Vector3(0.8f, LY + 0.78f, 102.2f), Vector3.back);
            k.Box(M("Leather"), new Vector3(0f, LY + 0.6f, 102.9f), new Vector3(0.7f, 1.2f, 0.35f));
            k.BoxMM(M("Wood"), -5.5f, 5.5f, LY, LY + 2.6f, 103.1f, 103.5f);
            for (int i = 0; i < 26; i++)
                k.Box(M(k.Pick("AwningRed", "AwningNavy", "AwningGreen", "Leather", "BarrierWhite")),
                    new Vector3(-5.2f + i * 0.41f, LY + 0.5f + (i % 3) * 0.75f, 103.02f), new Vector3(0.3f, 0.5f, 0.18f));
            CityProps.Tank(k, new Vector3(4.2f, LY, 92.6f), 0.75f, 2.6f);
            k.BoxMM(M("Leather"), -5.5f, -4.5f, LY, LY + 0.8f, 91f, 95f);
            k.Board("DR. E. VALE", new Vector3(0f, LY + 0.5f, 101.55f), 0f, 1.6f, 0.3f, Color.white, M("NeonGreen"));

            // lab B (east back)
            CityProps.LabBench(k, 8f, 14f, 94.5f, 95.7f, LY, Vector3.back, 3);
            foreach (float x in new[] { 8f, 11f, 14f }) CityProps.Tank(k, new Vector3(x, LY, 101.5f), 0.6f, 2.4f);
            k.Box(M("Screen"), new Vector3(6.18f, 2.2f, 97f), new Vector3(0.03f, 1.2f, 3f));

            // service room (east back)
            foreach (float x in new[] { 18.6f, 21.6f })
            {
                k.Cylinder(M("SteelPlate"), new Vector3(x, LY, 100.8f), 1f, 3.2f);
                k.Solid(new Vector3(x, 1.8f, 100.8f), new Vector3(2.1f, 3.4f, 2.1f));
                k.Pipe(M("PipeRed"), new Vector3(x, 3.4f, 100.8f), new Vector3(x, 4.3f, 100.8f), 0.12f);
            }
            k.Pipe(M("Pipe"), new Vector3(22.8f, 3.9f, 88.6f), new Vector3(22.8f, 3.9f, 103.3f), 0.14f);
            k.Pipe(M("PipeYellow"), new Vector3(23.15f, 3.55f, 88.6f), new Vector3(23.15f, 3.55f, 103.3f), 0.09f);
            k.BoxMM(md, 23f, 23.5f, LY, LY + 2f, 95f, 98.5f);
            k.Box(M("WarningLight"), new Vector3(22.97f, LY + 1.7f, 96f), new Vector3(0.04f, 0.08f, 0.08f));
            CityProps.Crate(k, new Vector3(17.2f, LY, 96.5f), new Vector3(1.2f, 1f, 1.2f));
            k.Text("SERVICE TUNNEL B-2", new Vector3(23.46f, 3.3f, 90f), 90f, 0.24f, new Color(0.9f, 0.7f, 0.2f));
        }

        static void GlassPartition(WorldKit k, float x, float z0, float z1)
        {
            k.BoxMM(M("GlassClear"), x - 0.08f, x + 0.08f, GY, 4.35f, z0, z1);
            k.BoxMM(M("MetalDark"), x - 0.1f, x + 0.1f, GY, LY + 0.1f, z0, z1, false);
            k.BoxMM(M("MetalDark"), x - 0.1f, x + 0.1f, 4.25f, 4.35f, z0, z1, false);
            for (float z = z0 + 2f; z < z1 - 0.5f; z += 2.5f) k.Box(M("MetalDark"), new Vector3(x, 2.25f, z), new Vector3(0.12f, 4.1f, 0.06f));
        }

        static void Band(WorldKit k, Material m, float x0, float x1, float z0, float z1, float y0, float y1, float o)
        {
            k.BoxMM(m, x0 - o, x1 + o, y0, y1, z0 - o, z0, false);
            k.BoxMM(m, x0 - o, x1 + o, y0, y1, z1, z1 + o, false);
            k.BoxMM(m, x0 - o, x0, y0, y1, z0, z1, false);
            k.BoxMM(m, x1, x1 + o, y0, y1, z0, z1, false);
        }

        // ------------------------------------------------------------------ service tunnel (enclosed corridor)
        static void Tunnel(WorldKit k)
        {
            var c = M("TunnelConcrete");
            k.BoxMM(c, 24f, 53.7f, GY - 0.02f, GY + 0.01f, 88.6f, 91.4f, false);
            k.BoxMM(c, 24f, 53.7f, 0f, 3.15f, 88.3f, 88.6f);
            k.BoxMM(c, 24f, 53.7f, 0f, 3.15f, 91.4f, 91.7f);
            k.BoxMM(c, 23.9f, 53.8f, 3.15f, 3.45f, 88.2f, 91.8f);
            k.Pipe(M("Pipe"), new Vector3(24f, 2.45f, 88.82f), new Vector3(53.6f, 2.45f, 88.82f), 0.13f);
            k.Pipe(M("Pipe"), new Vector3(24f, 2.82f, 88.85f), new Vector3(53.6f, 2.82f, 88.85f), 0.1f);
            k.Pipe(M("PipeYellow"), new Vector3(24f, 2.7f, 91.17f), new Vector3(53.6f, 2.7f, 91.17f), 0.11f);
            k.Pipe(M("PipeRed"), new Vector3(24f, 0.45f, 91.25f), new Vector3(53.6f, 0.45f, 91.25f), 0.08f);
            for (float x = 26f; x < 53f; x += 4f)
            {
                k.Box(M("MetalDark"), new Vector3(x, 2.65f, 88.7f), new Vector3(0.08f, 0.7f, 0.2f));
                k.Box(M("MetalDark"), new Vector3(x, 2.7f, 91.3f), new Vector3(0.08f, 0.4f, 0.2f));
            }
            for (float x = 26.5f; x < 53f; x += 5f) k.Box(M("TunnelStrip"), new Vector3(x, 3.13f, 90f), new Vector3(1.2f, 0.04f, 0.14f));
            k.Box(M("PaintYellow"), new Vector3(38.85f, GY + 0.014f, 88.68f), new Vector3(29.6f, 0.006f, 0.1f));
            k.Box(M("PaintYellow"), new Vector3(38.85f, GY + 0.014f, 91.32f), new Vector3(29.6f, 0.006f, 0.1f));
            CityProps.Puddle(k, new Vector3(33f, GY + 0.01f, 90.3f), 2.4f, 1.2f, 8f);
            k.Box(M("MetalDark"), new Vector3(44f, GY + 0.014f, 90f), new Vector3(0.9f, 0.006f, 0.9f));
            k.Text("VALE BIOTECH — SERVICE TUNNEL B-2 — LAB →", new Vector3(38f, 1.7f, 88.62f), 180f, 0.22f, new Color(0.9f, 0.75f, 0.25f), null, false);
            k.Box(M("WarningLight"), new Vector3(52f, 2.9f, 88.65f), new Vector3(0.15f, 0.15f, 0.08f));
        }

        // ------------------------------------------------------------------ boundaries
        static void Boundaries(WorldKit k)
        {
            const int ignoreRaycast = 2;
            k.SolidMM(-91f, -89.5f, -2f, 30f, -100f, 120f, ignoreRaycast);
            k.SolidMM(89.5f, 91f, -2f, 30f, -100f, 120f, ignoreRaycast);
            k.SolidMM(-100f, 100f, -2f, 30f, -91f, -89.2f, ignoreRaycast);
            k.SolidMM(-100f, 100f, -2f, 30f, 108.25f, 110f, ignoreRaycast);

            CityProps.Barrier(k, new Vector3(-84f, 0f, -34f), new Vector3(-84f, 0f, -22f));
            RoadClosed(k, new Vector3(-84.05f, 1.75f, -28f), -90f);
            CityProps.Hoarding(k, new Vector3(-88.6f, 0f, -39f), new Vector3(-88.6f, 0f, -17f));
            CityProps.Barrier(k, new Vector3(80f, 0f, -34f), new Vector3(80f, 0f, -22f));
            RoadClosed(k, new Vector3(79.95f, 1.75f, -28f), 90f);
            CityProps.Truck(k, new Vector3(84.5f, 0f, -28f), true, "CarWhite", "CITY WORKS");
            CityProps.Hoarding(k, new Vector3(88.6f, 0f, -39f), new Vector3(88.6f, 0f, -17f));
            CityProps.Barrier(k, new Vector3(-80f, 0f, 40f), new Vector3(-80f, 0f, 52f));
            RoadClosed(k, new Vector3(-80.05f, 1.75f, 46f), -90f);
            CityProps.Truck(k, new Vector3(-84.5f, 0f, 46f), true, "CarRed", "GAS WORKS");
            CityProps.Hoarding(k, new Vector3(-88.6f, 0f, 35f), new Vector3(-88.6f, 0f, 57f));
            CityProps.Barrier(k, new Vector3(84f, 0f, 40f), new Vector3(84f, 0f, 52f));
            RoadClosed(k, new Vector3(83.95f, 1.75f, 46f), 90f);
            CityProps.Hoarding(k, new Vector3(88.6f, 0f, 35f), new Vector3(88.6f, 0f, 57f));
        }

        // ------------------------------------------------------------------ skyline (non-walkable, outside "World")
        static void Skyline(WorldKit k, Rect near, Rect far)
        {
            k.Box(M("Ground"), new Vector3(0f, -0.06f, 0f), new Vector3(1000f, 0.1f, 1000f));
            k.Box(M("Asphalt"), new Vector3(0f, -0.008f, 260f), new Vector3(14f, 0.01f, 280f));
            k.Box(M("Asphalt"), new Vector3(0f, -0.008f, -250f), new Vector3(14f, 0.01f, 300f));
            Ring(k, near, 14f, 30f, 22f, 70f);
            Ring(k, far, 24f, 46f, 45f, 140f);
        }

        // photo facade towers: material, metres per texture tile (square texels)
        static readonly (string mat, float tile)[] TowerStyles =
        {
            ("TowerDense", 112f), ("TowerGrid", 112f), ("TowerBrown", 112f), ("TowerPiers", 48f), ("TowerGlassNight", 61f), ("TowerGlass", 42f),
        };

        static void Ring(WorldKit k, Rect r, float minW, float maxW, float minH, float maxH)
        {
            void Tower(float x, float z, float w, float d)
            {
                if (Mathf.Abs(x) < 9f + w * 0.5f && Mathf.Abs(z) > 100f) x += Mathf.Sign(x == 0 ? 1 : x) * (9f + w * 0.5f);
                float h = k.Rand(minH, maxH);
                var (mat, tile) = TowerStyles[k.Rng.Next(TowerStyles.Length)];
                float y = 0f;
                // podium (lit lobby band) on most towers
                if (k.Chance(0.6f))
                {
                    float ph = k.Rand(6f, 12f);
                    TowerBlock(k, "StoneSand", 6f * 3.2f, x, z, w + 4f, d + 4f, 0f, ph, true);
                    y = ph;
                }
                // shaft, optionally with one or two setbacks
                int tiers = h > 70f ? k.Rng.Next(1, 4) : h > 40f ? k.Rng.Next(1, 3) : 1;
                float tw = w, td = d;
                for (int t = 0; t < tiers; t++)
                {
                    float top = t == tiers - 1 ? h : Mathf.Lerp(y, h, k.Rand(0.55f, 0.75f));
                    TowerBlock(k, mat, tile, x, z, tw, td, y, top, false);
                    y = top; tw *= k.Rand(0.7f, 0.85f); td *= k.Rand(0.7f, 0.85f);
                }
                // crown: mechanical floor, antenna with aviation light
                k.Box(M("RoofMetal"), new Vector3(x, y + 1.6f, z), new Vector3(tw * 0.55f, 3.2f, td * 0.55f));
                if (h > 55f || k.Chance(0.3f))
                {
                    float ah = k.Rand(6f, 16f);
                    k.Cylinder(M("Rail"), new Vector3(x, y + 3.2f, z), 0.25f, ah);
                    k.Box(M("AviationLight"), new Vector3(x, y + 3.4f + ah, z), new Vector3(0.7f, 0.7f, 0.7f));
                }
            }
            for (float x = r.xMin; x < r.xMax; x += k.Rand(minW, maxW) + 2f)
            {
                float w = k.Rand(minW, maxW), d = k.Rand(minW, maxW);
                Tower(x + w * 0.5f, r.yMin - d * 0.5f, w, d);
                Tower(x + w * 0.5f, r.yMax + d * 0.5f, w, k.Rand(minW, maxW));
            }
            for (float z = r.yMin; z < r.yMax; z += k.Rand(minW, maxW) + 2f)
            {
                float w = k.Rand(minW, maxW), d = k.Rand(minW, maxW);
                Tower(r.xMin - w * 0.5f, z + d * 0.5f, w, d);
                Tower(r.xMax + w * 0.5f, z + d * 0.5f, k.Rand(minW, maxW), d);
            }
        }

        /// <summary>One box of a skyline tower with a photo facade quad on every side and a tar roof + parapet.</summary>
        static void TowerBlock(WorldKit k, string mat, float tile, float x, float z, float w, float d, float y0, float y1, bool podium)
        {
            Material m;
            float perM;
            if (podium) { var st = Facade(mat == "StoneSand" ? (k.Chance(0.5f) ? "StoneSand" : "StoneGrey") : mat); m = st.Mat; perM = 1f / (6f * 3.2f); }
            else { m = M(mat); perM = 1f / tile; }
            float x0 = x - w * 0.5f, x1 = x + w * 0.5f, z0 = z - d * 0.5f, z1 = z + d * 0.5f;
            k.Box(M("RoofTar"), new Vector3(x, (y0 + y1) * 0.5f, z), new Vector3(w - 0.1f, y1 - y0, d - 0.1f));
            foreach (Faces f in new[] { Faces.N, Faces.S, Faces.E, Faces.W })
            {
                var face = CityProps.GetFace(f, x0, x1, z0, z1);
                float u0 = (float)k.Rng.NextDouble(), v0 = (float)k.Rng.NextDouble();
                if (podium) { u0 = 0.5f / 6f; v0 = 0.5f / 6f; }
                k.WallQuad(m, face.origin, face.dir, face.normal, 0f, face.length, y0, y1, u0, perM, v0, perM, 0f);
            }
            k.BoxMM(M("RoofMetal"), x0 - 0.2f, x1 + 0.2f, y1, y1 + 0.6f, z0 - 0.2f, z1 + 0.2f, false);
        }

        // ------------------------------------------------------------------ gameplay markers
        static void Gameplay(WorldKit k, Transform root)
        {
            var doors = Child(root, "Doors");
            var npcs = Child(root, "NPCs");
            var routes = Child(root, "PatrolRoutes");
            var zones = Child(root, "Zones");

            var spawn = Child(root, "PlayerSpawn");
            spawn.SetPositionAndRotation(L.PlayerSpawn, Quaternion.identity);
            spawn.gameObject.AddComponent<PlayerSpawn>();

            // A) main gate — panel slides along +x into the fence
            var gate = Door(doors, "Door_MainGate", L.MainGate, Vector3.zero, new Vector3(2.9f, 3f, 0.2f), new Vector3(3.05f, 0f, 0f),
                M("Gate"), new Vector3(0f, 1.2f, -0.45f), "Main Gate", AccessPermission.SecurityGate, EntryRoute.MainGate, false);
            for (float y = 0.4f; y < 3f; y += 0.65f) Deco(gate.panel, M("MetalDark"), new Vector3(0f, y - 1.5f, -0.11f), new Vector3(2.8f, 0.12f, 0.03f));
            Deco(gate.panel, M("WarningLight"), new Vector3(0f, 1.25f, -0.12f), new Vector3(0.5f, 0.12f, 0.02f));

            // B) staff entrance — terminal controlled, panel slides along +z inside the wall
            var staff = Door(doors, "Door_StaffEntrance", L.StaffDoor, Vector3.up * 90f, new Vector3(2.4f, 2.6f, 0.2f), new Vector3(-2.5f, 0f, 0f),
                M("DoorStaff"), new Vector3(0f, 1.2f, -0.45f), "Staff Entrance", AccessPermission.None, EntryRoute.StaffTerminal, true);
            Deco(staff.panel, M("GlassDark"), new Vector3(0f, 0.55f, -0.11f), new Vector3(0.9f, 0.7f, 0.02f));
            Deco(staff.panel, M("ValeAccent"), new Vector3(0f, -0.4f, -0.11f), new Vector3(2.3f, 0.12f, 0.02f));
            Deco(staff.panel, M("Chrome"), new Vector3(0.85f, -0.2f, -0.13f), new Vector3(0.06f, 0.4f, 0.05f));

            var term = Child(root, "StaffTerminal");
            term.SetPositionAndRotation(L.StaffTerminal, Quaternion.Euler(0f, -90f, 0f));
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.name = "Screen";
            screen.transform.SetParent(term, false);
            screen.transform.localPosition = new Vector3(0f, 1.32f, 0.115f);
            screen.transform.localScale = new Vector3(0.46f, 0.34f, 0.02f);
            screen.GetComponent<MeshRenderer>().sharedMaterial = M("Screen");
            var st = term.gameObject.AddComponent<StaffTerminal>();
            st.terminalName = "Staff Terminal";
            st.linkedDoor = staff;
            st.screen = screen.GetComponent<MeshRenderer>();

            // C) service tunnel — panel slides along +z inside the door-house wall
            var tunnel = Door(doors, "Door_ServiceTunnel", L.TunnelDoor, Vector3.up * -90f, new Vector3(2.4f, 2.6f, 0.2f), new Vector3(2.5f, 0f, 0f),
                M("DoorService"), new Vector3(0f, 1.2f, -0.45f), "Service Tunnel", AccessPermission.ServiceTunnel, EntryRoute.ServiceTunnel, false);
            for (float x = -1f; x <= 1.01f; x += 0.5f) Deco(tunnel.panel, M("PaintYellow"), new Vector3(x, -1.05f, -0.11f), new Vector3(0.25f, 0.35f, 0.02f));
            Deco(tunnel.panel, M("Chrome"), new Vector3(-0.85f, -0.2f, -0.13f), new Vector3(0.06f, 0.4f, 0.05f));

            // zones
            Zone<LabInteriorZone>(zones, "LabInterior", new Vector3(0f, 0f, 88f), new Vector3(47f, 4.4f, 31f));
            Zone<RestrictedZone>(zones, "Restricted_Lab", new Vector3(0f, 0f, 88f), new Vector3(48f, 4.6f, 32f)).zoneName = "Vale Biotech Lab";
            Zone<RestrictedZone>(zones, "Restricted_Tunnel", new Vector3(38.85f, 0f, 90f), new Vector3(29.7f, 3.5f, 3.4f)).zoneName = "Service Tunnel";
            Zone<RestrictedZone>(zones, "Restricted_Campus", new Vector3(13.1f, 0f, 82.7f), new Vector3(73.7f, 5f, 50f)).zoneName = "Vale Biotech Campus";
            Checkpoint(zones, "Storm Drain", new Vector3(-33f, 0f, -84f), new Vector3(6f, 4f, 8f), L.PlayerSpawn);
            Checkpoint(zones, "City Plaza", L.Fountain, new Vector3(44f, 4f, 48f), L.PlazaSpot);
            Checkpoint(zones, "Main Gate", new Vector3(0f, 0f, 54.5f), new Vector3(16f, 4f, 5f), new Vector3(-3f, GY, 53.5f));
            Checkpoint(zones, "Staff Entrance", new Vector3(-36f, 0f, 80f), new Vector3(24f, 4f, 30f), new Vector3(-36f, GY, 76f));
            Checkpoint(zones, "Service Alley", new Vector3(58f, 0f, 82f), new Vector3(8f, 4f, 40f), new Vector3(57.5f, GY, 72f));

            // patrol routes
            var rLab = Route(routes, "Route_LabPerimeter", 2f, new Vector3(-30f, GY, 54.8f), new Vector3(30f, GY, 54.8f), new Vector3(57f, GY, 62f), new Vector3(30f, GY, 54.8f));
            var rAve = Route(routes, "Route_Avenue", 1.5f, new Vector3(-9.8f, GY, -14f), new Vector3(-9.8f, GY, 32f), new Vector3(9.8f, GY, 32f), new Vector3(9.8f, GY, -14f));
            var rLobby = Route(routes, "Route_LabLobby", 2.5f, new Vector3(0f, LY, 76.5f), L.LabCorridor, new Vector3(-19f, LY, 86f), new Vector3(19f, LY, 86f));

            int seed = 11;
            Npc(npcs, "Maya Chen", Profession.Civilian, NpcBehaviour.Wander, new Vector3(-30f, GY, 0f), 0f, seed++, wander: 14f);
            Npc(npcs, "Omar Haddad", Profession.Civilian, NpcBehaviour.Wander, new Vector3(-44f, GY, 21f), 120f, seed++, wander: 12f);
            Npc(npcs, "Lena Fischer", Profession.Civilian, NpcBehaviour.Wander, new Vector3(10f, GY, -6f), 200f, seed++, wander: 10f);
            Npc(npcs, "Theo Brooks", Profession.Civilian, NpcBehaviour.Wander, new Vector3(-14f, GY, 54.2f), 90f, seed++, wander: 8f);
            Npc(npcs, "Sgt. Marcus Reed", Profession.Guard, NpcBehaviour.Stationary, L.GuardPost, 180f, seed++);
            Npc(npcs, "Dana Kowalski", Profession.Guard, NpcBehaviour.Patrol, rLab.GetChild(0).position, 90f, seed++, rLab.GetComponent<PatrolRoute>());
            Npc(npcs, "Victor Hale", Profession.Guard, NpcBehaviour.Patrol, rAve.GetChild(0).position, 0f, seed++, rAve.GetComponent<PatrolRoute>());
            Npc(npcs, "Priya Nair", Profession.Guard, NpcBehaviour.Patrol, rLobby.GetChild(0).position, 0f, seed++, rLobby.GetComponent<PatrolRoute>());
            Npc(npcs, "Dr. Ingrid Sato", Profession.Scientist, NpcBehaviour.Stationary, L.ScientistSmokeSpot, 90f, seed++);
            Npc(npcs, "Dr. Felix Moreau", Profession.Scientist, NpcBehaviour.Wander, L.LabA, 180f, seed++, wander: 5f);
            Npc(npcs, "Dr. Hana Petrov", Profession.Scientist, NpcBehaviour.Stationary, L.CafeSpot, -90f, seed++);
            Npc(npcs, "Gus Romero", Profession.Maintenance, NpcBehaviour.Wander, L.MaintenanceAlleySpot, 0f, seed++, wander: 6f);
            Npc(npcs, "Rita Okafor", Profession.Maintenance, NpcBehaviour.Stationary, L.ServiceLane, 90f, seed++);
            Npc(npcs, "Dr. Vale", Profession.Scientist, NpcBehaviour.Stationary, L.DrVale, L.DrValeYaw, seed++, vale: true);
            MissionPlacement.Place(k, root);
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>Door whose local -Z faces the outside. Panel centred in the doorway (bottom on the root), sliding by openOffset.</summary>
        static AccessDoor Door(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 panelSize, Vector3 openOffset, Material mat,
            Vector3 interactLocal, string doorName, AccessPermission req, EntryRoute route, bool terminal)
        {
            var root = Child(parent, name);
            root.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(root, false);
            panel.transform.localPosition = new Vector3(0f, panelSize.y * 0.5f, 0f);
            panel.transform.localScale = panelSize;
            panel.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var ip = Child(root, "InteractPoint");
            ip.localPosition = interactLocal;
            var d = root.gameObject.AddComponent<AccessDoor>();
            d.doorName = doorName;
            d.required = req;
            d.route = route;
            d.terminalControlled = terminal;
            d.panel = panel.transform;
            d.openLocalOffset = openOffset;
            d.openSeconds = 1.1f;
            d.interactPoint = ip;
            return d;
        }

        static void Deco(Transform panel, Material m, Vector3 offset, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Deco";
            go.transform.SetParent(panel, false);
            var s = panel.localScale;
            go.transform.localPosition = new Vector3(offset.x / s.x, offset.y / s.y, offset.z / s.z);
            go.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        static T Zone<T>(Transform parent, string name, Vector3 bottomCenter, Vector3 size) where T : ZoneVolume
        {
            var t = Child(parent, name);
            t.position = bottomCenter;
            var z = t.gameObject.AddComponent<T>();
            z.size = size;
            return z;
        }

        static void Checkpoint(Transform parent, string name, Vector3 bottomCenter, Vector3 size, Vector3 respawn)
        {
            var z = Zone<CheckpointZone>(parent, "Checkpoint_" + name.Replace(' ', '_'), new Vector3(bottomCenter.x, 0f, bottomCenter.z), size);
            z.checkpointName = name;
            var r = Child(z.transform, "Respawn");
            r.position = respawn;
            z.respawnPoint = r;
        }

        static Transform Route(Transform parent, string name, float wait, params Vector3[] points)
        {
            var t = Child(parent, name);
            t.position = points[0];
            var pr = t.gameObject.AddComponent<PatrolRoute>();
            pr.loop = true;
            pr.waitAtPoint = wait;
            for (int i = 0; i < points.Length; i++) Child(t, "WP" + i).position = points[i];
            return t;
        }

        static void Npc(Transform parent, string displayName, Profession prof, NpcBehaviour behaviour, Vector3 pos, float yaw, int seed,
            PatrolRoute route = null, float wander = 14f, bool vale = false)
        {
            var t = Child(parent, "Spawn_" + displayName.Replace(' ', '_').Replace(".", ""));
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var s = t.gameObject.AddComponent<NpcSpawn>();
            s.displayName = displayName;
            s.profession = prof;
            s.behaviour = behaviour;
            s.route = route;
            s.wanderRadius = wander;
            s.isDrVale = vale;
            s.seed = seed;
        }

        // ------------------------------------------------------------------ lighting & camera
        static void Environment(Transform lighting)
        {
            var sun = WorldKit.AddLight(lighting, "Sun", LightType.Directional, Vector3.zero, SunColor, 1.3f, 0f, LightShadows.Soft, SunEuler);
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;
            RenderSettings.skybox = SkyMaterial;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.2f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.9f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0036f;
            RenderSettings.fogColor = new Color(0.56f, 0.55f, 0.58f);

            var vol = new GameObject("PostProcess");
            vol.transform.SetParent(lighting, false);
            var v = vol.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10f;
            v.sharedProfile = Profile;
        }

        static void CityLights(Transform lighting)
        {
            var warm = new Color(1f, 0.76f, 0.48f); var cool = new Color(0.85f, 0.93f, 1f);
            WorldKit.AddLight(lighting, "Light_Alley", LightType.Point, new Vector3(-31f, 3.4f, -76f), warm, 3f, 12f);
            WorldKit.AddLight(lighting, "Light_Plaza", LightType.Point, new Vector3(-36f, 6.5f, 9f), warm, 4f, 28f);
            WorldKit.AddLight(lighting, "Light_Avenue", LightType.Point, new Vector3(6.3f, 5.8f, 15f), warm, 3f, 20f);
            WorldKit.AddLight(lighting, "Light_MainGate", LightType.Spot, new Vector3(0f, 7.5f, 51.5f), cool, 7f, 20f, LightShadows.Soft, new Vector3(62f, 0f, 0f), 85f);
            WorldKit.AddLight(lighting, "Light_StaffEntrance", LightType.Point, new Vector3(-25.2f, 3f, 80f), cool, 2.5f, 9f);
            WorldKit.AddLight(lighting, "Light_ServiceAlley", LightType.Point, new Vector3(55.3f, 3.5f, 90f), new Color(1f, 0.6f, 0.25f), 3f, 11f);
            WorldKit.AddLight(lighting, "Light_Tunnel", LightType.Point, new Vector3(39f, 2.7f, 90f), new Color(0.6f, 1f, 0.8f), 2f, 18f);
            WorldKit.AddLight(lighting, "Light_Lobby", LightType.Point, new Vector3(0f, 3.9f, 79f), cool, 3f, 13f);
            WorldKit.AddLight(lighting, "Light_LabA", LightType.Point, new Vector3(-14f, 3.9f, 94f), cool, 3f, 11f);
            WorldKit.AddLight(lighting, "Light_ValeOffice", LightType.Point, new Vector3(0f, 3.9f, 96f), new Color(1f, 0.92f, 0.82f), 2.5f, 9f);
            WorldKit.AddLight(lighting, "Light_LabB", LightType.Point, new Vector3(13f, 3.9f, 94f), cool, 3f, 11f);
            // a few real street-lamp lights where the camera spends time (rest of the lamps are emissive only); total <= 16
            WorldKit.AddLight(lighting, "Light_Lamp_Market", LightType.Point, new Vector3(-26f, 4.6f, -34.2f), warm, 2.6f, 13f);
            WorldKit.AddLight(lighting, "Light_Lamp_MarketN", LightType.Point, new Vector3(30f, 4.6f, -21.8f), warm, 2.6f, 13f);
            WorldKit.AddLight(lighting, "Light_Lamp_Avenue", LightType.Point, new Vector3(-7.2f, 4.6f, -46f), warm, 2.6f, 13f);
            WorldKit.AddLight(lighting, "Light_Lamp_Campus", LightType.Point, new Vector3(-22f, 4.6f, 39.8f), warm, 2.6f, 13f);
        }

        static void Probe(Transform parent, string name, Vector3 center, Vector3 size, bool box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            rp.size = size;
            rp.boxProjection = box;
            rp.resolution = box ? 128 : 256;
            rp.hdr = true;
            rp.farClipPlane = 250f;
            rp.clearFlags = ReflectionProbeClearFlags.Skybox;
        }

        static Camera MainCamera(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(pos, rot);
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 700f;
            cam.fieldOfView = 55f;
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            return cam;
        }

        static void SaveAndBake(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!ls) { ls = new LightingSettings { name = "CityLighting" }; AssetDatabase.CreateAsset(ls, LightingPath); }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            Lightmapping.lightingSettings = ls;
            bool baked = false;
            try { baked = Lightmapping.Bake(); }
            catch (Exception e) { Debug.LogWarning("[Parasite] Environment bake failed: " + e.Message); }
            if (!baked)
            {
                Debug.LogWarning($"[Parasite] Could not bake environment lighting for {path}; using a trilight ambient fallback.");
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.9f);
                RenderSettings.ambientEquatorColor = new Color(0.55f, 0.48f, 0.45f);
                RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.22f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        // ================================================================== Bootstrap (main menu backdrop)
        static void BuildBootstrap()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var backdrop = new GameObject("Backdrop").transform;
            var lighting = new GameObject("Lighting").transform;
            var k = new WorldKit(backdrop, 4242);

            k.BoxMM(M("Asphalt"), -60f, 60f, -1f, 0f, -40f, 200f, false);
            Slab(k, M("Sidewalk"), -60f, -7f, -40f, 200f, Faces.E);
            Slab(k, M("Sidewalk"), 7f, 60f, -40f, 200f, Faces.W);
            for (float z = -36f; z < 196f; z += 6f) k.Box(M("PaintYellow"), new Vector3(0f, 0.005f, z + 1.5f), new Vector3(0.2f, 0.01f, 3f));
            var walls = new[] { "Brick", "BrickDark", "PlasterCream", "PlasterGrey", "PlasterTeal", "PlasterTerracotta" };
            var names = new[] { "CAFE LUMEN", "NOVA PHARMACY", "KITO RAMEN", "24/7 MART", "PIXEL ARCADE", "HOTEL ARDEN", "FLORA", "CITY BANK" };
            var awnings = new[] { "AwningRed", "AwningGreen", "AwningNavy", "AwningYellow" };
            var neons = new[] { "NeonCyan", "NeonPink", "NeonGreen", "NeonRed" };
            int n = 0;
            foreach (float side in new[] { -1f, 1f })
                for (float z = -30f; z < 150f;)
                {
                    float d = k.Rand(14f, 24f), h = k.Rand(12f, 32f);
                    float x0 = side < 0 ? -40f : 12f, x1 = side < 0 ? -12f : 40f;
                    var face = side < 0 ? Faces.E : Faces.W;
                    CityProps.Building(k, x0, x1, z, z + d, h, M(walls[n % walls.Length]), face | Faces.S,
                        new Shop(face, d * 0.5f, Mathf.Min(10f, d - 3f), names[n % names.Length], awnings[n % awnings.Length], neons[n % neons.Length]));
                    n++;
                    z += d;
                }
            for (float z = -20f; z < 150f; z += 18f)
            {
                CityProps.Lamp(k, new Vector3(-7.8f, GY, z), Vector3.right);
                CityProps.Lamp(k, new Vector3(7.8f, GY, z + 9f), Vector3.left);
                CityProps.Tree(k, new Vector3(-7.9f, GY, z + 6f));
            }
            CityProps.Car(k, new Vector3(5.4f, 0f, 4f), true, "CarRed");
            CityProps.Car(k, new Vector3(-5.4f, 0f, 18f), true, "CarWhite");
            CityProps.Car(k, new Vector3(5.4f, 0f, 40f), true, "CarBlue");

            // Vale Biotech tower at the end of the avenue
            k.BoxMM(M("LabWhite"), -20f, 20f, 0f, 70f, 160f, 182f, false);
            for (float y = 6f; y < 68f; y += 4f)
            for (float x = -18.5f; x < 19f; x += 3f)
                k.Box(M(k.Chance(0.55f) ? "WindowCool" : "GlassDark"), new Vector3(x, y, 159.95f), new Vector3(2.6f, 2.6f, 0.1f));
            Band(k, M("ValeAccent"), -20f, 20f, 160f, 182f, 70f, 71.5f, 0.2f);
            k.Text("VALE BIOTECH", new Vector3(0f, 74.5f, 159.7f), 0f, 6f, new Color(0.45f, 1f, 0.6f));
            k.Box(M("NeonGreen"), new Vector3(0f, 71.8f, 159.75f), new Vector3(36f, 0.4f, 0.1f));
            k.Root = new GameObject("Skyline").transform;
            Skyline(k, new Rect(-80f, -60f, 160f, 280f), new Rect(-200f, -150f, 400f, 450f));
            k.Flush();

            Environment(lighting);
            WorldKit.AddLight(lighting, "Light_Street1", LightType.Point, new Vector3(-6.3f, 5.8f, 16f), new Color(1f, 0.76f, 0.48f), 4f, 22f);
            WorldKit.AddLight(lighting, "Light_Street2", LightType.Point, new Vector3(6.3f, 5.8f, 43f), new Color(1f, 0.76f, 0.48f), 4f, 22f);
            Probe(lighting, "Probe_Street", new Vector3(0f, 5f, 20f), new Vector3(60f, 16f, 80f), false);

            MainCamera(new Vector3(1.2f, 2.1f, -22f), Quaternion.Euler(-4f, -2f, 0f));
            new GameObject("MainMenu").AddComponent<Parasite.UI.MainMenuController>();

            SaveAndBake(scene, BootstrapPath);
        }
    }
}
