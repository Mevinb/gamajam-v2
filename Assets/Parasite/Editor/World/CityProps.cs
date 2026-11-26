using System;
using System.Collections.Generic;
using UnityEngine;
using static Parasite.EditorTools.CityAssets;

namespace Parasite.EditorTools
{
    [Flags] internal enum Faces { None = 0, N = 1, S = 2, E = 4, W = 8, All = 15 }

    internal readonly struct Shop
    {
        public readonly Faces face; public readonly float along, width; public readonly string name; public readonly string awning, neon;
        public Shop(Faces face, float along, float width, string name, string awning = "AwningRed", string neon = "NeonCyan")
        { this.face = face; this.along = along; this.width = width; this.name = name; this.awning = awning; this.neon = neon; }
    }

    /// <summary>Reusable props: buildings (photo facades + storefronts), street furniture, vehicles, lab furniture.
    /// All static and merged by WorldKit; colliders are separate boxes that match the old gameplay footprint.</summary>
    internal static class CityProps
    {
        public const float GroundFloorTop = 4.35f, ShopDepth = 1.1f, Sidewalk = 0.15f;
        static readonly Color NeonText = new(1f, 0.93f, 0.8f);

        // String.GetHashCode is randomized across editor processes. Keep regenerated storefronts identical.
        static int ShopSeed(string name)
        {
            uint hash = 2166136261u;
            foreach (char c in name) hash = unchecked((hash ^ c) * 16777619u);
            return (int)(hash & 0x7fffffffu);
        }

        internal struct Face
        {
            public Vector3 origin, dir, normal; public float length;
            public Vector3 At(float along, float y, float off) => origin + dir * along + Vector3.up * y + normal * off;
            public Vector3 Size(float w, float h, float d) => Mathf.Abs(dir.x) > 0.5f ? new Vector3(w, h, d) : new Vector3(d, h, w);
            public float Yaw => Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
            public Quaternion Rot => Quaternion.Euler(0f, Yaw, 0f);
        }

        internal static Face GetFace(Faces f, float x0, float x1, float z0, float z1) => f switch
        {
            Faces.N => new Face { origin = new Vector3(x1, 0, z1), dir = Vector3.left, normal = Vector3.forward, length = x1 - x0 },
            Faces.S => new Face { origin = new Vector3(x0, 0, z0), dir = Vector3.right, normal = Vector3.back, length = x1 - x0 },
            Faces.E => new Face { origin = new Vector3(x1, 0, z0), dir = Vector3.forward, normal = Vector3.right, length = z1 - z0 },
            _ => new Face { origin = new Vector3(x0, 0, z1), dir = Vector3.back, normal = Vector3.left, length = z1 - z0 },
        };

        /// <summary>Shop.along is measured from the face's min-x / min-z corner (legacy convention); faces N and W run backwards.</summary>
        static float Along(Faces f, Face face, float legacyAlong) => f == Faces.N || f == Faces.W ? face.length - legacyAlong : legacyAlong;

        // ================================================================== buildings
        public static void Building(WorldKit k, float x0, float x1, float z0, float z1, float h, Material wall, Faces faces, params Shop[] shops) =>
            Building(k, StyleFor(wall), x0, x1, z0, z1, h, faces, shops);

        public static void Building(WorldKit k, FacadeStyle st, float x0, float x1, float z0, float z1, float h, Faces faces, params Shop[] shops)
        {
            const float GF = GroundFloorTop;
            // gameplay collider: the full footprint, exactly as before
            k.Solid(new Vector3((x0 + x1) * 0.5f, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, h, z1 - z0));

            Faces shopFaces = Faces.None;
            foreach (var s in shops) shopFaces |= s.face;
            float D = ShopDepth;
            // visual core: upper storeys full footprint, ground floor inset where storefronts are
            k.BoxMM(st.Wall, x0, x1, GF, h, z0, z1, false);
            float gx0 = x0 + ((shopFaces & Faces.W) != 0 ? D : 0f), gx1 = x1 - ((shopFaces & Faces.E) != 0 ? D : 0f);
            float gz0 = z0 + ((shopFaces & Faces.S) != 0 ? D : 0f), gz1 = z1 - ((shopFaces & Faces.N) != 0 ? D : 0f);
            k.BoxMM(st.Wall, gx0, gx1, 0f, GF, gz0, gz1, false);

            int floors = Mathf.Max(1, Mathf.RoundToInt((h - GF - 0.9f) / st.FloorHeight));
            float fh = (h - GF - 0.9f) / floors;
            if (fh < 2.7f) { floors = Mathf.Max(1, floors - 1); fh = (h - GF - 0.9f) / floors; }
            float yTop = GF + floors * fh;
            float tileU = 1f / st.BaysPerTile, tileV = 1f / st.FloorsPerTile;

            foreach (Faces f in new[] { Faces.N, Faces.S, Faces.E, Faces.W })
            {
                var face = GetFace(f, x0, x1, z0, z1);
                bool street = (faces & f) != 0;
                if (!street)
                {   // party wall: blank, with a little grime at the base
                    if (face.length > 2f) k.WallQuad(M("DecalGrime"), face.origin, face.dir, face.normal, 0f, face.length, 0f, 2.2f, 0f, 1f / 8f, 0f, 1f / 2.2f, 0.012f);
                    continue;
                }
                int bays = Mathf.Max(1, Mathf.RoundToInt(face.length / st.BayWidth));
                float B = face.length / bays;
                float u0 = 0.5f * tileU + k.Rng.Next(6) * tileU, uPer = tileU / B;
                float v0 = 0.5f * tileV + k.Rng.Next(6) * tileV, vPer = tileV / fh;
                // upper storeys
                k.WallQuad(st.Mat, face.origin, face.dir, face.normal, 0f, face.length, GF, yTop, u0, uPer, v0, vPer, 0.01f);
                EnvironmentRealismAssets.Windows(k, face, GF, B, fh, floors, bays);
                // cornice + parapet + string course at the ground floor top
                k.Box(st.Trim, face.At(face.length * 0.5f, yTop + 0.16f, 0.14f), face.Size(face.length + 0.56f, 0.32f, 0.28f));
                k.Box(st.Trim, face.At(face.length * 0.5f, yTop - 0.05f, 0.06f), face.Size(face.length + 0.36f, 0.1f, 0.12f));
                k.Box(st.Trim, face.At(face.length * 0.5f, GF - 0.05f, 0.1f), face.Size(face.length + 0.2f, 0.3f, 0.2f));
                // ground floor
                var spans = new System.Collections.Generic.List<(float a, float b, Shop s)>();
                foreach (var s in shops)
                {
                    if (s.face != f) continue;
                    float c = Along(f, face, s.along);
                    float a = Mathf.Max(0f, Mathf.Round((c - s.width * 0.5f) / B) * B), b = Mathf.Min(face.length, Mathf.Round((c + s.width * 0.5f) / B) * B);
                    if (b - a < B) b = Mathf.Min(face.length, a + B);
                    if (a < 0.4f) a = Mathf.Min(B, b - 1f) > 0.4f ? Mathf.Min(0.6f, b - 2f) : a;
                    if (face.length - b < 0.4f) b = face.length - 0.6f;
                    spans.Add((a, b, s));
                }
                spans.Sort((p, q) => p.a.CompareTo(q.a));
                bool inset = (shopFaces & f) != 0;
                float cur = 0f;
                foreach (var sp in spans)
                {
                    if (sp.a > cur + 0.01f) GroundWall(k, st, face, cur, sp.a, inset ? D : 0f, u0, uPer, B);
                    ShopFront(k, st, face, sp.a, sp.b, sp.s);
                    cur = sp.b;
                }
                if (cur < face.length - 0.01f) GroundWall(k, st, face, cur, face.length, inset ? D : 0f, u0, uPer, B);
                // ground grime + drain pipes at both ends
                k.WallQuad(M("DecalGrime"), face.origin, face.dir, face.normal, 0f, face.length, 0f, 1.6f, k.Rand(0f, 1f), 1f / 7f, 0f, 1f / 1.6f, 0.05f + (inset ? 0f : 0f));
                foreach (float s in new[] { 0.3f, face.length - 0.3f })
                    k.Cylinder(M("Pipe"), face.At(s, 0.2f, 0.12f), 0.065f, h - 0.2f + 0.6f);
                if (st.Residential && floors >= 3 && face.length > 14f && k.Chance(0.55f)) Balconies(k, face, floors, fh, B, bays);
            }

            // roof: gravel, parapet ring, clutter
            float py = h + 0.9f;
            k.Box(M("Gravel"), new Vector3((x0 + x1) * 0.5f, h + 0.02f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 - 0.3f, 0.04f, z1 - z0 - 0.3f));
            k.BoxMM(st.Wall, x0, x1, h, py, z0, z0 + 0.25f, false);
            k.BoxMM(st.Wall, x0, x1, h, py, z1 - 0.25f, z1, false);
            k.BoxMM(st.Wall, x0, x0 + 0.25f, h, py, z0 + 0.25f, z1 - 0.25f, false);
            k.BoxMM(st.Wall, x1 - 0.25f, x1, h, py, z0 + 0.25f, z1 - 0.25f, false);
            k.BoxMM(st.Trim, x0 - 0.06f, x1 + 0.06f, py, py + 0.1f, z0 - 0.06f, z1 + 0.06f, false);
            Rooftop(k, st, x0, x1, z0, z1, h);
        }

