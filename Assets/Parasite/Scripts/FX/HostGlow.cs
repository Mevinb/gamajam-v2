using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>
    /// CONTRACT — host possession/unconscious/captured/targeted visuals. Animates the human's unique URP Lit
    /// materials' emission, a faint aura, an unconscious residue, and an in-world target indicator.
    /// </summary>
    public class HostGlow : MonoBehaviour
    {
        const float UnconsciousSeconds = 8f;
        static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
        static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

        Material[] mats;
        bool possessed, captured, unconscious, targeted;
        float unconsciousT, targetFade, capturedT;
        Color applied = Color.black;

        ParticleSystem aura, residue;
        GameObject indicator;
        Transform ring, arcs, marker;
        Material ringMat;
        LineRenderer[] arcLines;

        public void Init(Material[] uniqueMaterials)
        {
            mats = uniqueMaterials;
            if (mats == null) return;
            foreach (var m in mats)
                if (m) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            Apply(Color.black, true);
        }

        public void SetPossessed(bool on)
        {
            possessed = on;
            var a = Aura();
            if (!a) return;
            if (on)
            {
                var m = a.main; m.startColor = FxParticles.TwoTone(new Color(0.35f, 1f, 0.4f, 0.55f), new Color(0.7f, 1f, 0.4f, 0.4f));
                if (!a.isPlaying) a.Play();
            }
            else if (!captured) a.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void SetCaptured(bool on)
        {
            captured = on;
            capturedT = 0f;
            var a = Aura();
            if (!a) return;
            if (on)
            {
                var m = a.main; m.startColor = FxParticles.TwoTone(new Color(1f, 0.2f, 0.15f, 0.7f), new Color(1f, 0.5f, 0.2f, 0.5f));
                if (!a.isPlaying) a.Play();
                a.Emit(14);
            }
            else if (!possessed) a.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void SetUnconscious(bool on)
        {
            unconscious = on;
            unconsciousT = on ? UnconsciousSeconds : 0f;
            var r = Residue();
            if (!r) return;
            if (on) r.Play(); else r.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void SetTargeted(bool on)
        {
            targeted = on;
            if (on) { BuildIndicator(); if (indicator) indicator.SetActive(true); }
        }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;

            if (unconscious && unconsciousT > 0f)
            {
                unconsciousT -= dt;
                if (unconsciousT <= 0f && residue) residue.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Color e = Color.black;
            if (captured)
            {
                capturedT += dt;
                e = Palette.Danger * (0.08f + 0.2f * Mathf.Abs(Mathf.Sin(t * 9f)) + 0.4f * Mathf.Max(0f, 1f - capturedT * 3f));
            }
            else if (possessed) e = Palette.Amoeba * (0.035f + 0.03f * Mathf.Max(0f, Mathf.Sin(t * 3.2f)));
            else if (unconscious && unconsciousT > 0f) e = Palette.Amoeba * (0.04f * (unconsciousT / UnconsciousSeconds) * (0.75f + 0.25f * Mathf.Sin(t * 2f)));

            targetFade = Mathf.MoveTowards(targetFade, targeted ? 1f : 0f, dt * 5f);
            if (targetFade > 0f) e += Palette.Amoeba * (0.06f + 0.04f * Mathf.Sin(t * 6f)) * targetFade;
            Apply(e, false);
            AnimateIndicator(t);
        }

        void Apply(Color c, bool force)
        {
            if (mats == null) return;
            if (!force && c == applied) return;
            applied = c;
            foreach (var m in mats) if (m) m.SetColor(IdEmission, c);
        }

        // ---------- particles ----------
        ParticleSystem Aura()
        {
            if (aura) return aura;
            if (!Application.isPlaying) return null;
            aura = FxParticles.Make("PossessionAura", transform, new Vector3(0f, 1.2f, 0f), FxAssets.Additive);
            var m = aura.main;
            m.loop = true; m.duration = 5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            m.gravityModifier = -0.05f;
            m.maxParticles = 40;
            var em = aura.emission; em.rateOverTime = 7f;
            var sh = aura.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.38f;
            var vel = aura.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            FxParticles.SizeCurve(aura, 0.4f, 1f);
            FxParticles.FadeOut(aura, 1f, 0.5f);
            SetLayer(aura.gameObject);
            return aura;
        }

        ParticleSystem Residue()
        {
            if (residue) return residue;
            if (!Application.isPlaying) return null;
            residue = FxParticles.Make("UnconsciousResidue", transform, new Vector3(0f, 0.25f, 0f), FxAssets.Additive);
            var m = residue.main;
            m.loop = true; m.duration = 5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            m.startColor = FxParticles.TwoTone(new Color(0.35f, 1f, 0.4f, 0.4f), new Color(0.6f, 1f, 0.5f, 0.25f));
            m.gravityModifier = -0.04f;
            m.maxParticles = 30;
            var em = residue.emission; em.rateOverTime = 5f;
            var sh = residue.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.65f;
            sh.scale = new Vector3(1f, 0.3f, 1f);
            FxParticles.SizeCurve(residue, 0.5f, 1f);
            FxParticles.FadeOut(residue, 1f, 0.4f);
            SetLayer(residue.gameObject);
            return residue;
        }

        // ---------- target indicator ----------
        void BuildIndicator()
        {
            if (indicator || !Application.isPlaying) return;
            indicator = new GameObject("TargetIndicator");
            indicator.transform.SetParent(transform, false);

            // soft glowing ground ring (textured additive quad)
            if (FxAssets.RingIsTextured)
            {
                ringMat = new Material(FxAssets.Ring);
                ringMat.SetFloat("_Cull", 0f);
                ring = FxPrim.Make(PrimitiveType.Quad, indicator.transform, new Vector3(0f, 0.04f, 0f), Vector3.one * 1.9f, ringMat, "Ring", Quaternion.Euler(90f, 0f, 0f)).transform;
            }

            // three bright rotating arc segments flat on the ground (always available)
            arcs = new GameObject("Arcs").transform;
            arcs.SetParent(indicator.transform, false);
            arcs.localPosition = new Vector3(0f, 0.06f, 0f);
            arcs.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lineMat = MatFactory.Unlit(new Color(0.45f, 1.6f, 0.45f));
            arcLines = new LineRenderer[3];
            for (int k = 0; k < 3; k++)
            {
                var go = new GameObject("Arc" + k);
                go.transform.SetParent(arcs, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.alignment = LineAlignment.TransformZ;
                lr.widthMultiplier = 0.07f;
                lr.numCapVertices = 2;
                lr.sharedMaterial = lineMat;
                lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
                const int seg = 14;
                lr.positionCount = seg + 1;
                float a0 = k * 120f + 10f, a1 = a0 + 80f;
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.75f);
                }
                arcLines[k] = lr;
            }

            // floating diamond marker above the head
            marker = new GameObject("Marker").transform;
            marker.SetParent(indicator.transform, false);
            marker.localPosition = new Vector3(0f, 2.25f, 0f);
            FxPrim.Make(PrimitiveType.Cube, marker, Vector3.zero, new Vector3(0.16f, 0.16f, 0.05f),
                MatFactory.Emissive(Palette.Amoeba, Palette.Amoeba * 2.5f), "Gem", Quaternion.Euler(0f, 0f, 45f));

            SetLayer(indicator);
            indicator.SetActive(false);
        }

        void AnimateIndicator(float t)
        {
            if (!indicator) return;
            if (targetFade <= 0f) { if (indicator.activeSelf) indicator.SetActive(false); return; }
            if (!indicator.activeSelf) indicator.SetActive(true);
            float ease = Mathf.SmoothStep(0f, 1f, targetFade);
            float breathe = 1f + 0.06f * Mathf.Sin(t * 5f);
            if (ring)
            {
                ring.localScale = Vector3.one * 1.9f * Mathf.Lerp(1.5f, 1f, ease) * breathe;
                ring.localRotation = Quaternion.Euler(90f, t * 35f, 0f);
                if (ringMat) ringMat.SetColor(IdBaseColor, new Color(0.45f, 1f, 0.45f, (0.7f + 0.25f * Mathf.Sin(t * 5f)) * ease));
            }
            if (arcs)
            {
                arcs.localRotation = Quaternion.Euler(90f, 0f, -t * 90f);
                arcs.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, ease) * breathe;
            }
            if (arcLines != null) foreach (var lr in arcLines) if (lr) lr.widthMultiplier = 0.07f * ease;
            if (marker)
            {
                marker.localPosition = new Vector3(0f, 2.25f + 0.08f * Mathf.Sin(t * 4f) + (1f - ease) * 0.4f, 0f);
                marker.localRotation = Quaternion.Euler(0f, t * 160f, 0f);
                marker.localScale = Vector3.one * ease;
            }
        }

        void SetLayer(GameObject root)
        {
            int layer = gameObject.layer;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
        }

        void OnDestroy() { if (ringMat) Destroy(ringMat); }
    }
}
