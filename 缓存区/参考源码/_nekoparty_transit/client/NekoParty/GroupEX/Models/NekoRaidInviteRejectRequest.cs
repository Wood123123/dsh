using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRaidInviteRejectRequest(string inviteId)
{
	[DataMember(Name = "inviteId")]
	public string InviteId = inviteId;
}
