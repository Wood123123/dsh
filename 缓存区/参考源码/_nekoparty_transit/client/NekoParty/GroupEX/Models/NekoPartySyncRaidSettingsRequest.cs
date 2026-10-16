using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartySyncRaidSettingsRequest(string location, int side, int time)
{
	[DataMember(Name = "location")]
	public string Location = location;

	[DataMember(Name = "side")]
	public int Side = side;

	[DataMember(Name = "time")]
	public int Time = time;
}
