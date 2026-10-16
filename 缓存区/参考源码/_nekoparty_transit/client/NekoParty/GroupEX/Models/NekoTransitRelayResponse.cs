using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoTransitRelayResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "host")]
	public string Host;

	[DataMember(Name = "port")]
	public int Port;

	[DataMember(Name = "sessionId")]
	public string SessionId;
}
