using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>Shared title/pause settings with live previews and saved preferences.</summary>
    public class SettingsOverlay : MonoBehaviour
    {
        public Button FirstSelection { get; private set; }
        Text graphicsLabel, graphicsDetail, sensitivityValue, masterValue, ambienceValue, musicValue, effectsValue, voiceValue;
        Slider sensitivity, master, ambience, music, effects, voice;
        const float PanelW = 1120f, ColW = 488f;

        public static SettingsOverlay Create(Transform parent, UnityAction onBack)
        {
            var root = UIKit.Rect("SettingsOverlay", parent).Stretch();
            var overlay = root.gameObject.AddComponent<SettingsOverlay>();
            overlay.Build(onBack);
            root.gameObject.SetActive(false);
            return overlay;
        }

        void Build(UnityAction onBack)
        {
            var dim = UIKit.Img(transform, "Dim", UIKit.White, new Color(0, 0, 0, 0.7f));
            dim.rectTransform.Stretch(); dim.raycastTarget = true;
            var panel = UIKit.Panel(transform, "Panel", 14, UIKit.PanelBgLight, UIKit.AccentSoft);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, 760));
            var title = UIKit.Txt(panel.transform, "Title", "SETTINGS", 44, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -32), new Vector2(PanelW - 96, 60));
            var items = new List<Selectable>();
            FirstSelection = UIKit.Btn(panel.transform, "Graphics", () => { GraphicsPreset.Cycle(); RefreshGraphics(); }, PanelW - 96, 60, 26);
            ((RectTransform)FirstSelection.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(PanelW - 96, 60));
            graphicsLabel = FirstSelection.GetComponentInChildren<Text>();
            graphicsDetail = UIKit.Txt(panel.transform, "GraphicsDetail", "", 20, UIKit.TextDim, TextAnchor.UpperLeft, Weight.Medium);
            graphicsDetail.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -192), new Vector2(PanelW - 96, 54));
            items.Add(FirstSelection);

            // left column: input + master/music; right column: ambience, effects, dialogue
            const float left = 48f, right = PanelW - 48f - ColW;
            sensitivity = AddSlider(panel.transform, "MOUSE SENSITIVITY", "Sensitivity", left, -270, UIKit.SensMin, UIKit.SensMax,
                GameInput.MouseSensitivity, value => { UIKit.SaveSensitivity(value); RefreshValues(); }, out sensitivityValue);
            master = AddSlider(panel.transform, "MASTER VOLUME", "MasterVolume", left, -376, 0, 1,
                AudioPreferences.MasterVolume, value => { AudioPreferences.SetMaster(value); RefreshValues(); }, out masterValue);
            music = AddSlider(panel.transform, "MUSIC", "MusicVolume", left, -482, 0, 1,
                AudioPreferences.MusicVolume, value => { AudioPreferences.SetMusic(value); RefreshValues(); }, out musicValue);
            ambience = AddSlider(panel.transform, "AMBIENCE", "AmbienceVolume", right, -270, 0, 1,
                AudioPreferences.AmbienceVolume, value => { AudioPreferences.SetAmbience(value); RefreshValues(); }, out ambienceValue);
            effects = AddSlider(panel.transform, "SOUND EFFECTS", "EffectsVolume", right, -376, 0, 1,
                AudioPreferences.EffectsVolume, value => { AudioPreferences.SetEffects(value); RefreshValues(); }, out effectsValue);
            voice = AddSlider(panel.transform, "DIALOGUE", "VoiceVolume", right, -482, 0, 1,
                AudioPreferences.VoiceVolume, value => { AudioPreferences.SetVoice(value); RefreshValues(); }, out voiceValue);
            items.Add(sensitivity); items.Add(master); items.Add(music); items.Add(ambience); items.Add(effects); items.Add(voice);

            var note = UIKit.Txt(panel.transform, "Note", "Changes apply immediately and are saved when you return.", 20, UIKit.TextDim, TextAnchor.MiddleCenter, Weight.Medium);
            note.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 114), new Vector2(PanelW - 96, 36));
            var back = UIKit.Btn(panel.transform, "Back", onBack, 240, 56, 24, TextAnchor.MiddleCenter);
            ((RectTransform)back.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(240, 56));
            items.Add(back);
            UIKit.LinkVertical(items);
        }

        static Slider AddSlider(Transform parent, string label, string name, float x, float y, float min, float max, float value,
            UnityAction<float> onChange, out Text readout)
        {
            var heading = UIKit.Txt(parent, name + "Label", label, 21, UIKit.TextMain, TextAnchor.MiddleLeft);
            heading.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(ColW - 120, 30));
            readout = UIKit.Txt(parent, name + "Value", "", 22, UIKit.Accent, TextAnchor.MiddleRight, Weight.Bold);
            readout.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + ColW - 120, y), new Vector2(120, 30));
            var slider = UIKit.Sld(parent, name, min, max, value, onChange, ColW);
            ((RectTransform)slider.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y - 40), new Vector2(ColW, 30));
            return slider;
        }

        public void Open()
        {
            sensitivity.SetValueWithoutNotify(GameInput.MouseSensitivity);
            master.SetValueWithoutNotify(AudioPreferences.MasterVolume);
            ambience.SetValueWithoutNotify(AudioPreferences.AmbienceVolume);
            music.SetValueWithoutNotify(AudioPreferences.MusicVolume);
            effects.SetValueWithoutNotify(AudioPreferences.EffectsVolume);
            voice.SetValueWithoutNotify(AudioPreferences.VoiceVolume);
            RefreshGraphics(); RefreshValues();
            gameObject.SetActive(true);
            UIKit.Select(FirstSelection);
        }

        public void Close() => gameObject.SetActive(false);

        void OnDisable() => PlayerPrefs.Save();

        void RefreshGraphics()
        {
            graphicsLabel.text = "Graphics: " + GraphicsPreset.Names[GraphicsPreset.Current];
            string[] detail =
            {
                "70% render scale · short shadows · 6 lamps · probe reflections · FXAA",
                "85% render scale · medium shadows · 14 lamps · 16-step SSR · FXAA",
                "Native resolution · long shadows · 32 lamps · SSAO · 28-step SSR · SMAA",
                "125% supersampling · ultra shadows · 64 lamps · HQ SSAO · 36-step SSR · SMAA + 4x MSAA",
            };
            graphicsDetail.text = detail[GraphicsPreset.Current];
        }

        void RefreshValues()
        {
            // UI factory sets slider values before all widgets have been created.
            if (sensitivityValue) sensitivityValue.text = Mathf.RoundToInt(Mathf.InverseLerp(UIKit.SensMin, UIKit.SensMax, GameInput.MouseSensitivity) * 100f) + "%";
            if (masterValue) masterValue.text = Mathf.RoundToInt(AudioPreferences.MasterVolume * 100f) + "%";
            if (ambienceValue) ambienceValue.text = Mathf.RoundToInt(AudioPreferences.AmbienceVolume * 100f) + "%";
            if (musicValue) musicValue.text = Mathf.RoundToInt(AudioPreferences.MusicVolume * 100f) + "%";
            if (effectsValue) effectsValue.text = Mathf.RoundToInt(AudioPreferences.EffectsVolume * 100f) + "%";
            if (voiceValue) voiceValue.text = Mathf.RoundToInt(AudioPreferences.VoiceVolume * 100f) + "%";
        }
    }
}
