using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OnlyUp.EditorTools
{
    /// <summary>
    /// 0-50 m spiral xarita. Oraliqlar o'lchangan sakrash masofalariga tayanadi
    /// (JumpMetricsTests): yurib 2.1 m, yugurib 3.8 m (0.8 m balandlikka).
    /// </summary>
    public static class CourseBuilder
    {
        public const string ScenePath = "Assets/Scenes/Course.unity";
        const float TurnPerStep = 17f;

        static Transform level;
        static readonly List<Collider> route = new List<Collider>();
        static Vector3 cursor;
        static float cursorHalfLength, heading;

        [MenuItem("Only Up/Xaritani qayta yaratish")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = TestSceneBuilder.Material("Ground", new Color(0.35f, 0.3f, 0.25f));
            var wood = TestSceneBuilder.Material("Block", new Color(0.75f, 0.5f, 0.25f));
            var brick = TestSceneBuilder.Material("Wall", new Color(0.55f, 0.22f, 0.17f));
            var metal = TestSceneBuilder.Material("Pipe", new Color(0.75f, 0.65f, 0.15f));
            var rock = TestSceneBuilder.Material("Rock", new Color(0.42f, 0.4f, 0.38f));
            var rest = TestSceneBuilder.Material("Rest", new Color(0.82f, 0.78f, 0.68f));
            var bounce = TestSceneBuilder.Material("Bounce", new Color(0.2f, 0.65f, 0.3f));
            var finish = TestSceneBuilder.Material("Finish", new Color(0.95f, 0.75f, 0.1f));

            level = new GameObject("Level").transform;
            route.Clear();
            TestSceneBuilder.Box(level, "Ground", new Vector3(0f, -0.5f, 15f), new Vector3(70f, 1f, 70f), ground);

            // Boshlanish nuqtasi (0,0,0), birinchi platforma undan oldinda.
            cursor = new Vector3(0f, 0f, 4f);
            cursorHalfLength = 0f;
            heading = 70f;

            // 1-qism: yog'och yashiklar, oson.
            for (int i = 0; i < 12; i++) Step("Crate", 1.0f + 0.04f * i, 0.8f, new Vector3(2.2f, 0.8f, 2.2f), wood);
            Step("Rest", 1.2f, 0.4f, new Vector3(3.6f, 0.5f, 3.6f), rest);

            // 2-qism: ingichka to'sinlar va birinchi qirra.
            for (int i = 0; i < 3; i++)
            {
                Step("Beam", 1.2f, 0.6f, new Vector3(0.55f, 0.3f, 4f), metal);
                Step("Pad", 1.3f, 0.7f, new Vector3(1.8f, 0.4f, 1.8f), brick);
            }
            Step("Ledge", 1.1f, 2.4f, new Vector3(2.8f, 0.4f, 2.4f), brick);
            Step("Pad", 1.4f, 0.7f, new Vector3(1.8f, 0.4f, 1.8f), brick);
            Step("Rest", 1.3f, 0.5f, new Vector3(3.6f, 0.5f, 3.6f), rest);

            // 3-qism: batut va qirralar.
            Step("Trampoline", 1.3f, 0f, new Vector3(2f, 0.3f, 2f), bounce).gameObject.AddComponent<BouncePad>();
            Step("Landing", 1.0f, 4f, new Vector3(2.8f, 0.4f, 2.8f), rest);
            for (int i = 0; i < 4; i++) Step("Pipe", 1.4f, 0.75f, new Vector3(0.7f, 0.4f, 2.6f), metal);
            Step("Ledge", 1.1f, 2.4f, new Vector3(2.8f, 0.4f, 2.4f), brick);
            Step("Ledge", 1.1f, 2.4f, new Vector3(2.8f, 0.4f, 2.4f), brick);
            Step("Rest", 1.3f, 0.5f, new Vector3(3.6f, 0.5f, 3.6f), rest);

            // 4-qism: osmondagi toshlar, oraliqlar kattaroq.
            for (int i = 0; i < 8; i++)
                Step("Rock", 1.45f + 0.03f * i, 0.8f, new Vector3(1.7f - 0.05f * i, 0.7f, 1.7f - 0.05f * i), rock);
            Step("Trampoline", 1.3f, 0f, new Vector3(2f, 0.3f, 2f), bounce).gameObject.AddComponent<BouncePad>();
            Step("Landing", 1.0f, 4f, new Vector3(2.8f, 0.4f, 2.8f), rest);

            // Marragacha qolgan balandlik.
            while (cursor.y < 49.2f) Step("Rock", 1.5f, 0.8f, new Vector3(1.6f, 0.6f, 1.6f), rock);
            Step("Finish", 1.4f, 0.8f, new Vector3(4.5f, 0.6f, 4.5f), finish);

            level.gameObject.AddComponent<CourseRoute>().steps = route.ToArray();
            TestSceneBuilder.AddPlayerAndCamera();

            var session = new GameObject("GameSession").AddComponent<GameSession>();
            session.Configure(Object.FindAnyObjectByType<PlayerMotor>(),
                Camera.main.GetComponent<ThirdPersonCamera>(), route[route.Count - 1]);
            session.gameObject.AddComponent<UI.GameHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.UpdateBuildScenes();
            Debug.Log($"OnlyUp: xarita yaratildi, {route.Count} ta platforma, balandlik {cursor.y:0.0} m");
        }

        /// <summary>
        /// Oldingi platformadan keyingisini qo'yadi. gap: qirralar orasi, rise: ustki sirtlar farqi.
        /// </summary>
        static Collider Step(string kind, float gap, float rise, Vector3 size, Material material)
        {
            Vector3 direction = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            Vector3 top = cursor + direction * (cursorHalfLength + gap + size.z * 0.5f);
            top.y = cursor.y + rise;

            string name = $"{route.Count:00}_{kind}";
            var box = TestSceneBuilder.Box(level, name, top + Vector3.down * size.y * 0.5f, size, material);
            box.transform.rotation = Quaternion.Euler(0f, heading, 0f);

            cursor = top;
            cursorHalfLength = size.z * 0.5f;
            // Yo'l chapga burilib boradi va har aylanishda ancha balandga ko'tariladi.
            heading -= TurnPerStep;

            var collider = box.GetComponent<Collider>();
            route.Add(collider);
            return collider;
        }
    }
}
