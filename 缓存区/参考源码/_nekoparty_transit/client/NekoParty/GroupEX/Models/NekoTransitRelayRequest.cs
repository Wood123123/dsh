using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoTransitRelayRequest
{
	[DataMember(Name = "serverGuid")]
	public string ServerGuid;
}
