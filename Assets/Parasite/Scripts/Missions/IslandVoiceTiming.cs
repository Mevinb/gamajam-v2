using Parasite.Audio;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Finite spoken cue windows; unavailable narration retains the original silent timing.</summary>
    public static class IslandVoiceTiming
    {
        public const float Gap = .15f;
        public static float Window(string speaker, string line, float fallback)
        {
            if (!VoiceOver.Instance || !VoiceOver.Instance.isActiveAndEnabled || !AudioBus.Listener ||
                AudioListener.volume <= .0001f || AudioBus.VoiceGain <= .0001f) return fallback;
            float duration = VoiceOver.Duration(speaker, line);
            return duration > 0 && !float.IsNaN(duration) && !float.IsInfinity(duration)
                ? Mathf.Max(fallback, duration + Gap) : fallback;
        }
    }
}
