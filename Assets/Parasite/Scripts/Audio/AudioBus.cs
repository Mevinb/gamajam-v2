using Parasite.FX;
using Parasite.World;
using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>Acoustic zones of the generated city (used for beds, reverb, footsteps, music).</summary>
    public enum AudioZone { Street, Plaza, Alley, Lab, Tunnel }

    /// <summary>
    /// Code-level mixer: bus gains (from <see cref="Sfx"/> volumes set by AudioPreferences), voice ducking,
    /// the pause muffle amount and zone lookup. Static so every audio component reads one consistent state.
    /// </summary>
    public static class AudioBus
    {
        /// <summary>Base level of the −16 LUFS music stems under SFX and dialogue.</summary>
        public const float MusicBase = 0.5f;
        /// <summary>Music multiplier while dialogue speaks (≈ −8 dB), ambience multiplier (≈ −4 dB).</summary>
        public const float MusicDuckDepth = 0.4f, AmbienceDuckDepth = 0.65f;

        /// <summary>0..1 smoothed dialogue activity (set by VoiceOver).</summary>
        public static float VoiceActivity;
        /// <summary>0..1 pause-menu muffle amount (set by ListenerFx).</summary>
        public static float Muffle;

        public static float MusicGain => Sfx.MusicVolume * MusicBase * Mathf.Lerp(1f, MusicDuckDepth, VoiceActivity);
        public static float AmbienceGain => Sfx.AmbienceVolume * Mathf.Lerp(1f, AmbienceDuckDepth, VoiceActivity);
        public static float VoiceGain => Sfx.VoiceVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { VoiceActivity = 0f; Muffle = 0f; }

        static readonly Bounds Tunnel = new(new Vector3(38.85f, 1.6f, 90f), new Vector3(30.4f, 3.6f, 3.4f));
        static readonly Bounds TunnelDoorHouse = new(new Vector3(54.6f, 1.6f, 90f), new Vector3(2.4f, 3.6f, 3.6f));
        static readonly Bounds AlleySouth = new(new Vector3(-33f, 5f, -65f), new Vector3(6f, 12f, 50f));
        static readonly Bounds AlleyEast = new(new Vector3(58f, 5f, 82.5f), new Vector3(8f, 12f, 49f));
        static readonly Bounds Plaza = new(new Vector3(-36f, 5f, 9f), new Vector3(47f, 12f, 59f));

        public static AudioZone ZoneAt(Vector3 p)
        {
            if (Tunnel.Contains(p) || TunnelDoorHouse.Contains(p)) return AudioZone.Tunnel;
            if (LabInteriorZone.InAny(p) || CityLayout.LabInterior.Contains(p)) return AudioZone.Lab;
            if (AlleySouth.Contains(p) || AlleyEast.Contains(p)) return AudioZone.Alley;
            if (Plaza.Contains(p)) return AudioZone.Plaza;
            return AudioZone.Street;
        }

        public static bool Indoors(AudioZone z) => z == AudioZone.Lab || z == AudioZone.Tunnel;

        static AudioListener listener;
        static float listenerCheck;

        /// <summary>Forget the cached listener (scene loads, newly created cameras).</summary>
        public static void InvalidateListener() { listener = null; listenerCheck = 0f; }

        /// <summary>The enabled AudioListener (cached, re-resolved twice per second).</summary>
        public static AudioListener Listener
        {
            get
            {
                if ((!listener || !listener.isActiveAndEnabled) && Time.unscaledTime >= listenerCheck)
                {
                    listenerCheck = Time.unscaledTime + 0.5f;
                    listener = null;
                    foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                        if (l.isActiveAndEnabled) { listener = l; break; }
                }
                return listener;
            }
        }

        public static Vector3 ListenerPosition
        {
            get
            {
                var l = Listener;
                if (l) return l.transform.position;
                var c = Camera.main;
                return c ? c.transform.position : Vector3.zero;
            }
        }

        /// <summary>Where the player "is" for zone decisions: the controlled body, else the listener.</summary>
        public static Vector3 FocusPosition
        {
            get
            {
                var pm = Possession.PossessionManager.Instance;
                var body = pm ? pm.ActiveBody : null;
                if (body != null && body.Root) return body.Root.position + Vector3.up * 0.5f;
                return ListenerPosition;
            }
        }

        /// <summary>2D looping/one-shot source on a child object, excluded from reverb zones.</summary>
        public static AudioSource Make2D(Transform parent, string name, int priority = 64, bool bypassListenerFx = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 0f; a.priority = priority;
            a.bypassReverbZones = true; a.reverbZoneMix = 0f; a.dopplerLevel = 0f;
            a.bypassListenerEffects = bypassListenerFx;
            a.volume = 0f;
            return a;
        }
    }
}
