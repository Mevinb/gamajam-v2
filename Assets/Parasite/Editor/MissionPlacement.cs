using System.Collections.Generic;
using Parasite.Missions;
using Parasite.Stealth;
using UnityEditor;
using UnityEngine;
using static Parasite.EditorTools.CityAssets;
using L = Parasite.World.CityLayout;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Places the campaign's mission objects (called at the end of CityBuilder.Gameplay) and wires mission flags
    /// into the existing entrances. Positions live in Parasite.World.MissionLayout.
    /// </summary>
    internal static class MissionPlacement
    {
        public static void Place(WorldKit k, Transform gameplay)
        {
            var root = new GameObject("Missions").transform;
            root.SetParent(gameplay, false);
            WireEntrances(gameplay);
            ExtraNpcs(root);

            // M2: newsstand on City Plaza (front = local -Z, where the reader stands)
            var np = World.MissionLayout.Newsstand; float ny = World.MissionLayout.NewsstandYaw;
            var nq = Quaternion.Euler(0, ny, 0);
            k.Box(M("DumpsterGreen"), np + nq * new Vector3(0, 1.1f, 0.2f), new Vector3(2.4f, 2.2f, 1.2f), nq);
            k.Solid(np + nq * new Vector3(0, 1.1f, 0.2f), Flat(new Vector3(2.4f, 2.2f, 1.2f), ny));
            k.Box(M("AwningGreen"), np + nq * new Vector3(0, 2.35f, -0.25f), new Vector3(2.8f, 0.12f, 1.9f), nq);
            k.Box(M("SignBoard"), np + nq * new Vector3(0, 1.0f, -0.42f), new Vector3(2.2f, 0.9f, 0.05f), nq);
            k.Text("CITY NEWS", np + nq * new Vector3(0, 2.62f, -1.2f), ny, 0.28f, new Color(1f, 0.95f, 0.8f));
            k.Text("LAB LEAK RUMOURS DENIED BY VALE BIOTECH", np + nq * new Vector3(0, 1.25f, -0.46f), ny, 0.11f, new Color(0.95f, 0.95f, 0.9f));
            var news = Prop(root, "Newsstand", np + nq * new Vector3(0, 1.2f, -0.9f), MissionFlags.NewsRead, "Read the headlines", "Newsstand", 2.5f,
                "Front page: 'VALE BIOTECH DENIES LAB LEAK' — Dr. E. Vale, north block campus.", nq);
            news.requireHost = true;

            // M2: café eavesdrop (Dr. Hana Petrov stands at CafeSpot)
            var ez = new GameObject("Eavesdrop_Cafe").AddComponent<EavesdropZone>();
            ez.transform.SetParent(root, false);
            ez.transform.position = L.CafeSpot;
            ez.flag = MissionFlags.Eavesdropped;
            ez.radius = 4.5f; ez.seconds = 9f;
            ez.speaker = "DR. PETROV (on the phone)";
            ez.lines = new[]
            {
                "…no, Vale's pulling another all-nighter. Says Specimen 7 is 'progressing'.",
                "The campus? North block, past Campus Road. Badge at the main gate, like always.",
                "If you forgot your card, use the staff terminal on the west side — I left mine on this table again, ugh.",
                "Maintenance keeps the service tunnel shut since the substation tripped. Anyway, his office needs two of us to open. Paranoid.",
            };

            // M3-B: keycard on a café table next to Petrov
            var kp = World.MissionLayout.Keycard;
            k.Block(M("Wood"), kp + new Vector3(0, 0.37f, 0), new Vector3(0.9f, 0.05f, 0.9f));
            k.Cylinder(M("MetalDark"), kp, 0.05f, 0.37f);
            var card = Prop(root, "Keycard", kp + new Vector3(0, 0.42f, 0), MissionFlags.Keycard, "Take", "Dr. Petrov's keycard", 0f,
                "Keycard taken. The Staff Terminal will accept it — but only from a scientist.", Quaternion.identity, small: true);
            card.requireHost = true;
            card.requiredPermission = AccessPermission.StaffTerminal;
            card.permissionReason = "Only a scientist could pocket that keycard without looking odd.";
            card.hideWhenDone = true;

            // M3-C: substation power box in the east alley
            var sp = World.MissionLayout.Substation; float sy = World.MissionLayout.SubstationYaw; var sq = Quaternion.Euler(0, sy, 0);
            k.Box(M("MetalDark"), sp + sq * new Vector3(0, 1.1f, 0), new Vector3(1.6f, 2.2f, 0.7f), sq);
            k.Solid(sp + sq * new Vector3(0, 1.1f, 0), Flat(new Vector3(1.6f, 2.2f, 0.7f), sy));
            k.Box(M("PaintYellow"), sp + sq * new Vector3(0, 1.7f, -0.36f), new Vector3(0.5f, 0.35f, 0.02f), sq);
            k.Text("DANGER — SUBSTATION 4", sp + sq * new Vector3(0, 2.0f, -0.37f), sy, 0.12f, new Color(1f, 0.85f, 0.1f));
            var power = Prop(root, "Substation", sp + sq * new Vector3(0, 1.2f, -0.75f), MissionFlags.Power, "Reset breakers", "Substation 4", 3f,
                "Power restored. The service tunnel door has power again.", sq);
            power.requiredPermission = AccessPermission.ServiceTunnel;
            power.permissionReason = "Only maintenance would know how to reset a substation.";

            // M4: data terminals in Lab A and Lab B
            DataTerminal(k, root, World.MissionLayout.DataTerminalA, World.MissionLayout.DataTerminalAYaw, MissionFlags.DataA, "Lab A research terminal");
            DataTerminal(k, root, World.MissionLayout.DataTerminalB, World.MissionLayout.DataTerminalBYaw, MissionFlags.DataB, "Lab B research terminal");

            // M4: Dr. Vale's office door + two-person-rule readers
            var doorGo = new GameObject("Door_ValeOffice");
            doorGo.transform.SetParent(root, false);
            doorGo.transform.position = World.MissionLayout.OfficeDoor;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(doorGo.transform, false);
            panel.transform.localPosition = new Vector3(0, 1.32f, 0);
            panel.transform.localScale = new Vector3(2.44f, 2.64f, 0.12f);
            panel.GetComponent<Renderer>().sharedMaterial = M("DoorStaff");
            var office = doorGo.AddComponent<AccessDoor>();
            office.doorName = "Dr. Vale's Office";
            office.panel = panel.transform;
            office.openLocalOffset = new Vector3(2.45f, 0, 0);
            office.terminalControlled = true;
            office.requiredFlagReason = "TWO-PERSON RULE — badge both readers beside the door at the same time.";
            office.range = 2.2f;
            k.Text("DR. E. VALE — DIRECTOR", World.MissionLayout.OfficeDoor + new Vector3(0, 2.9f, -0.16f), 0f, 0.16f, new Color(0.85f, 1f, 0.9f));
            Reader(root, World.MissionLayout.ReaderA, "Reader A", office);
            Reader(root, World.MissionLayout.ReaderB, "Reader B", office);

            PostValeCampaign(k, root);
            TailMission(k, root);
            HunterMission(k, root);
            BlackoutMission(k, root);

            // stealth: CCTV
            var cams = new GameObject("SecurityCameras").transform;
            cams.SetParent(root, false);
            foreach (var (name, pos, yaw, range) in World.MissionLayout.Cameras) Cctv(cams, name, pos, yaw, range);
        }

        static void PostValeCampaign(WorldKit k, Transform root)
        {
            var coolant = Control(k, root, World.MissionLayout.CoolantBypass, "COOLANT BYPASS", "coolant", Profession.Maintenance,
                MissionFlags.HuntersDown, MissionFlags.SignedArchive);
            var archive = Operation(k, root, World.MissionLayout.SignedArchive, "COPY SIGNED ARCHIVE", MissionFlags.SignedArchive, MissionFlags.HuntersDown,
                Profession.Scientist, 8f, "The signed orders are copied. Now sever Vale's containment relay before he tracks the evidence.");
            archive.controlId = coolant.controlId;
            var dispatch = Control(k, root, World.MissionLayout.DispatchOverride, "DISPATCH OVERRIDE", "dispatch", Profession.Guard,
                MissionFlags.SignedArchive, MissionFlags.ContainmentOffline);
            var relay = Operation(k, root, World.MissionLayout.ContainmentRelay, "OVERLOAD CONTAINMENT", MissionFlags.ContainmentOffline, MissionFlags.SignedArchive,
                Profession.Maintenance, 7f, "Containment relay destroyed. CCTV is permanently offline and campus exits are unlocked.");
            relay.controlId = dispatch.controlId;
            var uplink = Operation(k, root, World.MissionLayout.JournalistUplink, "BROADCAST TO JOURNALIST", MissionFlags.EvidenceBroadcast, MissionFlags.ContainmentOffline,
                Profession.Civilian, 10f, "Evidence received by Independent News! But the neural kill-switch and trial files are in the Black Site Vault.");
            uplink.anyHost = true; uplink.requireCalm = true;

            // M9: THE BLACK SITE ARCHIVE
            var subvault = Control(k, root, World.MissionLayout.SubVaultDiversion, "SUB-VAULT DIVERSION", MissionFlags.SubVaultDiversion, Profession.Guard,
                MissionFlags.EvidenceBroadcast, MissionFlags.BlackSiteArchive, MissionFlags.SubVaultDiversion);
            k.Text("SUB-VAULT SECURITY & EVACUATION ALARM", World.MissionLayout.SubVaultDiversion + new Vector3(0, 2.2f, 0.45f), 0f, 0.11f, new Color(1f, 0.8f, 0.2f));
            k.Box(M("PaintYellow"), World.MissionLayout.SubVaultDiversion + new Vector3(0, 1.8f, 0.58f), new Vector3(1.1f, 0.08f, 0.52f));

            var blackSiteVault = Operation(k, root, World.MissionLayout.BlackSiteVault, "BLACK SITE VAULT", MissionFlags.BlackSiteArchive, MissionFlags.EvidenceBroadcast,
                Profession.Scientist, 10f, "Black Site Archive downloaded! Containment Protocol Epsilon initiated — facility lockdown active!");
            blackSiteVault.controlId = subvault.controlId;
            k.Text("SUBTERRANEAN BLACK SITE VAULT", World.MissionLayout.BlackSiteVault + new Vector3(0, 2.2f, 0.45f), 0f, 0.11f, new Color(1f, 0.35f, 0.35f));
            k.Box(M("MetalDark"), World.MissionLayout.BlackSiteVault + new Vector3(0, 1.2f, 0.8f), new Vector3(1.6f, 2.4f, 0.2f));
            k.Box(M("DoorStaff"), World.MissionLayout.BlackSiteVault + new Vector3(0, 1.1f, 0.7f), new Vector3(1.2f, 2.2f, 0.1f));
            k.Text("CLASSIFIED — AUTHORIZED SCIENTISTS ONLY", World.MissionLayout.BlackSiteVault + new Vector3(0, 1.8f, 0.62f), 0f, 0.08f, new Color(0.9f, 0.9f, 0.9f));

            // M10: CONTAINMENT PROTOCOL EPSILON
            var backupGen = Control(k, root, World.MissionLayout.BackupGenerator, "BACKUP GENERATOR", MissionFlags.BackupGenerator, Profession.Maintenance,
                MissionFlags.BlackSiteArchive, MissionFlags.GridOverride, MissionFlags.BackupGenerator);
            k.Text("AUXILIARY BACKUP GENERATOR — GRID EPSILON", World.MissionLayout.BackupGenerator + new Vector3(0, 2.2f, 0.45f), 0f, 0.11f, new Color(1f, 0.85f, 0.1f));
            k.Box(M("MetalDark"), World.MissionLayout.BackupGenerator + new Vector3(0, 1.0f, 0.8f), new Vector3(1.8f, 2.0f, 0.8f));
            k.Solid(World.MissionLayout.BackupGenerator + new Vector3(0, 1.0f, 0.8f), new Vector3(1.8f, 2.0f, 0.8f));
            k.Box(M("PaintYellow"), World.MissionLayout.BackupGenerator + new Vector3(0, 1.7f, 0.38f), new Vector3(0.6f, 0.3f, 0.02f));
            k.Text("HIGH VOLTAGE — 10,000V", World.MissionLayout.BackupGenerator + new Vector3(0, 1.7f, 0.36f), 0f, 0.09f, new Color(1f, 0.2f, 0.2f));

            var gridOverride = Operation(k, root, World.MissionLayout.GridOverride, "GRID OVERRIDE", MissionFlags.GridOverride, MissionFlags.BlackSiteArchive,
                Profession.Guard, 8f, "Campus security grid disabled! Containment Protocol Epsilon breached.");
            gridOverride.controlId = backupGen.controlId;
            gridOverride.allowedProfessions = new[] { Profession.Guard, Profession.Scientist };
            k.Text("SECURITY GRID PYLON — PROTOCOL EPSILON", World.MissionLayout.GridOverride + new Vector3(0, 2.2f, 0.45f), 0f, 0.11f, new Color(1f, 0.4f, 0.4f));
            k.Cylinder(M("MetalDark"), World.MissionLayout.GridOverride + new Vector3(0, 0f, 0.8f), 0.35f, 3.2f);
            k.Box(M("NeonRed"), World.MissionLayout.GridOverride + new Vector3(0, 3.2f, 0.8f), new Vector3(0.4f, 0.4f, 0.4f));
            k.Text("PERIMETER LOCKDOWN PYLON", World.MissionLayout.GridOverride + new Vector3(0, 2.8f, 0.6f), 0f, 0.10f, new Color(1f, 0.2f, 0.2f));

            // M11: THE FINAL CONVERGENCE
            var masterUplink = Operation(k, root, World.MissionLayout.MasterUplink, "PLAZA MASTER ANTENNA", MissionFlags.MasterBroadcast, MissionFlags.DistrictBlackout,
                Profession.Civilian, 12f, "The Black Site Archive has been broadcast worldwide! Eject from your host [Q] and escape through the storm drain.");
            masterUplink.anyHost = true; masterUplink.requireCalm = true;
            k.Text("METROPOLITAN BROADCAST MAST", World.MissionLayout.MasterUplink + new Vector3(0, 2.2f, 0.45f), 0f, 0.12f, new Color(0.4f, 0.9f, 1f));
            k.Cylinder(M("MetalDark"), World.MissionLayout.MasterUplink + new Vector3(0, 0f, 0.8f), 0.25f, 4.5f);
            k.Box(M("NeonCyan"), World.MissionLayout.MasterUplink + new Vector3(0, 4.5f, 0.8f), new Vector3(0.5f, 0.2f, 0.5f));
            k.Text("EMERGENCY CIVIL DEFENSE UPLINK", World.MissionLayout.MasterUplink + new Vector3(0, 1.8f, 0.45f), 0f, 0.09f, new Color(0.8f, 0.95f, 1f));

            var extraction = new GameObject("StormDrain_Extraction");
            extraction.transform.SetParent(root, false);
            extraction.transform.position = World.MissionLayout.Extraction;
            extraction.AddComponent<CampaignExtraction>();
            k.Text("DRAIN ACCESS — SPECIMEN 7", World.MissionLayout.Extraction + new Vector3(0, 0.65f, -0.35f), 0f, 0.11f, new Color(0.5f, 1f, 0.65f));

            // A nearby maintenance body makes the laboratory co-op accessible on every infiltration route.
            var worker = new GameObject("NpcSpawn_CoolantEngineer");
            worker.transform.SetParent(root, false);
            worker.transform.position = new Vector3(20f, L.LabFloor, 90.8f);
            var spawn = worker.AddComponent<World.NpcSpawn>();
            spawn.displayName = "Engineer Noor Patel"; spawn.profession = Profession.Maintenance;
            spawn.behaviour = World.NpcBehaviour.Stationary; spawn.seed = 211;
        }

        // ------------------------------------------------------------------ THE TAIL
        static void TailMission(WorldKit k, Transform root)
        {
            // Petrov's dead drop: a rusty junction box on the alley's west wall, behind the dumpster.
            var dp = World.MissionLayout.DeadDrop;
            k.Box(M("MetalDark"), new Vector3(-35.89f, dp.y, dp.z), new Vector3(0.2f, 0.55f, 0.46f));
            k.Box(M("SteelPlate"), new Vector3(-35.78f, dp.y + 0.2f, dp.z), new Vector3(0.03f, 0.1f, 0.36f));
            k.Text("J-BOX 7", new Vector3(-35.76f, dp.y + 0.2f, dp.z), 90f, 0.06f, new Color(0.85f, 0.85f, 0.8f), null, false);
            var drop = Prop(root, "DeadDrop", dp, MissionFlags.DeadDrop, "Take", "Petrov's envelope", 0f,
                "", Quaternion.Euler(0f, -90f, 0f), small: true);
            drop.requiredFlag = MissionFlags.TailDone;
            drop.requiredFlagReason = "Nothing here… yet.";
            drop.visibleWhenFlag = MissionFlags.TailDone;
            drop.hideWhenDone = true;
            var envelope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(envelope.GetComponent<Collider>());
            envelope.name = "Envelope";
            envelope.transform.SetParent(drop.transform, false);
            envelope.transform.position = new Vector3(-35.74f, dp.y - 0.05f, dp.z);
            envelope.transform.rotation = Quaternion.Euler(0f, 0f, 8f);
            envelope.transform.localScale = new Vector3(0.025f, 0.2f, 0.3f);
            envelope.GetComponent<Renderer>().sharedMaterial = M("PaintWhite");
            drop.revealObjects = new[] { envelope };

            var go = new GameObject("Tail_Petrov");
            go.transform.SetParent(root, false);
            go.transform.position = L.CafeSpot;
            var tail = go.AddComponent<TailTarget>();
            tail.targetName = "Dr. Hana Petrov";
            tail.route = (Vector3[])World.MissionLayout.TailRoute.Clone();
            tail.pauseSeconds = (float[])World.MissionLayout.TailPauses.Clone();
            tail.checkpointWaypoints = new[] { 1, 3 };
            tail.checkpointNames = new[] { "Market St bus stop", "storm-drain alley mouth" };
            tail.checkpointFlags = new[] { MissionFlags.TailLeg1, MissionFlags.TailLeg2 };
            tail.startName = "café terrace";
            tail.dropProp = drop;
            tail.dropYaw = -90f;
            tail.restPoint = World.MissionLayout.PetrovRest;
            tail.restYaw = 180f;
        }

        // ------------------------------------------------------------------ HUNTER PROTOCOL
        static void HunterMission(WorldKit k, Transform root)
        {
            var routes = new GameObject("HunterRoutes").transform;
            routes.SetParent(root, false);
            var corridor = PatrolPath(routes, "Route_HunterCorridor", 2f, World.MissionLayout.HunterCorridorRoute);
            var lobby = PatrolPath(routes, "Route_HunterLobby", 1.5f, World.MissionLayout.HunterLobbyRoute);
            var squadGo = new GameObject("HunterSquad");
            squadGo.transform.SetParent(root, false);
            var squad = squadGo.AddComponent<HunterSquad>();
            squad.hunterNames = new[] { "Hunter Kade", "Hunter Sol" };
            squad.hunterSeeds = new[] { 301, 302 };
            squad.hunterRoutes = new[] { corridor, lobby };
            squad.entryPoint = World.MissionLayout.HunterEntry;
            squad.exitPoint = World.MissionLayout.HunterExit;
            squad.officerName = "Officer Ines Duarte";
            squad.officerPost = World.MissionLayout.DutyOfficer;
            squad.officerYaw = World.MissionLayout.DutyOfficerYaw;
            squad.officerSeed = 311;

            // Bio-scan arch in the security office doorway (visual frame only — the opening stays fully walkable).
            var g = World.MissionLayout.ScanGate;
            foreach (float sx in new[] { -1.31f, 1.31f })
            {
                k.Box(M("MetalDark"), g + new Vector3(sx, 1.3f, 0f), new Vector3(0.12f, 2.6f, 0.42f));
                k.Box(M("WarningLight"), g + new Vector3(sx, 2.35f, 0.22f), new Vector3(0.1f, 0.12f, 0.03f));
            }
            k.Box(M("MetalDark"), g + new Vector3(0f, 2.66f, 0f), new Vector3(2.74f, 0.12f, 0.42f));
            k.Text("PROTOCOL HOUND — BIO-SCAN", g + new Vector3(0f, 2.66f, 0.22f), 180f, 0.07f, new Color(0.4f, 1f, 0.9f));
            var gateGo = new GameObject("ScanGate_SecurityOffice");
            gateGo.transform.SetParent(root, false);
            gateGo.transform.position = g;
            var gate = gateGo.AddComponent<ScanGate>();
            gate.gateName = "Security doorway bio-scanner";
            gate.size = new Vector3(2.5f, 3f, 1.4f);
            gate.requiredFlag = MissionFlags.Confronted;
            gate.retireFlag = MissionFlags.HuntersDown;
            gate.blobPassFlag = MissionFlags.HunterGate;
            var beams = new List<Renderer>();
            foreach (float y in new[] { 0.35f, 0.95f, 1.55f, 2.15f })
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(beam.GetComponent<Collider>());
                beam.name = "LaserBeam";
                beam.transform.SetParent(gateGo.transform, false);
                beam.transform.localPosition = new Vector3(0f, y, 0f);
                beam.transform.localScale = new Vector3(2.5f, 0.018f, 0.018f);
                var r = beam.GetComponent<Renderer>();
                r.sharedMaterial = M("NeonCyan");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beams.Add(r);
            }
            gate.beams = beams.ToArray();

            // Lockdown breaker on the security office's east wall; only a security badge releases it.
            var b = World.MissionLayout.HoundBreaker;
            k.Box(M("MetalDark"), new Vector3(23.38f, b.y, b.z), new Vector3(0.22f, 1.15f, 0.85f));
            k.Box(M("PaintYellow"), new Vector3(23.26f, b.y + 0.42f, b.z), new Vector3(0.02f, 0.12f, 0.75f));
            k.Box(M("PipeRed"), new Vector3(23.2f, b.y - 0.05f, b.z + 0.22f), new Vector3(0.12f, 0.42f, 0.07f));
            k.Text("PROTOCOL HOUND — LOCKDOWN", new Vector3(23.25f, b.y + 0.72f, b.z), 90f, 0.07f, new Color(1f, 0.85f, 0.3f));
            var breaker = Prop(root, "HoundBreaker", b, MissionFlags.HuntersDown, "Release lockdown", "Protocol Hound breaker", 4f,
                "Lockdown released. Protocol Hound scanners are going dark.", Quaternion.Euler(0f, 90f, 0f));
            breaker.requiredFlag = MissionFlags.Confronted;
            breaker.requiredFlagReason = "The lockdown panel is idle.";
            breaker.requiredPermission = AccessPermission.SecurityGate;
            breaker.permissionReason = "SECURITY BADGE REQUIRED — only a guard's badge can release Protocol Hound.";
        }

        static World.PatrolRoute PatrolPath(Transform parent, string name, float wait, Vector3[] points)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = points[0];
            var pr = t.gameObject.AddComponent<World.PatrolRoute>();
            pr.loop = true;
            pr.waitAtPoint = wait;
            for (int i = 0; i < points.Length; i++)
            {
                var wp = new GameObject("WP" + i).transform;
                wp.SetParent(t, false);
                wp.position = points[i];
            }
            return pr;
        }

        // ------------------------------------------------------------------ BLACKOUT
        static void BlackoutMission(WorldKit k, Transform root)
        {
            var gridGo = new GameObject("BlackoutGrid");
            gridGo.transform.SetParent(root, false);
            var grid = gridGo.AddComponent<BlackoutGrid>();
            var switches = new List<BlackoutSwitch>();
            foreach (var (name, pos, yaw) in World.MissionLayout.Transformers)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                var body = new Vector3(1.3f, 1.7f, 0.7f);
                k.Box(M("DumpsterGreen"), pos + q * new Vector3(0f, 0.85f, 0.15f), body, q);
                k.Solid(pos + q * new Vector3(0f, 0.85f, 0.15f), Flat(body, yaw));
                k.Box(M("MetalDark"), pos + q * new Vector3(0f, 1.74f, 0.15f), new Vector3(1.42f, 0.08f, 0.82f), q);
                k.Box(M("MetalDark"), pos + q * new Vector3(0f, 0.04f, 0.15f), new Vector3(1.4f, 0.08f, 0.8f), q);
                k.Box(M("PaintYellow"), pos + q * new Vector3(0.1f, 1.3f, -0.21f), new Vector3(0.62f, 0.3f, 0.02f), q);
                k.Text("DANGER 11 kV", pos + q * new Vector3(0.1f, 1.3f, -0.225f), yaw, 0.075f, new Color(0.8f, 0.1f, 0.05f), null, false);
                k.Text(name, pos + q * new Vector3(0f, 1.95f, -0.22f), yaw, 0.11f, new Color(1f, 0.85f, 0.35f));
                k.Box(M("PipeRed"), pos + q * new Vector3(0.48f, 0.95f, -0.25f), new Vector3(0.07f, 0.45f, 0.07f), q);
                k.Box(M("MetalDark"), pos + q * new Vector3(0.48f, 0.7f, -0.22f), new Vector3(0.16f, 0.1f, 0.06f), q);

                var go = new GameObject("Blackout_" + name.Replace(' ', '_'));
                go.transform.SetParent(gridGo.transform, false);
                go.transform.SetPositionAndRotation(pos, q);
                var sw = go.AddComponent<BlackoutSwitch>();
                sw.switchName = name;
                var ip = new GameObject("InteractPoint").transform;
                ip.SetParent(go.transform, false);
                ip.localPosition = new Vector3(0f, 1.1f, -0.55f);
                sw.interactPoint = ip;
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(lamp.GetComponent<Collider>());
                lamp.name = "StatusLamp";
                lamp.transform.SetParent(go.transform, false);
                lamp.transform.localPosition = new Vector3(-0.38f, 1.55f, -0.215f);
                lamp.transform.localScale = new Vector3(0.14f, 0.1f, 0.025f);
                lamp.GetComponent<Renderer>().sharedMaterial = M("NeonGreen");
                lamp.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sw.lamp = lamp.GetComponent<Renderer>();
                switches.Add(sw);
            }
            grid.switches = switches.ToArray();
            var extras = World.MissionLayout.BlackoutExtras;
            grid.extraNames = new string[extras.Length];
            grid.extraProfessions = new Profession[extras.Length];
            grid.extraPositions = new Vector3[extras.Length];
            grid.extraSeeds = new int[extras.Length];
            for (int i = 0; i < extras.Length; i++)
            {
                grid.extraNames[i] = extras[i].name;
                grid.extraProfessions[i] = extras[i].profession;
                grid.extraPositions[i] = extras[i].pos;
                grid.extraSeeds[i] = 401 + i;
            }
        }

        static Renderer CampaignTerminal(WorldKit k, Vector3 pos, string label)
        {
            k.Box(M("MetalDark"), pos + new Vector3(0, 0.58f, 0.6f), new Vector3(0.9f, 1.16f, 0.5f));
            k.Solid(pos + new Vector3(0, 0.58f, 0.6f), new Vector3(0.9f, 1.16f, 0.5f));
            k.Box(M("Screen"), pos + new Vector3(0, 1.4f, 0.53f), new Vector3(0.9f, 0.5f, 0.08f));
            k.Text(label, pos + new Vector3(0, 1.93f, 0.45f), 0f, 0.09f, new Color(0.65f, 1f, 0.75f));
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.name = "CampaignStatusLamp";
            lamp.transform.position = pos + new Vector3(0, 1.05f, 0.32f);
            lamp.transform.localScale = new Vector3(0.42f, 0.05f, 0.04f);
            lamp.GetComponent<Renderer>().sharedMaterial = M("NeonGreen");
            return lamp.GetComponent<Renderer>();
        }

        static CooperativeConsole Control(WorldKit k, Transform root, Vector3 pos, string label, string id, Profession profession, string required, string retire, string flag = null)
        {
            var go = new GameObject("Cooperative_" + id);
            go.transform.SetParent(root, false); go.transform.position = pos;
            var c = go.AddComponent<CooperativeConsole>();
            c.controlId = id; c.displayName = label; c.profession = profession; c.requiredFlag = required; c.retireWhenFlag = retire; c.flag = flag;
            c.lamp = CampaignTerminal(k, pos, label);
            c.lamp.transform.SetParent(go.transform, true);
            return c;
        }

        static CampaignOperation Operation(WorldKit k, Transform root, Vector3 pos, string label, string flag, string required, Profession profession, float seconds, string done)
        {
            var go = new GameObject("Campaign_" + flag);
            go.transform.SetParent(root, false); go.transform.position = pos;
            var op = go.AddComponent<CampaignOperation>();
            op.flag = flag; op.displayName = label; op.requiredFlag = required; op.profession = profession; op.seconds = seconds; op.doneToast = done;
            op.screen = CampaignTerminal(k, pos, label);
            op.screen.transform.SetParent(go.transform, true);
            return op;
        }

        static Material coneMat;

        static Material ConeMaterial()
        {
            if (coneMat) return coneMat;
            const string path = "Assets/Parasite/Materials/CameraCone.mat";
            coneMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!coneMat)
            {
                coneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(coneMat, path);
            }
            coneMat.SetFloat("_Surface", 1f); coneMat.SetFloat("_Blend", 0f);
            coneMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            coneMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            coneMat.SetFloat("_ZWrite", 0f); coneMat.SetFloat("_Cull", 0f);
            coneMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            coneMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            coneMat.SetOverrideTag("RenderType", "Transparent");
            coneMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f, 0.05f));
            EditorUtility.SetDirty(coneMat);
            return coneMat;
        }

        static Mesh ConeMesh(float length, float fov)
        {
            const string path = "Assets/Parasite/Materials/CameraCone.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            const int seg = 16;
            float r = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * length;
            var v = new Vector3[seg + 1]; var t = new int[seg * 3];
            v[0] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, length);
                t[i * 3] = 0; t[i * 3 + 1] = i + 1; t[i * 3 + 2] = (i + 1) % seg + 1;
            }
            var m = existing ? existing : new Mesh { name = "CameraCone" };
            m.Clear(); m.vertices = v; m.triangles = t; m.RecalculateNormals(); m.RecalculateBounds();
            if (!existing) AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Cctv(Transform parent, string name, Vector3 pos, float yaw, float range)
        {
            var go = new GameObject("CCTV_" + name.Replace(' ', '_'));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(arm.GetComponent<Collider>());
            arm.transform.SetParent(go.transform, false);
            arm.transform.localPosition = new Vector3(0, 0.12f, 0);
            arm.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
            arm.GetComponent<Renderer>().sharedMaterial = M("MetalDark");
            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localRotation = Quaternion.Euler(28f, yaw, 0f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(head, false);
            body.transform.localPosition = new Vector3(0, 0, 0.12f);
            body.transform.localScale = new Vector3(0.16f, 0.14f, 0.36f);
            body.GetComponent<Renderer>().sharedMaterial = M("PaintWhite");
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(head, false);
            lens.transform.localPosition = new Vector3(0, 0, 0.31f);
            lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
            lens.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);
            lens.GetComponent<Renderer>().sharedMaterial = M("GlassDark");
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(head, false);
            lamp.transform.localPosition = new Vector3(0.05f, 0.075f, 0.25f);
            lamp.transform.localScale = Vector3.one * 0.035f;
            lamp.GetComponent<Renderer>().sharedMaterial = M("NeonRed");
            var cone = new GameObject("ViewCone");
            cone.transform.SetParent(head, false);
            cone.transform.localPosition = new Vector3(0, 0, 0.32f);
            cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh(range * 0.75f, 40f);
            var cr = cone.AddComponent<MeshRenderer>();
            cr.sharedMaterial = ConeMaterial();
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = go.AddComponent<SecurityCamera>();
            c.cameraName = "CCTV " + name;
            c.head = head; c.lamp = lamp.GetComponent<Renderer>(); c.cone = cr;
            c.range = range; c.fov = 46f;
        }

        /// <summary>Pedestrians near the alley exit so the first possession is possible out of guards' sight.</summary>
        static void ExtraNpcs(Transform root)
        {
            var parent = new GameObject("MissionNPCs").transform;
            parent.SetParent(root, false);
            (string name, Vector3 pos, float yaw)[] npcs =
            {
                ("Leo Park", new Vector3(-31.6f, L.Sidewalk, -52f), 200f),
                ("Amara Diallo", new Vector3(-34.3f, L.Sidewalk, -60f), 20f),
            };
            int seed = 101;
            foreach (var (name, pos, yaw) in npcs)
            {
                var go = new GameObject("NpcSpawn_" + name.Replace(' ', '_'));
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
                var s = go.AddComponent<World.NpcSpawn>();
                s.displayName = name; s.profession = Profession.Civilian; s.behaviour = World.NpcBehaviour.Wander;
                s.wanderRadius = 3f; s.seed = seed++;
            }
        }

        static void WireEntrances(Transform gameplay)
        {
            foreach (var d in gameplay.GetComponentsInChildren<AccessDoor>(true))
            {
                switch (d.route)
                {
                    case EntryRoute.MainGate: d.openedFlag = MissionFlags.GateOpen; break;
                    case EntryRoute.StaffTerminal: d.openedFlag = MissionFlags.StaffOpen; break;
                    case EntryRoute.ServiceTunnel:
                        d.openedFlag = MissionFlags.TunnelOpen;
                        d.requiredFlag = MissionFlags.Power;
                        d.requiredFlagReason = "NO POWER — the tunnel door actuator is dead. Reset Substation 4 in the alley.";
                        break;
                }
            }
            foreach (var t in gameplay.GetComponentsInChildren<StaffTerminal>(true))
            {
                t.requiredFlag = MissionFlags.Keycard;
                t.requiredFlagReason = "CARD REQUIRED — insert a staff keycard. Dr. Petrov left hers at the café.";
            }
        }

        /// <summary>Axis-aligned collider size for a box rotated by a multiple of 90° in yaw.</summary>
        static Vector3 Flat(Vector3 size, float yaw) => Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.7f ? new Vector3(size.z, size.y, size.x) : size;

        static MissionProp Prop(Transform root, string name, Vector3 point, string flag, string verb, string display, float hold,
            string doneToast, Quaternion rot, bool small = false)
        {
            var go = new GameObject("Mission_" + name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(point, rot);
            var glow = GameObject.CreatePrimitive(small ? PrimitiveType.Cube : PrimitiveType.Sphere);
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.name = "Glow";
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = small ? new Vector3(0.09f, 0.006f, 0.055f) : Vector3.one * 0.12f;
            glow.GetComponent<Renderer>().sharedMaterial = M("NeonGreen");
            glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<MissionProp>();
            p.flag = flag; p.verb = verb; p.displayName = display; p.holdSeconds = hold; p.doneToast = doneToast;
            p.glow = glow.GetComponent<Renderer>();
            p.interactPoint = go.transform;
            return p;
        }

        static void DataTerminal(WorldKit k, Transform root, Vector3 pos, float yaw, string flag, string name)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            k.Box(M("LabCounter"), pos + q * new Vector3(0, 0.5f, 0), new Vector3(1.4f, 1f, 0.7f), q);
            k.Solid(pos + q * new Vector3(0, 0.5f, 0), Flat(new Vector3(1.4f, 1f, 0.7f), yaw));
            k.Box(M("Screen"), pos + q * new Vector3(0, 1.35f, 0.1f), new Vector3(1.1f, 0.65f, 0.05f), q);
            k.Text("SPECIMEN 7 — RESTRICTED", pos + q * new Vector3(0, 1.75f, 0.06f), yaw, 0.09f, new Color(0.5f, 1f, 0.6f));
            var p = Prop(root, name.Replace(' ', '_'), pos + q * new Vector3(0, 1.05f, -0.6f), flag, "Download files", name, 4f,
                $"{name}: Specimen 7 files copied.", q);
            p.requiredPermission = AccessPermission.LabInterior;
            p.permissionReason = "LOGIN FAILED — staff account required.";
        }

        static void Reader(Transform root, Vector3 pos, string name, AccessDoor door)
        {
            var go = new GameObject("DualAuth_" + name.Replace(' ', '_'));
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.22f, 0.32f, 0.06f);
            body.GetComponent<Renderer>().sharedMaterial = M("MetalDark");
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0, 0.1f, -0.035f);
            lamp.transform.localScale = new Vector3(0.12f, 0.04f, 0.01f);
            lamp.GetComponent<Renderer>().sharedMaterial = M("NeonRed");
            var r = go.AddComponent<DualAuthReader>();
            r.readerName = name;
            r.door = door;
            r.flag = MissionFlags.OfficeOpen;
            r.requiredFlag = MissionFlags.Research;
            r.requiredFlagReason = "Download the research from Lab A and Lab B first.";
            r.lamp = lamp.GetComponent<Renderer>();
        }
    }
}
