using System.Collections.Generic;
using Parasite.FX;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Stealth
{
    /// <summary>
    /// Protocol Hound doorway arch: any POSSESSED body inside its box is burned instantly; a free amoeba slips through
    /// unread (the arch only hums). Live between <see cref="requiredFlag"/> and <see cref="retireFlag"/>.
    /// </summary>
    public class ScanGate : MonoBehaviour
    {
        public static readonly List<ScanGate> All = new();
        public string gateName = "Security doorway scanner";
        public Vector3 size = new(2.6f, 3f, 2.2f);       // box above transform.position (bottom centre)
        public string requiredFlag = "", retireFlag = "", blobPassFlag = "";
        public Renderer[] beams = new Renderer[0];

        public bool Live => (string.IsNullOrEmpty(requiredFlag) || RunState.Has(requiredFlag)) &&
                            (string.IsNullOrEmpty(retireFlag) || !RunState.Has(retireFlag));
        public bool BlobInside { get; private set; }
        public int Burns { get; private set; }

        float scanT, alarmT, humT;
        bool blobLineSaid;
        int shown = -1;
        static Material liveMat, alarmMat, offMat;

        static readonly (string who, string line)[] BlobLine =
        {
            ("SPECIMEN 7", "No body, no signature. Their arch can't read me like this."),
        };

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 p)
        {
            Vector3 c = transform.position + Vector3.up * (size.y * 0.5f);
            Vector3 d = Quaternion.Inverse(transform.rotation) * (p - c);
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f && Mathf.Abs(d.z) <= size.z * 0.5f;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            bool live = Live;
            alarmT -= Time.deltaTime; humT -= Time.deltaTime;
            Show(!live ? 0 : alarmT > 0f && Mathf.Repeat(Time.time, 0.3f) < 0.18f ? 2 : 1);
            if (!live) { BlobInside = false; return; }
            scanT -= Time.deltaTime;
            if (scanT > 0f) return;
            scanT = 0.05f;
            var pm = PossessionManager.Instance;
            bool blob = false;
            if (pm != null)
                foreach (var a in pm.Agents)
                {
                    if (a == null || a.InTransit) continue;
                    if (a.Host != null)
                    {
                        if (!BioScanRules.Detectable(a.Host.State, false) || !Contains(a.Host.ChestPosition - Vector3.up * 0.6f)) continue;
                        bool fresh = !BioScanTracker.IsBurned(a.Host);
                        BioScanTracker.Feed(a.Host, BioScanRules.GateRate * 0.05f, gateName);
                        if (fresh) Burns++;
                        alarmT = 2.5f;
                    }
                    else if (a.Blob && Contains(a.Blob.Center))
                    {
                        blob = true;
                        if (humT <= 0f) { humT = 2f; Sfx.Play(SfxId.LaserHum, a.Blob.Center, 0.6f); }
                        if (!blobLineSaid) { blobLineSaid = true; Missions.MissionManager.Say(BlobLine); }
                        if (!string.IsNullOrEmpty(blobPassFlag)) Missions.MissionManager.SetFlag(blobPassFlag);
                    }
                }
            BlobInside = blob;
        }

        void Show(int state)
        {
            if (state == shown) return;
            shown = state;
            if (!liveMat) liveMat = MatFactory.Emissive(new Color(0.05f, 0.25f, 0.22f), new Color(0.25f, 2.2f, 1.8f));
            if (!alarmMat) alarmMat = MatFactory.Emissive(new Color(0.3f, 0.03f, 0.02f), new Color(3f, 0.25f, 0.15f));
            if (!offMat) offMat = MatFactory.Lit(new Color(0.08f, 0.09f, 0.1f), 0.6f, 0.4f);
            var m = state == 0 ? offMat : state == 2 ? alarmMat : liveMat;
            foreach (var b in beams) if (b) b.sharedMaterial = m;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * size.y * 0.5f, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
