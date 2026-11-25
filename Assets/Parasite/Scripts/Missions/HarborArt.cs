using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>Owns Harbor's runtime surface copies and procedural meshes; imported source assets remain immutable.</summary>
    public sealed class HarborArt : MonoBehaviour
    {
        public const string ResourceRoot = "Harbor/";
        readonly List<Material> ownedMaterials = new();
        readonly List<Mesh> ownedMeshes = new();
        readonly Dictionary<int, Mesh> boxMeshes = new();
        Material shared, signal;
        RiverReflectionCapture reflection;
        public int ModelInstances { get; private set; }
        public long RepeatedModelTriangles { get; private set; }
        public Material Shared
        {
            get
            {
                if (shared) return shared;
                var source = Resources.Load<Material>(ResourceRoot + "HarborSharedPBR");
                if (!source) throw new InvalidOperationException("Harbor shared gate-atlas material is missing. Import the complete authored kit.");
                shared = new Material(source) { name = "Harbor owned shared PBR" }; ownedMaterials.Add(shared); return shared;
            }
        }
        public GameObject Model(string asset, Vector3 position, float yaw = 0, Transform parent = null)
        {
            var source = Resources.Load<GameObject>(ResourceRoot + "Harbor" + asset);
            if (!source) throw new InvalidOperationException("Missing authored Harbor assembly: " + asset);
            var go = Instantiate(source, parent ? parent : transform, false); go.name = "Harbor " + asset + " visual";
            go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (go.GetComponentsInChildren<Collider>(true).Length != 0 || go.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Harbor visual-only model unexpectedly imported physics: " + asset);
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            { renderer.sharedMaterials = new[] { Shared }; renderer.receiveShadows = true; renderer.shadowCastingMode = ShadowCastingMode.On; }
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh) RepeatedModelTriangles += (long)filter.sharedMesh.GetIndexCount(0) / 3;
            ModelInstances++; return go;
        }
        Mesh BoxMesh(int tile)
        {
            if (boxMeshes.TryGetValue(tile, out var existing)) return existing;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh); mesh.name = "Harbor atlas tile " + tile;
            primitive.SetActive(false); Destroy(primitive);
            var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2((tile % 2) * .5f + .008f + uv[i].x * .484f, (tile / 2) * .5f + .008f + uv[i].y * .484f);
            mesh.uv = uv; mesh.RecalculateTangents(); boxMeshes.Add(tile, mesh); ownedMeshes.Add(mesh); return mesh;
        }
        public GameObject Box(string name, Vector3 position, Vector3 size, int tile = 0, bool solid = true, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent ? parent : transform, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(tile); go.AddComponent<MeshRenderer>().sharedMaterial = Shared;
            if (solid) go.AddComponent<BoxCollider>(); return go;
        }
        public Renderer Lamp(string name, Vector3 position, Transform parent = null)
        {
            if (!signal)
            {
                signal = new Material(Shared) { name = "Harbor owned indicator variant" }; signal.EnableKeyword("_EMISSION"); ownedMaterials.Add(signal);
            }
            var go = Box(name, position, new Vector3(.13f, .10f, .07f), 3, false, parent);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = signal;
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", new Color(.15f,.45f,.35f)); block.SetColor("_EmissionColor", new Color(.1f,.5f,.3f)); renderer.SetPropertyBlock(block); return renderer;
        }
        public void Paving()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            for (float x=-40;x<40;x+=4) for(float z=-42;z<48;z+=4)
            {
                int n=vertices.Count;float end=Mathf.Min(z+4,48);
                vertices.AddRange(new[] { new Vector3(x,.149f,z),new Vector3(x,.149f,end),new Vector3(x+4,.149f,end),new Vector3(x+4,.149f,z) });
                uv.AddRange(new[] { new Vector2(.008f,.008f),new Vector2(.008f,.492f),new Vector2(.492f,.492f),new Vector2(.492f,.008f) });
                indices.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 });
            }
            var mesh=new Mesh { name="Harbor four-metre concrete paving" };mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject("Harbor authored-atlas paving");go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Shared;
        }
        public void BuildWater()
        {
            var shader = Resources.Load<Shader>("Shaders/RiverWater"); if (!shader) throw new InvalidOperationException("Existing river shader missing.");
            var material = new Material(shader) { name = "Harbor owned tidal water" }; ownedMaterials.Add(material);
            material.SetFloat("_ShoreZ", HarborLayout.Origin.z - 42); material.SetFloat("_CleanWater", 1);
            var mesh = new Mesh { name = "Harbor water quad" };
            mesh.vertices = new[] { new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f) };
            mesh.normals = new[] { Vector3.up,Vector3.up,Vector3.up,Vector3.up }; mesh.uv = new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            var water = new GameObject("Harbor tidal water") { layer = 4 }; water.transform.SetParent(transform,false); water.transform.localPosition = new Vector3(0,-.3f,-72); water.transform.localScale = new Vector3(190,1,100);
            water.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = water.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            var cutout = gameObject.AddComponent<SkylineWaterCutout>();
            if (!cutout.ApplyToSkyline(new[] { renderer.bounds })) Debug.LogWarning("[Harbor] Water skyline cutout unavailable: " + cutout.LastFailure);
            // The existing capture owner enforces quality, image publication and retirement. Null runs never create a probe.
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) { reflection = gameObject.AddComponent<RiverReflectionCapture>(); reflection.Configure(new Vector3(0,3,-42)); }
        }
        void OnDestroy()
        {
            if (reflection) reflection.Dispose();
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
        }
    }
}
