using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaSystemPresetSource
{
	[DataMember(Name = "id")]
	public string Id;

	[DataMember(Name = "name")]
	public string Name;

	[DataMember(Name = "root")]
	public string Root;

	[DataMember(Name = "type")]
	public string Type;

	[DataMember(Name = "items")]
	public object[] Items;
}
