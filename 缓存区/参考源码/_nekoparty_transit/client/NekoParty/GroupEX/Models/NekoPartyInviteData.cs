using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartyInviteData
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

	[DataMember(Name = "fromProfile")]
	public NekoPartyInvitePlayerData FromProfile;

	[DataMember(Name = "members")]
	public NekoPartyInvitePlayerData[] Members;

	[DataMember(Name = "expiresAtUnix")]
	public long ExpiresAtUnix;

	[DataMember(Name = "status")]
	public ENekoInviteStatus Status;
}
