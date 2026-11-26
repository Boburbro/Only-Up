using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace OnlyUp.Tests
{
    /// <summary>Sakrash masofalarini o'lchaydi. Xarita shu chegaralarga tayanib quriladi.</summary>
    public class JumpMetricsTests : InputTestFixture
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
            prefs.Restore();
            base.TearDown();
        }

        /// <summary>Sakrab, boshlang'ich balandlikdan rise metr tepada qancha masofaga yetishini qaytaradi.</summary>
        IEnumerator Measure(bool sprint, float rise, System.Action<float> result)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 20f);
            ground.transform.localScale = new Vector3(10f, 1f, 60f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            var motor = player.GetComponent<PlayerMotor>();

            for (int i = 0; i < 20; i++) yield return null;
            // Soxta klaviaturada har bir tugma alohida kadrda o'zgartiriladi,
            // aks holda bitta kadrdagi o'zgarishlar bir-birini bekor qiladi.
            Press(keyboard.wKey);
            yield return null;
            if (sprint) Press(keyboard.leftShiftKey);
            for (int i = 0; i < 40; i++) yield return null;

            float startZ = player.transform.position.z;
            float startY = player.transform.position.y;
            Press(keyboard.spaceKey);
            float previous = startY;
            for (int i = 0; i < 200; i++)
            {
                yield return null;
                float y = player.transform.position.y;
                bool descending = y < previous || (i > 5 && motor.IsGrounded);
                if (descending && y <= startY + rise + 0.005f) break;
                previous = y;
            }
            result(player.transform.position.z - startZ);

            foreach (var key in new[] { keyboard.wKey, keyboard.spaceKey, keyboard.leftShiftKey })
            {
                Release(key);
                yield return null;
            }
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(ground);
        }

        [UnityTest]
        public IEnumerator Sakrash_masofalarini_olchash()
        {
            foreach (float rise in new[] { 0f, 0.4f, 0.8f, 1.1f })
            {
                float walk = 0f, run = 0f;
                yield return Measure(false, rise, d => walk = d);
                yield return Measure(true, rise, d => run = d);
                Debug.Log($"METRIC rise={rise:0.0} walk={walk:0.00} sprint={run:0.00}");
                // CourseBuilder oddiy yurishda 1.6 m, yugurishda 2.6 m oraliqdan oshmaydi.
                if (rise <= 0.8f)
                {
                    Assert.That(walk, Is.InRange(1.8f, 4f), $"rise {rise}: yurib sakrash masofasi");
                    Assert.That(run, Is.InRange(3.2f, 7f), $"rise {rise}: yugurib sakrash masofasi");
                }
            }
        }
    }
}
