using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public class NekoArenaMatchReportRequest
{
	[DataMember(Name = "locationId")]
	public string LocationId;

	[DataMember(Name = "kills")]
	public int Kills;

	[DataMember(Name = "deaths")]
	public int Deaths;

	[DataMember(Name = "respawnsUsed")]
	public int RespawnsUsed;

	[DataMember(Name = "survived")]
	public bool Survived;

	[DataMember(Name = "bestKillStreak")]
	public int BestKillStreak;
}
