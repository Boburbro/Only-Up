using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OnlyUp.EditorTools
{
    /// <summary>
    /// O'yin tovushlarini kod bilan sintez qilib WAV fayl yozadi: hech narsa yuklab olinmaydi.
    /// Keyin fayllarni shu nomdagi haqiqiy yozuvlar bilan almashtirish mumkin.
    /// </summary>
    public static class SoundBuilder
    {
        const string Folder = "Assets/Audio";
        const string BankPath = Folder + "/SoundBank.asset";
        const int Rate = 44100;
        const float Tau = Mathf.PI * 2f;

        static System.Random random;

        public static SoundBank LoadOrBuild()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            return bank != null && bank.IsComplete ? bank : Build();
        }

        [MenuItem("Only Up/Tovushlarni qayta yaratish")]
        public static SoundBank Build()
        {
            Directory.CreateDirectory(Folder);
            random = new System.Random(5);

            var steps = new AudioClip[4];
            for (int i = 0; i < steps.Length; i++) steps[i] = Save($"step{i + 1}", Step(), 0.5f);
            var jump = Save("jump", Jump(), 0.45f);
            var land = Save("land", Land(), 0.75f);
            var bounce = Save("bounce", Bounce(), 0.6f);
            var grab = Save("grab", Grab(), 0.55f);
            var climb = Save("climb", Climb(), 0.4f);
            var splash = Save("splash", Splash(), 0.7f);
            var click = Save("click", Click(), 0.35f);
            var victory = Save("victory", Victory(), 0.7f);
            var wind = Save("wind", Loop(8f, 1f, Wind), 0.5f);
            var sea = Save("sea", Loop(12f, 1.5f, Sea), 0.5f);
            var slide = Save("slide", Loop(2f, 0.3f, SlideNoise), 0.45f);
            var music = Save("music", Music(), 0.55f, true);

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            bank.steps = steps;
            bank.jump = jump;
            bank.land = land;
            bank.bounce = bounce;
            bank.grab = grab;
            bank.climb = climb;
            bank.splash = splash;
            bank.click = click;
            bank.victory = victory;
            bank.wind = wind;
            bank.sea = sea;
            bank.slide = slide;
            bank.music = music;
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log("OnlyUp: tovushlar yaratildi");
            return bank;
        }

        // ---------- Yozish ----------

        static AudioClip Save(string name, float[] samples, float peak, bool isLong = false)
        {
            float max = 1e-6f;
            foreach (var s in samples) max = Mathf.Max(max, Mathf.Abs(s));

            string path = $"{Folder}/{name}.wav";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2;
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + bytes);
                writer.Write("WAVEfmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(Rate);
                writer.Write(Rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write("data".ToCharArray());
                writer.Write(bytes);
                foreach (var s in samples) writer.Write((short)(s / max * peak * short.MaxValue));
            }
            AssetDatabase.ImportAsset(path);

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = isLong ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---------- Asosiy bo'laklar ----------

        static float Noise() => (float)random.NextDouble() * 2f - 1f;

        static float[] Render(float seconds, Func<float, float> sample)
        {
            var result = new float[(int)(seconds * Rate)];
            for (int i = 0; i < result.Length; i++) result[i] = sample(i / (float)Rate);
            // Boshi va oxirida "chirs" etmasligi uchun.
            int fade = Mathf.Min(220, result.Length / 4);
            for (int i = 0; i < fade; i++)
            {
                result[i] *= i / (float)fade;
                result[result.Length - 1 - i] *= i / (float)fade;
            }
            return result;
        }

        /// <summary>Oddiy past chastota filtri. cutoff Hz da.</summary>
        static Func<float, float, float> LowPass()
        {
            float state = 0f;
            return (input, cutoff) =>
            {
                state += (input - state) * (1f - Mathf.Exp(-Tau * cutoff / Rate));
                return state;
            };
        }

        /// <summary>Chastotasi o'zgaradigan sinus.</summary>
        static Func<float, float> Sine()
        {
            float phase = 0f;
            return frequency =>
            {
                phase += Tau * frequency / Rate;
                return Mathf.Sin(phase);
            };
        }

        static float Decay(float t, float speed) => Mathf.Exp(-t * speed);

        /// <summary>Takrorlanadigan tovush: oxiri boshiga silliq ulanadi.</summary>
        static float[] Loop(float seconds, float overlap, Func<Func<float, float>> create)
        {
            var sample = create();
            int length = (int)(seconds * Rate), extra = (int)(overlap * Rate);
            var raw = new float[length + extra];
            for (int i = 0; i < raw.Length; i++) raw[i] = sample(i / (float)Rate);

            var result = new float[length];
            Array.Copy(raw, result, length);
            for (int i = 0; i < extra; i++)
            {
                float k = i / (float)extra;
                result[i] = raw[i] * Mathf.Sqrt(k) + raw[length + i] * Mathf.Sqrt(1f - k);
            }
            return result;
        }

        // ---------- Tovushlar ----------

        static float[] Step()
        {
            var body = LowPass();
            var grit = LowPass();
            var thump = Sine();
            float pitch = 80f + (float)random.NextDouble() * 35f;
            float tone = 700f + (float)random.NextDouble() * 600f;
            return Render(0.16f, t =>
                body(Noise(), tone) * Decay(t, 38f) * 1.4f + thump(pitch) * Decay(t, 45f) * 0.7f +
                (Noise() - grit(Noise(), 2500f)) * Decay(t, 90f) * 0.08f);
        }

        static float[] Jump()
        {
            var air = LowPass();
            var voice = Sine();
            return Render(0.25f, t =>
                air(Noise(), 500f + t * 5000f) * Mathf.Sin(Mathf.PI * t / 0.25f) * 0.9f +
                voice(110f + t * 320f) * Decay(t, 20f) * 0.35f);
        }

        static float[] Land()
        {
            var body = LowPass();
            var thump = Sine();
            return Render(0.35f, t =>
                body(Noise(), 450f) * Decay(t, 18f) * 1.6f + thump(95f - t * 150f) * Decay(t, 13f));
        }

        static float[] Bounce()
        {
            var tone = Sine();
            var overtone = Sine();
            return Render(0.6f, t =>
            {
                float f = 130f + 420f * (1f - Decay(t, 7f)) + Mathf.Sin(t * 60f) * 35f * Decay(t, 5f);
                return (tone(f) + overtone(f * 2.01f) * 0.3f) * Decay(t, 5f);
            });
        }

        static float[] Grab()
        {
            var body = LowPass();
            var thump = Sine();
            return Render(0.18f, t =>
            {
                // Ikki qo'l bir oz ketma-ket tegadi.
                float second = t > 0.045f ? Decay(t - 0.045f, 55f) : 0f;
                return body(Noise(), 1800f) * (Decay(t, 55f) + second) + thump(150f) * Decay(t, 40f) * 0.5f;
            });
        }

        static float[] Climb()
        {
            var cloth = LowPass();
            var rumble = LowPass();
            return Render(0.5f, t =>
            {
                float n = Noise();
                float swish = cloth(n, 1600f) - rumble(n, 300f);
                return swish * Mathf.Sin(Mathf.PI * t / 0.5f) * (0.6f + 0.4f * Mathf.Sin(t * 38f));
            });
        }

        static float[] Splash()
        {
            var low = LowPass();
            var high = LowPass();
            return Render(1.2f, t =>
                low(Noise(), 2800f * Decay(t, 2.2f) + 250f) * Decay(t, 3.5f) +
                (Noise() - high(Noise(), 3000f)) * Decay(t, 9f) * 0.15f);
        }

        static float[] Click()
        {
            var tone = Sine();
            return Render(0.07f, t => tone(880f) * Decay(t, 60f));
        }

        static float[] Victory()
        {
            // Do-major bo'ylab ko'tariluvchi qisqa kuy va yakuniy akkord.
            var notes = new (float start, float frequency)[]
            {
                (0f, 523.25f), (0.14f, 659.25f), (0.28f, 783.99f), (0.42f, 1046.5f),
                (0.72f, 523.25f), (0.72f, 659.25f), (0.72f, 783.99f), (0.72f, 1046.5f)
            };
            return Render(2.8f, t =>
            {
                float sum = 0f;
                foreach (var note in notes)
                {
                    float local = t - note.start;
                    if (local < 0f) continue;
                    float envelope = Mathf.Min(1f, local * 80f) * Decay(local, note.start > 0.6f ? 1.6f : 5f);
                    sum += (Mathf.Sin(Tau * note.frequency * local) +
                            Mathf.Sin(Tau * note.frequency * 2f * local) * 0.25f) * envelope;
                }
                return sum;
            });
        }

        static Func<float, float> Wind()
        {
            var a = LowPass();
            var b = LowPass();
            return t =>
            {
                // Kuch va balandlik sekin o'zgaradi, davrlar 8 soniyaga karrali.
                float gust = 0.65f + 0.25f * Mathf.Sin(Tau * t / 8f) + 0.1f * Mathf.Sin(Tau * t * 3f / 8f);
                float cutoff = 380f + 260f * gust;
                return b(a(Noise(), cutoff), cutoff) * gust;
            };
        }

        static Func<float, float> Sea()
        {
            var low = LowPass();
            var foam = LowPass();
            return t =>
            {
                float swell = Mathf.Pow(0.5f - 0.5f * Mathf.Cos(Tau * t / 6f), 2f);
                float n = Noise();
                return low(n, 280f + 500f * swell) * (0.35f + swell) + (n - foam(n, 3500f)) * swell * swell * 0.05f;
            };
        }

        static Func<float, float> SlideNoise()
        {
            var high = LowPass();
            var low = LowPass();
            return t =>
            {
                float n = Noise();
                return high(n, 2200f) - low(n, 350f);
            };
        }

        /// <summary>Sokin fon musiqasi: sekin almashadigan akkordlar va siyrak qo'ng'iroqcha notalar.</summary>
        static float[] Music()
        {
            const float bar = 8f;
            // La minor, Fa major, Do major, Sol major.
            var chords = new[]
            {
                new[] { 110f, 220f, 261.63f, 329.63f },
                new[] { 87.31f, 220f, 261.63f, 349.23f },
                new[] { 130.81f, 196f, 261.63f, 329.63f },
                new[] { 98f, 196f, 246.94f, 293.66f }
            };
            float[] scale = { 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f };
            int length = (int)(bar * chords.Length * Rate);
            var result = new float[length];

            // Har bir nota halqa bo'ylab yoziladi, shuning uchun oxiri boshiga uzilmay ulanadi.
            void Add(float start, float duration, float frequency, float volume, float attack, float decay)
            {
                int first = (int)(start * Rate), count = (int)(duration * Rate);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)Rate;
                    float envelope = Mathf.Min(1f, t / attack) * Mathf.Min(1f, (duration - t) / attack) * Decay(t, decay);
                    float wave = Mathf.Sin(Tau * frequency * t) + 0.3f * Mathf.Sin(Tau * frequency * 1.004f * t) +
                                 0.12f * Mathf.Sin(Tau * frequency * 2f * t);
                    result[(first + i) % length] += wave * envelope * volume;
                }
            }

            for (int c = 0; c < chords.Length; c++)
            {
                foreach (var frequency in chords[c])
                    Add(c * bar, bar + 2f, frequency, 0.16f, 2.2f, 0.05f);
                for (int beat = 0; beat < 8; beat++)
                    if (random.NextDouble() < 0.45)
                        Add(c * bar + beat, 2.5f, scale[random.Next(scale.Length)], 0.08f, 0.02f, 1.8f);
            }
            return result;
        }
    }
}
