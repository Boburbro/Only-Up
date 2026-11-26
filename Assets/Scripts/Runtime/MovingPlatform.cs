using UnityEngine;

namespace OnlyUp
{
    /// <summary>Boshlang'ich nuqta bilan travel oralig'ida u yoqdan bu yoqqa yuradi.</summary>
    [DefaultExecutionOrder(-50)]
    public class MovingPlatform : CarryingSurface
    {
        public Vector3 travel = new Vector3(5f, 0f, 0f);
        public float speed = 2f;

        Vector3 origin;
        double elapsed;

        void Start() => origin = transform.position;

        protected override void Step(float dt)
        {
            float length = travel.magnitude;
            if (length < 0.01f) return;
            elapsed = WorldClock.Shared?.Invoke() ?? elapsed + dt;
            // PingPong uchun vaqt bitta davr ichiga olinadi: uzoq ochiq xonada ham aniqlik yo'qolmaydi.
            double phase = elapsed * speed / length % 2.0;
            // SmoothStep chetlarda sekinlashtiradi.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong((float)phase, 1f));
            transform.position = origin + travel * t;
        }
    }
}
