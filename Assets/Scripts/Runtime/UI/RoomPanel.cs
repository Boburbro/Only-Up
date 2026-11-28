using System;
using UnityEngine;
using UnityEngine.UI;

namespace OnlyUp.UI
{
    /// <summary>"Do'stlar bilan o'ynash" oynasi: xona ochish yoki kod bilan qo'shilish.</summary>
    public class RoomPanel
    {
        readonly Button host, join;
        readonly InputField code;
        readonly Text status;

        public GameObject Root { get; }
        public string Status => status.text;
        public InputField CodeInput => code;

        public RoomPanel(Transform canvas, Action onHost, Action<string> onJoin, Action onClose)
        {
            var shade = UiKit.Stretch(canvas, "Rooms", new Color(0f, 0f, 0f, 0.65f));
            Root = shade.gameObject;
            var panel = UiKit.Box(shade, "Window", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(820f, 760f), new Color(0.07f, 0.08f, 0.11f, 0.98f));
            UiKit.Label(panel, "Play with friends", new Vector2(0f, 305f), new Vector2(760f, 80f), 56);

            host = UiKit.Button(panel, "HostRoom", "Create room", new Vector2(0f, 185f),
                new Vector2(620f, 96f), () => onHost(), UiKit.Accent, 40);
            UiKit.Label(panel, "or enter the code your friend sent", new Vector2(0f, 75f),
                new Vector2(760f, 44f), 28, UiKit.Muted);
            code = UiKit.Input(panel, "CodeInput", "Room code", new Vector2(0f, -5f), new Vector2(620f, 90f), 12);
            code.onValueChanged.AddListener(text => code.SetTextWithoutNotify(Normalize(text)));
            join = UiKit.Button(panel, "JoinRoom", "Join", new Vector2(0f, -115f),
                new Vector2(620f, 96f), () => onJoin(Normalize(code.text)), null, 40);
            code.onSubmit.AddListener(text =>
            {
                if (join.interactable) onJoin(Normalize(text));
            });

            status = UiKit.Label(panel, "", new Vector2(0f, -210f), new Vector2(740f, 80f), 28,
                new Color(1f, 0.75f, 0.55f));
            UiKit.Button(panel, "CloseRooms", "Back", new Vector2(0f, -310f), new Vector2(360f, 76f),
                () => onClose());
            Root.SetActive(false);
        }

        /// <summary>Kod katta harf va raqamlardan iborat: bo'sh joy va kichik harflar tuzatiladi.</summary>
        public static string Normalize(string text)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in text ?? "")
                if (char.IsLetterOrDigit(c))
                    result.Append(char.ToUpperInvariant(c));
            return result.ToString();
        }

        public void Show(string message, bool busy)
        {
            Root.SetActive(true);
            status.text = message;
            host.interactable = join.interactable = !busy;
            code.interactable = !busy;
        }
    }
}
