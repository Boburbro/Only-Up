using UnityEngine;
using UnityEngine.InputSystem;

namespace OnlyUp
{
    /// <summary>Klaviatura, sichqoncha (g'ildirak ham sakratadi) va gamepad qiymatlarini o'qib beradi.</summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        // G'ildirakni "bosib turib" bo'lmaydi, shuning uchun u bilan sakrash har doim to'liq balandlikda
        // bo'lsin: sakrash tugmasi eng yuqori nuqtagacha bosib turilgandek hisoblanadi.
        const float WheelHoldTime = 0.6f;

        float wheelHold;
        bool wheelPressed;
        InputActionMap map;
        InputAction move, look, jump, sprint;

        public InputActionAsset Actions
        {
            get => actions;
            set => actions = value;
        }

        /// <summary>Telefonda ekran tugmalari; kompyuterda null.</summary>
        public UI.TouchControls Touch { get; set; }

        public Vector2 Move
        {
            get
            {
                Vector2 value = move?.ReadValue<Vector2>() ?? Vector2.zero;
                return Touch == null || !enabled ? value : Vector2.ClampMagnitude(value + Touch.Move, 1f);
            }
        }

        public Vector2 Look
        {
            get
            {
                Vector2 value = look?.ReadValue<Vector2>() ?? Vector2.zero;
                if (Touch == null || !enabled) return value;
                // Telefonda har qanday barmoq "pointer" hisoblanadi: joystik kamerani burmasligi
                // uchun faqat kamera sohasidagi surilish olinadi.
                return look?.activeControl?.device is Pointer || value == Vector2.zero ? Touch.Look : value;
            }
        }

        public bool LookIsFromPointer
        {
            get
            {
                var device = look?.activeControl?.device;
                if (Touch == null) return device is Pointer;
                return device == null || device is Pointer;
            }
        }

        public bool JumpPressed => jump != null && jump.WasPressedThisFrame() || wheelPressed ||
                                   Touch != null && enabled && Touch.JumpPressed;
        public bool JumpHeld => jump != null && jump.IsPressed() || wheelHold > 0f || Touch != null && enabled && Touch.JumpHeld;
        public bool SprintHeld => sprint != null && sprint.IsPressed() ||
                                  Touch != null && enabled && Touch.Sprinting;

        void OnEnable()
        {
            if (actions == null) return;
            map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            sprint = map.FindAction("Sprint", true);
            map.Enable();
        }

        void Update()
        {
            // Ikkala tomonga aylantirish ham sakratadi.
            var mouse = Mouse.current;
            wheelPressed = mouse != null && mouse.scroll.ReadValue().y != 0f;
            wheelHold = wheelPressed ? WheelHoldTime : Mathf.Max(0f, wheelHold - Time.deltaTime);
        }

        void OnDisable()
        {
            map?.Disable();
            wheelPressed = false;
            wheelHold = 0f;
        }
    }
}