        /// <summary>Ground floor without a shop: stone plinth, a storey of facade windows, the odd entrance door.</summary>
        static void GroundWall(WorldKit k, FacadeStyle st, Face face, float a, float b, float depth, float u0, float uPer, float B)
        {
            const float GF = GroundFloorTop, plinth = 1.15f;
            if (depth > 0f) k.Box(st.Wall, face.At((a + b) * 0.5f, GF * 0.5f, -depth * 0.5f), face.Size(b - a, GF, depth));
            float tv = 1f / 6f;
            k.WallQuad(st.Mat, face.origin, face.dir, face.normal, a, b, plinth, GF - 0.2f, u0 + a * uPer, uPer, 0.5f * tv, tv / (GF - 0.2f - plinth), 0.01f);
            k.Box(M("Stone"), face.At((a + b) * 0.5f, plinth * 0.5f, 0.04f), face.Size(b - a, plinth, 0.1f));
            // entrance doors on longer stretches, centred on a bay
            float len = b - a;
            if (len < 2.5f * B) return;
            int bayA = Mathf.CeilToInt(a / B), bayB = Mathf.FloorToInt(b / B) - 1;
            for (int i = bayA + 1; i <= bayB - 1; i += k.Rng.Next(4, 7))
            {
                float s = (i + 0.5f) * B;
                EnvironmentRealismAssets.Entrance(k, face, s);
                k.Box(M("Stone"), face.At(s, 0.08f + Sidewalk, 0.35f), face.Size(1.9f, 0.16f, 0.5f));
                WallLamp(k, face.At(s + 1.25f, 2.55f, 0.06f), face.Yaw);
            }
        }

        static readonly string[] Awnings = { "AwningRed", "AwningGreen", "AwningNavy", "AwningYellow", "AwningBlack", "AwningCream" };

