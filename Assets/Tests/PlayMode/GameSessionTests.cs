using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OnlyUp.Tests
{
    public class GameSessionTests : InputTestFixture
    {
        PrefsGuard prefs;
        Keyboard keyboard;
        GameSession session;
        UI.GameHud hud;
        PlayerMotor motor;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            GameSession.ContinueRequested = false;
            PlayerProfile.Nickname = "Sinovchi";
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            Time.captureDeltaTime = 1f / 60f;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            GameSession.ContinueRequested = false;
            prefs.Restore();
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator CloseScenes()
        {
            var open = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Empty" + Time.frameCount));
            yield return SceneManager.UnloadSceneAsync(open);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        IEnumerator OpenCourse()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/World.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(20);
            Bind();
        }

        void Bind()
        {
            session = Object.FindAnyObjectByType<GameSession>();
            hud = Object.FindAnyObjectByType<UI.GameHud>();
            motor = Object.FindAnyObjectByType<PlayerMotor>();
        }

        IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return Frames(2);
            Release(key);
            yield return Frames(2);
        }

        /// <summary>Yo'ldagi platformaning ustiga qo'yadi.</summary>
        IEnumerator StandOn(int step)
        {
            var box = Object.FindAnyObjectByType<CourseRoute>().steps[step];
            var top = box.bounds.center;
            top.y = box.bounds.max.y + 0.05f;
            motor.Teleport(top);
            yield return Frames(90);
        }

        static Button Find(string name) => GameObject.Find(name).GetComponent<Button>();

        [Test]
        public void Vaqt_va_balandlik_yozilishi()
        {
            Assert.AreEqual("00:00", SaveSystem.FormatTime(0f));
            Assert.AreEqual("18:13", SaveSystem.FormatTime(18 * 60 + 13.7f));
            Assert.AreEqual("1:02:03", SaveSystem.FormatTime(3723f));
            Assert.AreEqual("92.4 m", SaveSystem.FormatHeight(92.4f));
        }

        [UnityTest]
        public IEnumerator Ekranda_balandlik_vaqt_va_nik_korinadi()
        {
            yield return OpenCourse();
            Assert.AreEqual("Sinovchi", hud.NameText);
            Assert.AreEqual("0.0 m", hud.HeightText);
            yield return Frames(120);
            Assert.AreNotEqual("00:00", hud.TimeText);
            yield return StandOn(5);
            Assert.AreEqual(SaveSystem.FormatHeight(session.Height), hud.HeightText);
            Assert.Greater(session.Height, 4f);
        }

        [UnityTest]
        public IEnumerator Esc_pauza_qiladi_va_qayta_bosilsa_davom_etadi()
        {
            yield return OpenCourse();
            Assert.IsFalse(hud.PauseMenuVisible);
            yield return Tap(keyboard.escapeKey);
            Assert.IsTrue(session.Paused);
            Assert.IsTrue(hud.PauseMenuVisible);
            Assert.AreEqual(0f, Time.timeScale);

            float time = session.PlayTime;
            Vector3 position = motor.transform.position;
            Press(keyboard.wKey);
            yield return Frames(30);
            Release(keyboard.wKey);
            Assert.AreEqual(time, session.PlayTime, "Pauzada vaqt yurdi");
            Assert.AreEqual(position, motor.transform.position, "Pauzada personaj yurdi");

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(session.Paused);
            Assert.IsFalse(hud.PauseMenuVisible);
            Assert.AreEqual(1f, Time.timeScale);
            Press(keyboard.wKey);
            yield return Frames(30);
            Assert.Greater(Vector3.Distance(position, motor.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator Qaytadan_boshlash_ikki_marta_bosilganda_ishlaydi()
        {
            yield return OpenCourse();
            yield return StandOn(6);
            yield return Tap(keyboard.escapeKey);

            Find("Restart").onClick.Invoke();
            yield return Frames(2);
            Assert.Greater(session.Height, 4f, "Bitta bosishda o'chib ketdi");
            Assert.IsTrue(SaveSystem.HasSave);

            Find("Restart").onClick.Invoke();
            yield return Frames(10);
            Assert.Less(session.Height, 0.2f);
            Assert.Less(session.PlayTime, 1f);
            Assert.IsFalse(session.Paused);
            Assert.Greater(SaveSystem.BestHeight, 4f, "Rekord saqlanib qolishi kerak");
        }

        [UnityTest]
        public IEnumerator Chiqib_qaytganda_joy_va_vaqt_tiklanadi()
        {
            yield return OpenCourse();
            yield return StandOn(7);
            Vector3 position = motor.transform.position;
            yield return Tap(keyboard.escapeKey);
            float time = session.PlayTime;

            Find("ToMenu").onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale);

            var menu = Object.FindAnyObjectByType<UI.MainMenu>();
            Assert.IsNotNull(menu.ContinueButton, "Davom etish tugmasi yo'q");
            menu.ContinueButton.onClick.Invoke();
            for (int i = 0; i < 600 && SceneManager.GetActiveScene().name != GameScenes.Game; i++)
                yield return null;
            yield return Frames(20);
            Bind();

            Assert.Less(Vector3.Distance(position, motor.transform.position), 0.2f);
            Assert.That(session.PlayTime, Is.InRange(time, time + 1f));
        }

        [UnityTest]
        public IEnumerator Yangi_oyin_saqlanganni_tasdiqsiz_ochirmaydi()
        {
            SaveSystem.Write(new SaveData { map = GameScenes.Game, position = new Vector3(0f, 5f, 0f), time = 99f });
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainMenu.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<UI.MainMenu>();

            menu.StartButton.onClick.Invoke();
            yield return Frames(5);
            Assert.IsTrue(SaveSystem.HasSave);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);

            menu.StartButton.onClick.Invoke();
            for (int i = 0; i < 600 && SceneManager.GetActiveScene().name != GameScenes.Game; i++)
                yield return null;
            yield return Frames(10);
            Bind();
            Assert.Less(session.PlayTime, 2f);
            Assert.Less(session.Height, 0.2f);
        }

        [UnityTest]
        public IEnumerator Boshqa_xaritaning_saqlanishi_yuklanmaydi()
        {
            SaveSystem.Write(new SaveData { map = "Course", position = new Vector3(0f, 30f, 0f), time = 99f });
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainMenu.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<UI.MainMenu>().ContinueButton);

            GameSession.ContinueRequested = true;
            yield return OpenCourse();
            Assert.Less(session.Height, 0.2f);
            Assert.Less(session.PlayTime, 2f);
        }

        [UnityTest]
        public IEnumerator Havoda_va_yiqilayotganda_joy_saqlanmaydi()
        {
            yield return OpenCourse();
            yield return StandOn(6);
            float saved = SaveSystem.Read().position.y;

            // Xaritadan chetdagi bo'sh joy.
            motor.Teleport(new Vector3(60f, 30f, -45f));
            yield return Frames(30);
            session.Save();
            Assert.AreEqual(saved, SaveSystem.Read().position.y, 0.01f, "Havodagi joy saqlandi");

            // Yerga yiqilib tushgach, o'sha pastdagi joy saqlanadi: checkpoint yo'q.
            yield return Frames(240);
            Assert.Less(SaveSystem.Read().position.y, 0.2f);
        }

        [UnityTest]
        public IEnumerator Sozlamalar_saqlanadi()
        {
            yield return OpenCourse();
            yield return Tap(keyboard.escapeKey);
            Find("PauseSettings").onClick.Invoke();
            yield return null;
            GameObject.Find("Sensitivity").GetComponent<Slider>().value = 0.3f;
            Find("Invert").onClick.Invoke();
            Find("Quality").onClick.Invoke();
            Find("Close").onClick.Invoke();

            Assert.AreEqual(0.3f, GameSettings.MouseSensitivity, 0.001f);
            Assert.IsTrue(GameSettings.InvertY);
            Assert.AreEqual(GraphicsPreset.Low, GameSettings.Preset);
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional)
                    Assert.AreEqual(LightShadows.None, light.shadows);
        }

        [UnityTest]
        public IEnumerator Grafika_sozlamalari_darhol_qollanadi()
        {
            var pipeline = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            GameSettings.RestoreRenderScale();
            float original = pipeline.renderScale;
            int limit = Application.targetFrameRate, sync = QualitySettings.vSyncCount;

            yield return OpenCourse();
            var camera = Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            yield return Tap(keyboard.escapeKey);
            Find("PauseSettings").onClick.Invoke();
            yield return null;
            string Shown(string name) => Find(name).GetComponentInChildren<Text>().text;
            Assert.AreEqual("Yuqori", Shown("Quality"));

            Find("RenderScale").onClick.Invoke();
            Assert.AreEqual(0.85f, pipeline.renderScale, 0.001f);
            Assert.AreEqual("Maxsus", Shown("Quality"), "Alohida o'zgarish to'plam nomida ko'rinmadi");

            Find("Quality").onClick.Invoke();
            Assert.AreEqual("Past", Shown("Quality"));
            Assert.AreEqual("70%", Shown("RenderScale"));
            Assert.AreEqual("O'chiq", Shown("Shadows"));
            Assert.AreEqual(0.7f, pipeline.renderScale, 0.001f);
            Assert.IsFalse(camera.renderPostProcessing);

            Find("Quality").onClick.Invoke();
            Assert.AreEqual("O'rta", Shown("Quality"));
            Assert.IsTrue(camera.renderPostProcessing);

            Find("FpsLimit").onClick.Invoke();
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Find("FpsLimit").onClick.Invoke();
            Assert.AreEqual(120, Application.targetFrameRate);

            var meter = Object.FindAnyObjectByType<UI.FpsMeter>();
            var label = GameObject.Find("Fps").GetComponent<Text>();
            yield return null;
            Assert.IsFalse(label.enabled);
            Find("ShowFps").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(label.enabled);
            StringAssert.IsMatch(@"^\d+ FPS$", meter.Text);

            GameSettings.RestoreRenderScale();
            Assert.AreEqual(original, pipeline.renderScale, 0.001f, "Loyiha fayli o'zgarib qoldi");
            Application.targetFrameRate = limit;
            QualitySettings.vSyncCount = sync;
        }
    }
}
