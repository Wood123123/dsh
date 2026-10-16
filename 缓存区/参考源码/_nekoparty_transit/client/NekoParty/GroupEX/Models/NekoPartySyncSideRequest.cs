using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartySyncSideRequest(int preferredSide)
{
	[DataMember(Name = "preferredSide")]
	public int PreferredSide = preferredSide;
}