        static void ShopFront(WorldKit k, FacadeStyle st, Face f, float a, float b, Shop s)
        {
            const float GF = GroundFloorTop, D = ShopDepth, top = 3.2f, kick = Sidewalk + 0.4f;
            float w = b - a, c = (a + b) * 0.5f;
            int hash = ShopSeed(s.name);
            // piers at both ends (full depth), fascia above
            foreach (float e in new[] { a, b })
                k.Box(st.Wall, f.At(e, GF * 0.5f, -D * 0.5f + 0.02f), f.Size(0.5f, GF, D + 0.04f));
            k.Box(M("Stone"), f.At(a, 1.6f, 0.03f), f.Size(0.56f, 3.2f, 0.08f));
            k.Box(M("Stone"), f.At(b, 1.6f, 0.03f), f.Size(0.56f, 3.2f, 0.08f));
            // interior: back wall (lit shelves), floor, ceiling light
            int variant = s.name.Contains("BAR") || s.name.Contains("CAFE") || s.name.Contains("DINER") || s.name.Contains("RAMEN") || s.name.Contains("SUSHI") || s.name.Contains("DELI")
                ? 2 : new[] { 0, 1, 3 }[hash % 3];
            InteriorPanels(k, f, a + 0.25f, b - 0.25f, D, Sidewalk, top, variant);
            k.Box(M("ShopFloor"), f.At(c, Sidewalk + 0.005f, -D * 0.5f), f.Size(w - 0.4f, 0.01f, D));
            k.Box(M("ShopGlow"), f.At(c, top + 0.02f, -D * 0.5f), f.Size(w - 0.5f, 0.04f, D - 0.1f));
            // glazing
            k.WallQuad(M("GlassShop"), f.origin, f.dir, f.normal, a + 0.25f, b - 0.25f, kick, top, 0f, 0.2f, 0f, 0.2f, -0.06f);
            k.Box(M("KickPlate"), f.At(c, (Sidewalk + kick) * 0.5f, -0.06f), f.Size(w - 0.5f, kick - Sidewalk, 0.1f));
            k.Box(M("MetalFrame"), f.At(c, top + 0.04f, -0.05f), f.Size(w - 0.5f, 0.08f, 0.12f));
            k.Box(M("MetalFrame"), f.At(c, kick, -0.05f), f.Size(w - 0.5f, 0.06f, 0.12f));
            k.Box(M("MetalFrame"), f.At(c, 2.55f, -0.05f), f.Size(w - 0.5f, 0.05f, 0.1f));
            int lights = Mathf.Max(2, Mathf.RoundToInt((w - 0.5f) / 2.3f));
            float lw = (w - 0.5f) / lights;
            float doorAt = (hash & 1) == 0 ? a + 0.25f + lw * 0.5f : b - 0.25f - lw * 0.5f;
            for (int i = 1; i < lights; i++) k.Box(M("MetalFrame"), f.At(a + 0.25f + lw * i, (kick + top) * 0.5f, -0.05f), f.Size(0.07f, top - kick, 0.12f));
            // door (glass with frame + handle), slightly proud
            k.Box(M("MetalFrame"), f.At(doorAt, (Sidewalk + 2.5f) * 0.5f, -0.02f), f.Size(Mathf.Min(1.2f, lw - 0.2f) + 0.12f, 2.5f - Sidewalk, 0.06f));
            k.WallQuad(M("GlassDark"), f.origin, f.dir, f.normal, doorAt - Mathf.Min(0.55f, lw * 0.5f - 0.15f), doorAt + Mathf.Min(0.55f, lw * 0.5f - 0.15f),
                Sidewalk + 0.12f, 2.38f, 0f, 0.3f, 0f, 0.3f, 0.015f);
            k.Box(M("Chrome"), f.At(doorAt + 0.35f, 1.2f, 0.06f), f.Size(0.03f, 0.6f, 0.05f));
            // fascia board with the illuminated sign (text 0.35–0.6 m tall)
            var fasciaMat = M(st.Residential ? "SignBoard" : "MetalFrame");
            k.Box(fasciaMat, f.At(c, 3.72f, 0.1f), f.Size(w - 0.1f, 0.9f, 0.2f));
            float signH = Mathf.Clamp((w - 1f) / 6f, 0.5f, 0.68f);
            var used = k.Sign(s.name, f.At(c, 3.72f, 0.205f), f.Yaw, signH, w - 0.8f);
            if (used == Vector2.zero) k.Text(s.name, f.At(c, 3.72f, 0.21f), f.Yaw, 0.45f, NeonText, null, true);
            k.Box(M(s.neon), f.At(c, 3.25f, 0.18f), f.Size(w - 0.3f, 0.03f, 0.04f));
            for (float x = a + 0.8f; x < b - 0.5f; x += 1.6f) k.Box(M("ShopStrip"), f.At(x, 3.255f, 0.1f), f.Size(0.14f, 0.02f, 0.14f));
            // awning (about 60%): slanted canvas + valance
            if ((hash % 5) < 3)
            {
                var aw = M(s.awning ?? Awnings[hash % Awnings.Length]);
                float depth = 1.35f, drop = 0.5f;
                var slope = (f.normal * depth + Vector3.down * drop).normalized;
                var rot = Quaternion.LookRotation(slope, Vector3.Cross(slope, f.dir).y > 0f ? Vector3.Cross(slope, f.dir) : -Vector3.Cross(slope, f.dir));
                var ctr = f.At(c, 3.15f - drop * 0.5f, depth * 0.5f + 0.02f);
                var sz = new Vector3(w - 0.4f, 0.04f, Mathf.Sqrt(depth * depth + drop * drop));
                k.Box(aw, ctr, sz, rot);
                k.Box(aw, f.At(c, 3.15f - drop - 0.14f, depth + 0.02f), f.Size(w - 0.4f, 0.28f, 0.03f));
                foreach (float e in new[] { a + 0.25f, b - 0.25f }) k.Box(M("Rail"), f.At(e, 3.15f - drop * 0.5f, depth * 0.5f), f.Size(0.03f, 0.03f, depth));
            }
            // projecting blade sign for hotels / bars
            if (s.name.StartsWith("HOTEL") || s.name.Contains("BAR"))
            {
                string blade = s.name.StartsWith("HOTEL") ? "HOTEL" : "OPEN";
                var bp = f.At(b - 0.6f, 6.2f, 0.75f);
                var bq = Quaternion.Euler(0f, f.Yaw + 90f, 0f);
                k.Box(M("SignBoard"), bp, Mathf.Abs(f.dir.x) > 0.5f ? new Vector3(0.12f, 2.6f, 0.75f) : new Vector3(0.75f, 2.6f, 0.12f));
                // vertical text: quad rotated so the text reads top-to-bottom, on both sides
                foreach (float side in new[] { -1f, 1f })
                {
                    var n = f.dir * side;
                    var center = bp + n * 0.065f;
                    var yaw = Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg;
                    VerticalSign(k, blade, center, yaw, 0.6f, 2.4f);
                }
            }
            else if (hash % 3 == 0) k.Sign("OPEN", f.At(doorAt + (doorAt < c ? 1.1f : -1.1f), 2.0f, -0.12f), f.Yaw, 0.18f, 0.9f);
        }

        static void InteriorPanels(WorldKit k, Face f, float a, float b, float D, float y0, float y1, int variant)
        {
            float iw = b - a;
            int panels = Mathf.Max(1, Mathf.RoundToInt(iw / 3.2f));
            float pw = iw / panels, u0 = variant * 0.25f + 0.004f;
            for (int i = 0; i < panels; i++)
                k.WallQuad(M("ShopInterior"), f.origin, f.dir, f.normal, a + pw * i, a + pw * (i + 1), y0, y1, u0, 0.242f / pw, 0f, 1f / (y1 - y0), -D + 0.012f);
        }

        static void VerticalSign(WorldKit k, string text, Vector3 center, float yaw, float width, float height)
        {
            // atlas text is horizontal: rotate the quad 90° in its plane (text reads downwards)
            var q = Quaternion.Euler(0f, yaw, 0f);
            int i = Array.FindIndex(SignAtlas.Entries, e => e.text == text);
            if (i < 0) return;
            var (_, uv, _, fill) = SignAtlas.Entries[i];
            float u0 = uv.x + uv.width * (0.5f - fill * 0.5f), u1 = uv.x + uv.width * (0.5f + fill * 0.5f);
            // right axis points down, up axis points right -> text reads top to bottom
            k.Quad(M("Signs"), center, Vector3.down * (height * 0.5f), q * Vector3.right * (width * 0.5f), new Vector2(u0, uv.y), new Vector2(u1, uv.yMax));
        }

        static void Balconies(WorldKit k, Face f, int floors, float fh, float B, int bays)
        {
            const float GF = GroundFloorTop;
            int step = k.Rng.Next(2, 4), off = k.Rng.Next(step);
            bool glass = k.Chance(0.4f);
            for (int i = off; i < bays; i += step)
            {
                float s = (i + 0.5f) * B, w = B * 0.86f;
                for (int j = 1; j < floors; j++)
                {
                    float y = GF + j * fh;
                    k.Box(M("Stone"), f.At(s, y - 0.06f, 0.55f), f.Size(w, 0.14f, 1.1f));
                    if (glass) k.Box(M("GlassShop"), f.At(s, y + 0.5f, 1.06f), f.Size(w - 0.06f, 0.9f, 0.03f));
                    k.Box(M("Rail"), f.At(s, y + 1.0f, 1.07f), f.Size(w, 0.05f, 0.05f));
                    foreach (float e in new[] { -w * 0.5f + 0.03f, w * 0.5f - 0.03f })
                        k.Box(M("Rail"), f.At(s + e, y + 0.5f, 0.6f), f.Size(0.04f, 1.0f, 0.04f) + f.Size(0f, 0f, 0f));
                    k.Box(M("Rail"), f.At(s - w * 0.5f + 0.03f, y + 1.0f, 0.55f), f.Size(0.05f, 0.05f, 1.05f));
                    k.Box(M("Rail"), f.At(s + w * 0.5f - 0.03f, y + 1.0f, 0.55f), f.Size(0.05f, 0.05f, 1.05f));
                    if (!glass)
                        for (float x = -w * 0.5f + 0.15f; x < w * 0.5f - 0.1f; x += 0.14f)
                            k.Box(M("Rail"), f.At(s + x, y + 0.5f, 1.07f), f.Size(0.02f, 1.0f, 0.02f));
                    if (k.Chance(0.25f)) k.Box(M(k.Pick("AwningRed", "AwningGreen", "Wood", "BarrierWhite")), f.At(s - w * 0.3f, y + 0.25f, 0.8f), f.Size(0.4f, 0.5f, 0.4f));
                }
            }
        }

