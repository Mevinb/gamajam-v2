using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Give the annex's primitive architecture the city's metre-based texture density, without cloning materials.</summary>
    public sealed class AfterlakeSurfaceUV : MonoBehaviour, System.IDisposable
    {
        readonly List<(MeshFilter filter, Mesh original, Mesh projected)> owned = new();
        bool applied;

        public void Apply()
        {
            if (applied) return;
            applied = true;
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                // Only Unity primitive cubes from AfterlakeDistrict.Box use normalized face UVs. Imported Blender
                // assemblies keep their authored layouts, including their intentionally packed material atlases.
                if (!source || source.name != "Cube" || !source.isReadable || source.subMeshCount != 1) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.sharedMaterial) continue;
                float metres = MetresPerTile(renderer.sharedMaterial.name);
                if (metres <= 0f) continue;
                var vertices = source.vertices;
                var normals = source.normals;
                if (normals.Length != vertices.Length) continue;
                var uv = new Vector2[vertices.Length];
                var world = filter.transform.localToWorldMatrix;
                var normalMatrix = world.inverse.transpose;
                for (int i = 0; i < vertices.Length; i++)
                    uv[i] = WorldUV(world.MultiplyPoint3x4(vertices[i]), normalMatrix.MultiplyVector(normals[i]).normalized, metres);
                var projected = Instantiate(source);
                projected.name = source.name + " (Riverside metre UV)";
                projected.uv = uv;
                projected.RecalculateTangents();
                filter.sharedMesh = projected;
                owned.Add((filter, source, projected));
            }
        }

        // These are the exact scales used for the reused surfaces in Editor/World/CityAssets.CreateMaterials (T calls).
        // Untextured metal/roof colours, screen materials and world text are intentionally excluded.
        static float MetresPerTile(string name) => name.Replace(" (Instance)", "") switch
        {
            "Sidewalk" => 3f,
            "BrickDark" => 3f,
            "TunnelConcrete" => 3f,
            "Wood" => 2f,
            "LabWhite" => 6f,
            _ => 0f,
        };

        static Vector2 WorldUV(Vector3 p, Vector3 n, float metres)
        {
            // Same orientation as WorldKit.WorldUV, keeping adjacent slabs and wall courses in phase.
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 uv = ax >= ay && ax >= az ? new Vector2(n.x > 0 ? -p.z : p.z, p.y)
                : ay >= az ? new Vector2(p.x, p.z)
                : new Vector2(n.z > 0 ? p.x : -p.x, p.y);
            return uv / metres;
        }

        /// <summary>Explicit cleanup for editor tools; runtime scene destruction calls the same idempotent path.</summary>
        public void Dispose()
        {
            foreach (var item in owned)
            {
                if (item.filter && item.filter.sharedMesh == item.projected) item.filter.sharedMesh = item.original;
                if (item.projected) { if (Application.isPlaying) Destroy(item.projected); else DestroyImmediate(item.projected); }
            }
            owned.Clear();
            applied = false;
        }

        void OnDestroy() => Dispose();
    }
}
