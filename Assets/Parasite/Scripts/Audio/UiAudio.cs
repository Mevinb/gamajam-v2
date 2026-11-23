using System.Text.RegularExpressions;
using Parasite.FX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parasite.Audio
{
    /// <summary>
    /// Interface sounds that need no UI code changes: hover/navigation ticks (EventSystem selection changes),
    /// pause menu open/close, objective-updated and checkpoint chimes. Clicks come from UIButtonFx (SfxId.UiClick);
    /// the mission-complete stinger from MusicDirector.
    /// </summary>
    public sealed class UiAudio : MonoBehaviour
    {
        GameObject lastSelected;
        float sceneAge, rebindT, lastMissionComplete = -10f, lastObjectiveChime = -10f;
        string lastObjective = "";
        static readonly Regex Digits = new(@"[0-9]+", RegexOptions.Compiled);

        void OnEnable() { Bind(); SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { Unbind(); SceneManager.sceneLoaded -= OnSceneLoaded; }
        void OnSceneLoaded(Scene s, LoadSceneMode m) { Bind(); sceneAge = 0f; lastSelected = null; lastObjective = ""; }

        void Bind()
        {
            GameEvents.PauseChanged -= OnPause; GameEvents.PauseChanged += OnPause;
            GameEvents.ObjectiveChanged -= OnObjective; GameEvents.ObjectiveChanged += OnObjective;
            GameEvents.Toast -= OnToast; GameEvents.Toast += OnToast;
        }

        void Unbind()
        {
            GameEvents.PauseChanged -= OnPause; GameEvents.ObjectiveChanged -= OnObjective; GameEvents.Toast -= OnToast;
        }

        void OnPause(bool paused) => Sfx.Ui(paused ? SfxId.UiOpen : SfxId.UiClose, 0.8f);

        void OnObjective(string text)
        {
            string norm = Digits.Replace(text ?? "", "#");
            bool changed = norm != lastObjective;
            lastObjective = norm;
            if (!changed || string.IsNullOrWhiteSpace(text) || sceneAge < 2f) return;
            float now = Time.unscaledTime;
            if (now - lastMissionComplete < 3f || now - lastObjectiveChime < 2.5f) return;
            lastObjectiveChime = now;
            Sfx.Ui(SfxId.ObjectiveUpdate, 0.75f);
        }

        void OnToast(string msg, Color c)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (msg.StartsWith("MISSION COMPLETE")) lastMissionComplete = Time.unscaledTime;
            else if (msg.StartsWith("Checkpoint") && sceneAge > 2f) Sfx.Ui(SfxId.Checkpoint, 0.7f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            sceneAge += dt;
            rebindT -= dt;
            if (rebindT <= 0f) { rebindT = 1f; Bind(); }
            var es = EventSystem.current;
            var sel = es ? es.currentSelectedGameObject : null;
            if (sel == lastSelected) return;
            bool moved = sel && lastSelected && lastSelected.activeInHierarchy && sceneAge > 0.5f;
            lastSelected = sel;
            if (moved && sel.TryGetComponent<Selectable>(out var s) && s.interactable) Sfx.Ui(SfxId.UiHover, 0.6f);
        }
    }
}
