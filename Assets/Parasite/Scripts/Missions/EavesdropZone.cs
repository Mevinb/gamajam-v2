using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Stay within <see cref="radius"/> as a host (not in pursuit) for <see cref="seconds"/> to overhear a conversation.</summary>
    public class EavesdropZone : MonoBehaviour
    {
        public static EavesdropZone Active { get; private set; }
        public string flag = "eavesdropped";
        public string requiredFlag = "";
        public float radius = 4.5f;
        public float seconds = 7f;
        [TextArea] public string[] lines = { };
        public string speaker = "";

        public float Progress01 => Mathf.Clamp01(t / Mathf.Max(0.1f, seconds));
        public bool Listening { get; private set; }
        float t;
        int lineShown = -1;

        void Update()
        {
            Listening = false;
            if (RunState.Has(flag) || (!string.IsNullOrEmpty(requiredFlag) && !RunState.Has(requiredFlag))) { if (Active == this) Active = null; return; }
            var pm = Possession.PossessionManager.Instance;
            var a = pm != null ? pm.Active : null;
            bool ok = a != null && a.Host != null && a.Host.State == HostState.Possessed &&
                      (a.Host.transform.position - transform.position).sqrMagnitude < radius * radius &&
                      (Stealth.SuspicionSystem.Instance == null || Stealth.SuspicionSystem.Instance.Level < SuspicionLevel.Pursuing);
            if (!ok)
            {
                if (Active == this) { Active = null; if (lineShown >= 0) GameEvents.RaiseSubtitle("", ""); }
                t = Mathf.Max(0f, t - Time.deltaTime * 0.5f);
                lineShown = -1;
                return;
            }
            Active = this; Listening = true;
            t += Time.deltaTime;
            int li = lines.Length == 0 ? -1 : Mathf.Min(lines.Length - 1, (int)(Progress01 * lines.Length));
            if (li != lineShown && li >= 0) { lineShown = li; GameEvents.RaiseSubtitle(speaker, lines[li]); }
            if (t >= seconds)
            {
                Active = null;
                MissionManager.SetFlag(flag);
                StartCoroutine(ClearLater());
            }
        }

        System.Collections.IEnumerator ClearLater() { yield return new WaitForSeconds(3.5f); GameEvents.RaiseSubtitle("", ""); }

        void OnDrawGizmos() { Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.6f); Gizmos.DrawWireSphere(transform.position, radius); }
    }
}
