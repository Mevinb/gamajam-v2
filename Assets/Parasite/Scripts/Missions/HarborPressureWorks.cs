using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>Persistent valve positions, transient two-second balance and a real moving collision gate.</summary>
    public sealed class HarborPressureWorks : MonoBehaviour
    {
        public static HarborPressureWorks Instance { get; private set; }
        public const int TargetPressure = 4, MaximumPressure = 4;
        public const float RequiredBalanceSeconds = 2f;
        public Transform gate, gaugeNeedle; public Renderer lamp;
        public Vector3 openOffset = new(0, 4, 0);
        public float openSeconds = 1.2f;
        public int ValveMask { get; private set; }
        public int Pressure => Mathf.Max(0, ((ValveMask & 1) != 0 ? 3 : 0) + ((ValveMask & 2) != 0 ? 2 : 0) - ((ValveMask & 4) != 0 ? 1 : 0));
        public float BalancedSeconds { get; private set; }
        public float GateOpenFraction { get; private set; }
        public int OverflowCount { get; private set; }
        public bool Configured { get; private set; }
        public string StatusLine => RunState.Has(HarborCampaign.CanalOpen) ? "4 / 4 · lock released" : Pressure + " / 4 · stable " + BalancedSeconds.ToString("0.0") + " / 2.0 s";
        Vector3 closed; Quaternion needleRest; NavMeshObstacle obstacle; MaterialPropertyBlock block;
        void Awake() => Instance = this;
        public void Configure(Transform liftGate, Transform needle, Renderer statusLamp)
        {
            bool sameGate = Configured && gate == liftGate, sameNeedle = Configured && gaugeNeedle == needle;
            gate = liftGate; gaugeNeedle = needle; lamp = statusLamp;
            if (!gate || !gate.GetComponent<Collider>()) throw new System.ArgumentException("Harbor lock requires its original physical gate collider.");
            if (!sameGate) closed = gate.localPosition;
            if (!sameNeedle) needleRest = gaugeNeedle ? gaugeNeedle.localRotation : Quaternion.identity;
            obstacle = gate.GetComponent<NavMeshObstacle>(); Configured = true; RestorePositions(); Apply();
        }
        void Start() { if (!Configured && gate) Configure(gate, gaugeNeedle, lamp); }
        void RestorePositions()
        {
            ValveMask = 0; for (int i = 0; i < 3; i++) if (RunState.Has(HarborCampaign.ValveFlag(i))) ValveMask |= 1 << i;
            if (Pressure > MaximumPressure) { ValveMask = 0; for (int i = 0; i < 3; i++) RunState.Flags.Remove(HarborCampaign.ValveFlag(i)); }
            bool completed = RunState.Has(HarborCampaign.CanalOpen);
            if (completed) ValveMask = 7;
            BalancedSeconds = completed ? RequiredBalanceSeconds : 0; GateOpenFraction = completed ? 1 : 0;
        }
        public bool IsValveOpen(int slot) => slot >= 0 && slot < 3 && (ValveMask & (1 << slot)) != 0;
        public bool CanToggle(in InteractContext ctx, int slot, out string reason)
        {
            reason = null;
            if (!Configured || slot < 0 || slot > 2) { reason = "The pressure works are unavailable."; return false; }
            if (RunState.Has(HarborCampaign.CanalOpen)) { reason = "The lock pressure is already secured."; return false; }
            return HarborCampaign.ReadyHost(ctx, Profession.Maintenance, HarborLayout.World(HarborLayout.ValveLocal[slot]) + Vector3.up * 1.1f, 2.3f, out reason);
        }
        public bool TryToggle(in InteractContext ctx, int slot, out string reason)
        {
            if (!CanToggle(ctx, slot, out reason)) return false;
            ValveMask ^= 1 << slot; BalancedSeconds = 0;
            if (Pressure > MaximumPressure)
            {
                OverflowCount++; ValveMask = 0;
                for (int i = 0; i < 3; i++) RunState.Flags.Remove(HarborCampaign.ValveFlag(i));
                GameEvents.RaiseToast("Pressure exceeded 4. Relief reset A, B and C safely. Open bleed C before adding the final pump, then retry.", Palette.Warning);
            }
            else
            {
                if (IsValveOpen(slot)) MissionManager.SetFlag(HarborCampaign.ValveFlag(slot)); else RunState.Flags.Remove(HarborCampaign.ValveFlag(slot));
                GameEvents.RaiseToast("Valve " + (slot == 0 ? "A" : slot == 1 ? "B" : "C") + (IsValveOpen(slot) ? " open. " : " closed. ") + StatusLine, Palette.Info);
            }
            Apply(); return true;
        }
        void Update()
        {
            if (!Configured || !HarborCampaign.Active || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            if (!RunState.Has(HarborCampaign.CanalOpen))
            {
                BalancedSeconds = Pressure == TargetPressure ? Mathf.Min(RequiredBalanceSeconds, BalancedSeconds + Time.deltaTime) : 0;
                if (BalancedSeconds >= RequiredBalanceSeconds) MissionManager.SetFlag(HarborCampaign.CanalOpen);
            }
            GateOpenFraction = Mathf.MoveTowards(GateOpenFraction, RunState.Has(HarborCampaign.CanalOpen) ? 1 : 0, Time.deltaTime / Mathf.Max(.1f, openSeconds));
            Apply();
        }
        void Apply()
        {
            if (gate) gate.localPosition = closed + openOffset * Mathf.SmoothStep(0, 1, GateOpenFraction);
            if (obstacle) obstacle.enabled = GateOpenFraction < .95f;
            if (gaugeNeedle) gaugeNeedle.localRotation = needleRest * Quaternion.Euler(0, 0, Mathf.Lerp(70, -70, Pressure / 4f));
            if (lamp)
            {
                block ??= new MaterialPropertyBlock(); lamp.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", (RunState.Has(HarborCampaign.CanalOpen) ? Palette.Amoeba : Pressure == 4 ? Palette.Info : Palette.Warning) * 1.5f); lamp.SetPropertyBlock(block);
            }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
