using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OnlyUp.Tests
{
    public class MainMenuTests : InputTestFixture
    {
        PrefsGuard prefs;
        UI.MainMenu menu;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
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

        IEnumerator OpenMenu()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainMenu.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            menu = Object.FindAnyObjectByType<UI.MainMenu>();
            Assert.IsNotNull(menu);
        }

        static Button Find(string name) => GameObject.Find(name).GetComponent<Button>();

        [Test]
        public void Nik_qoidalari()
        {
            Assert.IsNotNull(PlayerProfile.NameError(""));
            Assert.IsNotNull(PlayerProfile.NameError("   "));
            Assert.IsNotNull(PlayerProfile.NameError("ab"));
            Assert.IsNotNull(PlayerProfile.NameError("  ab  "));
            Assert.IsNotNull(PlayerProfile.NameError(new string('a', 17)));
            Assert.IsNotNull(PlayerProfile.NameError("<b>ali"));
            Assert.IsNull(PlayerProfile.NameError("Ali"));
            Assert.IsNull(PlayerProfile.NameError("G'ayrat_07"));
            Assert.IsNull(PlayerProfile.NameError(new string('a', 16)));
        }

        [UnityTest]
        public IEnumerator Nik_notogri_bolsa_Boshlash_ochiq_emas()
        {
            yield return OpenMenu();
            Assert.IsFalse(menu.StartButton.interactable);
            Assert.IsNotEmpty(menu.ErrorText);
            menu.NameInput.text = "ab";
            Assert.IsFalse(menu.StartButton.interactable);
            menu.StartGame();
            yield return null;
            yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);

            menu.NameInput.text = "Bobur";
            Assert.IsTrue(menu.StartButton.interactable);
            Assert.IsEmpty(menu.ErrorText);
        }

        [UnityTest]
        public IEnumerator Tugmalar_bilan_3_personaj_aylanadi()
        {
            yield return OpenMenu();
            Assert.AreEqual(0, menu.SelectedIndex);
            foreach (int expected in new[] { 1, 2, 0 })
            {
                Find("Next").onClick.Invoke();
                yield return null;
                Assert.AreEqual(expected, menu.SelectedIndex);
                StringAssert.Contains(new[] { "Aziz", "Malika", "Sardor" }[expected], menu.Preview.name);
                Assert.AreEqual(1, GameObject.Find("PreviewSpot").transform.childCount);
            }
            Find("Previous").onClick.Invoke();
            Assert.AreEqual(2, menu.SelectedIndex);
        }

        [UnityTest]
        public IEnumerator Boshlash_tanlangan_personaj_bilan_oyinni_ochadi()
        {
            yield return OpenMenu();
            menu.NameInput.text = "  Bobur  ";
            menu.Select(2);
            menu.StartButton.onClick.Invoke();

            for (int i = 0; i < 600 && SceneManager.GetActiveScene().name != GameScenes.Game; i++)
                yield return null;
            yield return null;

            Assert.AreEqual(GameScenes.Game, SceneManager.GetActiveScene().name);
            var character = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.AreEqual(2, character.Index);
            StringAssert.Contains("Sardor", character.Model.name);
            Assert.AreEqual("Bobur", PlayerProfile.Nickname);
        }

        [UnityTest]
        public IEnumerator Nik_va_tanlov_eslab_qolinadi()
        {
            PlayerProfile.Nickname = "Dilnoza";
            PlayerProfile.CharacterIndex = 1;
            yield return OpenMenu();
            Assert.AreEqual("Dilnoza", menu.NameInput.text);
            Assert.AreEqual(1, menu.SelectedIndex);
            Assert.IsTrue(menu.StartButton.interactable);
        }

        [UnityTest]
        public IEnumerator Sichqoncha_bilan_tugma_bosiladi()
        {
            yield return OpenMenu();
            var module = Object.FindAnyObjectByType<InputSystemUIInputModule>();
            Assert.IsNotNull(module);
            // Test input tizimi almashgani uchun UI action'lari qayta ulanadi.
            module.AssignDefaultActions();
            module.enabled = false;
            module.enabled = true;
            yield return null;

            var mouse = Mouse.current;
            var next = (RectTransform)Find("Next").transform;
            Set(mouse.position, (Vector2)next.TransformPoint(next.rect.center));
            yield return null;
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.AreEqual(1, menu.SelectedIndex, "Sichqoncha bilan bosish ishlamadi");
        }
    }
}
