using UnityEngine;

namespace OnlyUp
{
    /// <summary>Dengiz to'lqinlarini sekin siljitadi (material nusxasida, asset o'zgarmaydi).</summary>
    public class SeaDrift : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.012f, 0.007f);
        Material material;

        void Start() => material = GetComponent<Renderer>().material;

        void Update()
        {
            var scale = material.GetTextureScale("_BaseMap");
            material.SetTextureOffset("_BaseMap", Vector2.Scale(speed * Time.time, scale * 10f));
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
