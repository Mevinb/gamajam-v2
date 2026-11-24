using Parasite.Possession;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// Street-lamp light budget. The city builder gives every lamp a real (shadowless) light; this keeps only the
    /// nearest <see cref="Budget"/> lights within <see cref="MaxDistance"/> of the camera enabled and fades them in/out
    /// by distance so there is no popping. Budget/distance come from the graphics preset (Forward+ makes the rest cheap).
    /// </summary>
    public class LampLights : MonoBehaviour
    {
        public Light[] lights = new Light[0];
        public float[] baseIntensity = new float[0];

        /// <summary>Max simultaneously enabled lamp lights (set by <see cref="GraphicsPreset"/>).</summary>
        public static int Budget = 32;
        /// <summary>Lamp lights further than this are off (set by <see cref="GraphicsPreset"/>).</summary>
        public static float MaxDistance = 70f;

        float[] dist, target;
        int[] order;
        float tick;
        DistComparer comparer;

        void Awake() => Allocate();

        void Allocate()
        {
            int n = lights.Length;
            if (dist != null && dist.Length == n) return;
            dist = new float[n]; target = new float[n]; order = new int[n];
            comparer = new DistComparer(dist);
            if (baseIntensity == null || baseIntensity.Length != n)
            {
                baseIntensity = new float[n];
                for (int i = 0; i < n; i++) baseIntensity[i] = lights[i] ? lights[i].intensity : 0f;
            }
        }

        static Camera ActiveCamera()
        {
            if (PlayerCamera.Instance && PlayerCamera.Instance.Cam && PlayerCamera.Instance.Cam.isActiveAndEnabled) return PlayerCamera.Instance.Cam;
            return Camera.main;
        }

        void Update()
        {
            var cam = ActiveCamera();
            if (!cam) return;
            if ((tick -= Time.unscaledDeltaTime) <= 0f) { tick = 0.2f; Select(cam.transform.position); }
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (!l) continue;
                float cur = l.enabled ? l.intensity : 0f;
                float next = Mathf.Lerp(cur, target[i], k);
                if (target[i] <= 0f && next < baseIntensity[i] * 0.02f) { if (l.enabled) l.enabled = false; continue; }
                if (!l.enabled) { l.enabled = true; next = Mathf.Max(next, 0.001f); }
                l.intensity = next;
            }
        }

        /// <summary>Choose the enabled set for a camera position. <paramref name="instant"/> applies it immediately (photos/tests).</summary>
        public void Select(Vector3 camPos, bool instant = false)
        {
            Allocate();
            int n = lights.Length, cnt = 0;
            for (int i = 0; i < n; i++)
            {
                target[i] = 0f;
                if (!lights[i]) continue;
                dist[i] = (lights[i].transform.position - camPos).magnitude;
                if (dist[i] <= MaxDistance) order[cnt++] = i;
            }
            System.Array.Sort(order, 0, cnt, comparer);
            int keep = Mathf.Min(cnt, Mathf.Max(0, Budget));
            for (int j = 0; j < keep; j++)
            {
                int i = order[j];
                float fade = Mathf.Clamp01((MaxDistance - dist[i]) / (MaxDistance * 0.25f));
                // Rank fading is only needed when candidates exceed the budget. A lone nearby lamp must not
                // lose 75% of its intensity simply because there are no other lights in range.
                if (cnt > keep && j > keep - 4) fade *= (keep - j) / 4f;
                target[i] = baseIntensity[i] * fade;
            }
            if (!instant) return;
            for (int i = 0; i < n; i++)
            {
                if (!lights[i]) continue;
                lights[i].enabled = target[i] > 0f;
                lights[i].intensity = target[i];
            }
        }

        sealed class DistComparer : System.Collections.Generic.IComparer<int>
        {
            readonly float[] d;
            public DistComparer(float[] d) { this.d = d; }
            public int Compare(int a, int b) => d[a].CompareTo(d[b]);
        }
    }
}
