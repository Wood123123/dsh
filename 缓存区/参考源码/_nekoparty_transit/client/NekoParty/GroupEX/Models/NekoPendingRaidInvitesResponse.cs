using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPendingRaidInvitesResponse
{
	[DataMember(Name = "invites")]
	public NekoRaidInviteData[] Invites;
}
