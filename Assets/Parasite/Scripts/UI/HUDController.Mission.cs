using System.Collections.Generic;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>Mission banner (title card on mission start) and screen-edge-clamped waypoint markers with distance.</summary>
    public partial class HUDController
    {
        RectTransform bannerRoot;
        CanvasGroup bannerGroup;
        Text bannerTitle, bannerSub;
        float bannerT;
        string pendingTitle, pendingSub;

        class Waypoint { public RectTransform rt; public Text label, dist; public Image icon, arrow; public bool on; }
        readonly List<Waypoint> waypointPool = new();
        RectTransform waypointLayer;

        void BuildMissionUI(Transform gameplayRoot, Transform root)
        {
            waypointLayer = UIKit.Rect("Waypoints", gameplayRoot).Stretch();
            BuildCompass(gameplayRoot);
            bannerRoot = UIKit.Rect("MissionBanner", root).Place(new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 150));
            bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f; bannerGroup.blocksRaycasts = false;
            var bg = UIKit.Img(bannerRoot, "Bg", UIKit.GradientH, new Color(0, 0, 0, 0.55f));
            bg.rectTransform.Stretch();
            var line = UIKit.Img(bannerRoot, "Line", UIKit.White, UIKit.Accent);
            line.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(760, 2));
            bannerTitle = UIKit.Txt(bannerRoot, "Title", "", 46, UIKit.Accent, TextAnchor.LowerCenter, Weight.Bold);
            bannerTitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(1080, 60));
            UIKit.AddShadow(bannerTitle, 2f, 0.8f);
            bannerSub = UIKit.Txt(bannerRoot, "Sub", "", 26, UIKit.TextMain, TextAnchor.UpperCenter, Weight.Medium);
            bannerSub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(1000, 70));
            MissionManager.Banner += OnBanner;
            var mm = MissionManager.Instance;
            if (mm && mm.Current != null) OnBanner($"MISSION {RunState.MissionIndex + 1}/{mm.Missions.Count} · {mm.Current.title}", mm.Current.briefing);
        }

        void OnBanner(string title, string sub) { pendingTitle = title; pendingSub = sub; }

        void ClearMissionBanner()
        {
            pendingTitle = pendingSub = null;
            bannerT = 0f;
            if (bannerGroup) bannerGroup.alpha = 0f;
        }

        void TickMission(float dt)
        {
            TickCompass();
            if (endOpen || state == GameState.Ended)
            {
                ClearMissionBanner();
                return;
            }
            if (pendingTitle != null && state == GameState.Playing && !subActive)
            {
                bannerTitle.text = UIKit.Clean(pendingTitle);
                bannerSub.text = UIKit.Clean(pendingSub);
                pendingTitle = null;
                bannerT = 5.5f;
            }
            if (bannerT > 0f) bannerT -= dt;
            float target = state != GameState.Playing || subActive ? 0f : bannerT > 0.6f ? 1f : bannerT > 0f ? bannerT / 0.6f : 0f;
            bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, target, dt * 3f);
        }

        Waypoint NewWaypoint()
        {
            var w = new Waypoint { rt = UIKit.Rect("Waypoint", waypointLayer) };
            w.rt.anchorMin = w.rt.anchorMax = Vector2.zero;
            w.rt.pivot = new Vector2(0.5f, 0.5f);
            w.rt.sizeDelta = new Vector2(44, 44);
            var ring = UIKit.Img(w.rt, "Bg", UIKit.Circle, new Color(0.01f, 0.03f, 0.02f, 0.7f));
            ring.rectTransform.Stretch();
            w.icon = UIKit.Img(w.rt, "Ring", UIKit.Ring(3f), UIKit.Yellow);
            w.icon.rectTransform.Stretch();
            var dot = UIKit.Img(w.rt, "Dot", UIKit.Circle, UIKit.Yellow);
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
            w.arrow = UIKit.Img(w.rt, "Arrow", UIKit.Chevron, UIKit.Yellow);
            w.arrow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(18, 12));
            w.label = UIKit.Txt(w.rt, "Label", "", 17, UIKit.Yellow, TextAnchor.LowerCenter, Weight.Bold);
            w.label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 4), new Vector2(260, 22));
            UIKit.AddShadow(w.label);
            w.dist = UIKit.Txt(w.rt, "Dist", "", 15, UIKit.TextMain, TextAnchor.UpperCenter, Weight.Semi);
            w.dist.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(160, 20));
            UIKit.AddShadow(w.dist);
            waypointPool.Add(w);
            return w;
        }

        void ProjectWaypoints(Camera cam)
        {
            if (!waypointLayer) return;
            var mm = MissionManager.Instance;
            int used = 0;
            if (mm && cam && state == GameState.Playing)
            {
                var pm = Possession.PossessionManager.Instance;
                Vector3 from = pm != null && pm.ActiveBody != null ? pm.ActiveBody.Root.position : cam.transform.position;
                float scale = canvas ? canvas.scaleFactor : 1f;
                float W = Screen.width, H = Screen.height, m = 60f * scale;
                foreach (var (pos, label) in mm.Waypoints)
                {
                    var w = used < waypointPool.Count ? waypointPool[used] : NewWaypoint();
                    used++;
                    if (!w.on) { w.on = true; w.rt.gameObject.SetActive(true); }
                    Vector3 world = pos + Vector3.up * 0.6f;
                    Vector3 sp = cam.WorldToScreenPoint(world);
                    bool behind = sp.z < 0f;
                    if (behind) { sp.x = W - sp.x; sp.y = H - sp.y; }
                    bool off = behind || sp.x < m || sp.x > W - m || sp.y < m || sp.y > H - m;
                    Vector2 p = new(sp.x, sp.y);
                    if (off)
                    {
                        Vector2 c = new(W * 0.5f, H * 0.5f), d = p - c;
                        if (behind && d.sqrMagnitude < 1f) d = Vector2.down;
                        float k = Mathf.Min((W * 0.5f - m) / Mathf.Max(1e-3f, Mathf.Abs(d.x)), (H * 0.5f - m) / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
                        p = c + d * Mathf.Min(k, behind ? k : 1f);
                        w.arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f);
                        w.arrow.rectTransform.anchoredPosition = Vector2.zero;
                        w.arrow.rectTransform.localPosition = (Vector3)(d.normalized * 30f);
                    }
                    else
                    {
                        w.arrow.rectTransform.localRotation = Quaternion.identity;
                        w.arrow.rectTransform.localPosition = new Vector3(0, -32, 0);
                    }
                    w.rt.position = new Vector3(p.x, p.y, 0f);
                    float dist = Vector3.Distance(from, pos);
                    w.label.text = label;
                    w.dist.text = dist < 1000f ? $"{dist:0} m" : "";
                    float s = off ? 0.85f : Mathf.Clamp(1.2f - dist / 200f, 0.75f, 1.15f);
                    w.rt.localScale = new Vector3(s, s, 1f);
                    var col = Color.Lerp(UIKit.Yellow, Color.white, 0.25f * (1f + Mathf.Sin(now * 4f)));
                    w.icon.color = col;
                }
            }
            for (int i = used; i < waypointPool.Count; i++)
                if (waypointPool[i].on) { waypointPool[i].on = false; waypointPool[i].rt.gameObject.SetActive(false); }
        }

        // ------------------------------------------------------------ compass
        const float CompassW = 620f, CompassSpan = 140f;
        RectTransform compassRoot;
        readonly List<(RectTransform rt, float heading)> compassMarks = new();
        readonly List<Image> compassPins = new();

        void BuildCompass(Transform p)
        {
            compassRoot = UIKit.Rect("Compass", p).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(CompassW, 30));
            compassRoot.gameObject.AddComponent<RectMask2D>();
            var bg = UIKit.Img(compassRoot, "Bg", UIKit.GradientH, new Color(0, 0, 0, 0.0f));
            bg.rectTransform.Stretch();
            var line = UIKit.Img(compassRoot, "Line", UIKit.White, UIKit.AccentFaint);
            line.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(CompassW, 1.5f));
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            for (int i = 0; i < 8; i++)
            {
                var t = UIKit.Txt(compassRoot, names[i], names[i], i % 2 == 0 ? 20 : 15, i == 0 ? UIKit.Accent : UIKit.TextDim, TextAnchor.MiddleCenter, Weight.Bold);
                t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 26));
                compassMarks.Add((t.rectTransform, i * 45f));
            }
            for (int i = 0; i < 24; i++)
            {
                if (i % 3 == 0) continue;
                var tick = UIKit.Img(compassRoot, "Tick", UIKit.White, UIKit.AccentSoft);
                tick.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1.5f, 7f));
                compassMarks.Add((tick.rectTransform, i * 15f));
            }
            var centre = UIKit.Img(compassRoot, "Centre", UIKit.Chevron, UIKit.Accent);
            centre.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(12, 8));
            centre.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
        }

        void TickCompass()
        {
            if (!compassRoot) return;
            var pc = Possession.PlayerCamera.Instance;
            if (!pc) return;
            float yaw = pc.Yaw;
            foreach (var (rt, h) in compassMarks)
            {
                float d = Mathf.DeltaAngle(yaw, h);
                bool vis = Mathf.Abs(d) < CompassSpan * 0.5f;
                if (rt.gameObject.activeSelf != vis) rt.gameObject.SetActive(vis);
                if (vis) rt.anchoredPosition = new Vector2(d / CompassSpan * CompassW, rt.anchoredPosition.y);
            }
            var mm = MissionManager.Instance;
            var pm = Possession.PossessionManager.Instance;
            int used = 0;
            if (mm && pm != null && pm.ActiveBody != null)
            {
                Vector3 from = pm.ActiveBody.Root.position;
                foreach (var (pos, _) in mm.Waypoints)
                {
                    Vector3 dir = pos - from; dir.y = 0;
                    if (dir.sqrMagnitude < 0.5f) continue;
                    float h = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float d = Mathf.Clamp(Mathf.DeltaAngle(yaw, h), -CompassSpan * 0.5f + 4f, CompassSpan * 0.5f - 4f);
                    Image pin;
                    if (used < compassPins.Count) pin = compassPins[used];
                    else
                    {
                        pin = UIKit.Img(compassRoot, "Pin", UIKit.Circle, UIKit.Yellow);
                        pin.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11, 11));
                        compassPins.Add(pin);
                    }
                    used++;
                    pin.gameObject.SetActive(true);
                    pin.rectTransform.anchoredPosition = new Vector2(d / CompassSpan * CompassW, -9f);
                }
            }
            for (int i = used; i < compassPins.Count; i++) compassPins[i].gameObject.SetActive(false);
        }

        void CleanupMissionUI() { MissionManager.Banner -= OnBanner; }
    }
}
