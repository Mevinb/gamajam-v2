using System.Collections;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>A protected ferry handoff, never another lake-outcome selection.</summary>
    public sealed class IslandArrival : MonoBehaviour
    {
        static readonly (string who, string line) SpecimenCue = ("SPECIMEN 7", "Two currents met beneath the hull. One specimen reached the far shore.");
        static readonly (string who, string line) RadioCue = ("ISLAND RADIO", "Low water opens the culvert. Maintenance can take the service gate. Find the dry refuge beyond the fence.");
        public static float PresentationSeconds => IslandVoiceTiming.Window(SpecimenCue.who, SpecimenCue.line, 1.5f) +
            IslandVoiceTiming.Window(RadioCue.who, RadioCue.line, 2.5f);

        public static void BeginIfNeeded()
        {
            if (!RunState.IslandStarted || RunState.Has(IslandCampaign.ArrivalSeen)) return;
            new GameObject("Island arrival").AddComponent<IslandArrival>();
        }
        IEnumerator Start()
        {
            var gm = GameManager.Instance;
            if (!gm) { Destroy(gameObject); yield break; }
            gm.BeginEnding();
            GameEvents.RaiseSubtitle(SpecimenCue.who, SpecimenCue.line);
            float radioAt = IslandVoiceTiming.Window(SpecimenCue.who, SpecimenCue.line, 1.5f);
            float radioWindow = IslandVoiceTiming.Window(RadioCue.who, RadioCue.line, 2.5f);
            float finishAt = radioAt + radioWindow;
            bool radioSaid = false, skipped = false;
            double started = Time.realtimeSinceStartupAsDouble;
            float age = 0; bool released = !GameInput.InteractPressed && !GameInput.JumpPressed;
            if (GameInput.Simulated) Debug.Log($"[IslandArrival] begin frame={Time.frameCount} priorDelta={Time.unscaledDeltaTime:F4} firstWindow={radioAt:F4} totalWindow={finishAt:F4}");
            while ((age = (float)(Time.realtimeSinceStartupAsDouble - started)) < finishAt)
            {
                // The scene-load frame elapsed before this cue; it must not consume any of its spoken window.
                if (!radioSaid && age > radioAt)
                {
                    radioSaid = true;
                    if (GameInput.Simulated) Debug.Log($"[IslandArrival] radio frame={Time.frameCount} elapsed={age:F4} firstWindow={radioAt:F4} priorSpeaking={Audio.VoiceOver.Speaking}");
                    GameEvents.RaiseSubtitle(RadioCue.who, RadioCue.line);
                    // A late frame at the cue transition must not consume the radio's own spoken window.
                    finishAt = (float)(Time.realtimeSinceStartupAsDouble - started) + radioWindow;
                }
                if (!GameInput.InteractPressed && !GameInput.JumpPressed) released = true;
                if (released && age > .5f && (GameInput.InteractPressed || GameInput.JumpPressed || GameInput.PausePressed)) { skipped = true; break; }
                yield return null;
            }
            if (GameInput.Simulated) Debug.Log($"[IslandArrival] finish frame={Time.frameCount} elapsed={Time.realtimeSinceStartupAsDouble - started:F4} skipped={skipped} radioSaid={radioSaid}");
            MissionManager.SetFlag(IslandCampaign.ArrivalSeen);
            GameEvents.RaiseSubtitle("", "");
            if (gm) gm.ResumeAfterDialogue();
            if (MissionManager.Instance) MissionManager.Instance.ReannounceCurrentMission();
            Destroy(gameObject);
        }
    }
}
