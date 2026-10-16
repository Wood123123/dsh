using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartyInviteMemberRequest(string targetProfileId)
{
	[DataMember(Name = "targetProfileId")]
	public string TargetProfileId = targetProfileId;
}
