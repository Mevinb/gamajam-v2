using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Static Blender detail dressing. Only adds visual triangles through WorldKit.Mesh: never colliders,
    /// navigation, lights, interaction objects, gameplay transforms or additional random-number consumption.
    /// </summary>
    internal static class EnvironmentRealismAssets
    {
        const string Root = EnvironmentRealismImporter.Root;
        const string MaterialPath = Root + "EnvironmentRealismShared.mat";
        static readonly string[] Names = { "WindowReveal", "EntranceSurround", "Condenser", "CurbStone", "DrainGrate", "FracturedStone", "GrassClump" };
        static readonly Dictionary<string, List<(Mesh mesh, Matrix4x4 local)>> Models = new();
        static Material material;

        public static void Prepare()
        {
            Models.Clear();
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Environment realism requires the existing URP Lit shader.");
            if (!material) { material = new Material(shader) { name = "EnvironmentRealismShared" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            else material.shader = shader;
            Texture2D Map(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/EnvironmentRealism_" + suffix + ".png")
                ?? throw new InvalidOperationException("Missing authored environment map " + suffix);
            material.SetTexture("_BaseMap", Map("A")); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Map("N")); material.SetFloat("_BumpScale", 1f); material.EnableKeyword("_NORMALMAP");
            var mask = Map("M");
            material.SetTexture("_MetallicGlossMap", mask); material.SetTexture("_OcclusionMap", mask);
            material.SetFloat("_Smoothness", 1f); material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            CityAssets.Tile[material] = 1f;

            foreach (string name in Names)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".fbx");
                if (!model) throw new InvalidOperationException("Missing authored environment model " + name);
                if (model.GetComponentsInChildren<Collider>(true).Length != 0 || model.GetComponentsInChildren<Light>(true).Length != 0)
                    throw new InvalidOperationException("Environment dressing must remain visual only: " + name);
                var parts = new List<(Mesh, Matrix4x4)>();
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh || !mf.sharedMesh.isReadable) throw new InvalidOperationException("Environment model must be readable for static generation: " + name);
                    parts.Add((mf.sharedMesh, model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix));
                }
                if (parts.Count == 0) throw new InvalidOperationException("Empty authored environment model " + name);
                Models.Add(name, parts);
            }

            // Existing scan-derived A/N/M and metre-scale UVs remain the source of fine detail.
            // Low-amplitude world-space colour/roughness variation removes the repeating ground carpet.
            var ground = Shader.Find("Parasite/RealisticSurface");
            if (!ground) throw new InvalidOperationException("Missing environment surface shader.");
            foreach (string name in new[] { "Asphalt", "Sidewalk", "Cobble", "Gravel", "Curb" })
            {
                var m = CityAssets.M(name); m.shader = ground;
                m.SetFloat("_DetailAlbedoMapScale", name == "Asphalt" ? 0.13f : 0.075f);
                m.SetFloat("_DetailNormalMapScale", name == "Gravel" ? 3.7f : 11.5f);
                m.SetFloat("_Parallax", name == "Asphalt" ? 0.045f : 0.025f);
                EditorUtility.SetDirty(m);
            }
        }

        static void Draw(WorldKit k, string name, Vector3 p, Quaternion q, Vector3 scale)
        {
            if (!material || !Models.TryGetValue(name, out var parts))
                throw new InvalidOperationException("Prepare environment assets before generating the city.");
            var trs = Matrix4x4.TRS(p, q, scale);
            foreach (var part in parts)
            {
                k.Mesh(material, part.mesh, trs * part.local);
                k.PropTriangles += part.mesh.triangles.Length / 3;
            }
        }

        public static void Windows(WorldKit k, CityProps.Face f, float groundTop, float bay, float floor, int floors, int bays)
        {
            // The photographed atlas's surveyed glass envelope is +/-0.31 of one bay/floor.
            // Dress the three closest storeys; far roofs retain the same scan + interior mapping.
            for (int row = 0; row < Mathf.Min(floors, 3); row++)
                for (int col = 0; col < bays; col++)
                    Draw(k, "WindowReveal", f.At((col + 0.5f) * bay, groundTop + (row + 0.5f) * floor, 0.016f),
                        f.Rot, new Vector3(bay * 0.62f, floor * 0.62f, 1f));
        }

        public static void Entrance(WorldKit k, CityProps.Face f, float along) =>
            Draw(k, "EntranceSurround", f.At(along, 0f, 0.005f), f.Rot, Vector3.one);

        public static void Condenser(WorldKit k, Vector3 p, Vector3 oldSize) =>
            Draw(k, "Condenser", p, Quaternion.identity, new Vector3(oldSize.x / 1.58f, oldSize.y / 0.92f, oldSize.z / 1.13f));

        public static void Curb(WorldKit k, float x0, float x1, float z0, float z1, Faces sides, float height)
        {
            void Run(Vector3 a, Vector3 b)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b)));
                float step = Vector3.Distance(a, b) / count;
                var q = Quaternion.Euler(0f, Mathf.Abs(a.x - b.x) < 0.01f ? 90f : 0f, 0f);
                for (int i = 0; i < count; i++) Draw(k, "CurbStone", Vector3.Lerp(a, b, (i + 0.5f) / count), q,
                    new Vector3(step, height / 0.162f, 1f));
            }
            // Matches previous curb strip centres (.3 m wide plus .02 m outer lip), with 3 mm joints.
            if ((sides & Faces.E) != 0) Run(new Vector3(x1 - 0.14f, 0, z0), new Vector3(x1 - 0.14f, 0, z1));
            if ((sides & Faces.W) != 0) Run(new Vector3(x0 + 0.14f, 0, z0), new Vector3(x0 + 0.14f, 0, z1));
            if ((sides & Faces.N) != 0) Run(new Vector3(x0, 0, z1 - 0.14f), new Vector3(x1, 0, z1 - 0.14f));
            if ((sides & Faces.S) != 0) Run(new Vector3(x0, 0, z0 + 0.14f), new Vector3(x1, 0, z0 + 0.14f));
        }

        public static void TreeBed(WorldKit k, Vector3 p, uint hash)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = ((hash & 0xffff) / 65535f) * Mathf.PI * 2f + i * 2.1f;
                var o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (0.36f + i * 0.065f);
                Draw(k, "GrassClump", p + o + Vector3.up * 0.015f, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), Vector3.one);
            }
            Draw(k, "FracturedStone", p + new Vector3(0.42f, 0.012f, -0.35f), Quaternion.Euler(0, hash % 360, 0), Vector3.one * 0.30f);
        }

        public static void Drain(WorldKit k, Vector3 p, float yaw) =>
            Draw(k, "DrainGrate", p, Quaternion.Euler(0, yaw, 0), new Vector3(0.9f / 0.62f, 0.4f, 0.45f / 0.64f));
    }
}
