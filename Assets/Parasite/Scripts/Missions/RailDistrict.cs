using System.Collections.Generic;
using Parasite.World;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>Runtime annex built before the normal CityDirector navigation/spawn pass. Staged visuals reuse authored Harbor models.</summary>
    public static class RailDistrict
    {
        public static void Build()
        {
            if (!RailCampaign.Active || GameObject.Find(RailLayout.RootName)) return;
            var world = GameObject.Find("World"); if (!world) world = new GameObject("World");
            var go = new GameObject(RailLayout.RootName); go.transform.SetParent(world.transform, false); go.transform.localPosition = RailLayout.Origin;
            var art = go.AddComponent<HarborArt>(); var visuals = go.AddComponent<RailVisuals>(); var root = go.transform;
            var authored = go.AddComponent<RailAuthoredArt>(); var heroTargets = new List<RailAuthoredArt.Target>();
            GameObject Box(string name, Vector3 at, Vector3 size, int tile = 0, bool solid = true, Transform parent = null) => art.Box(name, at, size, tile, solid, parent);
            Vector3 Local(Vector3 worldPoint) => worldPoint - RailLayout.Origin;
            Collider Slab(string name, Vector3 at, Vector3 size)
            { var floor = Box(name, at, size); floor.GetComponent<Renderer>().enabled = false; return floor.GetComponent<Collider>(); }
            var yardFooting = Slab("Northline yard physical footing", new Vector3(0, -.5f, 5), new Vector3(80, 1.3f, 100));
            var exitFooting = Slab("Northline exit track physical footing", new Vector3(8, -.5f, 60.5f), new Vector3(4.6f, 1.3f, 11));
            visuals.Paving(art, -40, 40, -45, 55); visuals.Paving(art, 5.7f, 10.3f, 55, 66);
            foreach (float x in new[] { -39.8f, 39.8f }) Box("Northline perimeter side", new Vector3(x, 1.65f, 5), new Vector3(.4f, 3, 100));
            Box("Northline south perimeter", new Vector3(0, 1.65f, -44.8f), new Vector3(79.2f, 3, .4f));
            Box("Northline north west perimeter", new Vector3(-17.05f, 1.85f, 54.8f), new Vector3(45.9f, 3.4f, .4f));
            Box("Northline north east perimeter", new Vector3(25.05f, 1.85f, 54.8f), new Vector3(29.9f, 3.4f, .4f));
            foreach (float x in new[] { 5.6f, 10.4f }) Box("Northline departure tunnel side", new Vector3(x, 1.85f, 60.5f), new Vector3(.4f, 3.4f, 11));
            Box("Northline departure tunnel end", new Vector3(8, 1.85f, 65.8f), new Vector3(5.2f, 3.4f, .4f));
            void Exclude(string name, Bounds bounds)
            { var area = new GameObject(name); area.transform.SetParent(root, false); var volume = area.AddComponent<NavMeshModifierVolume>(); volume.center = bounds.center; volume.size = bounds.size; volume.area = 1; }
            Exclude("Northline west rail nonwalkable strip", new Bounds(new Vector3(-22, 1, -16), new Vector3(36, 4, 4)));
            Exclude("Northline east rail nonwalkable strip", new Bounds(new Vector3(22, 1, -16), new Vector3(36, 4, 4)));
            foreach (float z in new[] { -20f, -12f })
                foreach (float x in new[] { -22f, 22f }) Box("Northline rail edge fence", new Vector3(x, 1.3f, z), new Vector3(36, 2.3f, .18f), 1);
            // Rails/ties have visible geometry but no extra footing collider or unintended player obstacle.
            foreach (float z in new[] { -16.7f, -15.3f }) Box("Northline running rail", new Vector3(0, .19f, z), new Vector3(70, .08f, .08f), 1, false);
            for (float x = -34; x <= 34; x += 1.2f) Box("Northline rail sleeper", new Vector3(x, .165f, -16), new Vector3(.20f, .03f, 2.4f), 3, false);
            Transform Gate(string name, Vector3 at, Vector3 size)
            {
                var gate = Box(name, at, size, 2); gate.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                var obstacle = gate.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one; obstacle.carving = true; obstacle.carveOnlyStationary = false;
                return gate.transform;
            }
            var south = Gate("Northline south crossing barrier", new Vector3(0, 1.45f, -20), new Vector3(8, 2.6f, .18f));
            var north = Gate("Northline north crossing barrier", new Vector3(0, 1.45f, -12), new Vector3(8, 2.6f, .18f));
            Rigidbody Body(string name, Vector3 at, Vector3 size)
            {
                var body = new GameObject(name); body.transform.SetParent(root, false); body.transform.localPosition = at;
                var collider = body.AddComponent<BoxCollider>(); collider.center = new Vector3(0, size.y * .5f, 0); collider.size = size;
                body.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                var rigidbody = body.AddComponent<Rigidbody>(); rigidbody.isKinematic = true; rigidbody.useGravity = false; rigidbody.detectCollisions = true; rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                return rigidbody;
            }
            var tram = Body("Northline actual maintenance tram", new Vector3(-32, .15f, -16), new Vector3(6, 1.8f, 2.4f));
            Box("Northline staged tram chassis", new Vector3(0, .32f, 0), new Vector3(6, .60f, 2.4f), 1, false, tram.transform);
            Box("Northline staged tram cab", new Vector3(0, 1.15f, 0), new Vector3(2.6f, 1.3f, 2.2f), 2, false, tram.transform);
            foreach (float x in new[] { -2.2f, 2.2f }) foreach (float z in new[] { -1.1f, 1.1f }) Box("Northline staged tram wheel", new Vector3(x, .24f, z), new Vector3(.65f, .48f, .22f), 3, false, tram.transform);
            var crossing = go.AddComponent<RailCrossing>(); crossing.Configure(tram, new[] { south, north });

            Exclude("Northline freight powered transfer bed", new Bounds(new Vector3(4, 1.5f, 13), new Vector3(60, 4, 26)));
            // This industrial cradle translates on orthogonal powered transfer decks; it is not a train cornering at right angles.
            void Fence(float x, float start, float end)
            { for (float z = start; z < end; z += .45f) Box("Northline freight real grille bar", new Vector3(x, 1.4f, z), new Vector3(.08f, 2.5f, .08f), 1); }
            Fence(-27, 0, 26); Fence(35, 0, 26);
            foreach (float z in new[] { 0f, 26f })
                for (float x = -27; x <= 35; x += .45f) Box("Northline freight real end grille", new Vector3(x, 1.4f, z), new Vector3(.08f, 2.5f, .08f), 1);
            foreach (float z in new[] { 6f, 8f, 18f, RailLayout.FreightReturnZ })
                foreach (float dz in new[] { -.55f, .55f }) Box("Northline transfer deck lateral rail", new Vector3(3, .20f, z + dz), new Vector3(52, .10f, .08f), 1, false);
            foreach (float x in new[] { -22f, -7f, 11f, 28f })
                foreach (float dx in new[] { -.55f, .55f }) Box("Northline transfer deck longitudinal rail", new Vector3(x + dx, .20f, (6f + RailLayout.FreightReturnZ) * .5f), new Vector3(.08f, .10f, RailLayout.FreightReturnZ - 6f), 1, false);
            var freightBody = Body("Northline actual sealed transfer cradle", Local(RailLayout.FreightDock), new Vector3(3, 1.5f, 2));
            Box("Northline staged transfer chassis", new Vector3(0, .20f, 0), new Vector3(3, .4f, 2), 1, false, freightBody.transform);
            art.Model("SealedCrate", new Vector3(0, .4f, 0), parent: freightBody.transform);
            var blades = new Transform[2];
            for (int i = 0; i < 2; i++)
            { var pivot = new GameObject("Northline mechanical point " + (i == 0 ? "A" : "B")); pivot.transform.SetParent(root, false); pivot.transform.localPosition = i == 0 ? new Vector3(-7, .25f, 6) : new Vector3(11, .25f, 13); blades[i] = pivot.transform; Box("Northline physical point blade visual", Vector3.zero, new Vector3(2.8f, .09f, .10f), 2, false, pivot.transform); }
            var freight = go.AddComponent<RailFreight>(); freight.Configure(freightBody, blades);

            // An enclosed east service area has one genuinely low entrance and a later full-height platform door.
            Box("Northline signal wall south", new Vector3(0, 1.65f, 27.5f), new Vector3(.6f, 3, 11));
            Box("Northline signal wall centre", new Vector3(0, 1.65f, 41), new Vector3(.6f, 3, 12));
            Box("Northline signal wall north", new Vector3(0, 1.65f, 53), new Vector3(.6f, 3, 4));
            Box("Northline signal enclosure south", new Vector3(20, 1.65f, 26.1f), new Vector3(40, 3, .2f));
            Box("Northline low service header", new Vector3(0, 2.1f, 34), new Vector3(4.4f, 2.1f, 2));
            foreach (float z in new[] { 33.1f, 34.9f }) Box("Northline low service side", new Vector3(0, .6f, z), new Vector3(4.4f, .9f, .2f));
            var lowGate = Gate("Northline dead-man service gate", new Vector3(-2.2f, .65f, 34), new Vector3(.18f, 1, 1.4f));
            var platform = Gate("Northline north platform release gate", new Vector3(0, 1.65f, 49), new Vector3(.3f, 3, 4));
            var portal = Gate("Northline departure portal", new Vector3(8, 1.85f, 54.8f), new Vector3(4.2f, 3.4f, .25f));
            var escape = new GameObject("Northline departure carriage"); escape.transform.SetParent(root, false); escape.transform.position = RailLayout.CoachStart;
            Box("Northline staged departure underframe", new Vector3(0, .43f, 0), new Vector3(3.05f, .32f, 13.8f), 1, true, escape.transform);
            var coachDeck = Box("Northline staged departure deck", new Vector3(0, .67f, 0), new Vector3(RailLayout.CoachWidth, .16f, RailLayout.CoachLength), 1, true, escape.transform);
            Box("Northline staged departure canopy", new Vector3(0, 3.2f, 0), new Vector3(RailLayout.CoachWidth, .18f, RailLayout.CoachLength), 1, true, escape.transform);
            foreach (float z in new[] { -6.9f, 6.9f }) Box("Northline departure end wall", new Vector3(0, 1.94f, z), new Vector3(3.2f, 2.38f, .18f), 1, true, escape.transform);
            Box("Northline departure east side", new Vector3(1.52f, 1.94f, 0), new Vector3(.16f, 2.38f, 13.8f), 1, true, escape.transform);
            foreach (float z in new[] { -4.05f, 4.05f }) Box("Northline departure west side beside boarding door", new Vector3(-1.52f, 1.94f, z), new Vector3(.16f, 2.38f, 5.7f), 1, true, escape.transform);
            foreach (float x in new[] { -1.45f, 1.45f }) foreach (float z in new[] { -5f, -4f, 4f, 5f }) Box("Northline departure wheel", new Vector3(x, .4f, z), new Vector3(.22f, .5f, .5f), 3, true, escape.transform);
            foreach (float z in new[] { -1.2f, 1.2f }) Box("Northline departure boarding door jamb", new Vector3(-1.52f, 1.94f, z), new Vector3(.16f, 2.38f, .08f), 2, true, escape.transform);
            // Seats are solid reachable fittings on the actual coach compound body.
            // These32 precise pan/back/support shapes are captured by SetTransitPhysics below;
            // the original deck/ramp/control/boarding and outer compound stay exact.
            for (int i = 0; i < RailLayout.CoachSeatBoundsLocal.Length; i++)
            {
                var seat = RailLayout.CoachSeatBoundsLocal[i];
                var part = Box("Northline coach solid seat " + i, seat.center, seat.size, 3, true, escape.transform);
                // AddComponent auto-fit can round world-space mesh bounds at x≈1500.
                // These authored local cube parts have exact local collision geometry.
                var collider = part.GetComponent<BoxCollider>(); collider.center = Vector3.zero; collider.size = Vector3.one;
            }
            Exclude("Northline parked coach nonwalkable floor", new Bounds(new Vector3(8, 1.8f, 45), new Vector3(3.6f, 4, 14.4f)));
            var boardingRamp = visuals.BoardingRamp(art);
            escape.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var coachBody = escape.AddComponent<Rigidbody>(); coachBody.isKinematic = true; coachBody.useGravity = false; coachBody.detectCollisions = true; coachBody.interpolation = RigidbodyInterpolation.None;
            var departure = go.AddComponent<RailDeparture>();
            var workControls = new List<RailWorkControl>();
            RailWorkControl Control(string name, RailWork kind, Vector3 point, bool low = false)
            {
                var station = new GameObject(name); station.transform.SetParent(root, false); station.transform.position = point;
                art.Model(low ? "Emitter" : "ValveBase", Vector3.zero, parent: station.transform);
                var collider = station.AddComponent<BoxCollider>(); collider.center = new Vector3(0, low ? .4f : .72f, 0); collider.size = new Vector3(.7f, low ? .8f : 1.44f, .5f);
                var control = station.AddComponent<RailWorkControl>(); control.kind = kind; workControls.Add(control); return control;
            }
            Control("Northline crossing request", RailWork.Crossing, RailLayout.CrossingControl);
            Control("Northline north-side return request", RailWork.CrossingReturn, RailLayout.CrossingReturnControl);
            Control("Northline sealed manifest desk", RailWork.Manifest, RailLayout.ManifestControl);
            for (int i = 0; i < 2; i++) Control("Northline point lever " + (i == 0 ? "A" : "B"), i == 0 ? RailWork.PointA : RailWork.PointB, RailLayout.PointControls[i]);
            Control("Northline sealed carriage dispatch", RailWork.Dispatch, RailLayout.DispatchControl);
            var crank = Control("Northline dead-man crank", RailWork.Crank, RailLayout.Crank + Vector3.forward * 1.4f);
            var crankWheel = art.Model("ValveWheel", new Vector3(0, 1.1f, -.36f), parent: crank.transform); departure.SetCrankVisual(crankWheel.transform);
            var signal = Control("Northline manual east signal", RailWork.Signal, RailLayout.Signal + Vector3.forward * 1.2f, true);
            Control("Northline inside service release", RailWork.EmergencyReturn, RailLayout.At(4, .15f, 35.5f), true);
            var boardingControl = Control("Northline departure control", RailWork.Departure, RailLayout.CoachStart + RailLayout.DepartureControlLocal, true);
            boardingControl.transform.SetParent(escape.transform, true);
            var receiver = new GameObject("Northline real optical receiver"); receiver.transform.SetParent(signal.transform, false); receiver.transform.position = RailLayout.Signal + new Vector3(0, .8f, .9f);
            departure.Configure(lowGate, platform, receiver.transform, escape.transform); departure.SetPortal(portal); departure.SetTransitPhysics(coachBody, new[] { yardFooting, exitFooting }, coachDeck.GetComponent<BoxCollider>());

            foreach (var cover in new[] { new Vector3(-19, 1.15f, 34), new Vector3(-17, 1.15f, -30), new Vector3(-34, 1.15f, 20) })
                Box("Northline opaque patrol cover", cover, new Vector3(.6f, 2, 7));
            void Spawn(string name, Profession role, Vector3 point, int seed, NpcBehaviour behaviour = NpcBehaviour.Stationary, PatrolRoute route = null)
            { var marker = new GameObject("Northline spawn " + name); marker.transform.SetParent(root, false); marker.transform.localPosition = point; var spawn = marker.AddComponent<NpcSpawn>(); spawn.displayName = name; spawn.profession = role; spawn.seed = seed; spawn.behaviour = behaviour; spawn.route = route; spawn.reportsPlayerViolations = role == Profession.Guard; }
            PatrolRoute Patrol(string name, params Vector3[] points)
            { var path = new GameObject(name); path.transform.SetParent(root, false); var route = path.AddComponent<PatrolRoute>(); route.waitAtPoint = 2; foreach (var point in points) { var marker = new GameObject("Northline patrol waypoint"); marker.transform.SetParent(path.transform, false); marker.transform.localPosition = point; } return route; }
            Spawn("Northline south maintenance worker", Profession.Maintenance, new Vector3(-22, .15f, -34), 2401);
            Spawn("Northline south scientist", Profession.Scientist, new Vector3(-17, .15f, -34), 2402);
            Spawn("Northline yard maintenance worker", Profession.Maintenance, new Vector3(-31, .15f, -3), 2501);
            Spawn("Northline manifest scientist", Profession.Scientist, new Vector3(-35, .15f, 5), 2502);
            var southPatrol = Patrol("Northline south patrol", new Vector3(14, .15f, -35), new Vector3(30, .15f, -35), new Vector3(30, .15f, -26), new Vector3(14, .15f, -26));
            var westPatrol = Patrol("Northline west signal patrol", new Vector3(-33, .15f, 30), new Vector3(-27, .15f, 30), new Vector3(-27, .15f, 44), new Vector3(-33, .15f, 44));
            Spawn("Northline southern crossing guard", Profession.Guard, new Vector3(30, .15f, -35), 2403, NpcBehaviour.Patrol, southPatrol);
            Spawn("Northline signal approach guard", Profession.Guard, new Vector3(-33, .15f, 44), 2601, NpcBehaviour.Patrol, westPatrol);
            // TextMesh's front faces local -Z. The first three signs are read on the northbound approach.
            void Sign(string text, Vector3 at, float yaw = 0)
            {
                var item = new GameObject("Northline sign " + text); item.transform.SetParent(root, false);
                item.transform.localPosition = at; item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var label = item.AddComponent<TextMesh>(); label.text = text;
                var font = Resources.Load<Font>("UI/Rajdhani-Medium");
                label.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 64; label.characterSize = .10f; label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center; label.color = new Color(.96f, .81f, .45f);
                item.AddComponent<RailWorldLabelMaterial>();
            }
            Sign("NORTHLINE\nREQUEST BEFORE CROSSING", new Vector3(-8, 2.4f, -25.8f));
            Sign(RailCampaign.Peace ? "MEDICAL COLD STORE\nA LEFT / B RIGHT" : "RELAY CARRIAGE\nA RIGHT / B LEFT", new Vector3(-30, 2.5f, 8));
            Sign("MAINTENANCE: HOLD CRANK\nTAB / FREE BODY: LOW DUCT", new Vector3(-9, 2.6f, 34));
            Sign("INSIDE RELEASE\nE / FREE BODY / RETURN ONLY", new Vector3(4, 1.5f, 35.8f), 180);
            Sign("BOTH FREE BODIES ABOARD\nE TO DEPART", new Vector3(4.5f, 2.4f, 45), 180);
            // Validate the complete eleven-model kit before hiding a single captured provisional face.
            // Physical primitive parents stay scaled for collision/navigation; the owned art child cancels only that scale.
            void Hero(string asset, Transform physicalParent, Vector3 metreOffset, params Transform[] except)
                => heroTargets.Add(new RailAuthoredArt.Target(asset, physicalParent, metreOffset, RailAuthoredArt.CaptureOriginals(physicalParent, art.Shared, except)));
            Hero("RailTram", tram.transform, Vector3.zero);
            Hero("RailTransferCradle", freightBody.transform, Vector3.zero);
            foreach (var blade in blades) Hero("RailPointBlade", blade, Vector3.zero);
            Hero("RailCrossingBarrier", south, Vector3.zero); Hero("RailCrossingBarrier", north, Vector3.zero);
            Hero("RailServiceGate", lowGate, Vector3.zero); Hero("RailPlatformGate", platform, Vector3.zero); Hero("RailPortalGate", portal, Vector3.zero);
            Hero("RailDepartureCoach", escape.transform, Vector3.zero, boardingControl.transform);
            Hero("RailBoardingRamp", boardingRamp.transform, new Vector3(3, 0, 45));
            foreach (var control in workControls)
            {
                bool low = control.kind == RailWork.Signal || control.kind == RailWork.EmergencyReturn || control.kind == RailWork.Departure;
                Hero(low ? "RailLowSignal" : "RailControl", control.transform, Vector3.zero, control == crank ? crankWheel.transform : null);
            }
            if (!authored.TryApply(art, heroTargets)) Debug.Log("[NorthlineArt] Retaining complete provisional visuals: " + authored.LastFailure);
            visuals.MergeStatic(art, tram.transform, freightBody.transform, blades[0], blades[1], south, north, lowGate, platform, portal, escape.transform, crankWheel.transform);
        }
    }
}
