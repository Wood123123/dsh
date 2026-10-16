using Fika.Core.Networking.LiteNetLib.Utils;

namespace TacticalSprint.Fika
{
    /// <summary>
    /// 战术冲刺动画同步包：携带"玩家 X 是否处于战术冲刺"。
    /// </summary>
    public struct TacticalSprintPacket : INetSerializable
    {
        public int PlayerId;
        public bool Active;

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
            Active = reader.GetBool();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerId);
            writer.Put(Active);
        }
    }
}
