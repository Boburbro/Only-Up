using UnityEngine;
using UnityEngine.InputSystem;

namespace OnlyUp
{
    /// <summary>Personaj atrofida sichqoncha bilan aylanadigan kamera.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerInputReader input;
        [SerializeField] Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] float distance = 4.5f;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 75f;
        [SerializeField] float stickSpeed = 150f;
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] LayerMask collisionMask = ~0;

        readonly RaycastHit[] hits = new RaycastHit[16];
        float yaw;
        float pitch = 15f;
        float currentDistance;

        /// <summary>false bo'lsa Esc va sichqoncha tugmasini boshqa tizim (pauza menyusi) boshqaradi.</summary>
        public bool HandlesCursorKeys { get; set; } = true;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public float Distance => distance;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;

        public void Bind(Transform newTarget, PlayerInputReader newInput)
        {
            target = newTarget;
            input = newInput;
            if (target != null) yaw = target.eulerAngles.y;
        }

        void Start()
        {
            currentDistance = distance;
            if (target != null) yaw = target.eulerAngles.y;
            LockCursor(true);
        }

        void OnDisable() => LockCursor(false);

        void Update()
        {
            if (!HandlesCursorKeys) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                LockCursor(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                LockCursor(true);
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (input != null && CursorWanted)
            {
                Vector2 look = input.Look;
                // Sichqoncha delta'si kadr bo'yicha keladi, stick esa tezlik beradi.
                float scale = input.LookIsFromPointer ? GameSettings.MouseSensitivity : stickSpeed * Time.deltaTime;
                yaw = Mathf.Repeat(yaw + look.x * scale, 360f);
                pitch = Mathf.Clamp(pitch + look.y * scale * (GameSettings.InvertY ? 1f : -1f), minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 back = rotation * Vector3.back;

            float allowed = AllowedDistance(pivot, back);
            // Devorga darrov yaqinlashadi, orqaga esa silliq qaytadi.
            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.MoveTowards(currentDistance, allowed, 8f * Time.deltaTime);

            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
        }

        float AllowedDistance(Vector3 pivot, Vector3 direction)
        {
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, hits, distance,
                collisionMask, QueryTriggerInteraction.Ignore);
            float result = distance;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(target)) continue;
                result = Mathf.Min(result, hits[i].distance);
            }
            return Mathf.Max(result, 0.3f);
        }

        /// <summary>O'yinchi kamerani boshqarmoqchimi (Esc bosilmagan).</summary>
        public bool CursorWanted { get; private set; } = true;

        public void LockCursor(bool locked)
        {
            CursorWanted = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
