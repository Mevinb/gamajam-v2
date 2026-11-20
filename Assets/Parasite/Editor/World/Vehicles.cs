using System.Collections.Generic;
using UnityEngine;
using static Parasite.EditorTools.CityAssets;

namespace Parasite.EditorTools
{
    /// <summary>Procedural parked vehicles: lofted body + greenhouse (smooth normals), arches, lights, wheels with rims.
    /// Local frame: +z forward, +x right, y up, origin on the ground at the car centre.</summary>
    internal static class Vehicles
    {
        internal enum Body { Sedan, Hatch, Suv, Van, Taxi }

        sealed class Spec
        {
            public float L, W, belt, roof, hood, deck, sill, wheelR, wheelBase, cabR, cabF, rearRake, frontRake, noseR, tailR;
            public bool sideGlassFrontOnly;
        }

        static Spec Get(Body b) => b switch
        {
            Body.Hatch => new Spec { L = 4.05f, W = 1.76f, belt = 0.92f, roof = 1.46f, hood = 0.86f, deck = 0.95f, sill = 0.27f, wheelR = 0.31f, wheelBase = 2.55f,
                                     cabR = -1.8f, cabF = 0.55f, rearRake = 0.22f, frontRake = 0.8f, noseR = 0.35f, tailR = 0.18f },
            Body.Suv => new Spec { L = 4.65f, W = 1.9f, belt = 1.1f, roof = 1.74f, hood = 1.05f, deck = 1.1f, sill = 0.42f, wheelR = 0.37f, wheelBase = 2.8f,
                                   cabR = -2.15f, cabF = 0.7f, rearRake = 0.18f, frontRake = 0.75f, noseR = 0.3f, tailR = 0.15f },
            Body.Van => new Spec { L = 5.0f, W = 1.98f, belt = 1.12f, roof = 2.15f, hood = 1.0f, deck = 1.2f, sill = 0.36f, wheelR = 0.35f, wheelBase = 3.2f,
                                   cabR = -2.45f, cabF = 1.35f, rearRake = 0.05f, frontRake = 0.6f, noseR = 0.3f, tailR = 0.08f, sideGlassFrontOnly = true },
            _ => new Spec { L = 4.6f, W = 1.82f, belt = 0.9f, roof = 1.42f, hood = 0.84f, deck = 0.93f, sill = 0.27f, wheelR = 0.32f, wheelBase = 2.75f,
                            cabR = -1.3f, cabF = 0.75f, rearRake = 0.55f, frontRake = 0.8f, noseR = 0.42f, tailR = 0.25f },
        };

        static readonly Dictionary<(Body, string), Mesh> cache = new();

        public static Body PickBody(WorldKit k, string paint)
        {
            if (paint == "CarTaxi") return Body.Taxi;
            double r = k.Rng.NextDouble();
            return r < 0.42 ? Body.Sedan : r < 0.72 ? Body.Hatch : r < 0.94 ? Body.Suv : Body.Van;
        }

