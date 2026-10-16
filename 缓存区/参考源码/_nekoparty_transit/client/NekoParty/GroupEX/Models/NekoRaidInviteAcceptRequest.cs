using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRaidInviteAcceptRequest(string inviteId, int preferredSide)
{
	[DataMember(Name = "inviteId")]
	public string InviteId = inviteId;

	[DataMember(Name = "preferredSide")]
	public int PreferredSide = preferredSide;
}
