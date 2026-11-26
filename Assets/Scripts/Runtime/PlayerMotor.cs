using UnityEngine;

namespace OnlyUp
{
    public enum MotorState { Grounded, Airborne, Sliding, Hanging, Climbing }

    /// <summary>Yurish, yugurish va sakrash. CharacterController ustida ishlaydi.</summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour, ICharacterState
    {
        [Header("Yurish")]
        [SerializeField] float walkSpeed = 3.8f;
        [SerializeField] float sprintSpeed = 6.8f;
        [SerializeField] float groundAcceleration = 45f;
        [SerializeField] float airAcceleration = 14f;
        [SerializeField] float turnSpeed = 720f;

        [Header("Sakrash")]
        [SerializeField] float jumpHeight = 1.35f;
        [SerializeField] float gravity = 22f;
        [SerializeField] float fallGravityMultiplier = 1.4f;
        [SerializeField] float lowJumpGravityMultiplier = 2f;
        [SerializeField] float maxFallSpeed = 45f;
        [Tooltip("Qirradan tushgandan keyin ham sakrash mumkin bo'lgan vaqt")]
        [SerializeField] float coyoteTime = 0.12f;
        [Tooltip("Yerga tegishdan oldin bosilgan Space eslab qolinadigan vaqt")]
        [SerializeField] float jumpBufferTime = 0.15f;

        [Header("Parkur")]
        [SerializeField] float slideAcceleration = 18f;
        [SerializeField] float maxSlideSpeed = 12f;
        [Tooltip("Osilgandan keyin tirmashish buyrug'i qabul qilinguncha kutiladigan vaqt")]
        [SerializeField] float hangDelay = 0.2f;
        [SerializeField] float climbDuration = 0.55f;
        [SerializeField] float regrabDelay = 0.4f;

        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        PlayerInputReader input;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float coyoteTimer;
        float bufferTimer;
        bool jumping;
        bool bouncing;

        // Yer
        Collider ground;
        bool onSteepSlope;
        Vector3 slopeNormal;
        float slideSpeed;

        // Harakatlanuvchi sirt
        Transform carrier;
        Vector3 carrierLocalPoint;
        float carrierYaw;
        Vector3 carrierVelocity;

        // Qirra: nuqtalar qirra egasiga nisbatan saqlanadi, u harakatlansa ham to'g'ri qoladi.
        Transform ledgeOwner;
        Vector3 localHang, localStand, localNormal;
        float stateTimer;
        float regrabTimer;

        public Transform CameraTransform
        {
            get => cameraTransform;
            set => cameraTransform = value;
        }

        public event System.Action Jumped;
        public event System.Action Bounced;

        public MotorState State { get; private set; } = MotorState.Airborne;
        public bool IsGrounded { get; private set; }
        public Transform Body => transform;
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
        public float JumpHeight => jumpHeight;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            regrabTimer -= dt;

            if (State == MotorState.Hanging) { UpdateHang(dt); return; }
            if (State == MotorState.Climbing) { UpdateClimb(dt); return; }

            // Platforma bilan surish isGrounded'ni o'zgartiradi, shuning uchun oldin o'qiladi.
            bool touchingGround = controller.isGrounded;
            FollowCarrier(dt);
            ProbeGround();

            IsGrounded = touchingGround && verticalVelocity <= 0f && !onSteepSlope;

            if (onSteepSlope && verticalVelocity <= 0f)
            {
                Slide(dt);
                return;
            }
            slideSpeed = 0f;

            if (IsGrounded && TryBounce())
                IsGrounded = false;

            MoveHorizontally(dt);
            UpdateJump(dt);

            Vector3 motion = Velocity;
            if (!IsGrounded) motion += carrierVelocity;
            CollisionFlags flags = controller.Move(motion * dt);

            // Shiftga bosh urilsa, ko'tarilish to'xtaydi.
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;

            State = IsGrounded ? MotorState.Grounded : MotorState.Airborne;
            RememberCarrier();

            if (!IsGrounded) TryGrabLedge();
        }

        // ---------- Yer va sirtlar ----------

