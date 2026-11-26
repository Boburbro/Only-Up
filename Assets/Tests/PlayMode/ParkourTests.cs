using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace OnlyUp.Tests
{
    public class ParkourTests : InputTestFixture
    {
        PrefsGuard prefs;
        Keyboard keyboard;
        GameObject player;
        PlayerMotor motor;
        readonly List<GameObject> spawned = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Time.captureDeltaTime = 1f / 60f;
            Box(new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            player = Object.Instantiate(prefab, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            motor = player.GetComponent<PlayerMotor>();
            spawned.Add(player);
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            foreach (var item in spawned) Object.DestroyImmediate(item);
            spawned.Clear();
            prefs.Restore();
            base.TearDown();
        }

        GameObject Box(Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = position;
            box.transform.localScale = size;
            spawned.Add(box);
            Physics.SyncTransforms();
            return box;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        IEnumerator JumpAndGrab()
        {
            yield return Frames(20);
            Press(keyboard.wKey);
            Press(keyboard.spaceKey);
            for (int i = 0; i < 90 && motor.State != MotorState.Hanging; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Baland_qirraga_osiladi_va_tepaga_chiqadi()
        {
            // Balandligi 2.6 m: oddiy sakrash (1.35 m) yetmaydi.
            Box(new Vector3(0f, 1.3f, 2f), new Vector3(3f, 2.6f, 2f));
            yield return JumpAndGrab();
            Assert.AreEqual(MotorState.Hanging, motor.State, $"Osilmadi: {player.transform.position}");

            for (int i = 0; i < 120 && motor.State != MotorState.Grounded; i++) yield return null;
            Release(keyboard.wKey);
            yield return Frames(20);
            Assert.AreEqual(2.6f, player.transform.position.y, 0.15f);
            Assert.IsTrue(motor.IsGrounded);
        }

        [UnityTest]
        public IEnumerator S_bosilsa_qirrani_qoyib_yuboradi()
        {
            Box(new Vector3(0f, 1.3f, 2f), new Vector3(3f, 2.6f, 2f));
            yield return JumpAndGrab();
            Assert.AreEqual(MotorState.Hanging, motor.State);
            Release(keyboard.wKey);
            Release(keyboard.spaceKey);
            yield return Frames(30);
            Assert.AreEqual(MotorState.Hanging, motor.State, "Tugma bosilmasa osilib turishi kerak");

            Press(keyboard.sKey);
            yield return Frames(3);
            Release(keyboard.sKey);
            yield return Frames(60);
            Assert.IsTrue(motor.IsGrounded);
            Assert.Less(player.transform.position.y, 0.2f);
        }

        [UnityTest]
        public IEnumerator Juda_baland_devorga_osilmaydi()
        {
            Box(new Vector3(0f, 3f, 2f), new Vector3(3f, 6f, 2f));
            yield return JumpAndGrab();
            Assert.AreNotEqual(MotorState.Hanging, motor.State);
        }

        [UnityTest]
        public IEnumerator Qirra_ustida_joy_bolmasa_osilmaydi()
        {
            Box(new Vector3(0f, 1.3f, 2f), new Vector3(3f, 2.6f, 2f));
            // Qalin shift: qirradan 0.6 m tepada boshlanadi, uning ustiga esa qo'l yetmaydi.
            Box(new Vector3(0f, 5f, 2f), new Vector3(3f, 3.6f, 2f));
            yield return JumpAndGrab();
            Assert.AreNotEqual(MotorState.Hanging, motor.State);
        }

        [UnityTest]
        public IEnumerator Tik_qiyalikdan_sirpanadi_va_sakrab_bolmaydi()
        {
            var slope = Box(new Vector3(0f, 5f, 10f), new Vector3(6f, 0.4f, 12f));
            slope.transform.rotation = Quaternion.Euler(-60f, 0f, 0f);
            Physics.SyncTransforms();
            motor.Teleport(new Vector3(0f, 8f, 11f));

            bool slid = false;
            for (int i = 0; i < 120 && !slid; i++)
            {
                yield return null;
                slid = motor.State == MotorState.Sliding;
            }
            Assert.IsTrue(slid, $"Sirpanmadi: {motor.State} {player.transform.position}");
            float height = player.transform.position.y;
            Press(keyboard.spaceKey);
            yield return Frames(15);
            Assert.Less(player.transform.position.y, height, "Qiyalikda sakrab yubordi");
        }

        [UnityTest]
        public IEnumerator Kub_qirrasida_sirpanmaydi()
        {
            Box(new Vector3(0f, 0.5f, 5f), new Vector3(2f, 1f, 2f));
            motor.Teleport(new Vector3(0f, 1.05f, 3.85f));
            yield return Frames(40);
            Assert.AreNotEqual(MotorState.Sliding, motor.State);
            Assert.AreEqual(1f, player.transform.position.y, 0.2f);
        }

        [UnityTest]
        public IEnumerator Batut_baland_otadi()
        {
            var pad = Box(new Vector3(0f, 0.15f, 0f), new Vector3(2f, 0.3f, 2f)).AddComponent<BouncePad>();
            motor.Teleport(new Vector3(0f, 1f, 0f));
            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, player.transform.position.y);
            }
            Assert.AreEqual(0.3f + pad.bounceHeight, peak, 0.35f);
        }

        [UnityTest]
        public IEnumerator Harakatlanuvchi_platforma_olib_yuradi()
        {
            var box = Box(new Vector3(0f, 1f, 0f), new Vector3(3f, 0.3f, 3f));
            motor.Teleport(new Vector3(0f, 1.2f, 0f));
            yield return Frames(10);
            var platform = box.AddComponent<MovingPlatform>();
            platform.travel = new Vector3(6f, 3f, 0f);
            yield return Frames(10);
            Vector3 offset = player.transform.position - box.transform.position;
            float startX = box.transform.position.x;

            int airborne = 0;
            for (int i = 0; i < 300; i++)
            {
                yield return null;
                if (!motor.IsGrounded) airborne++;
                Assert.Less(Vector3.Distance(player.transform.position - box.transform.position, offset), 0.25f,
                    $"Platformadan siljib ketdi, kadr {i}");
            }
            Assert.Greater(Mathf.Abs(box.transform.position.x - startX), 0.5f);
            Assert.Less(airborne, 10, "Platformada titrayapti");
        }

        [UnityTest]
        public IEnumerator Aylanuvchi_platforma_birga_aylantiradi()
        {
            var box = Box(new Vector3(0f, 1f, 0f), new Vector3(5f, 0.3f, 5f));
            motor.Teleport(new Vector3(1.5f, 1.2f, 0f));
            yield return Frames(10);
            box.AddComponent<RotatingPlatform>().degreesPerSecond = 45f;
            yield return Frames(120);
            Vector3 local = box.transform.InverseTransformPoint(player.transform.position);
            Assert.AreEqual(0.3f, local.x, 0.05f, $"Joyidan siljidi: {local}");
            Assert.AreEqual(0f, local.z, 0.05f);
            Assert.AreEqual(90f, player.transform.eulerAngles.y, 5f);
        }

        [UnityTest]
        public IEnumerator Ingichka_quvur_ustidan_yurib_otadi()
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.transform.SetPositionAndRotation(new Vector3(0f, 2f, 4f), Quaternion.Euler(90f, 0f, 0f));
            pipe.transform.localScale = new Vector3(0.4f, 4f, 0.4f);
            spawned.Add(pipe);
            Physics.SyncTransforms();
            // O'rtadan 10 sm chetda boshlaydi.
            motor.Teleport(new Vector3(0.1f, 2.25f, 0.5f));
            yield return Frames(20);
            Press(keyboard.wKey);
            for (int i = 0; i < 200 && player.transform.position.z < 7f; i++)
            {
                yield return null;
                Assert.Greater(player.transform.position.y, 1.9f, $"Quvurdan tushdi: {player.transform.position}");
                Assert.AreNotEqual(MotorState.Sliding, motor.State);
            }
            Assert.GreaterOrEqual(player.transform.position.z, 7f);
        }
    }
}
