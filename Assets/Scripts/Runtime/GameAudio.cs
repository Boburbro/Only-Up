using UnityEngine;

namespace OnlyUp
{
    /// <summary>
    /// Sahnadagi barcha tovushlar: musiqa, shamol, dengiz va (o'yinchi berilgan bo'lsa)
    /// qadam, sakrash, qo'nish kabi harakat tovushlari.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const float WalkStride = 1.5f;
        const float SprintStride = 2.1f;

        [SerializeField] SoundBank bank;
        [SerializeField] PlayerMotor motor;

        AudioSource effects, music, wind, sea, slide;
        FallReset fall;
        GameSession session;
        MotorState previous = MotorState.Airborne;
        float walked;
        float fallSpeed;
        float airTime;
        int stepIndex;
        float startHeight;

        public static GameAudio Current { get; private set; }

        public SoundBank Bank => bank;
        /// <summary>Oxirgi chalingan effekt.</summary>
        public AudioClip LastPlayed { get; private set; }
        public int StepCount { get; private set; }
        public float MusicLevel => music != null ? music.volume : 0f;

        public void Configure(SoundBank newBank, PlayerMotor newMotor)
        {
            bank = newBank;
            motor = newMotor;
        }

        void Awake()
        {
            Current = this;
            effects = Source(null, 1f, false);
            music = Source(bank.music, GameSettings.MusicVolume, true);
            wind = Source(bank.wind, 0.15f, true);
            sea = Source(bank.sea, 0.35f, true);
            slide = Source(bank.slide, 0f, true);
        }

        AudioSource Source(AudioClip clip, float volume, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            if (loop) source.Play();
            return source;
        }

        void Start()
        {
            session = FindAnyObjectByType<GameSession>();
            if (session != null) session.ReachedFinish += OnFinish;
            if (motor == null) return;
            startHeight = motor.transform.position.y;
            motor.Jumped += OnJump;
            motor.Bounced += OnBounce;
            fall = motor.GetComponent<FallReset>();
            if (fall != null) fall.Fell += OnFell;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (session != null) session.ReachedFinish -= OnFinish;
            if (motor != null)
            {
                motor.Jumped -= OnJump;
                motor.Bounced -= OnBounce;
            }
            if (fall != null) fall.Fell -= OnFell;
        }

        void OnJump() => Play(bank.jump, 0.7f);
        void OnBounce() => Play(bank.bounce, 0.9f);
        void OnFinish() => Play(bank.victory, 1f);

        void OnFell()
        {
            Play(bank.splash, 1f);
            airTime = fallSpeed = 0f;
        }

        public void PlayClick() => Play(bank.click, 0.8f);

        void Play(AudioClip clip, float volume)
        {
            effects.PlayOneShot(clip, volume);
            LastPlayed = clip;
        }

        void Update()
        {
            music.volume = GameSettings.MusicVolume;
            if (motor == null) return;

            float dt = Time.deltaTime;
            bool paused = dt <= 0f;
            if (paused)
            {
                slide.volume = 0f;
                return;
            }

            MotorState state = motor.State;
            Vector3 velocity = motor.Velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;

            if (state == MotorState.Airborne)
            {
                airTime += dt;
                fallSpeed = Mathf.Max(fallSpeed, -velocity.y);
            }
            else
            {
                // Zinadan tushishdagi mayda uzilishlarda "gup" etmaydi.
                if (state == MotorState.Grounded && previous == MotorState.Airborne && airTime > 0.2f && fallSpeed > 3f)
                    Play(bank.land, Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(3f, 18f, fallSpeed)));
                airTime = fallSpeed = 0f;
            }

            if (state != previous)
            {
                if (state == MotorState.Hanging) Play(bank.grab, 0.8f);
                if (state == MotorState.Climbing) Play(bank.climb, 0.8f);
                previous = state;
            }

            if (state == MotorState.Grounded && speed > 0.8f)
            {
                walked += speed * dt;
                float stride = speed > 5f ? SprintStride : WalkStride;
                if (walked >= stride)
                {
                    walked -= stride;
                    stepIndex = (stepIndex + 1 + Random.Range(0, bank.steps.Length - 1)) % bank.steps.Length;
                    Play(bank.steps[stepIndex], speed > 5f ? 0.75f : 0.55f);
                    StepCount++;
                }
            }
            else walked = WalkStride * 0.6f;

            slide.volume = Mathf.MoveTowards(slide.volume, state == MotorState.Sliding ? 0.6f : 0f, dt * 4f);

            // Balandlikda shamol kuchayadi, dengiz esa tinadi. Tez yiqilganda shamol guvillaydi.
            float height = Mathf.Max(0f, motor.transform.position.y - startHeight);
            float rush = Mathf.InverseLerp(10f, 30f, -velocity.y);
            wind.volume = Mathf.MoveTowards(wind.volume,
                Mathf.Lerp(0.12f, 0.55f, height / 150f) + rush * 0.45f, dt * 1.5f);
            sea.volume = Mathf.Lerp(0.35f, 0.04f, height / 60f);
        }
    }
}
