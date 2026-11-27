using UnityEngine;

namespace OnlyUp
{
    /// <summary>Xaritadan butunlay chiqib ketilsa, boshlang'ich nuqtaga qaytaradi.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class FallReset : MonoBehaviour
    {
        [SerializeField] float minHeight = -20f;

        PlayerMotor motor;
        Vector3 spawn;

        /// <summary>O'yinchi xaritadan tushib, qaytarilganda.</summary>
        public event System.Action Fell;

        public float MinHeight
        {
            get => minHeight;
            set => minHeight = value;
        }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            // Start'da emas: GameSession saqlangan joyni Start'da yuklaydi va ularning tartibi belgilanmagan.
            spawn = transform.position;
        }

        void LateUpdate()
        {
            if (transform.position.y >= minHeight) return;
            motor.Teleport(spawn);
            Fell?.Invoke();
        }
    }
}
