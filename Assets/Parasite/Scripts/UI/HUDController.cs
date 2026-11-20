using System;
using System.Collections.Generic;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>
    /// In-game HUD for the City scene, built entirely from code (uGUI, legacy Text).
    /// Panels: suspicion (top-center), objective (top-left), active body + amoeba slots (bottom-left),
    /// context prompt + key hints (bottom-center), toasts (right), subtitles + letterbox, flashes,
    /// containment overlay, controls card (F1), FPS (F3). World markers: HUDController.World.cs.
    /// Pause / controls / end screen: HUDController.Menus.cs.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public partial class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        public static HUDController Create()
        {
            if (Instance) return Instance;
            var go = new GameObject("HUD");
            Instance = go.AddComponent<HUDController>();
            return Instance;
        }

        Canvas canvas;
        RectTransform root;
        CanvasGroup gameplay;
        GameState state = GameState.Playing;
        float now;   // unscaled time

        static readonly string[] Numbers = BuildNumbers();
        static string[] BuildNumbers() { var a = new string[101]; for (int i = 0; i <= 100; i++) a[i] = i.ToString(); return a; }

        // ================================================================== lifecycle
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            UIKit.LoadSettings();
            UIKit.EnsureEventSystem();

            canvas = UIKit.CreateCanvas("HUD Canvas", 10, transform);
            root = (RectTransform)canvas.transform;

            BuildWorldLayer(root);
            BuildFlash(root);
            BuildLetterbox(root);

            var g = UIKit.Rect("Gameplay", root).Stretch();
            gameplay = g.gameObject.AddComponent<CanvasGroup>();
            gameplay.interactable = false; gameplay.blocksRaycasts = false;
            BuildSuspicion(g);
            BuildObjective(g);
            BuildBody(g);
            BuildPrompt(g);
            BuildToasts(g);
            BuildControlsCard(g);

            BuildSubtitles(root);
            BuildFps(root);
            BuildReset(root);
            BuildPause(root);
            BuildEnd(root);

            GameEvents.Toast += OnToast;
            GameEvents.ObjectiveChanged += SetObjective;
            GameEvents.Subtitle += OnSubtitle;
            GameEvents.Ending += OnEnding;
            GameEvents.PauseChanged += OnPause;
            GameEvents.PossessionSplash += OnSplash;
            GameEvents.Captured += OnCaptured;
            Canvas.willRenderCanvases += ProjectWorldMarkers;
        }

        void Start()
        {
            var mm = MissionManager.Instance;
            if (mm && !string.IsNullOrEmpty(mm.Objective)) SetObjective(mm.Objective);
        }

        void OnDestroy()
        {
            GameEvents.Toast -= OnToast;
            GameEvents.ObjectiveChanged -= SetObjective;
            GameEvents.Subtitle -= OnSubtitle;
            GameEvents.Ending -= OnEnding;
            GameEvents.PauseChanged -= OnPause;
            GameEvents.PossessionSplash -= OnSplash;
            GameEvents.Captured -= OnCaptured;
            Canvas.willRenderCanvases -= ProjectWorldMarkers;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            now = Time.unscaledTime;
            var gm = GameManager.Instance;
            state = gm ? gm.State : GameState.Playing;
            bool paused = state == GameState.Paused;
            float gdt = paused ? 0f : dt;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) ToggleCard();
                if (kb.f3Key.wasPressedThisFrame) { fpsOn = !fpsOn; fpsText.gameObject.SetActive(fpsOn); }
            }

            bool hudOn = (state == GameState.Playing || paused) && !subActive && !endOpen;
            gameplay.alpha = Mathf.MoveTowards(gameplay.alpha, hudOn ? 1f : 0f, dt * 3f);

            if (gameplay.alpha > 0f)
            {
                TickSuspicion(dt);
                TickObjective(dt);
                TickBodyAndSlots(dt);
                TickPrompt(dt);
                TickCard(gdt);
            }
            TickToasts(gdt);
            TickSubtitles(dt);
            TickFlash(dt);
            TickFps(dt);
            TickReset(dt);
            TickMenus(dt);
        }

        void LateUpdate() => UpdateWorldMarkers();

        // ================================================================== suspicion (top-center)
        const float SusBarW = 500f;
        RectTransform susRoot, susFill, graceRoot;
        Image susFillImg, susEdge, susGlow;
        Text susLevel, susValue, graceText;
        CanvasGroup graceGroup;
        float susShown, susPunch, susK = -1f;
        int susLastInt = -1, graceLastTenths = -1;
        SuspicionLevel susLastLevel = (SuspicionLevel)(-1);
        Color susColor = UIKit.Accent;

        void BuildSuspicion(Transform p)
        {
            susRoot = UIKit.Rect("Suspicion", p).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(560, 74));
            susGlow = UIKit.Img(susRoot, "Glow", UIKit.Glow, UIKit.Red.WithAlpha(0f));
            susGlow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 240));
            var panel = UIKit.Panel(susRoot, "Panel", 10);
            panel.rectTransform.Stretch();
            susEdge = UIKit.EdgeOf(panel);

            var lbl = UIKit.Txt(panel.transform, "Label", "SUSPICION", 18, UIKit.TextDim, TextAnchor.MiddleLeft);
            lbl.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -9), new Vector2(120, 28));
            susValue = UIKit.Txt(panel.transform, "Value", "0", 20, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Bold);
            susValue.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(122, -9), new Vector2(80, 28));
            susLevel = UIKit.Txt(panel.transform, "Level", "CALM", 24, UIKit.Accent, TextAnchor.MiddleRight, Weight.Bold);
            susLevel.horizontalOverflow = HorizontalWrapMode.Overflow;
            susLevel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-30, -23), new Vector2(260, 30));

            var bar = UIKit.Img(panel.transform, "Bar", UIKit.Rounded(4), new Color(1, 1, 1, 0.07f));
            bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 15), new Vector2(SusBarW, 12));
            susFillImg = UIKit.Img(bar.transform, "Fill", UIKit.Rounded(4), UIKit.Accent);
            susFill = susFillImg.rectTransform;
            susFill.anchorMin = Vector2.zero; susFill.anchorMax = new Vector2(0, 1);
            susFill.offsetMin = susFill.offsetMax = Vector2.zero;
            foreach (int t in new[] { SuspicionRules.CuriousAt, SuspicionRules.InvestigatingAt, SuspicionRules.PursuingAt })
            {
                var tick = UIKit.Img(bar.transform, "Tick" + t, UIKit.White, new Color(1, 1, 1, 0.5f));
                tick.rectTransform.Place(new Vector2(t / SuspicionRules.Max, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 20));
            }

            graceRoot = UIKit.Rect("Grace", susRoot).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(320, 34));
            graceGroup = graceRoot.gameObject.AddComponent<CanvasGroup>();
            graceGroup.alpha = 0f;
            var gp = UIKit.Panel(graceRoot, "Panel", 8, new Color(0.16f, 0.11f, 0.01f, 0.85f), UIKit.Yellow.WithAlpha(0.55f));
            gp.rectTransform.Stretch();
            graceText = UIKit.Txt(gp.transform, "Text", "ESCAPE WINDOW", 20, UIKit.Yellow, TextAnchor.MiddleCenter, Weight.Bold);
            graceText.rectTransform.Stretch();
        }

        void TickSuspicion(float dt)
        {
            var ss = SuspicionSystem.Instance;
            float v = ss ? ss.Value : 0f;
            var lvl = ss ? ss.Level : SuspicionLevel.Calm;

            susShown = Mathf.Abs(susShown - v) < 0.05f ? v : Mathf.Lerp(susShown, v, 1f - Mathf.Exp(-10f * dt));
            float k = Mathf.Clamp01(susShown / SuspicionRules.Max);
            if (!Mathf.Approximately(k, susK)) { susK = k; susFill.anchorMax = new Vector2(k, 1f); }
            int iv = Mathf.Clamp(Mathf.RoundToInt(v), 0, 100);
            if (iv != susLastInt) { susLastInt = iv; susValue.text = Numbers[iv]; }
            if (lvl != susLastLevel)
            {
                if ((int)susLastLevel >= 0 && lvl > susLastLevel) susPunch = 1f;
                susLastLevel = lvl;
                susLevel.text = SuspicionRules.Label(lvl);
            }

            susColor = Color.Lerp(susColor, UIKit.SuspicionColor(lvl), 1f - Mathf.Exp(-8f * dt));
            bool pursuit = lvl == SuspicionLevel.Pursuing;
            float pulse = pursuit ? 0.5f + 0.5f * Mathf.Sin(now * 9f) : 0f;
            susFillImg.color = Color.Lerp(susColor, Color.white, pulse * 0.35f);
            susLevel.color = susColor;
            susEdge.color = pursuit ? UIKit.Red.WithAlpha(0.35f + 0.5f * pulse) : Color.Lerp(UIKit.AccentFaint, susColor.WithAlpha(0.45f), k);
            susGlow.color = UIKit.Red.WithAlpha(pursuit ? 0.10f + 0.22f * pulse : 0f);
            susPunch = Mathf.MoveTowards(susPunch, 0f, dt * 2.5f);
            susLevel.rectTransform.localScale = Vector3.one * (1f + susPunch * 0.3f);
            susRoot.localScale = Vector3.one * (1f + susPunch * 0.05f + pulse * 0.02f);

            float grace = ss ? ss.GraceRemaining : 0f;
            if (grace > 0f)
            {
                int tenths = Mathf.CeilToInt(grace * 10f);
                if (tenths != graceLastTenths)
                {
                    graceLastTenths = tenths;
                    graceText.text = "ESCAPE WINDOW  " + (tenths / 10f).ToString("0.0") + "s";
                }
                graceGroup.alpha = 0.7f + 0.3f * Mathf.Sin(now * 10f);
            }
            else if (graceGroup.alpha > 0f) graceGroup.alpha = Mathf.MoveTowards(graceGroup.alpha, 0f, dt * 4f);
        }

        // ================================================================== objective (top-left)
        RectTransform objRoot;
        Text objText;
        Image objEdge, objFlash;
        CanvasGroup objGroup;
        float objFlashT;
        string objLast = "";

        void BuildObjective(Transform p)
        {
            objRoot = UIKit.Rect("Objective", p).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(480, 110));
            objGroup = objRoot.gameObject.AddComponent<CanvasGroup>();
            objGroup.alpha = 0f;
            var panel = UIKit.Panel(objRoot, "Panel", 10);
            panel.rectTransform.Stretch();
            objEdge = UIKit.EdgeOf(panel);
            objFlash = UIKit.Img(panel.transform, "Flash", UIKit.Rounded(10), UIKit.Accent.WithAlpha(0f));
            objFlash.rectTransform.Stretch();
            var bar = UIKit.Img(panel.transform, "Bar", UIKit.Rounded(2), UIKit.Accent);
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(0, 1);
            bar.rectTransform.pivot = new Vector2(0, 0.5f);
            bar.rectTransform.offsetMin = new Vector2(12, 16); bar.rectTransform.offsetMax = new Vector2(16, -16);
            var head = UIKit.Txt(panel.transform, "Header", "OBJECTIVE", 18, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -12), new Vector2(300, 24));
            objText = UIKit.Txt(panel.transform, "Text", "", 23, UIKit.TextMain, TextAnchor.UpperLeft, Weight.Medium);
            objText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -40), new Vector2(426, 60));
        }

        void SetObjective(string text)
        {
            text = UIKit.Clean(text);
            if (text == objLast) return;
            objLast = text;
            objText.text = text;
            float h = Mathf.Max(28f, objText.preferredHeight);
            objText.rectTransform.sizeDelta = new Vector2(426, h);
            objRoot.sizeDelta = new Vector2(480, 40 + h + 18);
            objFlashT = text.Length > 0 ? 1f : 0f;
        }

        void TickObjective(float dt)
        {
            objGroup.alpha = Mathf.MoveTowards(objGroup.alpha, objLast.Length > 0 ? 1f : 0f, dt * 4f);
            if (objFlashT <= 0f) return;
            objFlashT = Mathf.MoveTowards(objFlashT, 0f, dt * 0.6f);
            float e = objFlashT * objFlashT;
            objFlash.color = UIKit.Accent.WithAlpha(0.18f * e);
            objEdge.color = Color.Lerp(UIKit.AccentFaint, UIKit.Accent, e);
            objRoot.anchoredPosition = new Vector2(28f - 40f * Mathf.Pow(Mathf.Max(0f, objFlashT - 0.75f) * 4f, 2f), -24f);
        }

        // ================================================================== active body + slots (bottom-left)
        const float BodyW = 470f;
        RectTransform bodyRoot, capRoot, capFill, slotsRoot;
        Image bodyBar, bodyEdge, capEdge;
        Text bodyHeader, bodyName, bodySub, bodyAbility, capTimer;
        CanvasGroup bodyGroup, capGroup;
        AmoebaAgent lastAgent;
        object lastHost;
        bool lastTransit, lastCaptured, lastHostAlive;
        int lastId = -1, capLastTenths = -1;

        class Slot
        {
            public RectTransform rt; public CanvasGroup g;
            public Image edge, badge, bg; public Text num, state;
            public string lastState; public int lastMode = -1;
        }
        readonly Slot[] slots = new Slot[SplitRules.MaxAmoebas];

        void BuildBody(Transform p)
        {
            bodyRoot = UIKit.Rect("Body", p).Place(Vector2.zero, Vector2.zero, new Vector2(28, 28), new Vector2(BodyW, 150));
            bodyGroup = bodyRoot.gameObject.AddComponent<CanvasGroup>();
            var panel = UIKit.Panel(bodyRoot, "Panel", 10);
            panel.rectTransform.Stretch();
            bodyEdge = UIKit.EdgeOf(panel);
            bodyBar = UIKit.Img(panel.transform, "Bar", UIKit.Rounded(2), UIKit.Accent);
            bodyBar.rectTransform.anchorMin = Vector2.zero; bodyBar.rectTransform.anchorMax = new Vector2(0, 1);
            bodyBar.rectTransform.pivot = new Vector2(0, 0.5f);
            bodyBar.rectTransform.offsetMin = new Vector2(12, 16); bodyBar.rectTransform.offsetMax = new Vector2(16, -16);

            bodyHeader = UIKit.Txt(panel.transform, "Header", "AMOEBA #1", 18, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Bold);
            bodyHeader.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -12), new Vector2(300, 24));
            bodyName = UIKit.Txt(panel.transform, "Name", "", 34, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Bold);
            bodyName.horizontalOverflow = HorizontalWrapMode.Overflow;
            bodyName.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -36), new Vector2(420, 40));
            bodySub = UIKit.Txt(panel.transform, "Sub", "", 22, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Semi);
            bodySub.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -76), new Vector2(420, 28));
            bodyAbility = UIKit.Txt(panel.transform, "Ability", "", 20, UIKit.TextDim, TextAnchor.UpperLeft, Weight.Medium);
            bodyAbility.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -108), new Vector2(420, 30));

            // captured overlay
            capRoot = UIKit.Rect("Captured", bodyRoot).Stretch();
            capGroup = capRoot.gameObject.AddComponent<CanvasGroup>();
            var cp = UIKit.Panel(capRoot, "Panel", 10, new Color(0.28f, 0.015f, 0.02f, 0.95f), UIKit.Red);
            cp.rectTransform.Stretch();
            capEdge = UIKit.EdgeOf(cp);
            var ct = UIKit.Txt(cp.transform, "Title", "CAPTURED — PRESS E TO EJECT", 30, new Color(1f, 0.86f, 0.82f), TextAnchor.MiddleCenter, Weight.Bold, true);
            ct.horizontalOverflow = HorizontalWrapMode.Overflow;
            ct.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(440, 40));
            capTimer = UIKit.Txt(cp.transform, "Timer", "", 20, new Color(1f, 0.6f, 0.55f), TextAnchor.MiddleCenter, Weight.Semi);
            capTimer.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(440, 26));
            var cbar = UIKit.Img(cp.transform, "Bar", UIKit.Rounded(3), new Color(1, 1, 1, 0.1f));
            cbar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(400, 8));
            var cf = UIKit.Img(cbar.transform, "Fill", UIKit.Rounded(3), UIKit.Red);
            capFill = cf.rectTransform;
            capFill.anchorMin = Vector2.zero; capFill.anchorMax = Vector2.one; capFill.offsetMin = capFill.offsetMax = Vector2.zero;
            capRoot.gameObject.SetActive(false);

            // amoeba slots
            slotsRoot = UIKit.Rect("Slots", p).Place(Vector2.zero, Vector2.zero, new Vector2(28, 188), new Vector2(BodyW, 46));
            for (int i = 0; i < slots.Length; i++)
            {
                var s = new Slot();
                float w = (BodyW - 10f) / slots.Length;
                s.rt = UIKit.Rect("Slot" + (i + 1), slotsRoot).Place(Vector2.zero, Vector2.zero, new Vector2(i * (w + 10f), 0), new Vector2(w, 46));
                s.g = s.rt.gameObject.AddComponent<CanvasGroup>();
                s.bg = UIKit.Panel(s.rt, "Panel", 8);
                s.bg.rectTransform.Stretch();
                s.edge = UIKit.EdgeOf(s.bg);
                s.badge = UIKit.Img(s.rt, "Badge", UIKit.Circle, UIKit.Accent);
                s.badge.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(32, 32));
                s.num = UIKit.Txt(s.badge.transform, "Num", Numbers[i + 1], 20, Color.black, TextAnchor.MiddleCenter, Weight.Bold);
                s.num.rectTransform.Stretch(0, 1, 0, 0);
                s.state = UIKit.Txt(s.rt, "State", "", 19, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Semi);
                s.state.horizontalOverflow = HorizontalWrapMode.Overflow;
                s.state.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(w - 58, 40));
                slots[i] = s;
            }
        }

        const string FreeAbility = "Small, fast and visible to everyone. Hop with Space — aim at a person and press E to possess them.";

        void TickBodyAndSlots(float dt)
        {
            var pm = PossessionManager.Instance;
            var a = pm != null ? pm.Active : null;
            bodyGroup.alpha = Mathf.MoveTowards(bodyGroup.alpha, a != null ? 1f : 0f, dt * 4f);

            if (a != null)
            {
                var host = a.Host;
                bool hostAlive = host;
                if (a != lastAgent || a.Id != lastId || !ReferenceEquals(host, lastHost) || hostAlive != lastHostAlive ||
                    a.InTransit != lastTransit || a.CapturedPendingEject != lastCaptured)
                {
                    lastAgent = a; lastId = a.Id; lastHost = host; lastHostAlive = hostAlive;
                    lastTransit = a.InTransit; lastCaptured = a.CapturedPendingEject;
                    RebuildBody(a);
                }
                if (a.CapturedPendingEject)
                {
                    float frac = Mathf.Clamp01(a.CaptureTimer / PossessionManager.AutoEjectSeconds);
                    capFill.anchorMax = new Vector2(frac, 1f);
                    int tenths = Mathf.CeilToInt(Mathf.Max(0f, a.CaptureTimer) * 10f);
                    if (tenths != capLastTenths) { capLastTenths = tenths; capTimer.text = "Auto-eject in " + (tenths / 10f).ToString("0.0") + " s"; }
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * 12f);
                    capEdge.color = Color.Lerp(UIKit.Red.WithAlpha(0.5f), Color.white, pulse * 0.6f);
                    capGroup.alpha = 0.88f + 0.12f * pulse;
                }
            }

            // slots: one per amoeba id (1..2)
            int count = pm != null ? pm.Agents.Count : 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                AmoebaAgent ag = null;
                if (pm != null) for (int j = 0; j < pm.Agents.Count; j++) if (pm.Agents[j].Id == i + 1) { ag = pm.Agents[j]; break; }
                string st; int mode; // 0 empty, 1 inactive, 2 active, 3 captured
                if (ag == null) { st = count < SplitRules.MaxAmoebas ? "Empty  ·  [V] split" : "Empty"; mode = 0; }
                else
                {
                    st = ag.CapturedPendingEject ? "CAPTURED" : ag.InTransit ? "Leaping…" : ag.Host ? ag.Host.DisplayName : "Free amoeba";
                    mode = ag.CapturedPendingEject ? 3 : ag == a ? 2 : 1;
                }
                if (!ReferenceEquals(st, s.lastState)) { s.lastState = st; s.state.text = st; }
                if (mode != s.lastMode)
                {
                    s.lastMode = mode;
                    bool active = mode == 2;
                    s.g.alpha = mode == 0 ? 0.4f : active || mode == 3 ? 1f : 0.75f;
                    s.edge.color = mode == 3 ? UIKit.Red : active ? UIKit.Accent.WithAlpha(0.9f) : UIKit.AccentFaint;
                    s.bg.color = active ? UIKit.PanelBgLight : UIKit.PanelBg;
                    s.badge.sprite = active || mode == 3 ? UIKit.Circle : UIKit.Ring(3f);
                    s.badge.color = mode == 3 ? UIKit.Red : mode == 0 ? UIKit.TextDim : UIKit.Accent;
                    s.num.color = active || mode == 3 ? new Color(0.02f, 0.05f, 0.04f) : mode == 0 ? UIKit.TextDim : UIKit.Accent;
                    s.state.color = mode == 3 ? UIKit.Red : active ? Color.white : UIKit.TextDim;
                }
            }
        }

        void RebuildBody(AmoebaAgent a)
        {
            bodyHeader.text = "AMOEBA #" + a.Id + (a.Host ? "  ·  HOST" : "");
            string ability;
            Color bar;
            if (a.InTransit)
            {
                bodyName.text = "Leaping…"; bodySub.text = "In transit"; bodySub.color = UIKit.Accent;
                ability = ""; bar = UIKit.Accent;
            }
            else if (a.Host)
            {
                var prof = a.Host.Profile;
                bodyName.text = a.Host.DisplayName; bodySub.text = prof.title; bodySub.color = UIKit.Accent;
                ability = prof.abilityText; bar = UIKit.Accent;
            }
            else
            {
                bodyName.text = "Free amoeba"; bodySub.text = "(vulnerable)"; bodySub.color = UIKit.Yellow;
                ability = FreeAbility; bar = UIKit.Yellow;
            }
            bodyAbility.text = ability;
            float ah = ability.Length > 0 ? bodyAbility.preferredHeight : 0f;
            bodyAbility.rectTransform.sizeDelta = new Vector2(420, ah);
            float h = Mathf.Max(a.CapturedPendingEject ? 140f : 0f, 108f + ah + (ah > 0 ? 18f : 8f));
            bodyRoot.sizeDelta = new Vector2(BodyW, h);
            slotsRoot.anchoredPosition = new Vector2(28, 28 + h + 10);
            bodyBar.color = bar;
            bodyEdge.color = a.Host ? UIKit.AccentSoft : UIKit.Yellow.WithAlpha(0.35f);
            capRoot.gameObject.SetActive(a.CapturedPendingEject);
            capLastTenths = -1;
        }

        // ================================================================== prompt + hints (bottom-center)
        RectTransform promptRoot, promptKey;
        Image promptEdge, promptKeyEdge;
        Text promptKeyLabel, promptText, hintText;
        CanvasGroup promptGroup, hintGroup;
        string promptLast;
        int promptStyle = -1, hintSig = -1;
        readonly string[] hintCache = new string[16];

        void BuildPrompt(Transform p)
        {
            promptRoot = UIKit.Rect("Prompt", p).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(400, 58));
            promptGroup = promptRoot.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            var panel = UIKit.Panel(promptRoot, "Panel", 10, UIKit.PanelBgLight, UIKit.AccentSoft);
            panel.rectTransform.Stretch();
            promptEdge = UIKit.EdgeOf(panel);
            promptKey = UIKit.KeyCap(panel.transform, "E", 40, 24);
            promptKey.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(40, 40));
            promptKeyEdge = UIKit.EdgeOf(promptKey.GetComponent<Image>());
            promptKeyLabel = promptKey.GetComponentInChildren<Text>();
            promptText = UIKit.Txt(panel.transform, "Text", "", 27, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Semi);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptText.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(300, 50));

            var hint = UIKit.Rect("Hints", p).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 108), new Vector2(1000, 30));
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintText = UIKit.Txt(hint, "Text", "", 21, UIKit.TextDim, TextAnchor.MiddleCenter, Weight.Semi, true);
            hintText.rectTransform.Stretch();
        }

        void TickPrompt(float dt)
        {
            var pm = PossessionManager.Instance;
            var a = pm != null ? pm.Active : null;
            bool playing = state == GameState.Playing;
            var t = pm != null ? pm.CurrentTarget : TargetInfo.None;
            bool show = playing && a != null && (t.kind != TargetKind.None || (t.host && !t.allowed)) && !string.IsNullOrEmpty(t.prompt);

            if (show)
            {
                if (!ReferenceEquals(t.prompt, promptLast) && t.prompt != promptLast)
                {
                    promptLast = t.prompt;
                    promptText.text = UIKit.Clean(t.prompt);
                    float w = promptText.preferredWidth;
                    promptText.rectTransform.sizeDelta = new Vector2(w + 4, 50);
                    promptRoot.sizeDelta = new Vector2(64 + w + 26, 58);
                }
                int style = t.kind == TargetKind.Eject ? 2 : t.allowed ? 0 : 1;
                if (style != promptStyle)
                {
                    promptStyle = style;
                    Color c = style == 0 ? UIKit.Accent : UIKit.Red;
                    promptEdge.color = style == 0 ? UIKit.AccentSoft : UIKit.Red.WithAlpha(0.7f);
                    promptKeyEdge.color = c; promptKeyLabel.color = c;
                    promptText.color = style == 0 ? UIKit.TextMain : style == 1 ? new Color(1f, 0.72f, 0.68f) : new Color(1f, 0.86f, 0.82f);
                }
                if (promptGroup.alpha <= 0f) promptRoot.localScale = Vector3.one * 0.94f;
                float s = style == 2 ? 1f + 0.04f * Mathf.Sin(now * 14f) : 1f;
                promptRoot.localScale = Vector3.Lerp(promptRoot.localScale, Vector3.one * s, 1f - Mathf.Exp(-18f * dt));
            }
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, show ? 1f : 0f, dt * (show ? 10f : 6f));

            // available keys hint
            int sig = -1;
            if (playing && a != null && !a.InTransit && !a.CapturedPendingEject)
            {
                int n = pm.Agents.Count;
                sig = (a.Host ? 1 : 0) | (SplitRules.CanSplit(n) ? 2 : 0) | (n >= 2 ? 4 : 0);
            }
            if (sig != hintSig)
            {
                hintSig = sig;
                if (sig >= 0)
                {
                    hintCache[sig] ??= BuildHint(sig);
                    hintText.text = hintCache[sig];
                }
            }
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, sig > 0 ? 1f : 0f, dt * 5f);
        }

        static string Key(string k, string label) => $"<color={UIKit.AccentHex}>[{k}]</color> {label}";

        static string BuildHint(int sig)
        {
            var parts = new List<string>(3);
            if ((sig & 1) != 0) parts.Add(Key("Q", "Leave host"));
            if ((sig & 2) != 0) parts.Add(Key("V", "Split"));
            if ((sig & 4) != 0) parts.Add(Key("Tab", "Switch"));
            return string.Join("        ", parts);
        }

        // ================================================================== toasts (right)
        const float ToastW = 440f, ToastLife = 3.5f, ToastFade = 0.5f;
        const int ToastMax = 4;

        class Toast { public RectTransform rt; public CanvasGroup g; public Image bar; public Text text; public float age, h, y; }
        readonly List<Toast> toasts = new();
        readonly Stack<Toast> toastPool = new();
        RectTransform toastRoot;

        void BuildToasts(Transform p)
        {
            toastRoot = UIKit.Rect("Toasts", p).Place(Vector2.one, Vector2.one, new Vector2(-28, -64), new Vector2(ToastW, 420));
        }

        Toast NewToast()
        {
            var t = new Toast();
            t.rt = UIKit.Rect("Toast", toastRoot);
            t.rt.anchorMin = t.rt.anchorMax = t.rt.pivot = Vector2.one;
            t.g = t.rt.gameObject.AddComponent<CanvasGroup>();
            var panel = UIKit.Panel(t.rt, "Panel", 8);
            panel.rectTransform.Stretch();
            t.bar = UIKit.Img(panel.transform, "Bar", UIKit.Rounded(2), Color.white);
            t.bar.rectTransform.anchorMin = Vector2.zero; t.bar.rectTransform.anchorMax = new Vector2(0, 1);
            t.bar.rectTransform.pivot = new Vector2(0, 0.5f);
            t.bar.rectTransform.offsetMin = new Vector2(8, 9); t.bar.rectTransform.offsetMax = new Vector2(12, -9);
            t.text = UIKit.Txt(panel.transform, "Text", "", 21, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Semi);
            t.text.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(ToastW - 40, 30));
            return t;
        }

        void OnToast(string msg, Color c)
        {
            msg = UIKit.Clean(msg);
            if (msg.Length == 0) return;
            if (toasts.Count > 0 && toasts[0].text.text == msg && toasts[0].age < 2.5f) { toasts[0].age = Mathf.Min(toasts[0].age, 0.2f); return; }
            if (toasts.Count >= ToastMax) { var old = toasts[^1]; toasts.RemoveAt(toasts.Count - 1); old.rt.gameObject.SetActive(false); toastPool.Push(old); }
            var t = toastPool.Count > 0 ? toastPool.Pop() : NewToast();
            t.rt.gameObject.SetActive(true);
            t.rt.SetAsLastSibling();
            t.text.text = msg;
            t.text.color = Color.Lerp(c, Color.white, 0.55f);
            t.bar.color = c.WithAlpha(1f);
            float th = t.text.preferredHeight;
            t.text.rectTransform.sizeDelta = new Vector2(ToastW - 40, th);
            t.h = Mathf.Max(44f, th + 22f);
            t.rt.sizeDelta = new Vector2(ToastW, t.h);
            t.age = 0f; t.y = 0f; t.g.alpha = 0f;
            t.rt.anchoredPosition = new Vector2(60f, 0f);
            toasts.Insert(0, t);
        }

        void TickToasts(float dt)
        {
            float yAcc = 0f;
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                t.age += dt;
                t.y = Mathf.Lerp(t.y, -yAcc, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                yAcc += t.h + 8f;
                float appear = Mathf.Clamp01(t.age / 0.2f);
                float fade = t.age > ToastLife ? 1f - (t.age - ToastLife) / ToastFade : 1f;
                t.g.alpha = appear * Mathf.Clamp01(fade);
                float ease = 1f - (1f - appear) * (1f - appear);
                t.rt.anchoredPosition = new Vector2((1f - ease) * 60f, t.y);
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                if (t.age < ToastLife + ToastFade) continue;
                toasts.RemoveAt(i);
                t.rt.gameObject.SetActive(false);
                toastPool.Push(t);
            }
        }

        // ================================================================== subtitles + letterbox
        const float LetterboxH = 118f;
        RectTransform lbTop, lbBottom;
        CanvasGroup subGroup;
        Text subSpeaker, subLine, skipHint;
        bool subActive;
        float subLineT, lbK;

        void BuildLetterbox(Transform p)
        {
            lbTop = UIKit.Img(p, "LetterboxTop", UIKit.White, new Color(0, 0, 0, 0.94f)).rectTransform;
            lbTop.anchorMin = new Vector2(0, 1); lbTop.anchorMax = Vector2.one; lbTop.pivot = new Vector2(0.5f, 1);
            lbTop.sizeDelta = new Vector2(0, LetterboxH); lbTop.anchoredPosition = new Vector2(0, LetterboxH);
            lbBottom = UIKit.Img(p, "LetterboxBottom", UIKit.White, new Color(0, 0, 0, 0.94f)).rectTransform;
            lbBottom.anchorMin = Vector2.zero; lbBottom.anchorMax = new Vector2(1, 0); lbBottom.pivot = new Vector2(0.5f, 0);
            lbBottom.sizeDelta = new Vector2(0, LetterboxH); lbBottom.anchoredPosition = new Vector2(0, -LetterboxH);
            skipHint = UIKit.Txt(lbBottom, "Skip", Key("E", "Next line"), 18, UIKit.TextDim, TextAnchor.MiddleRight, Weight.Semi);
            skipHint.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(400, 30));
            lbTop.gameObject.SetActive(false); lbBottom.gameObject.SetActive(false);
        }

        void BuildSubtitles(Transform p)
        {
            var r = UIKit.Rect("Subtitles", p).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, LetterboxH + 22), new Vector2(1400, 140));
            subGroup = r.gameObject.AddComponent<CanvasGroup>();
            subGroup.alpha = 0f;
            subSpeaker = UIKit.Txt(r, "Speaker", "", 22, UIKit.Accent, TextAnchor.LowerCenter, Weight.Bold, true);
            subSpeaker.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1400, 30));
            subLine = UIKit.Txt(r, "Line", "", 34, Color.white, TextAnchor.UpperCenter, Weight.Medium, true);
            subLine.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(1300, 100));
        }

        void OnSubtitle(string speaker, string line)
        {
            if (string.IsNullOrEmpty(line)) { subActive = false; return; }
            subActive = true;
            subSpeaker.text = string.IsNullOrEmpty(speaker) ? "" : speaker.ToUpperInvariant();
            subLine.text = UIKit.Clean(line);
            subLineT = 0f;
        }

        void TickSubtitles(float dt)
        {
            bool cinematic = (subActive || state == GameState.Ending) && !endOpen;
            lbK = Mathf.MoveTowards(lbK, cinematic ? 1f : 0f, dt * 2.2f);
            bool lbOn = lbK > 0f;
            if (lbTop.gameObject.activeSelf != lbOn) { lbTop.gameObject.SetActive(lbOn); lbBottom.gameObject.SetActive(lbOn); }
            if (lbOn)
            {
                float e = 1f - (1f - lbK) * (1f - lbK);
                lbTop.anchoredPosition = new Vector2(0, (1f - e) * LetterboxH);
                lbBottom.anchoredPosition = new Vector2(0, -(1f - e) * LetterboxH);
                skipHint.enabled = subActive && state == GameState.Ending;
            }
            subLineT += dt;
            float target = subActive && !endOpen ? Mathf.Clamp01(subLineT / 0.3f) : 0f;
            subGroup.alpha = subActive ? target : Mathf.MoveTowards(subGroup.alpha, 0f, dt * 3f);
        }

        // ================================================================== screen flash
        Image flash;
        float flashT = 1f, flashDur = 1f, flashPeak;

        void BuildFlash(Transform p)
        {
            flash = UIKit.Img(p, "Flash", UIKit.Vignette, Color.clear);
            flash.rectTransform.Stretch();
            flash.enabled = false;
        }

        void Flash(Color c, float peak, float dur)
        {
            flash.color = c.WithAlpha(0f);
            flashPeak = peak; flashDur = dur; flashT = 0f;
            flash.enabled = true;
        }

        void OnSplash(Vector3 _) => Flash(UIKit.Accent, 0.32f, 0.5f);
        void OnCaptured() => Flash(UIKit.Red, 0.45f, 0.8f);

        void TickFlash(float dt)
        {
            if (!flash.enabled) return;
            flashT += dt;
            float k = Mathf.Clamp01(flashT / flashDur);
            float a = flashT < 0.06f ? flashPeak * (flashT / 0.06f) : flashPeak * (1f - k) * (1f - k);
            flash.color = flash.color.WithAlpha(a);
            if (k >= 1f) flash.enabled = false;
        }

        // ================================================================== containment overlay
        CanvasGroup resetGroup;
        Text resetTitle, resetWhere;
        bool resetShown;

        void BuildReset(Transform p)
        {
            var r = UIKit.Rect("Containment", p).Stretch();
            resetGroup = r.gameObject.AddComponent<CanvasGroup>();
            resetGroup.alpha = 0f; resetGroup.blocksRaycasts = false;
            UIKit.Img(r, "Tint", UIKit.White, new Color(0.16f, 0f, 0.01f, 0.7f)).rectTransform.Stretch();
            UIKit.Img(r, "Vignette", UIKit.Vignette, new Color(0.55f, 0f, 0.02f, 0.9f)).rectTransform.Stretch();
            var line = UIKit.Img(r, "Line", UIKit.White, UIKit.Red.WithAlpha(0.6f));
            line.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(900, 2));
            resetTitle = UIKit.Txt(r, "Title", "SPECIMEN CONTAINED", 88, UIKit.Red, TextAnchor.LowerCenter, Weight.Bold, true);
            resetTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            resetTitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1400, 110));
            var sub = UIKit.Txt(r, "Sub", "Returning to last checkpoint…", 30, UIKit.TextMain, TextAnchor.UpperCenter, Weight.Semi, true);
            sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1200, 40));
            resetWhere = UIKit.Txt(r, "Where", "", 22, UIKit.TextDim, TextAnchor.UpperCenter, Weight.Medium, true);
            resetWhere.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(1200, 30));
            r.gameObject.SetActive(false);
        }

        void TickReset(float dt)
        {
            bool on = state == GameState.Resetting;
            if (on && !resetShown)
            {
                resetShown = true;
                resetGroup.gameObject.SetActive(true);
                resetWhere.text = RunState.HasCheckpoint && !string.IsNullOrEmpty(RunState.CheckpointName) ? "Checkpoint  ·  " + RunState.CheckpointName : "";
            }
            if (!resetShown) return;
            resetGroup.alpha = Mathf.MoveTowards(resetGroup.alpha, on ? 1f : 0f, dt * 2.5f);
            resetTitle.color = UIKit.Red.WithAlpha(0.8f + 0.2f * Mathf.Sin(now * 6f));
            if (!on && resetGroup.alpha <= 0f) { resetShown = false; resetGroup.gameObject.SetActive(false); }
        }

        // ================================================================== controls card (F1)
        RectTransform cardRoot;
        CanvasGroup cardGroup;
        bool cardVisible, cardAuto, cardMoved;
        float cardTime, cardMoveAt;

        void BuildControlsCard(Transform p)
        {
            const float w = 430f;
            cardRoot = UIKit.Rect("ControlsCard", p);
            cardGroup = cardRoot.gameObject.AddComponent<CanvasGroup>();
            var panel = UIKit.Panel(cardRoot, "Panel", 10);
            panel.rectTransform.Stretch();
            var head = UIKit.Txt(panel.transform, "Header", "CONTROLS", 20, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(200, 26));
            var toggle = UIKit.Txt(panel.transform, "Toggle", Key("F1", "hide"), 17, UIKit.TextDim, TextAnchor.MiddleRight, Weight.Semi);
            toggle.rectTransform.Place(Vector2.one, Vector2.one, new Vector2(-22, -14), new Vector2(200, 26));
            var list = UIKit.ControlsList(panel.transform, w - 48, 19, false, 9);
            list.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -50), list.sizeDelta);
            cardRoot.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 40), new Vector2(w, 50 + list.sizeDelta.y + 16));

            cardVisible = cardAuto = RunState.Resets == 0;
            cardGroup.alpha = cardVisible ? 1f : 0f;
            cardRoot.gameObject.SetActive(cardVisible);
        }

        void ToggleCard()
        {
            if (state != GameState.Playing && state != GameState.Paused) return;
            cardAuto = false;
            cardVisible = !cardVisible;
            if (cardVisible) cardRoot.gameObject.SetActive(true);
        }

        void TickCard(float dt)
        {
            if (cardAuto && cardVisible)
            {
                cardTime += dt;
                if (!cardMoved && cardTime > 0.5f && GameInput.MoveValue.sqrMagnitude > 0.04f) { cardMoved = true; cardMoveAt = cardTime; }
                if (cardTime > 10f || (cardMoved && cardTime - cardMoveAt > 4f)) { cardVisible = false; cardAuto = false; }
            }
            if (!cardRoot.gameObject.activeSelf) return;
            float a = Mathf.MoveTowards(cardGroup.alpha, cardVisible ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            cardGroup.alpha = a;
            cardRoot.anchoredPosition = new Vector2(28f - (1f - a) * 30f, 40f);
            if (!cardVisible && a <= 0f) cardRoot.gameObject.SetActive(false);
        }

        // ================================================================== FPS (F3)
        Text fpsText;
        bool fpsOn;
        int fpsFrames;
        float fpsTime;

        void BuildFps(Transform p)
        {
            fpsText = UIKit.Txt(p, "FPS", "", 18, UIKit.TextDim, TextAnchor.MiddleRight, Weight.Semi, true);
            fpsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            fpsText.rectTransform.Place(Vector2.one, Vector2.one, new Vector2(-28, -20), new Vector2(320, 26));
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, "-showfps", StringComparison.OrdinalIgnoreCase)) fpsOn = true;
            fpsText.gameObject.SetActive(fpsOn);
        }

        void TickFps(float dt)
        {
            if (!fpsOn) return;
            fpsFrames++; fpsTime += dt;
            if (fpsTime < 0.5f) return;
            float fps = fpsFrames / fpsTime;
            fpsText.text = fps.ToString("0") + " FPS  ·  " + (1000f / Mathf.Max(1f, fps)).ToString("0.0") + " ms";
            fpsText.color = fps >= 55f ? UIKit.Accent : fps >= 30f ? UIKit.Yellow : UIKit.Red;
            fpsFrames = 0; fpsTime = 0f;
        }
    }
}