        /// <summary>Steel fire escape on a facade (alleys): landings each floor, ladders between, drop ladder above the street.</summary>
        public static void FireEscape(WorldKit k, Vector3 origin, Vector3 dir, Vector3 normal, float along, float width, int floors, float fh = 3.2f, float y0 = 4.35f)
        {
            var f = new Face { origin = origin, dir = dir, normal = normal, length = 0f };
            var m = M("Rail");
            for (int j = 0; j < floors; j++)
            {
                float y = y0 + j * fh;
                k.Box(m, f.At(along, y, 0.65f), f.Size(width, 0.05f, 1.2f));
                for (float x = -width * 0.5f; x <= width * 0.5f + 0.01f; x += 0.12f) k.Box(m, f.At(along + x, y + 0.5f, 1.24f), f.Size(0.02f, 1.0f, 0.02f));
                k.Box(m, f.At(along, y + 1.0f, 1.24f), f.Size(width, 0.05f, 0.05f));
                foreach (float e in new[] { -width * 0.5f, width * 0.5f }) k.Box(m, f.At(along + e, y + 1.0f, 0.65f), f.Size(0.05f, 0.05f, 1.2f));
                k.Box(m, f.At(along - width * 0.5f + 0.4f, y - 0.4f, 0.1f), f.Size(0.08f, 0.8f, 0.08f));
                k.Box(m, f.At(along + width * 0.5f - 0.4f, y - 0.4f, 0.1f), f.Size(0.08f, 0.8f, 0.08f));
                if (j < floors - 1)
                {   // inclined stair to the next landing
                    float run = width * 0.62f, rise = fh;
                    var slope = (dir * (j % 2 == 0 ? run : -run) + Vector3.up * rise).normalized;
                    var rot = Quaternion.LookRotation(slope, Vector3.Cross(slope, normal).y > 0f ? Vector3.Cross(slope, normal) : -Vector3.Cross(slope, normal));
                    var ctr = f.At(along, y + rise * 0.5f, 0.75f);
                    var sz = new Vector3(0.6f, 0.05f, Mathf.Sqrt(run * run + rise * rise));
                    k.Box(m, ctr, sz, rot);
                }
            }
            // drop ladder
            float ly = y0 - 0.2f;
            foreach (float e in new[] { -0.22f, 0.22f }) k.Box(m, f.At(along + width * 0.5f - 0.5f + e, ly - 1.0f, 1.2f), f.Size(0.04f, 2.0f, 0.04f));
            for (float y = ly - 1.9f; y < ly; y += 0.3f) k.Box(m, f.At(along + width * 0.5f - 0.5f, y, 1.2f), f.Size(0.44f, 0.03f, 0.03f));
        }

        static void Rooftop(WorldKit k, FacadeStyle st, float x0, float x1, float z0, float z1, float h)
        {
            float W = x1 - x0, Dz = z1 - z0;
            // stair / lift housing
            var c = new Vector3(k.Rand(x0 + 4f, x1 - 4f), h, k.Rand(z0 + 4f, z1 - 4f));
            if (W > 10f && Dz > 10f)
            {
                var sz = new Vector3(k.Rand(3f, 4.5f), k.Rand(2.6f, 3.4f), k.Rand(3f, 4f));
                k.Box(st.Wall, c + Vector3.up * sz.y * 0.5f, sz);
                k.Box(st.Trim, c + Vector3.up * (sz.y + 0.06f), sz + new Vector3(0.2f, -sz.y + 0.12f, 0.2f));
                k.Box(M("DoorService"), c + new Vector3(0f, 1.05f, -sz.z * 0.5f - 0.02f), new Vector3(0.9f, 2.0f, 0.05f));
            }
            // membrane patches + skylights + a duct run so big roofs are not one flat colour
            for (int i = 0; i < Mathf.Clamp((int)(W * Dz / 150f), 1, 8); i++)
                k.Box(M(k.Chance(0.5f) ? "RoofTar" : "Concrete"), new Vector3(k.Rand(x0 + 3f, x1 - 3f), h + 0.045f, k.Rand(z0 + 3f, z1 - 3f)),
                    new Vector3(k.Rand(2f, Mathf.Min(9f, W - 4f)), 0.012f, k.Rand(2f, Mathf.Min(7f, Dz - 4f))));
            for (int i = 0; i < Mathf.Clamp((int)(W * Dz / 400f), 0, 3); i++)
            {
                var sp = new Vector3(k.Rand(x0 + 3f, x1 - 3f), h, k.Rand(z0 + 3f, z1 - 3f));
                k.Box(M("MetalFrame"), sp + Vector3.up * 0.25f, new Vector3(2.2f, 0.5f, 1.4f));
                k.Box(M("GlassShop"), sp + Vector3.up * 0.55f, new Vector3(2.1f, 0.1f, 1.3f));
            }
            if (W > 14f) k.Box(M("RoofMetal"), new Vector3((x0 + x1) * 0.5f, h + 0.55f, c.z + 2.6f), new Vector3(W * k.Rand(0.4f, 0.7f), 0.6f, 0.7f));
            int units = Mathf.Clamp((int)(W * Dz / 120f), 2, 12);
            for (int i = 0; i < units; i++)
            {
                var p = new Vector3(k.Rand(x0 + 2f, x1 - 2f), h, k.Rand(z0 + 2f, z1 - 2f));
                if (Vector3.Distance(p, c) < 4f) continue;
                float r = (float)k.Rng.NextDouble();
                if (r < 0.55f)
                {   // condenser unit: casing + fan grille + pipes
                    var s = new Vector3(k.Rand(1.2f, 2.2f), k.Rand(0.9f, 1.3f), k.Rand(1.0f, 1.6f));
                    EnvironmentRealismAssets.Condenser(k, p, s);
                    k.Pipe(M("Pipe"), p + new Vector3(s.x * 0.5f, 0.3f, 0f), p + new Vector3(s.x * 0.5f + 1.2f, 0.3f, 0f), 0.05f);
                }
                else if (r < 0.75f && st.Residential)
                {   // timber water tank on legs
                    float tr = k.Rand(1.1f, 1.6f);
                    foreach (var o in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                        k.Box(M("Rail"), p + o * tr * 0.6f + Vector3.up * 1.0f, new Vector3(0.12f, 2f, 0.12f));
                    k.Box(M("Rail"), p + Vector3.up * 2.0f, new Vector3(tr * 1.6f, 0.1f, tr * 1.6f));
                    k.Cylinder(M("Wood"), p + Vector3.up * 2.05f, tr, 2.6f);
                    k.Cylinder(M("RoofMetal"), p + Vector3.up * 4.65f, tr + 0.08f, 0.12f);
                    k.Cylinder(M("RoofMetal"), p + Vector3.up * 4.77f, tr * 0.5f, 0.35f);
                }
                else if (r < 0.88f)
                {   // vent stacks
                    for (int v = 0; v < 3; v++) k.Cylinder(M("RoofMetal"), p + new Vector3(v * 0.6f, 0f, 0f), 0.15f, k.Rand(0.6f, 1.4f));
                }
                else
                {   // antenna / satellite dish
                    k.Cylinder(M("Rail"), p, 0.04f, k.Rand(3f, 6f));
                    k.Box(M("Rail"), p + Vector3.up * 2.5f, new Vector3(1.4f, 0.03f, 0.03f));
                    k.Box(M("Rail"), p + Vector3.up * 3.1f, new Vector3(1.0f, 0.03f, 0.03f));
                    k.Sphere(M("BarrierWhite"), p + new Vector3(1.2f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.25f));
                }
            }
        }

        // ================================================================== lamps: registry + light cones
        /// <summary>Every lamp placed this build: head position, outward direction, pole (true) or wall lamp.
        /// CityBuilder turns these into (culled) real lights; CityProps adds the fake volumetric cones.</summary>
        internal struct LampSite { public Vector3 head; public Vector3 outward; public bool pole; public Transform root; }
        public static readonly List<LampSite> Lamps = new();
        public static void ResetLamps() => Lamps.Clear();

        static Mesh coneMesh;
        /// <summary>Open unit cone: apex ring radius 0.08 at y = 0, radius 1 at y = -1; uv.y 0 (lamp) .. 1 (ground).</summary>
        static Mesh ConeMesh()
        {
            if (coneMesh) return coneMesh;
            const int seg = 20, rings = 5;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float f = r / (float)rings, rad = Mathf.Lerp(0.08f, 1f, f);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d * rad + Vector3.down * f);
                    n.Add((d + Vector3.up * 0.92f).normalized);
                    uv.Add(new Vector2(i / (float)seg, f));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = r * (seg + 1) + i, a1 = a0 + 1, b0 = a0 + seg + 1, b1 = b0 + 1;
                    t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            coneMesh = new Mesh { name = "LampCone" };
            coneMesh.SetVertices(v); coneMesh.SetNormals(n); coneMesh.SetUVs(0, uv); coneMesh.SetTriangles(t, 0);
            return coneMesh;
        }

