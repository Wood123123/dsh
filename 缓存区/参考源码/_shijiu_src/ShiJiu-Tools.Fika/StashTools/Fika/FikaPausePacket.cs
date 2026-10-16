using System.IO;
using Fika.Core.Networking.LiteNetLib.Utils;

namespace StashTools.Fika;

public sealed class FikaPausePacket : INetSerializable
{
	internal enum MessageKind : byte
	{
		Hello,
		Capability,
		Press,
		State,
		Applied,
		Failure
	}

	internal MessageKind Kind;

	internal int Protocol = 10402;

	internal string Room = "";

	internal string Map = "";

	internal string Host = "";

	internal string Json = "";

	internal long Sequence;

	internal double Sent;

	internal double HostTime;

	internal bool Flag;

	public void Serialize(NetDataWriter writer)
	{
		writer.Put(Protocol);
		writer.Put((byte)Kind);
		writer.Put(Room, 0);
		writer.Put(Map, 0);
		writer.Put(Host, 0);
		writer.Put(Sequence);
		writer.Put(Sent);
		writer.Put(HostTime);
		writer.Put(Flag);
		writer.Put(Json, 0);
	}

	public void Deserialize(NetDataReader reader)
	{
		Protocol = reader.GetInt();
		Kind = (MessageKind)reader.GetByte();
		Room = reader.GetString(80);
		Map = reader.GetString(80);
		Host = reader.GetString(80);
		Sequence = reader.GetLong();
		Sent = reader.GetDouble();
		HostTime = reader.GetDouble();
		Flag = reader.GetBool();
		Json = reader.GetString(16384);
		if ((int)Kind > 5 || !double.IsFinite(Sent) || Sent < 0.0 || !double.IsFinite(HostTime) || HostTime < 0.0)
		{
			throw new InvalidDataException("暂停网络包无效");
		}
	}
}
