using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRaidInviteDecisionResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "serverId")]
	public string ServerId;

	[DataMember(Name = "joinData")]
	public NekoInviteJoinData? JoinData;
}
