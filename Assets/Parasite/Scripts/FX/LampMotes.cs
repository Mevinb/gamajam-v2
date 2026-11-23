using UnityEngine;

namespace Parasite.FX
{
    /// <summary>Dust motes and the odd moth drifting in a street lamp's light (built at runtime, culled off-screen).</summary>
    public class LampMotes : MonoBehaviour
    {
        public Color color = new(1f, 0.78f, 0.5f, 0.7f);
        public float radius = 1.3f;

        void Awake()
        {
            var ps = FxParticles.Make("Motes", transform, Vector3.down * 0.9f, FxAssets.Additive);
            var m = ps.main;
            m.loop = true; m.duration = 6f; m.prewarm = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.034f);
            m.startColor = FxParticles.TwoTone(color, new Color(color.r, color.g * 0.9f, color.b * 0.8f, color.a * 0.5f));
            m.maxParticles = 36;
            m.cullingMode = ParticleSystemCullingMode.Pause;
            var em = ps.emission; em.rateOverTime = 6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            var nz = ps.noise; nz.enabled = true; nz.strength = 0.18f; nz.frequency = 0.45f; nz.scrollSpeed = 0.2f; nz.quality = ParticleSystemNoiseQuality.Low;
            FxParticles.FadeOut(ps, 1f, 0.7f);
            ps.Play();

            // two moths: bigger, quicker, circling the lamp head
            var moths = FxParticles.Make("Moths", transform, Vector3.down * 0.25f, FxAssets.Additive);
            var mm = moths.main;
            mm.loop = true; mm.duration = 5f; mm.prewarm = true;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            mm.startSpeed = 0.3f;
            mm.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.05f);
            mm.startColor = new Color(1f, 0.85f, 0.6f, 0.55f);
            mm.maxParticles = 3;
            mm.cullingMode = ParticleSystemCullingMode.Pause;
            var me = moths.emission; me.rateOverTime = 0.8f;
            var ms = moths.shape; ms.shapeType = ParticleSystemShapeType.Sphere; ms.radius = 0.35f;
            var mn = moths.noise; mn.enabled = true; mn.strength = 1.6f; mn.frequency = 1.6f; mn.scrollSpeed = 1.2f; mn.quality = ParticleSystemNoiseQuality.Low;
            FxParticles.FadeOut(moths, 1f, 0.8f);
            moths.Play();
        }
    }
}
