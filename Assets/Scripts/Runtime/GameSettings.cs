using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OnlyUp
{
    public enum ShadowQuality { Off, Hard, Soft }

    /// <summary>Tayyor grafika to'plamlari. Custom: o'yinchi alohida sozlamalarni o'zi o'zgartirgan.</summary>
    public enum GraphicsPreset { Low, Medium, High, Custom }

    /// <summary>Menyu va pauzadagi sozlamalar.</summary>
    public static class GameSettings
    {
        public const string SensitivityKey = "settings.sensitivity";
        public const string InvertKey = "settings.invertY";
        public const string VolumeKey = "settings.volume";
        public const string MusicKey = "settings.music";
        public const string RenderScaleKey = "settings.renderScale";
        public const string ShadowsKey = "settings.shadows";
        public const string EffectsKey = "settings.effects";
        public const string FpsLimitKey = "settings.fpsLimit";
        public const string ShowFpsKey = "settings.showFps";

        public const float DefaultSensitivity = 0.12f;
        public const float MinSensitivity = 0.03f;
        public const float MaxSensitivity = 0.4f;

        /// <summary>Tanlanadigan tiniqlik darajalari: 3D rasm ekranning qancha qismida chiziladi.</summary>
        public static readonly int[] RenderScales = { 100, 85, 70, 55 };

        /// <summary>
        /// 0: ekran yangilanishiga qarab. 120 faqat shunday tez ekranda seziladi:
        /// brauzer va ekran o'z tezligidan ortiq kadr ko'rsatmaydi.
        /// </summary>
        public static readonly int[] FpsLimits = { 0, 60, 120, 30 };

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            set => PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(InvertKey, 0) == 1;
            set => PlayerPrefs.SetInt(InvertKey, value ? 1 : 0);
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Mathf.Clamp01(value);
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.35f);
            set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        }

        // ---------- Grafika ----------

        // Telefonda boshlanishida past sifat: soya va effektlar batareya va FPS'ni yeydi.
        static GraphicsPreset DefaultPreset => Application.isMobilePlatform ? GraphicsPreset.Low : GraphicsPreset.High;

        static (int scale, ShadowQuality shadows, bool effects) Values(GraphicsPreset preset) => preset switch
        {
            GraphicsPreset.Low => (70, ShadowQuality.Off, false),
            GraphicsPreset.Medium => (85, ShadowQuality.Hard, true),
            _ => (100, ShadowQuality.Soft, true)
        };

        public static int RenderScale
        {
            get => PlayerPrefs.GetInt(RenderScaleKey, Values(DefaultPreset).scale);
            set
            {
                PlayerPrefs.SetInt(RenderScaleKey, Mathf.Clamp(value, 50, 100));
                Apply();
            }
        }

        public static ShadowQuality Shadows
        {
            get => (ShadowQuality)PlayerPrefs.GetInt(ShadowsKey, (int)Values(DefaultPreset).shadows);
            set
            {
                PlayerPrefs.SetInt(ShadowsKey, (int)value);
                Apply();
            }
        }

        /// <summary>Yorug'lik porlashi, rang tuzatish va boshqa post-effektlar.</summary>
        public static bool Effects
        {
            get => PlayerPrefs.GetInt(EffectsKey, Values(DefaultPreset).effects ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(EffectsKey, value ? 1 : 0);
                Apply();
            }
        }

        public static GraphicsPreset Preset
        {
            get
            {
                var current = (RenderScale, Shadows, Effects);
                for (var preset = GraphicsPreset.Low; preset < GraphicsPreset.Custom; preset++)
                    if (Values(preset) == current) return preset;
                return GraphicsPreset.Custom;
            }
            set
            {
                if (value == GraphicsPreset.Custom) return;
                var (scale, shadows, effects) = Values(value);
                PlayerPrefs.SetInt(RenderScaleKey, scale);
                PlayerPrefs.SetInt(ShadowsKey, (int)shadows);
                PlayerPrefs.SetInt(EffectsKey, effects ? 1 : 0);
                Apply();
            }
        }

        public static int FpsLimit
        {
            // Telefonda ekran 120 Hz bo'lishi mumkin: batareya uchun boshlanishida 60.
            get => PlayerPrefs.GetInt(FpsLimitKey, Application.isMobilePlatform ? 60 : 0);
            set
            {
                PlayerPrefs.SetInt(FpsLimitKey, value);
                Apply();
            }
        }

        public static bool ShowFps
        {
            get => PlayerPrefs.GetInt(ShowFpsKey, 0) == 1;
            set => PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
        }

        public static void Apply()
        {
            AudioListener.volume = Volume;
            ApplyFrameRate();
            if (Application.isMobilePlatform) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            ApplyRenderScale();
            if (Camera.main != null && Camera.main.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data.renderPostProcessing = Effects;
            var shadows = Shadows switch
            {
                ShadowQuality.Soft => LightShadows.Soft,
                ShadowQuality.Hard => LightShadows.Hard,
                _ => LightShadows.None
            };
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional)
                    light.shadows = shadows;
        }

        static void ApplyFrameRate()
        {
            int limit = FpsLimit;
            if (limit > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = limit;
                return;
            }
            QualitySettings.vSyncCount = 1;
            // Android vSync'ni hisobga olmaydi va o'z holicha 30 FPS bilan cheklaydi.
            Application.targetFrameRate = Application.isMobilePlatform
                ? Mathf.Max(60, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value))
                : -1;
        }

        static UniversalRenderPipelineAsset changed;
        static float originalScale;

        static void ApplyRenderScale()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)) return;
            if (changed != asset)
            {
                RestoreRenderScale();
                changed = asset;
                originalScale = asset.renderScale;
                Application.quitting += RestoreRenderScale;
            }
            asset.renderScale = RenderScale / 100f;
        }

        /// <summary>
        /// Tiniqlik umumiy sozlama faylida turadi: o'yin yopilganda asl qiymati qaytariladi,
        /// aks holda editorda loyiha fayli o'zgarib qoladi.
        /// </summary>
        public static void RestoreRenderScale()
        {
            Application.quitting -= RestoreRenderScale;
            if (changed != null) changed.renderScale = originalScale;
            changed = null;
        }
    }
}
