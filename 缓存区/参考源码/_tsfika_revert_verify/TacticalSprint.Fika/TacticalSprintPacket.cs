using Fika.Core.Networking.LiteNetLib.Utils;

namespace TacticalSprint.Fika;

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
