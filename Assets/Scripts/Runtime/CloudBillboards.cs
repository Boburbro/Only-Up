using UnityEngine;

namespace OnlyUp
{
    /// <summary>Bulut bo'laklari doim kameraga qarab turadi.</summary>
    public class CloudBillboards : MonoBehaviour
    {
        public Transform[] puffs = new Transform[0];

        void LateUpdate()
        {
            var view = Camera.main;
            if (view == null) return;
            var rotation = view.transform.rotation;
            foreach (var puff in puffs) puff.rotation = rotation;
        }
    }
}
