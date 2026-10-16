using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public record NekoRaidInviteReceivedNotification : NekoPartyNotificationBase
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

	[JsonPropertyName("serverId")]
	public string ServerId { get; set; } = string.Empty;

	[JsonPropertyName("raidCode")]
	public string RaidCode { get; set; } = string.Empty;

	[JsonPropertyName("location")]
	public string Location { get; set; } = string.Empty;

	[JsonPropertyName("side")]
	public int Side { get; set; }

	[JsonPropertyName("targetSide")]
	public int TargetSide { get; set; }

	[JsonPropertyName("time")]
	public int Time { get; set; }

	[JsonPropertyName("expiresAtUnix")]
	public long ExpiresAtUnix { get; set; }

	public NekoRaidInviteReceivedNotification()
	{
		base.NekoType = "nekoparty.raidInviteReceived";
	}
}
