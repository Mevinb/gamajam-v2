using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>Runtime-owned paving and static batching; the staged chapter reuses the existing authored Harbor kit.</summary>
    public sealed class RailVisuals : MonoBehaviour
    {
        readonly List<Mesh> owned = new();
        public void Paving(HarborArt art, float x0, float x1, float z0, float z1)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            for (float x = x0; x < x1; x += 4) for (float z = z0; z < z1; z += 4)
            {
                int n = vertices.Count; float ex = Mathf.Min(x + 4, x1), ez = Mathf.Min(z + 4, z1);
                vertices.AddRange(new[] { new Vector3(x, .149f, z), new Vector3(x, .149f, ez), new Vector3(ex, .149f, ez), new Vector3(ex, .149f, z) });
                uv.AddRange(new[] { new Vector2(.008f, .008f), new Vector2(.008f, .492f), new Vector2(.492f, .492f), new Vector2(.492f, .008f) });
                indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            var mesh = new Mesh { name = "Northline concrete paving", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); owned.Add(mesh);
            var go = new GameObject(mesh.name); go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = art.Shared;
        }
        public GameObject BoardingRamp(HarborArt art)
        {
            // A real convex wedge rises from the .15m yard to the .75m coach deck.
            // Its upper edge stops5cm short of the moving vehicle envelope: a normal boarding gap, not intersecting geometry.
            const float x0 = 3f, x1 = 6.35f, z0 = 44f, z1 = 46f, bottom = .13f, low = .15f, high = RailLayout.CoachDeckY;
            Vector3[] corners = { new(x0,bottom,z0),new(x0,bottom,z1),new(x1,bottom,z1),new(x1,bottom,z0),
                new(x0,low,z0),new(x0,low,z1),new(x1,high,z1),new(x1,high,z0) };
            int[][] faces = { new[] {4,5,6,7},new[] {0,3,2,1},new[] {0,1,5,4},new[] {3,7,6,2},new[] {0,4,7,3},new[] {1,2,6,5} };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            foreach (var face in faces)
            {
                int n=vertices.Count; foreach(int index in face) vertices.Add(corners[index]);
                uv.AddRange(new[] {new Vector2(.508f,.008f),new Vector2(.508f,.492f),new Vector2(.992f,.492f),new Vector2(.992f,.008f)});
                triangles.AddRange(new[] {n,n+1,n+2,n,n+2,n+3});
            }
            var mesh=new Mesh {name="Northline actual boarding ramp"}; mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();owned.Add(mesh);
            var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=art.Shared;
            var physical=go.AddComponent<MeshCollider>();physical.sharedMesh=mesh;physical.convex=true;
            return go;
        }
        public void MergeStatic(HarborArt art, params Transform[] movingRoots)
        {
            // Imported Harbor meshes deliberately remain unreadable; retain those renderers and combine runtime-owned readable geometry only.
            var combines = new List<CombineInstance>(); var renderers = new List<MeshRenderer>();
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || renderer.sharedMaterial != art.Shared || !filter.sharedMesh || !filter.sharedMesh.isReadable) continue;
                bool moving = false; foreach (var root in movingRoots) if (root && filter.transform.IsChildOf(root)) { moving = true; break; }
                if (moving) continue;
                combines.Add(new CombineInstance { mesh = filter.sharedMesh, transform = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix }); renderers.Add(renderer);
            }
            if (combines.Count == 0) return;
            var mesh = new Mesh { name = "Northline shared static geometry", indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(combines.ToArray(), true, true); mesh.RecalculateBounds(); owned.Add(mesh);
            var batch = new GameObject(mesh.name); batch.transform.SetParent(transform, false); batch.AddComponent<MeshFilter>().sharedMesh = mesh; batch.AddComponent<MeshRenderer>().sharedMaterial = art.Shared;
            foreach (var renderer in renderers) renderer.enabled = false; // Specific copied static faces only; every collider and moving renderer remains.
        }
        void OnDestroy() { foreach (var mesh in owned) if (mesh) Destroy(mesh); }
    }
}
