using UnityEngine;

namespace OnlyUp
{
    /// <summary>
    /// PlayerMotor holatiga qarab animatsiya tanlaydi. Osilish uchun tayyor animatsiya
    /// yo'qligi sababli qo'llar IK bilan qirraga qo'yiladi. Animator bilan bitta obyektda turadi.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimator : MonoBehaviour
    {
        public const string Locomotion = "Locomotion";
        public const string JumpStart = "JumpStart";
        public const string Air = "Air";
        public const string Land = "Land";
        public const string Slide = "Slide";
        public const string Hang = "Hang";
        public const string Climb = "Climb";

        [Tooltip("Osilganda model shuncha pastga tushadi, shunda qo'llar tepaga cho'ziladi")]
        [SerializeField] float hangDrop = 0.28f;
        [SerializeField] float handSpacing = 0.22f;

        Animator animator;
        ICharacterState motor;
        string current;
        float stateTime;
        float airTime;
        float handWeight;
        Vector3 restPosition;

        public string Current => current;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            restPosition = transform.localPosition;
        }

        void Start() => motor = GetComponentInParent<ICharacterState>();

        void Update()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            stateTime += dt;

            Vector3 velocity = motor.Velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            animator.SetFloat("Speed", speed, 0.1f, dt);

            string next = Choose(speed, velocity.y);
            airTime = motor.State == MotorState.Airborne ? airTime + dt : 0f;
            if (next != current)
            {
                animator.CrossFadeInFixedTime(next, current == null ? 0f : 0.12f);
                current = next;
                stateTime = 0f;
            }

            bool hanging = motor.State == MotorState.Hanging;
            handWeight = Mathf.MoveTowards(handWeight, hanging ? 1f : 0f, dt * (hanging ? 10f : 5f));
            Vector3 target = restPosition + Vector3.down * (hanging ? hangDrop : 0f);
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, dt * 2.5f);
        }

        string Choose(float speed, float verticalSpeed)
        {
            switch (motor.State)
            {
                case MotorState.Sliding: return Slide;
                case MotorState.Hanging: return Hang;
                case MotorState.Climbing: return Climb;
                case MotorState.Airborne:
                    if (current == JumpStart && stateTime < 0.25f) return JumpStart;
                    if (verticalSpeed > 3f && airTime < 0.1f) return JumpStart;
                    // Zinadan tushishdagi qisqa uzilishlar yurishni buzmasligi kerak.
                    if (current == Locomotion && airTime < 0.12f && verticalSpeed <= 0f) return Locomotion;
                    return Air;
                default:
                    bool landing = current == Air || current == JumpStart || (current == Land && stateTime < 0.3f);
                    return landing && speed < 1f ? Land : Locomotion;
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (motor == null || !animator.isHuman) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            if (handWeight <= 0f || !motor.TryGetLedgeEdge(out Vector3 edge)) return;

            Vector3 side = motor.Body.right * handSpacing;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, edge - side);
            animator.SetIKPosition(AvatarIKGoal.RightHand, edge + side);
        }
    }
}
