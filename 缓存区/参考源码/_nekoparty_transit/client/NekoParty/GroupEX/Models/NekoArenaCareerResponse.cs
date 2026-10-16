using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public class NekoArenaCareerResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "stats")]
	public NekoArenaCareerStats Stats;

	[DataMember(Name = "currentTier")]
	public NekoArenaTierView CurrentTier;

	[DataMember(Name = "nextTier")]
	public NekoArenaTierView NextTier;

	[DataMember(Name = "rpIntoTier")]
	public int RpIntoTier;

	[DataMember(Name = "rpForNextTier")]
	public int RpForNextTier;

	[DataMember(Name = "achievements")]
	public NekoArenaAchievementView[] Achievements;
}
