using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Owns the instantiated Blender seating materials for the playable quay.</summary>
    public sealed class AfterlakeArt : MonoBehaviour
    {
        readonly List<Material> materials = new();

        public void Build()
        {
            if (!Resources.Load<GameObject>(CinematicArt.ResourceRoot + "LakesideBench")) return;
            for (int i = 0; i < 3; i++)
            {
                var bench = CinematicArt.Instantiate("LakesideBench", transform, 0, materials);
                bench.transform.localPosition = new Vector3(198 + i * 21, 0.15f, -28);
                bench.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var collider = bench.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 0.5f, 0); collider.size = new Vector3(2.4f, 1, 0.9f);
            }
        }

        void OnDestroy() { foreach (var material in materials) if (material) Destroy(material); }
    }
}
