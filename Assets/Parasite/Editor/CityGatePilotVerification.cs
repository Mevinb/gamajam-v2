using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Parasite.Missions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Actual import and pre/post serialized physics gates. No scene generation or runtime pass is inferred.</summary>
    public static class CityGatePilotVerification
    {
        [Serializable] sealed class ColliderRow
        {
            public string path, type, mesh, physicsMaterial; public int layer, staticFlags, direction;
            public bool enabled, trigger, convex; public float radius, height, contactOffset;
            public Vector3 localPosition, localScale, position, boundsCenter, boundsSize, center, size; public Quaternion localRotation, rotation;
        }
        [Serializable] sealed class ObstacleRow
        {
            public string path, shape; public bool enabled, carving, carveOnlyStationary; public float radius, height, moveThreshold, stationaryTime;
            public Vector3 position, localScale, center, size; public Quaternion rotation;
        }
        [Serializable] sealed class DoorRow
        {
            public string path, panelPath, interactPath, doorName, permission, route, requiredFlag, openedFlag;
            public bool terminalControlled, startsOpen; public float seconds, range;
            public Vector3 rootPosition, panelPosition, panelScale, offset, openEuler, interactLocal; public Quaternion rootRotation, panelRotation;
            public List<string> warningDecorations = new();
        }
        [Serializable] sealed class LightRow { public string path, type, shadows; public bool enabled; public Vector3 position; public Quaternion rotation; public Color color; public float intensity, range, spotAngle; }
        [Serializable] sealed class Inventory { public List<ColliderRow> colliders = new(); public List<ObstacleRow> obstacles = new(); public List<DoorRow> doors = new(); public List<LightRow> lights = new(); }
        [Serializable] sealed class Report
        {
            public string status, utc, unityVersion, target, device, sceneSha256, baselineSha256, inventorySha256, baselineInventorySha256, exception;
            public List<string> checks = new(); public List<string> failures = new(); public Inventory inventory;
            public List<TextureRow> textures = new(); public string editorAssemblySha256, modelSha256, materialSha256;
            public string scope = "Actual Unity invocation only. Serialized inventory equality does not replace PlayMode motion/access/LOS/navigation or native visual/performance checks.";
        }
        [Serializable] sealed class TextureRow { public string path, sha256, shape, type, graphicsFormat, format; public int width, height, mips, maxSize; public bool srgb; public long objectMemoryBytes; }
        static string Project => Directory.GetParent(Application.dataPath).FullName;
        static string Baseline => Argument("-gatePilotBaseline", "Logs/city-gate-pilot/baseline-physics.json");
        static string Argument(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, key); string path = at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
            string full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Project, path));
            if (!full.StartsWith(Path.Combine(Project, "Logs") + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new ArgumentException("Gate verification outputs/inputs must be under project Logs.");
            return full;
        }
        static Report NewReport() => new() { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, target = EditorUserBuildSettings.activeBuildTarget.ToString(), device = SystemInfo.graphicsDeviceType.ToString(), editorAssemblySha256 = Sha(typeof(CityGatePilotVerification).Assembly.Location) };
        static void Require(Report report, bool condition, string label) { report.checks.Add(label); if (!condition) report.failures.Add(label); }
        static void Finish(Report report, string path, bool baseline = false)
        {
            report.status = report.exception == null && report.failures.Count == 0 ? baseline ? "BASELINE_RECORDED" : "PASS" : "FAIL";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
            Debug.Log("[GatePilotVerification] " + report.status + "; " + report.checks.Count + " checks; " + path);
            EditorApplication.Exit(report.status == "PASS" || report.status == "BASELINE_RECORDED" ? 0 : 1);
        }
        public static void CaptureBaselineBatch()
        {
            var report = NewReport(); string path = Baseline;
            if (File.Exists(path))
            {
                Debug.LogError("[GatePilotVerification] Refusing to overwrite an existing baseline: " + path);
                EditorApplication.Exit(1); return;
            }
            try
            {
                if (Application.isPlaying) throw new InvalidOperationException("Capture the saved Editor scene outside PlayMode.");
                report.inventory = Capture(); report.inventorySha256 = ShaText(JsonUtility.ToJson(report.inventory)); report.sceneSha256 = Sha(CityBuilder.CityPath);
                Require(report, report.inventory.colliders.Count > 0 && report.inventory.doors.Any(d => d.doorName == "Main Gate"), "Original generated scene contains physics and the main gate");
                Require(report, !Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(r => r.gameObject.scene.path == CityBuilder.CityPath && r.sharedMaterial && r.sharedMaterial.name == "CityGatePilot_SharedPBR"), "Baseline scene precedes pilot regeneration");
                // This reads the CURRENT saved scene before regeneration; it never saves or calls a builder.
            }
            catch (Exception e) { report.exception = e.ToString(); }
            Finish(report, path, true);
        }
        public static void ImportAndVerifyBatch()
        {
            var report = NewReport(); string output = Argument("-gatePilotReport", "Logs/city-gate-pilot/unity-import.json");
            try
            {
                Require(report, !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Importer compilation completed before invocation");
                Require(report, EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64, "Standalone Linux import target");
                if (report.failures.Count > 0) throw new InvalidOperationException("Import preconditions failed.");
                var oldMaps = OldMapHashes();
                foreach (string suffix in new[] { "A", "N", "M" }) AssetDatabase.ImportAsset(CityGatePilotArt.Root + "Textures/CityGatePilot_" + suffix + ".png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(CityGatePilotArt.ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                CityGatePilotArt.PrepareMaterial();
                foreach (string suffix in new[] { "A", "N", "M" }) CheckTexture(report, suffix);
                Require(report, OldMapHashes().SequenceEqual(oldMaps), "All 18 existing island texture importer/meta hashes unchanged");
                Require(report, CityGatePilotArt.TryLoad(out var kit, out string reason), "Complete paired kit readiness: " + (reason ?? "valid"));
                if (kit != null)
                {
                    Require(report, kit.staticMesh.GetIndexCount(0) / 3 == 2616 && kit.movingMesh.GetIndexCount(0) / 3 == 1192, "Exact 2616 static /1192 moving triangles");
                    Require(report, kit.material && kit.material.GetTexture("_BumpMap") && kit.material.GetTexture("_MetallicGlossMap"), "One explicit shared PBR material with real maps");
                }
                report.modelSha256 = Sha(CityGatePilotArt.ModelPath); report.materialSha256 = Sha(CityGatePilotArt.MaterialPath);
            }
            catch (Exception e) { report.exception = e.ToString(); }
            Finish(report, output);
        }
        public static void VerifyRegeneratedBatch()
        {
            var report = NewReport(); string output = Argument("-gatePilotReport", "Logs/city-gate-pilot/regenerated-physics.json");
            try
            {
                string baseline = Baseline; var old = JsonUtility.FromJson<Report>(File.ReadAllText(baseline)); report.baselineSha256 = Sha(baseline);
                Require(report, old != null && old.status == "BASELINE_RECORDED" && old.inventory != null, "Actual baseline was captured before generation");
                if (old == null || old.inventory == null) throw new InvalidDataException("Missing baseline inventory.");
                report.inventory = Capture(); report.inventorySha256 = ShaText(JsonUtility.ToJson(report.inventory)); report.baselineInventorySha256 = old.inventorySha256;
                report.sceneSha256 = Sha(CityBuilder.CityPath);
                Require(report, report.inventorySha256 == old.inventorySha256, "Every serialized collider/obstacle/door/light and warning-decoration contract matches baseline exactly");
                var gate = Object.FindObjectsByType<AccessDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(d => d.gameObject.scene.path == CityBuilder.CityPath && d.doorName == "Main Gate");
                Require(report, gate.panel.GetComponent<BoxCollider>() && !gate.panel.GetComponent<MeshRenderer>().enabled, "Original panel BoxCollider retained; only old panel renderer suppressed");
                var moving = gate.panel.Find("CityGatePilot_Moving");
                Require(report, moving && moving.GetComponentsInChildren<Collider>(true).Length == 0 && moving.GetComponentsInChildren<MeshRenderer>(true).Length == 1, "One moving visual and no new collider");
                Require(report, moving && Vector3.Distance(Vector3.Scale(gate.panel.localScale, moving.localScale), Vector3.one) < .0001f, "Inverse child scale retains metre geometry below the original parent");
                var visuals = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.scene.path == CityBuilder.CityPath && r.sharedMaterial && r.sharedMaterial.name == "CityGatePilot_SharedPBR").ToArray();
                Require(report, visuals.Length == 2, "Exactly one merged static pilot renderer and one moving renderer");
                Require(report, CityGatePilotArt.TryLoad(out _, out _), "Imported paired kit still valid after generation");
            }
            catch (Exception e) { report.exception = e.ToString(); }
            Finish(report, output);
        }
        static Inventory Capture()
        {
            EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single); Physics.SyncTransforms();
            var inventory = new Inventory();
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.gameObject.scene.path == CityBuilder.CityPath))
            {
                var row = new ColliderRow { path = PathOf(c.transform), type = c.GetType().FullName, layer = c.gameObject.layer, staticFlags = (int)GameObjectUtility.GetStaticEditorFlags(c.gameObject), enabled = c.enabled, trigger = c.isTrigger,
                    contactOffset = c.contactOffset, physicsMaterial = c.sharedMaterial ? c.sharedMaterial.name : "", localPosition = c.transform.localPosition, localRotation = c.transform.localRotation, localScale = c.transform.localScale,
                    position = c.transform.position, rotation = c.transform.rotation, boundsCenter = c.bounds.center, boundsSize = c.bounds.size };
                if (c is BoxCollider box) { row.center = box.center; row.size = box.size; }
                else if (c is SphereCollider sphere) { row.center = sphere.center; row.radius = sphere.radius; }
                else if (c is CapsuleCollider capsule) { row.center = capsule.center; row.radius = capsule.radius; row.height = capsule.height; row.direction = capsule.direction; }
                else if (c is MeshCollider mesh) { row.mesh = mesh.sharedMesh ? AssetDatabase.GetAssetPath(mesh.sharedMesh) + "#" + mesh.sharedMesh.name : ""; row.convex = mesh.convex; }
                inventory.colliders.Add(row);
            }
            foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(o => o.gameObject.scene.path == CityBuilder.CityPath))
                inventory.obstacles.Add(new() { path = PathOf(o.transform), shape = o.shape.ToString(), enabled = o.enabled, carving = o.carving, carveOnlyStationary = o.carveOnlyStationary, radius = o.radius, height = o.height, moveThreshold = o.carvingMoveThreshold, stationaryTime = o.carvingTimeToStationary, position = o.transform.position, rotation = o.transform.rotation, localScale = o.transform.localScale, center = o.center, size = o.size });
            foreach (var d in Object.FindObjectsByType<AccessDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(d => d.gameObject.scene.path == CityBuilder.CityPath))
            {
                var row = new DoorRow { path = PathOf(d.transform), panelPath = d.panel ? PathOf(d.panel) : "", interactPath = d.interactPoint ? PathOf(d.interactPoint) : "", doorName = d.doorName, permission = d.required.ToString(), route = d.route.ToString(), requiredFlag = d.requiredFlag, openedFlag = d.openedFlag, terminalControlled = d.terminalControlled, startsOpen = d.startsOpen, seconds = d.openSeconds, range = d.range,
                    rootPosition = d.transform.position, rootRotation = d.transform.rotation, panelPosition = d.panel ? d.panel.localPosition : Vector3.zero, panelRotation = d.panel ? d.panel.localRotation : Quaternion.identity, panelScale = d.panel ? d.panel.localScale : Vector3.one, offset = d.openLocalOffset, openEuler = d.openLocalEuler, interactLocal = d.interactPoint ? d.interactPoint.localPosition : Vector3.zero };
                if (d.panel) foreach (var r in d.panel.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterials.Any(m => m && m.name.Contains("WarningLight")))) row.warningDecorations.Add(PathOf(r.transform) + "|" + JsonUtility.ToJson(new PoseRow { position = r.transform.localPosition, rotation = r.transform.localRotation, scale = r.transform.localScale }) + "|" + r.enabled);
                row.warningDecorations.Sort(StringComparer.Ordinal); inventory.doors.Add(row);
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.gameObject.scene.path == CityBuilder.CityPath)) inventory.lights.Add(new() { path = PathOf(l.transform), type = l.type.ToString(), shadows = l.shadows.ToString(), enabled = l.enabled, position = l.transform.position, rotation = l.transform.rotation, color = l.color, intensity = l.intensity, range = l.range, spotAngle = l.spotAngle });
            inventory.colliders = inventory.colliders.OrderBy(r => r.path, StringComparer.Ordinal).ThenBy(r => r.type, StringComparer.Ordinal).ToList(); inventory.obstacles = inventory.obstacles.OrderBy(r => r.path, StringComparer.Ordinal).ToList(); inventory.doors = inventory.doors.OrderBy(r => r.path, StringComparer.Ordinal).ToList(); inventory.lights = inventory.lights.OrderBy(r => r.path, StringComparer.Ordinal).ToList();
            return inventory;
        }
        [Serializable] sealed class PoseRow { public Vector3 position, scale; public Quaternion rotation; }
        static void CheckTexture(Report report, string suffix)
        {
            string path = CityGatePilotArt.Root + "Textures/CityGatePilot_" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter; var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(report, importer && texture, path + " actual TextureImporter/Texture2D"); if (!importer || !texture) return;
            report.textures.Add(new() { path = path, sha256 = Sha(path), width = texture.width, height = texture.height, mips = texture.mipmapCount, maxSize = importer.maxTextureSize, shape = importer.textureShape.ToString(), type = importer.textureType.ToString(), srgb = importer.sRGBTexture, graphicsFormat = texture.graphicsFormat.ToString(), format = texture.format.ToString(), objectMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture) });
            var platform = importer.GetPlatformTextureSettings("Standalone");
            Require(report, importer.textureShape == TextureImporterShape.Texture2D && texture.width == 2048 && texture.height == 2048 && importer.maxTextureSize == 2048, path + " actual2K/2D");
            Require(report, importer.textureType == (suffix == "N" ? TextureImporterType.NormalMap : TextureImporterType.Default) && importer.sRGBTexture == (suffix == "A"), path + " correct PBR type/color space");
            Require(report, platform.overridden && platform.format == TextureImporterFormat.BC7 && texture.format == TextureFormat.BC7 && GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat), path + " actual standalone BC7");
            Require(report, importer.mipmapEnabled && texture.mipmapCount > 1, path + " actual mip chain");
        }
        static string[] OldMapHashes() => new[] { "IslandClinic", "IslandConcrete", "IslandFabric", "IslandMetal", "IslandOxide", "IslandTimber" }.SelectMany(surface => new[] { "A", "N", "M" }.Select(suffix =>
        {
            string path = "Assets/Parasite/Resources/Island/Textures/" + surface + "_" + suffix + ".png.meta";
            return path + "=" + Sha(path);
        })).ToArray();
        static string PathOf(Transform node) => node.parent ? PathOf(node.parent) + "/" + node.name : node.name;
        static string ShaText(string text) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        static string Sha(string path) { if (!Path.IsPathRooted(path)) path = Path.Combine(Project, path); using var hash = SHA256.Create(); using var file = File.OpenRead(path); return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }
    }
}
