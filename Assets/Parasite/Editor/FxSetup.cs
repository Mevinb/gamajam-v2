using System.IO;
using Parasite.FX;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Bakes the FX assets into Assets/Parasite/Resources/FX so the custom shaders and URP particle variants
    /// are referenced (and therefore survive build stripping): amoeba + nucleus materials, particle
    /// materials, and procedurally generated particle textures. Idempotent.
    /// </summary>
    public static class FxSetup
    {
        const string Dir = "Assets/Parasite/Resources/FX";

        [MenuItem("Parasite/Setup/FX Materials")]
        public static void Run()
        {
            Directory.CreateDirectory(Dir);

            var soft = BakeTexture(FxTextures.SoftDot(), "SoftDot");
            var ring = BakeTexture(FxTextures.Ring(), "Ring");
            var streak = BakeTexture(FxTextures.Streak(), "Streak");
            var splat = BakeTexture(FxTextures.Splat(), "Splat");

            var amoebaShader = LoadShader("Assets/Parasite/Shaders/Amoeba.shader", FxMaterials.AmoebaShader);
            var nucleusShader = LoadShader("Assets/Parasite/Shaders/AmoebaNucleus.shader", FxMaterials.NucleusShader);
            var particleShader = Shader.Find(FxMaterials.ParticleShader);
            if (!amoebaShader) Debug.LogError("[FxSetup] Shader 'Parasite/Amoeba' not found (compile error?). Check Assets/Parasite/Shaders.");
            if (!nucleusShader) Debug.LogError("[FxSetup] Shader 'Parasite/AmoebaNucleus' not found.");
            if (!particleShader) Debug.LogError("[FxSetup] URP particle shader not found.");

            if (amoebaShader)
            {
                var m = Ensure("AmoebaMaterial", amoebaShader);
                m.renderQueue = 3050;   // after the other transparents: the jelly refracts the scene behind it
                m.SetFloat("_Alpha", 0.26f);
                m.SetFloat("_Emission", 0.12f);
                m.SetFloat("_Refraction", 0.012f);
                m.SetFloat("_RefractWeight", 0.9f);
                m.SetColor("_AbsorbColor", new Color(0.52f, 0.84f, 0.64f, 1f));
                m.SetColor("_BaseColor", new Color(0.16f, 0.42f, 0.24f, 1f));
                m.SetColor("_DeepColor", new Color(0.02f, 0.075f, 0.035f, 1f));
                m.SetColor("_RimColor", new Color(0.16f, 0.40f, 0.24f, 1f));
                m.SetColor("_SSSColor", new Color(0.18f, 0.38f, 0.22f, 1f));
                m.SetColor("_CoreColor", new Color(0.12f, 0.32f, 0.18f, 1f));
                m.SetFloat("_Gloss", 96f);
                m.SetFloat("_PodAmount", 0.09f);
                EditorUtility.SetDirty(m);
            }
            if (nucleusShader)
            {
                var m = Ensure("AmoebaNucleus", nucleusShader);
                m.SetFloat("_RadianceScale", 0.18f);
                EditorUtility.SetDirty(m);
            }
            var water = LoadShader("Assets/Parasite/Shaders/GroundWater.shader", FxMaterials.SlimeShader);
            if (water)
            {
                // glossy, faintly glowing slime decals left by the amoeba (vertex alpha fades them)
                var m = Ensure("SlimeTrail", water);
                FxMaterials.ConfigureSlime(m, splat);
                EditorUtility.SetDirty(m);
            }
            if (particleShader)
            {
                Particle("ParticleAdditive", particleShader, true, soft);
                Particle("ParticleAlpha", particleShader, false, soft);
                Particle("RingAdditive", particleShader, true, ring);
                Particle("TrailAdditive", particleShader, true, streak);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Parasite] FX materials ready in " + Dir);
        }

        static Shader LoadShader(string path, string name)
        {
            if (File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (sh && ShaderUtil.ShaderHasError(sh)) Debug.LogError($"[FxSetup] Shader {name} has compile errors — see the shader inspector.");
            return sh ? sh : Shader.Find(name);
        }

        static Material Ensure(string name, Shader shader)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        static void Particle(string name, Shader shader, bool additive, Texture2D tex)
        {
            var m = Ensure(name, shader);
            FxMaterials.ConfigureParticle(m, additive, tex);
            EditorUtility.SetDirty(m);
        }

        static Texture2D BakeTexture(Texture2D generated, string name)
        {
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.sRGBTexture = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
