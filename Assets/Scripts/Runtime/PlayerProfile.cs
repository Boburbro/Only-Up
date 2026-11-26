using System.Linq;
using UnityEngine;

namespace OnlyUp
{
    /// <summary>Nik va tanlangan personaj. Shu qurilmada saqlanadi; xonada boshqa o'yinchilarga ham ko'rinadi.</summary>
    public static class PlayerProfile
    {
        public const int MinNameLength = 3;
        public const int MaxNameLength = 16;
        public const string NameKey = "profile.name";
        public const string CharacterKey = "profile.character";

        public static string Nickname
        {
            get => PlayerPrefs.GetString(NameKey, "");
            set => PlayerPrefs.SetString(NameKey, Clean(value));
        }

        public static int CharacterIndex
        {
            get => PlayerPrefs.GetInt(CharacterKey, 0);
            set => PlayerPrefs.SetInt(CharacterKey, Mathf.Max(0, value));
        }

        public static string Clean(string name) => (name ?? "").Trim();

        /// <summary>Nik yaroqli bo'lsa null, aks holda foydalanuvchiga ko'rsatiladigan sabab.</summary>
        public static string NameError(string name)
        {
            name = Clean(name);
            if (name.Length == 0) return "Nik kiriting";
            if (name.Length < MinNameLength) return $"Nik kamida {MinNameLength} ta belgidan iborat bo'lsin";
            if (name.Length > MaxNameLength) return $"Nik {MaxNameLength} ta belgidan oshmasin";
            // '<' va '>' matn formatlash teglariga aylanib ketmasligi uchun harflar cheklanadi.
            if (!name.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '\''))
                return "Faqat harf, raqam, bo'sh joy, _ - ' ishlating";
            return null;
        }
    }
}
