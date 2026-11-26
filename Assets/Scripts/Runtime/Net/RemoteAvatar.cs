using UnityEngine;

namespace OnlyUp.Net
{
    /// <summary>
    /// Boshqa o'yinchining ko'rinishi. Fizik chegarasi yo'q; tarmoqdan kelgan holatga silliq yetib boradi.
    /// </summary>
    public class RemoteAvatar : MonoBehaviour, ICharacterState
    {
        const float SnapDistance = 4f;

        PlayerPose target;
        float age;
        bool placed;

        public ulong Id { get; private set; }
        public string PlayerName { get; private set; }
        public PlayerPose Pose => target;

        public MotorState State => target.State;
        public Vector3 Velocity => target.Velocity;
        public Transform Body => transform;

        public bool TryGetLedgeEdge(out Vector3 edge)
        {
            edge = target.Ledge;
            return target.HasLedge;
        }

        public static RemoteAvatar Create(ulong id, PlayerInfo info, CharacterRoster roster)
        {
            var go = new GameObject($"Remote_{id}");
            var avatar = go.AddComponent<RemoteAvatar>();
            avatar.Id = id;
            avatar.PlayerName = info.Name;
            // Model ichidagi CharacterAnimator holatni shu komponentdan oladi.
            Instantiate(roster.Get(info.Character).model, go.transform, false);
            foreach (var part in go.GetComponentsInChildren<Transform>())
                part.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            return avatar;
        }

        public void Apply(PlayerPose pose)
        {
            if (!pose.Position.Equals(target.Position)) age = 0f;
            target = pose;
            if (placed) return;
            placed = true;
            transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0f, pose.Yaw, 0f));
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            // Keyingi paket kelguncha tezlik bo'yicha biroz oldinga taxmin qilinadi.
            Vector3 goal = target.Position + target.Velocity * Mathf.Min(age, 0.1f);
            if ((goal - transform.position).sqrMagnitude > SnapDistance * SnapDistance) transform.position = goal;
            float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, goal, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, target.Yaw, 0f), blend);
        }
    }
}
