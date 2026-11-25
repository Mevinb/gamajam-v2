using System;
using System.Linq;
using Parasite.Missions;
using Parasite.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Editor-only paired visual kit. Missing/invalid inputs always retain the old gate rendering.</summary>
    internal static class CityGatePilotArt
    {
        internal const string Root = "Assets/Parasite/Art/CityGatePilot/";
        internal const string ModelPath = Root + "CityGatePilot.fbx";
        internal const string MaterialPath = Root + "CityGatePilot_SharedPBR.mat";
        internal sealed class Kit
        {
            internal Mesh staticMesh, movingMesh;
            internal Matrix4x4 staticMatrix, movingMatrix;
            internal Material material;
        }
        internal static bool TryLoad(out Kit kit, out string reason)
        {
            kit = null; reason = null;
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!importer || !model || !material || !material.shader || material.shader.name != "Universal Render Pipeline/Lit")
                return Fail("Model or explicit opaque URP material is missing.", out reason);
            if (importer.addCollider || importer.importAnimation || !importer.isReadable || !importer.bakeAxisConversion || !importer.useFileScale || Mathf.Abs(importer.globalScale - 1) > .00001f)
                return Fail("Model import policy does not match the verified metre-scale visual-only contract.", out reason);
            if (model.GetComponentsInChildren<Collider>(true).Length != 0 || model.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                return Fail("Imported kit unexpectedly includes physics.", out reason);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            var stationaryParts = filters.Where(f => f.name.StartsWith("GateStaticShell", StringComparison.Ordinal)).ToArray();
            var movingParts = filters.Where(f => f.name.StartsWith("GateMovingLeaf", StringComparison.Ordinal)).ToArray();
            if (filters.Length != 2 || stationaryParts.Length != 1 || movingParts.Length != 1) return Fail("Both semantic mesh parts are required.", out reason);
            var stationary = stationaryParts[0]; var moving = movingParts[0];
            var sm = model.transform.worldToLocalMatrix * stationary.transform.localToWorldMatrix;
            var mm = model.transform.worldToLocalMatrix * moving.transform.localToWorldMatrix;
            if (!MeshContract(stationary.sharedMesh, sm, new(-8, -.15f, -.4f), new(8, 4.35f, .4f), 2616) ||
                !MeshContract(moving.sharedMesh, mm, new(-1.45f, -1.5f, -.13f), new(1.45f, 1.5f, .1f), 1192) || !Decomposable(mm))
                return Fail("Mesh counts, finite data, transformed bounds or moving-part transform are invalid.", out reason);
            foreach (string suffix in new[] { "A", "N", "M" })
            {
                string path = Root + "Textures/CityGatePilot_" + suffix + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var policy = AssetImporter.GetAtPath(path) as TextureImporter;
                bool normal = suffix == "N", srgb = suffix == "A";
                if (!texture || !policy || texture.width != 2048 || texture.height != 2048 || policy.maxTextureSize != 2048 ||
                    policy.textureShape != TextureImporterShape.Texture2D || policy.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) ||
                    policy.sRGBTexture != srgb || !policy.mipmapEnabled || texture.mipmapCount <= 1)
                    return Fail("All three correctly imported 2K Texture2D PBR maps are required.", out reason);
                string property = suffix == "A" ? "_BaseMap" : suffix == "N" ? "_BumpMap" : "_MetallicGlossMap";
                if (material.GetTexture(property) != texture) return Fail("Material PBR map references are incomplete.", out reason);
            }
            if (!material.IsKeywordEnabled("_NORMALMAP") || !material.IsKeywordEnabled("_METALLICSPECGLOSSMAP") ||
                Mathf.Abs(material.GetFloat("_Surface")) > .0001f || Mathf.Abs(material.GetFloat("_Metallic") - 1) > .0001f || Mathf.Abs(material.GetFloat("_Smoothness") - 1) > .0001f)
                return Fail("Material packing/opacity contract is invalid.", out reason);
            kit = new Kit { staticMesh = stationary.sharedMesh, movingMesh = moving.sharedMesh, staticMatrix = sm, movingMatrix = mm, material = material };
            return true;
        }
        static bool Fail(string message, out string reason) { reason = message; return false; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool MeshContract(Mesh mesh, Matrix4x4 matrix, Vector3 min, Vector3 max, long triangles)
        {
            if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles || mesh.GetIndexCount(0) / 3 != triangles || matrix.determinant <= 0) return false;
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            if (vertices.Length == 0 || normals.Length != vertices.Length || uv.Length != vertices.Length ||
                vertices.Any(v => !Finite(v.x) || !Finite(v.y) || !Finite(v.z)) || normals.Any(v => !Finite(v.x) || !Finite(v.y) || !Finite(v.z)) ||
                uv.Any(v => !Finite(v.x) || !Finite(v.y))) return false;
            var points = vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            return Vector3.Distance(bounds.min, min) < .001f && Vector3.Distance(bounds.max, max) < .001f;
        }
        static bool Decomposable(Matrix4x4 matrix)
        {
            var rebuilt = Matrix4x4.TRS(matrix.GetColumn(3), matrix.rotation, matrix.lossyScale);
            for (int i = 0; i < 16; i++) if (!Finite(matrix[i]) || Mathf.Abs(matrix[i] - rebuilt[i]) > .0001f) return false;
            return true;
        }
        internal static bool TryAddStatic(WorldKit kit)
        {
            if (!TryLoad(out var source, out string reason)) { Debug.LogWarning("[GatePilot] Retaining original gate visuals: " + reason); return false; }
            // WorldKit copies real vertex/normal/UV data, and recalculates tangents for the shared normal-mapped material.
            kit.Mesh(source.material, source.staticMesh, Matrix4x4.TRS(CityLayout.MainGate, Quaternion.identity, Vector3.one) * source.staticMatrix);
            return true;
        }
        internal static bool TryAddMoving(AccessDoor door)
        {
            if (!door || !door.panel || !TryLoad(out var source, out _)) return false;
            var panel = door.panel;
            if (Vector3.Distance(panel.localScale, new(2.9f, 3, .2f)) > .0001f || Vector3.Distance(panel.localPosition, new(0, 1.5f, 0)) > .0001f || Quaternion.Angle(panel.localRotation, Quaternion.identity) > .001f)
                return false;
            var original = panel.GetComponent<MeshRenderer>(); if (!original) return false;
            GameObject visual = null;
            try
            {
                visual = new GameObject("CityGatePilot_Moving"); visual.transform.SetParent(panel, false);
                visual.transform.localScale = new(1 / panel.localScale.x, 1 / panel.localScale.y, 1 / panel.localScale.z);
                var meshObject = new GameObject("GateMovingLeaf"); meshObject.transform.SetParent(visual.transform, false);
                meshObject.transform.localPosition = source.movingMatrix.GetColumn(3);
                meshObject.transform.localRotation = source.movingMatrix.rotation; meshObject.transform.localScale = source.movingMatrix.lossyScale;
                meshObject.AddComponent<MeshFilter>().sharedMesh = source.movingMesh;
                var renderer = meshObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = source.material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                // Only this renderer changes. The original panel collider, obstacle and AccessDoor target remain identical.
                original.enabled = false; return true;
            }
            catch (Exception e)
            {
                if (visual) Object.DestroyImmediate(visual);
                Debug.LogWarning("[GatePilot] Moving visual failed; keeping original panel/ribs. " + e.Message);
                return false;
            }
        }
        internal static void PrepareMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (!shader) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material) { material = new Material(shader) { name = "CityGatePilot_SharedPBR" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            material.name = "CityGatePilot_SharedPBR";
            material.shader = shader; material.SetColor("_BaseColor", Color.white); material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1); material.SetFloat("_Cull", (float)CullMode.Back); material.SetFloat("_AlphaToMask", 0);
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_BumpScale", .65f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/CityGatePilot_A.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/CityGatePilot_N.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/CityGatePilot_M.png"));
            material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_EMISSION"); material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = -1; EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        }
    }
}
