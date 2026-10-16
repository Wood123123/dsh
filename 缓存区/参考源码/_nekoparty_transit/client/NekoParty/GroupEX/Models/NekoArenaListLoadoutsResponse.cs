using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaListLoadoutsResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "userLoadouts")]
	public NekoArenaLoadoutEntry[] UserLoadouts;

	[DataMember(Name = "systemLoadouts")]
	public NekoArenaLoadoutEntry[] SystemLoadouts;

	[DataMember(Name = "lastSelectedId")]
	public string LastSelectedId;

	[DataMember(Name = "userLoadoutCapacity")]
	public int UserLoadoutCapacity;
}
