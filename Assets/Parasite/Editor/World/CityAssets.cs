using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.EditorTools
{
    /// <summary>Texture import settings, generated mask/skyline textures, materials, skybox and post-processing profile.</summary>
    internal static class CityAssets
    {
        public const string MatDir = "Assets/Parasite/Materials";
        public const string ArtDir = "Assets/Parasite/Art";
        public const string GenDir = "Assets/Parasite/Art/City/Generated";
        public const string SettingsDir = "Assets/Parasite/Settings";
        public const string HdriPath = "Assets/Parasite/Art/City/sky/sunset_jhbcentral_1k.hdr";
        public const string VolumePath = SettingsDir + "/CityVolume.asset";

        static readonly (string name, string folder, float metal)[] Sets =
        {
            ("asphalt", "City", 0f), ("sidewalk", "City", 0f), ("brick", "City", 0f), ("plaster", "City", 0f),
            ("metal", "City", 0.65f), ("wood", "City", 0f), ("labfloor", "Lab", 0f), ("concrete", "Lab", 0f), ("labmetal", "Lab", 0.8f),
        };

        static readonly Dictionary<string, Material> mats = new();
        public static readonly Dictionary<Material, float> Tile = new();
        public static readonly HashSet<Material> NoShadow = new();
        public static Material SkyMaterial { get; private set; }
        public static VolumeProfile Profile { get; private set; }
        public static Font Font { get; private set; }

        public static Material M(string name) => mats.TryGetValue(name, out var m) ? m : mats["Concrete"];

        public static void Prepare()
        {
            foreach (var d in new[] { MatDir, GenDir, SettingsDir }) Directory.CreateDirectory(d);
            mats.Clear(); Tile.Clear(); NoShadow.Clear();
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GenerateTextures();
            AssetDatabase.Refresh();
            ConfigureImporters();
            CreateMaterials();
            SkyMaterial = CreateSkybox();
            Profile = CreateVolumeProfile();
            AssetDatabase.SaveAssets();
        }

        static string Tex(string set, string kind)
        {
            foreach (var s in Sets)
                if (s.name == set) return $"{ArtDir}/{s.folder}/{set}/{set}_{kind}";
            return null;
        }

        // ------------------------------------------------------------------ textures
        static void GenerateTextures()
        {
            foreach (var s in Sets)
            {
                string rough = Tex(s.name, "rough") + ".jpg", mask = Tex(s.name, "mask") + ".png";
                if (!File.Exists(rough) || File.Exists(mask)) continue;
                var src = new Texture2D(2, 2);
                if (!src.LoadImage(File.ReadAllBytes(rough))) { Object.DestroyImmediate(src); continue; }
                var px = src.GetPixels32();
                byte metal = (byte)Mathf.RoundToInt(s.metal * 255f);
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(metal, 0, 0, (byte)(255 - px[i].r));
                var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
                dst.SetPixels32(px); dst.Apply();
                File.WriteAllBytes(mask, dst.EncodeToPNG());
                Object.DestroyImmediate(src); Object.DestroyImmediate(dst);
            }

            string alb = $"{GenDir}/skyline_albedo.png", emi = $"{GenDir}/skyline_emission.png";
            if (File.Exists(alb) && File.Exists(emi)) return;
            const int N = 512, cells = 8, cs = N / cells;
            var a = new Color32[N * N]; var e = new Color32[N * N];
            var rng = new System.Random(7);
            var frame = new Color32(24, 26, 30, 255); var glass = new Color32(18, 24, 32, 255);
            for (int cy = 0; cy < cells; cy++)
            for (int cx = 0; cx < cells; cx++)
            {
                bool lit = rng.NextDouble() < 0.38;
                float k = 0.55f + (float)rng.NextDouble() * 0.45f;
                bool cool = rng.NextDouble() < 0.3;
                var lc = cool ? new Color(0.62f, 0.78f, 1f) * k : new Color(1f, 0.74f, 0.42f) * k;
                for (int y = 0; y < cs; y++)
                for (int x = 0; x < cs; x++)
                {
                    int i = (cy * cs + y) * N + cx * cs + x;
                    bool win = x > cs * 0.18f && x < cs * 0.82f && y > cs * 0.22f && y < cs * 0.86f;
                    a[i] = win ? (lit ? (Color32)Color.Lerp(lc, Color.white, 0.2f) : glass) : frame;
                    e[i] = win && lit ? (Color32)lc : new Color32(0, 0, 0, 255);
                }
            }
            void Write(string p, Color32[] data)
            {
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                t.SetPixels32(data); t.Apply(); File.WriteAllBytes(p, t.EncodeToPNG()); Object.DestroyImmediate(t);
            }
            Write(alb, a); Write(emi, e);
        }

        static void ConfigureImporters()
        {
            foreach (var s in Sets)
            {
                Configure(Tex(s.name, "albedo") + ".jpg", TextureImporterType.Default, true, s.name == "asphalt" ? 2048 : 1024);
                Configure(Tex(s.name, "normal") + ".jpg", TextureImporterType.NormalMap, false, 1024);
                Configure(Tex(s.name, "mask") + ".png", TextureImporterType.Default, false, 1024);
                Configure(Tex(s.name, "rough") + ".jpg", TextureImporterType.Default, false, 256);
            }
            Configure($"{GenDir}/skyline_albedo.png", TextureImporterType.Default, true, 512);
            Configure($"{GenDir}/skyline_emission.png", TextureImporterType.Default, true, 512);

            if (AssetImporter.GetAtPath(HdriPath) is TextureImporter hi)
            {
                var st = new TextureImporterSettings();
                hi.ReadTextureSettings(st);
                bool dirty = st.textureShape != TextureImporterShape.TextureCube || st.generateCubemap != TextureImporterGenerateCubemap.AutoCubemap
                             || !hi.mipmapEnabled || hi.maxTextureSize != 2048;
                if (dirty)
                {
                    st.textureShape = TextureImporterShape.TextureCube;
                    st.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                    st.cubemapConvolution = TextureImporterCubemapConvolution.None;
                    st.mipmapEnabled = true;
                    hi.SetTextureSettings(st);
                    hi.maxTextureSize = 2048;
                    hi.textureCompression = TextureImporterCompression.Compressed;
                    hi.SaveAndReimport();
                }
            }
        }

        static void Configure(string path, TextureImporterType type, bool srgb, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            if (ti.textureType == type && ti.sRGBTexture == srgb && ti.maxTextureSize == maxSize && ti.mipmapEnabled
                && ti.textureCompression == TextureImporterCompression.Compressed && ti.wrapMode == TextureWrapMode.Repeat) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 4;
            ti.alphaSource = type == TextureImporterType.NormalMap ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ materials
        static void CreateMaterials()
        {
            Color c(float r, float g, float b) => new(r, g, b);
            // textured surfaces: name, set, tint, tile metres, smoothness scale
            T("Asphalt", "asphalt", c(0.78f, 0.78f, 0.8f), 7f, 0.75f);
            T("Sidewalk", "sidewalk", c(0.92f, 0.9f, 0.88f), 3f, 0.7f);
            T("Curb", "concrete", c(0.8f, 0.8f, 0.78f), 2f, 0.6f);
            T("Brick", "brick", Color.white, 3f, 0.8f);
            T("BrickDark", "brick", c(0.58f, 0.5f, 0.48f), 3f, 0.8f);
            T("PlasterCream", "plaster", c(1f, 0.9f, 0.76f), 4f, 0.7f);
            T("PlasterGrey", "plaster", c(0.7f, 0.72f, 0.76f), 4f, 0.7f);
            T("PlasterTeal", "plaster", c(0.5f, 0.7f, 0.7f), 4f, 0.7f);
            T("PlasterTerracotta", "plaster", c(0.86f, 0.56f, 0.44f), 4f, 0.7f);
            T("LabWhite", "plaster", c(0.96f, 0.97f, 0.98f), 6f, 0.9f);
            T("LabFloor", "labfloor", c(0.95f, 0.97f, 1f), 2.4f, 1f);
            T("Concrete", "concrete", c(0.85f, 0.85f, 0.85f), 4f, 0.7f);
            T("TunnelConcrete", "concrete", c(0.55f, 0.58f, 0.56f), 3f, 0.8f);
            T("RustyMetal", "metal", Color.white, 2f, 0.9f);
            T("SteelPlate", "labmetal", c(0.85f, 0.87f, 0.9f), 2f, 1f);
            T("Wood", "wood", Color.white, 2f, 0.9f);
            T("Hoarding", "wood", c(0.75f, 0.68f, 0.55f), 2.5f, 0.6f);

            P("GlassDark", c(0.04f, 0.055f, 0.07f), 0.94f, 0.3f);
            P("SignBoard", c(0.06f, 0.07f, 0.09f), 0.55f, 0.3f);
            P("MetalDark", c(0.11f, 0.12f, 0.13f), 0.55f, 0.75f);
            P("Chrome", c(0.75f, 0.76f, 0.78f), 0.88f, 1f);
            P("Tire", c(0.03f, 0.03f, 0.03f), 0.25f, 0f);
            P("CarRed", c(0.5f, 0.04f, 0.04f), 0.82f, 0.45f);
            P("CarBlue", c(0.06f, 0.14f, 0.36f), 0.82f, 0.45f);
            P("CarWhite", c(0.82f, 0.83f, 0.84f), 0.8f, 0.3f);
            P("CarBlack", c(0.03f, 0.035f, 0.04f), 0.85f, 0.45f);
            P("CarSilver", c(0.5f, 0.52f, 0.55f), 0.8f, 0.7f);
            P("TruckWhite", c(0.75f, 0.76f, 0.74f), 0.45f, 0.1f);
            P("BarrierOrange", c(0.95f, 0.36f, 0.04f), 0.45f, 0f);
            P("BarrierWhite", c(0.9f, 0.9f, 0.88f), 0.45f, 0f);
            P("DumpsterGreen", c(0.08f, 0.24f, 0.13f), 0.45f, 0.5f);
            P("Grass", c(0.12f, 0.22f, 0.08f), 0.15f, 0f);
            P("Foliage", c(0.09f, 0.2f, 0.07f), 0.2f, 0f);
            P("FoliageAutumn", c(0.32f, 0.2f, 0.06f), 0.2f, 0f);
            P("Trunk", c(0.2f, 0.14f, 0.09f), 0.2f, 0f);
            P("Soil", c(0.12f, 0.09f, 0.07f), 0.1f, 0f);
            P("Water", c(0.03f, 0.08f, 0.1f), 0.97f, 0.1f);
            P("Puddle", c(0.02f, 0.025f, 0.03f), 0.98f, 0f);
            P("AwningRed", c(0.45f, 0.06f, 0.05f), 0.3f, 0f);
            P("AwningGreen", c(0.06f, 0.3f, 0.18f), 0.3f, 0f);
            P("AwningNavy", c(0.06f, 0.09f, 0.22f), 0.3f, 0f);
            P("AwningYellow", c(0.75f, 0.55f, 0.08f), 0.3f, 0f);
            P("ValeAccent", c(0.08f, 0.45f, 0.32f), 0.7f, 0.4f);
            P("LabPlastic", c(0.86f, 0.88f, 0.9f), 0.6f, 0f);
            P("LabCounter", c(0.2f, 0.22f, 0.25f), 0.7f, 0.1f);
            P("Locker", c(0.42f, 0.5f, 0.58f), 0.55f, 0.7f);
            P("Rug", c(0.2f, 0.08f, 0.07f), 0.1f, 0f);
            P("Leather", c(0.09f, 0.06f, 0.05f), 0.5f, 0f);
            P("Pipe", c(0.3f, 0.32f, 0.3f), 0.55f, 0.8f);
            P("PipeYellow", c(0.7f, 0.55f, 0.06f), 0.5f, 0.4f);
            P("PipeRed", c(0.45f, 0.06f, 0.05f), 0.5f, 0.4f);
            P("Gate", c(0.65f, 0.5f, 0.06f), 0.5f, 0.6f);
            P("DoorStaff", c(0.12f, 0.2f, 0.3f), 0.6f, 0.6f);
            P("DoorService", c(0.35f, 0.33f, 0.2f), 0.45f, 0.7f);
            P("PaintWhite", c(0.82f, 0.82f, 0.8f), 0.35f, 0f, shadow: false);
            P("PaintYellow", c(0.85f, 0.62f, 0.08f), 0.35f, 0f, shadow: false);
            P("Ground", c(0.05f, 0.05f, 0.055f), 0.3f, 0f, shadow: false);

            E("WindowWarm", c(0.9f, 0.75f, 0.55f), c(1f, 0.72f, 0.42f) * 1.8f);
            E("WindowCool", c(0.7f, 0.8f, 0.9f), c(0.6f, 0.8f, 1f) * 1.5f);
            E("ShopGlow", c(0.95f, 0.85f, 0.65f), c(1f, 0.82f, 0.58f) * 2.2f);
            E("LampHead", c(1f, 0.9f, 0.7f), c(1f, 0.82f, 0.55f) * 7f);
            E("LabPanel", c(0.95f, 0.98f, 1f), c(0.85f, 0.94f, 1f) * 4f);
            E("TunnelStrip", c(0.6f, 1f, 0.8f), c(0.5f, 1f, 0.75f) * 3f);
            E("TankLiquid", c(0.1f, 0.6f, 0.2f), c(0.2f, 1f, 0.32f) * 2.6f);
            E("Screen", c(0.2f, 0.45f, 0.7f), c(0.3f, 0.7f, 1f) * 2.2f);
            E("NeonGreen", c(0.3f, 1f, 0.5f), c(0.25f, 1f, 0.45f) * 4f);
            E("NeonRed", c(1f, 0.3f, 0.25f), c(1f, 0.2f, 0.15f) * 4f);
            E("NeonCyan", c(0.3f, 0.9f, 1f), c(0.2f, 0.85f, 1f) * 4f);
            E("NeonPink", c(1f, 0.35f, 0.75f), c(1f, 0.25f, 0.7f) * 4f);
            E("Headlight", c(1f, 1f, 0.9f), c(1f, 0.95f, 0.8f) * 3f);
            E("Taillight", c(0.6f, 0.05f, 0.03f), c(1f, 0.05f, 0.02f) * 2f);
            E("WarningLight", c(1f, 0.6f, 0.1f), c(1f, 0.5f, 0.05f) * 5f);

            var sky = Mat("Skyline");
            var sa = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GenDir}/skyline_albedo.png");
            var se = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GenDir}/skyline_emission.png");
            sky.SetColor("_BaseColor", new Color(0.55f, 0.57f, 0.62f));
            sky.SetFloat("_Smoothness", 0.5f);
            if (sa) sky.SetTexture("_BaseMap", sa);
            sky.EnableKeyword("_EMISSION");
            sky.SetColor("_EmissionColor", sa ? Color.white * 1.3f : new Color(0.05f, 0.05f, 0.06f));
            if (se) sky.SetTexture("_EmissionMap", se);
            sky.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Done(sky, 24f, true);

            Glass("GlassClear", new Color(0.75f, 0.88f, 0.92f, 0.18f));
            Glass("GlassTank", new Color(0.7f, 0.95f, 0.85f, 0.14f));
        }

        static Material Mat(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = new Material(lit);
            if (!m) { m = fresh; m.name = name; AssetDatabase.CreateAsset(m, path); }
            else { m.shader = lit; m.CopyPropertiesFromMaterial(fresh); m.shaderKeywords = new string[0]; Object.DestroyImmediate(fresh); }
            m.renderQueue = -1;
            m.SetOverrideTag("RenderType", "");
            m.SetShaderPassEnabled("ShadowCaster", true);
            mats[name] = m;
            return m;
        }

        static void Done(Material m, float tile, bool shadow)
        {
            Tile[m] = tile;
            if (!shadow) NoShadow.Add(m);
            EditorUtility.SetDirty(m);
        }

        static void T(string name, string set, Color tint, float tile, float smooth)
        {
            var m = Mat(name);
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex(set, "albedo") + ".jpg");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex(set, "normal") + ".jpg");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex(set, "mask") + ".png");
            m.SetColor("_BaseColor", alb ? tint : tint * 0.6f);
            if (alb) m.SetTexture("_BaseMap", alb);
            if (nor) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (mask) { m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", smooth); }
            else { m.SetFloat("_Smoothness", 0.3f * smooth); m.SetFloat("_Metallic", 0f); }
            Done(m, tile, true);
        }

        static void P(string name, Color color, float smooth, float metal, bool shadow = true)
        {
            var m = Mat(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            Done(m, 2f, shadow);
        }

        static void E(string name, Color color, Color emission)
        {
            var m = Mat(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Done(m, 2f, false);
        }

        static void Glass(string name, Color color)
        {
            var m = Mat(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.96f);
            m.SetFloat("_Metallic", 0.1f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            Done(m, 2f, false);
        }

        // ------------------------------------------------------------------ sky + post
        static Material CreateSkybox()
        {
            string path = $"{MatDir}/SkyEvening.mat";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(HdriPath);
            var shader = Shader.Find(cube ? "Skybox/Cubemap" : "Skybox/Procedural");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            if (cube)
            {
                m.SetTexture("_Tex", cube);
                m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
                m.SetFloat("_Exposure", 1.05f);
                m.SetFloat("_Rotation", 0f);
            }
            else
            {
                Debug.LogWarning("[Parasite] HDRI cubemap missing — using a procedural evening sky.");
                m.SetFloat("_SunSize", 0.05f);
                m.SetFloat("_AtmosphereThickness", 1.6f);
                m.SetColor("_SkyTint", new Color(0.55f, 0.5f, 0.6f));
                m.SetColor("_GroundColor", new Color(0.2f, 0.19f, 0.2f));
                m.SetFloat("_Exposure", 1.1f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static VolumeProfile CreateVolumeProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (!p) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, VolumePath); }
            foreach (var c in p.components.ToArray()) { AssetDatabase.RemoveObjectFromAsset(c); Object.DestroyImmediate(c, true); }
            p.components.Clear();

            var tm = p.Add<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
            var bl = p.Add<Bloom>();
            bl.threshold.Override(1f); bl.intensity.Override(0.6f); bl.scatter.Override(0.65f);
            bl.tint.Override(new Color(1f, 0.95f, 0.9f)); bl.highQualityFiltering.Override(true);
            var ca = p.Add<ColorAdjustments>();
            ca.postExposure.Override(0.15f); ca.contrast.Override(12f); ca.saturation.Override(8f);
            var vg = p.Add<Vignette>(); vg.intensity.Override(0.2f); vg.smoothness.Override(0.45f);
            var wb = p.Add<WhiteBalance>(); wb.temperature.Override(4f); wb.tint.Override(2f);
            var smh = p.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
