using UnityEngine;

namespace OnlyUp
{
    /// <summary>Tanlangan personaj modelini Player ichiga joylaydi.</summary>
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] CharacterRoster roster;
        [SerializeField] Transform visualRoot;

        int index;

        GameObject model;

        public CharacterRoster Roster => roster;
        public int Index => index;
        public GameObject Model => model;

        public void Configure(CharacterRoster newRoster, Transform root)
        {
            roster = newRoster;
            visualRoot = root;
        }

        void Awake() => Show(PlayerProfile.CharacterIndex);

        public void Show(int newIndex)
        {
            if (roster == null || roster.Count == 0) return;
            index = Mathf.Clamp(newIndex, 0, roster.Count - 1);
            if (model != null)
            {
                model.SetActive(false);
                Destroy(model);
            }
            model = Instantiate(roster.Get(index).model, visualRoot, false);
        }
    }
}
