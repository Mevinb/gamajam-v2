using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>
    /// Persistent audio root (survives scene loads so music crossfades from the title into the city).
    /// Hosts the scene-independent systems: dialogue voice-over, adaptive music, listener effects (pause muffle,
    /// zone reverb), UI sounds and lakeside narration. Per-scene world sound lives in FX.SfxAmbience.
    /// Every child re-binds its GameEvents handlers after scene loads because CityDirector clears the event hub.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }
        static bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; quitting = false; Application.quitting -= OnQuit; Application.quitting += OnQuit; }
        static void OnQuit() => quitting = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Ensure();

        public static AudioDirector Ensure()
        {
            if (Instance) return Instance;
            if (quitting || !Application.isPlaying) return null;
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioDirector>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            gameObject.AddComponent<VoiceOver>();
            gameObject.AddComponent<MusicDirector>();
            gameObject.AddComponent<ListenerFx>();
            gameObject.AddComponent<UiAudio>();
            gameObject.AddComponent<LakesideNarration>();
            SyncListenerPresence(true);
        }

        bool sourcesEnabled = true;

        /// <summary>Keep the persistent sources disabled while no AudioListener exists (test bootstrap scenes),
        /// which also avoids Unity's "no audio listeners" warning.</summary>
        void SyncListenerPresence(bool force)
        {
            bool has = AudioBus.Listener;
            if (!force && has == sourcesEnabled) return;
            sourcesEnabled = has;
            foreach (var s in GetComponentsInChildren<AudioSource>(true)) s.enabled = has;
            foreach (var z in GetComponentsInChildren<AudioReverbZone>(true)) z.enabled = has;
        }

        void Update() => SyncListenerPresence(false);

        void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { AudioBus.InvalidateListener(); SyncListenerPresence(false); }

        /// <summary>True when the source can play now (re-checks the listener once if it was disabled).</summary>
        public static bool Ready(AudioSource source)
        {
            if (!source) return false;
            if (source.enabled) return true;
            var d = Instance;
            if (!d) return false;
            AudioBus.InvalidateListener();
            d.SyncListenerPresence(false);
            return source.enabled;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
