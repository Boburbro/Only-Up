using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using OnlyUp.Net;

namespace OnlyUp.UI
{
    /// <summary>Xona kodi, balandlik bo'yicha o'yinchilar ro'yxati va boshlar ustidagi niklar.</summary>
    public class RoomHud : MonoBehaviour
    {
        const float TagDistance = 60f;

        readonly Dictionary<ulong, Text> tags = new Dictionary<ulong, Text>();

        MultiplayerGame game;
        GameSession session;
        Transform tagRoot;
        Text codeLabel, list;
        float refresh;

        public string CodeText => codeLabel.text;
        public string ListText => list.text;
        public IReadOnlyDictionary<ulong, Text> Tags => tags;

        public void Configure(MultiplayerGame newGame, GameSession newSession)
        {
            game = newGame;
            session = newSession;
            // Asosiy HUD va menyular bu yozuvlarning ustida turadi.
            var canvas = UiKit.CreateCanvas("RoomCanvas", -1).transform;
            canvas.SetParent(transform, false);
            tagRoot = UiKit.Rect(canvas, "NameTags", Vector2.zero, Vector2.zero, Vector2.zero);
            var area = (RectTransform)tagRoot;
            area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;

            var safe = new GameObject("SafeRoom", typeof(RectTransform), typeof(SafeArea))
                .GetComponent<RectTransform>();
            safe.SetParent(canvas, false);
            codeLabel = Corner(safe, new Vector2(-70f, -125f), 38, UiKit.Accent);
            list = Corner(safe, new Vector2(-70f, -180f), 30, Color.white);
        }

        static Text Corner(Transform parent, Vector2 position, int size, Color color)
        {
            var corner = new Vector2(1f, 1f);
            var label = UiKit.Label(parent, "", position, new Vector2(620f, 50f), size, color,
                TextAnchor.UpperRight, corner, corner);
            label.fontStyle = FontStyle.Bold;
            Outline(label);
            return label;
        }

        static void Outline(Text label)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        void LateUpdate()
        {
            if (game == null || game.Room == null || game.Room.Closed) return;
            UpdateTags();
            if ((refresh -= Time.unscaledDeltaTime) > 0f) return;
            refresh = 0.25f;
            var room = game.Room;
            codeLabel.text = $"Xona: {room.Code}  ({room.Players.Count}/{NetRoom.MaxPlayers})";
            list.text = Ranking();
        }

        /// <summary>Avval marraga yetganlar (vaqt bo'yicha), keyin qolganlar balandlik bo'yicha.</summary>
        string Ranking()
        {
            var room = game.Room;
            var rows = room.Players.Select(pair =>
            {
                bool mine = pair.Key == room.LocalId;
                room.Poses.TryGetValue(pair.Key, out var pose);
                float height = mine ? session.Height : pose.Height;
                float finish = mine ? (session.Finished ? session.PlayTime : 0f) : pose.FinishTime;
                return (name: pair.Value.Name + (mine ? " (siz)" : ""), height, finish);
            });
            return string.Join("\n", rows
                .OrderBy(r => r.finish > 0f ? r.finish : float.MaxValue)
                .ThenByDescending(r => r.height)
                .Select((r, i) => $"{i + 1}. {r.name}  " + (r.finish > 0f
                    ? $"marra {SaveSystem.FormatTime(r.finish)}"
                    : SaveSystem.FormatHeight(r.height))));
        }

        void UpdateTags()
        {
            foreach (var id in tags.Keys.Where(id => !game.Avatars.ContainsKey(id)).ToArray())
            {
                Destroy(tags[id].gameObject);
                tags.Remove(id);
            }
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var pair in game.Avatars)
            {
                if (!tags.TryGetValue(pair.Key, out var label))
                {
                    label = UiKit.Label(tagRoot, pair.Value.PlayerName, Vector2.zero, new Vector2(420f, 44f), 28);
                    label.fontStyle = FontStyle.Bold;
                    Outline(label);
                    tags[pair.Key] = label;
                }
                Vector3 head = pair.Value.transform.position + Vector3.up * 2.15f;
                Vector3 view = camera.WorldToViewportPoint(head);
                bool visible = view.z > 0f && view.z < TagDistance;
                label.enabled = visible;
                if (!visible) continue;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(view.x, view.y);
            }
        }
    }
}
