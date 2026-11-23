using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.FX
{
    /// <summary>Stretches the flying blob along its direction of travel.</summary>
    internal class FxProjectileWobble : MonoBehaviour
    {
        public Transform body;
        public TrailRenderer trail, trail2;
        Vector3 dir = Vector3.forward;
        public ParticleSystem[] systems;
        Vector3 last; float stretch; bool init;
        void LateUpdate()
        {
            if (!init)
            {
                // start emitting only once the projectile is at its real start position (no streak from the origin)
                if (trail) { trail.Clear(); trail.emitting = true; }
                if (trail2) { trail2.Clear(); trail2.emitting = true; }
                if (systems != null) foreach (var ps in systems) if (ps) ps.Play();
            }
            if (!body) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var delta = transform.position - last;
            float speed = init ? delta.magnitude / dt : 0f;
            if (init && delta.sqrMagnitude > 1e-6f) dir = Vector3.Slerp(dir, delta.normalized, 1f - Mathf.Exp(-20f * dt));
            last = transform.position; init = true;
            body.rotation = Quaternion.LookRotation(dir, Vector3.up);
            stretch = Mathf.Lerp(stretch, Mathf.Clamp(speed * 0.035f, 0f, 0.6f), 1f - Mathf.Exp(-12f * dt));
            float wob = Mathf.Sin(Time.time * 22f) * 0.05f;
            float z = 1f + stretch + wob, xy = 1f / Mathf.Sqrt(z);
            body.localScale = new Vector3(xy, xy, z) * 0.38f;
        }
    }
}
