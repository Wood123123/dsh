using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoBossScavStatusResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "enabled")]
	public bool Enabled;

	[DataMember(Name = "canRefresh")]
	public bool CanRefresh;

	[DataMember(Name = "remainingRefreshes")]
	public int RemainingRefreshes;

	[DataMember(Name = "maxRefreshes")]
	public int MaxRefreshes;

	[DataMember(Name = "currentRole")]
	public string CurrentRole;
}
