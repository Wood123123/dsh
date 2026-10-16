using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoTransitConfigResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "enabled")]
	public bool Enabled;

	[DataMember(Name = "punchHost")]
	public string PunchHost;

	[DataMember(Name = "punchPort")]
	public int PunchPort;

	[DataMember(Name = "relayEnabled")]
	public bool RelayEnabled;

	[DataMember(Name = "natProbePort1")]
	public int NatProbePort1;

	[DataMember(Name = "natProbePort2")]
	public int NatProbePort2;
}
