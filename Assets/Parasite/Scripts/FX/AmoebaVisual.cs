using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>
    /// CONTRACT — the visible body of a free amoeba, built procedurally under the owner (owner pivot = feet).
    /// Translucent jelly shell ("Parasite/Amoeba"), pulsing nucleus, orbiting organelles, a small green point
    /// light and drip particles. Spring-damped squash &amp; stretch (velocity stretch + Squash impulses).
    /// </summary>
    public class AmoebaVisual : MonoBehaviour
    {
        public float radius = 0.38f;

        Transform pivot, body, nucleus;
        Transform[] organelles;
        Vector3[] orbitAxes, orbitStart;
        float[] orbitSpeed;
        Material shellMat, nucleusMat, organelleMat;
        bool shellIsShader, nucleusIsShader;
        Light glow;
        ParticleSystem drips;

        Vector3 vel, faceDir = Vector3.forward, lastPos;
        float dripAccum;
        bool grounded = true, active = true;
        float squash, squashVel, stretch, airStretch, activity = 1f, infect, phase, pulsePhase, wobbleTime, orbitTime;

        static readonly int IdActivity = Shader.PropertyToID("_Activity");
        static readonly int IdInfect = Shader.PropertyToID("_InfectPulse");
        static readonly int IdPulseRate = Shader.PropertyToID("_PulseRate");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int IdPhase = Shader.PropertyToID("_Phase");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdPulsePhase = Shader.PropertyToID("_PulsePhase");
        static readonly int IdWobbleTime = Shader.PropertyToID("_WobbleTime");
        static readonly int IdWobbleSpeed = Shader.PropertyToID("_WobbleSpeed");

        public void Build(float r)
        {
            radius = r;
            if (pivot) Destroy(pivot.gameObject);
            if (drips) Destroy(drips.gameObject);
            DestroyMats();
            phase = Random.value * 10f;

            pivot = new GameObject("AmoebaPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localScale = Vector3.one * r * 2f;

            shellMat = FxAssets.NewShell(out shellIsShader);
            if (shellIsShader) { shellMat.SetFloat(IdPulseRate, 0f); shellMat.SetFloat(IdWobbleSpeed, 0f); }
            body = FxPrim.Make(PrimitiveType.Sphere, pivot, new Vector3(0f, 0.5f, 0f), Vector3.one, shellMat, "AmoebaBody", null, true).transform;

            nucleusMat = FxAssets.NewNucleus(out nucleusIsShader);
            if (nucleusIsShader) { nucleusMat.SetFloat(IdPulseRate, 0f); nucleusMat.SetFloat(IdPhase, phase); }
            nucleus = FxPrim.Make(PrimitiveType.Sphere, body, new Vector3(0f, 0.04f, 0.03f), Vector3.one * 0.4f, nucleusMat, "Nucleus").transform;

            organelleMat = FxAssets.NewNucleus(out _);
            if (nucleusIsShader)
            {
                organelleMat.SetColor(IdColor, new Color(0.95f, 1.6f, 0.35f, 1f));
                organelleMat.SetFloat(IdIntensity, 1.2f);
                organelleMat.SetFloat(IdPulseRate, 5f);
                organelleMat.SetFloat(IdPhase, phase + 1.7f);
            }
            int count = 3;
            organelles = new Transform[count];
            orbitAxes = new Vector3[count];
            orbitStart = new Vector3[count];
            orbitSpeed = new float[count];
            for (int i = 0; i < count; i++)
            {
                float s = Random.Range(0.08f, 0.13f);
                organelles[i] = FxPrim.Make(PrimitiveType.Sphere, body, Vector3.zero, Vector3.one * s, organelleMat, "Organelle" + i).transform;
                orbitAxes[i] = Random.onUnitSphere;
                orbitStart[i] = Vector3.Cross(orbitAxes[i], Random.onUnitSphere).normalized * Random.Range(0.22f, 0.3f);
                orbitSpeed[i] = Random.Range(40f, 110f) * (Random.value < 0.5f ? -1f : 1f);
            }

            glow = new GameObject("AmoebaLight").AddComponent<Light>();
            glow.transform.SetParent(body, false);
            glow.type = LightType.Point;
            glow.color = Palette.Amoeba;
            glow.range = 3f;
            glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.Auto;

            drips = FxParticles.Make("Drips", transform, new Vector3(0f, r * 0.25f, 0f), FxAssets.Alpha);
            var m = drips.main;
            m.loop = true; m.duration = 5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.25f);
            m.startSize = new ParticleSystem.MinMaxCurve(r * 0.1f, r * 0.22f);
            m.startColor = FxParticles.TwoTone(new Color(0.35f, 1f, 0.4f, 0.85f), new Color(0.1f, 0.6f, 0.2f, 0.8f));
            m.gravityModifier = 0.9f;
            m.maxParticles = 60;
            var em = drips.emission; em.rateOverTime = 0.5f; // movement drips are emitted manually (teleport-safe)
            var sh = drips.shape; sh.radius = r * 0.6f;
            FxParticles.SizeCurve(drips, 1f, 0.2f);
            FxParticles.FadeOut(drips, 1f, 0.6f);
            drips.Play();

            ApplyLayer();
            lastPos = transform.position;
            infect = 1f;
            Squash(0.8f);
        }

        void Start() => ApplyLayer();

        void ApplyLayer()
        {
            int layer = gameObject.layer;
            if (pivot) foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            if (drips) drips.gameObject.layer = layer;
        }

        /// <summary>World velocity + grounded, every frame.</summary>
        public void SetMotion(Vector3 velocity, bool grounded) { vel = velocity; this.grounded = grounded; }

        /// <summary>Whether this amoeba is the one the player controls.</summary>
        public void SetActive(bool active)
        {
            if (active && !this.active) infect = Mathf.Max(infect, 0.7f);
            this.active = active;
        }

        /// <summary>Short squash used for hops / landing / spawn.</summary>
        public void Squash(float amount = 1f) => squashVel -= Mathf.Clamp(amount, 0f, 2f) * 4.5f;

        void OnEnable()
        {
            // re-appearing after a possession: pop back with a glow
            lastPos = transform.position;
            if (pivot) { infect = Mathf.Max(infect, 0.8f); squash = -0.25f; squashVel = 0f; }
        }

        void LateUpdate()
        {
            if (!pivot) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float t = Time.time + phase;

            // drips while sliding along (skip teleports)
            var p = transform.position;
            float moved = (p - lastPos).magnitude;
            lastPos = p;
            if (drips && moved < 1f)
            {
                dripAccum += moved * (grounded ? 5f : 2f);
                int n = (int)dripAccum;
                if (n > 0) { dripAccum -= n; drips.Emit(n); }
            }

            // spring-damped squash (negative = flattened)
            squashVel += (-170f * squash - 11f * squashVel) * dt;
            squash = Mathf.Clamp(squash + squashVel * dt, -0.55f, 0.6f);

            var h = new Vector3(vel.x, 0f, vel.z);
            float hs = h.magnitude;
            if (hs > 0.2f) faceDir = Vector3.Slerp(faceDir, h / hs, 1f - Mathf.Exp(-10f * dt));
            stretch = Mathf.Lerp(stretch, Mathf.Clamp(hs * 0.05f, 0f, 0.3f), 1f - Mathf.Exp(-8f * dt));
            airStretch = Mathf.Lerp(airStretch, grounded ? 0f : Mathf.Clamp(Mathf.Abs(vel.y) * 0.035f, 0f, 0.3f), 1f - Mathf.Exp(-10f * dt));

            activity = Mathf.MoveTowards(activity, active ? 1f : 0f, dt * 2f);
            infect = Mathf.MoveTowards(infect, 0f, dt * 1.1f);

            // phases are accumulated so speed changes (active <-> inactive) stay smooth
            float pulseRate = Mathf.Lerp(1.6f, 3.4f, activity);
            pulsePhase += dt * pulseRate;
            orbitTime += dt * Mathf.Lerp(0.5f, 1f, activity);
            wobbleTime += dt * Mathf.Lerp(1.1f, 2.2f, activity) * (1f + infect);
            float breathe = Mathf.Sin(pulsePhase * 0.75f + phase) * 0.03f;
            // volume-preserving: vertical squash q, then stretch along travel direction (local z)
            float q = Mathf.Max(0.4f, (1f + squash + breathe) * (1f + airStretch));
            float xz = 1f / Mathf.Sqrt(q), st = 1f + stretch, inv = 1f / Mathf.Sqrt(st);
            pivot.localScale = new Vector3(xz * inv, q * inv, xz * st) * radius * 2f;
            pivot.rotation = Quaternion.LookRotation(faceDir, Vector3.up) * Quaternion.Euler(stretch * 18f, 0f, 0f);

            // nucleus drifts and lags behind motion
            nucleus.localPosition = new Vector3(Mathf.Sin(t * 1.1f) * 0.03f, 0.04f + Mathf.Sin(t * 1.7f) * 0.03f, 0.03f - stretch * 0.25f);
            float np = 1f + Mathf.Sin(pulsePhase + phase) * Mathf.Lerp(0.04f, 0.09f, activity) + infect * 0.15f;
            nucleus.localScale = Vector3.one * 0.4f * np;
            for (int i = 0; i < organelles.Length; i++)
                organelles[i].localPosition = Quaternion.AngleAxis(orbitTime * orbitSpeed[i], orbitAxes[i]) * orbitStart[i];

            float pulse01 = 0.5f + 0.5f * Mathf.Sin(pulsePhase + phase);
            if (shellIsShader)
            {
                shellMat.SetFloat(IdActivity, activity);
                shellMat.SetFloat(IdInfect, infect);
                shellMat.SetFloat(IdPulsePhase, pulsePhase + phase);
                shellMat.SetFloat(IdWobbleTime, wobbleTime + phase);
            }
            else shellMat.SetColor(IdEmissionColor, Palette.Amoeba * (0.15f + 0.35f * activity * (0.7f + 0.3f * pulse01) + infect * 0.6f));

            if (nucleusIsShader)
            {
                nucleusMat.SetFloat(IdIntensity, Mathf.Lerp(0.6f, 1.8f, activity) + infect * 2f);
                nucleusMat.SetFloat(IdPhase, pulsePhase + phase);
            }
            else nucleusMat.SetColor(IdEmissionColor, Palette.Amoeba * (Mathf.Lerp(0.6f, 2f, activity) * (0.8f + 0.2f * pulse01) + infect * 2f));

            if (glow) glow.intensity = Mathf.Lerp(0.35f, 1.2f, activity) * (0.9f + 0.15f * pulse01) + infect * 1.5f;
        }

        void DestroyMats()
        {
            if (shellMat) Destroy(shellMat);
            if (nucleusMat) Destroy(nucleusMat);
            if (organelleMat) Destroy(organelleMat);
        }

        void OnDestroy() => DestroyMats();
    }

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
                e = Palette.Danger * (0.3f + 0.6f * Mathf.Abs(Mathf.Sin(t * 9f)) + Mathf.Max(0f, 1f - capturedT * 3f));
            }
            else if (possessed) e = Palette.Amoeba * (0.32f + 0.08f * Mathf.Sin(t * 3.2f));
            else if (unconscious && unconsciousT > 0f) e = Palette.Amoeba * (0.16f * (unconsciousT / UnconsciousSeconds) * (0.75f + 0.25f * Mathf.Sin(t * 2f)));

            targetFade = Mathf.MoveTowards(targetFade, targeted ? 1f : 0f, dt * 5f);
            if (targetFade > 0f) e += Palette.Amoeba * (0.2f + 0.12f * Mathf.Sin(t * 6f)) * targetFade;
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
