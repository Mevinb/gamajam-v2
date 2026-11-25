using System.Collections.Generic;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Real blob passage with telegraphed pulses and only physically visited safe ledges.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed class TidalCulvert : MonoBehaviour
    {
        public static TidalCulvert Instance { get; private set; }
        public Transform gate;
        public Renderer lamp;
        readonly Dictionary<AmoebaAgent, Vector3> visited = new();
        readonly HashSet<AmoebaAgent> entered = new();
        Material lampMaterial;
        float age, lastWashWarning = -10;
        public float Age => age;
        public TidePhase Phase => TideRules.Phase(age);
        public string StatusLine => $"{(Phase == TidePhase.Open ? "low tide" : Phase == TidePhase.Warning ? "rising water" : "surge — shelter")}, {TideRules.Remaining(age):0.0}s";
        public Vector3 RetryPoint(AmoebaAgent agent) => visited.TryGetValue(agent, out var point) ? point : IslandCampaign.CulvertRetry;

        void Awake() => Instance = this;
        public static bool InsideLowPassage(Vector3 world)
        {
            var p = world - IslandCampaign.Origin;
            return Mathf.Abs(p.x + 26) <= 1.9f && p.y < 1.5f && p.z >= -30.2f && p.z <= -16.0f;
        }
        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm || !gm.IsPlaying) return;
            age = TideRules.Advance(age, Time.deltaTime, true);
            if (gate)
            {
                var p = gate.localPosition;
                p.y = Phase == TidePhase.Surge ? .75f : 2.35f;
                gate.localPosition = p;
            }
            if (lamp)
            {
                if (!lampMaterial) lampMaterial = lamp.material;
                MatFactory.SetEmission(lampMaterial, Phase == TidePhase.Open ? Palette.Amoeba * 1.7f : Phase == TidePhase.Warning ? Palette.Warning * 2f : Palette.Danger * 2f);
            }
            var pm = PossessionManager.Instance;
            if (!pm) return;
            foreach (var agent in pm.Agents)
            {
                if (agent == null || !agent.IsFree || !agent.Blob) continue;
                var p = agent.Position - IslandCampaign.Origin;
                bool atEntry = Mathf.Abs(p.x + 26) < .65f && p.y < 1.2f && p.z > -31.1f && p.z < -29.4f;
                if (atEntry) { entered.Add(agent); visited[agent] = IslandCampaign.CulvertRetry; }
                if (!InsideLowPassage(agent.Position))
                {
                    if (!atEntry) entered.Remove(agent);
                    continue;
                }
                // This ledge is beyond the gate: never arm it until the actual body reaches it.
                bool onDryLedge = p.x > -25.05f && p.x < -24.55f && p.z > -24.5f && p.z < -23.2f;
                if (onDryLedge && entered.Contains(agent)) visited[agent] = IslandCampaign.At(-24.8f, .17f, -23.85f);
                if (TideRules.ReachedExit(p, entered.Contains(agent)))
                {
                    visited[agent] = IslandCampaign.CulvertExit;
                    MissionManager.SetFlag(IslandCampaign.CulvertCrossed);
                    continue;
                }
                if (Phase == TidePhase.Surge && p.z > -29.1f && p.z < -16.8f && !onDryLedge && entered.Contains(agent))
                {
                    Vector3 previous = agent.Blob.transform.position;
                    agent.Blob.Place(RetryPoint(agent));
                    if (PlayerCamera.Instance) PlayerCamera.Instance.NotifyBodyWarp(agent.Blob,agent.Blob.transform.position-previous);
                    if (Time.time - lastWashWarning > 1.5f)
                    {
                        lastWashWarning = Time.time;
                        GameEvents.RaiseToast("Strong current — return to the last dry ledge you reached. Watch the tide lamps.", Palette.Warning);
                    }
                }
            }
        }
        void LateUpdate()
        {
            var camera = PlayerCamera.Instance;
            var pm = PossessionManager.Instance;
            AmoebaController body = null;
            if (pm && pm.Active != null && pm.Active.IsFree)
            {
                var p = pm.Active.Position - IslandCampaign.Origin;
                if (Mathf.Abs(p.x+26)<2.5f && p.y<1.1f && p.z>=-31 && p.z<=-14) body=pm.Active.Blob;
            }
            if (camera) camera.SetLowClearance(body);
        }
        void OnDestroy()
        {
            if (PlayerCamera.Instance) PlayerCamera.Instance.SetLowClearance(null);
            if (lampMaterial) Destroy(lampMaterial);
            if (Instance == this) Instance = null;
        }
    }
}
