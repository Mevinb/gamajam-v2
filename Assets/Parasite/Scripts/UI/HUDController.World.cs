using System.Collections.Generic;
using Parasite.Characters;
using Parasite.Possession;
using UnityEngine;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>World-space-projected markers: amoeba numbers, current target, NPC alert icons, unconscious timers.</summary>
    public partial class HUDController
    {
        const float AlertRange = 45f, UnconsciousRange = 25f;
        static readonly Vector3 Offscreen = new(-10000f, -10000f, 0f);

        class Marker
        {
            public RectTransform rt;
            public GameObject go;
            public Vector3 world;
            public bool on;
            public float scale = 1f;      // base scale (state-dependent)
            public bool distScale;        // shrink with distance
            public float pulse;           // 0 = none
            public Image a, b;            // fill / ring
            public Text t;
            public int style = -1;
        }

        RectTransform worldLayer;
        readonly Marker[] amoebaMarkers = new Marker[SplitRules.MaxAmoebas];
        Marker targetMarker;
        RectTransform targetChevron;
        readonly List<Marker> alertPool = new();
        readonly List<Marker> sleepPool = new();
        int alertUsed, sleepUsed;

        void BuildWorldLayer(Transform p)
        {
            worldLayer = UIKit.Rect("WorldMarkers", p).Stretch();
            // separate canvas so per-frame marker movement doesn't rebatch the rest of the HUD
            worldLayer.gameObject.AddComponent<Canvas>();
            for (int i = 0; i < amoebaMarkers.Length; i++) amoebaMarkers[i] = NewAmoebaMarker(i + 1);
            targetMarker = NewTargetMarker();
        }

        Marker NewMarker(string name, float size)
        {
            var m = new Marker { rt = UIKit.Rect(name, worldLayer) };
            m.rt.anchorMin = m.rt.anchorMax = Vector2.zero;
            m.rt.pivot = new Vector2(0.5f, 0f);
            m.rt.sizeDelta = new Vector2(size, size);
            m.go = m.rt.gameObject;
            m.go.SetActive(false);
            return m;
        }

        Marker NewAmoebaMarker(int id)
        {
            var m = NewMarker("Amoeba" + id, 40f);
            var chev = UIKit.Img(m.rt, "Chevron", UIKit.Chevron, UIKit.Accent);
            chev.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(14, 9));
            m.a = UIKit.Img(m.rt, "Fill", UIKit.Circle, UIKit.Accent);
            m.a.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(34, 34));
            m.b = UIKit.Img(m.a.transform, "Ring", UIKit.Ring(3f), UIKit.Accent);
            m.b.rectTransform.Stretch();
            m.t = UIKit.Txt(m.a.transform, "Num", Numbers[id], 21, Color.black, TextAnchor.MiddleCenter, Weight.Bold);
            m.t.rectTransform.Stretch(0, 1, 0, 0);
            m.distScale = true;
            return m;
        }

        Marker NewTargetMarker()
        {
            var m = NewMarker("Target", 36f);
            m.b = UIKit.Img(m.rt, "Ring", UIKit.Ring(2.5f), UIKit.Accent);
            m.b.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(26, 26));
            m.a = UIKit.Img(m.rt, "Chevron", UIKit.Chevron, UIKit.Accent);
            targetChevron = m.a.rectTransform;
            targetChevron.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(18, 12));
            m.distScale = true;
            return m;
        }

        Marker NewAlertMarker()
        {
            var m = NewMarker("Alert", 38f);
            m.a = UIKit.Img(m.rt, "Bg", UIKit.Circle, new Color(0.02f, 0.02f, 0.02f, 0.65f));
            m.a.rectTransform.Stretch();
            m.b = UIKit.Img(m.rt, "Ring", UIKit.Ring(3f), UIKit.Yellow);
            m.b.rectTransform.Stretch();
            m.t = UIKit.Txt(m.rt, "Icon", "?", 30, UIKit.Yellow, TextAnchor.MiddleCenter, Weight.Bold);
            m.t.rectTransform.Stretch(0, 2, 0, 0);
            m.distScale = true;
            alertPool.Add(m);
            return m;
        }

        Marker NewSleepMarker()
        {
            var m = NewMarker("Unconscious", 34f);
            var bg = UIKit.Img(m.rt, "Bg", UIKit.Circle, new Color(0.02f, 0.02f, 0.02f, 0.6f));
            bg.rectTransform.Stretch();
            var track = UIKit.Img(m.rt, "Track", UIKit.Ring(4f), new Color(1, 1, 1, 0.15f));
            track.rectTransform.Stretch();
            m.a = UIKit.Img(m.rt, "Fill", UIKit.Ring(4f), UIKit.Yellow);
            m.a.rectTransform.Stretch();
            m.a.type = Image.Type.Filled;
            m.a.fillMethod = Image.FillMethod.Radial360;
            m.a.fillOrigin = (int)Image.Origin360.Top;
            m.a.fillClockwise = false;
            m.t = UIKit.Txt(m.rt, "Zz", "Zz", 14, UIKit.TextMain, TextAnchor.MiddleCenter, Weight.Bold);
            m.t.rectTransform.Stretch();
            m.distScale = true;
            sleepPool.Add(m);
            return m;
        }

        static void SetOn(Marker m, bool on)
        {
            if (m.on == on) return;
            m.on = on;
            m.go.SetActive(on);
        }

        Camera WorldCam()
        {
            var pc = PlayerCamera.Instance;
            var cam = pc ? pc.Cam : null;
            return cam ? cam : Camera.main;
        }

        /// <summary>LateUpdate: decide which markers exist and how they look. Positions are set in ProjectWorldMarkers.</summary>
        void UpdateWorldMarkers()
        {
            var pm = PossessionManager.Instance;
            var cam = WorldCam();
            bool visible = cam && pm && (state == GameState.Playing || state == GameState.Paused) && !subActive && !endOpen;
            if (worldLayer.gameObject.activeSelf != visible) worldLayer.gameObject.SetActive(visible);
            if (!visible) return;

            // --- amoeba markers
            var active = pm.Active;
            int count = pm.Agents.Count;
            for (int i = 0; i < amoebaMarkers.Length; i++)
            {
                var m = amoebaMarkers[i];
                AmoebaAgent ag = null;
                for (int j = 0; j < count; j++) if (pm.Agents[j].Id == i + 1) { ag = pm.Agents[j]; break; }
                bool show = ag != null && !ag.InTransit && (ag.Host || ag.Blob);
                SetOn(m, show);
                if (!show) continue;
                m.world = ag.Host ? ag.Host.transform.position + Vector3.up * 2.2f : ag.Blob.Center + Vector3.up * 0.7f;
                bool isActive = ag == active;
                int style = ag.CapturedPendingEject ? 2 : isActive ? 1 : 0;
                if (count < 2) style += 10;           // lone amoeba: keep the marker subtle
                if (style != m.style)
                {
                    m.style = style;
                    int s = style % 10;
                    Color c = s == 2 ? UIKit.Red : UIKit.Accent;
                    m.a.color = s == 0 ? new Color(0.02f, 0.06f, 0.05f, 0.8f) : c;
                    m.b.color = s == 0 ? UIKit.AccentSoft : c;
                    m.t.color = s == 0 ? UIKit.Accent : new Color(0.02f, 0.05f, 0.04f);
                    m.scale = s == 0 ? 0.8f : 1.1f;
                    var cg = m.go.GetComponent<CanvasGroup>();
                    if (!cg) cg = m.go.AddComponent<CanvasGroup>();
                    cg.alpha = style >= 10 ? 0.55f : s == 0 ? 0.8f : 1f;
                    m.pulse = s == 2 ? 1f : 0f;
                }
            }

            // --- current target
            var t = pm.CurrentTarget;
            bool tShow = state == GameState.Playing && active != null && t.kind != TargetKind.Eject &&
                         (t.kind != TargetKind.None || (t.host && !t.allowed));
            SetOn(targetMarker, tShow);
            if (tShow)
            {
                targetMarker.world = t.worldPos;
                int style = t.allowed ? 0 : 1;
                if (style != targetMarker.style)
                {
                    targetMarker.style = style;
                    Color c = style == 0 ? UIKit.Accent : UIKit.Red;
                    targetMarker.a.color = c; targetMarker.b.color = c.WithAlpha(0.8f);
                }
                targetChevron.anchoredPosition = new Vector2(0, Mathf.Abs(Mathf.Sin(now * 5f)) * 6f);
                targetMarker.b.rectTransform.localEulerAngles = new Vector3(0, 0, now * 90f);
            }

            // --- NPC alert icons + unconscious timers
            alertUsed = sleepUsed = 0;
            Vector3 cp = cam.transform.position;
            var all = HostCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (!h || h.IsPossessed) continue;
                Vector3 hp = h.transform.position;
                float d2 = (hp - cp).sqrMagnitude;
                if (h.State == HostState.Unconscious)
                {
                    if (d2 > UnconsciousRange * UnconsciousRange) continue;
                    var m = sleepUsed < sleepPool.Count ? sleepPool[sleepUsed] : NewSleepMarker();
                    sleepUsed++;
                    SetOn(m, true);
                    m.world = hp + Vector3.up * 1.0f;
                    float p01 = Mathf.Clamp01(h.UnconsciousProgress01);
                    m.a.fillAmount = 1f - p01;
                    m.a.color = Color.Lerp(UIKit.Yellow, UIKit.Red, Mathf.InverseLerp(0.6f, 1f, p01));
                    continue;
                }
                if (h.State != HostState.Normal || !h.Brain) continue;
                char ic = h.Brain.AlertIcon;
                if (ic != '?' && ic != '!') continue;
                if (d2 > AlertRange * AlertRange) continue;
                var am = alertUsed < alertPool.Count ? alertPool[alertUsed] : NewAlertMarker();
                alertUsed++;
                SetOn(am, true);
                am.world = hp + Vector3.up * 2.45f;
                int st = ic == '!' ? 1 : 0;
                if (st != am.style)
                {
                    am.style = st;
                    Color c = st == 1 ? UIKit.Red : UIKit.Yellow;
                    am.t.text = st == 1 ? "!" : "?";
                    am.t.color = c; am.b.color = c;
                    am.pulse = st == 1 ? 1f : 0f;
                }
            }
            for (int i = alertUsed; i < alertPool.Count; i++) SetOn(alertPool[i], false);
            for (int i = sleepUsed; i < sleepPool.Count; i++) SetOn(sleepPool[i], false);
        }

        /// <summary>Runs right before canvases render (after every LateUpdate, incl. Cinemachine) so markers never lag the camera.</summary>
        void ProjectWorldMarkers()
        {
            if (!worldLayer || !worldLayer.gameObject.activeInHierarchy) return;
            var cam = WorldCam();
            if (!cam) return;
            for (int i = 0; i < amoebaMarkers.Length; i++) Project(cam, amoebaMarkers[i]);
            Project(cam, targetMarker);
            for (int i = 0; i < alertUsed; i++) Project(cam, alertPool[i]);
            for (int i = 0; i < sleepUsed; i++) Project(cam, sleepPool[i]);
        }

        void Project(Camera cam, Marker m)
        {
            if (!m.on) return;
            Vector3 sp = cam.WorldToScreenPoint(m.world);
            if (sp.z < 0.25f) { m.rt.position = Offscreen; return; }
            m.rt.position = new Vector3(sp.x, sp.y, 0f);
            float s = m.scale;
            if (m.distScale) s *= Mathf.Clamp(10f / sp.z, 0.55f, 1.1f);
            if (m.pulse > 0f) s *= 1f + 0.12f * Mathf.Abs(Mathf.Sin(now * 8f));
            m.rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
