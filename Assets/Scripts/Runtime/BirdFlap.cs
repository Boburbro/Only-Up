using UnityEngine;

namespace OnlyUp
{
    /// <summary>Qush qanotlari.</summary>
    public class BirdFlap : MonoBehaviour
    {
        public Transform left, right;
        public float phase;

        void Update()
        {
            float flap = Mathf.Sin(Time.time * 6f + phase) * 28f;
            left.localRotation = Quaternion.Euler(0f, 0f, flap);
            right.localRotation = Quaternion.Euler(0f, 0f, -flap);
        }
    }
}
