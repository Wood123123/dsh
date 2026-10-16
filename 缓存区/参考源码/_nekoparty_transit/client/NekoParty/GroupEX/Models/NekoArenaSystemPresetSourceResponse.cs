using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaSystemPresetSourceResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "presets")]
	public NekoArenaSystemPresetSource[] Presets;
}
