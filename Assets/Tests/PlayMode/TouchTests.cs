using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using OnlyUp.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace OnlyUp.Tests
{
    /// <summary>Telefon boshqaruvi: ekran tugmalariga barmoq hodisalari yuboriladi.</summary>
    public class TouchTests : InputTestFixture
    {
        PrefsGuard prefs;
        Touchscreen screen;
        PlayerMotor motor;
        ThirdPersonCamera orbit;
        GameSession session;
        TouchControls controls;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            screen = InputSystem.AddDevice<Touchscreen>();
            TouchControls.ForceEnabled = true;
            Time.captureDeltaTime = 1f / 60f;
        }

        public override void TearDown()
        {
            TouchControls.ForceEnabled = false;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            prefs.Restore();
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var scene = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("TouchEmpty" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        IEnumerator Open()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/World.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("World"));
            yield return Frames(60);
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            orbit = Object.FindAnyObjectByType<ThirdPersonCamera>();
            session = Object.FindAnyObjectByType<GameSession>();
            controls = Object.FindAnyObjectByType<TouchControls>();
        }

        static TouchPad Pad(string name) => GameObject.Find(name).GetComponent<TouchPad>();

        static PointerEventData Finger(int id, Vector2 position) =>
            new PointerEventData(EventSystem.current) { pointerId = id, position = position };

        static void Drag(TouchPad pad, PointerEventData finger, Vector2 delta)
        {
            finger.delta = delta;
            finger.position += delta;
            pad.OnDrag(finger);
        }

        [UnityTest]
        public IEnumerator Joystik_yuritadi_va_qoyib_yuborilsa_toxtaydi()
        {
            yield return Open();
            Assert.IsNotNull(controls, "Ekran tugmalari yaratilmadi");
            var stick = Pad("StickZone");
            var finger = Finger(1, new Vector2(100f, 100f));
            float yaw = orbit.Yaw;
            Vector3 start = motor.transform.position;

            stick.OnPointerDown(finger);
            Drag(stick, finger, new Vector2(0f, 500f));
            yield return Frames(60);
            Vector3 moved = motor.transform.position - start;
            Assert.That(moved.z, Is.InRange(2.5f, 4.5f), $"Yurish tezligida oldinga yurmadi: {moved}");
            Assert.Less(Mathf.Abs(moved.x), 0.3f);
            Assert.AreEqual(yaw, orbit.Yaw, 0.01f, "Joystik kamerani burib yubordi");

            stick.OnPointerUp(finger);
            yield return Frames(30);
            Vector3 stopped = motor.transform.position;
            yield return Frames(30);
            Assert.Less(Vector3.Distance(stopped, motor.transform.position), 0.05f);
        }

        [UnityTest]
        public IEnumerator Yugur_tugmasi_tezlashtiradi()
        {
            yield return Open();
            var sprint = Pad("TouchSprint");
            sprint.OnPointerDown(Finger(2, Vector2.zero));
            sprint.OnPointerUp(Finger(2, Vector2.zero));
            var stick = Pad("StickZone");
            var finger = Finger(1, new Vector2(100f, 100f));
            stick.OnPointerDown(finger);
            Drag(stick, finger, new Vector2(0f, 500f));
            yield return Frames(60);
            var velocity = motor.Velocity;
            Assert.Greater(new Vector2(velocity.x, velocity.z).magnitude, 6f);
        }

        [UnityTest]
        public IEnumerator Sakrash_bosib_turilsa_balandroq()
        {
            yield return Open();
            var jump = Pad("TouchJump");
            float[] heights = new float[2];
            for (int round = 0; round < 2; round++)
            {
                float ground = motor.transform.position.y;
                var finger = Finger(3, Vector2.zero);
                jump.OnPointerDown(finger);
                if (round == 0)
                {
                    yield return Frames(2);
                    jump.OnPointerUp(finger);
                }
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                    heights[round] = Mathf.Max(heights[round], motor.transform.position.y - ground);
                }
                if (round == 1) jump.OnPointerUp(finger);
                yield return Frames(20);
            }
            Assert.Greater(heights[0], 0.3f, "Qisqa bosishda sakramadi");
            Assert.That(heights[1], Is.InRange(1.15f, 1.5f), "To'liq sakrash balandligi");
            Assert.Greater(heights[1], heights[0] + 0.2f);
        }

        [UnityTest]
        public IEnumerator Ekranni_surish_kamerani_buradi()
        {
            yield return Open();
            var look = Pad("LookZone");
            float yaw = orbit.Yaw;
            var finger = Finger(4, new Vector2(500f, 300f));
            look.OnPointerDown(finger);
            Drag(look, finger, new Vector2(100f, 0f));
            yield return Frames(3);
            float expected = 100f * (1080f / UnityEngine.Screen.height) * 1.5f * GameSettings.MouseSensitivity;
            Assert.AreEqual(expected, Mathf.DeltaAngle(yaw, orbit.Yaw), 0.5f);
            float after = orbit.Yaw;
            yield return Frames(10);
            Assert.AreEqual(after, orbit.Yaw, 0.01f, "Barmoq to'xtagach kamera ham to'xtashi kerak");
        }

        [UnityTest]
        public IEnumerator Barmoqning_ozi_sichqoncha_kabi_kamerani_burmaydi()
        {
            yield return Open();
            float yaw = orbit.Yaw, pitch = orbit.Pitch;
            // Sensorli ekran qurilmasi: tugma sohalaridan tashqaridagi xom harakat.
            InputSystem.QueueStateEvent(screen, new UnityEngine.InputSystem.LowLevel.TouchState
                { touchId = 1, phase = TouchPhase.Began, position = new Vector2(100f, 100f) });
            yield return Frames(2);
            InputSystem.QueueStateEvent(screen, new UnityEngine.InputSystem.LowLevel.TouchState
                { touchId = 1, phase = TouchPhase.Moved, position = new Vector2(300f, 200f), delta = new Vector2(200f, 100f) });
            yield return Frames(3);
            // UI hodisasi LookZone'ga yetib borgan bo'lsa, u o'zi buradi, shuning uchun u o'chiriladi.
            if (Pad("LookZone").Held) Assert.Inconclusive("UI barmoqni qabul qildi");
            Assert.AreEqual(yaw, orbit.Yaw, 0.01f);
            Assert.AreEqual(pitch, orbit.Pitch, 0.01f);
        }

        [UnityTest]
        public IEnumerator Pauza_tugmasi_ishlaydi_va_boshqaruv_yashiriladi()
        {
            yield return Open();
            var stick = Pad("StickZone");
            var finger = Finger(1, new Vector2(100f, 100f));
            stick.OnPointerDown(finger);
            Drag(stick, finger, new Vector2(0f, 500f));
            yield return Frames(5);

            Pad("TouchPause").OnPointerDown(Finger(5, Vector2.zero));
            yield return Frames(3);
            Assert.IsTrue(session.Paused);
            Assert.IsFalse(controls.Visible);

            session.SetPaused(false);
            yield return Frames(3);
            Assert.IsTrue(controls.Visible);
            // Pauzadan oldin bosib turilgan joystik "yopishib" qolmasligi kerak.
            Assert.AreEqual(Vector2.zero, controls.Move);
        }

        [UnityTest]
        public IEnumerator Kompyuterda_ekran_tugmalari_chiqmaydi()
        {
            TouchControls.ForceEnabled = false;
            yield return Open();
            Assert.IsNull(controls);
        }
    }
}
