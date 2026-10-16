using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPartyDisbandedNotification : NekoPartyNotificationBase
{
	[JsonPropertyName("leaderProfileId")]
	public string LeaderProfileId { get; set; } = string.Empty;

	[JsonPropertyName("leaderNickname")]
	public string LeaderNickname { get; set; } = string.Empty;

	public NekoPartyDisbandedNotification()
	{
		base.NekoType = "nekoparty.partyDisbanded";
	}
}
