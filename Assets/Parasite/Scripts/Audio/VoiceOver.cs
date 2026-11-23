using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite.Audio
{
    /// <summary>
    /// Plays the generated voice-over for every subtitle raised through <see cref="GameEvents.RaiseSubtitle"/>.
    /// Clips live in Resources/Audio/Voice/vo_&lt;fnv1a64(SPEAKER|line)&gt; (Tools/audio/gen_voice.sh). A new line
    /// replaces the current one (short fade), an empty line stops it, lines without a clip are skipped silently
    /// (and, in the Editor, appended to Logs/voice_lines_seen.tsv so the generator can pick them up).
    /// 2D, unaffected by timeScale and the pause low-pass; it pauses with the pause menu; it ducks music and ambience.
    /// </summary>
    public sealed class VoiceOver : MonoBehaviour
    {
        public static VoiceOver Instance { get; private set; }
        public const string ResourceFolder = "Audio/Voice/";
        const float ReplaceFade = 0.09f;

        AudioSource a, b, current, fading;
        float fadeT, rebindT, activity;
        string currentKey, currentSpeaker;
        bool pausedByMenu;
        // Eavesdropped conversations advance their subtitles faster than the lines can be spoken; queue them instead
        // of cutting each line off, and let the conversation finish if the zone completed (not if the player left).
        readonly Queue<(string speaker, string line)> conversation = new();
        Missions.EavesdropZone conversationZone;

        static readonly Dictionary<string, AudioClip> cache = new();
        static readonly HashSet<string> missing = new();

        public static bool Speaking => Instance && Instance.current && (Instance.current.isPlaying || Instance.pausedByMenu);
        /// <summary>Key of the clip currently playing (null when silent).</summary>
        public static string CurrentKey => Speaking ? Instance.currentKey : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); missing.Clear(); Instance = null; }

        // ------------------------------------------------------------------ keys (must match Tools/audio/gen_voice.py)
        static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
        static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
        static string Norm(string s) => Spaces.Replace(Tags.Replace(s ?? "", ""), " ").Trim();

        /// <summary>Stable clip key: "vo_" + FNV-1a 64 (hex) of UTF-8 Upper(speaker) + "|" + line, whitespace-collapsed.</summary>
        public static string KeyFor(string speaker, string line)
        {
            string sp = Norm(speaker);
            if (sp.Length == 0) sp = "NARRATOR";
            var bytes = Encoding.UTF8.GetBytes(sp.ToUpperInvariant() + "|" + Norm(line));
            ulong h = 14695981039346656037UL;
            foreach (byte x in bytes) { h ^= x; h *= 1099511628211UL; }
            return "vo_" + h.ToString("x16");
        }

        static AudioClip Load(string key)
        {
            if (cache.TryGetValue(key, out var c)) return c;
            c = Resources.Load<AudioClip>(ResourceFolder + key);
            cache[key] = c;
            return c;
        }

        /// <summary>The voice clip for a subtitle, or null when none was generated.</summary>
        public static AudioClip ClipFor(string speaker, string line) =>
            string.IsNullOrWhiteSpace(line) ? null : Load(KeyFor(speaker, line));

        /// <summary>Spoken length in seconds (0 when there is no clip). Lets dialogue hold each line long enough.</summary>
        public static float Duration(string speaker, string line)
        {
            var c = ClipFor(speaker, line);
            return c ? c.length : 0f;
        }

        /// <summary>Speak a line without showing a subtitle (e.g. lakeside narration). Empty line stops.</summary>
        public static void Speak(string speaker, string line)
        {
            var d = AudioDirector.Ensure();
            var vo = d ? d.GetComponent<VoiceOver>() : null;
            if (vo) vo.OnSubtitle(speaker, line);
        }

        public static void StopSpeaking() { if (Instance) Instance.OnSubtitle("", ""); }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            Instance = this;
            a = AudioBus.Make2D(transform, "Voice A", 0, bypassListenerFx: true);
            b = AudioBus.Make2D(transform, "Voice B", 0, bypassListenerFx: true);
            a.ignoreListenerPause = b.ignoreListenerPause = true;
        }

        void OnEnable() { Bind(); SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { Unbind(); SceneManager.sceneLoaded -= OnSceneLoaded; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            Bind();
            if (m != LoadSceneMode.Single) return;
            pausedByMenu = false; conversation.Clear(); conversationZone = null;
            OnSubtitle("", "");
        }

        void Bind()
        {
            GameEvents.Subtitle -= OnSubtitle; GameEvents.Subtitle += OnSubtitle;
            GameEvents.PauseChanged -= OnPause; GameEvents.PauseChanged += OnPause;
        }

        void Unbind() { GameEvents.Subtitle -= OnSubtitle; GameEvents.PauseChanged -= OnPause; }

        void OnPause(bool paused)
        {
            if (!current) return;
            if (paused && current.isPlaying) { current.Pause(); pausedByMenu = true; }
            else if (!paused && pausedByMenu) { current.UnPause(); pausedByMenu = false; }
        }

        static bool ZoneDone(Missions.EavesdropZone z) => z && RunState.Has(z.flag);

        void OnSubtitle(string speaker, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (conversationZone && ZoneDone(conversationZone) && (conversation.Count > 0 || (current && current.isPlaying))) return;
                conversation.Clear(); conversationZone = null;
                BeginFade();
                currentKey = null;
                return;
            }
            string key = KeyFor(speaker, line);
            if (key == currentKey && current && (current.isPlaying || pausedByMenu)) return;
            var zone = Missions.EavesdropZone.Active;
            if (zone && zone.speaker == speaker && current && current.isPlaying && currentSpeaker == speaker && Load(key))
            {
                conversationZone = zone;
                conversation.Enqueue((speaker, line));
                return;
            }
            conversation.Clear();
            conversationZone = zone && zone.speaker == speaker ? zone : null;
            PlayLine(speaker, line, key);
        }

        void PlayLine(string speaker, string line, string key)
        {
            var clip = Load(key);
            BeginFade();
            currentKey = key;
            currentSpeaker = speaker;
            pausedByMenu = false;
            if (!clip) { LogMissing(speaker, line, key); return; }
            var src = current == a ? b : a;
            if (src == fading) { src.Stop(); fading = null; }
            if (!AudioDirector.Ready(src)) return;
            src.clip = clip;
            src.volume = AudioBus.VoiceGain;
            src.pitch = 1f;
            src.mute = false;
            src.Play();
            current = src;
        }

        void BeginFade()
        {
            if (current && current.isPlaying)
            {
                if (fading && fading != current) fading.Stop();
                fading = current;
                fadeT = 0f;
            }
            else if (current) current.Stop();
            current = null;
            pausedByMenu = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            rebindT -= dt;
            if (rebindT <= 0f) { rebindT = 1f; Bind(); }
            if (fading)
            {
                fadeT += dt;
                fading.volume = AudioBus.VoiceGain * Mathf.Clamp01(1f - fadeT / ReplaceFade);
                if (fadeT >= ReplaceFade) { fading.Stop(); fading = null; }
            }
            if (current)
            {
                current.volume = AudioBus.VoiceGain;
                current.mute = false;   // the lakeside epilogue mutes pre-existing sources; narration must stay audible
            }
            if (conversation.Count > 0 && !pausedByMenu && (!current || !current.isPlaying))
            {
                var (sp, ln) = conversation.Dequeue();
                PlayLine(sp, ln, KeyFor(sp, ln));
            }
            bool talking = (current && current.isPlaying) || (fading && fading.isPlaying);
            float target = talking && !pausedByMenu ? 1f : 0f;
            activity = Mathf.MoveTowards(activity, target, dt / (target > activity ? 0.15f : 0.7f));
            AudioBus.VoiceActivity = activity;
        }

#if UNITY_EDITOR
        static HashSet<string> logged;
#endif

        static void LogMissing(string speaker, string line, string key)
        {
            if (!missing.Add(key)) return;
#if UNITY_EDITOR
            try
            {
                string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs"));
                string path = System.IO.Path.Combine(dir, "voice_lines_seen.tsv");
                if (logged == null)
                {
                    logged = new HashSet<string>();
                    if (System.IO.File.Exists(path)) foreach (var row in System.IO.File.ReadAllLines(path)) logged.Add(row);
                }
                string rowText = Norm(speaker) + "\t" + (line ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", "\\n");
                if (!logged.Add(rowText)) return;
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(path, rowText + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning("[VoiceOver] could not record missing line: " + e.Message); }
#endif
        }
    }
}
