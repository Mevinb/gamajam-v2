using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.EditorTools
{
    /// <summary>A photo facade material whose texture is a grid of window bays × floors (see Tools/gen_city_art.py).</summary>
    internal sealed class FacadeStyle
    {
        public string Name;
        public Material Mat;           // upper floors (windows in the texture)
        public Material Wall;          // matching blank wall (party walls, parapets)
        public Material Trim;          // cornices / sills / coping
        public float BayWidth = 3f, FloorHeight = 3.2f;
        public int BaysPerTile = 6, FloorsPerTile = 6;
        public bool Residential;
    }

    /// <summary>Poly Haven model, flattened into mesh parts (model space, metres, y up) with project materials per submesh.</summary>
    internal sealed class PropModel
    {
        public string Id;
        public readonly List<(Mesh mesh, Matrix4x4 local, Material[] mats)> Parts = new();
        public int Lod;   // Mesh LOD level merged into the scene (generated at import)
        public Bounds Bounds;
        public int Triangles;
    }

    /// <summary>Texture import settings, materials (URP/Lit), facade styles, props, skybox and post-processing profile.</summary>
    internal static class CityAssets
    {
        public const string MatDir = "Assets/Parasite/Materials";
        public const string ArtDir = "Assets/Parasite/Art";
        public const string CityArt = "Assets/Parasite/Art/City";
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
        public static readonly Dictionary<string, FacadeStyle> Facades = new();
        public static readonly Dictionary<string, PropModel> Props = new();
        public static Material SkyMaterial { get; private set; }
        public static VolumeProfile Profile { get; private set; }
        public static Font Font { get; private set; }

        public static Material M(string name) => mats.TryGetValue(name, out var m) ? m : mats["Concrete"];
        public static bool Has(string name) => mats.ContainsKey(name);
        public static FacadeStyle Facade(string name) => Facades.TryGetValue(name, out var f) ? f : Facades["BrickRed"];
        public static PropModel Prop(string id) => Props.TryGetValue(id, out var p) ? p : null;

        public static void Prepare()
        {
            foreach (var d in new[] { MatDir, MatDir + "/Props", GenDir, SettingsDir }) Directory.CreateDirectory(d);
            mats.Clear(); Tile.Clear(); NoShadow.Clear(); Facades.Clear(); Props.Clear();
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GenerateTextures();
            AssetDatabase.Refresh();
            ConfigureImporters();
            CreateMaterials();
            CreateFacades();
            LoadProps();
            SkyMaterial = CreateSkybox();
            Profile = CreateVolumeProfile();
            ConfigurePipeline();
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
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(metal, 255, 0, (byte)(255 - px[i].r));
                var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
                dst.SetPixels32(px); dst.Apply();
                File.WriteAllBytes(mask, dst.EncodeToPNG());
                Object.DestroyImmediate(src); Object.DestroyImmediate(dst);
            }
        }

        enum TexKind { Color, ColorAlpha, Normal, Linear }

        static void ConfigureImporters()
        {
            foreach (var s in Sets)
            {
                Configure(Tex(s.name, "albedo") + ".jpg", TexKind.Color, s.name == "asphalt" ? 2048 : 1024);
                Configure(Tex(s.name, "normal") + ".jpg", TexKind.Normal, 1024);
                Configure(Tex(s.name, "mask") + ".png", TexKind.Linear, 1024);
                Configure(Tex(s.name, "rough") + ".jpg", TexKind.Linear, 256);
            }
            // everything produced by Tools/gen_city_art.py follows the *_albedo/_normal/_mask/_height/_emission naming
            foreach (var dir in new[] { "Facades", "Skyline", "Ground", "Signs", "Foliage", "Decals", "Shop" })
            {
                string d = $"{CityArt}/{dir}";
                if (!Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d))
                {
                    if (f.EndsWith(".meta")) continue;
                    string p = f.Replace('\\', '/'), n = Path.GetFileNameWithoutExtension(p);
                    int size = dir == "Signs" ? 4096 : dir == "Skyline" || dir == "Shop" ? 1024 : 2048;
                    if (n.EndsWith("_normal")) Configure(p, TexKind.Normal, dir == "Skyline" ? 1024 : 2048);
                    else if (n.EndsWith("_mask") || n.EndsWith("_height")) Configure(p, TexKind.Linear, n.EndsWith("_height") ? 1024 : 2048);
                    else if (n.EndsWith("_emission")) Configure(p, TexKind.Color, dir == "Signs" ? 4096 : 1024);
                    else if (n.EndsWith("_albedo")) Configure(p, p.EndsWith(".png") && dir != "Signs" ? TexKind.ColorAlpha : TexKind.Color, size);
                }
            }
            // Poly Haven props
            if (Directory.Exists($"{CityArt}/Props"))
                foreach (var f in Directory.GetFiles($"{CityArt}/Props", "*", SearchOption.AllDirectories))
                {
                    if (f.EndsWith(".meta")) continue;
                    string p = f.Replace('\\', '/'), n = Path.GetFileNameWithoutExtension(p);
                    if (p.EndsWith(".fbx")) ConfigureModel(p);
                    else if (n.Contains("_nor_gl")) Configure(p, TexKind.Normal, 1024);
                    else if (n.EndsWith("_mask")) Configure(p, TexKind.Linear, 512);
                    else if (n.Contains("_diff")) Configure(p, p.EndsWith(".png") ? TexKind.ColorAlpha : TexKind.Color, 1024);
                    else if (n.Contains("_emissive")) Configure(p, TexKind.Color, 512);
                }

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

        static void Configure(string path, TexKind kind, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            var type = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = kind == TexKind.Color || kind == TexKind.ColorAlpha;
            bool alphaT = kind == TexKind.ColorAlpha;
            if (ti.textureType == type && ti.sRGBTexture == srgb && ti.maxTextureSize == maxSize && ti.mipmapEnabled && ti.alphaIsTransparency == alphaT
                && ti.textureCompression == TextureImporterCompression.CompressedHQ && ti.wrapMode == TextureWrapMode.Repeat && ti.anisoLevel == 8) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 8;
            ti.alphaSource = type == TextureImporterType.NormalMap ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = alphaT;
            ti.mipMapsPreserveCoverage = alphaT;
            if (alphaT) ti.alphaTestReferenceValue = 0.45f;
            ti.SaveAndReimport();
        }

        static void ConfigureModel(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            if (mi.isReadable && mi.bakeAxisConversion && mi.generateMeshLods && !mi.importAnimation && mi.materialImportMode == ModelImporterMaterialImportMode.ImportStandard
                && mi.materialLocation == ModelImporterMaterialLocation.InPrefab && !mi.importCameras && !mi.importLights) return;
            mi.isReadable = true;
            mi.bakeAxisConversion = true;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.generateMeshLods = true;
            mi.SaveAndReimport();
        }

        static Texture2D T2(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        // ------------------------------------------------------------------ materials
        static void CreateMaterials()
        {
            Color c(float r, float g, float b) => new(r, g, b);
            var worldText = Mat("WorldText", "Parasite/WorldText");
            worldText.SetColor("_Color", Color.white);
            if (Font) worldText.mainTexture = Font.material.mainTexture;
            Done(worldText, 1f, false);
            string G = $"{CityArt}/Ground";
            // textured surfaces: name, set, tint, tile metres, smoothness scale
            if (File.Exists($"{G}/Road_albedo.jpg"))
            {
                Lit("Asphalt", $"{G}/Road", c(0.82f, 0.82f, 0.84f), 1f, tile: 6f);
                Lit("Cobble", $"{G}/Cobble", c(0.85f, 0.83f, 0.8f), 1f, tile: 3.2f);
                Lit("Gravel", $"{G}/Gravel", c(0.7f, 0.7f, 0.7f), 1f, tile: 2.5f);
            }
            else T("Asphalt", "asphalt", c(0.78f, 0.78f, 0.8f), 7f, 0.75f);
            T("Sidewalk", "sidewalk", c(0.86f, 0.85f, 0.84f), 3f, 0.75f);
            T("Curb", "concrete", c(0.72f, 0.72f, 0.7f), 2f, 0.6f);
            T("Brick", "brick", Color.white, 3f, 0.8f);
            T("BrickDark", "brick", c(0.58f, 0.5f, 0.48f), 3f, 0.8f);
            T("PlasterCream", "plaster", c(1f, 0.9f, 0.76f), 4f, 0.7f);
            T("PlasterGrey", "plaster", c(0.7f, 0.72f, 0.76f), 4f, 0.7f);
            T("PlasterTeal", "plaster", c(0.5f, 0.7f, 0.7f), 4f, 0.7f);
            T("PlasterTerracotta", "plaster", c(0.86f, 0.56f, 0.44f), 4f, 0.7f);
            T("PlasterWhite", "plaster", c(0.86f, 0.86f, 0.83f), 4f, 0.7f);
            T("PlasterCharcoal", "plaster", c(0.32f, 0.32f, 0.33f), 4f, 0.7f);
            T("PlasterOchre", "plaster", c(0.8f, 0.6f, 0.38f), 4f, 0.7f);
            T("PlasterSand", "plaster", c(0.84f, 0.76f, 0.62f), 4f, 0.7f);
            T("Stone", "concrete", c(0.62f, 0.6f, 0.57f), 3f, 0.7f);
            T("LabWhite", "plaster", c(0.96f, 0.97f, 0.98f), 6f, 0.9f);
            T("LabFloor", "labfloor", c(0.95f, 0.97f, 1f), 2.4f, 1f);
            T("Concrete", "concrete", c(0.85f, 0.85f, 0.85f), 4f, 0.7f);
            T("TunnelConcrete", "concrete", c(0.55f, 0.58f, 0.56f), 3f, 0.8f);
            T("RustyMetal", "metal", Color.white, 2f, 0.9f);
            T("SteelPlate", "labmetal", c(0.85f, 0.87f, 0.9f), 2f, 1f);
            T("Wood", "wood", Color.white, 2f, 0.9f);
            T("Hoarding", "wood", c(0.75f, 0.68f, 0.55f), 2.5f, 0.6f);
            T("ShopFloor", "labfloor", c(0.8f, 0.78f, 0.74f), 1.6f, 0.9f);
            if (!Has("Cobble")) T("Cobble", "sidewalk", c(0.8f, 0.78f, 0.75f), 3f, 0.7f);
            if (!Has("Gravel")) T("Gravel", "concrete", c(0.5f, 0.5f, 0.5f), 3f, 0.4f);

            P("GlassDark", c(0.04f, 0.055f, 0.07f), 0.94f, 0.3f);
            P("SignBoard", c(0.06f, 0.07f, 0.09f), 0.55f, 0.3f);
            P("MetalDark", c(0.11f, 0.12f, 0.13f), 0.55f, 0.75f);
            P("DrainIron", c(0.055f, 0.06f, 0.06f), 0.22f, 0.55f);
            P("MetalFrame", c(0.16f, 0.17f, 0.18f), 0.6f, 0.8f);
            P("Chrome", c(0.75f, 0.76f, 0.78f), 0.88f, 1f);
            P("Tire", c(0.03f, 0.03f, 0.03f), 0.25f, 0f);
            P("Rim", c(0.55f, 0.56f, 0.58f), 0.75f, 0.9f);
            P("CarTrim", c(0.025f, 0.025f, 0.028f), 0.45f, 0.1f);
            CarPaint("CarRed", c(0.42f, 0.03f, 0.03f));
            CarPaint("CarBlue", c(0.05f, 0.12f, 0.32f));
            CarPaint("CarWhite", c(0.8f, 0.8f, 0.8f));
            CarPaint("CarBlack", c(0.025f, 0.028f, 0.032f));
            CarPaint("CarSilver", c(0.45f, 0.47f, 0.5f), 0.85f);
            CarPaint("CarGreen", c(0.06f, 0.18f, 0.12f));
            CarPaint("CarTaxi", c(0.85f, 0.6f, 0.05f));
            CarPaint("CarBeige", c(0.55f, 0.48f, 0.38f));
            P("TruckWhite", c(0.75f, 0.76f, 0.74f), 0.45f, 0.1f);
            P("BarrierOrange", c(0.95f, 0.36f, 0.04f), 0.45f, 0f);
            P("BarrierWhite", c(0.9f, 0.9f, 0.88f), 0.45f, 0f);
            P("DumpsterGreen", c(0.08f, 0.24f, 0.13f), 0.45f, 0.5f);
            P("Grass", c(0.12f, 0.22f, 0.08f), 0.15f, 0f);
            P("Foliage", c(0.09f, 0.2f, 0.07f), 0.2f, 0f);
            P("FoliageAutumn", c(0.32f, 0.2f, 0.06f), 0.2f, 0f);
            P("Trunk", c(0.2f, 0.16f, 0.12f), 0.2f, 0f);
            P("Soil", c(0.09f, 0.07f, 0.05f), 0.1f, 0f);
            P("Water", c(0.03f, 0.08f, 0.1f), 0.97f, 0.1f);
            P("Puddle", c(0.02f, 0.025f, 0.03f), 0.98f, 0f);
            P("AwningRed", c(0.45f, 0.06f, 0.05f), 0.3f, 0f);
            P("AwningGreen", c(0.06f, 0.3f, 0.18f), 0.3f, 0f);
            P("AwningNavy", c(0.06f, 0.09f, 0.22f), 0.3f, 0f);
            P("AwningYellow", c(0.75f, 0.55f, 0.08f), 0.3f, 0f);
            P("AwningBlack", c(0.04f, 0.04f, 0.045f), 0.3f, 0f);
            P("AwningCream", c(0.75f, 0.7f, 0.6f), 0.3f, 0f);
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
            P("DoorWood", c(0.18f, 0.1f, 0.06f), 0.5f, 0f);
            P("PaintWhite", c(0.78f, 0.78f, 0.76f), 0.35f, 0f, shadow: false);
            P("PaintYellow", c(0.8f, 0.58f, 0.08f), 0.35f, 0f, shadow: false);
            P("Ground", c(0.05f, 0.05f, 0.055f), 0.3f, 0f, shadow: false);
            P("RoofTar", c(0.07f, 0.07f, 0.075f), 0.35f, 0f);
            P("RoofMetal", c(0.32f, 0.33f, 0.34f), 0.45f, 0.7f);
            P("Rail", c(0.06f, 0.065f, 0.07f), 0.5f, 0.8f);
            P("KickPlate", c(0.09f, 0.09f, 0.1f), 0.7f, 0.6f);

            E("WindowWarm", c(0.16f, 0.13f, 0.1f), c(1f, 0.68f, 0.4f) * 0.85f);
            E("WindowCool", c(0.1f, 0.12f, 0.14f), c(0.42f, 0.56f, 0.72f) * 0.9f);
            E("ShopGlow", c(0.95f, 0.85f, 0.65f), c(1f, 0.82f, 0.58f) * 1.4f);
            E("ShopStrip", c(1f, 0.95f, 0.85f), c(1f, 0.9f, 0.75f) * 2.5f);
            E("LampHead", c(1f, 0.9f, 0.7f), c(1f, 0.8f, 0.52f) * 5f);
            E("LabPanel", c(0.95f, 0.98f, 1f), c(0.85f, 0.94f, 1f) * 4f);
            E("TunnelStrip", c(0.6f, 1f, 0.8f), c(0.5f, 1f, 0.75f) * 3f);
            E("TankLiquid", c(0.1f, 0.6f, 0.2f), c(0.2f, 1f, 0.32f) * 2.6f);
            E("Screen", c(0.2f, 0.45f, 0.7f), c(0.3f, 0.7f, 1f) * 2.2f);
            E("NeonGreen", c(0.3f, 1f, 0.5f), c(0.25f, 1f, 0.45f) * 3f);
            E("NeonRed", c(1f, 0.3f, 0.25f), c(1f, 0.2f, 0.15f) * 3f);
            E("NeonCyan", c(0.3f, 0.9f, 1f), c(0.2f, 0.85f, 1f) * 3f);
            E("NeonPink", c(1f, 0.35f, 0.75f), c(1f, 0.25f, 0.7f) * 3f);
            E("Headlight", c(1f, 1f, 0.9f), c(1f, 0.95f, 0.8f) * 2.5f);
            E("Taillight", c(0.6f, 0.05f, 0.03f), c(1f, 0.05f, 0.02f) * 1.6f);
            E("WarningLight", c(1f, 0.6f, 0.1f), c(1f, 0.5f, 0.05f) * 4f);
            E("AviationLight", c(1f, 0.1f, 0.05f), c(1f, 0.08f, 0.03f) * 6f);

            // signs (text atlas rendered by Tools/gen_city_art.py) and shop interiors behind the glass
            var signs = Lit("Signs", $"{CityArt}/Signs/signs", Color.white, 0.55f, emission: 1.35f, tile: 1f);
            if (signs) NoShadow.Add(signs);
            var interior = Lit("ShopInterior", $"{CityArt}/Shop/Interior", Color.white, 0.2f, emission: 0.75f, tile: 1f);
            if (interior) NoShadow.Add(interior);

            // foliage cards (alpha clipped, both faces)
            Lit("LeavesGreen", $"{CityArt}/Foliage/LeavesGreen", c(1f, 1.05f, 0.92f), 0.3f, tile: 1f, clip: 0.45f, twoSided: true);
            Lit("LeavesAutumn", $"{CityArt}/Foliage/LeavesAutumn", c(0.9f, 0.82f, 0.75f), 0.25f, tile: 1f, clip: 0.45f, twoSided: true);
            var grass = Lit("GrassCard", $"{CityArt}/Foliage/Grass", c(0.7f, 0.8f, 0.65f), 0.15f, tile: 1f, clip: 0.4f, twoSided: true);
            if (grass) NoShadow.Add(grass);

            // mesh decals (alpha blended, no shadows) — slightly offset from the surface they sit on
            foreach (var (n, sm) in new[] { ("Graffiti", 0.35f), ("AsphaltDamage", 0.25f), ("Grime", 0.15f), ("Puddle", 0.97f), ("Manhole", 0.5f) })
            {
                var d = Lit("Decal" + n, $"{CityArt}/Decals/{n}", Color.white, sm, tile: 1f, transparent: n != "Manhole", clip: n == "Manhole" ? 0.5f : -1f);
                if (!d) continue;
                NoShadow.Add(d);
                if (n == "Puddle") { d.SetFloat("_Metallic", 0f); d.SetFloat("_Smoothness", 0.97f); }
                if (n == "AsphaltDamage") d.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.52f, 1f));
            }

            // skyline photo facades (night office towers)
            foreach (var n in new[] { "TowerDense", "TowerGrid", "TowerBrown", "TowerPiers", "TowerGlassNight", "TowerGlass" })
                Lit(n, $"{CityArt}/Skyline/{n}", Color.white, 0.85f, emission: n == "TowerGlass" ? 1.2f : 1.0f, tile: 1f);
            if (!Has("TowerGrid")) LegacySkyline();
            mats["Skyline"] = M(Has("TowerGrid") ? "TowerGrid" : "Skyline");

            Glass("GlassClear", new Color(0.75f, 0.88f, 0.92f, 0.18f));
            Glass("GlassTank", new Color(0.7f, 0.95f, 0.85f, 0.14f));
            Glass("GlassShop", new Color(0.55f, 0.62f, 0.66f, 0.16f), 0.97f);
            Glass("GlassCar", new Color(0.05f, 0.06f, 0.07f, 0.72f), 0.95f);
            Glass("LampGlass", new Color(1f, 0.92f, 0.75f, 0.4f), 0.9f);
        }

        static void LegacySkyline()
        {
            var sky = Mat("Skyline");
            sky.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.36f));
            sky.SetFloat("_Smoothness", 0.5f);
            Done(sky, 24f, true);
        }

        static Material Mat(string name, string shader = "Universal Render Pipeline/Lit", string dir = MatDir)
        {
            string path = $"{dir}/{name}.mat";
            var lit = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = new Material(lit);
            if (!m) { m = fresh; m.name = name; AssetDatabase.CreateAsset(m, path); }
            else { m.shader = lit; m.CopyPropertiesFromMaterial(fresh); m.shaderKeywords = new string[0]; Object.DestroyImmediate(fresh); }
            m.renderQueue = -1;
            m.SetOverrideTag("RenderType", "");
            m.SetShaderPassEnabled("ShadowCaster", true);
            m.enableInstancing = true;
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
            var alb = T2(Tex(set, "albedo") + ".jpg");
            var nor = T2(Tex(set, "normal") + ".jpg");
            var mask = T2(Tex(set, "mask") + ".png");
            m.SetColor("_BaseColor", alb ? tint : tint * 0.6f);
            if (alb) m.SetTexture("_BaseMap", alb);
            if (nor) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (mask) { m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", smooth); }
            else { m.SetFloat("_Smoothness", 0.3f * smooth); m.SetFloat("_Metallic", 0f); }
            Done(m, tile, true);
        }

        /// <summary>URP/Lit from a texture prefix (prefix_albedo.jpg|png, _normal.jpg, _mask.png, _height.png, _emission.jpg|png).</summary>
        static Material Lit(string name, string prefix, Color tint, float smooth, float emission = 0f, float tile = 1f, float clip = -1f,
            bool transparent = false, bool twoSided = false, float parallax = 0f, string dir = MatDir)
        {
            Texture2D Find(string kind) => T2($"{prefix}_{kind}.jpg") ?? T2($"{prefix}_{kind}.png");
            var alb = Find("albedo");
            if (!alb) return null;
            var m = Mat(name, dir: dir);
            m.SetTexture("_BaseMap", alb);
            m.SetColor("_BaseColor", tint);
            var nor = Find("normal");
            if (nor) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            var mask = Find("mask");
            if (mask)
            {
                m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f);
                m.SetFloat("_Smoothness", smooth);
            }
            else { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f); }
            var h = Find("height");
            if (h && parallax > 0f) { m.SetTexture("_ParallaxMap", h); m.SetFloat("_Parallax", parallax); m.EnableKeyword("_PARALLAXMAP"); }
            var e = Find("emission");
            if (e && emission > 0f)
            {
                m.SetTexture("_EmissionMap", e);
                m.SetColor("_EmissionColor", Color.white * emission);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (clip >= 0f) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", clip); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout"); }
            if (twoSided) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
            if (transparent) MakeTransparent(m, true);
            Done(m, tile, !transparent);
            return m;
        }

        static void MakeTransparent(Material m, bool premulOff)
        {
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
        }

        static void P(string name, Color color, float smooth, float metal, bool shadow = true)
        {
            var m = Mat(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            Done(m, 2f, shadow);
        }

        static void CarPaint(string name, Color color, float metal = 0.55f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Complex Lit");
            var m = Mat(name, sh ? "Universal Render Pipeline/Complex Lit" : "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.7f);
            m.SetFloat("_Metallic", metal);
            if (sh)
            {
                m.SetFloat("_ClearCoat", 1f); m.EnableKeyword("_CLEARCOAT");
                m.SetFloat("_ClearCoatMask", 1f); m.SetFloat("_ClearCoatSmoothness", 0.95f);
            }
            Done(m, 2f, true);
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

        static void Glass(string name, Color color, float smooth = 0.96f)
        {
            var m = Mat(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0.1f);
            MakeTransparent(m, true);
            Done(m, 2f, false);
        }

        // ------------------------------------------------------------------ facades
        static void CreateFacades()
        {
            string F = $"{CityArt}/Facades";
            // style, texture base (normal/mask/height), matching plain wall, trim, residential?
            var defs = new (string name, string bas, string wall, string trim, bool res)[]
            {
                ("BrickRed", "Res", "Brick", "Stone", true), ("BrickCream", "Res", "PlasterCream", "PlasterWhite", true),
                ("BrickWhite", "Res", "PlasterWhite", "Stone", true), ("BrickOchre", "Res", "PlasterOchre", "PlasterCream", true),
                ("StoneGrey", "Office", "PlasterGrey", "PlasterWhite", false), ("StoneSand", "Office", "PlasterSand", "PlasterWhite", false),
                ("LoftBrown", "Loft", "BrickDark", "Stone", false), ("LoftCharcoal", "Loft", "PlasterCharcoal", "Stone", false),
            };
            foreach (var d in defs)
            {
                var alb = T2($"{F}/{d.name}_albedo.jpg");
                var st = new FacadeStyle { Name = d.name, Wall = M(d.wall), Trim = M(d.trim), Residential = d.res, BayWidth = d.res ? 2.9f : 3.1f, FloorHeight = 3.2f };
                if (alb)
                {
                    var m = Mat("Facade" + d.name);
                    m.SetTexture("_BaseMap", alb);
                    m.SetColor("_BaseColor", Color.white);
                    var nor = T2($"{F}/{d.bas}_normal.jpg");
                    if (nor) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
                    var mask = T2($"{F}/{d.bas}_mask.png");
                    if (mask)
                    {
                        m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f);
                        m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f);
                    }
                    var h = T2($"{F}/{d.bas}_height.png");
                    if (h) { m.SetTexture("_ParallaxMap", h); m.SetFloat("_Parallax", 0.012f); m.EnableKeyword("_PARALLAXMAP"); }
                    var e = T2($"{F}/{d.name}_emission.jpg");
                    if (e)
                    {
                        m.SetTexture("_EmissionMap", e); m.SetColor("_EmissionColor", Color.white * 1.3f); m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    Done(m, 1f, true);
                    st.Mat = m;
                }
                else st.Mat = st.Wall;
                Facades[d.name] = st;
            }
        }

        /// <summary>Facade style matching a legacy wall material (keeps old CityProps.Building callers working).</summary>
        public static FacadeStyle StyleFor(Material wall)
        {
            string n = wall ? wall.name : "";
            return Facade(n switch
            {
                "Brick" => "BrickRed", "BrickDark" => "LoftBrown", "PlasterCream" => "BrickCream", "PlasterGrey" => "StoneGrey",
                "PlasterTeal" => "BrickWhite", "PlasterTerracotta" => "BrickOchre", "Concrete" => "StoneSand", _ => "BrickRed",
            });
        }

        // ------------------------------------------------------------------ props (Poly Haven)
        static void LoadProps()
        {
            string root = $"{CityArt}/Props";
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                string id = Path.GetFileName(dir), fbx = $"{root}/{id}/{id}.fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (!go) { Debug.LogWarning("[Parasite] prop model missing: " + fbx); continue; }
                var sets = Directory.GetFiles(dir, "*_diff_1k.*").Where(f => !f.EndsWith(".meta"))
                    .Select(f => Path.GetFileName(f)).Select(f => f.Substring(0, f.IndexOf("_diff_1k"))).Distinct().ToList();
                var pm = new PropModel { Id = id, Lod = PropLod.TryGetValue(id, out var lod) ? lod : 1 };
                bool first = true;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf.sharedMesh;
                    if (!mesh || SkipPart(id, mesh.name)) continue;
                    var mr = mf.GetComponent<MeshRenderer>();
                    var local = mf.transform.localToWorldMatrix;   // includes the asset root's own transform (unit scale / axis fix)
                    var src = mr ? mr.sharedMaterials : new Material[0];
                    var ms = new Material[mesh.subMeshCount];
                    for (int i = 0; i < ms.Length; i++)
                        ms[i] = PropMaterial(id, dir, sets, i < src.Length && src[i] ? src[i].name : "");
                    pm.Parts.Add((mesh, local, ms));
                    var b = TransformBounds(mesh.bounds, local);
                    if (first) { pm.Bounds = b; first = false; } else pm.Bounds.Encapsulate(b);
                    int l = Mathf.Clamp(pm.Lod, 0, Mathf.Max(0, mesh.lodCount - 1));
                    for (int i = 0; i < mesh.subMeshCount; i++) pm.Triangles += mesh.GetTriangles(i, l, true).Length / 3;
                }
                Props[id] = pm;
                Debug.Log($"[Parasite] prop {id}: size {pm.Bounds.size.x:F2} x {pm.Bounds.size.y:F2} x {pm.Bounds.size.z:F2} m, min y {pm.Bounds.min.y:F2}, " +
                          $"{pm.Triangles} tris (lod {pm.Lod}), parts {pm.Parts.Count}: " + string.Join(",", pm.Parts.Select(p => p.mesh.name + "/" + p.mesh.lodCount)));
            }
        }

        // Mesh LOD merged per prop (each level roughly halves the triangle count). Repeated sidewalk fixtures
        // keep their silhouettes/normal maps without merging thousands of invisible bevel triangles per instance.
        static readonly Dictionary<string, int> PropLod = new()
        {
            ["street_lamp_01"] = 5, ["street_lamp_02"] = 4, ["fire_hydrant"] = 5, ["metal_trash_can"] = 3, ["exterior_aircon_unit"] = 3,
            ["concrete_road_barrier_02"] = 4, ["covered_car"] = 2, ["shrub_04"] = 2, ["trashbag"] = 3, ["cardboard_box_01"] = 4,
            ["wooden_crate_01"] = 2, ["plastic_crate_02"] = 3, ["barrel_03"] = 1, ["old_tyre"] = 2, ["utility_box_01"] = 2, ["utility_box_02"] = 3,
            ["planter_box_01"] = 3, ["security_camera_01"] = 4, ["security_light"] = 2, ["rollershutter_door"] = 0, ["rollershutter_window_01"] = 0,
            ["industrial_wall_lamp"] = 3, ["modular_fire_escape"] = 2, ["modular_metal_gutter"] = 3, ["modular_street_seating"] = 3,
            ["water_manhole_cover"] = 3,
        };

        static bool SkipPart(string id, string part)
        {
            string p = part.ToLowerInvariant();
            if (p.Contains("_aged") || p.Contains("_rust") || p.Contains("graffiti")) return true;
            if (p.Contains("_lod") && !p.EndsWith("_lod0")) return true;                 // authored LOD chains: keep LOD0 only
            if (id == "shrub_04" && !p.Contains("_a_")) return true;                    // the FBX holds four shrubs side by side
            return false;
        }

        static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return r;
        }

        static Material PropMaterial(string id, string dir, List<string> sets, string srcName)
        {
            string n = srcName.ToLowerInvariant();
            string set = sets.FirstOrDefault(s => s.ToLowerInvariant() == n)
                         ?? sets.OrderByDescending(s => s.Length).FirstOrDefault(s => n.Contains(s.ToLowerInvariant()) || s.ToLowerInvariant().Contains(n) && n.Length > 3)
                         ?? sets.FirstOrDefault(s => !s.Contains("rust") && !s.Contains("aged") && !s.Contains("graffiti")) ?? sets.FirstOrDefault();
            if (set == null) return M("MetalDark");
            string key = "Prop_" + set;
            if (mats.TryGetValue(key, out var have)) return have;
            string p = $"{dir}/{set}".Replace('\\', '/');
            var alb = T2(p + "_diff_1k.png") ?? T2(p + "_diff_1k.jpg");
            bool alpha = alb && AssetDatabase.GetAssetPath(alb).EndsWith(".png");
            var m = Mat(key, dir: MatDir + "/Props");
            m.SetTexture("_BaseMap", alb);
            m.SetColor("_BaseColor", Color.white);
            var nor = T2(p + "_nor_gl_1k.jpg");
            if (nor) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
            var mask = T2(p + "_mask.png");
            if (mask)
            {
                m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f);
                m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP");
            }
            var em = T2(p + "_emissive_1k.jpg");
            if (em)
            {
                m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionColor", Color.white * (id.Contains("lamp") || id.Contains("light") ? 3.5f : 1.5f));
                m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (alpha) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.AlphaTest; m.SetFloat("_Cull", 0f); }
            Done(m, 1f, true);
            mats[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ sky + post + pipeline
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
                m.SetFloat("_Exposure", 1.0f);
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
            bl.threshold.Override(1.1f); bl.intensity.Override(0.5f); bl.scatter.Override(0.68f);
            bl.tint.Override(new Color(1f, 0.93f, 0.86f)); bl.highQualityFiltering.Override(true);
            var ca = p.Add<ColorAdjustments>();
            ca.postExposure.Override(0.25f); ca.contrast.Override(14f); ca.saturation.Override(6f);
            var vg = p.Add<Vignette>(); vg.intensity.Override(0.22f); vg.smoothness.Override(0.45f);
            var wb = p.Add<WhiteBalance>(); wb.temperature.Override(3f); wb.tint.Override(1f);
            var sp = p.Add<SplitToning>();
            sp.shadows.Override(new Color(0.36f, 0.52f, 0.58f)); sp.highlights.Override(new Color(0.75f, 0.58f, 0.4f)); sp.balance.Override(-15f);
            var smh = p.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.96f, 1.0f, 1.05f, 0f));
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

        /// <summary>PC quality: shadow distance/cascades, SSAO strength (PC_RPAsset / PC_Renderer).</summary>
        static void ConfigurePipeline()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (rp)
            {
                rp.shadowDistance = 120f;
                rp.shadowCascadeCount = 4;
                rp.cascade4Split = new Vector3(0.067f, 0.2f, 0.47f);
                rp.cascadeBorder = 0.12f;
                rp.mainLightShadowmapResolution = 4096;
                EditorUtility.SetDirty(rp);
            }
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (rd)
            {
                foreach (var f in rd.rendererFeatures)
                {
                    if (!f || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    var so = new SerializedObject(f);
                    var s = so.FindProperty("m_Settings");
                    if (s == null) continue;
                    void Set(string n, float v) { var pr = s.FindPropertyRelative(n); if (pr != null) pr.floatValue = v; }
                    Set("Intensity", 0.75f); Set("Radius", 0.45f); Set("DirectLightingStrength", 0.3f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(rd);
            }
        }
    }
}
