using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    public enum VehicleBody { Sedan, Hatch, Suv, Van, BoxTruck }

    /// <summary>Shared Blender visual assets. Movement, colliders, lane routing and wheel spin remain owned by TrafficCar.</summary>
    public static class VehicleVisuals
    {
        public const string ResourceRoot = "Vehicles/";
        static readonly Dictionary<VehicleBody, GameObject> prefabs = new();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static Vector3[] WheelCentres(VehicleBody body)
        {
            if (body == VehicleBody.BoxTruck)
            {
                var result = new List<Vector3>();
                foreach (float x in new[] { -1.05f, 1.05f })
                    foreach (float z in new[] { -3f, -1.9f, 3.1f }) result.Add(new Vector3(x, .48f, z));
                return result.ToArray();
            }
            var dimensions = body switch
            {
                VehicleBody.Hatch => new Vector3(1.76f, .31f, 2.55f),
                VehicleBody.Suv => new Vector3(1.9f, .37f, 2.8f),
                VehicleBody.Van => new Vector3(1.85f, .34f, 3.2f),
                _ => new Vector3(1.85f, .34f, 2.7f)
            };
            var centres = new List<Vector3>();
            foreach (float side in new[] { -1f, 1f })
                foreach (float sign in new[] { -1f, 1f })
                    centres.Add(new Vector3(side * (dimensions.x * .5f - .10f), dimensions.y, sign * dimensions.z * .5f));
            return centres.ToArray();
        }

        public static GameObject Prefab(VehicleBody body)
        {
            if (prefabs.TryGetValue(body, out var prefab) && prefab) return prefab;
            prefab = Resources.Load<GameObject>(ResourceRoot + "Vehicle" + body);
            if (!prefab) throw new InvalidOperationException("Missing Blender vehicle kit: " + body + ". Run VehicleArtImporter.PrepareBatch before building the city.");
            prefabs[body] = prefab;
            return prefab;
        }

        public static GameObject Create(Transform parent, VehicleBody body, Color paint, bool taxi, out Transform[] wheels)
        {
            var visual = UnityEngine.Object.Instantiate(Prefab(body), parent, false);
            visual.name = "Blender " + body + " visual";
            var block = new MaterialPropertyBlock(); block.SetColor(BaseColor, paint);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                    if (materials[slot] && materials[slot].name == "VehiclePaint") renderer.SetPropertyBlock(block, slot);
            }
            if (taxi) AttachTaxiSign(visual);
            var pivots = new List<Transform>();
            foreach (var child in visual.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("WheelPivot_", StringComparison.Ordinal)) pivots.Add(child);
            pivots.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); wheels = pivots.ToArray();
            return visual;
        }

        public static void AttachTaxiSign(GameObject visual)
        {
            var source = Resources.Load<GameObject>(ResourceRoot + "VehicleTaxiSign");
            if (!source) throw new InvalidOperationException("Missing authored Blender taxi roof sign.");
            var sign = UnityEngine.Object.Instantiate(source, visual.transform, false); sign.name = "Authored taxi roof sign";
            var group = visual.GetComponent<LODGroup>();
            if (!group) return;
            var additions = sign.GetComponentsInChildren<Renderer>(true); var lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                var renderers = new List<Renderer>(lods[i].renderers); renderers.AddRange(additions); lods[i].renderers = renderers.ToArray();
            }
            group.SetLODs(lods); group.RecalculateBounds();
        }
    }
}
