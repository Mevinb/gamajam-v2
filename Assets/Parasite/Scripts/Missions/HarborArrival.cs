using System.Collections;
using UnityEngine;
namespace Parasite.Missions
{
    /// <summary>Finite protected, caption-only harbor arrival. No prerecorded narration is claimed for these new lines.</summary>
    public sealed class HarborArrival : MonoBehaviour
    {
        public static HarborArrival Instance { get; private set; }
        public const float PresentationSeconds = 8f;
        public float ElapsedSeconds { get; private set; }
        public bool Running { get; private set; }
        GameManager manager; bool released, cleaned, ownsObject, retired, retirementScheduled, capturedPresentation;
        int presentationScene;
        void Awake()
        {
            if (Instance && Instance != this) { Retire(); return; }
            Instance = this;
        }
        public static void BeginIfNeeded()
        {
            if (!HarborCampaign.Active || RunState.Has(HarborCampaign.ArrivalSeen)) return;
            if (Instance)
            {
                if (!Instance.retired) return;
                Instance = null;
            }
            new GameObject("Harbor arrival captions").AddComponent<HarborArrival>().ownsObject = true;
        }
        IEnumerator Start()
        {
            manager = GameManager.Instance;
            if (retired || !isActiveAndEnabled || !manager || !manager.IsPlaying || !HarborCampaign.Active || RunState.Has(HarborCampaign.ArrivalSeen) || Instance != this)
            { Retire(); yield break; }
            presentationScene = manager.gameObject.scene.handle; capturedPresentation = true;
            manager.BeginEnding(); Running = true;
            GameEvents.RaiseSubtitle("SPECIMEN 7", HarborCampaign.Peace ? "The island is behind us. Ahead, the harbor still holds the cure behind its locks."
                : "The island answered. Ahead, the harbor carries the pieces of the next connection.");
            double started = Time.realtimeSinceStartupAsDouble; bool radio = false;
            released = !GameInput.InteractPressed && !GameInput.JumpPressed;
            try
            {
                while ((ElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - started)) < PresentationSeconds)
                {
                    if (retired || !OwnsPresentationContext()) { Retire(); yield break; }
                    if (!radio && ElapsedSeconds >= 3f)
                    {
                        radio = true;
                        GameEvents.RaiseSubtitle("HARBOR RADIO", "Balance the lock: A adds three, B adds two, C bleeds one. Stay below five. Then read the moving lot numbers before you scan.");
                    }
                    if (!GameInput.InteractPressed && !GameInput.JumpPressed) released = true;
                    if (released && ElapsedSeconds > .5f && (GameInput.InteractPressed || GameInput.JumpPressed || GameInput.PausePressed)) break;
                    yield return null;
                }
                if (!retired && OwnsPresentationContext()) MissionManager.SetFlag(HarborCampaign.ArrivalSeen);
            }
            finally { Cleanup(); }
            if (!retired && manager && manager == GameManager.Instance && MissionManager.Instance) MissionManager.Instance.ReannounceCurrentMission();
            Retire();
        }
        bool OwnsPresentationContext() => capturedPresentation && manager && manager == GameManager.Instance && Instance == this
            && manager.State == GameState.Ending && manager.gameObject.scene.handle == presentationScene && gameObject.scene.handle == presentationScene;
        void Cleanup()
        {
            if (cleaned) return; cleaned = true;
            bool ownedContext = OwnsPresentationContext(); capturedPresentation = false; Running = false;
            if (!ownedContext) return;
            GameEvents.RaiseSubtitle("", ""); manager.ResumeAfterDialogue();
        }
        void Retire()
        {
            retired = true; if (Instance == this) Instance = null;
            if (retirementScheduled) return; retirementScheduled = true;
            if (ownsObject) Destroy(gameObject); else Destroy(this);
        }
        void OnDisable()
        {
            // Component disable does not stop Unity coroutines. Cancellation is permanent even if re-enabled before Start.
            retired = true;
            try { StopAllCoroutines(); }
            finally { Cleanup(); Retire(); }
        }
        void OnDestroy()
        {
            retired = true; Cleanup(); if (Instance == this) Instance = null;
        }
    }
}