        /// <summary>Parked car, yaw 0 = nose towards +z. Collider = body box.</summary>
        public static void Car(WorldKit k, Vector3 p, float yaw, string paint, Body body)
        {
            if (k.Chance(0.5f)) yaw += 180f;
            var sp = Get(body == Body.Taxi ? Body.Sedan : body);
            string pm = body == Body.Taxi ? "CarTaxi" : body == Body.Van && paint == "CarRed" ? "CarWhite" : paint;
            var mesh = BodyMesh(body, sp);
            var q = Quaternion.Euler(0f, yaw, 0f);
            var trs = Matrix4x4.TRS(p, q, Vector3.one);
            k.Mesh(new[] { M(pm), M("GlassCar"), M("CarTrim"), M("Headlight"), M("Taillight"), M("Chrome") }, mesh, trs);
            // wheels: tyre + rim + hub
            foreach (float zs in new[] { -0.5f, 0.5f })
            foreach (float xs in new[] { -1f, 1f })
            {
                var c = new Vector3(xs * (sp.W * 0.5f - 0.13f), sp.wheelR, zs * sp.wheelBase + (body == Body.Van ? 0.2f : 0f));
                var a = p + q * (c + Vector3.right * xs * -0.11f); var b = p + q * (c + Vector3.right * xs * 0.11f);
                k.Pipe(M("Tire"), a, b, sp.wheelR);
                k.Pipe(M("Rim"), p + q * (c + Vector3.right * xs * 0.1f), p + q * (c + Vector3.right * xs * 0.125f), sp.wheelR * 0.62f);
                k.Pipe(M("CarTrim"), p + q * (c + Vector3.right * xs * 0.12f), p + q * (c + Vector3.right * xs * 0.135f), sp.wheelR * 0.2f);
            }
            if (body == Body.Taxi)
            {
                k.Box(M("CarTrim"), p + q * new Vector3(0f, sp.roof + 0.02f, -0.25f), new Vector3(0.75f, 0.04f, 0.28f), q);
                k.Box(M("WarningLight"), p + q * new Vector3(0f, sp.roof + 0.14f, -0.25f), new Vector3(0.7f, 0.2f, 0.24f), q);
            }
            // contact shadow under the car (helps grounding at low sun angles)
            k.FloorQuad(M("DecalGrime"), p + Vector3.up * 0.006f, sp.W + 0.3f, sp.L + 0.2f, yaw, new Rect(0f, 0.02f, 1f, 0.02f));
            float L = Mathf.Max(sp.L, 4.4f), W = Mathf.Max(sp.W, 1.84f);
            var cs = q * new Vector3(W, 1.5f, L);
            k.Solid(p + Vector3.up * 0.75f, new Vector3(Mathf.Abs(cs.x), 1.5f, Mathf.Abs(cs.z)));
        }

