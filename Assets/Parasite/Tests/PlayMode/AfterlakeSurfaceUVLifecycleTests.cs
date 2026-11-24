using System.Collections;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class AfterlakeSurfaceUVLifecycleTests
    {
        [UnityTest]
        public IEnumerator DestroyingRuntimeOwnerReleasesProjectedMeshes()
        {
            var root = new GameObject("Runtime surface cleanup fixture");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Sidewalk" };
            Mesh projected = null;
            try
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.transform.SetParent(root.transform);
                slab.transform.localScale = new Vector3(9, 0.14f, 6);
                slab.GetComponent<MeshRenderer>().sharedMaterial = material;
                var filter = slab.GetComponent<MeshFilter>();
                var original = filter.sharedMesh;
                root.AddComponent<AfterlakeSurfaceUV>().Apply();
                projected = filter.sharedMesh;
                Assert.AreNotSame(original, projected);
                Object.Destroy(root);
                yield return null; yield return null; // OnDestroy also schedules its owned Mesh for destruction.
                Assert.IsFalse(projected, "The runtime scene lifecycle releases UV meshes without explicit tool cleanup.");
                Assert.IsTrue(original, "The shared built-in primitive mesh remains available.");
            }
            finally
            {
                if (root) Object.Destroy(root);
                if (projected) Object.Destroy(projected);
                Object.Destroy(material);
            }
        }
    }
}
