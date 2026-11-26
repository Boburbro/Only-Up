using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OnlyUp.EditorTools
{
    /// <summary>O'yinni tarqatiladigan dasturga aylantiradi. Faqat bosh menyu va asosiy xarita kiradi.</summary>
    public static class PlayerBuilder
    {
        const string FileName = "Tepaga";

        [MenuItem("Only Up/Build/Linux")]
        public static void Linux() =>
            Build(BuildTarget.StandaloneLinux64, $"Builds/Linux/{FileName}.x86_64", BuildOptions.None);

        [MenuItem("Only Up/Build/Linux (sinov)")]
        public static void LinuxDevelopment() =>
            Build(BuildTarget.StandaloneLinux64, $"Builds/LinuxDev/{FileName}.x86_64", BuildOptions.Development);

        [MenuItem("Only Up/Build/Windows")]
        public static void Windows() =>
            WithProductName("Tepaga",
                () => Build(BuildTarget.StandaloneWindows64, $"Builds/Windows/{FileName}.exe", BuildOptions.None));

        /// <summary>Brauzer versiyasi: Builds/Web papkasini istalgan statik hostingga joylash mumkin.</summary>
        [MenuItem("Only Up/Build/Web")]
        public static void Web()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // Hosting maxsus HTTP sarlavhalarini bermasa ham fayllar brauzerda ochiladi.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // Assets/WebGLTemplates/Tepaga: o'yin butun brauzer oynasini egallaydi.
            PlayerSettings.WebGL.template = "PROJECT:Tepaga";
            WithProductName("Tepaga", () => Build(BuildTarget.WebGL, "Builds/Web", BuildOptions.None));
        }

        static void WithProductName(string name, Action build)
        {
            // Nom faqat build paytida almashtiriladi: loyiha nomi o'zgarsa,
            // kompyuterdagi saqlangan ma'lumotlar yo'li ham o'zgarib ketadi.
            string productName = PlayerSettings.productName;
            PlayerSettings.productName = name;
            try
            {
                build();
            }
            finally
            {
                PlayerSettings.productName = productName;
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Telefonga qo'lda o'rnatiladigan APK (sinov kaliti bilan imzolanadi).</summary>
        [MenuItem("Only Up/Build/Android")]
        public static void Android()
        {
            ConfigureAndroid();
            WithProductName("Tepaga",
                () => Build(BuildTarget.Android, $"Builds/Android/{FileName}.apk", BuildOptions.None));
        }

        public static void ConfigureAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "uz.tepaga.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Faqat yotiq holat, telefonni ikkala tomonga burish mumkin.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        static void Build(BuildTarget target, string path, BuildOptions options)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new InvalidOperationException(
                    $"{target} uchun modul o'rnatilmagan: Unity Hub > Installs > Add modules");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainMenuBuilder.ScenePath, WorldBuilder.ScenePath }.ToArray(),
                locationPathName = path,
                target = target,
                options = options
            });
            var summary = report.summary;
            Debug.Log($"OnlyUp: build {summary.result}, {summary.totalSize / (1024 * 1024)} MB, " +
                      $"xatolar: {summary.totalErrors}, {path}");
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build bajarilmadi");
        }
    }
}