        /// <summary>Fake volumetric light cone hanging from <paramref name="head"/> down to the ground at <paramref name="groundY"/>.</summary>
        public static void LightCone(WorldKit k, Vector3 head, float groundY, float groundRadius, string mat = "LightCone")
        {
            float h = Mathf.Max(0.5f, head.y - groundY);
            k.Mesh(M(mat), ConeMesh(), Matrix4x4.TRS(head, Quaternion.identity, new Vector3(groundRadius, h, groundRadius)));
        }

        /// <summary>String of festoon bulbs hanging between two points (catenary-ish sag). Bulbs are emissive (bloom),
        /// a few points along the string are registered as small lamp lights.</summary>
        public static void Festoon(WorldKit k, Vector3 a, Vector3 b, float sag, float spacing = 0.65f, int lights = 1)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            var wire = M("Rail"); var bulb = Has("FestoonBulb") ? M("FestoonBulb") : M("LampHead");
            var prev = P(0f);
            for (int i = 1; i <= n; i++)
            {
                var cur = P(i / (float)n);
                var d = cur - prev;
                k.Box(wire, (prev + cur) * 0.5f, new Vector3(0.018f, 0.018f, d.magnitude + 0.01f), Quaternion.LookRotation(d.normalized, Vector3.up));
                var bp = cur + Vector3.down * 0.07f;
                k.Box(bulb, bp, new Vector3(0.055f, 0.08f, 0.055f), Quaternion.Euler(0f, 45f, 0f));
                prev = cur;
            }
            for (int i = 0; i < lights; i++)
            {
                float t = (i + 1f) / (lights + 1f);
                Lamps.Add(new LampSite { head = P(t) + Vector3.down * 0.25f, outward = Vector3.down, pole = false, root = k.Root });
            }
        }

        // ================================================================== street furniture
        public static void Lamp(WorldKit k, Vector3 p, Vector3 toRoad)
        {
            var head = p + Vector3.up * 4.72f;
            Lamps.Add(new LampSite { head = head, outward = toRoad, pole = true, root = k.Root });
            if (Has("LightCone")) LightCone(k, head + Vector3.down * 0.25f, p.y, 2.3f);
            float yaw = Mathf.Atan2(toRoad.x, toRoad.z) * Mathf.Rad2Deg;
            var pm = Prop("street_lamp_01");
            if (pm != null)
            {
                float s = 5.2f / Mathf.Max(0.5f, pm.Bounds.size.y);
                k.Prop("street_lamp_01", p, yaw, s);
            }
            else
            {
                var md = M("MetalDark");
                k.Box(md, p + Vector3.up * 0.25f, new Vector3(0.36f, 0.5f, 0.36f));
                k.Cylinder(md, p, 0.08f, 6.1f);
                var lantern = p + Vector3.up * 5.98f + toRoad * 1.55f;
                k.Box(md, lantern, new Vector3(0.55f, 0.14f, 0.55f));
                k.Box(M("LampHead"), lantern + Vector3.down * 0.09f, new Vector3(0.44f, 0.04f, 0.44f));
            }
            k.Solid(p + Vector3.up * 1.5f, new Vector3(0.32f, 3f, 0.32f));
        }

