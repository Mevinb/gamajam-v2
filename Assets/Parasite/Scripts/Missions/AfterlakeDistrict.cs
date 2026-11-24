using System.Collections.Generic;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Deterministic, checkpoint-safe riverside district, created before CityDirector's NavMesh bake.</summary>
    public static class AfterlakeDistrict
    {
        public const string RootName = "Afterlake Riverside";

        public static void Build()
        {
            if (GameObject.Find(RootName)) return;
            var world = GameObject.Find("World");
            if (!world) world = new GameObject("World");
            var root = new GameObject(RootName).transform; root.SetParent(world.transform, false);
            var materials = new Dictionary<string, Material>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var m in renderer.sharedMaterials)
                    if (m && !materials.ContainsKey(m.name)) materials[m.name] = m;
            Material M(string name, Color fallback)
            {
                if (materials.TryGetValue(name, out var m)) return m;
                return MatFactory.Lit(fallback, 0.38f);
            }
            var paving = M("Sidewalk", new Color(0.32f, 0.34f, 0.33f));
            var brick = M("BrickDark", new Color(0.26f, 0.21f, 0.18f));
            var concrete = M("TunnelConcrete", new Color(0.4f, 0.42f, 0.41f));
            var steel = M("MetalDark", new Color(0.10f, 0.14f, 0.16f));
            var wood = M("Wood", new Color(0.35f, 0.22f, 0.12f));
            var roof = M("RoofMetal", new Color(0.20f, 0.26f, 0.28f));
            var white = M("LabWhite", new Color(0.72f, 0.75f, 0.71f));
            var lamp = MatFactory.Emissive(new Color(0.8f, 0.72f, 0.51f), new Color(1.5f, 1.15f, 0.7f));
            var teal = MatFactory.Emissive(new Color(0.05f, 0.28f, 0.27f), new Color(0.1f, 0.8f, 0.65f));

            GameObject Box(string name, Vector3 pos, Vector3 size, Material material, bool solid = true)
            {
                var go = Prim.Make(PrimitiveType.Cube, root, pos, size, material, solid, name);
                if (!solid) { var collider = go.GetComponent<Collider>(); if (collider) collider.enabled = false; }
                return go;
            }
            GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material material, bool solid = true)
            {
                var go = Prim.Make(PrimitiveType.Cylinder, root, pos, scale, material, solid, name);
                if (!solid) { var collider = go.GetComponent<Collider>(); if (collider) collider.enabled = false; }
                return go;
            }
            void Sign(string text, Vector3 pos, float size = 0.11f)
            {
                var go = new GameObject("Sign · " + text); go.transform.SetParent(root, false);
                go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, 180, 0);
                var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 64; mesh.characterSize = size;
                mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = new Color(0.85f, 0.88f, 0.79f);
                var signFont = Resources.Load<Font>("UI/Rajdhani-Medium");
                mesh.font = signFont ? signFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                go.AddComponent<WorldTextFont>().Configure(M("WorldText", Color.white));
            }
            void Shed(string name, float x, float z, float width, float depth, Material wall)
            {
                // Open fronts and real interiors give possessed hosts a navigable working area.
                Box(name + " rear", new Vector3(x, 1.9f, z + depth * 0.5f), new Vector3(width, 3.5f, 0.3f), wall);
                Box(name + " left", new Vector3(x - width * 0.5f, 1.9f, z), new Vector3(0.3f, 3.5f, depth), wall);
                Box(name + " right", new Vector3(x + width * 0.5f, 1.9f, z), new Vector3(0.3f, 3.5f, depth), wall);
                Box(name + " roof", new Vector3(x, 3.8f, z), new Vector3(width + 0.7f, 0.25f, depth + 0.7f), roof);
                Box(name + " lintel", new Vector3(x, 3.25f, z - depth * 0.5f), new Vector3(width, 0.8f, 0.3f), wall);
                Box(name + " light", new Vector3(x, 2.7f, z - depth * 0.5f - 0.18f), new Vector3(width * 0.65f, 0.12f, 0.12f), lamp, false);
                Sign(name.ToUpperInvariant(), new Vector3(x, 3.25f, z - depth * 0.5f - 0.18f));
            }

            // An actual water berth, rather than water drawn over an invisible floor. Each slab remains physical;
            // the ferry retains its original deck and connects to the north quay with a shallow gangway.
            foreach (var slab in new[]
            {
                (name: "west", x: 222.5f, z: -3f, width: 85f, depth: 70f),
                (name: "east", x: 277.5f, z: -3f, width: 5f, depth: 70f),
                (name: "berth head", x: 270f, z: 4f, width: 10f, depth: 56f),
            })
            {
                Box("Quayside foundation " + slab.name, new Vector3(slab.x, -0.7f, slab.z), new Vector3(slab.width, 1.7f, slab.depth), concrete);
                Box("Quayside paving " + slab.name, new Vector3(slab.x, 0.08f, slab.z), new Vector3(slab.width, 0.14f, slab.depth), paving);
            }
            AfterlakeWater.Create(root);
            // Low retaining walls establish a bounded district without invisible fall edges.
            Box("West retaining wall", new Vector3(180.5f, 0.8f, -3), new Vector3(0.7f, 1.4f, 70), brick);
            Box("East retaining wall", new Vector3(279.5f, 0.8f, -3), new Vector3(0.7f, 1.4f, 70), brick);
            Box("North retaining wall", new Vector3(230, 1.5f, 31.5f), new Vector3(100, 2.7f, 0.8f), brick);
            Box("Sea wall west", new Vector3(222.5f, 0.65f, -37.5f), new Vector3(85, 1.0f, 0.6f), concrete);
            Box("Sea wall east", new Vector3(277.5f, 0.65f, -37.5f), new Vector3(5, 1.0f, 0.6f), concrete);
            for (int i = 0; i < 16; i++)
            {
                float x = 184 + i * 6;
                if (x > 265 && x < 275) continue;
                Cylinder("Mooring bollard", new Vector3(x, 0.6f, -34), new Vector3(0.45f, 0.45f, 0.45f), steel);
                Box("Pier coping", new Vector3(x, 0.25f, -35.6f), new Vector3(5.8f, 0.15f, 1.6f), concrete);
            }

            Shed("Landing office", 199, -14, 10, 8, brick);
            Shed("Flood control", 215, -17, 8, 8, concrete);
            Shed("Petrov field clinic", 231, 9, 14, 17, white);
            Shed("Shelter dispatch", 245, 11, 8, 11, brick);
            Shed("Shore radio", 263, 10, 10, 14, brick);
            for (int i = 0; i < 4; i++)
            {
                float z = -7 + i * 6;
                Box("Clinic privacy screen", new Vector3(220, 1.15f, z), new Vector3(0.35f, 2, 4.5f), white);
                Box("Supply crate", new Vector3(249, 0.65f, -17 + i * 2), new Vector3(1.8f, 1, 1.5f), wood);
            }
            // A covered service route lets the free amoeba approach the guarded radio yard unseen.
            for (int i = 0; i < 7; i++)
            {
                Box("Riverside cover", new Vector3(253 + i * 3, 0.9f, -12), new Vector3(2.7f, 1.5f, 1.3f), concrete);
                Cylinder("Service pipe", new Vector3(253 + i * 3, 1.2f, -17), new Vector3(0.7f, 1.3f, 0.7f), steel);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = 191 + i * 18;
                Cylinder("Quay lamp pole", new Vector3(x, 2.4f, -29), new Vector3(0.12f, 2.2f, 0.12f), steel);
                Box("Quay lamp head", new Vector3(x, 4.6f, -29), new Vector3(0.8f, 0.18f, 0.4f), lamp, false);
                var lightGo = new GameObject("Quay light"); lightGo.transform.SetParent(root, false); lightGo.transform.position = new Vector3(x, 4.3f, -29);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, 0.8f, 0.55f); light.range = 10; light.intensity = 1.6f; light.shadows = LightShadows.None;
            }
            Cylinder("Radio mast", new Vector3(270, 8, 19), new Vector3(0.3f, 8, 0.3f), steel);
            for (int i = 0; i < 4; i++) Box("Antenna cross arm", new Vector3(270, 7 + i * 2.2f, 19), new Vector3(3 - i * 0.5f, 0.1f, 0.1f), steel, false);
            Box("Ferry deck", new Vector3(270, 0.35f, -30), new Vector3(6, 0.4f, 8), wood);
            var gangway = Box("Ferry gangway", new Vector3(268.2f, 0.35f, -25), new Vector3(1.8f, 0.15f, 3.6f), wood);
            gangway.transform.localRotation = Quaternion.Euler(7, 0, 0);
            // Visible dock barriers keep normal movement on real footing, directing boarding through the gangway.
            Box("Berth west barrier", new Vector3(265.05f, 0.675f, -31), new Vector3(0.12f, 1.05f, 14), steel);
            Box("Berth east barrier", new Vector3(274.95f, 0.675f, -31), new Vector3(0.12f, 1.05f, 14), steel);
            Box("Berth head barrier west", new Vector3(266.1f, 0.675f, -24.05f), new Vector3(2.2f, 1.05f, 0.12f), steel);
            Box("Berth head barrier east", new Vector3(272.1f, 0.675f, -24.05f), new Vector3(5.8f, 1.05f, 0.12f), steel);
            Box("Ferry cabin", new Vector3(270, 1.6f, -32), new Vector3(3.5f, 2.2f, 2.5f), white);
            Box("Ferry cabin glazing", new Vector3(270, 1.9f, -30.72f), new Vector3(2.6f, 0.8f, 0.04f), M("GlassDark", new Color(0.1f, 0.18f, 0.2f)), false);
            Sign("RIVERSIDE / SOUTH FERRY", new Vector3(270, 2.5f, -30.65f), 0.07f);

            GameObject Terminal(string name, Vector3 position)
            {
                var terminal = new GameObject(name) { layer = Layers.Interactable }; terminal.transform.SetParent(root, false); terminal.transform.position = position;
                var body = Prim.Make(PrimitiveType.Cube, terminal.transform, new Vector3(0, 0.8f, 0), new Vector3(1.1f, 1.3f, 0.55f), steel, true, "Housing");
                body.layer = Layers.Interactable;
                var display = Prim.Make(PrimitiveType.Cube, terminal.transform, new Vector3(0, 1.2f, -0.3f), new Vector3(0.85f, 0.38f, 0.04f), teal, false, "Screen");
                var collider = display.GetComponent<Collider>(); if (collider) collider.enabled = false;
                return terminal;
            }
            CampaignOperation Operation(string flag, string name, Vector3 position, string prerequisite, Profession role, float seconds)
            {
                var terminal = Terminal(name, position); var op = terminal.AddComponent<CampaignOperation>();
                op.flag = flag; op.displayName = name; op.requiredFlag = prerequisite; op.profession = role; op.seconds = seconds;
                op.screen = terminal.transform.Find("Screen").GetComponent<Renderer>(); op.requireCalm = true;
                op.doneToast = name + " complete"; return op;
            }
            void Control(string id, string name, Vector3 position, string prerequisite, string retire, Profession role)
            {
                var terminal = Terminal(name, position); var c = terminal.AddComponent<CooperativeConsole>();
                c.controlId = id; c.displayName = name; c.requiredFlag = prerequisite; c.retireWhenFlag = retire; c.profession = role;
                c.lamp = terminal.transform.Find("Screen").GetComponent<Renderer>();
            }
            var receiver = Terminal("Emergency receiver", AfterlakeCampaign.Receiver).AddComponent<MissionProp>();
            receiver.flag = AfterlakeCampaign.Signal; receiver.displayName = "Petrov's emergency receiver"; receiver.verb = "Answer"; receiver.holdSeconds = 2;
            receiver.requiredFlag = AfterlakeCampaign.Started; receiver.doneToast = "Petrov: shut the floodgate, then reach the clinic.";
            Operation(AfterlakeCampaign.Floodgate, "Isolate floodgate", AfterlakeCampaign.FloodgatePanel, AfterlakeCampaign.Signal, Profession.Maintenance, 6);
            Operation(AfterlakeCampaign.WaterAnalysed, "Analyse runoff sample", AfterlakeCampaign.Analysis, AfterlakeCampaign.Floodgate, Profession.Scientist, 7);
            Control(AfterlakeCampaign.CorridorControl, "Shelter dispatch", AfterlakeCampaign.Dispatch, AfterlakeCampaign.WaterAnalysed, AfterlakeCampaign.CorridorOpen, Profession.Guard);
            var records = Operation(AfterlakeCampaign.CorridorOpen, AfterlakeCampaign.Peace ? "Protect evacuation records" : "Acquire shelter identities", AfterlakeCampaign.Records, AfterlakeCampaign.WaterAnalysed, Profession.Scientist, 8);
            records.controlId = AfterlakeCampaign.CorridorControl;
            Control(AfterlakeCampaign.RadioControl, "Mast power stabiliser", AfterlakeCampaign.RadioPower, AfterlakeCampaign.CorridorOpen, AfterlakeCampaign.ChannelOpened, Profession.Maintenance);
            var radio = Operation(AfterlakeCampaign.ChannelOpened, AfterlakeCampaign.Peace ? "Send anonymous relief call" : "Seed the neural relay", AfterlakeCampaign.Radio, AfterlakeCampaign.CorridorOpen, Profession.Scientist, 10);
            radio.controlId = AfterlakeCampaign.RadioControl;
            radio.allowedProfessions = AfterlakeCampaign.Peace ? new[] { Profession.Civilian, Profession.Scientist } : new[] { Profession.Guard, Profession.Scientist };
            var ferry = new GameObject("Ferry intake") { layer = Layers.Interactable }; ferry.transform.SetParent(root, false); ferry.transform.position = AfterlakeCampaign.Ferry;
            ferry.AddComponent<AfterlakeDeparture>();
            Box("Ferry intake grille", AfterlakeCampaign.Ferry + Vector3.up * 0.25f, new Vector3(1.4f, 0.2f, 0.7f), teal, false);

            void Spawn(string name, Profession profession, Vector3 pos, int seed, NpcBehaviour behaviour = NpcBehaviour.Stationary, PatrolRoute route = null)
            {
                var marker = new GameObject("Shore spawn " + name); marker.transform.SetParent(root, false); marker.transform.position = pos;
                var spawn = marker.AddComponent<NpcSpawn>(); spawn.displayName = name; spawn.profession = profession;
                spawn.seed = seed; spawn.behaviour = behaviour; spawn.wanderRadius = 5; spawn.route = route;
            }
            Spawn("Mara · ferry volunteer", Profession.Civilian, new Vector3(195, 0.15f, -19), 731);
            Spawn("Eli · flood engineer", Profession.Maintenance, new Vector3(211, 0.15f, -21), 732);
            Spawn("Dr. Hana Petrov · riverside", Profession.Scientist, new Vector3(227, 0.15f, 1), 733);
            Spawn("Jonas · relief dispatch", Profession.Guard, new Vector3(244, 0.15f, 6), 734);
            Spawn("Noor · mast engineer", Profession.Maintenance, new Vector3(258, 0.15f, -5), 735);
            Spawn("Leah · shelter volunteer", Profession.Civilian, new Vector3(235, 0.15f, -12), 736);
            Spawn("Ingrid · field researcher", Profession.Scientist, new Vector3(229, 0.15f, 9), 737);
            var routeGo = new GameObject("Shore patrol route"); routeGo.transform.SetParent(root, false);
            var route = routeGo.AddComponent<PatrolRoute>(); route.waitAtPoint = 2;
            foreach (var pos in new[] { new Vector3(253, 0.15f, 18), new Vector3(274, 0.15f, 18), new Vector3(274, 0.15f, -7), new Vector3(253, 0.15f, -7) })
            { var point = new GameObject("Patrol point"); point.transform.SetParent(routeGo.transform, false); point.transform.position = pos; }
            Spawn("Vale recovery patrol", Profession.Guard, new Vector3(274, 0.15f, 18), 738, NpcBehaviour.Patrol, route);
            var zoneGo = new GameObject("Radio cordon"); zoneGo.transform.SetParent(root, false); zoneGo.transform.position = new Vector3(264, 0.15f, 9);
            var zone = zoneGo.AddComponent<RestrictedZone>(); zone.zoneName = "Shore radio cordon"; zone.size = new Vector3(28, 4, 28); zone.required = AccessPermission.LabInterior;
            root.gameObject.AddComponent<AfterlakeArt>().Build();
            root.gameObject.AddComponent<AfterlakeProps>().Build();
            Sign("RIVERSIDE RELIEF / KEEP THE WATER CLEAN", new Vector3(230, 2, 30.95f), 0.14f);
            root.gameObject.AddComponent<AfterlakeSurfaceUV>().Apply();
        }
    }
}
