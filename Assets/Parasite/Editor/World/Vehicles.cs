using System;
using Parasite.World;
using UnityEditor;
using UnityEngine;
using static Parasite.EditorTools.CityAssets;

namespace Parasite.EditorTools
{
    /// <summary>Original Blender vehicle visuals with the established parked footprints and deterministic placements.</summary>
    internal static class Vehicles
    {
        internal enum Body { Sedan, Hatch, Suv, Van, Taxi }

        public static Body PickBody(WorldKit k, string paint)
        {
            if (paint == "CarTaxi") return Body.Taxi;
            double r = k.Rng.NextDouble();
            return r < 0.42 ? Body.Sedan : r < 0.72 ? Body.Hatch : r < 0.94 ? Body.Suv : Body.Van;
        }

        static Material Paint(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(VehicleArtImporter.Root + "Surfaces/" + name + ".mat");
            if (!material) throw new InvalidOperationException("Authored vehicle paint missing: " + name + ". Prepare the Blender kit before generating scenes.");
            return material;
        }

        /// <summary>Parked geometry is a separate LOD hierarchy; only the existing body box contributes collision/navigation.</summary>
        public static void Car(WorldKit k, Vector3 p, float yaw, string paint, Body body)
        {
            if (k.Chance(0.5f)) yaw += 180f;
            var kind = body switch { Body.Hatch => VehicleBody.Hatch, Body.Suv => VehicleBody.Suv, Body.Van => VehicleBody.Van, _ => VehicleBody.Sedan };
            string paintName = body == Body.Taxi ? "CarTaxi" : body == Body.Van && paint == "CarRed" ? "CarWhite" : paint;
            var color = Paint(paintName);
            var instance = k.Instance(VehicleVisuals.Prefab(kind), "Authored road vehicles", p, yaw, 1,
                material => material && material.name == "VehiclePaint" ? color : material);
            if (body == Body.Taxi) VehicleVisuals.AttachTaxiSign(instance);
            // Exact pre-upgrade collider contract, including the original minimum hatch footprint and SUV/van height.
            var size = body switch
            {
                Body.Hatch => new Vector3(1.84f, 1.5f, 4.4f),
                Body.Suv => new Vector3(1.9f, 1.5f, 4.65f),
                Body.Van => new Vector3(1.98f, 1.5f, 5f),
                _ => new Vector3(1.84f, 1.5f, 4.6f)
            };
            var q = Quaternion.Euler(0, yaw, 0); var cs = q * size;
            k.Solid(p + Vector3.up * .75f, new Vector3(Mathf.Abs(cs.x), 1.5f, Mathf.Abs(cs.z)));
        }

        public static void Truck(WorldKit k, Vector3 p, float yaw, string paint, string label)
        {
            var color = Paint(paint);
            k.Instance(VehicleVisuals.Prefab(VehicleBody.BoxTruck), "Authored road vehicles", p, yaw, 1,
                material => material && material.name == "VehiclePaint" ? color : material);
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float l, float y, float w) => p + q * new Vector3(w, y, l);
            var sz = q * new Vector3(2.45f, 3.6f, 8.1f);
            k.Solid(P(0, 1.8f, 0), new Vector3(Mathf.Abs(sz.x), 3.6f, Mathf.Abs(sz.z)));
            if (label == null) return;
            foreach (float side in new[] { -1f, 1f })
            {
                var normal = q * Vector3.right * side;
                float textYaw = Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
                k.Text(label, P(-1, 2.4f, side * (2.45f * .5f + .02f)), textYaw, .6f, new Color(.15f,.2f,.3f), null, false);
            }
        }
    }
}
