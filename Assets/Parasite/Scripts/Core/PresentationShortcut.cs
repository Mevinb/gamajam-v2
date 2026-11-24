using Parasite.Missions;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite
{
    /// <summary>One input owner for Ctrl+M from initial boot through menus, story and gameplay.</summary>
    [DefaultExecutionOrder(-9000)]
    public class PresentationShortcut : MonoBehaviour
    {
        static PresentationShortcut instance;
        bool pending;
        AsyncOperation cityLoad;
        GameManager preparedManager;
        int readyFrame;
        public static bool IsPending => instance && instance.pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Ensure();

        public static PresentationShortcut Ensure()
        {
            if (instance) return instance;
            return new GameObject("Presentation Shortcut").AddComponent<PresentationShortcut>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            GameInput.Ensure();
        }

        /// <summary>Queue the same global action exposed by the keyboard; never choose an outcome.</summary>
        public static void Request()
        {
            var owner = Ensure();
            if (RunState.AfterlakeStarted) { owner.pending = false; return; }
            if (LakesideEpilogue.Instance)
            {
                // Arrival may be skipped to the choice, but resolution/summary and its choice remain intact.
                LakesideEpilogue.Instance.RevealChoices();
                owner.pending = false;
                Debug.Log("[PresentationShortcut] Lakeside already active; preserving its explicit choice.");
                return;
            }
            var manager = GameManager.Instance;
            if (manager && manager.State == GameState.Ended)
            {
                owner.pending = false;
                return;
            }
            owner.pending = true;
            Debug.Log("[PresentationShortcut] Ctrl+M received; final escape requested from " + SceneManager.GetActiveScene().name + ".");
            GameEvents.RaiseToast("Ctrl+M received — opening the final escape…", Palette.Info);
        }

        void Update()
        {
            if (GameInput.PresentationEndingPressed) Request();
            if (!pending) return;
            if (RunState.AfterlakeStarted) { pending = false; return; }
            if (LakesideEpilogue.Instance) { pending = false; return; }
            var manager = GameManager.Instance;
            if (manager && manager.State == GameState.Ended) { pending = false; return; }
            var mission = MissionManager.Instance;
            if (mission && mission.EndingStarted) { pending = false; return; }
            if (cityLoad != null)
            {
                if (!cityLoad.isDone) return;
                cityLoad = null;
            }

            // This cleanup is synchronous: the old story cannot resume over the requested ending later.
            OpeningCinematic.CancelForPresentationShortcut();
            if (SceneManager.GetActiveScene().name != GameManager.CityScene)
            {
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                if (menu) menu.CancelForPresentationShortcut();
                RunState.NewGame();
                Time.timeScale = 1f;
                preparedManager = null;
                cityLoad = SceneManager.LoadSceneAsync(GameManager.CityScene, LoadSceneMode.Single);
                return;
            }
            if (!manager || !CityDirector.Instance || !CityDirector.Instance.Ready || !mission) return;
            // A pending reset is allowed to finish its existing real-time reload; the request survives it.
            if (manager.State == GameState.Resetting) return;
            if (preparedManager != manager)
            {
                preparedManager = manager;
                readyFrame = Time.frameCount;
                return;
            }
            // Let scene Start methods register props before the finale claims the campaign.
            if (Time.frameCount <= readyFrame) return;
            if (manager.State == GameState.Paused) manager.SetPaused(false);
            // Mission-owned Vale dialogue is cancelled there; possession transit is retried, not discarded.
            if (!mission.TryPresentationEscape()) return;
            pending = false;
            Debug.Log("[PresentationShortcut] Final escape started; lakeside choice remains unselected.");
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
