using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace OnlyUp.Tests
{
    public class AudioTests : InputTestFixture
    {
        PrefsGuard prefs;
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Time.captureDeltaTime = 1f / 60f;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            prefs.Restore();
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var scene = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("AudioEmpty" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator Open(string name)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode($"Assets/Scenes/{name}.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(name));
            yield return Frames(40);
        }

        [UnityTest]
        public IEnumerator Qadam_sakrash_va_qonish_tovushlari_chalinadi()
        {
            yield return Open("TestScene");
            var audio = GameAudio.Current;
            Assert.IsNotNull(audio);
            Assert.IsTrue(audio.Bank.IsComplete, "Tovushlar to'liq emas");

            int before = audio.StepCount;
            yield return Frames(30);
            Assert.AreEqual(before, audio.StepCount, "Turgan joyda qadam tovushi chiqdi");

            Press(keyboard.wKey);
            yield return Frames(90);
            Release(keyboard.wKey);
            // 1.5 soniyada 3.8 m/s tezlik bilan taxminan 3-4 qadam.
            Assert.That(audio.StepCount - before, Is.InRange(2, 6));
            CollectionAssert.Contains(audio.Bank.steps, audio.LastPlayed);

            yield return Frames(30);
            Press(keyboard.spaceKey);
            yield return Frames(4);
            Assert.AreSame(audio.Bank.jump, audio.LastPlayed);
            yield return Frames(20);
            Release(keyboard.spaceKey);
            yield return Frames(60);
            Assert.AreSame(audio.Bank.land, audio.LastPlayed, "Qo'nish tovushi chiqmadi");
        }

        [UnityTest]
        public IEnumerator Menyuda_musiqa_chalinadi_va_sozlamaga_boysunadi()
        {
            yield return Open("MainMenu");
            var audio = GameAudio.Current;
            Assert.IsNotNull(audio);
            Assert.Greater(audio.MusicLevel, 0f);
            GameSettings.MusicVolume = 0f;
            yield return Frames(2);
            Assert.AreEqual(0f, audio.MusicLevel);

            var button = GameObject.Find("Next").GetComponent<UnityEngine.UI.Button>();
            button.onClick.Invoke();
            Assert.AreSame(audio.Bank.click, audio.LastPlayed);
        }

        [UnityTest]
        public IEnumerator Tovushlar_jim_va_buzilgan_emas()
        {
            yield return Open("TestScene");
            foreach (var clip in GameAudio.Current.Bank.All)
            {
                Assert.Greater(clip.length, 0.05f, clip.name);
                if (clip.loadType != AudioClipLoadType.DecompressOnLoad) continue;
                var samples = new float[clip.samples * clip.channels];
                Assert.IsTrue(clip.GetData(samples, 0), clip.name);
                float peak = 0f;
                foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
                Assert.That(peak, Is.InRange(0.1f, 1f), clip.name);
            }
        }
    }
}
