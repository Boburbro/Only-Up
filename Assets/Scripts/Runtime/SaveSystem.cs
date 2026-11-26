using System;
using UnityEngine;

namespace OnlyUp
{
    [Serializable]
    public class SaveData
    {
        /// <summary>Qaysi xaritada saqlangani: boshqa xaritaga yuklab bo'lmaydi.</summary>
        public string map;
        public Vector3 position;
        public float yaw;
        public float time;
        public bool finished;
    }

    /// <summary>
    /// Bitta saqlangan o'yin va rekordlar (PlayerPrefs ichida). Xarita qayta qurilsa, kalitlardagi raqam
    /// oshiriladi: eski joy havoda qolib ketadi, eski rekord esa yangisi bilan taqqoslanmaydi.
    /// </summary>
    public static class SaveSystem
    {
        public const string SaveKey = "save.data.2";
        public const string BestHeightKey = "record.world2.height";
        public const string BestTimeKey = "record.world2.time";

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);

        public static bool HasSaveFor(string map) => HasSave && Read()?.map == map;

        public static void Write(SaveData data)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static SaveData Read() =>
            HasSave ? JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) : null;

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public static float BestHeight => PlayerPrefs.GetFloat(BestHeightKey, 0f);

        /// <summary>Marraga yetilgan eng qisqa vaqt; hali yetilmagan bo'lsa 0.</summary>
        public static float BestTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);

        public static void SubmitHeight(float height)
        {
            if (height > BestHeight) PlayerPrefs.SetFloat(BestHeightKey, height);
        }

        public static void SubmitFinishTime(float time)
        {
            if (BestTime <= 0f || time < BestTime) PlayerPrefs.SetFloat(BestTimeKey, time);
        }

        public static string FormatTime(float seconds)
        {
            var span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }

        public static string FormatHeight(float meters) =>
            meters.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m";
    }
}
