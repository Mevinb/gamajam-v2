using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Characters
{
    /// <summary>
    /// Garment colour palettes (sRGB) used to re-tint anonymous NPC instances. The cloth atlas stores tinted regions as
    /// normalised grey (mean linear value 0.55), so a palette colour c becomes the linear tint c.linear / 0.55.
    /// </summary>
    public static class CharacterPalettes
    {
        public const float AtlasGrey = 0.55f;

        static Color C(float r, float g, float b) => new(r, g, b, 1f);

        static readonly Dictionary<string, Color[]> palettes = new()
        {
            ["top"] = new[] { C(0.78f, 0.30f, 0.28f), C(0.20f, 0.38f, 0.60f), C(0.85f, 0.82f, 0.74f), C(0.25f, 0.45f, 0.35f), C(0.55f, 0.45f, 0.62f), C(0.88f, 0.62f, 0.25f), C(0.14f, 0.14f, 0.16f), C(0.70f, 0.70f, 0.72f) },
            ["shirt"] = new[] { C(0.62f, 0.70f, 0.80f), C(0.86f, 0.86f, 0.84f), C(0.55f, 0.62f, 0.50f), C(0.70f, 0.55f, 0.55f), C(0.30f, 0.36f, 0.48f), C(0.80f, 0.74f, 0.60f), C(0.20f, 0.35f, 0.55f) },
            ["tshirt"] = new[] { C(0.85f, 0.85f, 0.83f), C(0.30f, 0.32f, 0.36f), C(0.62f, 0.20f, 0.18f), C(0.22f, 0.36f, 0.58f) },
            ["denim"] = new[] { C(0.20f, 0.28f, 0.42f), C(0.28f, 0.38f, 0.55f), C(0.12f, 0.14f, 0.20f), C(0.38f, 0.48f, 0.62f), C(0.16f, 0.16f, 0.18f) },
            ["trousers"] = new[] { C(0.18f, 0.19f, 0.22f), C(0.30f, 0.27f, 0.22f), C(0.38f, 0.36f, 0.30f), C(0.12f, 0.12f, 0.13f), C(0.25f, 0.28f, 0.33f) },
            ["hoodie"] = new[] { C(0.36f, 0.48f, 0.34f), C(0.55f, 0.55f, 0.57f), C(0.18f, 0.20f, 0.30f), C(0.62f, 0.22f, 0.20f), C(0.85f, 0.72f, 0.30f), C(0.12f, 0.12f, 0.13f) },
            ["jacket"] = new[] { C(0.25f, 0.30f, 0.26f), C(0.32f, 0.22f, 0.15f), C(0.16f, 0.17f, 0.20f), C(0.45f, 0.40f, 0.32f), C(0.20f, 0.26f, 0.40f) },
            ["skirt"] = new[] { C(0.15f, 0.15f, 0.18f), C(0.35f, 0.18f, 0.20f), C(0.22f, 0.26f, 0.40f), C(0.45f, 0.42f, 0.36f) },
            ["sweater"] = new[] { C(0.45f, 0.20f, 0.17f), C(0.73f, 0.65f, 0.53f), C(0.25f, 0.33f, 0.45f), C(0.30f, 0.40f, 0.30f), C(0.55f, 0.52f, 0.50f) },
            ["cap"] = new[] { C(0.30f, 0.27f, 0.22f), C(0.20f, 0.20f, 0.22f), C(0.35f, 0.30f, 0.26f) },
            ["suit"] = new[] { C(0.22f, 0.24f, 0.32f), C(0.18f, 0.18f, 0.20f), C(0.35f, 0.33f, 0.30f), C(0.30f, 0.20f, 0.20f) },
            ["uniform"] = new[] { C(0.10f, 0.13f, 0.24f), C(0.08f, 0.10f, 0.17f), C(0.14f, 0.16f, 0.22f) },
            ["uniform_pants"] = new[] { C(0.07f, 0.08f, 0.11f), C(0.10f, 0.10f, 0.12f) },
            ["coverall"] = new[] { C(0.16f, 0.22f, 0.36f), C(0.24f, 0.26f, 0.28f), C(0.20f, 0.30f, 0.48f), C(0.30f, 0.32f, 0.22f), C(0.55f, 0.28f, 0.10f) },
        };

        /// <summary>Linear tint for the cloth shader from a palette colour, or false if the palette is unknown.</summary>
        public static bool TryPick(string palette, System.Random rnd, out Color linearTint)
        {
            linearTint = Color.white;
            if (string.IsNullOrEmpty(palette) || !palettes.TryGetValue(palette, out var list) || list.Length == 0) return false;
            var c = list[rnd.Next(list.Length)];
            float jitter = 0.92f + (float)rnd.NextDouble() * 0.16f;
            var lin = c.linear * jitter / AtlasGrey;
            linearTint = new Color(lin.r, lin.g, lin.b, 1f);
            return true;
        }
    }
}
