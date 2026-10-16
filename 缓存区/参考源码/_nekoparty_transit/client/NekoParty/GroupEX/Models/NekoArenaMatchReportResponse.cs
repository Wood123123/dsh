using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public class NekoArenaMatchReportResponse : NekoArenaCareerResponse
{
	[DataMember(Name = "rpGained")]
	public int RpGained;

	[DataMember(Name = "rankedUp")]
	public bool RankedUp;

	[DataMember(Name = "previousTierId")]
	public string PreviousTierId;

	[DataMember(Name = "newlyUnlocked")]
	public NekoArenaAchievementView[] NewlyUnlocked;
}