        static Mesh BodyMesh(Body body, Spec s)
        {
            if (cache.TryGetValue((body, ""), out var m) && m) return m;
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            var sub = new List<int>[6]; for (int i = 0; i < 6; i++) sub[i] = new List<int>();
            float hl = s.L * 0.5f;
            float wb = s.wheelBase * 0.5f + (body == Body.Van ? 0.2f : 0f), wbR = -s.wheelBase * 0.5f + (body == Body.Van ? 0.2f : 0f);
            float Top(float z)
            {
                float t;
                if (z < s.cabR) t = s.deck;
                else if (z > s.cabF) t = Mathf.Lerp(s.belt, s.hood, Mathf.InverseLerp(s.cabF, hl - s.noseR, z));
                else t = s.belt;
                // rounded nose / tail
                if (z > hl - s.noseR) t -= (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((z - (hl - s.noseR)) / s.noseR, 2f)))) * s.noseR * 0.9f;
                if (z < -hl + s.tailR) t -= (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((-hl + s.tailR - z) / s.tailR, 2f)))) * s.tailR * 0.8f;
                return t;
            }
            float Bottom(float z)
            {
                float y = s.sill;
                float ra = s.wheelR + 0.06f;
                foreach (float wz in new[] { wbR, wb })
                {
                    float d = Mathf.Abs(z - wz);
                    if (d < ra) y = Mathf.Max(y, s.wheelR + Mathf.Sqrt(ra * ra - d * d));
                }
                if (z > hl - 0.35f) y = Mathf.Lerp(y, s.sill + 0.08f, (z - (hl - 0.35f)) / 0.35f);
                if (z < -hl + 0.3f) y = Mathf.Lerp(y, s.sill + 0.1f, (-hl + 0.3f - z) / 0.3f);
                return y;
            }
            // ---- lower body loft
            const int N = 44;
            float hw = s.W * 0.5f;
            var rings = new List<Vector3[]>();
            for (int i = 0; i <= N; i++)
            {
                float z = Mathf.Lerp(-hl, hl, i / (float)N);
                float yb = Bottom(z), yt = Top(z), ym1 = Mathf.Lerp(yb, yt, 0.3f), ym2 = Mathf.Lerp(yb, yt, 0.82f);
                float endPinch = 1f - 0.06f * Mathf.Pow(Mathf.Abs(z) / hl, 6f);
                float w0 = hw * endPinch;
                rings.Add(new[]
                {
                    new Vector3(-w0 + 0.05f, yb, z), new Vector3(-w0, ym1, z), new Vector3(-w0 + 0.01f, ym2, z), new Vector3(-w0 + 0.1f, yt, z),
                    new Vector3(w0 - 0.1f, yt, z), new Vector3(w0 - 0.01f, ym2, z), new Vector3(w0, ym1, z), new Vector3(w0 - 0.05f, yb, z),
                });
            }
            int[] edgeMat = { 0, 0, 0, 0, 0, 0, 0, 2 }; // ring edge j -> j+1 ; last edge (bottom) = trim
            Loft(v, uv, sub, rings, edgeMat, true);
            Cap(v, uv, sub[0], rings[0], false);
            Cap(v, uv, sub[0], rings[N], true);
            // ---- greenhouse
            const int C = 18;
            var cab = new List<Vector3[]>();
            float roofR = s.cabR + s.rearRake, roofF = s.cabF - s.frontRake;
            for (int i = 0; i <= C; i++)
            {
                float z = Mathf.Lerp(s.cabR, s.cabF, i / (float)C);
                float yt = z < roofR ? Mathf.Lerp(s.belt, s.roof, Mathf.SmoothStep(0f, 1f, (z - s.cabR) / Mathf.Max(0.01f, s.rearRake)))
                         : z > roofF ? Mathf.Lerp(s.roof, s.belt, Mathf.SmoothStep(0f, 1f, (z - roofF) / Mathf.Max(0.01f, s.frontRake))) : s.roof;
                float wbelt = hw - 0.12f, wroof = hw - 0.26f;
                float baseY = Top(z) - 0.005f;
                cab.Add(new[] { new Vector3(-wbelt, baseY, z), new Vector3(-wroof, yt, z), new Vector3(wroof, yt, z), new Vector3(wbelt, baseY, z) });
            }
            for (int i = 0; i < C; i++)
            {
                var a = cab[i]; var b = cab[i + 1];
                float zm = (a[0].z + b[0].z) * 0.5f;
                bool roofSeg = a[1].y > s.roof - 0.02f && b[1].y > s.roof - 0.02f;
                bool pillar = Mathf.Abs(zm - Mathf.Lerp(s.cabR, s.cabF, 0.52f)) < 0.07f || zm < s.cabR + 0.12f || zm > s.cabF - 0.1f
                              || s.sideGlassFrontOnly && zm < roofF - 1.1f || body == Body.Suv && zm < s.cabR + 0.45f;
                var cc = new Vector3(0f, s.belt, (s.cabR + s.cabF) * 0.5f);
                QuadV(v, uv, sub[pillar ? 0 : 1], a[0], b[0], b[1], a[1], cc);     // left side
                QuadV(v, uv, sub[pillar ? 0 : 1], b[3], a[3], a[2], b[2], cc);     // right side
                QuadV(v, uv, sub[roofSeg ? 0 : 1], a[1], b[1], b[2], a[2], cc);    // roof / windshield / rear glass
            }
            Cap(v, uv, sub[1], cab[0], false);
            Cap(v, uv, sub[1], cab[C], true);
            // ---- lights, grille, bumpers, plates, mirrors (boxes in local space)
            void Box(int mat, Vector3 c, Vector3 size)
            {
                var h = size * 0.5f;
                Vector3[] p = { new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1), new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1) };
                for (int i = 0; i < 8; i++) p[i] = c + Vector3.Scale(p[i], h);
                QuadV(v, uv, sub[mat], p[0], p[1], p[2], p[3], c); QuadV(v, uv, sub[mat], p[5], p[4], p[7], p[6], c);
                QuadV(v, uv, sub[mat], p[4], p[0], p[3], p[7], c); QuadV(v, uv, sub[mat], p[1], p[5], p[6], p[2], c);
                QuadV(v, uv, sub[mat], p[3], p[2], p[6], p[7], c); QuadV(v, uv, sub[mat], p[4], p[5], p[1], p[0], c);
            }
            float fy = Top(hl - 0.08f), ry = Top(-hl + 0.06f);
            foreach (float xs in new[] { -1f, 1f })
            {
                Box(3, new Vector3(xs * (hw - 0.3f), fy - 0.12f, hl - 0.05f), new Vector3(0.34f, 0.11f, 0.1f));
                Box(4, new Vector3(xs * (hw - 0.22f), ry - 0.14f, -hl + 0.03f), new Vector3(0.36f, 0.12f, 0.08f));
                Box(2, new Vector3(xs * (hw + 0.07f), s.belt + 0.07f, s.cabF - 0.25f), new Vector3(0.14f, 0.11f, 0.2f));
            }
            Box(2, new Vector3(0f, fy - 0.15f, hl - 0.03f), new Vector3(hw * 0.75f, 0.14f, 0.08f));
            Box(2, new Vector3(0f, s.sill + 0.12f, hl - 0.02f), new Vector3(s.W - 0.16f, 0.18f, 0.12f));
            Box(2, new Vector3(0f, s.sill + 0.14f, -hl + 0.02f), new Vector3(s.W - 0.16f, 0.2f, 0.12f));
            Box(5, new Vector3(0f, s.sill + 0.28f, hl + 0.02f), new Vector3(0.5f, 0.11f, 0.02f));
            Box(5, new Vector3(0f, s.sill + 0.33f, -hl - 0.02f), new Vector3(0.5f, 0.11f, 0.02f));
            // side trim strips (door handles line)
            foreach (float xs in new[] { -1f, 1f }) Box(2, new Vector3(xs * (hw - 0.005f), s.sill + 0.08f, 0f), new Vector3(0.03f, 0.08f, s.wheelBase - 0.9f));

            m = new Mesh { name = "Car_" + body };
            m.SetVertices(v); m.SetUVs(0, uv);
            m.subMeshCount = 6;
            for (int i = 0; i < 6; i++) m.SetTriangles(sub[i], i);
            m.SetNormals(SmoothNormals(v, sub, 55f));
            m.RecalculateBounds();
            cache[(body, "")] = m;
            return m;
        }

        /// <summary>Per-vertex normals averaged over coincident vertices whose faces are within <paramref name="angle"/> degrees.</summary>
        static List<Vector3> SmoothNormals(List<Vector3> v, List<int>[] sub, float angle)
        {
            var faceN = new Vector3[v.Count];
            foreach (var t in sub)
                for (int i = 0; i < t.Count; i += 3)
                {
                    var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    faceN[t[i]] += n; faceN[t[i + 1]] += n; faceN[t[i + 2]] += n;
                }
            var groups = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < v.Count; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 500f);
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<int>();
                l.Add(i);
            }
            float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            var outN = new List<Vector3>(v.Count);
            for (int i = 0; i < v.Count; i++)
            {
                var own = faceN[i].normalized; var sum = Vector3.zero;
                foreach (int j in groups[Vector3Int.RoundToInt(v[i] * 500f)])
                    if (Vector3.Dot(faceN[j].normalized, own) > cos) sum += faceN[j];
                outN.Add(sum.sqrMagnitude > 0f ? sum.normalized : Vector3.up);
            }
            return outN;
        }

        static void Loft(List<Vector3> v, List<Vector2> uv, List<int>[] sub, List<Vector3[]> rings, int[] edgeMat, bool closed)
        {
            int n = rings[0].Length;
            for (int i = 0; i < rings.Count - 1; i++)
            {
                var c = (Centre(rings[i]) + Centre(rings[i + 1])) * 0.5f;
                for (int j = 0; j < n; j++)
                {
                    int j2 = (j + 1) % n;
                    if (!closed && j2 == 0) continue;
                    QuadV(v, uv, sub[edgeMat[j]], rings[i][j], rings[i][j2], rings[i + 1][j2], rings[i + 1][j], c);
                }
            }
        }

        static void Cap(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3[] ring, bool front)
        {
            int i0 = v.Count;
            foreach (var p in ring) { v.Add(p); uv.Add(new Vector2(p.x, p.y)); }
            var n = Vector3.Cross(ring[1] - ring[0], ring[2] - ring[0]);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(ring[2] - ring[0], ring[3] - ring[0]);
            bool keep = Vector3.Dot(n, front ? Vector3.forward : Vector3.back) > 0f;
            for (int i = 1; i < ring.Length - 1; i++)
                t.AddRange(keep ? new[] { i0, i0 + i, i0 + i + 1 } : new[] { i0, i0 + i + 1, i0 + i });
        }

        /// <summary>Quad a-b-c-d; front side = the side facing away from <paramref name="centre"/>.</summary>
        static void QuadV(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centre)
        {
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(a.z, a.y)); uv.Add(new Vector2(b.z, b.y)); uv.Add(new Vector2(c.z, c.y)); uv.Add(new Vector2(d.z, d.y));
            var n = Vector3.Cross(b - a, d - a);
            var outward = (a + b + c + d) * 0.25f - centre;
            if (Vector3.Dot(n, outward) > 0f) t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            else t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }

        static Vector3 Centre(Vector3[] ring)
        {
            var c = Vector3.zero;
            foreach (var p in ring) c += p;
            return c / ring.Length;
        }

        // ------------------------------------------------------------------ box truck
        public static void Truck(WorldKit k, Vector3 p, float yaw, string paint, string label)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float l, float y, float w) => p + q * new Vector3(w, y, l);
            const float W = 2.45f;
            var cargo = M("TruckWhite");
            k.Box(cargo, P(-1f, 2.25f, 0), new Vector3(W, 2.7f, 5.8f), q);
            k.Box(M("MetalFrame"), P(-1f, 3.62f, 0), new Vector3(W + 0.04f, 0.06f, 5.84f), q);
            k.Box(M("MetalFrame"), P(-3.92f, 2.25f, 0), new Vector3(W + 0.04f, 2.72f, 0.06f), q);
            for (float l = -3.4f; l < 1.9f; l += 1.2f) k.Box(M("MetalFrame"), P(l, 2.25f, 0), new Vector3(W + 0.03f, 2.6f, 0.04f), q);
            // cab: lofted van front
            var vs = Get(Body.Van);
            k.Box(M(paint), P(2.9f, 1.25f, 0), new Vector3(W - 0.05f, 1.3f, 2.1f), q);
            k.Box(M(paint), P(2.75f, 2.35f, 0), new Vector3(W - 0.15f, 0.9f, 1.8f), q);
            k.Box(M("GlassCar"), P(3.68f, 2.3f, 0), new Vector3(W - 0.3f, 0.75f, 0.06f), q * Quaternion.Euler(-12f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                k.Box(M("GlassCar"), P(2.85f, 2.35f, s * (W * 0.5f - 0.07f)), new Vector3(0.04f, 0.6f, 1.1f), q);
                k.Box(M("Headlight"), P(3.96f, 1.0f, s * 0.85f), new Vector3(0.3f, 0.16f, 0.05f), q);
                k.Box(M("Taillight"), P(-3.96f, 0.9f, s * 1.0f), new Vector3(0.25f, 0.14f, 0.05f), q);
                k.Box(M("CarTrim"), P(3.4f, 2.4f, s * (W * 0.5f + 0.12f)), new Vector3(0.06f, 0.35f, 0.2f), q);
            }
            k.Box(M("CarTrim"), P(3.97f, 0.65f, 0), new Vector3(W, 0.3f, 0.12f), q);
            k.Box(M("CarTrim"), P(0.5f, 0.75f, 0), new Vector3(W - 0.3f, 0.3f, 8f), q);
            foreach (float l in new[] { -3f, -1.9f, 3.1f })
            foreach (float w in new[] { -1.05f, 1.05f })
            {
                k.Pipe(M("Tire"), P(l, 0.48f, w - 0.16f), P(l, 0.48f, w + 0.16f), 0.48f);
                k.Pipe(M("Rim"), P(l, 0.48f, w + Mathf.Sign(w) * 0.14f), P(l, 0.48f, w + Mathf.Sign(w) * 0.17f), 0.3f);
            }
            k.FloorQuad(M("DecalGrime"), p + Vector3.up * 0.006f, W + 0.4f, 8.4f, yaw, new Rect(0f, 0.02f, 1f, 0.02f));
            var sz = q * new Vector3(W, 3.6f, 8.1f);
            k.Solid(P(0f, 1.8f, 0), new Vector3(Mathf.Abs(sz.x), 3.6f, Mathf.Abs(sz.z)));
            if (label != null)
                foreach (float s in new[] { -1f, 1f })
                {
                    var n = q * Vector3.right * s;
                    float ty = Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg;
                    k.Text(label, P(-1f, 2.4f, s * (W * 0.5f + 0.02f)), ty, 0.6f, new Color(0.15f, 0.2f, 0.3f), null, false);
                }
        }
    }
}
