using System.Collections;
using System.Linq;
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
    /// <summary>Sinov sahnasidagi yo'lni faqat klaviatura tugmalari bilan bosib o'tadi.</summary>
    public class CourseTests : InputTestFixture
    {
        PrefsGuard prefs;
        Keyboard keyboard;
        PlayerMotor motor;
        Transform steer;
        System.Collections.Generic.Dictionary<MovingPlatform, Vector3> startPoints;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTearDown]
        public IEnumerator CloseCourseScene()
        {
            // Sinov sahnasi keyingi testlarga xalaqit bermasligi uchun yopiladi.
            var course = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Empty" + Time.frameCount));
            yield return SceneManager.UnloadSceneAsync(course);
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            prefs.Restore();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Sinov_sahnasida_yerdan_oxirgi_platformagacha_chiqish_mumkin()
        {
            yield return Open("Assets/Scenes/TestScene.unity");
            var steps = new[] { "Step1", "Step2", "Step3", "Air1", "Air2", "Air3", "Beam" }
                .Select(n => GameObject.Find(n).GetComponent<Collider>()).ToArray();
            yield return Climb(steps);
        }

        [UnityTest]
        public IEnumerator Xaritani_boshidan_marragacha_bosib_otish_mumkin()
        {
            yield return Open("Assets/Scenes/Course.unity");
            var route = Object.FindAnyObjectByType<CourseRoute>();
            Assert.Greater(route.steps.Length, 40);
            yield return Climb(route.steps);
            Assert.GreaterOrEqual(motor.transform.position.y, 49.5f, "Marra 50 m atrofida bo'lishi kerak");
            var session = Object.FindAnyObjectByType<GameSession>();
            Assert.IsTrue(session.Finished, "Marra aniqlanmadi");
            for (int i = 0; i < 200 && !Object.FindAnyObjectByType<UI.GameHud>().VictoryVisible; i++)
                yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<UI.GameHud>().VictoryVisible);
            Assert.Greater(SaveSystem.BestTime, 10f);
        }

        [UnityTest]
        public IEnumerator Bezaklar_yol_ustiga_tushmagan()
        {
            yield return Open("Assets/Scenes/World.unity");
            var route = Object.FindAnyObjectByType<CourseRoute>();
            // Tosh bezaklarining fizik chegarasi WorldArt ichida turadi.
            var scenery = GameObject.Find("Scenery").GetComponentsInChildren<Collider>()
                .Concat(GameObject.Find("WorldArt").GetComponentsInChildren<Collider>()).ToArray();
            Assert.Greater(scenery.Length, 40, "Bezak juda kam");
            foreach (var step in route.steps)
            {
                var space = step.bounds;
                space.Expand(new Vector3(5f, 6f, 5f));
                foreach (var item in scenery)
                    Assert.IsFalse(space.Intersects(item.bounds), $"{item.name} yo'lga yaqin: {step.name}");
            }
        }

        [UnityTest]
        public IEnumerator Balanddan_yiqilgan_oyinchi_pastroqda_ushlanib_qoladi()
        {
            yield return Open("Assets/Scenes/World.unity");
            var route = Object.FindAnyObjectByType<CourseRoute>();
            int samples = 0, caught = 0;
            float deepest = 0f;
            foreach (var step in route.steps.Where(s => s.bounds.max.y > 20f))
            {
                // Platformaning ikki yonidan 1,2 m narida pastga qaraladi.
                float half = step.transform.localScale.x * 0.5f + 1.2f;
                foreach (float side in new[] { -half, half })
                {
                    var from = step.bounds.center + step.transform.right * side;
                    from.y = step.bounds.max.y;
                    samples++;
                    if (!Physics.Raycast(from, Vector3.down, out var hit, 24f)) continue;
                    caught++;
                    deepest = Mathf.Max(deepest, hit.distance);
                }
            }
            Debug.Log($"CATCH {caught}/{samples}, eng chuqur {deepest:0.0} m");
            Assert.Greater(caught, samples * 0.9f, $"Ushlab qolish kam: {caught}/{samples}");
        }

        [UnityTest]
        public IEnumerator Ushlab_qoluvchi_maydonlar_platforma_ustini_tosmaydi()
        {
            yield return Open("Assets/Scenes/World.unity");
            var route = Object.FindAnyObjectByType<CourseRoute>();
            var decks = GameObject.Find("CatchDecks").GetComponentsInChildren<Collider>();
            Assert.Greater(decks.Length, 20);
            foreach (var step in route.steps)
            {
                // Turish va sakrash uchun joy.
                var head = new Bounds(step.bounds.center, step.bounds.size);
                head.SetMinMax(new Vector3(head.min.x, step.bounds.max.y + 0.1f, head.min.z),
                    new Vector3(head.max.x, step.bounds.max.y + 3f, head.max.z));
                foreach (var deck in decks)
                    Assert.IsFalse(head.Intersects(deck.bounds), $"{deck.name} {step.name} ustini to'sgan");
            }
        }

        [UnityTest]
        public IEnumerator Atrofdagi_manzara_butun_va_yolga_xalaqit_bermaydi()
        {
            foreach (var path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/World.unity" })
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                    .Where(f => f.name == "Slopes" || f.name == "Forest").ToArray();
                Assert.Greater(filters.Length, 10, path + ": tog'lar yo'q");
                foreach (var mesh in filters.Select(f => f.sharedMesh).Distinct())
                {
                    // NaN nuqta tog'da qora chetli teshik bo'lib ko'rinadi.
                    foreach (var p in mesh.vertices.Concat(mesh.normals))
                        Assert.IsTrue(float.IsFinite(p.x + p.y + p.z), $"{mesh.name}: buzilgan nuqta");
                }
            }

            var route = Object.FindAnyObjectByType<CourseRoute>();
            var land = GameObject.Find("Landscape");
            Assert.IsEmpty(land.GetComponentsInChildren<Collider>(), "Manzarada fizik chegara bo'lmasligi kerak");
            // Qo'zg'almas manzara yo'l ustiga chiqib qolmasin.
            var mountains = land.transform.Find("Mountains").GetComponentsInChildren<MeshRenderer>();
            foreach (var step in route.steps)
            foreach (var mountain in mountains)
            {
                var area = mountain.bounds;
                var local = mountain.transform.InverseTransformPoint(step.bounds.center);
                // Birlik tog'ning radiusi 100 m: yo'l undan tashqarida bo'lishi kerak.
                Assert.Greater(new Vector2(local.x, local.z).magnitude, 100f,
                    $"{step.name} {mountain.transform.parent.name} ichida ({area.center})");
            }
        }

        /// <summary>
        /// Sahnaga saqlangan komponent o'z nomidagi faylda turishi shart. Aks holda editorda hammasi
        /// ishlaydi, tayyor o'yin esa sahnani o'qiy olmaydi ("level0 is corrupted").
        /// </summary>
        [Test]
        public void Sahnadagi_har_bir_komponent_oz_nomidagi_faylda()
        {
            var pattern = new System.Text.RegularExpressions.Regex(
                @"m_Script: \{fileID: 11500000, guid: (\w+), type: 3\}\s+m_Name:.*\s+" +
                @"m_EditorClassIdentifier: OnlyUp\.Runtime::(\S+)");
            int checkedCount = 0;
            foreach (var scene in System.IO.Directory.GetFiles("Assets/Scenes", "*.unity"))
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(System.IO.File.ReadAllText(scene)))
            {
                string file = UnityEditor.AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                string type = match.Groups[2].Value.Split('.').Last();
                Assert.AreEqual(type, System.IO.Path.GetFileNameWithoutExtension(file), $"{scene}: {type}");
                checkedCount++;
            }
            Assert.Greater(checkedCount, 20, "Sahnalarda komponent topilmadi: tekshiruv ishlamayapti");
        }

        [UnityTest]
        public IEnumerator Platforma_korinishi_tosiq_bilan_mos()
        {
            yield return Open("Assets/Scenes/World.unity");
            var route = Object.FindAnyObjectByType<CourseRoute>();
            var movers = route.steps.Where(s => s.GetComponent<CarryingSurface>() != null).ToArray();
            Assert.IsNotEmpty(movers);
            for (int i = 0; i < 90; i++) yield return null;

            foreach (var step in movers)
            {
                var art = step.transform.Find("Art").GetComponent<Renderer>();
                Assert.Less(Vector3.Distance(art.bounds.center, step.bounds.center), 0.05f,
                    $"{step.name}: ko'rinish platformadan ajralib qolgan");
            }

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null) continue;
                Assert.IsNotNull(filter.sharedMesh, $"{renderer.name}: mesh yo'qolgan");
                foreach (var material in renderer.sharedMaterials)
                    Assert.IsNotNull(material, $"{renderer.name}: material yo'qolgan");
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Asosiy_xaritani_boshidan_marragacha_bosib_otish_mumkin()
        {
            yield return Open("Assets/Scenes/World.unity");
            var route = Object.FindAnyObjectByType<CourseRoute>();
            Assert.Greater(route.steps.Length, 130);
            yield return Climb(route.steps);

            Assert.That(motor.transform.position.y, Is.InRange(150f, 200f), "Marra 150-200 m orasida bo'lishi kerak");
            var session = Object.FindAnyObjectByType<GameSession>();
            var hud = Object.FindAnyObjectByType<UI.GameHud>();
            Assert.IsTrue(session.Finished, "Marra aniqlanmadi");
            for (int i = 0; i < 200 && !hud.VictoryVisible; i++) yield return null;
            Assert.IsTrue(hud.VictoryVisible, "G'alaba ekrani chiqmadi");
            Assert.IsTrue(session.Paused);
            Assert.IsFalse(hud.PauseMenuVisible);
            StringAssert.Contains(SaveSystem.FormatTime(session.PlayTime), hud.VictoryText);
            StringAssert.Contains(SaveSystem.FormatHeight(session.Height), hud.VictoryText);

            GameObject.Find("PlayAgain").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse(hud.VictoryVisible);
            Assert.IsFalse(session.Finished);
            Assert.Less(session.Height, 0.2f);
            Assert.AreEqual(1f, Time.timeScale);
        }

        /// <summary>Harakatlanuvchi platforma yo'lning shu chetida turibdimi.</summary>
        static bool At(MovingPlatform mover, Vector3 end) => Vector3.Distance(mover.transform.position, end) < 0.3f;

        IEnumerator Open(string path)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
            // Platformalar hali yurmagan paytdagi joylari yozib olinadi.
            startPoints = Object.FindObjectsByType<MovingPlatform>().ToDictionary(m => m, m => m.transform.position);
            yield return null;
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            // Bot yo'nalishni shu "kamera"ni burib tanlaydi, qolgani oddiy W tugmasi.
            steer = new GameObject("Steer").transform;
            motor.CameraTransform = steer;
            for (int i = 0; i < 20; i++) yield return null;
        }

        IEnumerator Climb(Collider[] steps)
        {
            var player = motor.transform;
            bool forward = false;
            // Har bir harakatlanuvchi platformaning boshlang'ich joyi (u yurishni boshlashidan oldin emas,
            // yurish chizig'ining ikki cheti kerak).
            var origins = steps.Select(s => s.GetComponent<MovingPlatform>())
                .Where(m => m != null).ToDictionary(m => m, m => startPoints[m]);

            for (int index = 0; index < steps.Length; index++)
            {
                var target = steps[index];
                var mover = target.GetComponent<MovingPlatform>();
                var riding = index > 0 ? steps[index - 1].GetComponent<MovingPlatform>() : null;

                // Platforma yaqin chetiga kelguncha (minish uchun) yoki narigi chetiga
                // yetguncha (undan tushish uchun) joyida kutadi.
                if (mover != null || riding != null)
                {
                    var waitFor = mover != null ? mover : riding;
                    var end = mover != null ? origins[mover] : origins[riding] + riding.travel;
                    // Chetga endi yaqinlashayotgan paytni tutadi, shunda sakrashga vaqt qoladi.
                    bool wasNear = At(waitFor, end);
                    int guard = 0;
                    while (guard++ < 1500)
                    {
                        yield return null;
                        bool near = At(waitFor, end);
                        if (near && !wasNear) break;
                        wasNear = near;
                    }
                    Assert.Less(guard, 1500, $"{target.name}: platforma kelmadi");
                }

                bool pad = target.GetComponent<BouncePad>() != null;
                bool landed = false, jumpHeld = false;
                int sinceJump = 0;

                for (int frame = 0; frame < 420 && !landed; frame++)
                {
                    Vector3 to = target.bounds.center - player.position;
                    to.y = 0f;
                    bool hanging = motor.State == MotorState.Hanging || motor.State == MotorState.Climbing;
                    bool wantForward = hanging || to.magnitude > 0.25f;
                    if (!hanging && to.sqrMagnitude > 0.01f) steer.rotation = Quaternion.LookRotation(to);

                    if (wantForward != forward)
                    {
                        if (wantForward) Press(keyboard.wKey);
                        else Release(keyboard.wKey);
                        forward = wantForward;
                    }
                    else if (jumpHeld && ++sinceJump > 6 && motor.IsGrounded)
                    {
                        Release(keyboard.spaceKey);
                        jumpHeld = false;
                    }
                    else if (!jumpHeld && motor.IsGrounded && wantForward)
                    {
                        Vector3 near = target.ClosestPoint(player.position);
                        near.y = player.position.y;
                        bool close = Vector3.Distance(near, player.position) < 0.7f;
                        bool edge = !Physics.Raycast(player.position + to.normalized * 0.3f + Vector3.up * 0.3f,
                            Vector3.down, 0.7f);
                        if (close || edge)
                        {
                            Press(keyboard.spaceKey);
                            jumpHeld = true;
                            sinceJump = 0;
                        }
                    }

                    yield return null;

                    bool onTop = Mathf.Abs(player.position.y - target.bounds.max.y) < 0.15f;
                    landed = pad
                        ? motor.Velocity.y > 10f && to.magnitude < 1.4f
                        : motor.IsGrounded && to.magnitude < 0.6f && onTop;
                    // Oxirgi platformada marra aniqlanishi kifoya.
                    var game = Object.FindAnyObjectByType<GameSession>();
                    if (index == steps.Length - 1 && game != null && game.Finished) landed = true;
                }

                Assert.IsTrue(landed,
                    $"{index}-qadam ({target.name}) ga chiqib bo'lmadi, o'yinchi: {player.position}, nishon: {target.bounds.center}");

                // Batutdan keyin to'xtamasdan keyingi platformaga yo'naladi.
                if (pad) continue;
                if (forward) { Release(keyboard.wKey); forward = false; yield return null; }
                if (jumpHeld) Release(keyboard.spaceKey);
                for (int i = 0; i < 15; i++) yield return null;
            }
        }
    }
}