        void ProbeGround()
        {
            ground = null;
            onSteepSlope = false;

            float radius = controller.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (radius + 0.15f);
            if (!Physics.SphereCast(origin, radius, Vector3.down, out var hit, 0.15f + 0.12f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            ground = hit.collider;
            if (Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit) return;

            // Kub qirrasida sfera normali qiya chiqadi, shuning uchun sirtning o'z normali tekshiriladi.
            Vector3 inward = -new Vector3(hit.normal.x, 0f, hit.normal.z).normalized * 0.02f;
            var down = new Ray(hit.point + inward + Vector3.up * 0.1f, Vector3.down);
            if (!hit.collider.Raycast(down, out var surface, 0.4f)) return;

            float angle = Vector3.Angle(surface.normal, Vector3.up);
            if (angle > controller.slopeLimit && angle < 85f)
            {
                onSteepSlope = true;
                slopeNormal = surface.normal;
            }
        }

        void Slide(float dt)
        {
            State = MotorState.Sliding;
            IsGrounded = false;
            coyoteTimer = bufferTimer = 0f;
            jumping = bouncing = false;
            carrier = null;

            slideSpeed = Mathf.Min(slideSpeed + slideAcceleration * dt, maxSlideSpeed);
            Vector3 direction = Vector3.ProjectOnPlane(Vector3.down, slopeNormal).normalized;
            controller.Move((direction * slideSpeed + Vector3.down * 2f) * dt);

            horizontalVelocity = new Vector3(direction.x, 0f, direction.z) * slideSpeed;
            verticalVelocity = Mathf.Min(0f, direction.y * slideSpeed);
        }

        bool TryBounce()
        {
            if (ground == null || !ground.TryGetComponent<BouncePad>(out var pad)) return false;
            verticalVelocity = Mathf.Sqrt(2f * gravity * pad.bounceHeight);
            coyoteTimer = bufferTimer = 0f;
            jumping = false;
            bouncing = true;
            Bounced?.Invoke();
            return true;
        }

        void FollowCarrier(float dt)
        {
            if (carrier == null)
            {
                if (IsGrounded) carrierVelocity = Vector3.zero;
                return;
            }

            Vector3 delta = carrier.TransformPoint(carrierLocalPoint) - transform.position;
            float yawDelta = Mathf.DeltaAngle(carrierYaw, carrier.eulerAngles.y);
            if (delta.sqrMagnitude > 0f) controller.Move(delta);
            transform.Rotate(0f, yawDelta, 0f, Space.World);
            // Platformadan sakralganda uning gorizontal tezligi saqlanadi.
            carrierVelocity = new Vector3(delta.x, 0f, delta.z) / dt;
        }

        void RememberCarrier()
        {
            carrier = null;
            if (!IsGrounded || ground == null) return;

            var surface = ground.GetComponentInParent<CarryingSurface>();
            if (surface == null)
            {
                carrierVelocity = Vector3.zero;
                return;
            }
            carrier = surface.transform;
            carrierLocalPoint = carrier.InverseTransformPoint(transform.position);
            carrierYaw = carrier.eulerAngles.y;
        }

        // ---------- Qirra ----------

        void TryGrabLedge()
        {
            if (regrabTimer > 0f || verticalVelocity > 2f) return;

            Vector3 wish = WishDirection();
            if (wish.sqrMagnitude < 0.25f) return;
            if (!LedgeDetector.TryFind(transform.position, wish.normalized, controller.radius,
                    controller.height, out var ledge))
                return;
            if (Vector3.Dot(wish.normalized, -ledge.WallNormal) < 0.5f) return;

            ledgeOwner = ledge.Collider.transform;
            localHang = ledgeOwner.InverseTransformPoint(ledge.HangPoint);
            localStand = ledgeOwner.InverseTransformPoint(ledge.StandPoint);
            localNormal = ledgeOwner.InverseTransformDirection(ledge.WallNormal);

            State = MotorState.Hanging;
            stateTimer = 0f;
            carrier = null;
            carrierVelocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            coyoteTimer = bufferTimer = 0f;
            jumping = bouncing = false;
            controller.enabled = false;
            SnapToLedge();
        }

        Vector3 LedgeNormal => ledgeOwner.TransformDirection(localNormal);

        /// <summary>Hozir ushlab turilgan qirraning o'rtasi (qo'llar qo'yiladigan joy).</summary>
        public bool TryGetLedgeEdge(out Vector3 edge)
        {
            edge = default;
            if (ledgeOwner == null || (State != MotorState.Hanging && State != MotorState.Climbing)) return false;
            edge = ledgeOwner.TransformPoint(localHang) + Vector3.up * LedgeDetector.HangDepth
                   - LedgeNormal * (controller.radius + 0.05f);
            return true;
        }

        void SnapToLedge()
        {
            transform.SetPositionAndRotation(ledgeOwner.TransformPoint(localHang),
                Quaternion.LookRotation(-LedgeNormal));
        }

        void UpdateHang(float dt)
        {
            if (ledgeOwner == null || !ledgeOwner.gameObject.activeInHierarchy)
            {
                ReleaseLedge();
                return;
            }

            stateTimer += dt;
            SnapToLedge();
            if (stateTimer < hangDelay) return;

            float towards = Vector3.Dot(WishDirection(), -LedgeNormal);
            if (input.JumpPressed || towards > 0.5f)
            {
                State = MotorState.Climbing;
                stateTimer = 0f;
            }
            else if (towards < -0.5f)
            {
                ReleaseLedge();
            }
        }

        void ReleaseLedge()
        {
            State = MotorState.Airborne;
            regrabTimer = regrabDelay;
            controller.enabled = true;
        }

        void UpdateClimb(float dt)
        {
            if (ledgeOwner == null)
            {
                ReleaseLedge();
                return;
            }

            stateTimer += dt;
            float t = Mathf.Clamp01(stateTimer / climbDuration);
            Vector3 hang = ledgeOwner.TransformPoint(localHang);
            Vector3 stand = ledgeOwner.TransformPoint(localStand);
            Vector3 above = new Vector3(hang.x, stand.y, hang.z);

            // Avval tepaga tortiladi, keyin oldinga qadam qo'yadi.
            const float riseShare = 0.65f;
            transform.position = t < riseShare
                ? Vector3.Lerp(hang, above, Mathf.SmoothStep(0f, 1f, t / riseShare))
                : Vector3.Lerp(above, stand, (t - riseShare) / (1f - riseShare));

            if (t < 1f) return;
            controller.enabled = true;
            State = MotorState.Grounded;
            verticalVelocity = -2f;
            regrabTimer = regrabDelay;
        }

        // ---------- Yurish va sakrash ----------

        Vector3 WishDirection()
        {
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            return Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y);
        }

        void MoveHorizontally(float dt)
        {
            Vector3 wish = WishDirection();

            float speed = input.SprintHeld ? sprintSpeed : walkSpeed;
            float acceleration = IsGrounded ? groundAcceleration : airAcceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish * speed, acceleration * dt);

            if (wish.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(wish);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            }
        }

