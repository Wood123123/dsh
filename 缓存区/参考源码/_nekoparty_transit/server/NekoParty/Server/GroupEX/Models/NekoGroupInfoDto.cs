using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public sealed class NekoGroupInfoDto
{
	[JsonPropertyName("Nickname")]
	public string? Nickname { get; set; }

	[JsonPropertyName("Side")]
	public string? Side { get; set; }

	[JsonPropertyName("Level")]
	public int Level { get; set; }

	[JsonPropertyName("PrestigeLevel")]
	public int PrestigeLevel { get; set; }

	[JsonPropertyName("MemberCategory")]
	public int MemberCategory { get; set; }

	[JsonPropertyName("SelectedMemberCategory")]
	public int SelectedMemberCategory { get; set; }

	[JsonPropertyName("GameVersion")]
	public string GameVersion { get; set; } = "live";

	[JsonPropertyName("HasCoopExtension")]
	public bool HasCoopExtension { get; set; }

	[JsonPropertyName("SavageLockTime")]
	public double SavageLockTime { get; set; }

	[JsonPropertyName("SavageNickname")]
	public string? SavageNickname { get; set; }
}
