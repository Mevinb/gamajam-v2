using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>Detailed Blender visuals over the tested riverside collision and interaction layout.</summary>
    public sealed class AfterlakeProps : MonoBehaviour
    {
        readonly Dictionary<string, Material> surfaces = new();
        readonly List<Material> ownedMaterials = new();
        bool built;

        public void Build()
        {
            if (built) return;
            built = true;
            Replace("RiversideFerry", new Vector3(270, 0.15f, -30),
                "Ferry deck", "Ferry cabin", "Ferry cabin glazing", "Ferry intake grille", "Sign · RIVERSIDE / SOUTH FERRY");
            Replace("RadioMast", new Vector3(270, 0.15f, 19), "Radio mast");
            HideNamed("Antenna cross arm");
            // Pump skid sits beside the interactive floodgate pedestal, inside the existing open-front shed.
            InstantiateModel("FloodgateMachinery", transform, new Vector3(215, 0.15f, -15.9f), 0);

            foreach (var operation in GetComponentsInChildren<CampaignOperation>())
            {
                string asset = operation.flag == AfterlakeCampaign.WaterAnalysed || operation.flag == AfterlakeCampaign.CorridorOpen
                    ? "ClinicConsole" : operation.flag == AfterlakeCampaign.ChannelOpened ? "RadioConsole" : "IndustrialConsole";
                ReplaceTerminal(operation.transform, asset);
            }
            foreach (var control in GetComponentsInChildren<CooperativeConsole>())
                ReplaceTerminal(control.transform, control.controlId == AfterlakeCampaign.RadioControl ? "IndustrialConsole" : "RadioConsole");
            foreach (var receiver in GetComponentsInChildren<MissionProp>())
                if (receiver.flag == AfterlakeCampaign.Signal) ReplaceTerminal(receiver.transform, "RadioConsole");

            // The fabric/frame replacement has the same footprint as every old privacy-screen collider.
            foreach (Transform child in transform)
            {
                if (child.name != "Clinic privacy screen") continue;
                if (InstantiateModel("ClinicScreen", transform, child.localPosition - Vector3.up, 0))
                { var renderer = child.GetComponent<Renderer>(); if (renderer) renderer.enabled = false; }
            }
        }

        void ReplaceTerminal(Transform terminal, string asset)
        {
            if (!InstantiateModel(asset, terminal, Vector3.zero, Layers.Interactable)) return;
            var housing = terminal.Find("Housing");
            if (housing) { var renderer = housing.GetComponent<Renderer>(); if (renderer) renderer.enabled = false; }
            // The original Screen remains visible, and still receives live operation/control emission updates.
        }

        void Replace(string asset, Vector3 position, params string[] originalNames)
        {
            if (!InstantiateModel(asset, transform, position, 0)) return;
            foreach (string name in originalNames) HideNamed(name);
        }

        void HideNamed(string name)
        {
            foreach (Transform child in transform)
            {
                if (child.name != name) continue;
                var renderer = child.GetComponent<Renderer>(); if (renderer) renderer.enabled = false;
                // Never disable the GameObject: original collider bounds remain part of the NavMesh bake.
            }
        }

        GameObject InstantiateModel(string name, Transform parent, Vector3 localPosition, int layer)
        {
            var model = Resources.Load<GameObject>("Riverside/" + name);
            if (!model) { Debug.LogWarning("[AfterlakeProps] Missing Blender asset Riverside/" + name); return null; }
            var instance = Instantiate(model, parent, false); instance.name = name + " (Blender)";
            instance.transform.localPosition = localPosition;
            foreach (var item in instance.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = layer;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = Surface(materials[i] ? materials[i].name : "CinemaDark");
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            // These exports are visual assemblies. Game collision belongs exclusively to the tested layout.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return instance;
        }

        public Material Surface(string name)
        {
            name = name.Replace(" (Instance)", "");
            if (surfaces.TryGetValue(name, out var material)) return material;
            string texture = null, resourceRoot = "Riverside/Textures/";
            Color color = Color.white; float smoothness = 0.4f, metallic = 0;
            if (name.Contains("MarinePaint")) { texture = "MarinePaint"; smoothness = 0.48f; metallic = 0.18f; }
            else if (name.Contains("Rubber")) { texture = "Rubber"; smoothness = 0.14f; }
            else if (name.Contains("ClinicFabric")) { texture = "ClinicFabric"; smoothness = 0.14f; }
            else if (name.Contains("ClinicEnamel")) { texture = "ClinicEnamel"; smoothness = 0.73f; }
            else if (name.Contains("Steel")) { texture = "Steel"; resourceRoot = "Cinematics/Textures/"; smoothness = 0.64f; metallic = 0.85f; }
            else if (name.Contains("Timber")) { texture = "Timber"; resourceRoot = "Cinematics/Textures/"; smoothness = 0.25f; }
            else if (name.Contains("Glass")) { color = new Color(0.065f, 0.14f, 0.18f); smoothness = 0.94f; metallic = 0.3f; }
            else if (name.Contains("Dark")) { color = new Color(0.035f, 0.045f, 0.05f); smoothness = 0.5f; metallic = 0.65f; }
            else if (name.Contains("Cream")) { color = new Color(0.76f, 0.77f, 0.68f); smoothness = 0.6f; metallic = 0.1f; }
            else if (name.Contains("Red")) { color = new Color(0.53f, 0.07f, 0.045f); smoothness = 0.6f; metallic = 0.1f; }
            else if (name.Contains("Yellow")) { color = new Color(0.82f, 0.56f, 0.12f); smoothness = 0.55f; metallic = 0.2f; }
            else if (name.Contains("Glow")) { color = new Color(0.12f, 0.65f, 0.43f); smoothness = 0.76f; }
            else if (name.Contains("Warm")) { color = new Color(0.9f, 0.48f, 0.17f); smoothness = 0.75f; }
            else if (name.Contains("Shore")) { texture = "Shore"; resourceRoot = "Cinematics/Textures/"; smoothness = 0.06f; }
            else if (name.Contains("Stone")) { texture = "Stone"; resourceRoot = "Cinematics/Textures/"; smoothness = 0.12f; }
            else if (name.Contains("Reed")) { color = new Color(0.2f, 0.26f, 0.14f); smoothness = 0.1f; }
            material = MatFactory.LitUnique(color, smoothness, metallic);
            material.name = name + " PBR";
            if (texture != null)
            {
                var albedo = Resources.Load<Texture2D>(resourceRoot + texture + "_A");
                var normal = Resources.Load<Texture2D>(resourceRoot + texture + "_N");
                var mask = Resources.Load<Texture2D>(resourceRoot + texture + "_M");
                if (albedo) material.SetTexture("_BaseMap", albedo);
                if (normal) { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 0.6f); material.EnableKeyword("_NORMALMAP"); }
                if (mask) { material.SetTexture("_MetallicGlossMap", mask); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            }
            if (name.Contains("Glow") || name.Contains("Warm")) MatFactory.SetEmission(material, color * 1.4f);
            surfaces[name] = material; ownedMaterials.Add(material); return material;
        }

        void OnDestroy() { foreach (var material in ownedMaterials) if (material) Destroy(material); }
    }
}
