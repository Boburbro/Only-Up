using System;
using UnityEngine;

namespace OnlyUp
{
    /// <summary>Tanlash mumkin bo'lgan personajlar ro'yxati. Tezlik va sakrash hammasida bir xil.</summary>
    public class CharacterRoster : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string displayName;
            public GameObject model;
        }

        public Entry[] characters = new Entry[0];

        public int Count => characters.Length;
        public Entry Get(int index) => characters[Mathf.Clamp(index, 0, characters.Length - 1)];
    }
}
