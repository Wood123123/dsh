using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaInstallSystemPresetsRequest
{
	[DataMember(Name = "entries")]
	public NekoArenaLoadoutEntry[] Entries;
}
