using System.Collections.Generic;
using Parasite.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Editor-time geometry builder. Boxes/meshes are accumulated per (root, material, 48 m cell) into merged meshes
    /// with world-space UVs (texture tiling in metres), colliders are separate BoxColliders. Call Flush() at the end.
    /// </summary>
    internal sealed class WorldKit
    {
        public const StaticEditorFlags StaticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                     StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI |
                                                     StaticEditorFlags.ReflectionProbeStatic;
        const float Cell = 48f;

        sealed class Batch
        {
            public readonly List<Vector3> v = new(), n = new();
            public readonly List<Vector2> uv = new();
            public readonly List<int> t = new();
        }

        readonly Dictionary<(Transform, Material, int, int), Batch> batches = new();
        readonly Dictionary<Transform, Transform> colliderRoots = new();
        readonly Mesh cylinder, sphere;
        public Transform Root;
        public readonly System.Random Rng;
        int colliderCount;

        public WorldKit(Transform root, int seed)
        {
            Root = root;
            Rng = new System.Random(seed);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            sphere = PrimitiveMesh(PrimitiveType.Sphere);
        }

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return m;
        }

        public float Rand(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
        public bool Chance(float p) => Rng.NextDouble() < p;
        public T Pick<T>(params T[] items) => items[Rng.Next(items.Length)];

        Batch Get(Material m, Vector3 c)
        {
            var key = (Root, m, Mathf.FloorToInt(c.x / Cell), Mathf.FloorToInt(c.z / Cell));
            if (!batches.TryGetValue(key, out var b)) batches[key] = b = new Batch();
            return b;
        }

        static float TileOf(Material m) => CityAssets.Tile.TryGetValue(m, out var t) ? t : 2f;

        static Vector2 WorldUV(Vector3 p, Vector3 n, float tile)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 uv = ax >= ay && ax >= az ? new Vector2(n.x > 0 ? -p.z : p.z, p.y)
                       : ay >= az ? new Vector2(p.x, p.z)
                       : new Vector2(n.z > 0 ? p.x : -p.x, p.y);
            return uv / tile;
        }

        static readonly Vector3[] FaceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        // ------------------------------------------------------------------ visuals
        public void Box(Material m, Vector3 c, Vector3 s, Quaternion? rot = null)
        {
            var q = rot ?? Quaternion.identity;
            var h = s * 0.5f;
            var b = Get(m, c);
            float tile = TileOf(m);
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceN[f];
                Vector3 u = f < 2 ? Vector3.forward : Vector3.right;
                Vector3 w = f == 2 || f == 3 ? Vector3.forward : Vector3.up;
                bool ccw = Vector3.Dot(Vector3.Cross(u, w), n) > 0f;
                int i0 = b.v.Count;
                Vector3 wn = q * n;
                for (int k = 0; k < 4; k++)
                {
                    float su = k == 1 || k == 2 ? 1f : -1f, sw = k >= 2 ? 1f : -1f;
                    Vector3 wp = c + q * Vector3.Scale(n + u * su + w * sw, h);
                    b.v.Add(wp); b.n.Add(wn); b.uv.Add(WorldUV(wp, wn, tile));
                }
                if (ccw) b.t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                else b.t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
        }

        public void BoxMM(Material m, float x0, float x1, float y0, float y1, float z0, float z1, bool solid = true)
        {
            var c = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var s = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            Box(m, c, s);
            if (solid) Solid(c, s);
        }

        public void Block(Material m, Vector3 c, Vector3 s) { Box(m, c, s); Solid(c, s); }

        public void Mesh(Material m, Mesh src, Matrix4x4 trs)
        {
            var b = Get(m, trs.GetColumn(3));
            var nm = trs.inverse.transpose;
            int i0 = b.v.Count;
            var verts = src.vertices; var norms = src.normals; var uvs = src.uv;
            for (int i = 0; i < verts.Length; i++)
            {
                b.v.Add(trs.MultiplyPoint3x4(verts[i]));
                b.n.Add(nm.MultiplyVector(norms[i]).normalized);
                b.uv.Add(i < uvs.Length ? uvs[i] : Vector2.zero);
            }
            foreach (var t in src.triangles) b.t.Add(i0 + t);
        }

        /// <summary>Vertical cylinder standing on base.y (or centred if axis given).</summary>
        public void Cylinder(Material m, Vector3 baseCenter, float radius, float height, Quaternion? rot = null)
        {
            var q = rot ?? Quaternion.identity;
            var center = baseCenter + q * Vector3.up * (height * 0.5f);
            Mesh(m, cylinder, Matrix4x4.TRS(center, q, new Vector3(radius * 2f, height * 0.5f, radius * 2f)));
        }

        /// <summary>Horizontal cylinder between two points.</summary>
        public void Pipe(Material m, Vector3 a, Vector3 b, float radius)
        {
            var d = b - a;
            var q = Quaternion.FromToRotation(Vector3.up, d.normalized);
            Mesh(m, cylinder, Matrix4x4.TRS((a + b) * 0.5f, q, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f)));
        }

        public void Sphere(Material m, Vector3 c, Vector3 size) => Mesh(m, sphere, Matrix4x4.TRS(c, Quaternion.identity, size));

        // ------------------------------------------------------------------ colliders
        public BoxCollider Solid(Vector3 c, Vector3 s, int layer = 0)
        {
            if (!colliderRoots.TryGetValue(Root, out var cr))
            {
                cr = new GameObject("Colliders").transform;
                cr.SetParent(Root, false);
                colliderRoots[Root] = cr;
            }
            var go = new GameObject("C" + colliderCount++) { layer = layer };
            go.transform.SetParent(cr, false);
            go.transform.position = c;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = s;
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            return bc;
        }

        public void SolidMM(float x0, float x1, float y0, float y1, float z0, float z1, int layer = 0) =>
            Solid(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0), layer);

        /// <summary>Wall along X (x0..x1) of thickness z0..z1 with door openings (from, to, top height).</summary>
        public void WallX(Material m, float x0, float x1, float z0, float z1, float y0, float y1, params Vector3[] openings)
        {
            float x = x0;
            foreach (var o in Sorted(openings))
            {
                if (o.x > x) BoxMM(m, x, o.x, y0, y1, z0, z1);
                if (o.z < y1) BoxMM(m, o.x, o.y, o.z, y1, z0, z1);
                x = o.y;
            }
            if (x1 > x) BoxMM(m, x, x1, y0, y1, z0, z1);
        }

        /// <summary>Wall along Z (z0..z1) of thickness x0..x1 with door openings (from, to, top height).</summary>
        public void WallZ(Material m, float z0, float z1, float x0, float x1, float y0, float y1, params Vector3[] openings)
        {
            float z = z0;
            foreach (var o in Sorted(openings))
            {
                if (o.x > z) BoxMM(m, x0, x1, y0, y1, z, o.x);
                if (o.z < y1) BoxMM(m, x0, x1, o.z, y1, o.x, o.y);
                z = o.y;
            }
            if (z1 > z) BoxMM(m, x0, x1, y0, y1, z, z1);
        }

        static List<Vector3> Sorted(Vector3[] o)
        {
            var l = new List<Vector3>(o);
            l.Sort((a, b) => a.x.CompareTo(b.x));
            return l;
        }

        // ------------------------------------------------------------------ signs, lights
        public GameObject Text(string text, Vector3 pos, float yaw, float height, Color color, Transform parent = null, bool label = true)
        {
            var go = new GameObject("Sign_" + text.Replace(' ', '_'));
            go.transform.SetParent(parent ? parent : Root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = CityAssets.Font;
            tm.fontSize = 64;
            tm.characterSize = height * 10f / 64f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            if (CityAssets.Font) mr.sharedMaterial = CityAssets.Font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (label)
            {
                var sl = go.AddComponent<SignLabel>();
                sl.text = text; sl.color = color;
            }
            return go;
        }

        /// <summary>Dark board with a neon edge and text, facing -Z rotated by yaw. Board centre at pos.</summary>
        public void Board(string text, Vector3 pos, float yaw, float width, float height, Color color, Material neon, Transform textParent = null)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Box(CityAssets.M("SignBoard"), pos, new Vector3(width, height, 0.12f), q);
            Box(neon, pos + q * new Vector3(0f, -height * 0.5f - 0.03f, -0.02f), new Vector3(width, 0.05f, 0.1f), q);
            Text(text, pos + q * new Vector3(0f, 0f, -0.08f), yaw, height * 0.62f, color, textParent);
        }

        public static Light AddLight(Transform parent, string name, LightType type, Vector3 pos, Color color, float intensity, float range,
            LightShadows shadows = LightShadows.None, Vector3? euler = null, float spotAngle = 60f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler ?? Vector3.zero));
            var l = go.AddComponent<Light>();
            l.type = type; l.color = color; l.intensity = intensity; l.range = range; l.shadows = shadows;
            if (type == LightType.Spot) { l.spotAngle = spotAngle; l.innerSpotAngle = spotAngle * 0.55f; }
            l.lightmapBakeType = LightmapBakeType.Realtime;
            if (!go.TryGetComponent<UniversalAdditionalLightData>(out _)) go.AddComponent<UniversalAdditionalLightData>();
            return l;
        }

        // ------------------------------------------------------------------ flush
        public int Flush()
        {
            int count = 0;
            var geoRoots = new Dictionary<Transform, Transform>();
            foreach (var kv in batches)
            {
                var (root, m, cx, cz) = kv.Key;
                var b = kv.Value;
                if (b.v.Count == 0) continue;
                if (!geoRoots.TryGetValue(root, out var gr))
                {
                    gr = new GameObject("Geometry").transform;
                    gr.SetParent(root, false);
                    GameObjectUtility.SetStaticEditorFlags(gr.gameObject, StaticFlags);
                    geoRoots[root] = gr;
                }
                var mesh = new Mesh { name = $"{root.name}_{m.name}_{cx}_{cz}" };
                if (b.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.t, 0);
                mesh.RecalculateBounds();
                if (m.IsKeywordEnabled("_NORMALMAP")) mesh.RecalculateTangents();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(gr, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = CityAssets.NoShadow.Contains(m) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                mr.receiveGI = ReceiveGI.LightProbes;
                mr.lightProbeUsage = LightProbeUsage.BlendProbes;
                mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
                count++;
            }
            batches.Clear();
            return count;
        }
    }
}
