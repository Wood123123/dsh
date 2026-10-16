using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaSaveLoadoutRequest
{
	[DataMember(Name = "name")]
	public string Name;

	[DataMember(Name = "inventoryDescriptorBase64")]
	public string InventoryDescriptorBase64;

	[DataMember(Name = "overwriteId")]
	public string OverwriteId;
}
