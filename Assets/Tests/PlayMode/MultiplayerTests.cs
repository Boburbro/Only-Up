using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using OnlyUp.Net;
using OnlyUp.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace OnlyUp.Tests
{
    /// <summary>
    /// Bitta jarayonda ikki va undan ko'p xona nusxasi haqiqiy tarmoq paketlari bilan (127.0.0.1)
    /// bog'lanadi. Relay (internet) bu testlarga kirmaydi.
    /// </summary>
    public class MultiplayerTests : InputTestFixture
    {
        PrefsGuard prefs;
        readonly System.Collections.Generic.List<NetRoom> rooms = new System.Collections.Generic.List<NetRoom>();
        static ushort port = 17800;

        public override void Setup()
        {
            base.Setup();
            prefs = PrefsGuard.Take();
            port++;
        }

        [UnityTearDown]
        public IEnumerator Down()
        {
            // Mijozlar xona egasidan oldin yopiladi.
            for (int i = rooms.Count - 1; i >= 0; i--)
                if (rooms[i] != null) rooms[i].Leave();
            for (float wait = Time.realtimeSinceStartup + 5f;
                 rooms.Any(r => r != null) && Time.realtimeSinceStartup < wait;) yield return null;
            Assert.IsTrue(rooms.All(r => r == null), "Xona obyekti o'chirilmadi");
            rooms.Clear();
            NetRoom.Current = null;
            NetRoom.LastMessage = null;
            WorldClock.Shared = null;
            var scene = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("NetEmpty" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
            Time.timeScale = 1f;
            prefs.Restore();
        }

        public override void TearDown() => base.TearDown();

        static PlayerInfo Info(string name, int character = 0) =>
            new PlayerInfo { Name = name, Character = character };

        NetRoom Host(string name, bool webSockets = false)
        {
            var room = NetRoom.Create();
            rooms.Add(room);
            room.HostDirect(port, Info(name, 0), webSockets);
            return room;
        }

        NetRoom Join(string name, int character = 1, bool webSockets = false)
        {
            var room = NetRoom.Create();
            rooms.Add(room);
            room.JoinDirect("127.0.0.1", port, Info(name, character), webSockets);
            return room;
        }

        static IEnumerator Until(System.Func<bool> condition, string failure, float seconds = 8f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, failure);
                yield return null;
            }
        }

        static PlayerPose Pose(float x, float height) => new PlayerPose
        {
            Position = new Vector3(x, height + 0.05f, 2f), Yaw = 90f, State = MotorState.Grounded, Height = height
        };

        static IEnumerator Exchange(bool webSockets, MultiplayerTests t)
        {
            var host = t.Host("Egasi", webSockets);
            var guest = t.Join("Guest Ali", 2, webSockets);
            var third = t.Join("Vali", 1, webSockets);
            yield return Until(() => guest.Ready && third.Ready && guest.Players.Count == 3 &&
                                     third.Players.Count == 3 && host.Players.Count == 3, "Uch o'yinchi ulanmadi");
            Assert.AreEqual("Guest Ali", third.Players[guest.LocalId].Name);
            Assert.AreEqual(2, third.Players[guest.LocalId].Character);

            host.SetLocalPose(Pose(1f, 3f));
            guest.SetLocalPose(Pose(2f, 7f));
            third.SetLocalPose(Pose(3f, 5f));
            yield return Until(() => host.Poses.Count == 2 && guest.Poses.Count == 2 && third.Poses.Count == 2,
                "Holatlar tarqalmadi");
            // Mijoz boshqa mijozning holatini xona egasi orqali oladi.
            Assert.AreEqual(7f, third.Poses[guest.LocalId].Height, 0.001f);
            Assert.AreEqual(1f, guest.Poses[host.LocalId].Position.x, 0.001f);
            Assert.IsFalse(guest.Poses.ContainsKey(guest.LocalId), "O'zining nusxasi ko'rinmasligi kerak");

            for (float wait = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < wait;) yield return null;
            Assert.AreEqual(host.Time, guest.Time, 0.15, "Soatlar mos emas");

            guest.Leave();
            yield return Until(() => host.Players.Count == 2 && third.Players.Count == 2 && third.Poses.Count == 1,
                "Chiqib ketgan o'yinchi ro'yxatda qoldi");

            host.Leave();
            yield return Until(() => third.Closed, "Xona egasi chiqqani bilinmadi");
            Assert.IsNotEmpty(third.CloseReason);
        }

        [UnityTest]
        public IEnumerator Uch_oyinchi_ulanadi_va_holatlar_tarqaladi() => Exchange(false, this);

        [UnityTest]
        public IEnumerator Websocket_orqali_ham_ishlaydi() => Exchange(true, this);

        [UnityTest]
        public IEnumerator Tola_xonaga_kirib_bolmaydi()
        {
            var host = Host("Egasi");
            var guests = Enumerable.Range(1, NetRoom.MaxPlayers - 1).Select(i => Join("Oyinchi " + i)).ToArray();
            yield return Until(() => host.Players.Count == NetRoom.MaxPlayers && guests.All(g => g.Ready),
                "8 o'yinchi sig'madi", 15f);

            // Eng og'ir holat: 8 o'yinchining hammasi osilib turibdi, paket baribir yetib borishi kerak.
            var heavy = Pose(1f, 2f);
            heavy.HasLedge = true;
            heavy.Ledge = Vector3.one;
            host.SetLocalPose(heavy);
            foreach (var guest in guests) guest.SetLocalPose(heavy);
            yield return Until(() => guests.All(g => g.Poses.Count == NetRoom.MaxPlayers - 1),
                "To'la xonada holatlar yetib bormadi");

            var extra = Join("Ortiqcha");
            yield return Until(() => extra.Closed, "To'la xona rad etmadi");
            Assert.AreEqual("The room is full", extra.CloseReason);
            Assert.AreEqual(NetRoom.MaxPlayers, host.Players.Count);
        }

        [UnityTest]
        public IEnumerator Oyinda_boshqa_oyinchi_korinadi_va_qoidalar_ozgaradi()
        {
            PlayerProfile.Nickname = "Egasi";
            var host = Host("Egasi");
            var guest = Join("Dostim", 1);
            yield return Until(() => guest.Ready && host.Players.Count == 2, "Ulanmadi");
            NetRoom.Current = host;

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/World.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("World"));
            for (int i = 0; i < 30; i++) yield return null;

            var session = Object.FindAnyObjectByType<GameSession>();
            var game = Object.FindAnyObjectByType<MultiplayerGame>();
            Assert.IsTrue(session.Multiplayer);
            Assert.IsNotNull(game);

            var pose = Pose(2f, 12f);
            pose.State = MotorState.Airborne;
            guest.SetLocalPose(pose);
            yield return Until(() => game.Avatars.Count == 1, "Do'st ko'rinmadi");
            var avatar = game.Avatars[guest.LocalId];
            yield return Until(() => Vector3.Distance(avatar.transform.position, pose.Position) < 0.1f,
                "Do'st o'z joyiga bormadi");
            Assert.IsEmpty(avatar.GetComponentsInChildren<Collider>(), "Do'st bilan to'qnashmasligi kerak");
            var animator = avatar.GetComponentInChildren<CharacterAnimator>();
            yield return Until(() => animator.Current == CharacterAnimator.Air, "Animatsiya holatga mos emas");

            // Mijoz xona egasining holatini oladi.
            yield return Until(() => guest.Poses.ContainsKey(host.LocalId), "Egasining holati yetib bormadi");

            var hud = Object.FindAnyObjectByType<RoomHud>();
            yield return Until(() => hud.ListText.Contains("12.0 m") && hud.Tags.Count == 1, "Ro'yxat yoki nik yo'q");
            StringAssert.StartsWith("1. Dostim", hud.ListText, "Balandroqdagi birinchi bo'lishi kerak");
            StringAssert.Contains("(2/8)", hud.CodeText);

            // Harakatlanuvchi platformalar xona soatiga ulangan.
            Assert.IsNotNull(WorldClock.Shared);
            Assert.AreEqual(host.Time, WorldClock.Shared(), 0.01);

            float time = session.PlayTime;
            session.SetPaused(true);
            Assert.AreEqual(1f, Time.timeScale, "Xonada pauza vaqtni to'xtatmaydi");
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(session.PlayTime, time);
            session.SetPaused(false);
            session.Save();
            Assert.IsFalse(SaveSystem.HasSave, "Xonadagi o'yin saqlanmasligi kerak");

            guest.Leave();
            yield return Until(() => game.Avatars.Count == 0 && hud.Tags.Count == 0, "Chiqqan do'st qolib ketdi");
        }

        [UnityTest]
        public IEnumerator Menyuda_xona_oynasi_nik_va_kodni_tekshiradi()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainMenu.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("MainMenu"));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenu>();

            GameObject.Find("OpenRooms").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsTrue(menu.Rooms.Root.activeSelf);
            StringAssert.Contains("nickname", menu.Rooms.Status);

            menu.NameInput.text = "Sinovchi";
            menu.Rooms.CodeInput.text = " ab-1 2 ";
            Assert.AreEqual("AB12", menu.Rooms.CodeInput.text);
            GameObject.Find("JoinRoom").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual("Enter the full code", menu.Rooms.Status);

            GameObject.Find("CloseRooms").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(menu.Rooms.Root.activeSelf);
        }
    }
}
