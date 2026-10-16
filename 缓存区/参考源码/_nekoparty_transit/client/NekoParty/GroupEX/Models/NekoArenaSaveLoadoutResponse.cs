using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaSaveLoadoutResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "loadout")]
	public NekoArenaLoadoutEntry Loadout;
}
