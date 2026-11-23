using System.Collections.Generic;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Stealth;
using Parasite.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite.Audio
{
    /// <summary>Musical context chosen each frame by <see cref="MusicDirector"/>.</summary>
    public enum MusicContext { Silence, Title, Explore, Opening, Story, Lakeside, EndPeace, EndDominion }

    /// <summary>
    /// Adaptive music. Gameplay uses four sample-locked stems (same tempo/key/length, started together with
    /// PlayScheduled): city bed, lab bed, tension and chase; their gains follow the suspicion level, guard pursuit and
    /// whether the player is inside the lab/tunnel. Story moments (title, opening, dialogue, lakeside, both endings)
    /// crossfade to dedicated cues. A mission-complete stinger dips the stems. Everything runs on unscaled time,
    /// keeps playing (muffled by ListenerFx) in the pause menu and ducks under dialogue via <see cref="AudioBus"/>.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }
        public MusicContext Context { get; private set; } = MusicContext.Silence;
        /// <summary>0 calm · 1 curious · 2 investigating · 3 pursuit (after hold times).</summary>
        public int Intensity { get; private set; }
        public bool InLab { get; private set; }

        const string Folder = "Audio/Music/";
        static readonly string[] StemNames = { "mus_city_calm", "mus_lab_calm", "mus_tension", "mus_chase" };
        const int City = 0, Lab = 1, Tension = 2, Chase = 3;

        readonly AudioSource[] stems = new AudioSource[4];
        readonly float[] stemLevel = new float[4];
        AudioSource cueA, cueB, cue, stinger;
        string cueName;
        float cueLevelA, cueLevelB, exploreLevel, stingerDip, rebindT, pursuitHold, tensionHold;
        bool stemsStarted;
        double stemsStartDsp;
        float cueStarted;
        EndingOutcome lastOutcome;
        static readonly Dictionary<string, AudioClip> clips = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { clips.Clear(); Instance = null; }

        static AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>(Folder + name);
            clips[name] = c;
            return c;
        }

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < stems.Length; i++)
            {
                stems[i] = AudioBus.Make2D(transform, "Stem " + StemNames[i], 2);
                stems[i].loop = true;
            }
            cueA = AudioBus.Make2D(transform, "Cue A", 1);
            cueB = AudioBus.Make2D(transform, "Cue B", 1);
            stinger = AudioBus.Make2D(transform, "Stinger", 1);
            cue = cueA;
        }

        void OnEnable() { Bind(); SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { Unbind(); SceneManager.sceneLoaded -= OnSceneLoaded; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnSceneLoaded(Scene s, LoadSceneMode m) { Bind(); lastOutcome = EndingOutcome.Unchosen; }

        void Bind()
        {
            GameEvents.Toast -= OnToast; GameEvents.Toast += OnToast;
            GameEvents.Ending -= OnEnding; GameEvents.Ending += OnEnding;
        }

        void Unbind() { GameEvents.Toast -= OnToast; GameEvents.Ending -= OnEnding; }

        void OnEnding(EndingData d) { if (d.outcome != EndingOutcome.Unchosen) lastOutcome = d.outcome; }

        void OnToast(string msg, Color c)
        {
            if (string.IsNullOrEmpty(msg) || !msg.StartsWith("MISSION COMPLETE")) return;
            PlayStinger("stinger_mission");
        }

        public void PlayStinger(string name)
        {
            var clip = Clip(name);
            if (!clip || !AudioDirector.Ready(stinger)) return;
            stinger.clip = clip;
            stinger.volume = Mathf.Clamp01(AudioBus.MusicGain * 1.6f);
            stinger.Play();
            stingerDip = clip.length;
        }

        // ------------------------------------------------------------------ context
        MusicContext Resolve()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (scene == GameManager.MenuScene) return MusicContext.Title;
            var lake = LakesideEpilogue.Instance;
            if (lake)
                return lake.Outcome == EndingOutcome.Peace ? MusicContext.EndPeace
                     : lake.Outcome == EndingOutcome.Dominion ? MusicContext.EndDominion : MusicContext.Lakeside;
            var gm = GameManager.Instance;
            if (!gm) return MusicContext.Silence;
            if (gm.State == GameState.Ended)
                return lastOutcome == EndingOutcome.Dominion ? MusicContext.EndDominion : MusicContext.EndPeace;
            var opening = OpeningCinematic.Instance;
            if (opening && opening.Running) return MusicContext.Opening;
            var mm = MissionManager.Instance;
            if (mm && (mm.DialoguePlaying || (mm.EndingStarted && gm.State == GameState.Ending))) return MusicContext.Story;
            return MusicContext.Explore;
        }

        static string CueFor(MusicContext c) => c switch
        {
            MusicContext.Title => "mus_title",
            MusicContext.Opening => "mus_opening",
            MusicContext.Story => "mus_story",
            MusicContext.Lakeside => "mus_lakeside",
            MusicContext.EndPeace => "mus_end_peace",
            MusicContext.EndDominion => "mus_end_dominion",
            _ => null,
        };

        int ResolveIntensity(float dt)
        {
            int level = 0;
            var ss = SuspicionSystem.Instance;
            if (ss) level = (int)ss.Level;
            bool pursuit = level >= 3;
            if (!pursuit)
                foreach (var h in HostCharacter.All)
                    if (h && h.IsAwakeNpc && h.Brain is GuardAI g && g.GState == GuardState.Pursue) { pursuit = true; break; }
            if (pursuit) { pursuitHold = 6f; level = 3; }
            else if (pursuitHold > 0f) { pursuitHold -= dt; level = 3; }
            if (level >= 1) tensionHold = Mathf.Max(tensionHold, level >= 2 ? 5f : 3f);
            else if (tensionHold > 0f) { tensionHold -= dt; level = 1; }
            var gm = GameManager.Instance;
            if (gm && gm.State == GameState.Resetting) level = 0;
            return level;
        }

        void StartStems()
        {
            if (stemsStarted || !AudioDirector.Ready(stems[0])) return;
            double at = AudioSettings.dspTime + 0.2;
            stemsStartDsp = at;
            bool any = false;
            for (int i = 0; i < stems.Length; i++)
            {
                var clip = Clip(StemNames[i]);
                if (!clip) continue;
                stems[i].clip = clip;
                stems[i].volume = 0f;
                stems[i].PlayScheduled(at);
                any = true;
            }
            stemsStarted = any;
        }

        void SwitchCue(string name, MusicContext ctx)
        {
            if (name == cueName) return;
            cueName = name;
            var clip = Clip(name);
            var next = cue == cueA ? cueB : cueA;
            next.Stop();
            next.clip = clip;
            next.loop = ctx != MusicContext.Opening;
            next.volume = 0f;
            cue = next;
            StartCue(ctx);
        }

        void StartCue(MusicContext ctx)
        {
            if (!cue.clip || !AudioDirector.Ready(cue)) return;
            cue.time = 0f;
            if (ctx == MusicContext.Opening && OpeningCinematic.Instance)
                cue.time = Mathf.Clamp(OpeningCinematic.Instance.ElapsedSeconds, 0f, cue.clip.length - 0.05f);
            cue.Play();
            cueStarted = Time.realtimeSinceStartup;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            rebindT -= dt;
            if (rebindT <= 0f) { rebindT = 1f; Bind(); }

            Context = Resolve();
            string want = CueFor(Context);
            if (want != null) SwitchCue(want, Context);
            else if (cueName != null && Context != MusicContext.Story) cueName = null;

            // sources disabled while no listener existed stop playing: restart them in sync
            if (stemsStarted && AudioSettings.dspTime > stemsStartDsp + 1.0 && stems[0].enabled && stems[0].clip && !stems[0].isPlaying) { stemsStarted = false; foreach (var s in stems) s.Stop(); }
            if (cueName != null && cue.enabled && cue.clip && !cue.isPlaying && Time.realtimeSinceStartup > cueStarted + 1f &&
                (cue.loop || (Context == MusicContext.Opening && OpeningCinematic.Instance && OpeningCinematic.Instance.ElapsedSeconds < cue.clip.length - 1f)))
                StartCue(Context);
            if (Context == MusicContext.Explore) StartStems();
            Intensity = Context == MusicContext.Explore ? ResolveIntensity(dt) : 0;
            var zone = AudioBus.ZoneAt(AudioBus.FocusPosition);
            InLab = zone == AudioZone.Lab || zone == AudioZone.Tunnel;

            // opening score stays locked to the cinematic clock at normal speed
            if (Context == MusicContext.Opening && cue && cue.isPlaying && OpeningCinematic.Instance &&
                Mathf.Abs(OpeningCinematic.Instance.PlaybackSpeed - 1f) < 0.01f &&
                Mathf.Abs(cue.time - OpeningCinematic.Instance.ElapsedSeconds) > 0.6f && cue.clip &&
                OpeningCinematic.Instance.ElapsedSeconds < cue.clip.length - 0.1f)
                cue.time = OpeningCinematic.Instance.ElapsedSeconds;

            // explore stems vs. cues
            float exploreTarget = Context == MusicContext.Explore ? 1f : 0f;
            exploreLevel = Mathf.MoveTowards(exploreLevel, exploreTarget, dt / (exploreTarget > exploreLevel ? 2.5f : 1.5f));
            float[] targets = StemTargets(Intensity, InLab);
            for (int i = 0; i < 4; i++)
            {
                float up = i == Chase ? 0.8f : i == Tension ? 1.2f : 3f;
                float down = i == Chase ? 4f : i == Tension ? 5f : 3f;
                stemLevel[i] = Mathf.MoveTowards(stemLevel[i], targets[i], dt / (targets[i] > stemLevel[i] ? up : down));
            }

            float cueTargetA = cue == cueA && cueName != null ? 1f : 0f, cueTargetB = cue == cueB && cueName != null ? 1f : 0f;
            float cueIn = Context == MusicContext.Opening ? 0.6f : Context == MusicContext.Story ? 1.5f : 2.5f;
            cueLevelA = Mathf.MoveTowards(cueLevelA, cueTargetA, dt / (cueTargetA > cueLevelA ? cueIn : 2.5f));
            cueLevelB = Mathf.MoveTowards(cueLevelB, cueTargetB, dt / (cueTargetB > cueLevelB ? cueIn : 2.5f));
            if (cueLevelA <= 0f && cueA.isPlaying && cue != cueA) cueA.Stop();
            if (cueLevelB <= 0f && cueB.isPlaying && cue != cueB) cueB.Stop();

            if (stingerDip > 0f) stingerDip -= dt;
            float dip = stingerDip > 0f ? 0.45f : 1f;
            float master = AudioBus.MusicGain * Mathf.Lerp(1f, 0.75f, AudioBus.Muffle);
            for (int i = 0; i < 4; i++)
            {
                stems[i].volume = master * stemLevel[i] * exploreLevel * dip;
                stems[i].mute = false;
            }
            cueA.volume = master * cueLevelA * CueGain(cueA);
            cueB.volume = master * cueLevelB * CueGain(cueB);
            cueA.mute = cueB.mute = stinger.mute = false;
        }

        static float CueGain(AudioSource s) => s.clip && s.clip.name == "mus_story" ? 0.8f : 1f;

        static readonly float[] tmp = new float[4];

        static float[] StemTargets(int intensity, bool lab)
        {
            float bed = intensity switch { 0 => 1f, 1 => 0.85f, 2 => 0.6f, _ => 0.3f };
            tmp[City] = lab ? 0f : bed;
            tmp[Lab] = lab ? bed : 0f;
            tmp[Tension] = intensity switch { 0 => 0f, 1 => 0.55f, 2 => 1f, _ => 0.75f };
            tmp[Chase] = intensity >= 3 ? 1f : 0f;
            return tmp;
        }
    }
}
