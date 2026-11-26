using UnityEngine;
using UnityEngine.UI;

namespace OnlyUp.UI
{
    /// <summary>Sozlamada yoqilsa, ekran tepasida soniyadagi kadrlar sonini ko'rsatadi.</summary>
    public class FpsMeter : MonoBehaviour
    {
        const float Interval = 0.5f;
        Text label;
        float elapsed;
        int frames;

        public string Text => label.text;

        public static FpsMeter Create(Transform parent)
        {
            var label = UiKit.Label(parent, "", new Vector2(0f, -30f), new Vector2(300f, 50f), 32, Color.white,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            label.name = "Fps";
            label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            var meter = parent.gameObject.AddComponent<FpsMeter>();
            meter.label = label;
            return meter;
        }

        void Update()
        {
            bool show = GameSettings.ShowFps;
            label.enabled = show;
            // Pauzada ham ishlashi uchun o'yin vaqtiga bog'liq emas.
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < Interval) return;
            label.text = $"{Mathf.RoundToInt(frames / elapsed)} FPS";
            elapsed = 0f;
            frames = 0;
        }
    }
}
