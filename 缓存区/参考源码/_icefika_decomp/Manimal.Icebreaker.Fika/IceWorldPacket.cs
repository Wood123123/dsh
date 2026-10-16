using Fika.Core.Networking.LiteNetLib.Utils;

namespace Manimal.Icebreaker.Fika;

public struct IceWorldPacket : INetSerializable
{
	public byte Kind;

	public string DoorId;

	public bool Sealing;

	public int IntA;

	public float Value;

	public void Serialize(NetDataWriter writer)
	{
		writer.Put(Kind);
		writer.Put(DoorId ?? "", 0);
		writer.Put(Sealing);
		writer.Put(IntA);
		writer.Put(Value);
	}

	public void Deserialize(NetDataReader reader)
	{
		Kind = reader.GetByte();
		DoorId = reader.GetString();
		Sealing = reader.GetBool();
		IntA = reader.GetInt();
		Value = reader.GetFloat();
	}
}
