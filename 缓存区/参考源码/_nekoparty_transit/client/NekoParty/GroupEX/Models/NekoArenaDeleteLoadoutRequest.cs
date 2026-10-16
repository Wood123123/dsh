using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaDeleteLoadoutRequest
{
	[DataMember(Name = "loadoutId")]
	public string LoadoutId;
}
