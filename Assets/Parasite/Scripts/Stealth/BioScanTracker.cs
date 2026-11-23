using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using UnityEngine;

namespace Parasite.Stealth
{
    /// <summary>
    /// Runtime bio-scan state shared by every hunter scanner and the doorway arch: a read meter per possessed body and
    /// the set of "burned" bodies. Burning a body starts a pursuit on it; leaving that body (or hopping to another)
    /// drops the suspect, which is how the player breaks the trail. A burned body stays burned for the whole chapter.
    /// </summary>
    public static class BioScanTracker
    {
        struct Entry { public float meter, lastFed; public bool warned; }

        static readonly Dictionary<HostCharacter, Entry> entries = new();
        static readonly HashSet<HostCharacter> burned = new();
        static readonly List<HostCharacter> scratch = new();
        static float lastReport = -99f;

        public static int BurnCount { get; private set; }
        public static HostCharacter LastBurned { get; private set; }

        public static void Reset()
        {
            entries.Clear(); burned.Clear();
            BurnCount = 0; LastBurned = null; lastReport = -99f;
        }

        public static float Meter(HostCharacter h) => h && entries.TryGetValue(h, out var e) ? e.meter : 0f;
        public static bool IsBurned(HostCharacter h) => h && burned.Contains(h);

        /// <summary>The most-read body the player currently wears (either amoeba), for the HUD.</summary>
        public static HostCharacter HighestPossessed(out float meter)
        {
            HostCharacter best = null; meter = 0f;
            foreach (var kv in entries)
                if (kv.Key && kv.Key.State == HostState.Possessed && kv.Value.meter > meter) { meter = kv.Value.meter; best = kv.Key; }
            return best;
        }

        public static HostCharacter WornBurnedBody()
        {
            foreach (var h in burned) if (h && h.State == HostState.Possessed) return h;
            return null;
        }

        /// <summary>A scanner reads <paramref name="host"/> this frame. Only possessed humans carry a signature.</summary>
        public static void Feed(HostCharacter host, float amount, string source)
        {
            if (!host || !BioScanRules.Detectable(host.State, false)) return;
            entries.TryGetValue(host, out var e);
            e.meter = Mathf.Clamp01(e.meter + amount);
            e.lastFed = Time.time;
            if (!e.warned && e.meter >= BioScanRules.AlertAt && !burned.Contains(host))
            {
                e.warned = true;
                GameEvents.RaiseToast($"BIO-SCAN {e.meter * 100f:0}% on {host.DisplayName} — break line of sight or swap bodies!", Palette.Warning);
                Missions.MissionManager.Bark("HUNTER", "Scanner's twitching. Hold still, you.");
                Sfx.Ui(SfxId.Heartbeat, 0.7f);
            }
            entries[host] = e;
            if (BioScanRules.Burned(e.meter) || burned.Contains(host)) Burn(host, source);
        }

        static void Burn(HostCharacter host, string source)
        {
            bool first = burned.Add(host);
            if (!first && Time.time - lastReport < 0.75f) return;
            lastReport = Time.time;
            var ss = SuspicionSystem.Instance;
            if (ss)
            {
                ss.ReportDirect(ViolationType.Trespassing, host.ChestPosition, host, SuspicionRules.Max, source);
                ss.AlertGuards(host.transform.position);
            }
            if (!first) return;
            BurnCount++;
            LastBurned = host;
            Sfx.Play(SfxId.AlarmSiren, host.transform.position, 0.8f);
            Missions.MissionManager.Say(("HUNTER", "Positive read! Parasite in that body — take it down!"));
            GameEvents.RaiseToast($"BODY BURNED — the hunters know {host.DisplayName}. Leave it [Q] or hop to another body to break the trail.", Palette.Danger);
        }

        /// <summary>Reads fade when nothing feeds them; a body the player left carries no live signature.</summary>
        public static void Tick(float dt)
        {
            scratch.Clear();
            foreach (var kv in entries) scratch.Add(kv.Key);
            foreach (var h in scratch)
            {
                if (!h) { entries.Remove(h); continue; }
                var e = entries[h];
                if (h.State != HostState.Possessed) { entries.Remove(h); continue; }
                if (Time.time - e.lastFed < 0.25f) continue;
                e.meter = BioScanRules.Step(e.meter, false, 0f, dt);
                if (e.meter < BioScanRules.AlertAt * 0.5f) e.warned = false;
                if (e.meter <= 0f) entries.Remove(h); else entries[h] = e;
            }
            burned.RemoveWhere(h => !h);
        }
    }
}
