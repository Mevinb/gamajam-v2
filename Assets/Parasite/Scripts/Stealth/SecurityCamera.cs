using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using Parasite.World;
using UnityEngine;

namespace Parasite.Stealth
{
    /// <summary>
    /// Sweeping CCTV camera. Sees free amoebas, trespassing hosts (restricted zones) and unconscious bodies inside its
    /// cone; reports to the SuspicionSystem and calls nearby guards. A maintenance host can disable it for a while.
    /// </summary>
    public class SecurityCamera : MonoBehaviour
    {
        public static readonly List<SecurityCamera> All = new();
        public string cameraName = "CCTV";
        public float sweepDegrees = 45f, sweepSpeed = 0.35f, fov = 46f, range = 15f, pitchDown = 28f;
        public Transform head;
        public Renderer lamp;
        public Renderer cone;

        public bool Disabled => Time.time < disabledUntil;
        public bool SeesSomething { get; private set; }
        float disabledUntil, baseYaw, scanT, phase;
        readonly RaycastHit[] hits = new RaycastHit[12];
        Material lampMat, coneMat;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (!head) head = transform;
            baseYaw = head.localEulerAngles.y;
            phase = Random.value * 6f;
            if (lamp) { lampMat = lamp.material; lampMat.EnableKeyword("_EMISSION"); }
            if (cone) coneMat = cone.material;
        }

        public void DisableFor(float seconds)
        {
            disabledUntil = Time.time + seconds;
            Sfx.Play(SfxId.Release, transform.position, 0.6f);
        }

        Vector3 Eye => head.position;
        Vector3 Forward => head.forward;

        void Update()
        {
            float t = Time.time;
            bool off = Disabled;
            if (!off) head.localRotation = Quaternion.Euler(pitchDown, baseYaw + Mathf.Sin(t * sweepSpeed + phase) * sweepDegrees, 0f);
            if (lampMat)
            {
                Color c = off ? Color.black : SeesSomething ? new Color(1f, 0.1f, 0.05f) * (2f + 2f * Mathf.Sin(t * 20f)) : new Color(1f, 0.1f, 0.05f) * (Mathf.Repeat(t, 1.2f) < 0.15f ? 3f : 0.3f);
                lampMat.SetColor("_EmissionColor", c);
            }
            if (cone) cone.enabled = !off;
            if (coneMat) coneMat.SetColor("_BaseColor", SeesSomething ? new Color(1f, 0.15f, 0.1f, 0.16f) : new Color(1f, 0.85f, 0.5f, 0.05f));
            if (off) { SeesSomething = false; return; }

            scanT -= Time.deltaTime;
            if (scanT > 0f) return;
            const float interval = 0.2f;
            scanT = interval;
            Scan(interval);
        }

        bool InView(Vector3 p, Transform target)
        {
            Vector3 d = p - Eye; float dist = d.magnitude;
            if (dist > range || Vector3.Angle(Forward, d) > fov * 0.5f) return false;
            int n = Physics.RaycastNonAlloc(Eye, d / dist, hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var tr = hits[i].transform;
                if (tr.IsChildOf(transform) || (target && tr.IsChildOf(target))) continue;
                int l = tr.gameObject.layer;
                if (l == Layers.Characters || l == Layers.Amoeba) continue;
                return false;
            }
            return true;
        }

        void Scan(float dt)
        {
            var ss = SuspicionSystem.Instance;
            var pm = PossessionManager.Instance;
            SeesSomething = false;
            if (ss == null || pm == null) return;
            foreach (var a in pm.Agents)
            {
                if (a.InTransit) continue;
                if (a.Host == null && a.Blob && a.Blob.gameObject.activeInHierarchy && InView(a.Blob.Center, a.Blob.transform))
                {
                    SeesSomething = true;
                    ss.ReportDirect(ViolationType.BlobSighted, a.Blob.Center, a.Blob, SuspicionRules.BaseAmount(ViolationType.BlobSighted) * 1.4f * dt, cameraName);
                    ss.AlertGuards(a.Blob.transform.position);
                }
                else if (a.Host != null && a.Host.State == HostState.Possessed)
                {
                    var z = RestrictedZone.At(a.Host.transform.position);
                    if (z && !AccessRules.Has(a.Host.Permissions, z.required) && InView(a.Host.ChestPosition, a.Host.transform))
                    {
                        SeesSomething = true;
                        ss.ReportDirect(ViolationType.Trespassing, a.Host.ChestPosition, a.Host, SuspicionRules.BaseAmount(ViolationType.Trespassing) * dt, cameraName);
                    }
                }
            }
            foreach (var h in HostCharacter.All)
            {
                if (!h || h.State != HostState.Unconscious || h.KnockoutDiscovered) continue;
                if (!InView(h.transform.position + Vector3.up * 0.3f, h.transform)) continue;
                h.KnockoutDiscovered = true;
                SeesSomething = true;
                ss.ReportDirect(ViolationType.UnconsciousHostFound, h.transform.position, null, SuspicionRules.BaseAmount(ViolationType.UnconsciousHostFound), cameraName);
                ss.AlertGuards(h.transform.position);
            }
        }

        void OnDrawGizmos()
        {
            var h = head ? head : transform;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(h.position, h.forward * range);
        }
    }
}
