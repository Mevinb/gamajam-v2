using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parasite.World;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Actual imported-data acceptance. This does not claim rendered appearance or native traffic behavior.</summary>
    public static class VehicleArtVerification
    {
        [Serializable] sealed class WindingRow
        {
            public string model, mesh, material, region; public int triangles;
            public float determinant, meanGeometricUp, meanImportedUp, meanNormalAgreement;
            public Vector3 firstCenter, firstGeometricNormal, firstImportedNormal;
        }
        [Serializable] sealed class WindingReport { public List<WindingRow> rows = new(); }

        public static void DumpWindingBatch()
        {
            var report = new WindingReport();
            void Read(GameObject instance, string name)
            {
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (name != "Cube" && !filter.name.Contains("_Body_LOD0")) continue;
                    var mesh = filter.sharedMesh; var renderer = filter.GetComponent<Renderer>();
                    using var acquired = MeshUtility.AcquireReadOnlyMeshData(mesh); var data = acquired[0];
                    using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp); data.GetVertices(vertices);
                    using var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp); data.GetNormals(normals);
                    var indices16 = mesh.indexFormat == IndexFormat.UInt16 ? data.GetIndexData<ushort>() : default;
                    var indices32 = mesh.indexFormat == IndexFormat.UInt32 ? data.GetIndexData<uint>() : default;
                    var matrix = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var normalMatrix = matrix.inverse.transpose;
                    for (int sub = 0; sub < data.subMeshCount; sub++)
                    {
                        var part = data.GetSubMesh(sub); var materials = renderer.sharedMaterials;
                        var rows = new Dictionary<string, WindingRow>();
                        for (int offset = part.indexStart; offset < part.indexStart + part.indexCount; offset += 3)
                        {
                            int At(int index) => (mesh.indexFormat == IndexFormat.UInt16 ? indices16[index] : (int)indices32[index]) + part.baseVertex;
                            int a = At(offset), b = At(offset + 1), c = At(offset + 2);
                            var p = matrix.MultiplyPoint3x4(vertices[a]); var q = matrix.MultiplyPoint3x4(vertices[b]); var r = matrix.MultiplyPoint3x4(vertices[c]);
                            var center = (p + q + r) / 3; var geometric = Vector3.Cross(q - p, r - p).normalized;
                            var imported = normalMatrix.MultiplyVector(normals[a] + normals[b] + normals[c]).normalized;
                            string region = name == "Cube" ? (center.y > .49f ? "cube top" : "cube other") :
                                center.y > 1.44f && Mathf.Abs(center.x) < .60f && center.z > -.73f && center.z < -.03f ? "sedan roof interior" :
                                center.y > .87f && center.y < 1.0f && Mathf.Abs(center.x) < .55f && center.z > .90f && center.z < 1.95f ? "sedan hood interior" : "other";
                            if (region == "other" || region == "cube other") continue;
                            if (!rows.TryGetValue(region, out var row))
                            {
                                row = new WindingRow { model = name, mesh = filter.name, material = sub < materials.Length && materials[sub] ? materials[sub].name : "NULL", region = region,
                                    determinant = matrix.determinant, firstCenter = center, firstGeometricNormal = geometric, firstImportedNormal = imported };
                                rows[region] = row;
                            }
                            row.triangles++; row.meanGeometricUp += geometric.y; row.meanImportedUp += imported.y; row.meanNormalAgreement += Vector3.Dot(geometric, imported);
                        }
                        foreach (var row in rows.Values)
                        {
                            row.meanGeometricUp /= row.triangles; row.meanImportedUp /= row.triangles; row.meanNormalAgreement /= row.triangles; report.rows.Add(row);
                        }
                    }
                }
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleArtImporter.Root + "VehicleSedan.prefab");
            var car = Object.Instantiate(source); var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try { Read(car, "VehicleSedan"); Read(cube, "Cube"); }
            finally { Object.DestroyImmediate(car); Object.DestroyImmediate(cube); }
            string path = "Logs/vehicle-winding-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + ".json";
            Directory.CreateDirectory("Logs"); File.WriteAllText(path, JsonUtility.ToJson(report, true)); Debug.Log("[VehicleArt] Winding data: " + path);
        }

        [Serializable] sealed class ModelResult
        { public string name; public int[] triangles; public int wheels, colliders, rigidbodies; public Vector3 min, max; }
        [Serializable] sealed class Report
        { public string status = "FAIL", utc, error; public List<ModelResult> models = new(); public int textures; public string scope = "Unity imported mesh/material/texture/pivot/LOD data only; GPU appearance, FPS and native behavior remain separate."; }

        public static void VerifyBatch()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            try
            {
                foreach (VehicleBody body in Enum.GetValues(typeof(VehicleBody)))
                {
                    string name = "Vehicle" + body;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleArtImporter.Root + name + ".prefab");
                    if (!prefab) throw new InvalidOperationException("Missing visual prefab: " + name);
                    var instance = Object.Instantiate(prefab);
                    try
                    {
                        var group = instance.GetComponent<LODGroup>();
                        if (!group || group.lodCount != 3) throw new InvalidOperationException("Three authored vehicle LODs are required.");
                        var colliders = instance.GetComponentsInChildren<Collider>(true); var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                        if (colliders.Length != 0 || rigidbodies.Length != 0) throw new InvalidOperationException("Imported visuals changed physics.");
                        var pivots = instance.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("WheelPivot_", StringComparison.Ordinal)).OrderBy(t => t.name).ToArray();
                        var centres = VehicleVisuals.WheelCentres(body);
                        if (pivots.Length != centres.Length) throw new InvalidOperationException("Authored wheel count differs.");
                        for (int i = 0; i < pivots.Length; i++)
                        {
                            if (Vector3.Distance(pivots[i].localPosition, centres[i]) > .0001f || Quaternion.Angle(pivots[i].localRotation, Quaternion.identity) > .01f || pivots[i].childCount != 3)
                                throw new InvalidOperationException("Wheel pivots must rotate around car-local X at their authored axle centres.");
                            var wheel = pivots[i].GetComponentsInChildren<Renderer>(true).Single(r => r.name.Contains("_LOD0"));
                            var localCentre = instance.transform.InverseTransformPoint(wheel.bounds.center);
                            if (Mathf.Abs(localCentre.y - centres[i].y) > .003f || Mathf.Abs(localCentre.z - centres[i].z) > .003f)
                                throw new InvalidOperationException("Imported wheel geometry is not centred on its spin axle.");
                        }
                        int[] triangles = new int[3]; Bounds bounds = default; bool first = true;
                        var lods = group.GetLODs();
                        for (int lod = 0; lod < 3; lod++)
                        {
                            if (lods[lod].renderers.Length != centres.Length + 2) throw new InvalidOperationException("Incomplete authored vehicle LOD.");
                            foreach (var renderer in lods[lod].renderers)
                            {
                                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                                if (!mesh || mesh.isReadable) throw new InvalidOperationException("Imported vehicle mesh must be valid and CPU-unreadable.");
                                for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles[lod] += (int)mesh.GetIndexCount(sub) / 3;
                                foreach (var material in renderer.sharedMaterials)
                                    if (!material || !material.shader || !material.enableInstancing || !AssetDatabase.GetAssetPath(material).StartsWith(VehicleArtImporter.Root + "Surfaces/", StringComparison.Ordinal))
                                        throw new InvalidOperationException("Shared vehicle surface mapping/instancing differs.");
                                if (lod == 0) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                            }
                        }
                        if (!(triangles[0] > triangles[1] && triangles[1] > triangles[2] && triangles[2] < 5500)) throw new InvalidOperationException("Vehicle distance LOD budget differs.");
                        var paint = AssetDatabase.LoadAssetAtPath<Material>(VehicleArtImporter.Root + "Surfaces/VehiclePaint.mat");
                        if (paint.shader.name != "Universal Render Pipeline/Complex Lit" || !paint.IsKeywordEnabled("_CLEARCOAT")) throw new InvalidOperationException("Layered clear-coat paint is required.");
                        foreach (string dielectric in new[] { "VehicleRubber", "VehicleTrim", "VehicleInterior", "VehiclePlate" })
                        {
                            var surface = AssetDatabase.LoadAssetAtPath<Material>(VehicleArtImporter.Root + "Surfaces/" + dielectric + ".mat");
                            if (!surface || surface.GetFloat("_Metallic") != 0 || surface.IsKeywordEnabled("_METALLICSPECGLOSSMAP"))
                                throw new InvalidOperationException("Non-metal vehicle surfaces must retain dielectric response: " + dielectric);
                        }
                        var glass = AssetDatabase.LoadAssetAtPath<Material>(VehicleArtImporter.Root + "Surfaces/VehicleGlass.mat");
                        if (glass.GetFloat("_Surface") != 1 || glass.GetFloat("_ZWrite") != 0 || glass.GetFloat("_Cull") != 2 || glass.GetShaderPassEnabled("ShadowCaster")) throw new InvalidOperationException("Thin glass surface/depth/culling differs.");
                        report.models.Add(new ModelResult { name = name, triangles = triangles, wheels = centres.Length, colliders = colliders.Length, rigidbodies = rigidbodies.Length, min = bounds.min, max = bounds.max });
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                foreach (string suffix in new[] { "A", "N", "M" })
                {
                    string path = VehicleArtImporter.ArtRoot + "Textures/VehicleDetail_" + suffix + ".png";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (!texture || texture.width != 2048 || texture.height != 2048 || texture.mipmapCount < 2 || texture.isReadable || !texture.streamingMipmaps
                        || texture.format != (suffix == "N" ? TextureFormat.BC5 : TextureFormat.BC7) || importer.sRGBTexture != (suffix == "A") || importer.wrapMode != TextureWrapMode.Repeat)
                        throw new InvalidOperationException("Shared vehicle map size/mips/format/colour-space/streaming differs: " + path);
                    report.textures++;
                }
                report.status = "IMPORTED_DATA_PASS";
            }
            catch (Exception error) { report.error = error.ToString(); }
            string pathOut = "Logs/vehicle-import-verification-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + ".json";
            Directory.CreateDirectory("Logs"); File.WriteAllText(pathOut, JsonUtility.ToJson(report, true));
            if (report.status != "IMPORTED_DATA_PASS") throw new InvalidOperationException(report.error);
            Debug.Log("[VehicleArt] IMPORTED_DATA_PASS: five bodies, three LODs, shared2K PBR, transparent glazing and axle-centred rotating wheel pivots. " + pathOut);
        }
    }
}
