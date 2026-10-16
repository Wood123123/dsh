using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public class NekoArenaTierView
{
	[DataMember(Name = "id")]
	public string Id;

	[DataMember(Name = "index")]
	public int Index;

	[DataMember(Name = "nameKey")]
	public string NameKey;

	[DataMember(Name = "colorHex")]
	public string ColorHex;

	[DataMember(Name = "minRp")]
	public int MinRp;
}
