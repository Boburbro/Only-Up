using UnityEngine;

namespace OnlyUp
{
    /// <summary>Vertikal o'q atrofida aylanadi.</summary>
    [DefaultExecutionOrder(-50)]
    public class RotatingPlatform : CarryingSurface
    {
        public float degreesPerSecond = 35f;

        Quaternion origin;

        void Start() => origin = transform.rotation;

        protected override void Step(float dt)
        {
            if (WorldClock.Shared == null)
            {
                transform.Rotate(0f, degreesPerSecond * dt, 0f, Space.World);
                return;
            }
            float angle = (float)(WorldClock.Shared() * degreesPerSecond % 360.0);
            transform.rotation = Quaternion.Euler(0f, angle, 0f) * origin;
        }
    }
}