        void UpdateJump(float dt)
        {
            coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - dt;
            bufferTimer = input.JumpPressed ? jumpBufferTime : bufferTimer - dt;

            if (IsGrounded)
            {
                jumping = bouncing = false;
                // Pastga ozgina bosib turish yer bilan aloqani saqlaydi.
                verticalVelocity = -2f;
            }

            if (bufferTimer > 0f && coyoteTimer > 0f)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                bufferTimer = 0f;
                coyoteTimer = 0f;
                jumping = true;
                IsGrounded = false;
                Jumped?.Invoke();
                return;
            }

            if (IsGrounded) return;

            // Qirradan yurib tushganda -2 tezlik bilan emas, noldan boshlanadi.
            if (!jumping && verticalVelocity == -2f) verticalVelocity = 0f;

            float multiplier = 1f;
            if (verticalVelocity < 0f) multiplier = fallGravityMultiplier;
            else if (jumping && !bouncing && !input.JumpHeld) multiplier = lowJumpGravityMultiplier;

            verticalVelocity = Mathf.Max(verticalVelocity - gravity * multiplier * dt, -maxFallSpeed);
        }

        /// <summary>Personajni boshqa joyga ko'chiradi va tezligini nolga tushiradi.</summary>
        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            State = MotorState.Airborne;
            IsGrounded = false;
            carrier = null;
            carrierVelocity = Vector3.zero;
            slideSpeed = 0f;
            jumping = bouncing = false;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            coyoteTimer = bufferTimer = 0f;
        }
    }
}
