using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// CONTRACT — the visible body of a free amoeba. Built procedurally under the owner transform.
    /// Placeholder implementation; the FX pass replaces internals (shader, nucleus, squash/stretch, light).
    /// </summary>
    public class AmoebaVisual : MonoBehaviour
    {
        public float radius = 0.38f;
        Transform body;
        Vector3 vel;

        public void Build(float r)
        {
            radius = r;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "AmoebaBody";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, r, 0);
            go.transform.localScale = Vector3.one * r * 2f;
            go.GetComponent<Renderer>().sharedMaterial = MatFactory.Emissive(new Color(0.3f, 0.9f, 0.3f), Palette.Amoeba * 0.6f);
            body = go.transform;
        }

        /// <summary>World velocity + grounded, every frame.</summary>
        public void SetMotion(Vector3 velocity, bool grounded) { vel = velocity; }

        /// <summary>Whether this amoeba is the one the player controls.</summary>
        public void SetActive(bool active) { }

        /// <summary>Short squash used for hops / landing / spawn.</summary>
        public void Squash(float amount = 1f) { }

        void LateUpdate()
        {
            if (!body) return;
            float s = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
            float stretch = Mathf.Clamp(new Vector3(vel.x, 0, vel.z).magnitude * 0.04f, 0, 0.25f);
            body.localScale = new Vector3(radius * 2f * (s + stretch * 0.5f), radius * 2f * (s - stretch), radius * 2f * (s + stretch * 0.5f));
        }
    }

    /// <summary>CONTRACT — host possession/unconscious/captured visuals. Placeholder (emission tint only).</summary>
    public class HostGlow : MonoBehaviour
    {
        Material[] mats;
        bool possessed, captured;

        public void Init(Material[] uniqueMaterials) { mats = uniqueMaterials; }
        public void SetPossessed(bool on) { possessed = on; Refresh(); }
        public void SetCaptured(bool on) { captured = on; Refresh(); }
        public void SetUnconscious(bool on) { Refresh(); }
        public void SetTargeted(bool on) { }

        void Refresh()
        {
            if (mats == null) return;
            var c = captured ? new Color(0.6f, 0.05f, 0.05f) : possessed ? Palette.Amoeba * 0.35f : Color.black;
            foreach (var m in mats) MatFactory.SetEmission(m, c);
        }
    }
}
