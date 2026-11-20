using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>
    /// Title screen for the Bootstrap scene (place on a GameObject "MainMenu"). Builds its own uGUI:
    /// glowing PARASITE title, tagline, premise, Begin Infiltration / Controls / Quit, controls overlay.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        const string Premise =
            "You are Specimen 7 — an intelligent amoeba that just slipped out of Vale Biotech's containment lab. " +
            "Slide into a host and wear their job: guards carry badges, scientists have terminal access, maintenance " +
            "crews hold the tunnel keys. Stay unseen, get inside the lab, and find Dr. Vale.";

        RectTransform root, column;
        Text title;
        Outline titleOutline;
        Image titleGlow, fade;
        Button btnBegin, btnControls;
        GameObject controlsOverlay;
        Button controlsBack;
        bool controlsOpen, loading;
        float t;

        struct Cell { public RectTransform rt; public Image img; public Vector2 pos, vel; public float phase, size, alpha; }
        readonly List<Cell> cells = new();

        void Awake()
        {
            Time.timeScale = 1f;
            UIKit.LoadSettings();
            UIKit.EnsureEventSystem();
            UnlockCursor();

            var canvas = UIKit.CreateCanvas("MainMenu Canvas", 0, transform);
            root = (RectTransform)canvas.transform;
            BuildBackground();
            BuildColumn();
            BuildFooter();
            controlsOverlay = UIKit.ControlsOverlay(root, CloseControls, out controlsBack);

            fade = UIKit.Img(root, "Fade", UIKit.White, Color.black);
            fade.rectTransform.Stretch();
            fade.raycastTarget = false;
        }

        void Start()
        {
            UnlockCursor();
            UIKit.Select(btnBegin);
        }

        static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ------------------------------------------------------------------ build
        void BuildBackground()
        {
            var bg = UIKit.Rect("Background", root).Stretch();
            UIKit.Img(bg, "Tint", UIKit.White, new Color(0f, 0.015f, 0.012f, 0.35f)).rectTransform.Stretch();
            var left = UIKit.Img(bg, "LeftShade", UIKit.GradientH, new Color(0f, 0.01f, 0.008f, 0.92f));
            left.rectTransform.anchorMin = Vector2.zero; left.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
            var bottom = UIKit.Img(bg, "BottomShade", UIKit.GradientV, new Color(0f, 0f, 0f, 0.75f));
            bottom.rectTransform.anchorMin = Vector2.zero; bottom.rectTransform.anchorMax = new Vector2(1f, 0.4f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;
            UIKit.Img(bg, "Vignette", UIKit.Vignette, new Color(0, 0, 0, 0.75f)).rectTransform.Stretch();

            // slow drifting "cells" for a subtle bio-lab ambience
            var rng = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                bool ring = i % 3 == 0;
                float size = 30f + (float)rng.NextDouble() * (ring ? 90f : 220f);
                var img = UIKit.Img(bg, "Cell" + i, ring ? UIKit.Ring(2f) : UIKit.Glow, UIKit.Accent.WithAlpha(0f));
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                img.rectTransform.sizeDelta = new Vector2(size, size);
                cells.Add(new Cell
                {
                    rt = img.rectTransform, img = img, size = size,
                    pos = new Vector2((float)rng.NextDouble() * 1920f, (float)rng.NextDouble() * 1080f),
                    vel = new Vector2(((float)rng.NextDouble() - 0.5f) * 14f, 6f + (float)rng.NextDouble() * 16f),
                    phase = (float)rng.NextDouble() * 6.28f,
                    alpha = ring ? 0.10f + (float)rng.NextDouble() * 0.08f : 0.04f + (float)rng.NextDouble() * 0.06f,
                });
            }
        }

        void BuildColumn()
        {
            column = UIKit.Rect("Column", root).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(900, 1000));
            Vector2 tl = new(0, 1);

            var kicker = UIKit.Txt(column, "Kicker", "VALE BIOTECH   //   CONTAINMENT BREACH   //   SPECIMEN 7", 18, UIKit.Accent.WithAlpha(0.8f), TextAnchor.MiddleLeft, Weight.Semi);
            kicker.horizontalOverflow = HorizontalWrapMode.Overflow;
            kicker.rectTransform.Place(tl, tl, new Vector2(6, -150), new Vector2(900, 26));

            titleGlow = UIKit.Img(column, "TitleGlow", UIKit.Glow, UIKit.Accent.WithAlpha(0.12f));
            titleGlow.rectTransform.Place(tl, new Vector2(0.5f, 0.5f), new Vector2(330, -272), new Vector2(1100, 360));
            title = UIKit.Txt(column, "Title", "PARASITE", 176, new Color(0.88f, 1f, 0.91f), TextAnchor.MiddleLeft, Weight.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.rectTransform.Place(tl, tl, new Vector2(-4, -180), new Vector2(900, 190));
            titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectDistance = new Vector2(3, -3);
            titleOutline.effectColor = UIKit.Accent.WithAlpha(0.25f);
            UIKit.AddShadow(title, 6f, 0.5f);

            var tagline = UIKit.Txt(column, "Tagline", "It escaped. It's looking for its creator.", 38, UIKit.TextMain, TextAnchor.MiddleLeft, Weight.Semi, true);
            tagline.horizontalOverflow = HorizontalWrapMode.Overflow;
            tagline.rectTransform.Place(tl, tl, new Vector2(6, -372), new Vector2(900, 48));

            var rule = UIKit.Img(column, "Rule", UIKit.White, UIKit.Accent.WithAlpha(0.5f));
            rule.rectTransform.Place(tl, tl, new Vector2(8, -436), new Vector2(120, 2));

            var premise = UIKit.Txt(column, "Premise", Premise, 22, UIKit.TextDim, TextAnchor.UpperLeft, Weight.Medium, true);
            premise.lineSpacing = 1.1f;
            premise.rectTransform.Place(tl, tl, new Vector2(8, -456), new Vector2(760, 130));

            var items = new List<Selectable>();
            float y = -610f;
            Button Add(string label, UnityEngine.Events.UnityAction a)
            {
                var b = UIKit.Btn(column, label, a, 480, 64, 28);
                ((RectTransform)b.transform).Place(tl, tl, new Vector2(4, y), new Vector2(480, 64));
                y -= 76f;
                items.Add(b);
                return b;
            }
            btnBegin = Add("Begin Infiltration", Begin);
            btnControls = Add("Controls", OpenControls);
            Add("Quit", GameManager.QuitApp);
            UIKit.LinkVertical(items);
        }

        void BuildFooter()
        {
            string k = UIKit.AccentHex;
            var hint = UIKit.Txt(root, "Hint", $"<color={k}>[Enter]</color> Select     <color={k}>[Esc]</color> Back     Keyboard · Mouse · Gamepad",
                17, UIKit.TextDim, TextAnchor.MiddleLeft, Weight.Semi);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(156, 36), new Vector2(900, 26));
            var ver = UIKit.Txt(root, "Version", "v" + Application.version, 16, UIKit.TextDim.WithAlpha(0.6f), TextAnchor.MiddleRight, Weight.Semi);
            ver.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 36), new Vector2(300, 26));
        }

        // ------------------------------------------------------------------ actions
        void Begin()
        {
            if (loading) return;
            loading = true;
            StartCoroutine(BeginRoutine());
        }

        IEnumerator BeginRoutine()
        {
            fade.raycastTarget = true;
            for (float a = fade.color.a; a < 1f; a += Time.unscaledDeltaTime / 0.4f)
            {
                fade.color = new Color(0, 0, 0, a);
                yield return null;
            }
            fade.color = Color.black;
            RunState.NewGame();
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameManager.CityScene);
        }

        void OpenControls()
        {
            controlsOpen = true;
            controlsOverlay.SetActive(true);
            column.gameObject.SetActive(false);
            UIKit.Select(controlsBack);
        }

        void CloseControls()
        {
            controlsOpen = false;
            controlsOverlay.SetActive(false);
            column.gameObject.SetActive(true);
            UIKit.Select(btnControls);
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible) UnlockCursor();

            if (!loading && fade.color.a > 0f) fade.color = new Color(0, 0, 0, Mathf.MoveTowards(fade.color.a, 0f, dt / 0.8f));

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
            titleGlow.color = UIKit.Accent.WithAlpha(0.08f + 0.10f * pulse);
            titleOutline.effectColor = UIKit.Accent.WithAlpha(0.12f + 0.22f * pulse);
            titleGlow.rectTransform.localScale = Vector3.one * (0.96f + 0.06f * pulse);

            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                c.pos += c.vel * dt;
                c.pos.x += Mathf.Sin(t * 0.4f + c.phase) * 6f * dt;
                if (c.pos.y > 1080f + c.size) { c.pos.y = -c.size; c.pos.x = Mathf.Repeat(c.pos.x + 613f, 1920f); }
                c.rt.anchoredPosition = c.pos;
                float s = 1f + 0.08f * Mathf.Sin(t * 0.9f + c.phase);
                c.rt.localScale = new Vector3(s, s, 1f);
                c.img.color = UIKit.Accent.WithAlpha(c.alpha * (0.7f + 0.3f * Mathf.Sin(t * 0.7f + c.phase)));
                cells[i] = c;
            }

            if (loading) return;
            var kb = Keyboard.current;
            bool back = kb != null && kb.escapeKey.wasPressedThisFrame;
            var pad = Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) back = true;
            if (controlsOpen && back) CloseControls();

            UIKit.KeepFocus(controlsOpen ? controlsBack : btnBegin, root);
        }
    }
}
