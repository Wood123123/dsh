using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaSelectLoadoutRequest
{
	[DataMember(Name = "loadoutId")]
	public string LoadoutId;

	[DataMember(Name = "source")]
	public ENekoArenaLoadoutSource Source;

	[DataMember(Name = "locationId")]
	public string LocationId;
}
