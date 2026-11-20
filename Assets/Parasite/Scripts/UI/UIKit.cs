using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Parasite.UI
{
    public enum Weight { Medium, Semi, Bold }

    /// <summary>
    /// Code-only uGUI toolkit for PARASITE: palette, fonts, procedural (cached) sprites and widget factories.
    /// Everything is built at runtime — no prefabs or sprite assets.
    /// </summary>
    public static class UIKit
    {
        // ------------------------------------------------------------------ palette
        public static readonly Color Accent = new(0.36f, 1f, 0.52f);
        public static readonly Color AccentSoft = new(0.36f, 1f, 0.52f, 0.38f);
        public static readonly Color AccentFaint = new(0.36f, 1f, 0.52f, 0.16f);
        public static readonly Color PanelBg = new(0.012f, 0.035f, 0.03f, 0.80f);
        public static readonly Color PanelBgLight = new(0.03f, 0.08f, 0.065f, 0.88f);
        public static readonly Color TextMain = new(0.88f, 0.96f, 0.92f);
        public static readonly Color TextDim = new(0.58f, 0.70f, 0.64f);
        public static readonly Color Yellow = new(1f, 0.86f, 0.28f);
        public static readonly Color Orange = new(1f, 0.56f, 0.16f);
        public static readonly Color Red = new(1f, 0.27f, 0.23f);
        public const string AccentHex = "#5CFF85";

        public static Color SuspicionColor(SuspicionLevel l) => l switch
        {
            SuspicionLevel.Calm => Accent,
            SuspicionLevel.Curious => Yellow,
            SuspicionLevel.Investigating => Orange,
            _ => Red,
        };

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        // ------------------------------------------------------------------ fonts
        static Font fMedium, fSemi, fBold, fBuiltin;
        public static Font GetFont(Weight w) => w switch
        {
            Weight.Medium => Load(ref fMedium, "UI/Rajdhani-Medium"),
            Weight.Bold => Load(ref fBold, "UI/Rajdhani-Bold"),
            _ => Load(ref fSemi, "UI/Rajdhani-SemiBold"),
        };

        static Font Load(ref Font cache, string path)
        {
            if (cache) return cache;
            cache = Resources.Load<Font>(path);
            if (!cache)
            {
                if (!fBuiltin) fBuiltin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                cache = fBuiltin;
            }
            return cache;
        }

        /// <summary>Replace glyphs the UI font lacks (arrows) with ones it has.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf('→') < 0 && s.IndexOf('←') < 0) return s;
            return s.Replace("→", "›").Replace("←", "‹");
        }

        // ------------------------------------------------------------------ procedural sprites (cached)
        static readonly Dictionary<string, Sprite> sprites = new();

        delegate float AlphaFn(float x, float y, int n);

        static Sprite Make(string key, int w, int h, AlphaFn fn, Vector4 border)
        {
            if (sprites.TryGetValue(key, out var s) && s) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "UI_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(fn(x + 0.5f, y + 0.5f, Mathf.Max(w, h))) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            s.hideFlags = HideFlags.DontUnloadUnusedAsset;
            sprites[key] = s;
            return s;
        }

        /// <summary>Signed distance to a rounded square of size n with corner radius r (negative inside).</summary>
        static float RoundSdf(float x, float y, int n, float r)
        {
            float cx = Mathf.Clamp(x, r, n - r), cy = Mathf.Clamp(y, r, n - r);
            float dx = x - cx, dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 0f) return d - r;
            return -Mathf.Min(Mathf.Min(x, n - x), Mathf.Min(y, n - y));
        }

        public static Sprite White => Make("white", 4, 4, (x, y, n) => 1f, Vector4.zero);

        /// <summary>Filled rounded rectangle, 9-sliced (corner radius = r canvas units at multiplier 1).</summary>
        public static Sprite Rounded(int r)
        {
            int n = r * 2 + 4;
            return Make("round" + r, n, n, (x, y, s) => Mathf.Clamp01(0.5f - RoundSdf(x, y, s, r)), new Vector4(r + 1, r + 1, r + 1, r + 1));
        }

        /// <summary>Rounded rectangle outline of thickness t, 9-sliced.</summary>
        public static Sprite RoundedOutline(int r, float t = 1.5f)
        {
            int n = r * 2 + 4;
            return Make($"outline{r}_{t:0.0}", n, n, (x, y, s) =>
            {
                float d = RoundSdf(x, y, s, r);
                return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + t + 0.5f);
            }, new Vector4(r + 1, r + 1, r + 1, r + 1));
        }

        public static Sprite Circle => Make("circle", 64, 64, (x, y, n) =>
            Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) + 0.5f), Vector4.zero);

        public static Sprite Ring(float thickness = 5f) => Make($"ring{thickness:0.0}", 64, 64, (x, y, n) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
            return Mathf.Clamp01(31.5f - d + 0.5f) * Mathf.Clamp01(d - (31.5f - thickness) + 0.5f);
        }, Vector4.zero);

        /// <summary>Soft radial blob (bright center, transparent edge).</summary>
        public static Sprite Glow => Make("glow", 128, 128, (x, y, n) =>
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 64f);
            float a = 1f - d; return a * a;
        }, Vector4.zero);

        /// <summary>Screen vignette: transparent center, opaque corners.</summary>
        public static Sprite Vignette => Make("vignette", 128, 128, (x, y, n) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 90.5f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
        }, Vector4.zero);

        /// <summary>Horizontal gradient: opaque at the left, transparent at the right.</summary>
        public static Sprite GradientH => Make("gradH", 256, 4, (x, y, n) => 1f - x / 256f, Vector4.zero);

        /// <summary>Vertical gradient: opaque at the bottom, transparent at the top.</summary>
        public static Sprite GradientV => Make("gradV", 4, 256, (x, y, n) => 1f - y / 256f, Vector4.zero);

        /// <summary>Downward-pointing chevron / triangle.</summary>
        public static Sprite Chevron => Make("chevron", 32, 32, (x, y, n) =>
        {
            float t = y / 32f;                // 0 bottom (tip) .. 1 top
            float half = 15f * t;
            float d = Mathf.Abs(x - 16f) - half;
            return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01((y - 1f) * 1.5f) * Mathf.Clamp01((31f - y) * 1.5f);
        }, Vector4.zero);

        // ------------------------------------------------------------------ rect helpers
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor at a single point, set pivot, position and size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float ppuMul = 1f)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            if (sprite && sprite.border.sqrMagnitude > 0f) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppuMul; }
            return img;
        }

        /// <summary>Dark translucent rounded panel with a thin green edge. Returns the panel image.</summary>
        public static Image Panel(Transform parent, string name, int radius = 10, Color? bg = null, Color? edge = null)
        {
            var img = Img(parent, name, Rounded(radius), bg ?? PanelBg);
            var e = Img(img.transform, "Edge", RoundedOutline(radius), edge ?? AccentFaint);
            e.rectTransform.Stretch();
            return img;
        }

        /// <summary>The outline child created by <see cref="Panel"/>.</summary>
        public static Image EdgeOf(Image panel) => panel && panel.transform.childCount > 0 ? panel.transform.GetChild(0).GetComponent<Image>() : null;

        public static Text Txt(Transform parent, string name, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, Weight weight = Weight.Semi, bool shadow = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GetFont(weight); t.fontSize = size; t.color = color; t.alignment = anchor;
            t.text = value; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1f;
            if (shadow) AddShadow(t);
            return t;
        }

        public static void AddShadow(Graphic g, float dist = 1.6f, float alpha = 0.7f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha); s.effectDistance = new Vector2(dist, -dist);
        }

        /// <summary>Keyboard key-cap ("E", "Tab"…). Returns the cap's rect; label is its child Text.</summary>
        public static RectTransform KeyCap(Transform parent, string key, float h = 36, int fontSize = 22, Color? tint = null)
        {
            Color c = tint ?? Accent;
            float w = Mathf.Max(h, key.Length * fontSize * 0.52f + 18f);
            var bg = Img(parent, "Key_" + key, Rounded(6), new Color(0.02f, 0.06f, 0.05f, 0.92f));
            bg.rectTransform.sizeDelta = new Vector2(w, h);
            var edge = Img(bg.transform, "Edge", RoundedOutline(6, 2f), c);
            edge.rectTransform.Stretch();
            var t = Txt(bg.transform, "Label", key, fontSize, c, TextAnchor.MiddleCenter, Weight.Bold);
            t.rectTransform.Stretch(0, 1, 0, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return bg.rectTransform;
        }

        // ------------------------------------------------------------------ buttons / slider
        public static readonly Color BtnNormal = new(0.035f, 0.085f, 0.07f, 0.90f);
        public static readonly Color BtnHot = new(0.09f, 0.28f, 0.17f, 0.96f);
        public static readonly Color BtnPressed = new(0.20f, 0.55f, 0.32f, 1f);

        public static Button Btn(Transform parent, string label, UnityAction onClick, float w = 440, float h = 60, int fontSize = 28,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var bg = Img(parent, "Btn_" + label, Rounded(8), Color.white);
            bg.raycastTarget = true;
            bg.rectTransform.sizeDelta = new Vector2(w, h);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = BtnNormal; cb.highlightedColor = BtnHot; cb.selectedColor = BtnHot;
            cb.pressedColor = BtnPressed; cb.disabledColor = new Color(0.05f, 0.06f, 0.06f, 0.5f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
            b.colors = cb;

            var edge = Img(bg.transform, "Edge", RoundedOutline(8), AccentFaint);
            edge.rectTransform.Stretch();
            var bar = Img(bg.transform, "Bar", Rounded(2), Accent);
            bar.rectTransform.anchorMin = new Vector2(0, 0.2f); bar.rectTransform.anchorMax = new Vector2(0, 0.8f);
            bar.rectTransform.pivot = new Vector2(0, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(4, 0); bar.rectTransform.anchoredPosition = new Vector2(8, 0);
            var t = Txt(bg.transform, "Label", label, fontSize, TextDim, align, Weight.Semi);
            t.rectTransform.Stretch(align == TextAnchor.MiddleCenter ? 0 : 28, 1, align == TextAnchor.MiddleCenter ? 0 : 16, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;

            var fx = bg.gameObject.AddComponent<UIButtonFx>();
            fx.Init(t, edge, bar);
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static Slider Sld(Transform parent, string name, float min, float max, float value, UnityAction<float> onChange, float w = 440, float h = 14)
        {
            var root = Rect(name, parent);
            root.sizeDelta = new Vector2(w, 30);
            var bg = Img(root, "Background", Rounded(4), new Color(0.03f, 0.08f, 0.065f, 0.95f));
            bg.raycastTarget = true;
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f); bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, h);
            var edge = Img(bg.transform, "Edge", RoundedOutline(4), AccentFaint);
            edge.rectTransform.Stretch();

            var fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, h);
            var fill = Img(fillArea, "Fill", Rounded(4), AccentSoft);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("Handle Slide Area", root);
            handleArea.Stretch(13, 0, 13, 0);
            var handle = Img(handleArea, "Handle", Circle, Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(26, 0);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.value = value;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = new Color(0.75f, 0.88f, 0.8f); cb.highlightedColor = Accent; cb.selectedColor = Accent;
            cb.pressedColor = Color.white; cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
            s.colors = cb;
            if (onChange != null) s.onValueChanged.AddListener(onChange);
            return s;
        }

        /// <summary>Explicit up/down navigation through the list (wrapping). Left/right stay free for sliders.</summary>
        public static void LinkVertical(IList<Selectable> items, bool wrap = true)
        {
            int n = items.Count;
            for (int i = 0; i < n; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? items[i - 1] : wrap ? items[n - 1] : null;
                nav.selectOnDown = i < n - 1 ? items[i + 1] : wrap ? items[0] : null;
                items[i].navigation = nav;
            }
        }

        public static void LinkHorizontal(IList<Selectable> items, bool wrap = true)
        {
            int n = items.Count;
            for (int i = 0; i < n; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnLeft = i > 0 ? items[i - 1] : wrap ? items[n - 1] : null;
                nav.selectOnRight = i < n - 1 ? items[i + 1] : wrap ? items[0] : null;
                items[i].navigation = nav;
            }
        }

        // ------------------------------------------------------------------ canvas / event system
        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name) { layer = 5 };
            if (parent) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            c.pixelPerfect = false;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 0.5f;
            cs.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>Make sure one EventSystem with the new Input System UI module exists.</summary>
        public static EventSystem EnsureEventSystem()
        {
            var es = EventSystem.current ? EventSystem.current : Object.FindFirstObjectByType<EventSystem>();
            if (!es)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy) Object.DestroyImmediate(legacy);
            if (!es.GetComponent<InputSystemUIInputModule>()) es.gameObject.AddComponent<InputSystemUIInputModule>();
            es.sendNavigationEvents = true;
            return es;
        }

        public static void Select(Selectable s)
        {
            var es = EventSystem.current;
            if (!es) return;
            es.SetSelectedGameObject(null);
            if (s) es.SetSelectedGameObject(s.gameObject);
        }

        /// <summary>Keep keyboard/gamepad focus inside a menu: reselect the default if focus was lost.</summary>
        public static void KeepFocus(Selectable fallback, Transform menuRoot)
        {
            var es = EventSystem.current;
            if (!es || !fallback) return;
            var cur = es.currentSelectedGameObject;
            if (cur && cur.activeInHierarchy && cur.transform.IsChildOf(menuRoot)) return;
            es.SetSelectedGameObject(fallback.gameObject);
        }

        // ------------------------------------------------------------------ controls reference
        public static readonly (string key, string pad, string action)[] Controls =
        {
            ("WASD", "LS", "Move"),
            ("Mouse", "RS", "Camera"),
            ("Shift", "L3", "Sprint"),
            ("Space", "A", "Jump  ·  hop as a blob"),
            ("E", "X", "Possess  ·  interact  ·  eject when captured"),
            ("Q", "B", "Leave host (it stays unconscious 8 s)"),
            ("V", "Y", "Split off a second amoeba (max 2)"),
            ("Tab", "RB", "Switch active amoeba"),
            ("F", "LB", "Host ability (job-specific)"),
            ("Esc", "Start", "Pause"),
            ("F1", "", "Show / hide controls card"),
            ("F3", "", "FPS counter"),
        };

        /// <summary>Two- or three-column list of key caps and actions. Returns its root (sized to content).</summary>
        public static RectTransform ControlsList(Transform parent, float width, int fontSize, bool showPad, int maxRows = 99)
        {
            var root = Rect("ControlsList", parent);
            float rowH = fontSize * 1.7f, capH = fontSize * 1.35f, capW = fontSize * 3.6f;
            int rows = Mathf.Min(maxRows, Controls.Length);
            root.sizeDelta = new Vector2(width, rows * rowH);
            for (int i = 0; i < rows; i++)
            {
                var (key, pad, action) = Controls[i];
                var row = Rect("Row_" + key, root).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * rowH), new Vector2(width, rowH));
                var cap = KeyCap(row, key, capH, Mathf.RoundToInt(fontSize * 0.82f));
                cap.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(capW, capH));
                var t = Txt(row, "Action", action, fontSize, TextMain, TextAnchor.MiddleLeft, Weight.Medium);
                t.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(capW + 16, 0), new Vector2(width - capW - 16 - (showPad ? 90 : 0), rowH));
                if (showPad && pad.Length > 0)
                {
                    var p = Txt(row, "Pad", pad, Mathf.RoundToInt(fontSize * 0.8f), TextDim, TextAnchor.MiddleRight, Weight.Semi);
                    p.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(86, rowH));
                }
            }
            return root;
        }

        /// <summary>Full controls overlay (used by the main menu and pause menu). Hidden by default.</summary>
        public static GameObject ControlsOverlay(Transform parent, UnityAction onBack, out Button back)
        {
            var root = Rect("ControlsOverlay", parent).Stretch();
            var dim = Img(root, "Dim", White, new Color(0, 0, 0, 0.55f));
            dim.rectTransform.Stretch(); dim.raycastTarget = true;
            var panel = Panel(root, "Panel", 14, PanelBgLight, AccentSoft);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 860));
            var title = Txt(panel.transform, "Title", "CONTROLS", 44, TextMain, TextAnchor.UpperLeft, Weight.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -34), new Vector2(500, 56));
            var padHint = Txt(panel.transform, "PadHint", "KEYBOARD  ·  GAMEPAD", 18, TextDim, TextAnchor.UpperRight, Weight.Semi);
            padHint.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-48, -52), new Vector2(320, 30));
            var list = ControlsList(panel.transform, 724, 24, true);
            list.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -110), list.sizeDelta);

            var tips = Txt(panel.transform, "Tips",
                $"<color={AccentHex}>POSSESS</color>  Aim at a person within 5 m and press E. Their job is your access: guards open the Main Gate, scientists the Staff Terminal, maintenance the Service Tunnel.\n" +
                $"<color={AccentHex}>SUSPICION</color>  Being seen as a blob, possessing in view, splitting or leaving bodies around raises it. Break line of sight to let it fall.",
                20, TextDim, TextAnchor.UpperLeft, Weight.Medium);
            tips.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -110 - list.sizeDelta.y - 18), new Vector2(724, 140));

            back = Btn(panel.transform, "Back", onBack, 240, 54, 24, TextAnchor.MiddleCenter);
            back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(240, 54));
            var nav = new Navigation { mode = Navigation.Mode.None };
            back.navigation = nav;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        // ------------------------------------------------------------------ settings persistence
        const string SensKey = "parasite.mouseSensitivity";
        public const float SensMin = 0.03f, SensMax = 0.4f;

        public static void LoadSettings()
        {
            if (PlayerPrefs.HasKey(SensKey)) GameInput.MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensKey), SensMin, SensMax);
            AudioPreferences.Load();
        }

        public static void SaveSensitivity(float v)
        {
            GameInput.MouseSensitivity = Mathf.Clamp(v, SensMin, SensMax);
            PlayerPrefs.SetFloat(SensKey, GameInput.MouseSensitivity);
        }
    }
}
