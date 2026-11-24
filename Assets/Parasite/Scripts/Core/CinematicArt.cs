using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite
{
    /// <summary>Meter-scale film sets authored in Blender; PBR surfaces are shared within each set instance.</summary>
    public static class CinematicArt
    {
        public const string ResourceRoot = "Cinematics/";

        public static GameObject Instantiate(string asset, Transform parent, int layer, List<Material> ownedMaterials)
        {
            var model = Resources.Load<GameObject>(ResourceRoot + asset);
            if (!model) throw new InvalidOperationException("Missing Blender cinematic model: " + ResourceRoot + asset);
            var instance = UnityEngine.Object.Instantiate(model, parent, false);
            instance.name = asset + " (Blender)";
            var surfaces = new Dictionary<string, Material>();
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string key = materials[i] ? materials[i].name.Replace(" (Instance)", "") : "CinemaSteel";
                    if (!surfaces.TryGetValue(key, out var material))
                    {
                        material = Surface(key);
                        surfaces.Add(key, material); ownedMaterials.Add(material);
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            foreach (var item in instance.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = layer;
            return instance;
        }

        static Material Surface(string name)
        {
            string texture = null;
            Color color = Color.white;
            float smoothness = 0.3f, metallic = 0f;
            if (name.Contains("Timber")) { texture = "Timber"; smoothness = 0.28f; }
            else if (name.Contains("Steel")) { texture = "Steel"; smoothness = 0.56f; metallic = 0.8f; }
            else if (name.Contains("Shore")) { texture = "Shore"; smoothness = 0.08f; }
            else if (name.Contains("Stone")) { texture = "Stone"; smoothness = 0.16f; }
            else if (name.Contains("Dark")) { color = new Color(0.055f, 0.075f, 0.08f); smoothness = 0.42f; metallic = 0.68f; }
            else if (name.Contains("Paint")) { color = new Color(0.27f, 0.36f, 0.38f); smoothness = 0.38f; metallic = 0.28f; }
            else if (name.Contains("Reed")) { color = new Color(0.22f, 0.27f, 0.12f); smoothness = 0.1f; }
            else if (name.Contains("Tip")) { color = new Color(0.15f, 0.09f, 0.04f); smoothness = 0.05f; }
            else if (name.Contains("Glow")) color = new Color(0.10f, 0.65f, 0.46f);
            else if (name.Contains("Warm")) color = new Color(0.95f, 0.54f, 0.18f);
            var material = MatFactory.LitUnique(color, smoothness, metallic);
            material.name = name + " PBR";
            if (texture != null)
            {
                var albedo = Resources.Load<Texture2D>(ResourceRoot + "Textures/" + texture + "_A");
                var normal = Resources.Load<Texture2D>(ResourceRoot + "Textures/" + texture + "_N");
                var mask = Resources.Load<Texture2D>(ResourceRoot + "Textures/" + texture + "_M");
                if (albedo) material.SetTexture("_BaseMap", albedo);
                if (normal) { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 0.65f); material.EnableKeyword("_NORMALMAP"); }
                if (mask) { material.SetTexture("_MetallicGlossMap", mask); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            }
            if (name.Contains("Glow") || name.Contains("Warm")) MatFactory.SetEmission(material, color * 1.4f);
            return material;
        }
    }
}
