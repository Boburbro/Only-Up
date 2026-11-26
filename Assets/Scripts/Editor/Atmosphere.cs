using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace OnlyUp.EditorTools
{
    /// <summary>Kechki osmon, quyosh, tuman va post-effektlar.</summary>
    static class Atmosphere
    {
        const string HdrPath = "Assets/Art/PolyHaven/Sky/kloppenheim_06_puresky_2k.hdr";

        public static void Apply(Camera camera, float fogDensity)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(HdrPath);
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath);
            Vector3 sunDirection = BrightestDirection(hdr);
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath);

            string skyPath = $"{ArtKit.Generated}/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetTexture("_MainTex", hdr);
            sky.SetFloat("_Exposure", 1f);
            sky.SetColor("_Tint", new Color(0.6f, 0.5f, 0.43f));
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            // Yorug'lik bake qilinmaydi, shuning uchun atrof nuri aniq ranglar bilan beriladi.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.62f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.47f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.2f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.76f, 0.63f, 0.6f);
            RenderSettings.fogDensity = fogDensity;

            // Juda past quyosh soyalarni haddan tashqari uzaytiradi.
            float elevation = Mathf.Max(Mathf.Asin(sunDirection.y) * Mathf.Rad2Deg, 24f);
            float azimuth = Mathf.Atan2(sunDirection.x, sunDirection.z) * Mathf.Rad2Deg;
            var sun = Object.FindAnyObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(elevation, azimuth + 180f, 0f);
            sun.color = new Color(1f, 0.74f, 0.48f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            camera.farClipPlane = 2000f;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            AddVolume();
        }

        /// <summary>Panoramadagi eng yorug' nuqta, ya'ni quyosh tomoni (Skybox/Panoramic koordinatalarida).</summary>
        static Vector3 BrightestDirection(Texture2D hdr)
        {
            var pixels = hdr.GetPixels();
            int best = 0;
            float max = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = pixels[i].r + pixels[i].g + pixels[i].b;
                if (value <= max) continue;
                max = value;
                best = i;
            }
            float u = (best % hdr.width + 0.5f) / hdr.width;
            float v = (best / hdr.width + 0.5f) / hdr.height;
            float longitude = (0.5f - u) * 2f * Mathf.PI;
            float latitude = (1f - v) * Mathf.PI;
            return new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude),
                Mathf.Sin(latitude) * Mathf.Sin(longitude));
        }

        static void AddVolume()
        {
            string path = $"{ArtKit.Generated}/PostEffects.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);

                T Add<T>() where T : VolumeComponent
                {
                    var component = profile.Add<T>(true);
                    component.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(component, profile);
                    return component;
                }

                Add<Tonemapping>().mode.value = TonemappingMode.Neutral;
                var bloom = Add<Bloom>();
                bloom.threshold.value = 1.1f;
                bloom.intensity.value = 0.35f;
                var color = Add<ColorAdjustments>();
                color.postExposure.value = 0.1f;
                color.contrast.value = 10f;
                color.saturation.value = 8f;
                Add<Vignette>().intensity.value = 0.22f;
                EditorUtility.SetDirty(profile);
            }

            var volume = new GameObject("PostEffects").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }
    }
}
