using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OnlyUp.Tests
{
    /// <summary>Haqiqiy Player prefabini soxta klaviatura va sichqoncha bilan sinaydi.</summary>
    public class MovementTests : InputTestFixture
    {
        PrefsGuard prefs;
        Keyboard keyboard;
        Mouse mouse;
        GameObject ground, player, cameraObject;
        PlayerMotor motor;
        ThirdPersonCamera orbit;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            Time.captureDeltaTime = 1f / 60f;

            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            Assert.IsNotNull(prefab, "Player prefab topilmadi");
            player = Object.Instantiate(prefab, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            motor = player.GetComponent<PlayerMotor>();

            cameraObject = new GameObject("TestCamera", typeof(Camera));
            orbit = cameraObject.AddComponent<ThirdPersonCamera>();
            orbit.Bind(player.transform, player.GetComponent<PlayerInputReader>());
            motor.CameraTransform = cameraObject.transform;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(ground);
            prefs.Restore();
            base.TearDown();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Wasd_va_strelkalar_toRi_yonalishga_yuradi()
        {
            yield return Frames(20);
            var cases = new (UnityEngine.InputSystem.Controls.KeyControl key, Vector3 direction)[]
            {
                (keyboard.wKey, Vector3.forward), (keyboard.upArrowKey, Vector3.forward),
                (keyboard.sKey, Vector3.back), (keyboard.downArrowKey, Vector3.back),
                (keyboard.aKey, Vector3.left), (keyboard.leftArrowKey, Vector3.left),
                (keyboard.dKey, Vector3.right), (keyboard.rightArrowKey, Vector3.right),
            };

            foreach (var (key, direction) in cases)
            {
                motor.Teleport(new Vector3(0f, 0.05f, 0f));
                yield return Frames(10);
                Vector3 start = player.transform.position;
                Press(key);
                yield return Frames(30);
                Release(key);
                Vector3 moved = player.transform.position - start;
                moved.y = 0f;
                Assert.Greater(moved.magnitude, 1f, $"{key.name} bilan yurmadi");
                Assert.Greater(Vector3.Dot(moved.normalized, direction), 0.95f, $"{key.name} yo'nalishi xato: {moved}");
                yield return Frames(20);
            }
        }

        [UnityTest]
        public IEnumerator Shift_bilan_tezroq_yuguradi()
        {
            yield return Frames(20);
            Press(keyboard.wKey);
            yield return Frames(60);
            float walk = Horizontal(motor.Velocity);
            Press(keyboard.leftShiftKey);
            yield return Frames(60);
            Assert.Greater(Horizontal(motor.Velocity), walk * 1.5f);
        }

        [UnityTest]
        public IEnumerator Space_bilan_sakraydi_va_yerga_qaytadi()
        {
            yield return Frames(20);
            Assert.IsTrue(motor.IsGrounded, "Boshida yerda emas");
            float start = player.transform.position.y;

            Press(keyboard.spaceKey);
            float peak = start;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, player.transform.position.y);
            }

            Assert.That(peak - start, Is.EqualTo(motor.JumpHeight).Within(0.2f));
            Assert.IsTrue(motor.IsGrounded, "Yerga qaytmadi");
        }

        [UnityTest]
        public IEnumerator Qisqa_bosilsa_pastroq_sakraydi()
        {
            yield return Frames(20);
            float start = player.transform.position.y;
            Press(keyboard.spaceKey);
            yield return Frames(3);
            Release(keyboard.spaceKey);
            float peak = start;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, player.transform.position.y);
            }
            Assert.Greater(peak - start, 0.3f);
            Assert.Less(peak - start, motor.JumpHeight * 0.8f);
        }

        [UnityTest]
        public IEnumerator Havoda_ikkinchi_marta_sakrab_bolmaydi()
        {
            yield return Frames(20);
            Press(keyboard.spaceKey);
            yield return Frames(25);
            Release(keyboard.spaceKey);
            yield return Frames(2);
            float before = motor.Velocity.y;
            Press(keyboard.spaceKey);
            yield return Frames(2);
            Assert.Less(motor.Velocity.y, before, "Havoda qayta sakradi");
        }

        [UnityTest]
        public IEnumerator Qirradan_tushgan_zahoti_sakrash_mumkin()
        {
            yield return Frames(20);
            ground.SetActive(false);
            yield return Frames(3);
            Press(keyboard.spaceKey);
            yield return Frames(2);
            Assert.Greater(motor.Velocity.y, 3f, "Coyote time ishlamadi");
        }

        [UnityTest]
        public IEnumerator Qirradan_kech_bosilsa_sakramaydi()
        {
            yield return Frames(20);
            ground.SetActive(false);
            yield return Frames(20);
            Press(keyboard.spaceKey);
            yield return Frames(2);
            Assert.Less(motor.Velocity.y, 0f);
        }

        [UnityTest]
        public IEnumerator Yerga_tegishdan_oldin_bosilgan_Space_ishlaydi()
        {
            motor.Teleport(new Vector3(0f, 1.5f, 0f));
            int guard = 0;
            while (player.transform.position.y > 0.4f && guard++ < 200) yield return null;
            Assert.IsFalse(motor.IsGrounded);
            Press(keyboard.spaceKey);

            bool jumped = false;
            for (int i = 0; i < 20 && !jumped; i++)
            {
                yield return null;
                jumped = motor.Velocity.y > 3f;
            }
            Assert.IsTrue(jumped, "Jump buffer ishlamadi");
        }

        [UnityTest]
        public IEnumerator Sichqoncha_kamerani_buradi_va_chegaradan_chiqmaydi()
        {
            yield return Frames(5);
            float yaw = orbit.Yaw;
            float pitch = orbit.Pitch;

            Set(mouse.delta, new Vector2(100f, 50f));
            // Soxta input keyingi kadrda keladi, kamera esa uni LateUpdate'da o'qiydi.
            yield return Frames(2);
            Assert.Greater(Mathf.DeltaAngle(yaw, orbit.Yaw), 1f, "O'ngga burilmadi");
            Assert.Less(orbit.Pitch, pitch, "Tepaga qaramadi");

            Set(mouse.delta, new Vector2(0f, 100000f));
            yield return Frames(2);
            Assert.AreEqual(orbit.MinPitch, orbit.Pitch, 0.01f);

            Set(mouse.delta, new Vector2(0f, -100000f));
            yield return Frames(2);
            Assert.AreEqual(orbit.MaxPitch, orbit.Pitch, 0.01f);
        }

        [UnityTest]
        public IEnumerator W_kamera_qaragan_tomonga_yuradi()
        {
            yield return Frames(20);
            // 750 birlik * 0.12 = 90 daraja o'ngga.
            Set(mouse.delta, new Vector2(750f, 0f));
            yield return null;
            Set(mouse.delta, Vector2.zero);
            yield return Frames(2);

            Press(keyboard.wKey);
            yield return Frames(40);
            Vector3 moved = player.transform.position;
            Assert.Greater(moved.x, 1f);
            Assert.Less(Mathf.Abs(moved.z), 0.4f);
        }

        [UnityTest]
        public IEnumerator Esc_bosilsa_kamera_toxtaydi()
        {
            yield return Frames(5);
            Press(keyboard.escapeKey);
            yield return Frames(2);
            float yaw = orbit.Yaw;
            Set(mouse.delta, new Vector2(300f, 0f));
            yield return Frames(2);
            Assert.AreEqual(yaw, orbit.Yaw, 0.01f);
        }

        [UnityTest]
        public IEnumerator Kamera_devorga_kirmaydi()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 3f, -2f);
            wall.transform.localScale = new Vector3(10f, 6f, 0.5f);
            Physics.SyncTransforms();
            yield return Frames(10);
            float z = cameraObject.transform.position.z;
            Object.DestroyImmediate(wall);
            Assert.Greater(z, -1.75f, "Kamera devor orqasida qoldi");
        }

        static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
