using Parasite.FX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parasite.UI
{
    /// <summary>Hover = select (mouse and keyboard/gamepad share one highlight), animated accent, click sound.</summary>
    [DisallowMultipleComponent]
    public class UIButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        Text label; Image edge, bar;
        bool selected;
        float t;
        Selectable sel;

        public void Init(Text label, Image edge, Image bar)
        {
            this.label = label; this.edge = edge; this.bar = bar;
            sel = GetComponent<Selectable>();
            if (sel is Button b) b.onClick.AddListener(() => Sfx.Ui(SfxId.UiClick));
            Apply(0f);
        }

        void OnDisable() { selected = false; t = 0f; Apply(0f); }

        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;

        public void OnPointerEnter(PointerEventData e)
        {
            if (sel && sel.interactable && EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void Update()
        {
            float target = selected ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 8f);
            Apply(t);
        }

        void Apply(float k)
        {
            if (label) label.color = Color.Lerp(UIKit.TextDim, Color.white, k);
            if (edge) edge.color = Color.Lerp(UIKit.AccentFaint, UIKit.Accent.WithAlpha(0.85f), k);
            if (bar)
            {
                bar.color = UIKit.Accent.WithAlpha(k);
                bar.rectTransform.localScale = new Vector3(1f, Mathf.Lerp(0.3f, 1f, k), 1f);
            }
            if (label && label.alignment != TextAnchor.MiddleCenter) label.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(0f, 6f, k), label.rectTransform.anchoredPosition.y);
        }
    }
}
