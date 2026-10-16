using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPendingPartyInvitesResponse
{
	[DataMember(Name = "invites")]
	public NekoPartyInviteData[] Invites;
}
