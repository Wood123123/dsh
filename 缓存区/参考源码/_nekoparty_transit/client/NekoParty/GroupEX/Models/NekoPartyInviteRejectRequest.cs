using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartyInviteRejectRequest(string inviteId)
{
	[DataMember(Name = "inviteId")]
	public string InviteId = inviteId;
}
