using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OnlyUp.UI
{
    /// <summary>Interfeys elementlarini koddan yaratadi, sahnada qo'lda ulash shart emas.</summary>
    public static class UiKit
    {
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.11f, 0.88f);
        public static readonly Color Control = new Color(0.2f, 0.22f, 0.28f, 1f);
        public static readonly Color Accent = new Color(0.95f, 0.55f, 0.18f, 1f);
        public static readonly Color Muted = new Color(0.72f, 0.74f, 0.8f, 1f);

        static Font font;
        static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Keng (telefon) ekranda balandlikka moslanadi, aks holda oynalar tepa-pastdan sig'maydi.
            scaler.matchWidthOrHeight = Screen.width * 9 >= Screen.height * 16 ? 1f : 0f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        /// <summary>anchor: ekrandagi nuqta (0..1); position: shu nuqtadan siljish.</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position,
            Vector2 size, Vector2? pivot = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(Transform parent, string name, Color color)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position,
            Vector2 size, Color color)
        {
            var rect = Rect(parent, name, anchor, position, size);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        public static Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize,
            Color? color = null, TextAnchor align = TextAnchor.MiddleCenter, Vector2? anchor = null,
            Vector2? pivot = null)
        {
            var rect = Rect(parent, "Label", anchor ?? new Vector2(0.5f, 0.5f), position, size, pivot);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color ?? Color.white;
            label.alignment = align;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size,
            UnityAction onClick, Color? color = null, int fontSize = 34)
        {
            var rect = Box(parent, name, new Vector2(0.5f, 0.5f), position, size, color ?? Control);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (GameAudio.Current != null) GameAudio.Current.PlayClick();
            });
            button.onClick.AddListener(onClick);
            var label = Label(rect, text, Vector2.zero, size, fontSize);
            label.fontStyle = FontStyle.Bold;
            return button;
        }

        public static void SetText(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        public static InputField Input(Transform parent, string name, string placeholder, Vector2 position,
            Vector2 size, int limit)
        {
            var rect = Box(parent, name, new Vector2(0.5f, 0.5f), position, size, Control);
            var textSize = size - new Vector2(40f, 0f);
            var hint = Label(rect, placeholder, Vector2.zero, textSize, 32, Muted, TextAnchor.MiddleLeft);
            var value = Label(rect, "", Vector2.zero, textSize, 36, Color.white, TextAnchor.MiddleLeft);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            var input = rect.gameObject.AddComponent<InputField>();
            input.textComponent = value;
            input.placeholder = hint;
            input.characterLimit = limit;
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.caretWidth = 3;
            return input;
        }

        public static Slider Slider(Transform parent, string name, Vector2 position, Vector2 size, float min,
            float max, float value, UnityAction<float> onChange)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.transform.Find("Background").GetComponent<Image>().color = Control;
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Accent;
            // Barmoq bilan tutish oson bo'lishi uchun bosiladigan soha ko'rinadiganidan katta.
            go.transform.Find("Background").GetComponent<Image>().raycastPadding = new Vector4(0f, -25f, 0f, -25f);
            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }
    }
}
