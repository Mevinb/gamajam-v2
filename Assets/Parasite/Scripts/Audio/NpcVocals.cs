using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>
    /// Gameplay barks voiced with the TTS vocal sets in Resources/Audio/Vocals (Tools/audio/gen_voice.sh --vocals),
    /// driven purely by polling public state — no AI code changes:
    /// guards (one of three consistent voices each) on Patrol→Investigate ("Who's there?"), →Pursue ("Stop right
    /// there!"), losing track ("Must've been nothing.") and captures; radio dispatch when the alert level rises and idle
    /// chatter from a possessed guard's radio; civilians gasp/flee when alarmed; scientists murmur in the lab.
    /// </summary>
    public sealed class NpcVocals : MonoBehaviour
    {
        readonly Dictionary<HostBrain, GuardState> guardStates = new();
        readonly Dictionary<HostBrain, char> icons = new();
        readonly Dictionary<HostBrain, float> lastBark = new();
        float tickT, globalCd, dispatchCd = 6f, chatterT = 40f, murmurT = 10f, rebindT, pendingDispatch = -1f;
        SuspicionLevel lastLevel;

        void OnEnable() => Bind();
        void OnDisable() => GameEvents.Captured -= OnCaptured;
        void Bind() { GameEvents.Captured -= OnCaptured; GameEvents.Captured += OnCaptured; }

        static string VoiceTag(HostCharacter h, int count)
        {
            uint hash = (uint)h.GetInstanceID() * 2654435761u;   // stable for the session
            return ((char)('a' + (int)(hash >> 16) % count)).ToString();
        }

        static bool Play(string set, Vector3 pos, float vol, float minDist = 3f, float maxDist = 32f, int priority = 60, bool radio = false)
        {
            var clips = SfxBackend.Variants("Vocals/" + set);
            if (clips == null) return false;
            var clip = SfxBackend.Pick(clips, set);
            var src = SfxBackend.PlayClip(clip, pos, vol * Sfx.VoiceVolume, 1f, SfxPool.TagVocal, priority, minDist, maxDist, 3, false, radio ? 0.2f : 1f);
            return src;
        }

        bool Bark(HostCharacter h, string category, float vol = 0.95f)
        {
            if (globalCd > 0f || !h) return false;
            var brain = h.Brain;
            if (brain && lastBark.TryGetValue(brain, out float t) && Time.time - t < 4.5f) return false;
            if (!Play(category + "_" + VoiceTag(h, 3), h.EyePosition, vol)) return false;
            if (brain) lastBark[brain] = Time.time;
            globalCd = 1.6f;
            return true;
        }

        void OnCaptured()
        {
            var pm = Possession.PossessionManager.Instance;
            var body = pm ? pm.ActiveBody : null;
            if (body == null || !body.Root) return;
            HostCharacter best = null; float bestD = 64f;
            foreach (var h in HostCharacter.All)
            {
                if (!h || !h.IsAwakeNpc || h.Profession != Profession.Guard) continue;
                float d = (h.transform.position - body.Root.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = h; }
            }
            if (best) { globalCd = 0f; Bark(best, "capture", 1f); }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            float dt = Time.deltaTime;
            rebindT -= Time.unscaledDeltaTime;
            if (rebindT <= 0f) { rebindT = 1f; Bind(); }
            if (!gm || !gm.IsPlaying || dt <= 0f) return;
            globalCd -= dt; dispatchCd -= dt; chatterT -= dt; murmurT -= dt;
            tickT -= dt;
            if (tickT > 0f) return;
            float step = 0.2f;
            tickT = step;
            Vector3 L = AudioBus.ListenerPosition;

            foreach (var h in HostCharacter.All)
            {
                if (!h || !h.Brain) continue;
                var brain = h.Brain;
                bool near = (h.transform.position - L).sqrMagnitude < 34f * 34f;
                if (brain is GuardAI g)
                {
                    var s = g.GState;
                    if (guardStates.TryGetValue(brain, out var prev) && prev != s && h.IsAwakeNpc && near)
                    {
                        if (s == GuardState.Investigate && prev == GuardState.Patrol) Bark(h, "investigate");
                        else if (s == GuardState.Pursue) { globalCd = Mathf.Min(globalCd, 0.3f); Bark(h, "pursue", 1f); }
                        else if (s == GuardState.Patrol && prev == GuardState.Investigate) Bark(h, "lost", 0.85f);
                        else if (s == GuardState.Investigate && prev == GuardState.Pursue) Bark(h, "lost", 0.9f);
                    }
                    guardStates[brain] = s;
                }
                else
                {
                    char icon = brain.AlertIcon;
                    icons.TryGetValue(brain, out char prevIcon);
                    if (icon == '!' && prevIcon != '!' && h.IsAwakeNpc && near && globalCd <= 0f)
                    {
                        string sex = VoiceTag(h, 2) == "a" ? "f" : "m";
                        bool flee = h.Velocity.sqrMagnitude > 4f || Random.value < 0.5f;
                        if (Play((flee ? "civ_flee_" : "civ_gasp_") + sex, h.EyePosition, 0.9f, 3f, 30f, 70)) globalCd = 1.2f;
                    }
                    icons[brain] = icon;
                }
            }

            // radio dispatch when the alert level rises (heard from the possessed guard's radio, else the nearest guard)
            var ss = SuspicionSystem.Instance;
            var level = ss ? ss.Level : SuspicionLevel.Calm;
            if (level >= SuspicionLevel.Investigating && lastLevel < SuspicionLevel.Investigating && dispatchCd <= 0f) pendingDispatch = 1.4f;
            lastLevel = level;
            if (pendingDispatch > 0f)
            {
                pendingDispatch -= step;
                if (pendingDispatch <= 0f) { pendingDispatch = -1f; if (RadioFrom(out var p, out bool own)) { Play("dispatch", p, own ? 0.55f : 0.8f, own ? 1.5f : 2f, own ? 10f : 22f, 70, true); dispatchCd = 25f; } }
            }
            if (chatterT <= 0f)
            {
                chatterT = Random.Range(38f, 75f);
                var active = ActiveHost();
                if (active && active.Profession == Profession.Guard && level < SuspicionLevel.Pursuing)
                    Play(Random.value < 0.5f ? "chatter" : "dispatch", active.ChestPosition, 0.45f, 1.5f, 10f, 90, true);
            }
            if (murmurT <= 0f)
            {
                murmurT = Random.Range(11f, 22f);
                if (AudioBus.ZoneAt(AudioBus.FocusPosition) == AudioZone.Lab && level == SuspicionLevel.Calm)
                {
                    HostCharacter pick = null; float best = 11f * 11f;
                    foreach (var h in HostCharacter.All)
                    {
                        if (!h || !h.IsAwakeNpc || h.Profession != Profession.Scientist || h.IsDrVale) continue;
                        float d = (h.transform.position - L).sqrMagnitude;
                        if (d < best && Random.value < 0.8f) { best = d; pick = h; }
                    }
                    if (pick) Play("sci_murmur", pick.EyePosition, 0.5f, 2f, 14f, 150);
                }
            }
        }

        static HostCharacter ActiveHost()
        {
            var pm = Possession.PossessionManager.Instance;
            return pm && pm.Active != null ? pm.Active.Host : null;
        }

        static bool RadioFrom(out Vector3 pos, out bool own)
        {
            var active = ActiveHost();
            own = active && active.Profession == Profession.Guard;
            if (own) { pos = active.ChestPosition; return true; }
            Vector3 L = AudioBus.ListenerPosition;
            HostCharacter best = null; float bestD = 26f * 26f;
            foreach (var h in HostCharacter.All)
            {
                if (!h || !h.IsAwakeNpc || h.Profession != Profession.Guard) continue;
                float d = (h.transform.position - L).sqrMagnitude;
                if (d < bestD) { bestD = d; best = h; }
            }
            pos = best ? best.ChestPosition : Vector3.zero;
            return best;
        }
    }
}
