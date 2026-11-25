using Parasite.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Parasite.Audio
{
    /// <summary>Live lakeside caption/beat narration, with a finite completion contract for the seven-second outcome film.</summary>
    public sealed class LakesideNarration : MonoBehaviour
    {
        public const string Speaker = "NARRATOR";
        public const float BeatDelay = .35f;
        const float BackendGrace = 1.5f;
        LakesideEpilogue bound;
        Text caption, beat;
        string lastCaption, lastBeat, pendingBeat, captionKey, beatKey;
        float pendingDelay, narrationDeadline;

        void Bind(LakesideEpilogue epilogue)
        {
            if (bound == epilogue) return;
            bound = epilogue; caption = beat = null;
            lastCaption = lastBeat = pendingBeat = captionKey = beatKey = null;
            narrationDeadline = 0;
            if (epilogue)
                foreach (var text in epilogue.GetComponentsInChildren<Text>(true))
                {
                    if (text.name == "Caption") caption = text;
                    else if (text.name == "Beat") beat = text;
                }
        }

        static LakesideNarration Current()
        {
            var director = AudioDirector.Instance;
            return director ? director.GetComponent<LakesideNarration>() : null;
        }

        static bool CanNarrate => VoiceOver.Instance && VoiceOver.Instance.isActiveAndEnabled &&
            AudioBus.Listener && AudioListener.volume > .0001f && AudioBus.VoiceGain > .0001f;

        /// <summary>Only this epilogue's caption/queued beat can delay automatic completion. Explicit skip bypasses it.</summary>
        public static bool HasPendingFor(LakesideEpilogue owner)
        {
            var narrator = Current();
            if (!owner || !owner.NarrationReady || !narrator || !narrator.isActiveAndEnabled || !CanNarrate) return false;
            narrator.Bind(owner);
            // Update order must not let a just-presented caption bypass narration before this component observes it.
            if ((narrator.caption && narrator.caption.isActiveAndEnabled && narrator.caption.text != narrator.lastCaption) ||
                (narrator.beat && narrator.beat.text != narrator.lastBeat)) return true;
            if (Time.unscaledTime > narrator.narrationDeadline)
            {
                narrator.pendingBeat = null;
                return false; // A disabled/stalled audio backend cannot trap the ending indefinitely.
            }
            bool ownVoice = VoiceOver.Speaking && (VoiceOver.CurrentKey == narrator.captionKey || VoiceOver.CurrentKey == narrator.beatKey);
            return narrator.pendingBeat != null || ownVoice;
        }

        /// <summary>Cancel queued narration as well as current speech when the player skips or leaves the outcome.</summary>
        public static void StopFor(LakesideEpilogue owner)
        {
            var narrator = Current();
            if (narrator && narrator.bound == owner)
            {
                // A disabled VO component cannot run its normal short fade in Update.
                // Stop only this epilogue's clips directly in that case.
                var voice = VoiceOver.Instance;
                if (voice && !voice.isActiveAndEnabled)
                {
                    var captionClip = VoiceOver.ClipFor(Speaker, narrator.lastCaption);
                    var beatClip = VoiceOver.ClipFor(Speaker, narrator.lastBeat);
                    foreach (var source in voice.GetComponentsInChildren<AudioSource>(true))
                        if (source.clip && (source.clip == captionClip || source.clip == beatClip)) source.Stop();
                }
                narrator.pendingBeat = narrator.captionKey = narrator.beatKey = null;
                narrator.narrationDeadline = 0;
            }
            VoiceOver.StopSpeaking();
        }

        void Update()
        {
            var epilogue = LakesideEpilogue.Instance;
            Bind(epilogue);
            if (!epilogue || !epilogue.NarrationReady || !CanNarrate) return;
            if (caption && caption.isActiveAndEnabled && caption.text != lastCaption)
            {
                lastCaption = caption.text;
                captionKey = VoiceOver.KeyFor(Speaker, lastCaption);
                beatKey = beat ? VoiceOver.KeyFor(Speaker, beat.text) : null;
                float spoken = VoiceOver.Duration(Speaker, lastCaption);
                float after = beat ? VoiceOver.Duration(Speaker, beat.text) : 0;
                narrationDeadline = Time.unscaledTime + spoken + BeatDelay + after + BackendGrace;
                VoiceOver.Speak(Speaker, lastCaption);
                pendingBeat = null;
                pendingDelay = BeatDelay;
            }
            if (beat && beat.text != lastBeat)
            {
                lastBeat = beat.text;
                beatKey = VoiceOver.KeyFor(Speaker, lastBeat);
                pendingBeat = string.IsNullOrWhiteSpace(lastBeat) ? null : lastBeat;
                narrationDeadline = Mathf.Max(narrationDeadline, Time.unscaledTime + VoiceOver.Duration(Speaker, lastCaption) +
                    BeatDelay + VoiceOver.Duration(Speaker, lastBeat) + BackendGrace);
            }
            if (pendingBeat != null && !VoiceOver.Speaking)
            {
                pendingDelay -= Time.unscaledDeltaTime;
                if (pendingDelay <= 0f) { VoiceOver.Speak(Speaker, pendingBeat); pendingBeat = null; }
            }
        }
    }
}
