using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaStoreInitialLoadoutRequest
{
	[DataMember(Name = "locationId")]
	public string LocationId;

	[DataMember(Name = "inventoryDescriptorBase64")]
	public string InventoryDescriptorBase64;
}
