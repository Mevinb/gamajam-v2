using System.Collections;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>Placeholder backend (simple primitives). The FX pass replaces this file.</summary>
    internal static class SfxBackend
    {
        public static void Play(SfxId id, Vector3 position, float volume, bool ui) { }
        public static void StartAmbience() { }
    }

    internal static class FxBackend
    {
        public static Transform CreateProjectile()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "AmoebaProjectile";
            go.transform.localScale = Vector3.one * 0.45f;
            go.GetComponent<Renderer>().sharedMaterial = MatFactory.Emissive(Palette.Amoeba, Palette.Amoeba * 2f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.35f; tr.startWidth = 0.35f; tr.endWidth = 0f;
            tr.sharedMaterial = MatFactory.Unlit(Palette.Amoeba);
            return go.transform;
        }

        public static void DestroyProjectile(Transform t) { if (t) Object.Destroy(t.gameObject, 0.3f); }
        public static void Splat(Vector3 position) { }
        public static void SplitBurst(Vector3 position) { }
        public static void ScreenPulse(Color color, float strength, float duration) { }
        public static void SetDanger(float t01) { }
    }
}
