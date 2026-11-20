using Parasite.FX;
using UnityEngine;

namespace Parasite
{
    /// <summary>Player audio preferences, independent of scene and temporary effect levels.</summary>
    public static class AudioPreferences
    {
        const string MasterKey = "parasite.audio.master";
        const string AmbienceKey = "parasite.audio.ambience";
        public static float MasterVolume { get; private set; } = 1f;
        public static float AmbienceVolume { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            MasterVolume = Normalize(PlayerPrefs.GetFloat(MasterKey, 1f));
            AmbienceVolume = Normalize(PlayerPrefs.GetFloat(AmbienceKey, 1f));
            AudioListener.volume = MasterVolume;
            Sfx.AmbienceVolume = AmbienceVolume;
        }

        public static void SetMaster(float value)
        {
            MasterVolume = Normalize(value);
            // Listener gain also changes sounds that were already playing when the slider moved.
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        }

        public static void SetAmbience(float value)
        {
            AmbienceVolume = Normalize(value);
            Sfx.AmbienceVolume = AmbienceVolume;
            PlayerPrefs.SetFloat(AmbienceKey, AmbienceVolume);
        }

        static float Normalize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
    }
}
