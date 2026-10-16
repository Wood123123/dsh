using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaStatusResponse
{
	[DataMember(Name = "hasInitialLoadout")]
	public bool HasInitialLoadout;

	[DataMember(Name = "hasDeathSnapshot")]
	public bool HasDeathSnapshot;

	[DataMember(Name = "respawnsUsed")]
	public int RespawnsUsed;

	[DataMember(Name = "respawnsLimit")]
	public int RespawnsLimit;

	[DataMember(Name = "respawnsRemaining")]
	public int RespawnsRemaining;

	[DataMember(Name = "locationId")]
	public string LocationId;
}
