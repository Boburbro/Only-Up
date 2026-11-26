using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OnlyUp.Net
{
    /// <summary>Xonadagi o'yinchi haqida kam o'zgaradigan ma'lumot.</summary>
    public struct PlayerInfo
    {
        public string Name;
        public int Character;
    }

    /// <summary>O'yinchining shu lahzadagi holati. Sekundiga ko'p marta yuboriladi.</summary>
    public struct PlayerPose
    {
        public Vector3 Position;
        public float Yaw;
        public Vector3 Velocity;
        public MotorState State;
        public bool HasLedge;
        public Vector3 Ledge;
        public float Height;
        /// <summary>Marraga yetgan vaqti; yetmagan bo'lsa 0.</summary>
        public float FinishTime;

        public void Write(ref FastBufferWriter writer)
        {
            writer.WriteValueSafe(Position);
            writer.WriteValueSafe(Yaw);
            writer.WriteValueSafe(Velocity);
            writer.WriteValueSafe((byte)State);
            writer.WriteValueSafe(HasLedge);
            if (HasLedge) writer.WriteValueSafe(Ledge);
            writer.WriteValueSafe(Height);
            writer.WriteValueSafe(FinishTime);
        }

        public static PlayerPose Read(ref FastBufferReader reader)
        {
            var pose = new PlayerPose();
            reader.ReadValueSafe(out pose.Position);
            reader.ReadValueSafe(out pose.Yaw);
            reader.ReadValueSafe(out pose.Velocity);
            reader.ReadValueSafe(out byte state);
            pose.State = (MotorState)Mathf.Min(state, (int)MotorState.Climbing);
            reader.ReadValueSafe(out pose.HasLedge);
            if (pose.HasLedge) reader.ReadValueSafe(out pose.Ledge);
            reader.ReadValueSafe(out pose.Height);
            reader.ReadValueSafe(out pose.FinishTime);
            return pose;
        }

        /// <summary>Tarmoqdan kelgan buzuq sonlar sahnani buzmasligi kerak.</summary>
        public bool IsValid => Finite(Position) && Finite(Velocity) && Finite(Ledge) && float.IsFinite(Yaw) &&
                               float.IsFinite(Height) && float.IsFinite(FinishTime) &&
                               Position.sqrMagnitude < 1e8f;

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    static class NetText
    {
        public static void WriteName(ref FastBufferWriter writer, string name)
        {
            var text = new FixedString128Bytes();
            text.CopyFromTruncated(name ?? "");
            writer.WriteValueSafe(text);
        }

        /// <summary>Boshqa o'yinchi yuborgan nik shu o'yin qoidalariga mos bo'lmasa, almashtiriladi.</summary>
        public static string ReadName(ref FastBufferReader reader)
        {
            reader.ReadValueSafe(out FixedString128Bytes text);
            string name = PlayerProfile.Clean(text.ToString());
            return PlayerProfile.NameError(name) == null ? name : "Mehmon";
        }
    }
}
