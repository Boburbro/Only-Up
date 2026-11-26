using UnityEngine;

namespace OnlyUp
{
    /// <summary>Animatsiya uchun kerakli holat: o'zimizning ham, boshqa o'yinchining ham personaji beradi.</summary>
    public interface ICharacterState
    {
        MotorState State { get; }
        Vector3 Velocity { get; }
        Transform Body { get; }
        /// <summary>Hozir ushlab turilgan qirraning o'rtasi (qo'llar qo'yiladigan joy).</summary>
        bool TryGetLedgeEdge(out Vector3 edge);
    }
}
