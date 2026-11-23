using Parasite.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Parasite.Audio
{
    /// <summary>
    /// Voices the lakeside epilogue's captions as "NARRATOR" without touching LakesideEpilogue: it watches the
    /// epilogue's "Caption" and "Beat" texts and speaks each new caption, then its beat line once the caption ends.
    /// Clips come from the same voice pipeline (gen_voice.py collects LakesideEpilogue caption/beat literals).
    /// </summary>
    public sealed class LakesideNarration : MonoBehaviour
    {
        public const string Speaker = "NARRATOR";
        LakesideEpilogue bound;
        Text caption, beat;
        string lastCaption, lastBeat, pendingBeat;
        float pendingDelay;

        void Update()
        {
            var epi = LakesideEpilogue.Instance;
            if (epi != bound)
            {
                bound = epi; caption = beat = null; lastCaption = lastBeat = pendingBeat = null;
                if (epi)
                    foreach (var t in epi.GetComponentsInChildren<Text>(true))
                    {
                        if (t.name == "Caption") caption = t;
                        else if (t.name == "Beat") beat = t;
                    }
                if (!epi) return;
            }
            if (!epi) return;
            if (caption && caption.isActiveAndEnabled && caption.text != lastCaption)
            {
                lastCaption = caption.text;
                VoiceOver.Speak(Speaker, lastCaption);
                pendingBeat = null;
                pendingDelay = 0.35f;
            }
            if (beat && beat.text != lastBeat)
            {
                lastBeat = beat.text;
                if (!string.IsNullOrWhiteSpace(lastBeat)) pendingBeat = lastBeat;
            }
            if (pendingBeat != null && !VoiceOver.Speaking)
            {
                pendingDelay -= Time.unscaledDeltaTime;
                if (pendingDelay <= 0f) { VoiceOver.Speak(Speaker, pendingBeat); pendingBeat = null; }
            }
        }
    }
}
