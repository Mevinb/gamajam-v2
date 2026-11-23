using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>
    /// CONTRACT — the visible body of a free amoeba, built procedurally under the owner (owner pivot = feet).
    /// Translucent jelly shell ("Parasite/Amoeba"), pulsing nucleus, orbiting organelles with bioluminescent
    /// synaptic energy pulses, enhanced point light emission, sliding ground luminescence wake, and spring-damped
    /// squash &amp; stretch (velocity stretch + Squash impulses).
    /// </summary>
    public class AmoebaVisual : MonoBehaviour
    {
        public float radius = 0.38f;

        Transform pivot, body, nucleus;
        Transform[] organelles;
        LineRenderer[] tendrils;
        Vector3[] orbitAxes, orbitStart;
        float[] orbitSpeed;
        Material shellMat, nucleusMat, organelleMat;
        bool shellIsShader, nucleusIsShader;
        Light glow;
        ParticleSystem drips;
        ParticleSystem wake;

        Vector3 vel, faceDir = Vector3.forward, lastPos;
        float dripAccum, wakeAccum;
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
            if (wake) Destroy(wake.gameObject);
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
                organelleMat.SetFloat(IdIntensity, 1.4f);
                organelleMat.SetFloat(IdPulseRate, 5f);
                organelleMat.SetFloat(IdPhase, phase + 1.7f);
            }
            int count = 3;
            organelles = new Transform[count];
            orbitAxes = new Vector3[count];
            orbitStart = new Vector3[count];
            orbitSpeed = new float[count];
            tendrils = new LineRenderer[count];

            for (int i = 0; i < count; i++)
            {
                float s = Random.Range(0.09f, 0.14f);
                organelles[i] = FxPrim.Make(PrimitiveType.Sphere, body, Vector3.zero, Vector3.one * s, organelleMat, "Organelle" + i).transform;
                orbitAxes[i] = Random.onUnitSphere;
                orbitStart[i] = Vector3.Cross(orbitAxes[i], Random.onUnitSphere).normalized * Random.Range(0.22f, 0.3f);
                orbitSpeed[i] = Random.Range(40f, 110f) * (Random.value < 0.5f ? -1f : 1f);

                // Bioluminescent synaptic energy pulses connecting nucleus to organelles
                var tgo = new GameObject("Tendril" + i);
                tgo.transform.SetParent(body, false);
                var lr = tgo.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 3;
                lr.startWidth = r * 0.045f;
                lr.endWidth = r * 0.02f;
                lr.sharedMaterial = FxAssets.Additive;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.lightProbeUsage = LightProbeUsage.Off;
                lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                tendrils[i] = lr;
            }

            // Enhanced bioluminescent glow light
            glow = new GameObject("AmoebaLight").AddComponent<Light>();
            glow.transform.SetParent(body, false);
            glow.type = LightType.Point;
            glow.color = new Color(0.28f, 1.0f, 0.42f);
            glow.range = 4.2f;
            glow.intensity = 1.6f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.Auto;

            // Drips particle system
            drips = FxParticles.Make("Drips", transform, new Vector3(0f, r * 0.25f, 0f), FxAssets.Alpha);
            var m = drips.main;
            m.loop = true; m.duration = 5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.25f);
            m.startSize = new ParticleSystem.MinMaxCurve(r * 0.1f, r * 0.22f);
            m.startColor = FxParticles.TwoTone(new Color(0.35f, 1f, 0.4f, 0.85f), new Color(0.1f, 0.6f, 0.2f, 0.8f));
            m.gravityModifier = 0.9f;
            m.maxParticles = 60;
            var em = drips.emission; em.rateOverTime = 0.5f; // movement drips emitted manually
            var sh = drips.shape; sh.radius = r * 0.6f;
            FxParticles.SizeCurve(drips, 1f, 0.2f);
            FxParticles.FadeOut(drips, 1f, 0.6f);
            drips.Play();

            // Bioluminescent ground wake / luminescence trail while sliding
            wake = FxParticles.Make("LumWake", transform, new Vector3(0f, 0.03f, 0f), FxAssets.Additive);
            var wm = wake.main;
            wm.loop = true;
            wm.duration = 4f;
            wm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            wm.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
            wm.startSize = new ParticleSystem.MinMaxCurve(r * 0.4f, r * 0.75f);
            wm.startColor = FxParticles.TwoTone(new Color(0.25f, 1.0f, 0.5f, 0.4f), new Color(0.12f, 0.75f, 0.85f, 0.25f));
            wm.gravityModifier = 0f;
            wm.maxParticles = 80;
            var wem = wake.emission;
            wem.rateOverTime = 0f; // emitted manually while moving on ground
            var wsh = wake.shape;
            wsh.shapeType = ParticleSystemShapeType.Circle;
            wsh.radius = r * 0.45f;
            wsh.rotation = new Vector3(90f, 0f, 0f);
            FxParticles.SizeCurve(wake, 1.0f, 0.15f);
            FxParticles.FadeOut(wake, 0.45f, 0.3f);
            wake.Play();

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
            if (wake) wake.gameObject.layer = layer;
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
        public void Squash(float amount = 1f) => squashVel -= Mathf.Clamp(amount, 0f, 2f) * 4.2f;

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

            var p = transform.position;
            float moved = (p - lastPos).magnitude;
            lastPos = p;

            var h = new Vector3(vel.x, 0f, vel.z);
            float hs = h.magnitude;

            // drips while sliding along (skip teleports)
            if (drips && moved < 1f)
            {
                dripAccum += moved * (grounded ? 5f : 2f);
                int n = (int)dripAccum;
                if (n > 0) { dripAccum -= n; drips.Emit(n); }
            }

            // Bioluminescent wake emitted when sliding along the ground
            if (wake && moved < 1f && grounded && hs > 0.2f)
            {
                wakeAccum += moved * 10f;
                int nw = (int)wakeAccum;
                if (nw > 0) { wakeAccum -= nw; wake.Emit(nw); }
            }

            // Smooth spring-damped squash response (underdamped viscoelastic jelly)
            squashVel += (-135f * squash - 13.5f * squashVel) * dt;
            squash = Mathf.Clamp(squash + squashVel * dt, -0.55f, 0.6f);

            if (hs > 0.15f) faceDir = Vector3.Slerp(faceDir, h / hs, 1f - Mathf.Exp(-11f * dt));
            stretch = Mathf.Lerp(stretch, Mathf.Clamp(hs * 0.052f, 0f, 0.32f), 1f - Mathf.Exp(-9f * dt));
            airStretch = Mathf.Lerp(airStretch, grounded ? 0f : Mathf.Clamp(Mathf.Abs(vel.y) * 0.038f, 0f, 0.32f), 1f - Mathf.Exp(-11f * dt));

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

            Vector3 nPos = nucleus.localPosition;
            float pulse01 = 0.5f + 0.5f * Mathf.Sin(pulsePhase + phase);

            for (int i = 0; i < organelles.Length; i++)
            {
                Vector3 oPos = Quaternion.AngleAxis(orbitTime * orbitSpeed[i], orbitAxes[i]) * orbitStart[i];
                organelles[i].localPosition = oPos;

                // Update energy tendril from nucleus to organelle
                if (tendrils != null && i < tendrils.Length && tendrils[i])
                {
                    float energyPulse = Mathf.Sin(pulsePhase * 2.8f + i * 2.1f) * 0.5f + 0.5f;
                    Vector3 mid = Vector3.Lerp(nPos, oPos, 0.5f) + Vector3.Cross(orbitAxes[i], (oPos - nPos).normalized) * (0.02f * Mathf.Sin(t * 5f + i));
                    tendrils[i].SetPosition(0, nPos);
                    tendrils[i].SetPosition(1, mid);
                    tendrils[i].SetPosition(2, oPos);

                    Color tendrilCol = Color.Lerp(
                        new Color(0.25f, 0.85f, 0.35f, 0.35f),
                        new Color(0.55f, 1.8f, 0.75f, 0.85f),
                        energyPulse * activity + infect * 0.4f
                    );
                    tendrils[i].startColor = tendrilCol;
                    tendrils[i].endColor = tendrilCol * 0.7f;
                    tendrils[i].startWidth = radius * (0.03f + 0.025f * energyPulse * activity + infect * 0.03f);
                    tendrils[i].endWidth = radius * 0.018f;
                }
            }

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
                nucleusMat.SetFloat(IdIntensity, Mathf.Lerp(0.8f, 2.2f, activity) + infect * 2.2f);
                nucleusMat.SetFloat(IdPhase, pulsePhase + phase);
            }
            else nucleusMat.SetColor(IdEmissionColor, Palette.Amoeba * (Mathf.Lerp(0.8f, 2.2f, activity) * (0.8f + 0.2f * pulse01) + infect * 2.2f));

            // Enhanced light emission response
            if (glow)
            {
                glow.intensity = Mathf.Lerp(0.55f, 1.85f, activity) * (0.9f + 0.2f * pulse01) + infect * 2.2f;
                glow.range = Mathf.Lerp(3.4f, 4.5f, activity) + infect * 1.5f;
            }
        }

        void DestroyMats()
        {
            if (shellMat) Destroy(shellMat);
            if (nucleusMat) Destroy(nucleusMat);
            if (organelleMat) Destroy(organelleMat);
        }

        void OnDestroy() => DestroyMats();
    }
}
