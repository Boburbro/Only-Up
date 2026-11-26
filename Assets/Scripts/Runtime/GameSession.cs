using UnityEngine;

namespace OnlyUp
{
    /// <summary>Bitta o'yin: balandlik, vaqt, pauza, saqlash va marra.</summary>
    public class GameSession : MonoBehaviour
    {
        /// <summary>Menyu "Davom etish" bosilganda true qiladi.</summary>
        public static bool ContinueRequested;

        [SerializeField] PlayerMotor motor;
        [SerializeField] ThirdPersonCamera orbit;
        [SerializeField] Collider finish;
        [SerializeField] float autoSaveInterval = 1f;

        PlayerInputReader input;
        Vector3 spawn;
        float spawnYaw;
        SaveData lastSafe;
        float saveTimer;
        bool dirty;

        public PlayerMotor Motor => motor;
        public float Height => Mathf.Max(0f, motor.transform.position.y - spawn.y);
        public float PlayTime { get; private set; }
        public bool Paused { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>Xonada o'ynalyaptimi: unda pauza vaqtni to'xtatmaydi va o'yin saqlanmaydi.</summary>
        public bool Multiplayer { get; private set; }
        bool hadPointerLock;

        public event System.Action<bool> PauseChanged;
        public event System.Action ReachedFinish;

        public void Configure(PlayerMotor newMotor, ThirdPersonCamera newOrbit, Collider newFinish)
        {
            motor = newMotor;
            orbit = newOrbit;
            finish = newFinish;
        }

        string MapName => gameObject.scene.name;

        void Awake() => Multiplayer = Net.NetRoom.Current != null;

        void Start()
        {
            Time.timeScale = 1f;
            input = motor.GetComponent<PlayerInputReader>();
            spawn = motor.transform.position;
            spawnYaw = motor.transform.eulerAngles.y;
            // Esc va kursorni endi pauza menyusi boshqaradi.
            if (orbit != null)
            {
                orbit.HandlesCursorKeys = false;
                orbit.LockCursor(true);
            }

            var saved = !Multiplayer && ContinueRequested && SaveSystem.HasSaveFor(MapName)
                ? SaveSystem.Read()
                : null;
            ContinueRequested = false;
            if (saved != null)
            {
                lastSafe = saved;
                PlayTime = saved.time;
                Finished = saved.finished;
                Place(saved.position, saved.yaw);
            }
            if (Multiplayer) JoinRoom(Net.NetRoom.Current);
            GameSettings.Apply();
        }

        void JoinRoom(Net.NetRoom room)
        {
            // O'yinchilar boshlanishda bir-birining ichida turmasligi uchun yonma-yon qo'yiladi.
            float side = ((int)(room.LocalId % 4) - 1.5f) * 0.9f;
            Place(spawn + Quaternion.Euler(0f, spawnYaw, 0f) * new Vector3(side, 0f, 0f), spawnYaw);
            gameObject.AddComponent<Net.MultiplayerGame>().Configure(room, this);
        }

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.startButton.wasPressedThisFrame))
            {
                SetPaused(!Paused);
                return;
            }
            KeepPointerLocked();
            if (Paused && !Multiplayer) return;

            if (!Finished) PlayTime += Time.deltaTime;
            SaveSystem.SubmitHeight(Height);
            CheckFinish();

            saveTimer += Time.deltaTime;
            if (IsSafeToSave())
            {
                lastSafe = new SaveData { position = motor.transform.position, yaw = motor.transform.eulerAngles.y };
                dirty = true;
            }
            if (dirty && saveTimer >= autoSaveInterval) Save();
        }

        /// <summary>
        /// Faqat qimirlamaydigan sirt ustida turilgan joy saqlanadi: havoda, qirrada,
        /// harakatlanuvchi platformada yoki batutda saqlansa, qaytganda yiqilib ketish mumkin.
        /// </summary>
        bool IsSafeToSave()
        {
            if (!motor.IsGrounded) return false;
            if (!Physics.Raycast(motor.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<CarryingSurface>() == null &&
                   hit.collider.GetComponent<BouncePad>() == null;
        }

        /// <summary>
        /// Brauzer kursorni faqat bosishdan keyin qulflaydi va Esc bosilganda o'zi bo'shatadi
        /// (bu tugma o'yinga yetib kelmasligi mumkin).
        /// </summary>
        void KeepPointerLocked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Paused || UI.TouchControls.Needed || orbit == null) { hadPointerLock = false; return; }
            if (Cursor.lockState == CursorLockMode.Locked) hadPointerLock = true;
            else if (hadPointerLock) { hadPointerLock = false; SetPaused(true); }
            else if (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true)
                orbit.LockCursor(true);
#endif
        }

        public void Save()
        {
            // Xonadagi natija yolg'iz o'yindagi saqlovning ustiga yozilmaydi.
            if (Multiplayer || lastSafe == null) return;
            lastSafe.map = MapName;
            lastSafe.time = PlayTime;
            lastSafe.finished = Finished;
            SaveSystem.Write(lastSafe);
            saveTimer = 0f;
            dirty = false;
        }

        void CheckFinish()
        {
            if (Finished || finish == null || !motor.IsGrounded) return;
            if (!Physics.Raycast(motor.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.6f) ||
                hit.collider != finish)
                return;
            Finished = true;
            SaveSystem.SubmitFinishTime(PlayTime);
            lastSafe = new SaveData { position = motor.transform.position, yaw = motor.transform.eulerAngles.y };
            Save();
            ReachedFinish?.Invoke();
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            Time.timeScale = paused && !Multiplayer ? 0f : 1f;
            input.enabled = !paused;
            if (orbit != null) orbit.LockCursor(!paused);
            if (paused) Save();
            PauseChanged?.Invoke(paused);
        }

        public void Restart()
        {
            if (Multiplayer)
            {
                // Poyga vaqti davom etadi, o'yinchi faqat boshlanish joyiga qaytadi.
                Place(spawn, spawnYaw);
                SetPaused(false);
                return;
            }
            SaveSystem.Delete();
            lastSafe = null;
            dirty = false;
            PlayTime = 0f;
            Finished = false;
            Place(spawn, spawnYaw);
            SetPaused(false);
        }

        void Place(Vector3 position, float yaw)
        {
            motor.Teleport(position);
            motor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (orbit != null) orbit.Bind(motor.transform, input);
        }

        void OnApplicationQuit() => Save();

        /// <summary>Telefonda ilova fonga o'tsa (qo'ng'iroq, boshqa ilova), o'yin to'xtab saqlanadi.</summary>
        void OnApplicationPause(bool background)
        {
            if (!background || !Application.isMobilePlatform || input == null) return;
            SetPaused(true);
            Save();
        }

        void OnDestroy() => Time.timeScale = 1f;
    }
}