        public static void WallLamp(WorldKit k, Vector3 p, float yaw)
        {
            if (Prop("street_lamp_02") != null)
            {
                var pm = Prop("street_lamp_02");
                float s = 0.9f / Mathf.Max(0.2f, pm.Bounds.size.y);
                k.Prop("street_lamp_02", p + Vector3.down * 0.45f, yaw + 180f, Mathf.Clamp(s, 0.2f, 1.5f));
            }
            var outward = -(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            var head = p + outward * 0.32f + Vector3.down * 0.12f;
            Lamps.Add(new LampSite { head = head, outward = outward, pole = false, root = k.Root });
            if (Has("LightCone")) LightCone(k, head + Vector3.down * 0.1f, Sidewalk, 1.25f);
            else k.Box(M("LampHead"), p, new Vector3(0.2f, 0.25f, 0.2f));
        }

        /// <summary>Street tree: steel-grated pit + a wind-animated Blender tree (3 species, LOD0..2, see
        /// Tools/blender/environment/trees.py). The old card tree's random draws are replayed exactly (without geometry)
        /// so every later ShopSeed-independent decoration keeps its place; species/yaw/size come from a position hash.</summary>
        public static void Tree(WorldKit k, Vector3 p, float scale = 1f)
        {
            // tree pit: steel grate + soil + low weeds
            k.Box(M("Soil"), p + Vector3.up * 0.006f, new Vector3(1.45f, 0.012f, 1.45f));
            k.Box(M("MetalDark"), p + Vector3.up * 0.016f, new Vector3(1.5f, 0.012f, 0.08f));
            k.Box(M("MetalDark"), p + new Vector3(0f, 0.016f, 0.71f), new Vector3(1.5f, 0.012f, 0.08f));
            k.Box(M("MetalDark"), p + new Vector3(0f, 0.016f, -0.71f), new Vector3(1.5f, 0.012f, 0.08f));
            k.Box(M("MetalDark"), p + new Vector3(0.71f, 0.016f, 0f), new Vector3(0.08f, 0.012f, 1.5f));
            k.Box(M("MetalDark"), p + new Vector3(-0.71f, 0.016f, 0f), new Vector3(0.08f, 0.012f, 1.5f));
            if (Prop("shrub_04") != null && k.Chance(0.5f)) k.Prop("shrub_04", p + new Vector3(k.Rand(-0.4f, 0.4f), 0f, k.Rand(-0.4f, 0.4f)), k.Rand(0f, 360f), k.Rand(0.6f, 0.9f));

            float hgt = k.Rand(3.0f, 3.6f) * scale;
            bool autumn = k.Chance(0.18f);
            var crown = p + Vector3.up * (hgt + 1.6f * scale);
            float rx = k.Rand(2.0f, 2.6f) * scale, ry = k.Rand(1.9f, 2.4f) * scale;
            for (int i = 0; i < 4; i++) k.Rand(-25f, 25f);                       // (old main limbs)
            int cards = (int)(44 * scale * scale);
            for (int i = 0; i < cards; i++)                                       // (old leaf cards)
            {
                var dir = new Vector3(k.Rand(-1f, 1f), k.Rand(-1f, 1f), k.Rand(-1f, 1f)); if (dir.sqrMagnitude < 0.01f) dir = Vector3.up; dir.Normalize();
                float rr = Mathf.Pow((float)k.Rng.NextDouble(), 0.4f);
                var pos = crown + new Vector3(dir.x * rx, dir.y * ry * 0.85f, dir.z * rx) * rr * 0.82f;
                if (pos.y < p.y + hgt * 0.8f) k.Rand(0f, 0.6f);
                k.Rand(1.3f, 1.9f); k.Rand(-70f, 70f); k.Rand(0f, 360f); k.Rand(0f, 360f); k.Rand(0.6f, 1f);
            }

            uint h = PosHash(p);
            EnvironmentRealismAssets.TreeBed(k, p, h);
            float h0 = (h & 0xffff) / 65535f, h1 = ((h >> 16) & 0xffff) / 65535f;
            string species = autumn ? "Maple" : h0 < 0.58f ? "Plane" : "Linden";
            string leaves = species == "Linden" && h1 > 0.78f ? "Yellow" : null;
            if (Trees.TryGetValue(species, out var model))
            {
                float s = scale * (0.66f + 0.14f * h1) * (species == "Linden" ? 0.95f : 1f);   // ~7-8 m street trees
                k.Instance(model, "Trees", p, h0 * 360f * 7.31f % 360f, s, m => TreeMaterial(m, leaves), new[] { 0.32f, 0.11f, 0.018f });
            }
            else
            {
                var trunk = M("Trunk");
                k.Cylinder(trunk, p, 0.15f * scale, hgt);
                k.Sphere(M(autumn ? "FoliageAutumn" : "Foliage"), crown, new Vector3(rx, ry, rx) * 1.6f);
            }
            // a few grass tufts in the pit soil (hash placed: no shared RNG)
            if (Has("GrassCard"))
                for (int i = 0; i < 3; i++)
                {
                    float a = (h0 * 6.283f) + i * 2.1f, r = 0.32f + 0.18f * ((h >> (i * 5)) & 7) / 7f;
                    var c = p + new Vector3(Mathf.Cos(a) * r, 0.17f, Mathf.Sin(a) * r);
                    float sz = 0.34f + 0.06f * i;
                    for (int j = 0; j < 2; j++)
                    {
                        var q = Quaternion.Euler(0f, a * 57.3f + j * 90f, 0f);
                        k.Quad(M("GrassCard"), c, q * Vector3.right * sz * 0.5f, Vector3.up * sz * 0.5f, new Vector2(0, 0), new Vector2(1, 1));
                        k.Quad(M("GrassCard"), c, q * Vector3.left * sz * 0.5f, Vector3.up * sz * 0.5f, new Vector2(1, 0), new Vector2(0, 1));
                    }
                }
            k.Solid(p + Vector3.up * 1.4f, new Vector3(0.42f, 2.8f, 0.42f));
        }

        static uint PosHash(Vector3 p)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(p.x * 10f) * 73856093u ^ (uint)Mathf.RoundToInt(p.z * 10f) * 19349663u ^ (uint)Mathf.RoundToInt(p.y * 10f) * 83492791u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return h;
            }
        }

        static void LeafCard(WorldKit k, Material m, Vector3 c, Vector3 right, Vector3 up, Vector3 crown, float bend)
        {
            // like WorldKit.Quad but with normals bent towards "away from the crown centre" for soft volumetric shading
            k.LeafQuad(m, c, right, up, crown);
        }

        /// <summary>Bench whose sitter faces <paramref name="facing"/> (axis aligned).</summary>
        public static void Bench(WorldKit k, Vector3 p, Vector3 facing)
        {
            bool fx = Mathf.Abs(facing.x) > 0.5f;
            Vector3 S(float along, float y, float depth) => fx ? new Vector3(depth, y, along) : new Vector3(along, y, depth);
            var wood = M("Wood"); var md = M("MetalDark");
            for (int i = 0; i < 3; i++) k.Box(wood, p + Vector3.up * 0.45f + facing * (0.16f - i * 0.16f), S(1.9f, 0.05f, 0.13f));
            for (int i = 0; i < 2; i++) k.Box(wood, p + Vector3.up * (0.68f + i * 0.17f) - facing * 0.27f, S(1.9f, 0.12f, 0.04f));
            foreach (float a in new[] { -0.8f, 0.8f })
            {
                var o = fx ? new Vector3(0, 0, a) : new Vector3(a, 0, 0);
                k.Box(md, p + o + Vector3.up * 0.22f, S(0.06f, 0.44f, 0.46f));
                k.Box(md, p + o + Vector3.up * 0.65f - facing * 0.27f, S(0.06f, 0.5f, 0.05f));
                k.Box(md, p + o + Vector3.up * 0.62f + facing * 0.05f, S(0.06f, 0.04f, 0.5f));
            }
            k.Solid(p + Vector3.up * 0.5f, S(1.95f, 1f, 0.6f));
        }

        public static void Barrier(WorldKit k, Vector3 a, Vector3 b)
        {
            var d = b - a; float len = d.magnitude; var dir = d / len;
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            Vector3 S(float l, float hgt, float t) => alongX ? new Vector3(l, hgt, t) : new Vector3(t, hgt, l);
            var cb = Prop("concrete_road_barrier_02");
            if (cb != null)
            {   // concrete jersey barriers + a flashing light every other block
                float bl = Mathf.Max(cb.Bounds.size.x, cb.Bounds.size.z);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / (bl + 0.1f)));
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (cb.Bounds.size.x > cb.Bounds.size.z ? 90f : 0f);
                for (int i = 0; i < n; i++)
                {
                    var c = a + dir * ((i + 0.5f) * len / n);
                    var wb = k.Prop("concrete_road_barrier_02", c, yaw, 1f);
                    if (i % 2 == 0 && wb.HasValue) k.Box(M("WarningLight"), c + Vector3.up * (wb.Value.size.y + 0.1f), new Vector3(0.16f, 0.16f, 0.16f));
                }
                k.Solid((a + b) * 0.5f + Vector3.up * 0.6f, S(len, 1.2f, 0.6f));
                return;
            }
            int m = Mathf.Max(1, Mathf.RoundToInt(len / 2f)); float seg = len / m;
            for (int i = 0; i < m; i++)
            {
                var c = a + dir * (seg * (i + 0.5f));
                bool o = i % 2 == 0;
                k.Box(M(o ? "BarrierOrange" : "BarrierWhite"), c + Vector3.up * 0.85f, S(seg - 0.1f, 0.3f, 0.06f));
                k.Box(M(o ? "BarrierWhite" : "BarrierOrange"), c + Vector3.up * 0.4f, S(seg - 0.1f, 0.25f, 0.06f));
                foreach (float s in new[] { -0.45f, 0.45f })
                    k.Box(M("MetalDark"), c + dir * (s * seg) + Vector3.up * 0.5f, S(0.06f, 1f, 0.5f));
                if (o) k.Box(M("WarningLight"), c + Vector3.up * 1.1f, new Vector3(0.16f, 0.16f, 0.16f));
            }
            k.Solid((a + b) * 0.5f + Vector3.up * 0.6f, S(len, 1.2f, 0.5f));
        }

        public static void Hoarding(WorldKit k, Vector3 a, Vector3 b, float height = 2.6f)
        {
            var d = b - a; float len = d.magnitude; var dir = d / len;
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            Vector3 S(float l, float hgt, float t) => alongX ? new Vector3(l, hgt, t) : new Vector3(t, hgt, l);
            k.Box(M("Hoarding"), (a + b) * 0.5f + Vector3.up * height * 0.5f, S(len, height, 0.08f));
            for (float s = 0; s <= len + 0.01f; s += 2.4f)
                k.Box(M("MetalDark"), a + dir * s + Vector3.up * height * 0.5f, S(0.1f, height + 0.1f, 0.14f));
            k.Box(M("BarrierOrange"), (a + b) * 0.5f + Vector3.up * (height - 0.25f), S(len, 0.18f, 0.1f));
            // tags on both sides
            var n = alongX ? Vector3.forward : Vector3.right;
            for (float s = 1.5f; s < len - 1.5f; s += k.Rand(3f, 6f))
                foreach (float side in new[] { -1f, 1f })
                    if (k.Chance(0.5f)) Graffiti(k, a + dir * s + Vector3.up * k.Rand(1.0f, 1.5f) + n * side * 0.05f, n * side, k.Rand(1.2f, 2f));
            k.Solid((a + b) * 0.5f + Vector3.up * height * 0.5f, S(len, height, 0.3f));
        }

        public static void Dumpster(WorldKit k, Vector3 p, bool alongX)
        {
            var s = alongX ? new Vector3(1.9f, 1.15f, 1.1f) : new Vector3(1.1f, 1.15f, 1.9f);
            k.Box(M("DumpsterGreen"), p + Vector3.up * 0.65f, s);
            k.Box(M("DumpsterGreen"), p + Vector3.up * 0.65f, s + (alongX ? new Vector3(-0.2f, -0.2f, 0.08f) : new Vector3(0.08f, -0.2f, -0.2f)));
            k.Box(M("MetalDark"), p + Vector3.up * 1.26f, s + new Vector3(0.06f, -1.08f, 0.06f));
            k.Box(M("MetalDark"), p + Vector3.up * 0.05f, s + new Vector3(-0.2f, -1.05f, -0.2f));
            k.Solid(p + Vector3.up * 0.65f, s + new Vector3(0, 0.1f, 0));
            if (Prop("trashbag") != null)
                for (int i = 0; i < 3; i++)
                    k.Prop("trashbag", p + (alongX ? new Vector3(k.Rand(-0.9f, 0.9f), 0, -0.9f) : new Vector3(-0.9f, 0, k.Rand(-0.9f, 0.9f))) * 1f, k.Rand(0, 360), k.Rand(0.9f, 1.2f));
        }

        public static void Crate(WorldKit k, Vector3 p, Vector3 size, string mat = "Wood")
        {
            string id = mat == "Wood" ? "wooden_crate_01" : null;
            var pm = id != null ? Prop(id) : null;
            if (pm != null)
            {
                _ = k.Rand(-8f, 8f); // Preserve the previous yaw draw so later trees/roof details keep their seeded variation.
                // The source crate is a wide, shallow chest. Scaling only to its height made a requested 1.1 m
                // crate over 2.3 m wide while its collider stayed 1.1 m. Match all three gameplay dimensions.
                var bounds = pm.Bounds.size;
                var scale = new Vector3(size.x / Mathf.Max(0.001f, bounds.x), size.y / Mathf.Max(0.001f, bounds.y), size.z / Mathf.Max(0.001f, bounds.z));
                k.Prop(id, p, scale);
            }
            else k.Box(M(mat), p + Vector3.up * size.y * 0.5f, size);
            k.Solid(p + Vector3.up * size.y * 0.5f, size);
        }

        // ------------------------------------------------------------------ small props (Poly Haven, with fallbacks)
        public static void Hydrant(WorldKit k, Vector3 p, float yaw)
        {
            if (k.Prop("fire_hydrant", p, yaw, ScaleTo("fire_hydrant", 0.8f)) == null) k.Cylinder(M("PipeRed"), p, 0.14f, 0.75f);
            k.Solid(p + Vector3.up * 0.4f, new Vector3(0.4f, 0.8f, 0.4f));
        }

        public static void TrashCan(WorldKit k, Vector3 p)
        {
            if (k.Prop("metal_trash_can", p, k.Rand(0f, 360f), ScaleTo("metal_trash_can", 0.95f)) == null)
            {
                k.Cylinder(M("MetalDark"), p, 0.28f, 0.95f);
                k.Cylinder(M("Chrome"), p + Vector3.up * 0.95f, 0.3f, 0.05f);
            }
            k.Solid(p + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f));
        }

        public static void UtilityBox(WorldKit k, Vector3 p, float yaw, bool big = false)
        {
            string id = big ? "utility_box_02" : "utility_box_01";
            var wb = k.Prop(id, p, yaw, ScaleTo(id, big ? 1.6f : 1.35f));
            if (wb == null) k.Box(M("MetalDark"), p + Vector3.up * 0.7f, new Vector3(0.8f, 1.4f, 0.4f));
            var sz = wb?.size ?? new Vector3(0.8f, 1.4f, 0.4f);
            k.Solid(p + Vector3.up * sz.y * 0.5f, sz * 0.95f);
        }

        public static void TrashPile(WorldKit k, Vector3 p, int n = 4)
        {
            for (int i = 0; i < n; i++)
            {
                var o = new Vector3(k.Rand(-0.8f, 0.8f), 0f, k.Rand(-0.8f, 0.8f));
                if (k.Chance(0.6f)) k.Prop("trashbag", p + o, k.Rand(0, 360), k.Rand(0.8f, 1.15f));
                else k.Prop("cardboard_box_01", p + o, k.Rand(0, 360), ScaleTo("cardboard_box_01", k.Rand(0.35f, 0.55f)));
            }
        }

        public static void Planter(WorldKit k, Vector3 p, float yaw)
        {
            var wb = k.Prop("planter_box_01", p, yaw, ScaleTo("planter_box_01", 0.55f));
            if (wb == null) { k.Box(M("Wood"), p + Vector3.up * 0.3f, new Vector3(1.2f, 0.6f, 0.5f)); return; }
            var b = wb.Value;
            k.Box(M("Soil"), new Vector3(p.x, b.max.y - 0.08f, p.z), new Vector3(b.size.x * 0.85f, 0.02f, b.size.z * 0.85f));
            for (int i = 0; i < 2; i++)
                k.Prop("shrub_04", new Vector3(p.x, b.max.y - 0.1f, p.z) + Quaternion.Euler(0, yaw, 0) * new Vector3((i - 0.5f) * b.size.x * 0.45f, 0, 0), k.Rand(0, 360), k.Rand(0.5f, 0.7f));
            k.Solid(b.center, b.size * 0.95f);
        }

        public static void Barrel(WorldKit k, Vector3 p)
        {
            if (k.Prop("barrel_03", p, k.Rand(0, 360), ScaleTo("barrel_03", 0.9f)) == null) k.Cylinder(M("PipeRed"), p, 0.3f, 0.9f);
            k.Solid(p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f));
        }

        public static void CoveredCar(WorldKit k, Vector3 p, float yaw)
        {
            var wb = k.Prop("covered_car", p, yaw, ScaleTo("covered_car", 1.45f));
            if (wb == null) { Car(k, p, Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) < 0.5f, "CarSilver"); return; }
            k.Solid(wb.Value.center, wb.Value.size * 0.92f);
        }

        public static void AirCon(WorldKit k, Vector3 wallPoint, float yaw)
        {
            if (k.Prop("exterior_aircon_unit", wallPoint, yaw, ScaleTo("exterior_aircon_unit", 0.6f)) == null)
                k.Box(M("BarrierWhite"), wallPoint + Vector3.up * 0.3f, new Vector3(0.8f, 0.6f, 0.3f));
        }

        public static void SecurityCamera(WorldKit k, Vector3 p, float yaw) => k.Prop("security_camera_01", p, yaw, ScaleTo("security_camera_01", 0.35f));

        public static void SecurityLight(WorldKit k, Vector3 p, float yaw) => k.Prop("security_light", p, yaw, ScaleTo("security_light", 0.45f));

        public static void Shutter(WorldKit k, Vector3 p, float yaw, float width, bool door = true)
        {
            string id = door ? "rollershutter_door" : "rollershutter_window_01";
            var pm = Prop(id);
            if (pm == null) { k.Box(M("RustyMetal"), p + Vector3.up * 1.3f, Quaternion.Euler(0, yaw, 0) * new Vector3(width, 2.6f, 0.08f)); return; }
            float s = width / Mathf.Max(0.3f, Mathf.Max(pm.Bounds.size.x, pm.Bounds.size.z));
            k.Prop(id, p, yaw, s);
        }

        public static void Tyre(WorldKit k, Vector3 p) => k.Prop("old_tyre", p, k.Rand(0, 360), ScaleTo("old_tyre", 0.25f));

        public static void PlasticCrates(WorldKit k, Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
                k.Prop("plastic_crate_02", p + Vector3.up * (i * 0.3f), k.Rand(-10f, 10f), ScaleTo("plastic_crate_02", 0.3f));
        }

        static float ScaleTo(string id, float height)
        {
            var pm = Prop(id);
            return pm == null ? 1f : height / Mathf.Max(0.05f, pm.Bounds.size.y);
        }

        // ------------------------------------------------------------------ decals
        public static void Graffiti(WorldKit k, Vector3 center, Vector3 normal, float size)
        {
            int cell = k.Rng.Next(16);
            var r = new Rect((cell % 4) * 0.25f, (cell / 4) * 0.25f, 0.25f, 0.25f);
            var right = Vector3.Cross(Vector3.up, normal).normalized * (size * 0.5f);
            k.Quad(M("DecalGraffiti"), center + normal * 0.01f, -right, Vector3.up * (size * 0.5f), r.min, r.max);
        }

        public static void RoadDamage(WorldKit k, Vector3 c, float length, float yaw)
        {
            int band = k.Rng.Next(4);
            k.FloorQuad(M("DecalAsphaltDamage"), c + Vector3.up * 0.004f, length, length * 0.25f, yaw, new Rect(0f, band * 0.25f, 1f, 0.25f));
        }

        public static void Puddle(WorldKit k, Vector3 c, float sx, float sz, float yaw) =>
            k.FloorQuad(M("DecalPuddle"), c + Vector3.up * 0.002f, sx, sz, yaw, new Rect(0, 0, 1, 1));

        public static void Manhole(WorldKit k, Vector3 c, float size = 0.95f) =>
            k.FloorQuad(M("DecalManhole"), c + Vector3.up * 0.007f, size, size, 0f, new Rect(0, 0, 1, 1));

        // ================================================================== vehicles
        public static void Car(WorldKit k, Vector3 p, bool alongZ, string paint) => Vehicles.Car(k, p, alongZ ? 0f : 90f, paint, Vehicles.PickBody(k, paint));

        public static void Truck(WorldKit k, Vector3 p, bool alongZ, string paint, string label = null) => Vehicles.Truck(k, p, alongZ ? 0f : 90f, paint, label);

        // ================================================================== lab furniture
        public static void Tank(WorldKit k, Vector3 p, float r, float h)
        {
            k.Cylinder(M("MetalDark"), p, r + 0.12f, 0.3f);
            k.Cylinder(M("TankLiquid"), p + Vector3.up * 0.3f, r - 0.07f, h * 0.82f);
            k.Cylinder(M("GlassTank"), p + Vector3.up * 0.3f, r, h);
            k.Cylinder(M("SteelPlate"), p + Vector3.up * (h + 0.3f), r + 0.08f, 0.22f);
            k.Pipe(M("Pipe"), p + Vector3.up * (h + 0.52f), new Vector3(p.x, 4.4f, p.z), 0.06f);
            k.Sphere(M("Trunk"), p + Vector3.up * (0.3f + h * 0.45f), new Vector3(r * 0.9f, r * 1.1f, r * 0.8f));
            k.Solid(p + Vector3.up * (h + 0.5f) * 0.5f, new Vector3(2f * r + 0.25f, h + 0.5f, 2f * r + 0.25f));
        }

        public static void Monitor(WorldKit k, Vector3 deskTop, Vector3 facing, float width = 0.6f)
        {
            bool fx = Mathf.Abs(facing.x) > 0.5f;
            Vector3 S(float w, float hgt, float d) => fx ? new Vector3(d, hgt, w) : new Vector3(w, hgt, d);
            k.Box(M("MetalDark"), deskTop + Vector3.up * 0.01f, S(0.25f, 0.02f, 0.2f));
            k.Box(M("MetalDark"), deskTop + Vector3.up * 0.18f - facing * 0.03f, S(0.05f, 0.34f, 0.04f));
            k.Box(M("MetalDark"), deskTop + Vector3.up * 0.4f, S(width + 0.04f, width * 0.6f + 0.04f, 0.04f));
            k.Box(M("Screen"), deskTop + Vector3.up * 0.4f + facing * 0.022f, S(width, width * 0.6f, 0.01f));
        }

        /// <summary>Lab bench (cabinet + counter) with monitors facing <paramref name="facing"/>.</summary>
        public static void LabBench(WorldKit k, float x0, float x1, float z0, float z1, float floor, Vector3 facing, int monitors)
        {
            k.BoxMM(M("LabPlastic"), x0, x1, floor, floor + 0.85f, z0, z1);
            k.BoxMM(M("LabCounter"), x0 - 0.04f, x1 + 0.04f, floor + 0.85f, floor + 0.92f, z0 - 0.04f, z1 + 0.04f, false);
            bool fx = Mathf.Abs(facing.x) > 0.5f;
            for (int i = 0; i < monitors; i++)
            {
                float t = (i + 0.5f) / monitors;
                var top = new Vector3(fx ? (x0 + x1) * 0.5f : Mathf.Lerp(x0, x1, t), floor + 0.92f, fx ? Mathf.Lerp(z0, z1, t) : (z0 + z1) * 0.5f);
                Monitor(k, top, facing);
                k.Box(M("SteelPlate"), top + Vector3.up * 0.1f + (fx ? Vector3.forward : Vector3.right) * 0.45f, new Vector3(0.25f, 0.2f, 0.25f));
            }
        }
    }
}
