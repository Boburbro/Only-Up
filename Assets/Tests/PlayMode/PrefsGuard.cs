using System.Collections.Generic;
using UnityEngine;

namespace OnlyUp.Tests
{
    /// <summary>
    /// Testlar o'yinchining saqlangan nik va sozlamalariga bog'liq bo'lmaydi va ularni buzmaydi:
    /// Take() qiymatlarni vaqtincha olib qo'yadi, Restore() qaytaradi.
    /// </summary>
    public class PrefsGuard
    {
        static readonly string[] Texts = { PlayerProfile.NameKey, SaveSystem.SaveKey };
        static readonly string[] Numbers =
        {
            PlayerProfile.CharacterKey, GameSettings.InvertKey,
            GameSettings.RenderScaleKey, GameSettings.ShadowsKey, GameSettings.EffectsKey,
            GameSettings.FpsLimitKey, GameSettings.ShowFpsKey
        };
        static readonly string[] Fractions =
        {
            GameSettings.SensitivityKey, GameSettings.VolumeKey, GameSettings.MusicKey, SaveSystem.BestHeightKey, SaveSystem.BestTimeKey
        };

        readonly List<System.Action> restore = new List<System.Action>();

        public static PrefsGuard Take()
        {
            var guard = new PrefsGuard();
            foreach (var key in Texts) guard.Hold(key, PlayerPrefs.GetString(key), v => PlayerPrefs.SetString(key, v));
            foreach (var key in Numbers) guard.Hold(key, PlayerPrefs.GetInt(key), v => PlayerPrefs.SetInt(key, v));
            foreach (var key in Fractions) guard.Hold(key, PlayerPrefs.GetFloat(key), v => PlayerPrefs.SetFloat(key, v));
            return guard;
        }

        void Hold<T>(string key, T value, System.Action<T> write)
        {
            bool existed = PlayerPrefs.HasKey(key);
            restore.Add(() =>
            {
                if (existed) write(value);
                else PlayerPrefs.DeleteKey(key);
            });
            PlayerPrefs.DeleteKey(key);
        }

        public void Restore()
        {
            foreach (var action in restore) action();
            PlayerPrefs.Save();
        }
    }
}
