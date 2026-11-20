using System.Collections.Generic;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>Pause menu, controls/settings overlays, journal and end screen.</summary>
    public partial class HUDController
    {
        // ================================================================== pause
        RectTransform pauseRoot, pausePanel, pauseContent;
        CanvasGroup pauseGroup;
        Button btnResume, btnControls, btnSettings;
        SettingsOverlay pauseSettings;
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

            const float W = 580f, H = 660f;
            // Center the complete menu/journal pair so both fit narrow laptop aspect ratios.
            pauseContent = UIKit.Rect("Content", pauseRoot).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1124, H));
            var panel = UIKit.Panel(pauseContent, "Panel", 14, UIKit.PanelBgLight, UIKit.AccentSoft);
            pausePanel = panel.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(W, H));
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
            btnSettings = Add("Settings", OpenSettings);
            Add("Quit to Menu", () => { var gm = GameManager.Instance; if (gm) gm.QuitToMenu(); });
            Add("Quit Game", GameManager.QuitApp);

            UIKit.LinkVertical(items);

            var foot = UIKit.Txt(panel.transform, "Foot", "Esc  resume", 17, UIKit.TextDim, TextAnchor.MiddleCenter);
            foot.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(W, 24));

            BuildJournal(pauseContent, W);
            pauseControls = UIKit.ControlsOverlay(pauseRoot, CloseControls, out pauseControlsBack);
            pauseSettings = SettingsOverlay.Create(pauseRoot, CloseSettings);
            pauseRoot.gameObject.SetActive(false);
            pauseGroup.alpha = 0f;
        }

        Text journalProgress, journalStats;
        ScrollRect journalScroll;
        readonly List<Text> journalEntries = new();

        void BuildJournal(Transform p, float pauseW)
        {
            var panel = UIKit.Panel(p, "Journal", 14, UIKit.PanelBgLight, UIKit.AccentFaint);
            panel.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0f, 0.5f), new Vector2(pauseW + 24f, 0), new Vector2(520, 640));
            var head = UIKit.Txt(panel.transform, "Head", "MISSION JOURNAL", 24, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(320, 34));
            journalProgress = UIKit.Txt(panel.transform, "Progress", "", 22, UIKit.Accent, TextAnchor.MiddleRight, Weight.Bold);
            journalProgress.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -22), new Vector2(100, 34));

            var scrollRoot = UIKit.Rect("ScrollView", panel.transform).Stretch(28, 82, 24, 74);
            var viewport = UIKit.Img(scrollRoot, "Viewport", UIKit.White, new Color(0, 0, 0, 0.001f));
            viewport.raycastTarget = true;
            viewport.rectTransform.Stretch(0, 0, 24, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;

            var track = UIKit.Img(scrollRoot, "Scrollbar", UIKit.Rounded(4), UIKit.AccentFaint);
            track.raycastTarget = true;
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.pivot = new Vector2(1, 0.5f); track.rectTransform.sizeDelta = new Vector2(10, 0);
            var handle = UIKit.Img(track.transform, "Handle", UIKit.Rounded(4), UIKit.AccentSoft);
            handle.raycastTarget = true; handle.rectTransform.Stretch();
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = btnResume, selectOnUp = btnResume, selectOnDown = btnResume };

            journalScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            journalScroll.viewport = viewport.rectTransform; journalScroll.content = content;
            journalScroll.horizontal = false; journalScroll.vertical = true;
            journalScroll.movementType = ScrollRect.MovementType.Clamped;
            journalScroll.scrollSensitivity = 32f; journalScroll.verticalScrollbar = scrollbar;

            journalStats = UIKit.Txt(panel.transform, "Stats", "", 18, UIKit.TextDim, TextAnchor.MiddleLeft, Weight.Medium);
            journalStats.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 38), new Vector2(464, 30));
            var hint = UIKit.Txt(panel.transform, "ScrollHint", "Wheel / drag  ·  PgUp/PgDn  ·  Right stick", 16, UIKit.TextDim, TextAnchor.MiddleLeft, Weight.Medium);
            hint.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 12), new Vector2(464, 24));
        }

        void RefreshJournal()
        {
            if (!journalScroll) return;
            var mm = Missions.MissionManager.Instance;
            if (!mm || mm.Missions == null) return;
            int active = Mathf.Clamp(RunState.MissionIndex, 0, mm.Missions.Count - 1);
            journalProgress.text = $"{active + 1:00} / {mm.Missions.Count:00}";
            float y = 0f, currentTop = 0f;
            for (int i = 0; i < mm.Missions.Count; i++)
            {
                if (i >= journalEntries.Count)
                    journalEntries.Add(UIKit.Txt(journalScroll.content, "Mission" + (i + 1), "", 19, UIKit.TextMain, TextAnchor.UpperLeft, Weight.Medium));
                var entry = journalEntries[i];
                entry.gameObject.SetActive(true);
                entry.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(journalScroll.viewport.rect.width - 4f, 30));
                var m = mm.Missions[i];
                bool done = i < RunState.MissionIndex, cur = i == RunState.MissionIndex;
                if (cur) currentTop = y;
                string col = done ? "#6f9a80" : cur ? "#5cff85" : "#879d90";
                var sb = new System.Text.StringBuilder();
                sb.Append($"<color={col}><size=22><b>{i + 1}. {m.title}</b></size>{(done ? "  — complete" : cur ? "  — current" : "")}</color>");
                if (cur)
                {
                    sb.Append($"\n<color=#a8c4b4><i>{m.briefing}</i></color>\n");
                    foreach (var o in m.objectives)
                    {
                        string extra = !o.IsDone && o.extra != null ? o.extra() : "";
                        sb.Append(o.IsDone ? $"\n<color=#6f9a80>+ {o.text}</color>" : $"\n› {o.text}{extra}");
                    }
                }
                entry.text = UIKit.Clean(sb.ToString());
                float height = Mathf.Max(32f, entry.preferredHeight);
                entry.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                y += height + 20f;
            }
            for (int i = mm.Missions.Count; i < journalEntries.Count; i++) journalEntries[i].gameObject.SetActive(false);
            journalStats.text = $"Hosts: {RunState.HostsPossessed}   ·   Captures: {RunState.Captures}   ·   Time: {(int)(RunState.PlayTime / 60)}:{(int)(RunState.PlayTime % 60):00}";
            journalScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
            journalScroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            float overflow = Mathf.Max(0f, y - journalScroll.viewport.rect.height);
            journalScroll.verticalNormalizedPosition = overflow > 0f ? 1f - Mathf.Clamp01(currentTop / overflow) : 1f;
        }

        void OnPause(bool paused)
        {
            if (endOpen) return;
            pauseOpen = paused;
            if (paused)
            {
                pauseRoot.gameObject.SetActive(true);
                CloseControlsSilently();
                Canvas.ForceUpdateCanvases();
                RefreshJournal();
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
            pauseContent.gameObject.SetActive(false);
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
            pauseSettings.Close();
            pauseContent.gameObject.SetActive(true);
        }

        void OpenSettings()
        {
            pauseContent.gameObject.SetActive(false);
            pauseSettings.Open();
        }

        void CloseSettings()
        {
            pauseSettings.Close();
            pauseContent.gameObject.SetActive(true);
            UIKit.Select(btnSettings);
        }

        /// <summary>Esc/Start goes back one level before resuming the game.</summary>
        public bool TryClosePauseSubmenu()
        {
            if (!pauseOpen) return false;
            if (pauseSettings.gameObject.activeSelf) { CloseSettings(); return true; }
            if (controlsOpen) { CloseControls(); return true; }
            return false;
        }

        // ================================================================== end screen
        RectTransform endRoot;
        CanvasGroup endGroup;
        Button endPlay;
        Text[] endValues;
        bool endOpen;

        const string Epilogue =
            "Vale opened the first door. You chose the last one. His signed orders are now public, his containment relay " +
            "is dark, and Specimen 7 has vanished through the storm drain. Two bodies carried the evidence beyond the lab — " +
            "and one mind finally escaped the experiment.";

        static readonly string[] StatLabels = { "Infiltration route", "Time to freedom", "Hosts possessed", "Peak suspicion", "Times captured" };

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
            var head = UIKit.Txt(col, "Headline", "THE EVIDENCE IS OUT.", 72, Color.white, TextAnchor.MiddleCenter, Weight.Bold, true);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.rectTransform.Place(top, top, new Vector2(0, -92), new Vector2(1100, 90));
            var line = UIKit.Img(col, "Rule", UIKit.White, UIKit.Accent.WithAlpha(0.45f));
            line.rectTransform.Place(top, top, new Vector2(0, -196), new Vector2(420, 2));
            var outcome = UIKit.Txt(col, "Outcome", "EVIDENCE PUBLIC  ·  CONTAINMENT OFFLINE  ·  SPECIMEN FREE", 20, UIKit.Accent, TextAnchor.MiddleCenter, Weight.Bold);
            outcome.rectTransform.Place(top, top, new Vector2(0, -214), new Vector2(1000, 30));
            var epi = UIKit.Txt(col, "Epilogue", Epilogue, 25, UIKit.TextDim, TextAnchor.UpperCenter, Weight.Medium);
            epi.lineSpacing = 1.1f;
            epi.rectTransform.Place(top, top, new Vector2(0, -258), new Vector2(940, 145));

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
                pauseContent.localScale = new Vector3(s, s, 1f);
                if (pauseOpen)
                {
                    var pad = Gamepad.current;
                    if (pad != null && pad.buttonEast.wasPressedThisFrame) TryClosePauseSubmenu();
                    if (!controlsOpen && !pauseSettings.gameObject.activeSelf) TickJournalScroll(dt);
                    UIKit.KeepFocus(pauseSettings.gameObject.activeSelf ? pauseSettings.FirstSelection : controlsOpen ? pauseControlsBack : btnResume, pauseRoot);
                }
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

        void TickJournalScroll(float dt)
        {
            if (!journalScroll || journalScroll.content.rect.height <= journalScroll.viewport.rect.height) return;
            float direction = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageUpKey.isPressed) direction += 1f;
                if (keyboard.pageDownKey.isPressed) direction -= 1f;
            }
            var pad = Gamepad.current;
            if (pad != null && Mathf.Abs(pad.rightStick.y.ReadValue()) > 0.2f) direction += pad.rightStick.y.ReadValue();
            if (Mathf.Abs(direction) < 0.01f) return;
            journalScroll.StopMovement();
            float overflow = journalScroll.content.rect.height - journalScroll.viewport.rect.height;
            journalScroll.verticalNormalizedPosition = Mathf.Clamp01(journalScroll.verticalNormalizedPosition + direction * dt * journalScroll.viewport.rect.height / overflow);
        }
    }
}
