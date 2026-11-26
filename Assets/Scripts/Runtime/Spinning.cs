using UnityEngine;

namespace OnlyUp
{
    /// <summary>O'z o'qi atrofida aylanish (mayoq nuri).</summary>
    public class Spinning : MonoBehaviour
    {
        public float degreesPerSecond = 40f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
