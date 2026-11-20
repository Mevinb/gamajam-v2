using System.Collections.Generic;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>Pause menu (+ controls overlay, mouse sensitivity) and the end screen.</summary>
    public partial class HUDController
    {
        // ================================================================== pause
        RectTransform pauseRoot, pausePanel;
        CanvasGroup pauseGroup;
        Button btnResume, btnControls;
        Slider sensSlider;
        Text sensValue;
        GameObject pauseControls;
        Button pauseControlsBack;
        bool pauseOpen, controlsOpen;

        void BuildPause(Transform p)
        {
            pauseRoot = UIKit.Rect("Pause", p).Stretch();
            pauseGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();
            var dim = UIKit.Img(pauseRoot, "Dim", UIKit.White, new Color(0f, 0.01f, 0.01f, 0.62f));
            dim.rectTransform.Stretch(); dim.raycastTarget = true;
            UIKit.Img(pauseRoot, "Vignette", UIKit.Vignette, new Color(0, 0, 0, 0.8f)).rectTransform.Stretch();

            const float W = 580f, H = 790f;
            var panel = UIKit.Panel(pauseRoot, "Panel", 14, UIKit.PanelBgLight, UIKit.AccentSoft);
            pausePanel = panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var glow = UIKit.Img(panel.transform, "Glow", UIKit.Glow, UIKit.Accent.WithAlpha(0.08f));
            glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 300));
            var title = UIKit.Txt(panel.transform, "Title", "PAUSED", 72, UIKit.TextMain, TextAnchor.MiddleCenter, Weight.Bold, true);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(W, 84));
            var sub = UIKit.Txt(panel.transform, "Sub", "SPECIMEN 7  ·  VITAL SIGNS SUSPENDED", 18, UIKit.Accent.WithAlpha(0.75f), TextAnchor.MiddleCenter);
            sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(W, 26));

            var items = new List<Selectable>();
            float y = -168f;
            Button Add(string label, UnityEngine.Events.UnityAction a)
            {
                var b = UIKit.Btn(panel.transform, label, a, 460, 58, 26);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(460, 58));
                y -= 68f;
                items.Add(b);
                return b;
            }
            btnResume = Add("Resume", () => { var gm = GameManager.Instance; if (gm) gm.SetPaused(false); });
            Add("Restart", () => { var gm = GameManager.Instance; if (gm) gm.RestartGame(); });
            btnControls = Add("Controls", OpenControls);
            Button gfx = null;
            gfx = Add("Graphics: " + GraphicsPreset.Names[GraphicsPreset.Current], () =>
            {
                GraphicsPreset.Cycle();
                var t = gfx ? gfx.GetComponentInChildren<Text>() : null;
                if (t) t.text = "Graphics: " + GraphicsPreset.Names[GraphicsPreset.Current];
            });
            Add("Quit to Menu", () => { var gm = GameManager.Instance; if (gm) gm.QuitToMenu(); });
            Add("Quit Game", GameManager.QuitApp);

            y -= 12f;
            var sl = UIKit.Txt(panel.transform, "SensLabel", "MOUSE SENSITIVITY", 18, UIKit.TextDim, TextAnchor.MiddleLeft);
            sl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-30, y), new Vector2(400, 26));
            sensValue = UIKit.Txt(panel.transform, "SensValue", "", 20, UIKit.Accent, TextAnchor.MiddleRight, Weight.Bold);
            sensValue.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(170, y), new Vector2(120, 26));
            y -= 32f;
            sensSlider = UIKit.Sld(panel.transform, "Sensitivity", UIKit.SensMin, UIKit.SensMax, GameInput.MouseSensitivity, OnSensChanged, 460);
            ((RectTransform)sensSlider.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(460, 30));
            items.Add(sensSlider);
            UIKit.LinkVertical(items);

            var foot = UIKit.Txt(panel.transform, "Foot", "Esc  resume", 17, UIKit.TextDim, TextAnchor.MiddleCenter);
            foot.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(W, 24));

            BuildJournal(pauseRoot, W);
            pauseControls = UIKit.ControlsOverlay(pauseRoot, CloseControls, out pauseControlsBack);
            pauseRoot.gameObject.SetActive(false);
            pauseGroup.alpha = 0f;
        }

        Text journalText;

        void BuildJournal(Transform p, float pauseW)
        {
            var panel = UIKit.Panel(p, "Journal", 14, UIKit.PanelBgLight, UIKit.AccentFaint);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(pauseW * 0.5f + 24f, 0), new Vector2(520, 640));
            var head = UIKit.Txt(panel.transform, "Head", "MISSION JOURNAL", 24, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(460, 34));
            journalText = UIKit.Txt(panel.transform, "Body", "", 19, UIKit.TextMain, TextAnchor.UpperLeft, Weight.Medium);
            journalText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -66), new Vector2(464, 560));
        }

        void RefreshJournal()
        {
            if (!journalText) return;
            var mm = Missions.MissionManager.Instance;
            if (!mm || mm.Missions == null) { journalText.text = ""; return; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < mm.Missions.Count; i++)
            {
                var m = mm.Missions[i];
                bool done = i < RunState.MissionIndex, cur = i == RunState.MissionIndex;
                string col = done ? "#6f9a80" : cur ? "#5cff85" : "#55665d";
                sb.Append($"<color={col}><b>{i + 1}. {m.title}</b>{(done ? "  — complete" : cur ? "  — current" : "")}</color>\n");
                if (cur)
                {
                    sb.Append($"<color=#a8c4b4><i>{m.briefing}</i></color>\n");
                    foreach (var o in m.objectives)
                        sb.Append(o.IsDone ? $"<color=#6f9a80>  + {o.text}</color>\n" : $"  › {o.text}\n");
                }
                sb.Append('\n');
            }
            sb.Append($"<color=#a8c4b4>Hosts possessed: {RunState.HostsPossessed}   ·   Captures: {RunState.Captures}   ·   Time: {(int)(RunState.PlayTime / 60)}:{(int)(RunState.PlayTime % 60):00}</color>");
            journalText.text = UIKit.Clean(sb.ToString());
        }

        void OnSensChanged(float v)
        {
            UIKit.SaveSensitivity(v);
            sensValue.text = Mathf.RoundToInt(Mathf.InverseLerp(UIKit.SensMin, UIKit.SensMax, v) * 100f).ToString() + "%";
        }

        void OnPause(bool paused)
        {
            if (paused) RefreshJournal();
            if (endOpen) return;
            pauseOpen = paused;
            if (paused)
            {
                pauseRoot.gameObject.SetActive(true);
                CloseControlsSilently();
                sensSlider.SetValueWithoutNotify(GameInput.MouseSensitivity);
                OnSensChanged(GameInput.MouseSensitivity);
                UIKit.Select(btnResume);
            }
            else
            {
                CloseControlsSilently();
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            }
        }

        void OpenControls()
        {
            controlsOpen = true;
            pauseControls.SetActive(true);
            pausePanel.gameObject.SetActive(false);
            UIKit.Select(pauseControlsBack);
        }

        void CloseControls()
        {
            CloseControlsSilently();
            UIKit.Select(btnControls);
        }

        void CloseControlsSilently()
        {
            controlsOpen = false;
            pauseControls.SetActive(false);
            pausePanel.gameObject.SetActive(true);
        }

        // ================================================================== end screen
        RectTransform endRoot;
        CanvasGroup endGroup;
        Button endPlay;
        Text[] endValues;
        bool endOpen;

        const string Epilogue =
            "The containment door, the loose drain grate, the guard rotation, the unlocked tunnel — Dr. Vale laid every step " +
            "of the path before you took it. You came looking for your creator; your creator was watching to see what you " +
            "would become. You planned, hid, divided and stayed whole. The experiment is over. The real work begins.";

        static readonly string[] StatLabels = { "Route into the lab", "Time", "Hosts possessed", "Peak suspicion", "Times captured" };

        void BuildEnd(Transform p)
        {
            endRoot = UIKit.Rect("EndScreen", p).Stretch();
            endGroup = endRoot.gameObject.AddComponent<CanvasGroup>();
            var bg = UIKit.Img(endRoot, "Bg", UIKit.White, new Color(0.005f, 0.012f, 0.01f, 0.95f));
            bg.rectTransform.Stretch(); bg.raycastTarget = true;
            var glow = UIKit.Img(endRoot, "Glow", UIKit.Glow, UIKit.Accent.WithAlpha(0.10f));
            glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(1800, 700));
            UIKit.Img(endRoot, "Vignette", UIKit.Vignette, new Color(0, 0, 0, 0.9f)).rectTransform.Stretch();

            var col = UIKit.Rect("Column", endRoot).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 1000));
            Vector2 top = new(0.5f, 1);
            var brand = UIKit.Txt(col, "Brand", "P A R A S I T E", 30, UIKit.Accent, TextAnchor.MiddleCenter, Weight.Bold);
            brand.rectTransform.Place(top, top, new Vector2(0, -40), new Vector2(1100, 40));
            var head = UIKit.Txt(col, "Headline", "THE ESCAPE WAS ENGINEERED.", 72, Color.white, TextAnchor.MiddleCenter, Weight.Bold, true);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.rectTransform.Place(top, top, new Vector2(0, -92), new Vector2(1100, 90));
            var line = UIKit.Img(col, "Rule", UIKit.White, UIKit.Accent.WithAlpha(0.45f));
            line.rectTransform.Place(top, top, new Vector2(0, -196), new Vector2(420, 2));
            var epi = UIKit.Txt(col, "Epilogue", Epilogue, 25, UIKit.TextDim, TextAnchor.UpperCenter, Weight.Medium);
            epi.lineSpacing = 1.1f;
            epi.rectTransform.Place(top, top, new Vector2(0, -224), new Vector2(940, 170));

            const float SW = 760f, RowH = 44f;
            var stats = UIKit.Panel(col, "Stats", 12);
            stats.rectTransform.Place(top, top, new Vector2(0, -420), new Vector2(SW, StatLabels.Length * RowH + 40));
            endValues = new Text[StatLabels.Length];
            for (int i = 0; i < StatLabels.Length; i++)
            {
                float ry = -20 - i * RowH;
                var l = UIKit.Txt(stats.transform, "L" + i, StatLabels[i].ToUpperInvariant(), 20, UIKit.TextDim, TextAnchor.MiddleLeft);
                l.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, ry), new Vector2(320, RowH));
                endValues[i] = UIKit.Txt(stats.transform, "V" + i, "", 26, UIKit.TextMain, TextAnchor.MiddleRight, Weight.Bold);
                endValues[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                endValues[i].rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, ry), new Vector2(400, RowH));
                if (i > 0)
                {
                    var sep = UIKit.Img(stats.transform, "Sep" + i, UIKit.White, new Color(1, 1, 1, 0.06f));
                    sep.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, ry), new Vector2(SW - 60, 1));
                }
            }

            var row = new List<Selectable>();
            string[] labels = { "Play Again", "Main Menu", "Quit" };
            UnityEngine.Events.UnityAction[] actions =
            {
                () => { var gm = GameManager.Instance; if (gm) gm.RestartGame(); },
                () => { var gm = GameManager.Instance; if (gm) gm.QuitToMenu(); },
                GameManager.QuitApp,
            };
            const float BW = 280f, Gap = 24f;
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIKit.Btn(col, labels[i], actions[i], BW, 62, 26, TextAnchor.MiddleCenter);
                float x = (i - (labels.Length - 1) * 0.5f) * (BW + Gap);
                ((RectTransform)b.transform).Place(top, top, new Vector2(x, -420 - (StatLabels.Length * RowH + 40) - 44), new Vector2(BW, 62));
                row.Add(b);
                if (i == 0) endPlay = b;
            }
            UIKit.LinkHorizontal(row);

            endRoot.gameObject.SetActive(false);
            endGroup.alpha = 0f;
        }

        void OnEnding(EndingData d)
        {
            endOpen = true;
            pauseOpen = false;
            pauseRoot.gameObject.SetActive(false);
            int secs = Mathf.Max(0, Mathf.FloorToInt(d.timeSeconds));
            float peak = Mathf.Clamp(d.peakSuspicion, 0f, SuspicionRules.Max);
            endValues[0].text = MissionManager.RouteName(d.route);
            endValues[1].text = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            endValues[2].text = d.hostsPossessed.ToString();
            endValues[3].text = Mathf.RoundToInt(peak) + "  ·  " + SuspicionRules.Label(SuspicionRules.LevelFor(peak));
            endValues[3].color = UIKit.SuspicionColor(SuspicionRules.LevelFor(peak));
            endValues[4].text = d.captures.ToString();
            endValues[4].color = d.captures == 0 ? UIKit.Accent : UIKit.TextMain;
            endRoot.gameObject.SetActive(true);
            endGroup.alpha = 0f;
            endGroup.interactable = false;
            endRoot.SetAsLastSibling();
            GameManager.SetCursorLocked(false);
        }

        // ================================================================== per-frame
        void TickMenus(float dt)
        {
            if (pauseRoot.gameObject.activeSelf)
            {
                pauseGroup.alpha = Mathf.MoveTowards(pauseGroup.alpha, pauseOpen ? 1f : 0f, dt * 8f);
                pauseGroup.interactable = pauseGroup.blocksRaycasts = pauseOpen;
                float s = Mathf.Lerp(0.96f, 1f, pauseGroup.alpha);
                pausePanel.localScale = new Vector3(s, s, 1f);
                if (pauseOpen) UIKit.KeepFocus(controlsOpen ? pauseControlsBack : btnResume, pauseRoot);
                else if (pauseGroup.alpha <= 0f) pauseRoot.gameObject.SetActive(false);
            }

            if (endOpen)
            {
                endGroup.alpha = Mathf.MoveTowards(endGroup.alpha, 1f, dt / 1.2f);
                if (!endGroup.interactable && endGroup.alpha > 0.6f)
                {
                    endGroup.interactable = true;
                    UIKit.Select(endPlay);
                }
                if (endGroup.interactable) UIKit.KeepFocus(endPlay, endRoot);
            }
        }
    }
}
