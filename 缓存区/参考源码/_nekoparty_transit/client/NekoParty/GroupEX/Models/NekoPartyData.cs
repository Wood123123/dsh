using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoPartyData
{
	[DataMember(Name = "partyId")]
	public string PartyId;

	[DataMember(Name = "leaderProfileId")]
	public string LeaderProfileId;

	[DataMember(Name = "status")]
	public ENekoPartyStatus Status;

	[DataMember(Name = "members")]
	public NekoPartyMemberData[] Members;

	[DataMember(Name = "location")]
	public string Location;

	[DataMember(Name = "side")]
	public int Side;

	[DataMember(Name = "time")]
	public int Time;
}
