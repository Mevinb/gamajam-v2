using System.Linq;
using Parasite.FX;
using Parasite.Possession;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>After extraction: a quiet borrowed life, then an explicit choice before the statistics.</summary>
    public class LakesideEpilogue : MonoBehaviour
    {
        enum Phase { Arrival, Choice, Resolution, Summary }
        public static LakesideEpilogue Instance { get; private set; }
        public EndingOutcome Outcome => ending.outcome;
        public bool ChoicesReady => phase == Phase.Choice && choiceGroup && choiceGroup.interactable;
        public Camera SceneCamera => set != null ? set.Camera : null;
        public Transform SeatedGirl => set != null ? set.Girl : null;

        EndingData ending;
        Phase phase;
        float age, phaseAge;
        LakesideSet set;
        CinematicFilm film;
        Canvas canvas, hudCanvas;
        bool oldHudEnabled, oldPlayerCameraEnabled;
        PlayerCamera playerCamera;
        Text caption, context, beat;
        CanvasGroup choiceGroup;
        GameObject choices;
        Button quiet, world, continueToChoice, summary;
        AudioSource shore;
        AudioClip shoreClip;
        (AudioSource source, bool mute)[] audioSnapshot;

        public static void Begin(EndingData data)
        {
            if (Instance) return;
            var epilogue = new GameObject("Lakeside Epilogue").AddComponent<LakesideEpilogue>();
            try { epilogue.Initialize(data); }
            catch { Object.Destroy(epilogue.gameObject); throw; }
        }

        void Awake() => Instance = this;

        void Initialize(EndingData data)
        {
            ending = data; ending.outcome = EndingOutcome.Unchosen;
            GameEvents.RaiseSubtitle("", "");
            var hud = HUDController.Instance;
            if (hud) { hudCanvas = hud.GetComponentInChildren<Canvas>(); oldHudEnabled = hudCanvas && hudCanvas.enabled; if (hudCanvas) hudCanvas.enabled = false; }
            playerCamera = PlayerCamera.Instance;
            if (playerCamera) { oldPlayerCameraEnabled = playerCamera.enabled; playerCamera.enabled = false; }
            audioSnapshot = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Select(source => (source, source.mute)).ToArray();
            foreach (var state in audioSnapshot) state.source.mute = true;
            set = new LakesideSet(); set.Build();
            BuildUI(); BuildSound();
            film = new CinematicFilm("LakesideEnding", canvas.transform);
            GameManager.SetCursorLocked(false);
            UIKit.Select(continueToChoice);
        }

        void BuildUI()
        {
            UIKit.EnsureEventSystem();
            canvas = UIKit.CreateCanvas("Lakeside Epilogue Canvas", 60, transform);
            var root = (RectTransform)canvas.transform;
            var shield = UIKit.Img(root, "InputShield", UIKit.White, Color.clear); shield.rectTransform.Stretch(); shield.raycastTarget = true;
            var top = UIKit.Img(root, "Top matte", UIKit.White, Color.black); top.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(2400, 70));
            var bottom = UIKit.Img(root, "Bottom matte", UIKit.White, Color.black); bottom.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(2400, 64));
            var shade = UIKit.Img(root, "Text shade", UIKit.GradientV, new Color(0.025f, 0.028f, 0.045f, 0.92f));
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(1, 0.52f); shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            context = UIKit.Txt(root, "Place", "SOME TIME LATER  /  BEYOND THE CITY", 20, new Color(1f, 0.86f, 0.65f), TextAnchor.MiddleCenter, Weight.Medium, true);
            context.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(1400, 34));
            caption = UIKit.Txt(root, "Caption", "The city kept moving.\nSomewhere beyond it, the water was still.", 38, new Color(1f, 0.94f, 0.84f), TextAnchor.MiddleCenter, Weight.Medium, true);
            caption.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(1320, 116));
            beat = UIKit.Txt(root, "Beat", "", 22, new Color(0.9f, 0.83f, 0.76f), TextAnchor.MiddleCenter, Weight.Medium, true);
            beat.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(1320, 44));
            continueToChoice = UIKit.Btn(root, "Continue to choice", RevealChoices, 360, 54, 24, TextAnchor.MiddleCenter);
            ((RectTransform)continueToChoice.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(360, 54));

            choices = UIKit.Rect("Choices", root).gameObject;
            ((RectTransform)choices.transform).Stretch();
            choiceGroup = choices.AddComponent<CanvasGroup>(); choiceGroup.interactable = choiceGroup.blocksRaycasts = false;
            quiet = Choice("Live quietly", "Keep one small life. Let the world remain its own.", -325, () => Choose(EndingOutcome.Peace));
            world = Choice("Become the world", "Infect new hosts. Build a world ruled by one mind.", 325, () => Choose(EndingOutcome.Dominion));
            UIKit.LinkHorizontal(new Selectable[] { quiet, world });
            choices.SetActive(false);
            summary = UIKit.Btn(root, "View your journey", FinishResolution, 360, 54, 24, TextAnchor.MiddleCenter);
            ((RectTransform)summary.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(360, 54));
            summary.gameObject.SetActive(false);
        }

        Button Choice(string label, string description, float x, UnityEngine.Events.UnityAction action)
        {
            var button = UIKit.Btn(choices.transform, label, action, 620, 72, 32, TextAnchor.MiddleCenter);
            ((RectTransform)button.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x, 170), new Vector2(620, 72));
            var detail = UIKit.Txt(choices.transform, label + "Detail", description, 22, new Color(0.94f, 0.88f, 0.78f), TextAnchor.UpperCenter, Weight.Medium, true);
            detail.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(x, 150), new Vector2(600, 62));
            return button;
        }

        public void RevealChoices()
        {
            if (phase != Phase.Arrival) return;
            phase = Phase.Choice; phaseAge = 0f;
            continueToChoice.gameObject.SetActive(false);
            caption.text = "What will you become?";
            caption.rectTransform.anchoredPosition = new Vector2(0, 290);
            beat.text = "No one was writing the answer now.";
            beat.rectTransform.anchoredPosition = new Vector2(0, 256);
            choices.SetActive(true);
            choiceGroup.interactable = choiceGroup.blocksRaycasts = false;
            if (UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        public void Choose(EndingOutcome outcome)
        {
            if (!ChoicesReady || outcome == EndingOutcome.Unchosen) return;
            ending.outcome = outcome;
            phase = Phase.Resolution; phaseAge = 0f;
            choiceGroup.interactable = choiceGroup.blocksRaycasts = false; choices.SetActive(false);
            caption.rectTransform.anchoredPosition = new Vector2(0, 230);
            beat.rectTransform.anchoredPosition = new Vector2(0, 176);
            context.text = outcome == EndingOutcome.Peace ? "A LIFE, NOT AN EXPERIMENT" : "ONE MIND. NO HORIZON.";
            caption.text = outcome == EndingOutcome.Peace ? "A small life. An ordinary name.\nA promise to leave every borrowed heart free."
                : "One mind had learned to live in two bodies.\nNext: a city of hosts. Then the world.";
            beat.text = outcome == EndingOutcome.Peace ? "For once, there was nothing to prove." : "What began as escape would become conquest.";
            summary.gameObject.SetActive(true); summary.interactable = false;
        }

        public void FinishResolution()
        {
            if (phase != Phase.Resolution || phaseAge < 0.8f || ending.outcome == EndingOutcome.Unchosen) return;
            phase = Phase.Summary;
            canvas.enabled = false;
            if (hudCanvas) hudCanvas.enabled = oldHudEnabled;
            var gm = GameManager.Instance;
            if (gm) gm.PublishFinalEnding(ending);
        }

        static bool ConfirmHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed)) return true;
            return Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (phase == Phase.Arrival && film != null && !film.Ready) dt = 0f;
            age += dt; phaseAge += dt;
            film?.Tick(phase == Phase.Arrival ? age : CinematicFilm.Duration);
            if (set != null) set.Tick(age, ending.outcome == EndingOutcome.Dominion);
            if (shore) shore.volume = Sfx.Volume * Sfx.AmbienceVolume * 0.35f;
            var keyboard = Keyboard.current;
            bool advance = keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame);
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame)) advance = true;
            if (phase == Phase.Arrival)
            {
                if (phaseAge > 5f) caption.text = "A girl sat at the edge of the lake.\nBehind her eyes, another heartbeat listened.";
                if (phaseAge > 10f) caption.text = "Vale had written the beginning.\nWhat came next belonged to you.";
                if (phaseAge > 15f || (phaseAge > 0.6f && advance)) RevealChoices();
                else UIKit.KeepFocus(continueToChoice, canvas.transform);
            }
            else if (phase == Phase.Choice)
            {
                if (!choiceGroup.interactable && phaseAge > 0.35f && !ConfirmHeld())
                { choiceGroup.interactable = choiceGroup.blocksRaycasts = true; UIKit.Select(quiet); }
                if (choiceGroup.interactable) UIKit.KeepFocus(quiet, choices.transform);
                // Waiting, Escape or a skip input never chooses either future.
            }
            else if (phase == Phase.Resolution)
            {
                if (!summary.interactable && phaseAge > 0.8f && !ConfirmHeld()) { summary.interactable = true; UIKit.Select(summary); }
                if (phaseAge > 7f || (phaseAge > 0.8f && advance)) FinishResolution();
                else if (summary.interactable) UIKit.KeepFocus(summary, canvas.transform);
            }
        }

        void BuildSound()
        {
            const int rate = 22050, count = rate * 8;
            var samples = new float[count]; var random = new System.Random(717); float low = 0;
            for (int i = 0; i < count; i++)
            {
                low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.025f);
                float seam = Mathf.Clamp01(Mathf.Min(i, count - i - 1) / (rate * 0.2f));
                samples[i] = low * 0.3f * seam * (0.7f + 0.25f * Mathf.Sin(i / (float)rate * 1.1f));
            }
            shoreClip = AudioClip.Create("Quiet water", count, 1, rate, false); shoreClip.SetData(samples, 0);
            shore = gameObject.AddComponent<AudioSource>(); shore.clip = shoreClip; shore.loop = true; shore.spatialBlend = 0f;
            shore.volume = 0f; shore.Play();
        }

        void OnDestroy()
        {
            film?.Dispose(); film = null;
            if (set != null) set.Dispose();
            if (hudCanvas) hudCanvas.enabled = oldHudEnabled;
            if (playerCamera) playerCamera.enabled = oldPlayerCameraEnabled;
            if (audioSnapshot != null) foreach (var state in audioSnapshot) if (state.source) state.source.mute = state.mute;
            if (shoreClip) Object.Destroy(shoreClip);
            if (Instance == this) Instance = null;
        }
    }
}
