using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OnlyUp.UI
{
    /// <summary>
    /// Telefon boshqaruvi: chapda joystik, o'ng tomonda barmoq bilan kamera,
    /// Sakrash va Yugurish tugmalari, tepada pauza.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class TouchControls : MonoBehaviour
    {
        public const float StickRadius = 150f;
        const float DeadZone = 0.12f;
        /// <summary>Barmoq surilishi sichqonchaga qaraganda qisqaroq, shuning uchun kuchaytiriladi.</summary>
        const float LookScale = 1.5f;

        /// <summary>Kompyuterda sinash uchun sensorli rejimni majburan yoqadi.</summary>
        public static bool ForceEnabled;

        public static bool Needed => ForceEnabled || Application.isMobilePlatform;

        GameSession session;
        PlayerInputReader input;
        GameObject root;
        RectTransform stickBase, knob;
        Image sprintImage;
        TouchPad stickPad, lookPad, jumpPad;
        Vector2 stickRest;
        Vector2 pendingLook;
        bool jumpQueued;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld => jumpPad != null && jumpPad.Held;
        public bool Sprinting { get; private set; }
        public bool Visible => root != null && root.activeSelf;

        /// <summary>canvas: HUD canvas'i; tugmalar boshqa oynalar ostida turishi uchun birinchi qo'shiladi.</summary>
        public static TouchControls Create(Transform canvas, GameSession session, PlayerInputReader input)
        {
            var area = UiKit.Rect(canvas, "TouchControls", Vector2.zero, Vector2.zero, Vector2.zero);
            Fill(area, Vector2.zero, Vector2.one);
            area.SetAsFirstSibling();
            var controls = area.gameObject.AddComponent<TouchControls>();
            controls.session = session;
            controls.input = input;
            controls.Build(area);
            input.Touch = controls;
            return controls;
        }

        static void Fill(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void Build(RectTransform area)
        {
            root = new GameObject("Controls", typeof(RectTransform));
            var all = root.GetComponent<RectTransform>();
            all.SetParent(area, false);
            Fill(all, Vector2.zero, Vector2.one);

            // Butun ekran kamerani buradi; keyin qo'shilgan joystik va tugmalar uning ustida turadi.
            lookPad = Zone(all, "LookZone", Vector2.zero, Vector2.one);
            lookPad.Dragged += data => pendingLook += data.delta * (1080f / Screen.height) * LookScale;

            var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeArea)).GetComponent<RectTransform>();
            safe.SetParent(all, false);

            stickPad = Zone(safe, "StickZone", Vector2.zero, new Vector2(0.42f, 0.75f));
            stickRest = new Vector2(300f, 300f);
            stickBase = Circle(safe, "StickBase", Vector2.zero, stickRest, StickRadius * 2f, 0.22f);
            knob = Circle(stickBase, "StickKnob", new Vector2(0.5f, 0.5f), Vector2.zero, 140f, 0.6f);
            stickPad.Pressed += data =>
            {
                // Joystik barmoq tekkan joyga ko'chadi.
                stickBase.position = ToWorld(safe, data);
                MoveStick(data);
            };
            stickPad.Dragged += MoveStick;
            stickPad.Released += ReleaseStick;

            var corner = new Vector2(1f, 0f);
            var jump = Circle(safe, "TouchJump", corner, new Vector2(-250f, 250f), 250f, 0.35f);
            jumpPad = MakeButton(jump, "SAKRA", 46);
            jumpPad.Pressed += _ => jumpQueued = true;

            var sprint = Circle(safe, "TouchSprint", corner, new Vector2(-500f, 165f), 170f, 0.3f);
            sprintImage = sprint.GetComponent<Image>();
            MakeButton(sprint, "YUGUR", 32).Pressed += _ => SetSprint(!Sprinting);

            var pause = Circle(safe, "TouchPause", new Vector2(0.5f, 1f), new Vector2(0f, -90f), 110f, 0.3f);
            MakeButton(pause, "II", 46).Pressed += _ => session.SetPaused(true);
        }

        static TouchPad Zone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = UiKit.Box(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
            Fill(rect, min, max);
            return rect.gameObject.AddComponent<TouchPad>();
        }

        static RectTransform Circle(Transform parent, string name, Vector2 anchor, Vector2 position,
            float size, float alpha)
        {
            var rect = UiKit.Box(parent, name, anchor, position, new Vector2(size, size),
                new Color(1f, 1f, 1f, alpha));
            var image = rect.GetComponent<Image>();
            image.sprite = CircleSprite();
            image.raycastTarget = false;
            return rect;
        }

        static TouchPad MakeButton(RectTransform rect, string text, int fontSize)
        {
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            // Doira tashqarisidagi burchaklar bosilmaydi.
            image.alphaHitTestMinimumThreshold = 0.5f;
            var label = UiKit.Label(rect, text, Vector2.zero, rect.sizeDelta, fontSize);
            label.fontStyle = FontStyle.Bold;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return rect.gameObject.AddComponent<TouchPad>();
        }

        static Sprite circle;

        static Sprite CircleSprite()
        {
            if (circle != null) return circle;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f));
                byte alpha = (byte)(Mathf.Clamp01(size * 0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            // alphaHitTest uchun tekstura o'qiladigan bo'lib qoladi.
            return circle = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
                SpriteMeshType.FullRect);
        }

        static Vector3 ToWorld(RectTransform rect, PointerEventData data)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, data.position, data.pressEventCamera,
                out var world);
            return world;
        }

        void MoveStick(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, data.position,
                data.pressEventCamera, out var offset);
            offset = Vector2.ClampMagnitude(offset, StickRadius);
            knob.anchoredPosition = offset;
            Vector2 value = offset / StickRadius;
            Move = value.magnitude < DeadZone ? Vector2.zero : value;
        }

        void ReleaseStick()
        {
            Move = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            stickBase.anchoredPosition = stickRest;
        }

        void SetSprint(bool on)
        {
            Sprinting = on;
            var color = on ? UiKit.Accent : Color.white;
            color.a = on ? 0.85f : 0.3f;
            sprintImage.color = color;
        }

        void Update()
        {
            bool show = !session.Paused;
            if (show != root.activeSelf)
            {
                root.SetActive(show);
                if (!show) ResetState();
            }
            // Bir kadrda yig'ilgan qiymatlar o'yinga bir marta beriladi.
            Look = pendingLook;
            pendingLook = Vector2.zero;
            JumpPressed = jumpQueued;
            jumpQueued = false;
        }

        void ResetState()
        {
            ReleaseStick();
            stickPad.Cancel();
            lookPad.Cancel();
            jumpPad.Cancel();
            pendingLook = Look = Vector2.zero;
            jumpQueued = JumpPressed = false;
        }

        void OnDestroy()
        {
            if (input != null && input.Touch == this) input.Touch = null;
        }
    }

    /// <summary>Bitta barmoqni kuzatadi: ikkinchi barmoq shu sohaga tegsa, e'tiborga olinmaydi.</summary>
    public class TouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        int? finger;

        public bool Held => finger != null;
        public event Action<PointerEventData> Pressed, Dragged;
        public event Action Released;

        public void OnPointerDown(PointerEventData data)
        {
            if (finger != null) return;
            finger = data.pointerId;
            Pressed?.Invoke(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (finger == data.pointerId) Dragged?.Invoke(data);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (finger != data.pointerId) return;
            finger = null;
            Released?.Invoke();
        }

        public void Cancel() => finger = null;

        void OnDisable() => finger = null;
    }
}
