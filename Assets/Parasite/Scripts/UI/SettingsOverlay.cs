using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>Shared title/pause settings. Graphics are staged; audio and input remain live.</summary>
    public class SettingsOverlay : MonoBehaviour
    {
        public Button FirstSelection { get; private set; }
        public bool HasUnappliedGraphics => draft != null && !draft.Equals(GraphicsPreset.CaptureSettings());
        const float PanelW = 1500f, PanelH = 900f, BodyW = 748f, RowH = 92f;
        readonly List<Page> pages = new();
        readonly List<Action> refresh = new();
        readonly Dictionary<GameObject, FocusInfo> focus = new();
        readonly List<Button> footer = new();
        GraphicsSettingsData draft;
        Text graphicsLabel, appliedLabel, effectiveLabel, status, detailTitle, detailText, detailCost;
        Button apply, cancel, defaults, back;
        Slider sensitivity, master, ambience, music, effects, voice;
        Text sensitivityValue, masterValue, ambienceValue, musicValue, effectsValue, voiceValue;
        UnityAction onBack;
        RectTransform panelRect;
        int activePage;
        GameObject lastSelection;

        sealed class Page
        {
            public RectTransform root, content;
            public ScrollRect scroll;
            public Button tab;
            public readonly List<Selectable> items = new();
            public float height;
        }
        sealed class FocusInfo
        {
            public RectTransform row;
            public Page page;
            public string title, description, cost;
        }

        public static SettingsOverlay Create(Transform parent, UnityAction onBack)
        {
            var root = UIKit.Rect("SettingsOverlay", parent).Stretch();
            var overlay = root.gameObject.AddComponent<SettingsOverlay>();
            overlay.Build(onBack);
            root.gameObject.SetActive(false);
            return overlay;
        }

        void Build(UnityAction close)
        {
            onBack = close;
            draft = GraphicsPreset.CaptureSettings();
            var dim = UIKit.Img(transform, "Dim", UIKit.White, new Color(0, 0, 0, .8f));
            dim.rectTransform.Stretch(); dim.raycastTarget = true;
            var panel = UIKit.Panel(transform, "Panel", 14, UIKit.PanelBgLight, UIKit.AccentSoft);
            panel.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(PanelW, PanelH));
            panelRect = panel.rectTransform;
            var title = UIKit.Txt(panel.transform, "Title", "SETTINGS", 44, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -28), new Vector2(840, 62));
            appliedLabel = UIKit.Txt(panel.transform, "AppliedGraphics", "", 21, UIKit.TextDim);
            appliedLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -94), new Vector2(900, 34));
            effectiveLabel = UIKit.Txt(panel.transform, "EffectiveGraphics", "", 19, UIKit.Yellow);
            effectiveLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -128), new Vector2(1000, 24));
            FirstSelection = UIKit.Btn(panel.transform, "Graphics", CyclePreset, 360, 64, 25, TextAnchor.MiddleCenter);
            ((RectTransform)FirstSelection.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -40), new Vector2(360, 64));
            graphicsLabel = FirstSelection.GetComponentInChildren<Text>();
            Bind(FirstSelection, null, null, "Overall preset", "Choose a complete starting profile, then adjust individual options. Graphics changes wait for Apply.", "Low / Medium / High / Ultra");
            FirstSelection.gameObject.AddComponent<SettingsCycleControl>().Initialize(direction => CyclePreset(direction));

            AddPage(panel.transform, "Display", "DISPLAY", "Image scale and frame pacing");
            AddPage(panel.transform, "Quality", "QUALITY", "Choose where to spend detail and lighting work");
            AddPage(panel.transform, "Effects", "EFFECTS", "Optional finishing effects for real-time scenes");
            AddPage(panel.transform, "Audio & Input", "AUDIO & INPUT", "These controls apply and save immediately");
            BuildDisplay(pages[0]); BuildQuality(pages[1]); BuildEffects(pages[2]); BuildAudio(pages[3]);
            foreach (var page in pages)
            {
                page.content.sizeDelta = new Vector2(0, Mathf.Max(page.height, 496));
                page.root.gameObject.SetActive(false);
            }
            var help = UIKit.Txt(panel.transform, "NavigationHelp", "Up / Down: move focus\nLeft / Right: change value\nWheel / drag: scroll\nEsc: back", 19, UIKit.TextDim, TextAnchor.UpperLeft, Weight.Medium);
            help.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(42, 192), new Vector2(208, 130));

            var details = UIKit.Panel(panel.transform, "Details", 10, new Color(.012f, .035f, .03f, .68f), UIKit.AccentFaint);
            details.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(1080, -164), new Vector2(382, 570));
            detailTitle = UIKit.Txt(details.transform, "DetailTitle", "", 29, UIKit.TextMain, TextAnchor.UpperLeft, Weight.Bold);
            detailTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -25), new Vector2(328, 84));
            detailText = UIKit.Txt(details.transform, "DetailText", "", 22, UIKit.TextDim, TextAnchor.UpperLeft, Weight.Medium);
            detailText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -125), new Vector2(328, 292));
            detailCost = UIKit.Txt(details.transform, "DetailCost", "", 20, UIKit.Accent, TextAnchor.UpperLeft);
            detailCost.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 30), new Vector2(328, 90));
            status = UIKit.Txt(panel.transform, "Status", "", 20, UIKit.TextDim, TextAnchor.MiddleLeft, Weight.Medium);
            status.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(280, 108), new Vector2(1178, 40));
            apply = Footer(panel.transform, "Apply", 280, ApplyDraft);
            cancel = Footer(panel.transform, "Cancel", 542, CancelDraft);
            defaults = Footer(panel.transform, "Defaults", 804, RestoreDefaults);
            back = Footer(panel.transform, "Back", 1100, () => onBack?.Invoke(), 360);
            Bind(apply, null, null, "Apply graphics", "Use and save the staged graphics settings. Audio and input changes are already saved.", "No display-mode change");
            Bind(cancel, null, null, "Cancel graphics changes", "Discard only the unapplied graphics changes and keep editing. Saved audio and sensitivity are preserved.", "Returns to the applied graphics profile");
            Bind(defaults, null, null, "Default graphics", "Stage the device's normal starting profile. Apply confirms it; Back discards it. Audio and input are preserved.", "Graphics only");
            Bind(back, null, null, "Back", "Return to the previous menu. Unapplied graphics changes are discarded; audio and input remain saved.", "Gameplay stays paused when returning to the pause menu");
            activePage = -1; ShowPage(0, false); RefreshAll();
        }

        Button Footer(Transform panel, string label, float x, UnityAction action, float width = 242)
        {
            var button = UIKit.Btn(panel, label, action, width, 58, 24, TextAnchor.MiddleCenter);
            ((RectTransform)button.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 35), new Vector2(width, 58));
            footer.Add(button); return button;
        }

        void AddPage(Transform parent, string name, string title, string subtitle)
        {
            int index = pages.Count;
            var page = new Page(); pages.Add(page);
            page.tab = UIKit.Btn(parent, name, () => ShowPage(index, true), 210, 58, 24);
            ((RectTransform)page.tab.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -164 - index * 72), new Vector2(210, 58));
            var trigger = page.tab.gameObject.AddComponent<EventTrigger>();
            var selected = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selected.callback.AddListener(_ => ShowPage(index, false)); trigger.triggers.Add(selected);
            Bind(page.tab, null, null, title, subtitle + ". Select an option for its description.", "Audio and input save immediately; graphics wait for Apply");
            page.root = UIKit.Rect("Page_" + name, parent);
            page.root.Stretch();
            var heading = UIKit.Txt(page.root, "PageTitle", title, 29, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Bold);
            heading.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, -154), new Vector2(754, 44));
            var note = UIKit.Txt(page.root, "PageSubtitle", subtitle, 20, UIKit.TextDim);
            note.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, -198), new Vector2(754, 30));
            var scrollRoot = UIKit.Rect("Scroll", page.root);
            scrollRoot.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, -238), new Vector2(768, 496));
            var viewport = UIKit.Img(scrollRoot, "Viewport", UIKit.White, new Color(1, 1, 1, .001f));
            viewport.raycastTarget = true; viewport.rectTransform.Stretch(0, 0, 20, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            page.content = UIKit.Rect("Content", viewport.transform);
            page.content.anchorMin = new Vector2(0, 1); page.content.anchorMax = Vector2.one; page.content.pivot = new Vector2(.5f, 1);
            page.content.anchoredPosition = Vector2.zero;
            var track = UIKit.Img(scrollRoot, "Scrollbar", UIKit.Rounded(4), UIKit.AccentFaint);
            track.raycastTarget = true;
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.pivot = new Vector2(1, .5f); track.rectTransform.sizeDelta = new Vector2(8, 0);
            var handle = UIKit.Img(track.transform, "Handle", UIKit.Rounded(4), UIKit.AccentSoft);
            handle.raycastTarget = true; handle.rectTransform.Stretch();
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            page.scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); page.scroll.viewport = viewport.rectTransform; page.scroll.content = page.content;
            page.scroll.horizontal = false; page.scroll.vertical = true; page.scroll.movementType = ScrollRect.MovementType.Clamped;
            page.scroll.scrollSensitivity = 32; page.scroll.verticalScrollbar = bar;
        }

        void BuildDisplay(Page page)
        {
            GraphicsSlider(page, "RenderScale", "Game render scale", 50, 150, () => draft.renderScale * 100,
                value => draft.renderScale = value / 100, value => Mathf.RoundToInt(value) + "%",
                "Changes the resolution used for the game world. Menus and captions stay at output resolution. Values above 100% supersample the scene.", "Higher scale increases GPU work");
            Cycle(page, "VSync", "VSync", () => draft.vSync ? "On" : "Off", _ => draft.vSync = !draft.vSync,
                "Synchronize presentation to the display when supported. The desktop compositor can also synchronize output.", "VSync may determine the effective frame rate");
            int[] limits = { -1, 30, 60, 90, 120, 144, 240 };
            Cycle(page, "FrameLimit", "Frame limit", () => draft.frameLimit < 0 ? "Unlimited" : draft.frameLimit + " FPS",
                direction => draft.frameLimit = Next(limits, draft.frameLimit, direction),
                "Limit frames per second when VSync is off. Lower caps can reduce power use and heat; they do not automatically increase visual quality.", "The cap is used when VSync is off");
        }

        void BuildQuality(Page page)
        {
            string[] aa = { "Off", "FXAA", "SMAA" };
            Cycle(page, "PostAA", "Post anti-aliasing", () => aa[draft.antiAliasing], direction => draft.antiAliasing = Mod(draft.antiAliasing + direction, aa.Length),
                "Smooth remaining image edges. FXAA is lighter; SMAA better preserves fine detail. MSAA is controlled separately below.", "SMAA uses more GPU work than FXAA");
            Cycle(page, "MSAA", "MSAA", () => draft.msaaSamples == 1 ? "Off" : draft.msaaSamples + "x", direction => draft.msaaSamples = Next(new[] { 1, 2, 4 }, draft.msaaSamples, direction),
                "Smooth geometric edges before post-processing. Unsupported sample counts fall back to the renderer's supported value.", "Higher samples use more GPU work and memory");
            Cycle(page, "Shadows", "Shadows", () => draft.shadows ? "On" : "Off", _ => draft.shadows = !draft.shadows,
                "Draw real-time shadows from the main scene light.", "Disabling shadows reduces GPU work");
            GraphicsSlider(page, "ShadowDistance", "Shadow distance", 0, 200, () => draft.shadowDistance, value => draft.shadowDistance = value,
                value => Mathf.RoundToInt(value) + " m", "Set how far real-time shadows are drawn. The distance is retained while shadows are off.", "Longer distances increase shadow work");
            Cycle(page, "AmbientOcclusion", "Ambient occlusion", () => draft.ambientOcclusion ? "On" : "Off", _ => draft.ambientOcclusion = !draft.ambientOcclusion,
                "Add contact shading around nearby objects and corners in real-time scenes.", "Screen-space contact shading adds GPU work");
            string[] reflection = { "Sky fallback", "SSR Standard", "SSR High", "SSR Ultra" };
            Cycle(page, "Reflections", "Reflections", () => reflection[draft.reflectionLevel], direction => draft.reflectionLevel = Mod(draft.reflectionLevel + direction, reflection.Length),
                "Choose the water's reflection detail. Standard reflects scenery visible on screen. High and Ultra also use a captured surrounding environment where supported.", "Higher levels use more GPU work");
        }

        void BuildEffects(Page page)
        {
            Cycle(page, "Bloom", "Bloom", () => draft.bloom ? "On" : "Off", _ => draft.bloom = !draft.bloom,
                "Give bright real-time lights and signs a soft glow. This does not change lighting already rendered into the story movies.", "Optional finishing effect");
            Cycle(page, "FilmGrain", "Film grain", () => draft.filmGrain ? "On" : "Off", _ => draft.filmGrain = !draft.filmGrain,
                "Keep or remove the authored grain in real-time scenes. The existing movies retain their rendered appearance.", "Turn off for a cleaner gameplay image");
            Cycle(page, "ChromaticAberration", "Chromatic aberration", () => draft.chromaticAberration ? "On" : "Off", _ => draft.chromaticAberration = !draft.chromaticAberration,
                "Keep or remove the color separation near screen edges in real-time scenes. Existing movies are unchanged.", "Turn off for clearer edges");
        }

        void BuildAudio(Page page)
        {
            sensitivity = LiveSlider(page, "Sensitivity", "Mouse sensitivity", UIKit.SensMin, UIKit.SensMax, GameInput.MouseSensitivity,
                value => { UIKit.SaveSensitivity(value); RefreshLiveValues(); }, out sensitivityValue, "Adjust how quickly mouse movement turns the camera.");
            master = LiveSlider(page, "MasterVolume", "Master volume", 0, 1, AudioPreferences.MasterVolume,
                value => { AudioPreferences.SetMaster(value); RefreshLiveValues(); }, out masterValue, "Set the overall game volume.");
            music = LiveSlider(page, "MusicVolume", "Music", 0, 1, AudioPreferences.MusicVolume,
                value => { AudioPreferences.SetMusic(value); RefreshLiveValues(); }, out musicValue, "Set the score and background music volume.");
            ambience = LiveSlider(page, "AmbienceVolume", "Ambience", 0, 1, AudioPreferences.AmbienceVolume,
                value => { AudioPreferences.SetAmbience(value); RefreshLiveValues(); }, out ambienceValue, "Set rain, wind and environmental ambience volume.");
            effects = LiveSlider(page, "EffectsVolume", "Sound effects", 0, 1, AudioPreferences.EffectsVolume,
                value => { AudioPreferences.SetEffects(value); RefreshLiveValues(); }, out effectsValue, "Set action, interaction and menu sound volume.");
            voice = LiveSlider(page, "VoiceVolume", "Dialogue", 0, 1, AudioPreferences.VoiceVolume,
                value => { AudioPreferences.SetVoice(value); RefreshLiveValues(); }, out voiceValue, "Set spoken dialogue and narration volume. Captions remain available.");
        }

        RectTransform Row(Page page, string name, string label)
        {
            var row = UIKit.Rect("Row_" + name, page.content);
            row.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -page.height), new Vector2(BodyW, RowH));
            page.height += RowH;
            var text = UIKit.Txt(row, "Label", label, 24, UIKit.TextMain, TextAnchor.MiddleLeft);
            text.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -8), new Vector2(438, 38));
            var line = UIKit.Img(row, "Divider", UIKit.White, UIKit.AccentFaint);
            line.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 3), new Vector2(BodyW - 20, 1));
            return row;
        }

        void Cycle(Page page, string name, string label, Func<string> value, Action<int> change, string description, string cost)
        {
            var row = Row(page, name, label);
            var button = UIKit.Btn(row, name, () => { change(1); Changed(); }, 268, 50, 23, TextAnchor.MiddleCenter);
            ((RectTransform)button.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(268, 50));
            button.gameObject.AddComponent<SettingsCycleControl>().Initialize(direction => { change(direction); Changed(); });
            var readout = button.GetComponentInChildren<Text>();
            refresh.Add(() => readout.text = "‹  " + value() + "  ›");
            page.items.Add(button); Bind(button, page, row, label, description, cost);
        }

        void GraphicsSlider(Page page, string name, string label, float min, float max, Func<float> value, Action<float> change, Func<float, string> format, string description, string cost)
        {
            var slider = LiveSlider(page, name, label, min, max, value(), null, out var readout, description, cost);
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(next => { change(next); Changed(); });
            refresh.Add(() => { slider.SetValueWithoutNotify(value()); readout.text = format(value()); });
        }

        Slider LiveSlider(Page page, string name, string label, float min, float max, float value, UnityAction<float> change, out Text readout, string description, string cost = "Applies and saves immediately")
        {
            var row = Row(page, name, label);
            var slider = UIKit.Sld(row, name, min, max, value, change, BodyW - 24);
            ((RectTransform)slider.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 20), new Vector2(BodyW - 24, 26));
            readout = UIKit.Txt(row, name + "Value", "", 23, UIKit.Accent, TextAnchor.MiddleRight, Weight.Bold);
            readout.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -8), new Vector2(218, 36));
            page.items.Add(slider); Bind(slider, page, row, label, description, cost);
            return slider;
        }

        void Bind(Selectable selectable, Page page, RectTransform row, string title, string description, string cost)
        {
            focus[selectable.gameObject] = new FocusInfo { page = page, row = row, title = title, description = description, cost = cost };
            var trigger = selectable.GetComponent<EventTrigger>();
            if (!trigger) trigger = selectable.gameObject.AddComponent<EventTrigger>();
            var entered = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entered.callback.AddListener(_ => { if (selectable.IsInteractable()) UIKit.Select(selectable); }); trigger.triggers.Add(entered);
        }

        void Update()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != lastSelection)
            {
                lastSelection = selected;
                if (selected && focus.TryGetValue(selected, out var info))
                {
                    detailTitle.text = info.title; detailText.text = info.description; detailCost.text = info.cost;
                    ScrollTo(info);
                }
            }
        }

        static void ScrollTo(FocusInfo info)
        {
            if (info.page == null || !info.row) return;
            Canvas.ForceUpdateCanvases();
            var scroll = info.page.scroll; var viewport = scroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, info.row);
            float offset = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y : bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0;
            if (Mathf.Abs(offset) < .01f) return;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0, Mathf.Max(0, scroll.content.rect.height - viewport.rect.height));
            scroll.StopMovement(); scroll.content.anchoredPosition = position;
        }

        void ShowPage(int index, bool selectFirst)
        {
            if (activePage != index)
            {
                activePage = index;
                for (int i = 0; i < pages.Count; i++) pages[i].root.gameObject.SetActive(i == index);
                pages[index].scroll.verticalNormalizedPosition = 1;
            }
            RebuildNavigation();
            if (selectFirst && pages[index].items.Count > 0) UIKit.Select(pages[index].items[0]);
        }

        void RebuildNavigation()
        {
            if (activePage < 0 || apply == null) return;
            var page = pages[activePage];
            Selectable first = page.items.Count > 0 ? page.items[0] : FirstSelection;
            Selectable last = page.items.Count > 0 ? page.items[page.items.Count - 1] : FirstSelection;
            FirstSelection.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = first, selectOnUp = pages[activePage].tab };
            for (int i = 0; i < pages.Count; i++)
                pages[i].tab.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = i > 0 ? pages[i - 1].tab : FirstSelection, selectOnDown = i + 1 < pages.Count ? pages[i + 1].tab : back, selectOnRight = first };
            for (int i = 0; i < page.items.Count; i++)
                page.items[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = i > 0 ? page.items[i - 1] : FirstSelection, selectOnDown = i + 1 < page.items.Count ? page.items[i + 1] : HasUnappliedGraphics ? apply : back };
            var available = new List<Selectable>();
            foreach (var button in footer)
                if (button.interactable) available.Add(button);
            UIKit.LinkHorizontal(available);
            foreach (var selectable in available)
            {
                var nav = selectable.navigation; nav.selectOnUp = last; nav.selectOnDown = FirstSelection; selectable.navigation = nav;
            }
        }

        void CyclePreset() => CyclePreset(1);
        void CyclePreset(int direction)
        {
            var next = GraphicsSettingsData.ForPreset(Mod(draft.preset + direction, GraphicsPreset.Names.Length));
            next.vSync = draft.vSync; next.frameLimit = draft.frameLimit;
            draft = next; RefreshAll();
        }
        void Changed() { draft = draft.Normalized(); RefreshAll(); }
        void ApplyDraft() { GraphicsPreset.ApplySettings(draft); draft = GraphicsPreset.CaptureSettings(); RefreshAll(); UIKit.Select(back); }
        void CancelDraft() { draft = GraphicsPreset.CaptureSettings(); RefreshAll(); UIKit.Select(FirstSelection); }
        void RestoreDefaults() { draft = GraphicsSettingsData.ForPreset(SystemInfo.graphicsMemorySize >= 4000 ? 3 : 2); RefreshAll(); }
        static int Mod(int value, int count) => (value % count + count) % count;
        static int Next(int[] values, int value, int direction)
        {
            int index = Array.IndexOf(values, value); return values[Mod((index < 0 ? 0 : index) + direction, values.Length)];
        }
        static string ProfileName(GraphicsSettingsData settings) => settings.Equals(GraphicsSettingsData.ForPreset(settings.preset)) ? GraphicsPreset.Names[settings.preset] : "Custom (" + GraphicsPreset.Names[settings.preset] + ")";

        void RefreshAll()
        {
            foreach (var update in refresh) update();
            graphicsLabel.text = "Graphics: " + ProfileName(draft);
            appliedLabel.text = "Applied graphics: " + GraphicsPreset.DisplayName;
            if (effectiveLabel)
            {
                var active = GraphicsPreset.CaptureSettings();
                var notes = new List<string>();
                if (active.msaaSamples != GraphicsPreset.EffectiveMsaaSamples)
                    notes.Add("MSAA: " + (GraphicsPreset.EffectiveMsaaSamples > 1 ? GraphicsPreset.EffectiveMsaaSamples + "x active" : "Off on this renderer"));
                if (active.ambientOcclusion && !GraphicsPreset.AmbientOcclusionAvailable) notes.Add("Ambient occlusion unavailable");
                if (active.reflectionLevel >= 2 && !GraphicsPreset.RealtimeReflectionsEnabled) notes.Add("Environment capture unavailable");
                effectiveLabel.text = string.Join(" · ", notes);
            }
            bool dirty = HasUnappliedGraphics;
            if (apply) apply.interactable = dirty;
            if (cancel) cancel.interactable = dirty;
            if (status) status.text = dirty ? "Unapplied graphics changes. Apply to save; Back discards them." : "Graphics are saved. Audio and input apply immediately.";
            RefreshLiveValues(); RebuildNavigation();
        }

        void RefreshLiveValues()
        {
            if (sensitivityValue) sensitivityValue.text = Mathf.RoundToInt(Mathf.InverseLerp(UIKit.SensMin, UIKit.SensMax, GameInput.MouseSensitivity) * 100) + "%";
            if (masterValue) masterValue.text = Mathf.RoundToInt(AudioPreferences.MasterVolume * 100) + "%";
            if (ambienceValue) ambienceValue.text = Mathf.RoundToInt(AudioPreferences.AmbienceVolume * 100) + "%";
            if (musicValue) musicValue.text = Mathf.RoundToInt(AudioPreferences.MusicVolume * 100) + "%";
            if (effectsValue) effectsValue.text = Mathf.RoundToInt(AudioPreferences.EffectsVolume * 100) + "%";
            if (voiceValue) voiceValue.text = Mathf.RoundToInt(AudioPreferences.VoiceVolume * 100) + "%";
        }

        public void Open()
        {
            draft = GraphicsPreset.CaptureSettings();
            sensitivity.SetValueWithoutNotify(GameInput.MouseSensitivity); master.SetValueWithoutNotify(AudioPreferences.MasterVolume);
            ambience.SetValueWithoutNotify(AudioPreferences.AmbienceVolume); music.SetValueWithoutNotify(AudioPreferences.MusicVolume);
            effects.SetValueWithoutNotify(AudioPreferences.EffectsVolume); voice.SetValueWithoutNotify(AudioPreferences.VoiceVolume);
            RefreshAll(); gameObject.SetActive(true); Canvas.ForceUpdateCanvases(); FitPanel(); lastSelection = null; UIKit.Select(FirstSelection);
        }

        void OnRectTransformDimensionsChange() => FitPanel();
        void FitPanel()
        {
            if (!panelRect) return;
            var available = ((RectTransform)transform).rect.size;
            if (available.x <= 0 || available.y <= 0) return;
            float scale = Mathf.Clamp(Mathf.Min((available.x - 48) / PanelW, (available.y - 48) / PanelH), .1f, 1f);
            panelRect.localScale = Vector3.one * scale;
        }

        public void Close() { draft = GraphicsPreset.CaptureSettings(); gameObject.SetActive(false); }
        void OnDisable() => PlayerPrefs.Save();
    }
}
