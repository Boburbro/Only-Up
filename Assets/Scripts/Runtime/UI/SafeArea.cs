using UnityEngine;

namespace OnlyUp.UI
{
    /// <summary>Elementlarni kamera o'yig'i va yumaloq burchaklardan ichkarida ushlaydi.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;

        void OnEnable()
        {
            applied = default;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            var area = Screen.safeArea;
            if (area == applied || Screen.width <= 0 || Screen.height <= 0) return;
            applied = area;
            var rect = (RectTransform)transform;
            var screen = new Vector2(Screen.width, Screen.height);
            rect.anchorMin = Vector2.Max(Vector2.zero, area.min / screen);
            rect.anchorMax = Vector2.Min(Vector2.one, area.max / screen);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
