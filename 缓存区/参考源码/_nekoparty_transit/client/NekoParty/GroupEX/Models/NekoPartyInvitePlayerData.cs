using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartyInvitePlayerData
{
	[DataMember(Name = "accountId")]
	public string AccountId;

	[DataMember(Name = "profileId")]
	public string ProfileId;

	[DataMember(Name = "nickname")]
	public string Nickname;

	[DataMember(Name = "isLeader")]
	public bool IsLeader;

	[DataMember(Name = "isReady")]
	public bool IsReady;

	[DataMember(Name = "side")]
	public int Side;

	[DataMember(Name = "level")]
	public int Level;

	[DataMember(Name = "prestigeLevel")]
	public int PrestigeLevel;

	[DataMember(Name = "memberCategory")]
	public int MemberCategory;

	[DataMember(Name = "selectedMemberCategory")]
	public int SelectedMemberCategory;
}
