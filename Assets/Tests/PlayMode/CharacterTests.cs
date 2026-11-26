using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace OnlyUp.Tests
{
    public class CharacterTests : InputTestFixture
    {
        PrefsGuard prefs;
        Keyboard keyboard;
        GameObject ground, wall, player;
        PlayerMotor motor;
        PlayerCharacter character;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Time.captureDeltaTime = 1f / 60f;
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            player = Object.Instantiate(prefab, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            motor = player.GetComponent<PlayerMotor>();
            character = player.GetComponent<PlayerCharacter>();
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(ground);
            if (wall != null) Object.DestroyImmediate(wall);
            prefs.Restore();
            base.TearDown();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        Animator Animator => character.Model.GetComponent<Animator>();
        string Current => character.Model.GetComponent<CharacterAnimator>().Current;
        float Height(HumanBodyBones bone) => Animator.GetBoneTransform(bone).position.y - player.transform.position.y;

        [UnityTest]
        public IEnumerator Uchta_personaj_bor_va_hammasi_togri_turadi()
        {
            Assert.AreEqual(3, character.Roster.Count);
            Assert.AreEqual(3, character.Roster.characters.Select(c => c.displayName).Distinct().Count());
            for (int i = 0; i < 3; i++)
            {
                character.Show(i);
                yield return Frames(40);
                Assert.IsTrue(Animator.isHuman, "Humanoid emas");
                Assert.AreEqual(1, player.GetComponentsInChildren<Animator>().Length);
                Assert.AreEqual(CharacterAnimator.Locomotion, Current);
                // T-pozada qolmagan va yerda tik turibdi.
                Assert.That(Height(HumanBodyBones.Head), Is.InRange(1.35f, 1.85f), "Bosh balandligi");
                Assert.That(Height(HumanBodyBones.LeftFoot), Is.InRange(-0.05f, 0.3f), "Oyoq balandligi");
                Assert.Less(Height(HumanBodyBones.LeftHand), 1.2f, "Qo'llar T-pozada");

                // Model orqaga qarab qolmagan: oyoq uchi oldinda, chap qo'l chapda.
                foreach (var (foot, toes) in new[]
                         {
                             (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
                             (HumanBodyBones.RightFoot, HumanBodyBones.RightToes)
                         })
                {
                    Vector3 step = Animator.GetBoneTransform(toes).position - Animator.GetBoneTransform(foot).position;
                    Assert.Greater(Vector3.Dot(step, player.transform.forward), 0.05f, $"{i}: model teskari qaragan");
                }
                var leftHand = Animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                Assert.Less(player.transform.InverseTransformPoint(leftHand).x, 0f, $"{i}: qo'llar almashgan");
            }
        }

        [UnityTest]
        public IEnumerator Yurganda_va_sakraganda_animatsiya_almashadi()
        {
            yield return Frames(30);
            Press(keyboard.wKey);
            yield return Frames(40);
            Assert.AreEqual(CharacterAnimator.Locomotion, Current);
            Assert.Greater(Animator.GetFloat("Speed"), 3f);

            // Oyoqlar haqiqatan harakatlanayotganini tekshirish.
            var foot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            float min = 10f, max = -10f;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                float z = player.transform.InverseTransformPoint(foot.position).z;
                min = Mathf.Min(min, z);
                max = Mathf.Max(max, z);
            }
            Assert.Greater(max - min, 0.25f, "Yugurish animatsiyasi ishlamayapti");
            foreach (var info in Animator.GetCurrentAnimatorClipInfo(0))
                Assert.IsTrue(info.clip.isLooping, info.clip.name + " takrorlanmaydi");
            Release(keyboard.wKey);
            yield return Frames(30);

            Press(keyboard.spaceKey);
            yield return Frames(3);
            Assert.AreEqual(CharacterAnimator.JumpStart, Current);
            yield return Frames(25);
            Assert.AreEqual(CharacterAnimator.Air, Current);
            int guard = 0;
            while (!motor.IsGrounded && guard++ < 120) yield return null;
            yield return Frames(2);
            Assert.AreEqual(CharacterAnimator.Land, Current);
            yield return Frames(40);
            Assert.AreEqual(CharacterAnimator.Locomotion, Current);
        }

        [UnityTest]
        public IEnumerator Osilganda_qollar_qirraga_yetadi()
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.3f, 2f);
            wall.transform.localScale = new Vector3(3f, 2.6f, 2f);
            Physics.SyncTransforms();

            for (int i = 0; i < 3; i++)
            {
                character.Show(i);
                motor.Teleport(new Vector3(0f, 0.05f, 0f));
                yield return Frames(20);
                Press(keyboard.wKey);
                Press(keyboard.spaceKey);
                for (int f = 0; f < 90 && motor.State != MotorState.Hanging; f++) yield return null;
                Release(keyboard.wKey);
                Release(keyboard.spaceKey);
                yield return Frames(40);

                Assert.AreEqual(CharacterAnimator.Hang, Current);
                Assert.IsTrue(motor.TryGetLedgeEdge(out var edge));
                foreach (var bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    var hand = Animator.GetBoneTransform(bone).position;
                    Assert.Less(Vector3.Distance(hand, edge), 0.45f, $"{i}: {bone} qirradan uzoq: {hand} / {edge}");
                }

                Press(keyboard.sKey);
                yield return Frames(3);
                Release(keyboard.sKey);
                yield return Frames(60);
            }
        }
    }
}
