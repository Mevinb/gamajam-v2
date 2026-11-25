using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Rendering-only holes in the known distant ground slab. No scene asset or collider is changed.</summary>
    public sealed class SkylineWaterCutout : MonoBehaviour, IDisposable
    {
        public const string SourcePath = "Skyline/Geometry/Skyline_Ground_0_0";
        static readonly Dictionary<int, SkylineWaterCutout> owners = new();
        MeshRenderer original, replacement;
        Mesh ownedMesh;
        bool originalEnabled;
        int sourceId;
        Rect[] appliedCuts;
        public bool Applied { get; private set; }
        public string LastFailure { get; private set; }
        public MeshRenderer Replacement => replacement;

        public bool ApplyToSkyline(IReadOnlyList<Bounds> water)
        {
            var source = GameObject.Find(SourcePath);
            return Apply(source ? source.GetComponent<MeshRenderer>() : null, water);
        }

        public bool Apply(MeshRenderer source, IReadOnlyList<Bounds> water)
        {
            LastFailure = null;
            if (!source) return Fail("Expected distant ground renderer is missing.");
            var filter = source.GetComponent<MeshFilter>();
            if (!filter || !ValidateSource(source, filter.sharedMesh)) return false;
            if (water == null || water.Count == 0) return Fail("No actual water surface bounds supplied.");
            var cuts = new Rect[water.Count];
            for (int i = 0; i < cuts.Length; i++)
            {
                var bounds = water[i];
                if (bounds.size.x <= 0 || bounds.size.z <= 0 || !Finite(bounds.min) || !Finite(bounds.max)) return Fail("Invalid actual water bounds.");
                cuts[i] = new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
            }
            if (Applied)
            {
                if (source != original || cuts.Length != appliedCuts.Length) return Fail("Dispose the previous cut before changing source/footprints.");
                for (int i = 0; i < cuts.Length; i++) if (cuts[i] != appliedCuts[i]) return Fail("Water footprints changed; dispose before rebuilding.");
                return true;
            }
            int id = source.GetInstanceID();
            if (owners.TryGetValue(id, out var owner) && owner && owner != this && owner.Applied) return Fail("Another rendering owner already cuts this slab.");
            var boundsSource = filter.sharedMesh.bounds;
            var pieces = new List<Rect> { new(boundsSource.min.x, boundsSource.min.z, boundsSource.size.x, boundsSource.size.z) };
            foreach (var cut in cuts)
            {
                var next = new List<Rect>();
                foreach (var piece in pieces) Subtract(piece, cut, next);
                pieces = next;
            }
            if (pieces.Count == 0) return Fail("Refusing to remove the complete skyline slab.");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            foreach (var piece in pieces) AddBox(piece, boundsSource.min.y, boundsSource.max.y, vertices, normals, uv, triangles);
            ownedMesh = new Mesh { name = "Distant ground with actual river/berth openings" };
            ownedMesh.SetVertices(vertices); ownedMesh.SetNormals(normals); ownedMesh.SetUVs(0, uv); ownedMesh.SetTriangles(triangles, 0);
            ownedMesh.RecalculateBounds(); ownedMesh.RecalculateTangents();
            var go = new GameObject("Skyline ground - river rendering cutout") { layer = source.gameObject.layer };
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition; go.transform.localRotation = source.transform.localRotation; go.transform.localScale = source.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
            replacement = go.AddComponent<MeshRenderer>();
            replacement.sharedMaterials = source.sharedMaterials;
            replacement.shadowCastingMode = source.shadowCastingMode; replacement.receiveShadows = source.receiveShadows;
            replacement.lightProbeUsage = source.lightProbeUsage; replacement.reflectionProbeUsage = source.reflectionProbeUsage;
            replacement.probeAnchor = source.probeAnchor; replacement.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
            replacement.motionVectorGenerationMode = source.motionVectorGenerationMode; replacement.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            replacement.renderingLayerMask = source.renderingLayerMask; replacement.sortingLayerID = source.sortingLayerID; replacement.sortingOrder = source.sortingOrder;
            // Unity exposes the authoring GI mode only in Editor builds. Runtime probe/lightmap flags are copied below.
#if UNITY_EDITOR
            replacement.receiveGI = source.receiveGI;
#endif
            replacement.lightmapIndex = source.lightmapIndex; replacement.lightmapScaleOffset = source.lightmapScaleOffset;
            replacement.realtimeLightmapIndex = source.realtimeLightmapIndex; replacement.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
            original = source; sourceId = id; originalEnabled = source.enabled; appliedCuts = cuts;
            replacement.enabled = originalEnabled; original.enabled = false;
            owners[id] = this; Applied = true;
            return true;
        }

        bool ValidateSource(MeshRenderer source, Mesh mesh)
        {
            if (source.name != "Skyline_Ground_0_0" || source.GetComponent<Collider>() || source.sharedMaterials.Length != 1 ||
                !source.sharedMaterial || source.sharedMaterial.name.Replace(" (Instance)", "") != "Ground") return Fail("Unexpected skyline source identity/material/collision.");
            for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++)
                if (Mathf.Abs(source.transform.localToWorldMatrix[row, col] - Matrix4x4.identity[row, col]) > 0.00001f) return Fail("Expected identity world transform.");
            if (source.additionalVertexStreams) return Fail("Unexpected additional source vertex stream.");
            if (!mesh || !mesh.isReadable || mesh.vertexCount != 24 || mesh.subMeshCount != 1 || mesh.GetIndexCount(0) != 36)
                return Fail("Expected readable original 24-vertex/12-triangle WorldKit ground box.");
            var b = mesh.bounds;
            if (!Finite(b.min) || !Finite(b.max)) return Fail("Nonfinite source bounds.");
            if ((b.center - new Vector3(0, -.06f, 0)).sqrMagnitude > .000001f || (b.size - new Vector3(1000, .1f, 1000)).sqrMagnitude > .000001f)
                return Fail("Unexpected skyline ground bounds.");
            var vertices = mesh.vertices; var normals = mesh.normals; var tex = mesh.uv;
            if (normals.Length != 24 || tex.Length != 24) return Fail("Missing source normals/UVs.");
            for (int i = 0; i < 24; i++)
            {
                if (!Finite(vertices[i]) || !Finite(normals[i])) return Fail("Nonfinite source vertices/normals.");
                for (int axis = 0; axis < 3; axis++) if (Mathf.Min(Mathf.Abs(vertices[i][axis] - b.min[axis]), Mathf.Abs(vertices[i][axis] - b.max[axis])) > .0001f)
                    return Fail("Source contains non-box vertices.");
                var n = normals[i]; float axisSum = Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z);
                if (Mathf.Abs(n.sqrMagnitude - 1) > .0001f || Mathf.Abs(axisSum - 1) > .0001f || (WorldUV(vertices[i], n) - tex[i]).sqrMagnitude > .000001f)
                    return Fail("Source UV phase or face normals differ from WorldKit's two-metre box.");
            }
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], c = indices[i + 1], d = indices[i + 2];
                if (normals[a] != normals[c] || normals[a] != normals[d] || Vector3.Dot(Vector3.Cross(vertices[c] - vertices[a], vertices[d] - vertices[a]), normals[a]) <= 0)
                    return Fail("Unexpected source face topology/winding.");
            }
            return true;
        }

        static void Subtract(Rect source, Rect cut, List<Rect> output)
        {
            float x0 = Mathf.Max(source.xMin, cut.xMin), x1 = Mathf.Min(source.xMax, cut.xMax);
            float z0 = Mathf.Max(source.yMin, cut.yMin), z1 = Mathf.Min(source.yMax, cut.yMax);
            if (x0 >= x1 || z0 >= z1) { output.Add(source); return; }
            if (source.xMin < x0) output.Add(Rect.MinMaxRect(source.xMin, source.yMin, x0, source.yMax));
            if (x1 < source.xMax) output.Add(Rect.MinMaxRect(x1, source.yMin, source.xMax, source.yMax));
            if (source.yMin < z0) output.Add(Rect.MinMaxRect(x0, source.yMin, x1, z0));
            if (z1 < source.yMax) output.Add(Rect.MinMaxRect(x0, z1, x1, source.yMax));
        }

        static Vector2 WorldUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 result = ax >= ay && ax >= az ? new Vector2(n.x > 0 ? -p.z : p.z, p.y)
                : ay >= az ? new Vector2(p.x, p.z) : new Vector2(n.z > 0 ? p.x : -p.x, p.y);
            return result * .5f; // CityAssets.P("Ground") / WorldKit.WorldUV use exactly two metres per tile.
        }

        static void AddBox(Rect rect, float bottom, float top, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            Vector3 c = new(rect.center.x, (bottom + top) * .5f, rect.center.y), h = new(rect.width * .5f, (top - bottom) * .5f, rect.height * .5f);
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int face = 0; face < 6; face++)
            {
                Vector3 normal = faces[face], u = face < 2 ? Vector3.forward : Vector3.right, w = face == 2 || face == 3 ? Vector3.forward : Vector3.up;
                int start = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float su = k == 1 || k == 2 ? 1 : -1, sw = k >= 2 ? 1 : -1;
                    var p = c + Vector3.Scale(normal + u * su + w * sw, h);
                    v.Add(p); n.Add(normal); uv.Add(WorldUV(p, normal));
                }
                if (Vector3.Dot(Vector3.Cross(u, w), normal) > 0) t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else t.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        bool Fail(string reason) { LastFailure = reason; return false; }

        public void Dispose()
        {
            if (original) original.enabled = originalEnabled;
            if (sourceId != 0 && owners.TryGetValue(sourceId, out var owner) && owner == this) owners.Remove(sourceId);
            if (replacement) { replacement.enabled = false; if (Application.isPlaying) Destroy(replacement.gameObject); else DestroyImmediate(replacement.gameObject); }
            if (ownedMesh) { if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh); }
            original = replacement = null; ownedMesh = null; sourceId = 0; Applied = false; appliedCuts = null;
        }
        void OnDestroy() => Dispose();
    }
}
