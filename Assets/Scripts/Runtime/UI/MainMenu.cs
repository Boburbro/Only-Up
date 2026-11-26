using System.Collections;
using System.Threading.Tasks;
using OnlyUp.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnlyUp.UI
{
    /// <summary>O'yin oldidagi ekran: nik, 3 ta personajdan biri va Boshlash.</summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] CharacterRoster roster;
        [SerializeField] Transform previewSpot;
        [SerializeField] float previewTurnSpeed = 20f;

        InputField nameInput;
        Text errorLabel, characterLabel;
        Button startButton, continueButton;
        bool newGameArmed;
        RoomPanel rooms;
        NetRoom pendingRoom;
        GameObject settings, loading, preview;
        Text loadingLabel;
        int index;
        bool starting;

        public int SelectedIndex => index;
        public GameObject Preview => preview;
        public InputField NameInput => nameInput;
        public Button StartButton => startButton;
        public string ErrorText => errorLabel.text;
        public Button ContinueButton => continueButton;

        public void Configure(CharacterRoster newRoster, Transform spot)
        {
            roster = newRoster;
            previewSpot = spot;
        }

        void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UiKit.EnsureEventSystem();
            Build();
            GameSettings.Apply();

            nameInput.SetTextWithoutNotify(PlayerProfile.Nickname);
            Select(PlayerProfile.CharacterIndex);
            Validate();

            // Xonadan chiqqan yoki chiqarilgan bo'lsak, sababi ko'rsatiladi.
            NetRoom.Current?.Leave();
            if (!string.IsNullOrEmpty(NetRoom.LastMessage)) rooms.Show(NetRoom.LastMessage, false);
            NetRoom.LastMessage = null;
        }

        void OnDestroy() => pendingRoom?.Leave();

        void Update()
        {
            // Personaj sekin tebranib turadi, orqasi bilan qolib ketmaydi.
            if (previewSpot != null)
                previewSpot.localRotation = Quaternion.Euler(0f,
                    180f + Mathf.Sin(Time.unscaledTime * previewTurnSpeed * Mathf.Deg2Rad * 2f) * 35f, 0f);
        }

        void Build()
        {
            var canvas = UiKit.CreateCanvas("MenuCanvas").transform;
            var panel = UiKit.Box(canvas, "Panel", new Vector2(0f, 0.5f), new Vector2(480f, 0f),
                new Vector2(760f, 960f), UiKit.Panel);

            var title = UiKit.Label(panel, "TEPAGA!", new Vector2(0f, 380f), new Vector2(700f, 110f), 90, UiKit.Accent);
            title.fontStyle = FontStyle.Bold;
            UiKit.Label(panel, "Yiqilmang va eng tepaga chiqing", new Vector2(0f, 300f),
                new Vector2(700f, 40f), 28, UiKit.Muted);

            UiKit.Label(panel, "Nikingiz", new Vector2(0f, 215f), new Vector2(620f, 40f), 30, null,
                TextAnchor.MiddleLeft);
            nameInput = UiKit.Input(panel, "NameInput", "Masalan: Bobur", new Vector2(0f, 150f),
                new Vector2(620f, 80f), PlayerProfile.MaxNameLength);
            nameInput.onValueChanged.AddListener(_ => Validate());
            nameInput.onSubmit.AddListener(_ =>
            {
                if (continueButton != null) ContinueGame();
                else StartGame();
            });
            errorLabel = UiKit.Label(panel, "", new Vector2(0f, 82f), new Vector2(620f, 44f), 24,
                new Color(1f, 0.62f, 0.5f), TextAnchor.MiddleLeft);

            UiKit.Label(panel, "Personaj", new Vector2(0f, 25f), new Vector2(620f, 40f), 30, null,
                TextAnchor.MiddleLeft);
            UiKit.Button(panel, "Previous", "<", new Vector2(-260f, -45f), new Vector2(100f, 80f),
                () => Select(index - 1), null, 44);
            characterLabel = UiKit.Label(panel, "", new Vector2(0f, -45f), new Vector2(400f, 80f), 40);
            UiKit.Button(panel, "Next", ">", new Vector2(260f, -45f), new Vector2(100f, 80f),
                () => Select(index + 1), null, 44);

            // Saqlangan o'yin bo'lsa, qator ikkiga bo'linadi: Davom etish va Yangi o'yin.
            bool saved = SaveSystem.HasSaveFor(GameScenes.Game);
            startButton = UiKit.Button(panel, "Start", saved ? "Yangi o'yin" : "Boshlash",
                new Vector2(saved ? 160f : 0f, -185f), new Vector2(saved ? 300f : 620f, 100f), StartGame,
                saved ? UiKit.Control : UiKit.Accent, saved ? 32 : 44);
            if (saved)
                continueButton = UiKit.Button(panel, "Continue", "Davom etish", new Vector2(-160f, -185f),
                    new Vector2(300f, 100f), ContinueGame, UiKit.Accent, 34);
            UiKit.Button(panel, "OpenRooms", "Do'stlar bilan o'ynash", new Vector2(0f, -300f),
                new Vector2(620f, 76f), OpenRooms);
            // Brauzerda "Chiqish" ishlamaydi: sahifa shunchaki yopiladi.
            bool canQuit = Application.platform != RuntimePlatform.WebGLPlayer;
            UiKit.Button(panel, "OpenSettings", "Sozlamalar", new Vector2(canQuit ? -160f : 0f, -392f),
                new Vector2(canQuit ? 300f : 620f, 76f), () => settings.SetActive(true));
            if (canQuit)
                UiKit.Button(panel, "Quit", "Chiqish", new Vector2(160f, -392f), new Vector2(300f, 76f), Quit);

            if (SaveSystem.BestHeight > 0f)
            {
                string record = "Rekord: " + SaveSystem.FormatHeight(SaveSystem.BestHeight);
                if (SaveSystem.BestTime > 0f) record += "   Marra: " + SaveSystem.FormatTime(SaveSystem.BestTime);
                UiKit.Label(panel, record, new Vector2(0f, -455f), new Vector2(700f, 36f), 24, UiKit.Muted);
            }

            rooms = new RoomPanel(canvas, HostRoom, JoinRoom, CloseRooms);

            settings = SettingsPanel.Create(canvas, () => settings.SetActive(false));
            settings.SetActive(false);

            loading = UiKit.Stretch(canvas, "Loading", new Color(0.04f, 0.05f, 0.07f, 1f)).gameObject;
            loadingLabel = UiKit.Label(loading.transform, "Yuklanmoqda...", Vector2.zero,
                new Vector2(900f, 80f), 48);
            loading.SetActive(false);
        }

        // ---------- Xona ----------

        public RoomPanel Rooms => rooms;

        void OpenRooms()
        {
            string error = PlayerProfile.NameError(nameInput.text);
            rooms.Show(error == null ? "" : "Avval bosh menyuda nik yozing", false);
        }

        void CloseRooms()
        {
            // Ulanish davom etayotgan bo'lsa, bekor qilinadi.
            if (pendingRoom != null)
            {
                pendingRoom.Leave();
                pendingRoom = null;
                starting = false;
                Validate();
            }
            rooms.Root.SetActive(false);
        }

        void HostRoom() => Connect("Xona ochilmoqda...", (room, info) => room.HostOnlineAsync(info));

        void JoinRoom(string code)
        {
            if (code.Length < 6) rooms.Show("Kodni to'liq yozing", false);
            else Connect("Xonaga ulanilmoqda...", (room, info) => room.JoinOnlineAsync(code, info));
        }

        async void Connect(string message, System.Func<NetRoom, PlayerInfo, Task> open)
        {
            if (starting) return;
            if (PlayerProfile.NameError(nameInput.text) != null)
            {
                rooms.Show("Avval bosh menyuda nik yozing", false);
                return;
            }
            starting = true;
            Validate();
            rooms.Show(message, true);
            var room = pendingRoom = NetRoom.Create();
            string failure = null;
            try
            {
                await open(room, new PlayerInfo { Name = PlayerProfile.Clean(nameInput.text), Character = index });
            }
            catch (System.Exception error)
            {
                Debug.LogWarning($"OnlyUp: xonaga ulanishda xato: {error.Message}");
                failure = RelayConnector.Describe(error);
            }
            // Kutish paytida oyna yopilgan yoki sahna almashgan bo'lishi mumkin.
            if (this == null || pendingRoom != room) return;
            if (failure != null) Fail(failure);
            else StartCoroutine(WaitForRoom(room));
        }

        IEnumerator WaitForRoom(NetRoom room)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (pendingRoom == room && !room.Ready && !room.Closed && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (pendingRoom != room) yield break;
            if (room.Ready) EnterRoom(room);
            else Fail(room.Closed ? room.CloseReason : "Xona javob bermadi");
        }

        void Fail(string reason)
        {
            pendingRoom?.Leave();
            pendingRoom = null;
            starting = false;
            Validate();
            rooms.Show(reason, false);
        }

        /// <summary>Tayyor xona bilan o'yinni boshlaydi.</summary>
        public void EnterRoom(NetRoom room)
        {
            pendingRoom = null;
            NetRoom.Current = room;
            rooms.Root.SetActive(false);
            Launch(false);
        }

        public void Select(int newIndex)
        {
            if (roster == null || roster.Count == 0) return;
            // Ro'yxat aylanma: oxiridan keyin yana birinchisi.
            index = (newIndex % roster.Count + roster.Count) % roster.Count;
            characterLabel.text = $"{roster.Get(index).displayName}  ({index + 1}/{roster.Count})";

            if (preview != null)
            {
                preview.SetActive(false);
                Destroy(preview);
            }
            preview = Instantiate(roster.Get(index).model, previewSpot, false);
        }

        void Validate()
        {
            string error = PlayerProfile.NameError(nameInput.text);
            startButton.interactable = error == null && !starting;
            if (continueButton != null) continueButton.interactable = startButton.interactable;
            errorLabel.text = error ?? "";
        }

        /// <summary>Yangi o'yin. Saqlangan o'yin bo'lsa, o'chirishdan oldin tasdiq so'raydi.</summary>
        public void StartGame()
        {
            if (!CanStart()) return;
            if (continueButton != null && !newGameArmed)
            {
                newGameArmed = true;
                UiKit.SetText(startButton, "O'chirilsinmi?");
                return;
            }
            SaveSystem.Delete();
            Launch(false);
        }

        public void ContinueGame()
        {
            if (CanStart() && continueButton != null) Launch(true);
        }

        bool CanStart() => !starting && PlayerProfile.NameError(nameInput.text) == null;

        void Launch(bool resume)
        {
            starting = true;
            Validate();
            GameSession.ContinueRequested = resume;
            PlayerProfile.Nickname = nameInput.text;
            PlayerProfile.CharacterIndex = index;
            PlayerPrefs.Save();
            StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            loading.SetActive(true);
            // Yuklanish ekrani kamida bir marta chizilishi uchun.
            yield return null;
            var operation = SceneManager.LoadSceneAsync(GameScenes.Game);
            while (!operation.isDone)
            {
                loadingLabel.text = $"Yuklanmoqda... {Mathf.RoundToInt(operation.progress * 100f)}%";
                yield return null;
            }
        }

        static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
