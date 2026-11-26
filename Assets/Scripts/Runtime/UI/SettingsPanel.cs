using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OnlyUp.UI
{
    /// <summary>Bosh menyu va pauzada bir xil ishlatiladigan sozlamalar oynasi.</summary>
    public static class SettingsPanel
    {
        const float Left = -430f, Right = 430f;

        public static GameObject Create(Transform parent, UnityAction onClose)
        {
            var shade = UiKit.Stretch(parent, "Settings", new Color(0f, 0f, 0f, 0.6f));
            var panel = UiKit.Box(shade, "Window", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1760f, 760f), new Color(0.07f, 0.08f, 0.11f, 0.98f));
            UiKit.Label(panel, "Sozlamalar", new Vector2(0f, 310f), new Vector2(800f, 70f), 52);
            Heading(panel, "Umumiy", Left);
            Heading(panel, "Grafika", Right);

            // Bitta sozlama o'zgarsa, boshqa tugmalardagi yozuv ham yangilanishi kerak (masalan, "Maxsus").
            var refresh = new List<Action>();

            Row(panel, "Kamera sezgirligi", Left, 140f);
            UiKit.Slider(panel, "Sensitivity", new Vector2(Left + 190f, 140f), new Vector2(340f, 30f),
                GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.MouseSensitivity,
                v => GameSettings.MouseSensitivity = v);
            Row(panel, "Ovoz balandligi", Left, 50f);
            UiKit.Slider(panel, "Volume", new Vector2(Left + 190f, 50f), new Vector2(340f, 30f), 0f, 1f,
                GameSettings.Volume, v => GameSettings.Volume = v);
            Row(panel, "Musiqa", Left, -40f);
            UiKit.Slider(panel, "Music", new Vector2(Left + 190f, -40f), new Vector2(340f, 30f), 0f, 1f,
                GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            Choice(panel, refresh, "Invert", "Tepa-pastni teskari qilish", Left, -130f,
                () => GameSettings.InvertY ? "Ha" : "Yo'q",
                () => GameSettings.InvertY = !GameSettings.InvertY);
            Choice(panel, refresh, "ShowFps", "FPS ko'rsatish", Left, -220f,
                () => GameSettings.ShowFps ? "Ha" : "Yo'q",
                () => GameSettings.ShowFps = !GameSettings.ShowFps);

            Choice(panel, refresh, "Quality", "Sifat", Right, 140f,
                () => GameSettings.Preset switch
                {
                    GraphicsPreset.Low => "Past",
                    GraphicsPreset.Medium => "O'rta",
                    GraphicsPreset.High => "Yuqori",
                    _ => "Maxsus"
                },
                () => GameSettings.Preset = GameSettings.Preset switch
                {
                    GraphicsPreset.Low => GraphicsPreset.Medium,
                    GraphicsPreset.Medium => GraphicsPreset.High,
                    _ => GraphicsPreset.Low
                });
            Choice(panel, refresh, "RenderScale", "Tiniqlik", Right, 50f,
                () => GameSettings.RenderScale + "%",
                () => GameSettings.RenderScale = Next(GameSettings.RenderScales, GameSettings.RenderScale));
            Choice(panel, refresh, "Shadows", "Soyalar", Right, -40f,
                () => GameSettings.Shadows switch
                {
                    ShadowQuality.Soft => "Yumshoq",
                    ShadowQuality.Hard => "Oddiy",
                    _ => "O'chiq"
                },
                () => GameSettings.Shadows = (ShadowQuality)(((int)GameSettings.Shadows + 2) % 3));
            Choice(panel, refresh, "Effects", "Effektlar", Right, -130f,
                () => GameSettings.Effects ? "Yoqilgan" : "O'chiq",
                () => GameSettings.Effects = !GameSettings.Effects);
            Choice(panel, refresh, "FpsLimit", "FPS chegarasi", Right, -220f,
                () => GameSettings.FpsLimit == 0 ? "Ekranga mos" : GameSettings.FpsLimit.ToString(),
                () => GameSettings.FpsLimit = Next(GameSettings.FpsLimits, GameSettings.FpsLimit));

            UiKit.Button(panel, "Close", "Tayyor", new Vector2(0f, -315f), new Vector2(360f, 76f), () =>
            {
                PlayerPrefs.Save();
                onClose();
            }, UiKit.Accent);
            return shade.gameObject;
        }

        static int Next(int[] values, int current) => values[(Array.IndexOf(values, current) + 1) % values.Length];

        static void Heading(Transform panel, string title, float x) =>
            UiKit.Label(panel, title, new Vector2(x, 230f), new Vector2(700f, 50f), 36, UiKit.Accent);

        static void Row(Transform panel, string title, float x, float y) =>
            UiKit.Label(panel, title, new Vector2(x - 200f, y), new Vector2(360f, 70f), 30, UiKit.Muted,
                TextAnchor.MiddleLeft);

        /// <summary>Bosilganda keyingi variantga o'tadigan tugma.</summary>
        static void Choice(Transform panel, List<Action> refresh, string name, string title, float x, float y,
            Func<string> text, Action next)
        {
            Row(panel, title, x, y);
            var button = UiKit.Button(panel, name, text(), new Vector2(x + 190f, y), new Vector2(360f, 64f), () =>
            {
                next();
                foreach (var update in refresh) update();
            }, null, 30);
            refresh.Add(() => UiKit.SetText(button, text()));
        }
    }
}
