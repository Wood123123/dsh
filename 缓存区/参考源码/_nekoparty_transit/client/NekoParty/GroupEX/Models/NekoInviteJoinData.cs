using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoInviteJoinData
{
	[DataMember(Name = "serverId")]
	public string ServerId;

	[DataMember(Name = "serverGuid")]
	public string ServerGuid;

	[DataMember(Name = "timestamp")]
	public long Timestamp;

	[DataMember(Name = "gameVersion")]
	public string GameVersion;

	[DataMember(Name = "crc32")]
	public uint Crc32;

	[DataMember(Name = "raidCode")]
	public string RaidCode;
}
