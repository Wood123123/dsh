using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public class NekoArenaAchievementView
{
	[DataMember(Name = "id")]
	public string Id;

	[DataMember(Name = "nameKey")]
	public string NameKey;

	[DataMember(Name = "descKey")]
	public string DescKey;

	[DataMember(Name = "rarity")]
	public string Rarity;

	[DataMember(Name = "unlocked")]
	public bool Unlocked;

	[DataMember(Name = "unlockedAtUnix")]
	public long UnlockedAtUnix;

	[DataMember(Name = "progress")]
	public int Progress;

	[DataMember(Name = "target")]
	public int Target;
}
