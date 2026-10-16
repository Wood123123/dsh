using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPartyMemberLeftNotification : NekoPartyNotificationBase
{
	[JsonPropertyName("memberProfileId")]
	public string MemberProfileId { get; set; } = string.Empty;

	[JsonPropertyName("memberNickname")]
	public string MemberNickname { get; set; } = string.Empty;

	public NekoPartyMemberLeftNotification()
	{
		base.NekoType = "nekoparty.partyMemberLeft";
	}
}
