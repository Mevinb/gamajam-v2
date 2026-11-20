using System;
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

    /// <summary>Reusable props: facades, street furniture, vehicles, lab furniture. All static, merged by WorldKit.</summary>
    internal static class CityProps
    {
        static readonly Color NeonText = new(1f, 0.93f, 0.8f);

        struct Face
        {
            public Vector3 origin, dir, normal; public float length;
            public Vector3 At(float along, float y, float off) => origin + dir * along + Vector3.up * y + normal * off;
            public Vector3 Size(float w, float h, float d) => Mathf.Abs(dir.x) > 0.5f ? new Vector3(w, h, d) : new Vector3(d, h, w);
            public float Yaw => Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
        }

        static Face GetFace(Faces f, float x0, float x1, float z0, float z1) => f switch
        {
            Faces.N => new Face { origin = new Vector3(x0, 0, z1), dir = Vector3.right, normal = Vector3.forward, length = x1 - x0 },
            Faces.S => new Face { origin = new Vector3(x0, 0, z0), dir = Vector3.right, normal = Vector3.back, length = x1 - x0 },
            Faces.E => new Face { origin = new Vector3(x1, 0, z0), dir = Vector3.forward, normal = Vector3.right, length = z1 - z0 },
            _ => new Face { origin = new Vector3(x0, 0, z0), dir = Vector3.forward, normal = Vector3.left, length = z1 - z0 },
        };

        // ------------------------------------------------------------------ buildings
        public static void Building(WorldKit k, float x0, float x1, float z0, float z1, float h, Material wall, Faces faces, params Shop[] shops)
        {
            k.BoxMM(wall, x0, x1, 0f, h, z0, z1);
            k.BoxMM(M("Concrete"), x0 - 0.06f, x1 + 0.06f, 0f, 0.6f, z0 - 0.06f, z1 + 0.06f, false);
            k.BoxMM(M("Curb"), x0 - 0.12f, x1 + 0.12f, 3.9f, 4.15f, z0 - 0.12f, z1 + 0.12f, false);
            k.BoxMM(M("Curb"), x0 - 0.3f, x1 + 0.3f, h - 0.55f, h + 0.06f, z0 - 0.3f, z1 + 0.3f, false);
            k.BoxMM(M("Concrete"), x0 + 0.3f, x1 - 0.3f, h, h + 0.15f, z0 + 0.3f, z1 - 0.3f, false);
            int units = Mathf.Clamp((int)((x1 - x0) * (z1 - z0) / 300f), 1, 4);
            for (int i = 0; i < units; i++)
            {
                var p = new Vector3(k.Rand(x0 + 3f, x1 - 3f), h + 0.15f, k.Rand(z0 + 3f, z1 - 3f));
                k.Box(M(k.Pick("MetalDark", "Concrete", "SteelPlate")), p + Vector3.up * 0.7f, new Vector3(k.Rand(1.5f, 3.5f), 1.4f, k.Rand(1.5f, 3f)));
            }
            foreach (Faces f in new[] { Faces.N, Faces.S, Faces.E, Faces.W })
            {
                if ((faces & f) == 0) continue;
                var face = GetFace(f, x0, x1, z0, z1);
                Windows(k, face, 4.6f, h - 1f);
                foreach (var s in shops) if (s.face == f) ShopFront(k, face, s);
            }
        }

        static void Windows(WorldKit k, Face f, float yStart, float yEnd)
        {
            int n = Mathf.FloorToInt((f.length - 1.5f) / 3f);
            if (n < 1) return;
            float spacing = f.length / n;
            var dark = M("GlassDark"); var warm = M("WindowWarm"); var cool = M("WindowCool"); var sill = M("Curb");
            for (float y = yStart; y + 1.9f < yEnd; y += 3.3f)
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) * spacing;
                var glass = k.Chance(0.28f) ? (k.Chance(0.75f) ? warm : cool) : dark;
                k.Box(glass, f.At(a, y + 0.9f, 0.03f), f.Size(1.3f, 1.8f, 0.1f));
                k.Box(sill, f.At(a, y - 0.06f, 0.08f), f.Size(1.55f, 0.12f, 0.22f));
            }
        }

        static void ShopFront(WorldKit k, Face f, Shop s)
        {
            float w = s.width;
            k.Box(M("ShopGlow"), f.At(s.along, 1.95f, 0.04f), f.Size(w, 2.7f, 0.12f));
            for (float m = -w * 0.5f; m <= w * 0.5f + 0.01f; m += Mathf.Max(1.5f, w / Mathf.Round(w / 2.2f)))
                k.Box(M("MetalDark"), f.At(s.along + m, 1.95f, 0.1f), f.Size(0.1f, 2.75f, 0.06f));
            k.Box(M("MetalDark"), f.At(s.along, 3.33f, 0.1f), f.Size(w + 0.1f, 0.1f, 0.08f));
            k.Box(M("GlassDark"), f.At(s.along, 1.55f, 0.11f), f.Size(1.3f, 2.2f, 0.05f));
            k.Box(M(s.awning), f.At(s.along, 3.5f, 0.7f), f.Size(w + 0.3f, 0.1f, 1.4f));
            k.Box(M(s.awning), f.At(s.along, 3.32f, 1.38f), f.Size(w + 0.3f, 0.36f, 0.04f));
            k.Board(s.name, f.At(s.along, 4.55f, 0.22f), f.Yaw, Mathf.Min(w, s.name.Length * 0.55f + 1.2f), 0.75f, NeonText, M(s.neon));
        }

        // ------------------------------------------------------------------ street furniture
        public static void Lamp(WorldKit k, Vector3 p, Vector3 toRoad)
        {
            var md = M("MetalDark");
            k.Box(md, p + Vector3.up * 0.25f, new Vector3(0.36f, 0.5f, 0.36f));
            k.Cylinder(md, p, 0.08f, 6.1f);
            var arm = p + Vector3.up * 6.05f + toRoad * 0.8f;
            k.Box(md, arm, Mathf.Abs(toRoad.x) > 0.5f ? new Vector3(1.6f, 0.08f, 0.08f) : new Vector3(0.08f, 0.08f, 1.6f));
            var head = p + Vector3.up * 5.98f + toRoad * 1.55f;
            k.Box(md, head, new Vector3(0.55f, 0.14f, 0.55f));
            k.Box(M("LampHead"), head + Vector3.down * 0.09f, new Vector3(0.44f, 0.04f, 0.44f));
            k.Solid(p + Vector3.up * 1.5f, new Vector3(0.32f, 3f, 0.32f));
        }

        public static void Tree(WorldKit k, Vector3 p, float scale = 1f)
        {
            k.Box(M("MetalDark"), p + Vector3.up * 0.012f, new Vector3(1.5f, 0.024f, 1.5f));
            k.Cylinder(M("Trunk"), p, 0.16f * scale, 3.4f * scale);
            var fol = M(k.Chance(0.25f) ? "FoliageAutumn" : "Foliage");
            k.Sphere(fol, p + Vector3.up * 4.3f * scale, new Vector3(3.6f, 3f, 3.6f) * scale);
            k.Sphere(fol, p + new Vector3(k.Rand(-0.6f, 0.6f), 5.4f, k.Rand(-0.6f, 0.6f)) * scale, new Vector3(2.6f, 2.2f, 2.6f) * scale);
            k.Sphere(fol, p + new Vector3(k.Rand(-1f, 1f), 3.7f, k.Rand(-1f, 1f)) * scale, new Vector3(2.2f, 1.8f, 2.2f) * scale);
            k.Solid(p + Vector3.up * 1.4f, new Vector3(0.42f, 2.8f, 0.42f));
        }

        /// <summary>Bench whose sitter faces <paramref name="facing"/> (axis aligned).</summary>
        public static void Bench(WorldKit k, Vector3 p, Vector3 facing)
        {
            bool fx = Mathf.Abs(facing.x) > 0.5f;
            Vector3 S(float along, float y, float depth) => fx ? new Vector3(depth, y, along) : new Vector3(along, y, depth);
            var wood = M("Wood"); var md = M("MetalDark");
            k.Box(wood, p + Vector3.up * 0.45f, S(1.9f, 0.07f, 0.5f));
            k.Box(wood, p + Vector3.up * 0.78f - facing * 0.24f, S(1.9f, 0.4f, 0.06f));
            foreach (float a in new[] { -0.8f, 0.8f })
            {
                var o = fx ? new Vector3(0, 0, a) : new Vector3(a, 0, 0);
                k.Box(md, p + o + Vector3.up * 0.22f, S(0.06f, 0.44f, 0.46f));
                k.Box(md, p + o + Vector3.up * 0.65f - facing * 0.24f, S(0.06f, 0.5f, 0.05f));
            }
            k.Solid(p + Vector3.up * 0.5f, S(1.95f, 1f, 0.6f));
        }

        public static void Barrier(WorldKit k, Vector3 a, Vector3 b)
        {
            var d = b - a; float len = d.magnitude; var dir = d / len;
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            Vector3 S(float l, float hgt, float t) => alongX ? new Vector3(l, hgt, t) : new Vector3(t, hgt, l);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2f)); float seg = len / n;
            for (int i = 0; i < n; i++)
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
            k.Solid((a + b) * 0.5f + Vector3.up * height * 0.5f, S(len, height, 0.3f));
        }

        public static void Dumpster(WorldKit k, Vector3 p, bool alongX)
        {
            var s = alongX ? new Vector3(1.9f, 1.15f, 1.1f) : new Vector3(1.1f, 1.15f, 1.9f);
            k.Box(M("DumpsterGreen"), p + Vector3.up * 0.65f, s);
            k.Box(M("MetalDark"), p + Vector3.up * 1.26f, s + new Vector3(0.06f, -1.08f, 0.06f));
            k.Solid(p + Vector3.up * 0.65f, s + new Vector3(0, 0.1f, 0));
        }

        public static void Crate(WorldKit k, Vector3 p, Vector3 size, string mat = "Wood")
        {
            k.Box(M(mat), p + Vector3.up * size.y * 0.5f, size);
            k.Solid(p + Vector3.up * size.y * 0.5f, size);
        }

        // ------------------------------------------------------------------ vehicles
        public static void Car(WorldKit k, Vector3 p, bool alongZ, string paint)
        {
            var fwd = alongZ ? Vector3.forward : Vector3.right;
            var side = alongZ ? Vector3.right : Vector3.forward;
            Vector3 S(float l, float hgt, float w) => alongZ ? new Vector3(w, hgt, l) : new Vector3(l, hgt, w);
            Vector3 P(float l, float y, float w) => p + fwd * l + Vector3.up * y + side * w;
            const float L = 4.4f, W = 1.84f;
            var body = M(paint);
            k.Box(body, P(0, 0.66f, 0), S(L, 0.62f, W));
            k.Box(M("GlassDark"), P(-0.25f, 1.2f, 0), S(L * 0.5f, 0.46f, W - 0.14f));
            k.Box(body, P(-0.25f, 1.45f, 0), S(L * 0.46f, 0.06f, W - 0.2f));
            k.Box(M("MetalDark"), P(L * 0.5f, 0.42f, 0), S(0.12f, 0.24f, W));
            k.Box(M("MetalDark"), P(-L * 0.5f, 0.42f, 0), S(0.12f, 0.24f, W));
            foreach (float w in new[] { -0.62f, 0.62f })
            {
                k.Box(M("Headlight"), P(L * 0.5f + 0.01f, 0.78f, w), S(0.04f, 0.12f, 0.3f));
                k.Box(M("Taillight"), P(-L * 0.5f - 0.01f, 0.8f, w), S(0.04f, 0.12f, 0.3f));
            }
            foreach (float l in new[] { -1.38f, 1.38f })
            foreach (float w in new[] { -0.82f, 0.82f })
                k.Pipe(M("Tire"), P(l, 0.34f, w - 0.13f), P(l, 0.34f, w + 0.13f), 0.34f);
            k.Solid(P(0, 0.75f, 0), S(L, 1.5f, W));
        }

        public static void Truck(WorldKit k, Vector3 p, bool alongZ, string paint, string label = null)
        {
            var fwd = alongZ ? Vector3.forward : Vector3.right;
            var side = alongZ ? Vector3.right : Vector3.forward;
            Vector3 S(float l, float hgt, float w) => alongZ ? new Vector3(w, hgt, l) : new Vector3(l, hgt, w);
            Vector3 P(float l, float y, float w) => p + fwd * l + Vector3.up * y + side * w;
            const float W = 2.45f;
            k.Box(M("TruckWhite"), P(-1f, 2.25f, 0), S(5.8f, 2.7f, W));
            k.Box(M(paint), P(3f, 1.6f, 0), S(2.1f, 2f, W - 0.05f));
            k.Box(M("GlassDark"), P(3.6f, 2.1f, 0), S(0.9f, 0.8f, W));
            k.Box(M("MetalDark"), P(0.5f, 0.75f, 0), S(8f, 0.3f, W - 0.3f));
            foreach (float l in new[] { -3f, -1.9f, 3.1f })
            foreach (float w in new[] { -1.05f, 1.05f })
                k.Pipe(M("Tire"), P(l, 0.48f, w - 0.16f), P(l, 0.48f, w + 0.16f), 0.48f);
            k.Solid(P(0f, 1.8f, 0), S(8.1f, 3.6f, W));
            if (label != null)
                foreach (float s in new[] { -1f, 1f })
                    k.Text(label, P(-1f, 2.4f, s * (W * 0.5f + 0.02f)), alongZ ? (s > 0 ? -90f : 90f) : (s > 0 ? 180f : 0f), 0.6f, new Color(0.15f, 0.2f, 0.3f), null, false);
        }

        // ------------------------------------------------------------------ lab furniture
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
