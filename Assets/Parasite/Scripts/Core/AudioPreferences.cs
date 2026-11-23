using Parasite.FX;
using UnityEngine;

namespace Parasite
{
    /// <summary>
    /// Player audio preferences (bus volumes), independent of scene and temporary effect levels.
    /// Master drives AudioListener.volume; the other buses drive the matching Sfx.* volume read by the audio systems.
    /// </summary>
    public static class AudioPreferences
    {
        public const string MasterKey = "parasite.audio.master";
        public const string AmbienceKey = "parasite.audio.ambience";
        public const string MusicKey = "parasite.audio.music";
        public const string EffectsKey = "parasite.audio.effects";
        public const string VoiceKey = "parasite.audio.voice";
        public static float MasterVolume { get; private set; } = 1f;
        public static float AmbienceVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float EffectsVolume { get; private set; } = 1f;
        public static float VoiceVolume { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            MasterVolume = Normalize(PlayerPrefs.GetFloat(MasterKey, 1f));
            AmbienceVolume = Normalize(PlayerPrefs.GetFloat(AmbienceKey, 1f));
            MusicVolume = Normalize(PlayerPrefs.GetFloat(MusicKey, 1f));
            EffectsVolume = Normalize(PlayerPrefs.GetFloat(EffectsKey, 1f));
            VoiceVolume = Normalize(PlayerPrefs.GetFloat(VoiceKey, 1f));
            AudioListener.volume = MasterVolume;
            Sfx.AmbienceVolume = AmbienceVolume;
            Sfx.MusicVolume = MusicVolume;
            Sfx.Volume = EffectsVolume;
            Sfx.VoiceVolume = VoiceVolume;
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

        public static void SetMusic(float value)
        {
            MusicVolume = Normalize(value);
            Sfx.MusicVolume = MusicVolume;
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        }

        public static void SetEffects(float value)
        {
            EffectsVolume = Normalize(value);
            Sfx.Volume = EffectsVolume;
            PlayerPrefs.SetFloat(EffectsKey, EffectsVolume);
        }

        public static void SetVoice(float value)
        {
            VoiceVolume = Normalize(value);
            Sfx.VoiceVolume = VoiceVolume;
            PlayerPrefs.SetFloat(VoiceKey, VoiceVolume);
        }

        static float Normalize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
    }
}
