using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaLoadoutEntry
{
	[DataMember(Name = "id")]
	public string Id;

	[DataMember(Name = "name")]
	public string Name;

	[DataMember(Name = "source")]
	public ENekoArenaLoadoutSource Source;

	[DataMember(Name = "createdAtUnix")]
	public long CreatedAtUnix;

	[DataMember(Name = "inventoryDescriptorBase64")]
	public string InventoryDescriptorBase64;
}
