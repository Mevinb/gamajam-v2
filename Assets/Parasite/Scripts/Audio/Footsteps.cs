using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>
    /// Surface-aware footsteps for every host near the listener (possessed or AI) and wet squelches for free amoebas.
    /// Surfaces come from the acoustic zone and height (asphalt road, concrete sidewalk, plaza cobble, lab tile, metal,
    /// grass planters, rooftop gravel); outdoors a wet splash layer is added because the city is always rainy.
    /// Profession flavour: guards/maintenance wear heavy boots, scientists soft shoes, some civilians heels.
    /// Stride cadence follows speed, so steps line up with the walk/run animation speeds.
    /// </summary>
    public sealed class Footsteps : MonoBehaviour
    {
        public static Footsteps Instance { get; private set; }
        /// <summary>True once footstep clips exist; the legacy SfxId.Footstep call is then ignored.</summary>
        public static bool Active => Instance && Instance.isActiveAndEnabled && Instance.ready;

        const float HearRange = 26f;
        sealed class Track { public float dist, airT; public bool grounded = true; public int flavour; }
        readonly Dictionary<Object, Track> tracks = new();
        readonly List<Object> dead = new();
        bool ready;
        float cleanT;

        void Awake()
        {
            Instance = this;
            ready = SfxBackend.Variants("Sfx/fs_concrete") != null;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static string SurfaceAt(Vector3 p, float groundY)
        {
            var zone = AudioBus.ZoneAt(p + Vector3.up * 0.3f);
            if (groundY > 3.5f) return "gravel";                     // rooftops
            switch (zone)
            {
                case AudioZone.Lab: return p.x > 16f ? "metal" : "tile";   // service room grating
                case AudioZone.Tunnel: return "metal";
            }
            if (groundY > 0.45f)
            {
                // forecourt planters (x ±6..14, z 62..64) are grass; anything else raised outdoors is a vehicle/prop
                if (p.z > 61.5f && p.z < 64.5f && Mathf.Abs(p.x) > 5.5f && Mathf.Abs(p.x) < 14.5f) return "grass";
                return "metal";
            }
            if (groundY < 0.08f) return "asphalt";
            if (zone == AudioZone.Plaza) return "cobble";
            return "concrete";
        }

        static bool Raining => RainSystem.Instance && RainSystem.Instance.isActiveAndEnabled;

        void Update()
        {
            if (!ready) return;
            var gm = GameManager.Instance;
            float dt = Time.deltaTime;
            if (dt <= 0f || (gm && !gm.IsPlaying)) return;
            Vector3 L = AudioBus.ListenerPosition;
            foreach (var h in HostCharacter.All)
            {
                if (!h || !h.isActiveAndEnabled) continue;
                if (h.State == HostState.Unconscious || h.State == HostState.Captured) continue;
                if (!h.IsPossessed && (h.transform.position - L).sqrMagnitude > HearRange * HearRange) continue;
                Tick(h, h.transform.position, h.Velocity, h.Grounded, dt, false, h);
            }
            var pm = PossessionManager.Instance;
            if (pm)
                foreach (var a in pm.Agents)
                {
                    if (a == null || !a.Blob || !a.Blob.isActiveAndEnabled || !a.IsFree) continue;
                    var cc = a.Blob.Controller;
                    bool grounded = cc && cc.enabled && cc.isGrounded;
                    Tick(a.Blob, a.Blob.transform.position, a.Blob.Velocity, grounded, dt, true, null);
                }
            cleanT -= dt;
            if (cleanT <= 0f)
            {
                cleanT = 5f; dead.Clear();
                foreach (var k in tracks.Keys) if (!k) dead.Add(k);
                foreach (var k in dead) tracks.Remove(k);
            }
        }

        void Tick(Object key, Vector3 pos, Vector3 vel, bool grounded, float dt, bool blob, HostCharacter host)
        {
            if (!tracks.TryGetValue(key, out var t))
            {
                t = new Track { flavour = host ? Flavour(host) : 0 };
                tracks[key] = t;
            }
            float speed = new Vector2(vel.x, vel.z).magnitude;
            if (!grounded) { t.airT += dt; t.grounded = false; return; }
            if (!t.grounded)
            {
                if (t.airT > 0.22f) Land(pos, blob, host, t);
                t.grounded = true; t.airT = 0f;
                t.dist = 0f;
                return;
            }
            float stride = blob ? 0.55f : Mathf.Lerp(0.7f, 1.15f, Mathf.InverseLerp(1.4f, 5.5f, speed));
            if (speed < 0.35f) { t.dist = Mathf.Min(t.dist, stride * 0.55f); return; }
            t.dist += speed * dt;
            if (t.dist < stride) return;
            t.dist -= stride;
            if (blob) Squish(pos, speed);
            else Step(pos, speed, host, t, 1f);
        }

        static int Flavour(HostCharacter h)
        {
            switch (h.Profession)
            {
                case Profession.Guard: return 1;        // boots
                case Profession.Maintenance: return 2;  // work boots
                case Profession.Scientist: return 3;    // soft shoes
            }
            int hash = 0;
            foreach (char c in h.DisplayName ?? "") hash = hash * 31 + c;
            return (hash & 3) == 0 ? 4 : 5;            // 4 heels, 5 sneakers
        }

        static void Step(Vector3 pos, float speed, HostCharacter host, Track t, float gain)
        {
            float groundY = pos.y;
            string surface = SurfaceAt(pos, groundY);
            var set = SfxBackend.Variants("Sfx/fs_" + surface) ?? SfxBackend.Variants("Sfx/fs_concrete");
            if (set == null) return;
            bool possessed = host && host.IsPossessed;
            float run = Mathf.InverseLerp(1.5f, 5.5f, speed);
            float vol = Mathf.Lerp(0.42f, 0.9f, run) * (possessed ? 1f : 0.7f) * gain * Sfx.Volume;
            float pitch = Random.Range(0.94f, 1.06f);
            switch (t.flavour)
            {
                case 1: pitch *= 0.88f; vol *= 1.1f; break;
                case 2: pitch *= 0.9f; vol *= 1.05f; break;
                case 3: pitch *= 1.05f; vol *= 0.75f; break;
                case 5: pitch *= 1.07f; vol *= 0.8f; break;
            }
            int prio = possessed ? 110 : 175;
            Vector3 at = pos + Vector3.up * 0.05f;
            SfxBackend.PlayClip(SfxBackend.Pick(set, surface), at, vol, pitch, SfxPool.TagFootstep, prio, 1f, 20f, 8);
            bool hard = surface != "grass" && surface != "gravel";
            if (hard && (t.flavour == 1 || t.flavour == 2))
                SfxBackend.PlayClip(SfxBackend.Pick(SfxBackend.Variants("Sfx/fs_boot"), "boot"), at, vol * 0.45f, pitch, SfxPool.TagFootstep, prio + 5, 1f, 18f, 8);
            else if (hard && t.flavour == 4)
                SfxBackend.PlayClip(SfxBackend.Pick(SfxBackend.Variants("Sfx/fs_heel"), "heel"), at, vol * 0.5f, pitch * 1.02f, SfxPool.TagFootstep, prio + 5, 1f, 20f, 8);
            if (Raining && !AudioBus.Indoors(AudioBus.ZoneAt(pos + Vector3.up * 0.3f)) && pos.y < 0.5f)
                SfxBackend.PlayClip(SfxBackend.Pick(SfxBackend.Variants("Sfx/fs_splash"), "splash"), at, vol * (surface == "asphalt" ? 0.55f : 0.35f),
                    Random.Range(0.92f, 1.1f), SfxPool.TagFootstep, prio + 10, 1f, 16f, 8);
        }

        static void Land(Vector3 pos, bool blob, HostCharacter host, Track t)
        {
            if (blob) { Sfx.Play(SfxId.BlobLand, pos, 0.8f); return; }
            Step(pos, 5f, host, t, 1.25f);
            Sfx.Play(SfxId.Land, pos, host && host.IsPossessed ? 0.7f : 0.45f);
        }

        static void Squish(Vector3 pos, float speed)
        {
            float vol = Mathf.Lerp(0.35f, 0.7f, Mathf.InverseLerp(1f, 6f, speed)) * Sfx.Volume;
            var set = SfxBackend.Variants("Sfx/blob_squish");
            if (set == null) return;
            SfxBackend.PlayClip(SfxBackend.Pick(set, "squish"), pos, vol, Random.Range(0.9f, 1.15f), SfxPool.TagSquish, 120, 1f, 16f, 3);
        }
    }
}
