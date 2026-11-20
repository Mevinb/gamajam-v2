using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Possession
{
    /// <summary>[F] — one profession-specific action per host type, with cooldowns.</summary>
    public class AbilitySystem : MonoBehaviour
    {
        public static AbilitySystem Instance { get; private set; }
        readonly Dictionary<Profession, float> readyAt = new();

        public static (string name, string desc, float cooldown) Info(Profession p) => p switch
        {
            Profession.Civilian => ("Throw a distraction", "Shout where you aim — nearby guards go and look.", 16f),
            Profession.Guard => ("Radio dispatch", "Send every other guard within 60 m to the spot you aim at.", 28f),
            Profession.Scientist => ("Professional calm", "\"Nothing to see here.\" Lowers suspicion by 30 (not during pursuit).", 30f),
            _ => ("Cut the power", "Disables security cameras within 25 m for 20 s.", 35f),
        };

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public float CooldownRemaining(Profession p) => readyAt.TryGetValue(p, out var t) ? Mathf.Max(0f, t - Time.time) : 0f;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;
            if (!GameInput.AbilityPressed) return;
            var pm = PossessionManager.Instance;
            var host = pm != null && pm.Active != null ? pm.Active.Host : null;
            if (!host || host.State != HostState.Possessed) { GameEvents.RaiseToast("Abilities need a host. Possess someone first.", Palette.Warning); return; }
            TryUse(host);
        }

        public bool TryUse(HostCharacter host)
        {
            var (name, _, cd) = Info(host.Profession);
            float left = CooldownRemaining(host.Profession);
            if (left > 0f) { GameEvents.RaiseToast($"{name} ready in {left:0}s", Palette.Warning); Sfx.Ui(SfxId.Denied, 0.4f); return false; }
            bool ok = host.Profession switch
            {
                Profession.Civilian => Distraction(host, 30f, false),
                Profession.Guard => Distraction(host, 60f, true),
                Profession.Scientist => Calm(),
                _ => CutPower(host),
            };
            if (ok) readyAt[host.Profession] = Time.time + cd;
            return ok;
        }

        static Vector3 AimPoint(HostCharacter host, float maxDist)
        {
            var cam = PlayerCamera.Instance;
            Vector3 o = cam ? cam.Position : host.EyePosition, d = cam ? cam.Forward : host.transform.forward;
            if (Physics.Raycast(o, d, out var hit, maxDist, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)) return hit.point;
            var p = o + d * maxDist;
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out hit, 60f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)) return hit.point;
            return host.transform.position + host.transform.forward * 10f;
        }

        static bool Distraction(HostCharacter host, float radius, bool radio)
        {
            Vector3 p = AimPoint(host, 35f);
            int n = 0;
            foreach (var h in HostCharacter.All)
            {
                if (!h || h == host || !h.IsAwakeNpc) continue;
                if ((h.transform.position - (radio ? host.transform.position : p)).sqrMagnitude > radius * radius) continue;
                if (h.Brain is GuardAI g) { g.OrderInvestigate(p); n++; }
                else if (!radio && h.Brain) h.Brain.Notice(p, 3f, '?');
            }
            Fx.Splat(p);
            Sfx.Play(radio ? SfxId.TerminalBeep : SfxId.AlertCurious, radio ? host.transform.position : p);
            GameEvents.RaiseToast(radio ? $"Radio: \"Suspicious activity, check it out.\" {n} guard(s) dispatched." : $"\"HEY! Over there!\" {n} guard(s) going to look.", Palette.Info);
            return true;
        }

        static bool Calm()
        {
            var ss = SuspicionSystem.Instance;
            if (ss == null || !ss.Reassure(30f)) { GameEvents.RaiseToast("Too late for calm words — you're being chased.", Palette.Danger); return false; }
            GameEvents.RaiseToast("\"Routine lab business, nothing to see.\" Suspicion lowered.", Palette.Amoeba);
            Sfx.Ui(SfxId.TerminalBeep, 0.6f);
            return true;
        }

        static bool CutPower(HostCharacter host)
        {
            int n = 0;
            foreach (var c in SecurityCamera.All)
                if (c && (c.transform.position - host.transform.position).sqrMagnitude < 25f * 25f) { c.DisableFor(20f); n++; }
            if (n == 0) { GameEvents.RaiseToast("No security cameras within 25 m.", Palette.Warning); return false; }
            GameEvents.RaiseToast($"Power cut — {n} camera(s) offline for 20 s.", Palette.Amoeba);
            return true;
        }
    }
}
