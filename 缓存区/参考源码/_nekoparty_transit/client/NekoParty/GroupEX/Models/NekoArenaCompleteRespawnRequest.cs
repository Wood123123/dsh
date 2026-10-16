using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaCompleteRespawnRequest
{
	[DataMember(Name = "succeeded")]
	public bool Succeeded;
}
