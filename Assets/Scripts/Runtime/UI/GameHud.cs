using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnlyUp.UI
{
    /// <summary>O'yin ustidagi yozuvlar, pauza menyusi va g'alaba ekrani.</summary>
    [RequireComponent(typeof(GameSession))]
    public class GameHud : MonoBehaviour
    {
        GameSession session;
        Text heightLabel, timeLabel, nameLabel, finishLabel;
        GameObject pauseMenu, settings, victory;
        Button restartButton;
        bool victoryPending;
        bool restartArmed;
        float finishTimer;

        public string HeightText => heightLabel.text;
        public string TimeText => timeLabel.text;
        public string NameText => nameLabel.text;
        public bool PauseMenuVisible => pauseMenu.activeSelf;
        public bool VictoryVisible => victory.activeSelf;
        public string VictoryText => finishLabel.text;

        void Awake()
        {
            session = GetComponent<GameSession>();
            Build();
            session.PauseChanged += OnPauseChanged;
            session.ReachedFinish += OnFinish;
        }

        void OnDestroy()
        {
            AdBanners.Set(AdBanners.Mode.Hidden);
            session.PauseChanged -= OnPauseChanged;
            session.ReachedFinish -= OnFinish;
        }

        void Start() => nameLabel.text = PlayerProfile.Nickname;

        void LateUpdate()
        {
            // Sozlamalar va g'alaba oynasi keng: banner ularni to'sib qo'ymasin.
            AdBanners.Set(settings.activeSelf || victory.activeSelf ? AdBanners.Mode.Hidden
                : session.Paused ? AdBanners.Mode.Clickable : AdBanners.Mode.Visible);
            heightLabel.text = SaveSystem.FormatHeight(session.Height);
            timeLabel.text = SaveSystem.FormatTime(session.PlayTime);
            // Qo'nish ko'rinib olishi uchun g'alaba ekrani biroz kechroq chiqadi.
            if (victoryPending && (finishTimer -= Time.deltaTime) <= 0f) ShowVictory();
        }

        void Build()
        {
            UiKit.EnsureEventSystem();
            var canvas = UiKit.CreateCanvas("HudCanvas").transform;

            var motor = session.Motor;
            if (TouchControls.Needed && motor != null)
                TouchControls.Create(canvas, session, motor.GetComponent<PlayerInputReader>());

            var safe = new GameObject("SafeHud", typeof(RectTransform), typeof(SafeArea))
                .GetComponent<RectTransform>();
            safe.SetParent(canvas, false);
            // SafeHud butun xavfsiz sohani egallaydi, hisoblagich uning tepa o'rtasida turadi.
            FpsMeter.Create(safe);

            heightLabel = Corner(safe, new Vector2(0f, 1f), new Vector2(70f, -50f), TextAnchor.UpperLeft, 56);
            timeLabel = Corner(safe, new Vector2(1f, 1f), new Vector2(-70f, -50f), TextAnchor.UpperRight, 56);
            // Telefonda pastki chap burchakni joystik egallaydi, shuning uchun nik balandlik ostiga o'tadi.
            nameLabel = TouchControls.Needed
                ? Corner(safe, new Vector2(0f, 1f), new Vector2(70f, -120f), TextAnchor.UpperLeft, 34)
                : Corner(safe, new Vector2(0f, 0f), new Vector2(70f, 40f), TextAnchor.LowerLeft, 34);

            var shade = UiKit.Stretch(canvas, "PauseMenu", new Color(0f, 0f, 0f, 0.55f));
            pauseMenu = shade.gameObject;
            var panel = UiKit.Box(shade, "Window", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(720f, 640f), UiKit.Panel);
            UiKit.Label(panel, session.Multiplayer ? "Menu" : "Paused", new Vector2(0f, 240f), new Vector2(600f, 90f), 64);
            var size = new Vector2(520f, 86f);
            UiKit.Button(panel, "Resume", "Resume", new Vector2(0f, 110f), size,
                () => session.SetPaused(false), UiKit.Accent);
            UiKit.Button(panel, "PauseSettings", "Settings", new Vector2(0f, 0f), size,
                () => settings.SetActive(true));
            restartButton = UiKit.Button(panel, "Restart", RestartText, new Vector2(0f, -110f), size, Restart);
            UiKit.Button(panel, "ToMenu", "Main menu", new Vector2(0f, -220f), size, ToMenu);
            pauseMenu.SetActive(false);

            BuildVictory(canvas);

            settings = SettingsPanel.Create(canvas, () =>
            {
                settings.SetActive(false);
                GameSettings.Apply();
            });
            settings.SetActive(false);
        }

        void BuildVictory(Transform canvas)
        {
            var shade = UiKit.Stretch(canvas, "Victory", new Color(0f, 0f, 0f, 0.55f));
            victory = shade.gameObject;
            var panel = UiKit.Box(shade, "Window", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(860f, 760f), UiKit.Panel);
            var title = UiKit.Label(panel, "YOU WIN!", new Vector2(0f, 290f), new Vector2(800f, 100f), 80,
                UiKit.Accent);
            title.fontStyle = FontStyle.Bold;
            finishLabel = UiKit.Label(panel, "", new Vector2(0f, 105f), new Vector2(780f, 250f), 40);
            finishLabel.lineSpacing = 1.25f;
            var size = new Vector2(560f, 86f);
            UiKit.Button(panel, "PlayAgain", "Play again", new Vector2(0f, -95f), size, () =>
            {
                victory.SetActive(false);
                session.Restart();
            }, UiKit.Accent);
            UiKit.Button(panel, "LookAround", "Look around", new Vector2(0f, -200f), size,
                () => session.SetPaused(false));
            UiKit.Button(panel, "VictoryMenu", "Main menu", new Vector2(0f, -305f), size, ToMenu);
            victory.SetActive(false);
        }

        void ShowVictory()
        {
            victoryPending = false;
            session.SetPaused(true);
            // Pauza menyusi o'rniga g'alaba oynasi ko'rsatiladi.
            pauseMenu.SetActive(false);
            victory.SetActive(true);
        }

        static Text Corner(Transform canvas, Vector2 anchor, Vector2 position, TextAnchor align, int size)
        {
            var label = UiKit.Label(canvas, "", position, new Vector2(600f, 80f), size, Color.white, align,
                anchor, anchor);
            label.fontStyle = FontStyle.Bold;
            // Yorug' osmonda ham o'qilishi uchun soya.
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return label;
        }

        void OnPauseChanged(bool paused)
        {
            pauseMenu.SetActive(paused);
            victory.SetActive(false);
            settings.SetActive(false);
            Disarm();
            if (paused) victoryPending = false;
            // Tanlangan tugma qolsa, Space bilan sakrash uni qayta "bosib" yuboradi.
            EventSystem.current?.SetSelectedGameObject(null);
        }

        void OnFinish()
        {
            finishLabel.text =
                $"{PlayerProfile.Nickname}\n" +
                $"Height: {SaveSystem.FormatHeight(session.Height)}\n" +
                $"Time: {SaveSystem.FormatTime(session.PlayTime)}\n" +
                $"Best time: {SaveSystem.FormatTime(SaveSystem.BestTime)}";
            victoryPending = true;
            finishTimer = 1.2f;
        }

        void Restart()
        {
            // Tasodifan bosilsa natija yo'qolmasligi uchun ikki marta so'raladi.
            if (!restartArmed)
            {
                restartArmed = true;
                UiKit.SetText(restartButton, "Sure? Press again");
                return;
            }
            session.Restart();
        }

        string RestartText => session.Multiplayer ? "Back to start" : "Restart";

        void Disarm()
        {
            restartArmed = false;
            UiKit.SetText(restartButton, RestartText);
        }

        void ToMenu()
        {
            session.Save();
            Net.NetRoom.Current?.Leave();
            SceneManager.LoadScene(GameScenes.MainMenu);
        }
    }
}
