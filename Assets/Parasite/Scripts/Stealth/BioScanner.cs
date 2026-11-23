using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Stealth
{
    /// <summary>
    /// Protocol Hound handheld bio-scanner carried by a hunter: a visible cone in front of the hunter that reads any
    /// possessed body inside it (line of sight required), filling <see cref="BioScanTracker"/>. Free amoebas are
    /// invisible to it — but not to the hunter's own eyes.
    /// </summary>
    public class BioScanner : MonoBehaviour
    {
        public static readonly List<BioScanner> All = new();
        public float range = BioScanRules.Range, halfAngle = BioScanRules.HalfAngle;
        public bool Active { get; private set; } = true;
        public bool Scanning { get; private set; }
        public HostCharacter Owner { get; private set; }
        public Vector3 Origin => Owner ? Owner.transform.position + Vector3.up * 1.25f : transform.position;
        public Vector3 Forward => Owner ? Owner.transform.forward : transform.forward;

        MeshRenderer cone, device;
        float scanT, humT;
        readonly RaycastHit[] hits = new RaycastHit[12];
        static Mesh coneMesh;
        static Material idleMat, hotMat, deviceMat;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(HostCharacter owner)
        {
            Owner = owner;
            var holder = new GameObject("BioScanner").transform;
            holder.SetParent(owner.transform, false);
            holder.localPosition = new Vector3(0.22f, 1.18f, 0.32f);
            holder.localRotation = Quaternion.Euler(6f, 0f, 0f);
            if (!deviceMat) deviceMat = MatFactory.Emissive(new Color(0.05f, 0.08f, 0.08f), new Color(0.2f, 1.6f, 1.3f));
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(d.GetComponent<Collider>());
            d.name = "ScannerDevice";
            d.transform.SetParent(holder, false);
            d.transform.localScale = new Vector3(0.09f, 0.07f, 0.22f);
            device = d.GetComponent<MeshRenderer>();
            device.sharedMaterial = deviceMat;
            device.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = new GameObject("ScannerCone");
            c.transform.SetParent(holder, false);
            c.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            c.AddComponent<MeshFilter>().sharedMesh = ConeMesh(range, halfAngle);
            cone = c.AddComponent<MeshRenderer>();
            if (!idleMat) idleMat = MatFactory.Transparent(new Color(0.25f, 1f, 0.85f, 0.09f));
            if (!hotMat) hotMat = MatFactory.Transparent(new Color(1f, 0.2f, 0.12f, 0.16f));
            cone.sharedMaterial = idleMat;
            cone.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cone.receiveShadows = false;
        }

        public void SetActive(bool on)
        {
            Active = on;
            if (!on) Scanning = false;
            if (cone) cone.enabled = on;
            if (device) device.enabled = on;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            bool live = Active && Owner && Owner.IsAwakeNpc;
            if (cone && cone.enabled != live) cone.enabled = live;
            if (!live) { Scanning = false; return; }
            scanT -= Time.deltaTime;
            humT -= Time.deltaTime;
            if (scanT > 0f) return;
            const float interval = 0.1f;
            scanT = interval;
            bool any = false;
            var pm = PossessionManager.Instance;
            if (pm != null)
                foreach (var a in pm.Agents)
                {
                    if (a == null || a.InTransit || a.Host == null) continue;
                    var h = a.Host;
                    if (!BioScanRules.Detectable(h.State, false)) continue;
                    Vector3 p = h.ChestPosition;
                    if (!BioScanRules.InCone(Origin, Forward, p, range, halfAngle) || !Clear(p, h.transform)) continue;
                    any = true;
                    BioScanTracker.Feed(h, BioScanRules.ScanRate(Vector3.Distance(Origin, p), range) * interval, Owner.DisplayName + "'s bio-scanner");
                }
            if (any && !Scanning && humT <= 0f) { humT = 1.5f; Sfx.Play(SfxId.LaserHum, Origin, 0.55f); }
            Scanning = any;
            if (cone) cone.sharedMaterial = any ? hotMat : idleMat;
        }

        bool Clear(Vector3 p, Transform target)
        {
            Vector3 o = Origin, d = p - o;
            float dist = d.magnitude;
            if (dist < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(o, d / dist, hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].transform;
                if ((Owner && t.IsChildOf(Owner.transform)) || t.IsChildOf(target)) continue;
                int l = t.gameObject.layer;
                if (l == Layers.Characters || l == Layers.Amoeba) continue;
                return false;
            }
            return true;
        }

        static Mesh ConeMesh(float length, float halfAngleDeg)
        {
            if (coneMesh) return coneMesh;
            const int seg = 20;
            float r = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad) * length;
            var v = new Vector3[seg + 1];
            var t = new int[seg * 6];
            v[0] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.45f, length);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i + 1, b = (i + 1) % seg + 1;
                t[i * 6] = 0; t[i * 6 + 1] = a; t[i * 6 + 2] = b;       // outside
                t[i * 6 + 3] = 0; t[i * 6 + 4] = b; t[i * 6 + 5] = a;   // inside (double-sided)
            }
            coneMesh = new Mesh { name = "BioScannerCone", vertices = v, triangles = t };
            coneMesh.RecalculateNormals();
            coneMesh.RecalculateBounds();
            return coneMesh;
        }
    }
}
