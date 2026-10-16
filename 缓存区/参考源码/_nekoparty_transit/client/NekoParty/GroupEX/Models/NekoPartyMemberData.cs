using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartyMemberData
{
	[DataMember(Name = "profileId")]
	public string ProfileId;

	[DataMember(Name = "nickname")]
	public string Nickname;

	[DataMember(Name = "isLeader")]
	public bool IsLeader;

	[DataMember(Name = "preferredSide")]
	public int PreferredSide;
}
