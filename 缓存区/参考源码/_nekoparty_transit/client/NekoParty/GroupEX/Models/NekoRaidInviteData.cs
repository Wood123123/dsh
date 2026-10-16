using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRaidInviteData
{
	[DataMember(Name = "inviteId")]
	public string InviteId;

	[DataMember(Name = "partyId")]
	public string PartyId;

	[DataMember(Name = "leaderProfileId")]
	public string LeaderProfileId;

	[DataMember(Name = "leaderNickname")]
	public string LeaderNickname;

	[DataMember(Name = "targetProfileId")]
	public string TargetProfileId;

	[DataMember(Name = "targetNickname")]
	public string TargetNickname;

	[DataMember(Name = "serverId")]
	public string ServerId;

	[DataMember(Name = "raidCode")]
	public string RaidCode;

	[DataMember(Name = "location")]
	public string Location;

	[DataMember(Name = "side")]
	public int Side;

	[DataMember(Name = "targetSide")]
	public int TargetSide;

	[DataMember(Name = "time")]
	public int Time;

	[DataMember(Name = "expiresAtUnix")]
	public long ExpiresAtUnix;

	[DataMember(Name = "status")]
	public ENekoInviteStatus Status;
}
