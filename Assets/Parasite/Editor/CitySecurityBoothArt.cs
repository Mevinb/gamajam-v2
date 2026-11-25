using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>Visual booth kit; the builder always retains the original physics and interaction objects.</summary>
    internal static class CitySecurityBoothArt
    {
        internal const string ModelPath = "Assets/Parasite/Art/CitySecurityBooth/CitySecurityBooth.fbx";
        internal static readonly Vector3 Origin = new(4.25f, 0, 55.1f);

        internal sealed class Kit
        {
            internal Mesh mesh;
            internal Matrix4x4 matrix;
            internal Material material;
            internal Bounds bounds;
        }

        internal static bool TryLoad(out Kit kit, out string reason)
        {
            kit = null;
            reason = null;
            if (!CityGatePilotArt.TryLoad(out var gate, out reason)) return false;
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!importer || !model) return Fail("The booth export has not been imported.", out reason);
            if (importer.addCollider || importer.importAnimation || importer.importCameras || importer.importLights ||
                !importer.isReadable || !importer.bakeAxisConversion || !importer.useFileScale || !importer.preserveHierarchy ||
                Mathf.Abs(importer.globalScale - 1) > .00001f)
                return Fail("The visual-only metre-scale import contract is invalid.", out reason);
            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                model.GetComponentsInChildren<Animation>(true).Length != 0 ||
                model.GetComponentsInChildren<Animator>(true).Length != 0)
                return Fail("The visual booth must not contain physics or animation.", out reason);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1 || !filters[0].name.StartsWith("SecurityBoothShell", StringComparison.Ordinal))
                return Fail("The export must have one semantic SecurityBoothShell mesh.", out reason);
            var mesh = filters[0].sharedMesh;
            if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.vertexCount == 0 ||
                mesh.triangles.Length == 0 || mesh.triangles.Length > 7000 * 3)
                return Fail("The booth mesh is missing, unreadable or outside its geometry budget.", out reason);
            var points = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            if (normals.Length != points.Length || uv.Length != points.Length ||
                points.Any(v => !Finite(v.x) || !Finite(v.y) || !Finite(v.z)) ||
                normals.Any(v => !Finite(v.x) || !Finite(v.y) || !Finite(v.z)) ||
                uv.Any(v => !Finite(v.x) || !Finite(v.y) || v.x < 0 || v.x > 1 || v.y < 0 || v.y > 1))
                return Fail("The imported positions, normals or atlas UVs are invalid.", out reason);
            var matrix = model.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(points[0]), Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(matrix.MultiplyPoint3x4(point));
            if (Vector3.Distance(bounds.min, new Vector3(-1.55f, 0, -1.8f)) > .001f ||
                Vector3.Distance(bounds.max, new Vector3(1.55f, 3.15f, 1.8f)) > .001f)
                return Fail("The booth no longer matches the original roof envelope.", out reason);
            kit = new Kit { mesh = mesh, matrix = matrix, material = gate.material, bounds = bounds };
            return true;
        }

        internal static bool TryAdd(WorldKit world)
        {
            if (!TryLoad(out var kit, out string reason))
            {
                Debug.LogWarning("[SecurityBooth] Retaining original booth visuals: " + reason);
                return false;
            }
            world.Mesh(kit.material, kit.mesh, Matrix4x4.TRS(Origin, Quaternion.identity, Vector3.one) * kit.matrix);
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Fail(string value, out string reason) { reason = value; return false; }
    }
}
