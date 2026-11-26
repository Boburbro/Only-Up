using UnityEngine;

namespace OnlyUp
{
    /// <summary>Aylana bo'ylab sekin suzadigan yoki uchadigan narsa: qayiq, havo shari, qushlar to'dasi.</summary>
    public class Cruiser : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 30f;
        public float degreesPerSecond = 2f;
        public float startAngle;
        public float bobHeight = 0.2f;
        public float bobSpeed = 1f;
        public float roll;

        void Start() => Place();

        void Update() => Place();

        void Place()
        {
            float angle = startAngle + Time.time * degreesPerSecond;
            var around = Quaternion.Euler(0f, angle, 0f);
            float wave = Mathf.Sin(Time.time * bobSpeed + startAngle);
            transform.position = center + around * Vector3.forward * radius + Vector3.up * wave * bobHeight;
            // Yuzi yurish tomonga qaraydi.
            var heading = around * Quaternion.Euler(0f, degreesPerSecond >= 0f ? 90f : -90f, 0f);
            transform.rotation = heading * Quaternion.Euler(0f, 0f, wave * roll);
        }
    }
}
