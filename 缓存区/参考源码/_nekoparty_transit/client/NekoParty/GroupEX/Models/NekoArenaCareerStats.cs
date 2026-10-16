using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public class NekoArenaCareerStats
{
	[DataMember(Name = "totalKills")]
	public int TotalKills;

	[DataMember(Name = "totalDeaths")]
	public int TotalDeaths;

	[DataMember(Name = "totalRespawns")]
	public int TotalRespawns;

	[DataMember(Name = "matchesPlayed")]
	public int MatchesPlayed;

	[DataMember(Name = "matchesWon")]
	public int MatchesWon;

	[DataMember(Name = "bestKillStreak")]
	public int BestKillStreak;

	[DataMember(Name = "rankPoints")]
	public int RankPoints;
}
