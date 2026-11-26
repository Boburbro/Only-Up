using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OnlyUp.Net
{
    /// <summary>O'yin sahnasini xonaga ulaydi: o'z holatini yuboradi va boshqalarni ko'rsatadi.</summary>
    [DefaultExecutionOrder(100)]
    public class MultiplayerGame : MonoBehaviour
    {
        readonly Dictionary<ulong, RemoteAvatar> avatars = new Dictionary<ulong, RemoteAvatar>();

        NetRoom room;
        GameSession session;
        CharacterRoster roster;
        bool leaving;

        public NetRoom Room => room;
        public IReadOnlyDictionary<ulong, RemoteAvatar> Avatars => avatars;

        public void Configure(NetRoom newRoom, GameSession newSession)
        {
            room = newRoom;
            session = newSession;
            roster = session.Motor.GetComponent<PlayerCharacter>().Roster;
            WorldClock.Shared = () => room.Time;
            gameObject.AddComponent<UI.RoomHud>().Configure(this, session);
        }

        void LateUpdate()
        {
            if (room == null || leaving) return;
            if (room.Closed)
            {
                // Xona egasi chiqib ketdi yoki aloqa uzildi.
                leaving = true;
                NetRoom.LastMessage = room.CloseReason;
                room.Leave();
                SceneManager.LoadScene(GameScenes.MainMenu);
                return;
            }
            room.SetLocalPose(LocalPose());
            SyncAvatars();
        }

        PlayerPose LocalPose()
        {
            var motor = session.Motor;
            var pose = new PlayerPose
            {
                Position = motor.transform.position,
                Yaw = motor.transform.eulerAngles.y,
                Velocity = motor.Velocity,
                State = motor.State,
                Height = session.Height,
                FinishTime = session.Finished ? session.PlayTime : 0f
            };
            pose.HasLedge = motor.TryGetLedgeEdge(out pose.Ledge);
            return pose;
        }

        void SyncAvatars()
        {
            foreach (var id in avatars.Keys.Where(id => !room.Players.ContainsKey(id)).ToArray())
            {
                Destroy(avatars[id].gameObject);
                avatars.Remove(id);
            }
            foreach (var pair in room.Players)
            {
                if (pair.Key == room.LocalId || !room.Poses.TryGetValue(pair.Key, out var pose)) continue;
                if (!avatars.TryGetValue(pair.Key, out var avatar))
                    avatars[pair.Key] = avatar = RemoteAvatar.Create(pair.Key, pair.Value, roster);
                avatar.Apply(pose);
            }
        }

        void OnDestroy()
        {
            WorldClock.Shared = null;
            foreach (var avatar in avatars.Values)
                if (avatar != null) Destroy(avatar.gameObject);
        }
    }
}
