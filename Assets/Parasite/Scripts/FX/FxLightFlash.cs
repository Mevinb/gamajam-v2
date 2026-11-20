using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>Fades a light to zero then removes itself.</summary>
    internal class FxLightFlash : MonoBehaviour
    {
        Light l; float start, dur;
        public static void Spawn(Vector3 pos, Color c, float intensity, float range, float duration, Transform parent = null)
        {
            var go = new GameObject("FX_Flash");
            go.transform.position = pos;
            if (parent) go.transform.SetParent(parent, true);
            var f = go.AddComponent<FxLightFlash>();
            f.l = go.AddComponent<Light>();
            f.l.type = LightType.Point; f.l.color = c; f.l.range = range; f.l.intensity = intensity;
            f.l.shadows = LightShadows.None;
            f.start = intensity; f.dur = Mathf.Max(0.05f, duration);
        }
        void Update()
        {
            if (!l) { Destroy(gameObject); return; }
            l.intensity -= start * Time.deltaTime / dur;
            if (l.intensity <= 0f) Destroy(gameObject);
        }
    }
}
