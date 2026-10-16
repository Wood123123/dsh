using System.Collections.Generic;
using System.Text.Json.Serialization;
using NekoParty.Server.Core.Models;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPartyInviteReceivedNotification : NekoPartyNotificationBase
{
	[JsonPropertyName("inviteId")]
	public string InviteId { get; set; } = string.Empty;

	[JsonPropertyName("partyId")]
	public string PartyId { get; set; } = string.Empty;

	[JsonPropertyName("leaderProfileId")]
	public string LeaderProfileId { get; set; } = string.Empty;

	[JsonPropertyName("leaderNickname")]
	public string LeaderNickname { get; set; } = string.Empty;

	[JsonPropertyName("targetProfileId")]
	public string TargetProfileId { get; set; } = string.Empty;

	[JsonPropertyName("targetNickname")]
	public string TargetNickname { get; set; } = string.Empty;

	[JsonPropertyName("fromProfile")]
	public NekoPartyInvitePlayerData? FromProfile { get; set; }

	[JsonPropertyName("members")]
	public List<NekoPartyInvitePlayerData> Members { get; set; } = new List<NekoPartyInvitePlayerData>();

	[JsonPropertyName("expiresAtUnix")]
	public long ExpiresAtUnix { get; set; }

	public NekoPartyInviteReceivedNotification()
	{
		base.NekoType = "nekoparty.partyInviteReceived";
	}
}
