using System.IO;
using System.Linq;
using UnityEditor;

namespace OnlyUp.EditorTools
{
    public static class ProjectSetup
    {
        /// <summary>Build ro'yxati: bosh menyu birinchi bo'lishi shart.</summary>
        public static void UpdateBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
                {
                    MainMenuBuilder.ScenePath, WorldBuilder.ScenePath, CourseBuilder.ScenePath,
                    TestSceneBuilder.ScenePath
                }
                .Where(File.Exists)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }

        /// <summary>Barcha sahnalarni noldan yaratadi.</summary>
        [MenuItem("Only Up/Hammasini qayta yaratish")]
        public static void BuildAll()
        {
            TestSceneBuilder.Build();
            CourseBuilder.Build();
            WorldBuilder.Build();
            MainMenuBuilder.Build();
        }
    }
}
