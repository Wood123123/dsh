using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPartyKickedNotification : NekoPartyNotificationBase
{
	[JsonPropertyName("leaderProfileId")]
	public string LeaderProfileId { get; set; } = string.Empty;

	[JsonPropertyName("leaderNickname")]
	public string LeaderNickname { get; set; } = string.Empty;

	public NekoPartyKickedNotification()
	{
		base.NekoType = "nekoparty.partyKicked";
	}
}
