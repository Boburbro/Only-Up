using UnityEngine;

namespace OnlyUp
{
    /// <summary>Ustida turgan personajni o'zi bilan olib yuradigan sirt.</summary>
    public abstract class CarryingSurface : MonoBehaviour
    {
        protected abstract void Step(float dt);

        void Update()
        {
            Step(Time.deltaTime);
            // CharacterController yangi joylashuvni shu kadrning o'zida ko'rishi kerak.
            Physics.SyncTransforms();
        }
    }
}
