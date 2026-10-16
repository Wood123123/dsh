using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public record NekoModMismatchNotification : NekoPartyNotificationBase
{
	[JsonPropertyName("peerNickname")]
	public string PeerNickname { get; set; } = string.Empty;

	[JsonPropertyName("report")]
	public string Report { get; set; } = string.Empty;

	[JsonPropertyName("blocked")]
	public bool Blocked { get; set; }

	public NekoModMismatchNotification()
	{
		base.NekoType = "nekoparty.modMismatch";
	}
}
