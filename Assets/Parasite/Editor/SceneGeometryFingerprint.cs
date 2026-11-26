using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite.EditorTools
{
    /// <summary>Semantic geometry inventory to verify storage changes without relying on scene YAML or mesh GUIDs.</summary>
    internal static class SceneGeometryFingerprint
    {
        [Serializable] sealed class Item { public string path, type, payload; }
        [Serializable] sealed class Inventory { public string scene; public List<Item> items = new(); }
        static string PathOf(Transform t) => (t.parent ? PathOf(t.parent) + "/" : "") + t.name + "[" + t.GetSiblingIndex() + "]";
        static string Hash(byte[] bytes) { using var h = SHA256.Create(); return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Vector(BinaryWriter w, Vector3 p) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
        static void Matrix(BinaryWriter w, Matrix4x4 m) { for (int i = 0; i < 16; i++) w.Write(m[i]); }
        static string Asset(UnityEngine.Object value) => value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) + ":" + value.name : "null";
        static string MeshHash(Mesh mesh)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            using var array = MeshUtility.AcquireReadOnlyMeshData(mesh); var data = array[0];
            writer.Write(data.vertexCount); writer.Write(data.vertexBufferCount); writer.Write((int)data.indexFormat);
            foreach (var attribute in mesh.GetVertexAttributes()) { writer.Write((int)attribute.attribute); writer.Write((int)attribute.format); writer.Write(attribute.dimension); writer.Write(attribute.stream); }
            for (int i = 0; i < data.vertexBufferCount; i++) writer.Write(data.GetVertexData<byte>(i).ToArray());
            writer.Write(data.GetIndexData<byte>().ToArray()); writer.Write(data.subMeshCount);
            for (int i = 0; i < data.subMeshCount; i++)
            { var s = data.GetSubMesh(i); writer.Write(s.indexStart); writer.Write(s.indexCount); writer.Write((int)s.topology); writer.Write(s.baseVertex); writer.Write(s.firstVertex); writer.Write(s.vertexCount); }
            return Hash(stream.ToArray());
        }
        public static void Write(Scene scene, string destination)
        {
            var inventory = new Inventory { scene = scene.name }; var cache = new Dictionary<Mesh, string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                    string hash;
                    if (mesh)
                    { if (!cache.TryGetValue(mesh, out hash)) { hash = MeshHash(mesh); cache.Add(mesh, hash); } }
                    else
                    {
                        // TextMesh owns an internal generated font mesh and has no MeshFilter.
                        var text = renderer.GetComponent<TextMesh>();
                        if (!text) throw new InvalidOperationException("Uninventoried renderer: " + PathOf(renderer.transform));
                        hash = Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new TextState(text))));
                    }
                    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                    writer.Write(hash); Matrix(writer, renderer.localToWorldMatrix); writer.Write(renderer.enabled); writer.Write(renderer.gameObject.activeSelf);
                    writer.Write((int)renderer.shadowCastingMode); writer.Write(renderer.receiveShadows); writer.Write(renderer.gameObject.layer);
                    foreach (var material in renderer.sharedMaterials) writer.Write(Asset(material));
                    inventory.items.Add(new Item { path = PathOf(renderer.transform), type = "MeshRenderer", payload = Hash(stream.ToArray()) });
                }
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                    Matrix(writer, collider.transform.localToWorldMatrix); writer.Write(collider.enabled); writer.Write(collider.isTrigger); writer.Write(collider.gameObject.activeSelf); writer.Write(collider.gameObject.layer); writer.Write(Asset(collider.sharedMaterial));
                    if (collider is BoxCollider box) { Vector(writer, box.center); Vector(writer, box.size); }
                    else if (collider is SphereCollider sphere) { Vector(writer, sphere.center); writer.Write(sphere.radius); }
                    else if (collider is CapsuleCollider capsule) { Vector(writer, capsule.center); writer.Write(capsule.radius); writer.Write(capsule.height); writer.Write(capsule.direction); }
                    else if (collider is MeshCollider mc) { writer.Write(mc.convex); writer.Write(mc.sharedMesh ? MeshHash(mc.sharedMesh) : "null"); }
                    else throw new InvalidOperationException("Uninventoried collider: " + collider.GetType().Name);
                    inventory.items.Add(new Item { path = PathOf(collider.transform), type = collider.GetType().Name, payload = Hash(stream.ToArray()) });
                }
            }
            inventory.items = inventory.items.OrderBy(r => r.path, StringComparer.Ordinal).ThenBy(r => r.type, StringComparer.Ordinal).ToList();
            File.WriteAllText(destination, JsonUtility.ToJson(inventory, true) + "\n");
        }
        [Serializable] sealed class TextState
        {
            public string text, font; public int size, anchor, alignment; public float scale, spacing, offset; public bool richText; public Color color;
            public TextState(TextMesh value)
            { text = value.text; font = Asset(value.font); size = value.fontSize; anchor = (int)value.anchor; alignment = (int)value.alignment; scale = value.characterSize; spacing = value.lineSpacing; offset = value.offsetZ; richText = value.richText; color = value.color; }
        }
    }
}
