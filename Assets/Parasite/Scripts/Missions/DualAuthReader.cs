using System.Collections.Generic;
using Parasite.FX;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>
    /// Two-person rule: both readers of a group must be badged by staff hosts within <see cref="window"/> seconds.
    /// Needs the split mechanic: one amoeba per host, Tab between them.
    /// </summary>
    public class DualAuthReader : MonoBehaviour, IInteractable
    {
        static readonly List<DualAuthReader> all = new();
        public string group = "vale_office";
        public string readerName = "Reader A";
        public AccessDoor door;
        public string flag = "office_open";
        public string requiredFlag = "";
        public string requiredFlagReason = "";
        public float window = 8f;
        public float range = 2.2f;
        public Renderer lamp;

        public float ArmedRemaining => Mathf.Max(0f, armedUntil - Time.time);
        float armedUntil = -1f;
        Possession.AmoebaAgent armedBy;

        public Vector3 InteractPoint => transform.position;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && !RunState.Has(flag) && ArmedRemaining <= 0f;

        void OnEnable() { all.Add(this); Interactables.Register(this); }
        void OnDisable() { all.Remove(this); Interactables.Unregister(this); }

        public string GetPrompt(in InteractContext ctx) => $"Badge in — {readerName} (two-person rule)";

        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (ctx.IsBlob) { deniedReason = "A badge reader needs a staff host."; return false; }
            if (!AccessRules.Has(ctx.permissions, AccessPermission.LabInterior)) { deniedReason = "ACCESS DENIED — staff only."; return false; }
            if (!string.IsNullOrEmpty(requiredFlag) && !RunState.Has(requiredFlag)) { deniedReason = requiredFlagReason; return false; }
            foreach (var r in all)
                if (r != this && r.group == group && r.ArmedRemaining > 0f && r.armedBy == ctx.agent)
                { deniedReason = "The same person can't badge both readers. Split (V) and use a second host."; return false; }
            return true;
        }

        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var reason)) { GameEvents.RaiseToast(reason, Palette.Danger); Sfx.Play(SfxId.Denied, InteractPoint); return; }
            armedUntil = Time.time + window;
            armedBy = ctx.agent;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            DualAuthReader partner = null;
            foreach (var r in all) if (r != this && r.group == group && r.ArmedRemaining > 0f) partner = r;
            if (partner)
            {
                MissionManager.SetFlag(flag);
                if (door) { door.Unlock(); door.Open(); }
                GameEvents.RaiseToast("Dual authorization accepted.", Palette.Amoeba);
                armedUntil = partner.armedUntil = -1f;
            }
            else GameEvents.RaiseToast($"{readerName} armed — badge the other reader within {window:0} s (Tab to switch amoeba)", Palette.Info);
        }

        void Update()
        {
            if (!lamp) return;
            var m = lamp.material;
            m.EnableKeyword("_EMISSION");
            Color c = RunState.Has(flag) ? new Color(0.2f, 1f, 0.3f) * 2f :
                      ArmedRemaining > 0f ? new Color(1f, 0.75f, 0.1f) * (1.5f + Mathf.Sin(Time.time * 12f)) :
                      new Color(1f, 0.15f, 0.1f) * 1.5f;
            m.SetColor("_EmissionColor", c);
        }

        public static DualAuthReader ArmedAny()
        {
            foreach (var r in all) if (r && r.ArmedRemaining > 0f) return r;
            return null;
        }
    }
}
