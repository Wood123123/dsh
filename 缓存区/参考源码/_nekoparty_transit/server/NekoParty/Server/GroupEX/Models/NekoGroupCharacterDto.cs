using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Match;

namespace NekoParty.Server.GroupEX.Models;

public sealed class NekoGroupCharacterDto
{
	[JsonPropertyName("_id")]
	public string? Id { get; set; }

	[JsonPropertyName("aid")]
	public string? Aid { get; set; }

	[JsonPropertyName("IsLeader")]
	public bool IsLeader { get; set; }

	[JsonPropertyName("IsReady")]
	public bool IsReady { get; set; }

	[JsonPropertyName("region")]
	public string? Region { get; set; }

	[JsonPropertyName("lookingGroup")]
	public bool LookingGroup { get; set; }

	[JsonPropertyName("Info")]
	public NekoGroupInfoDto? Info { get; set; }

	[JsonPropertyName("PlayerVisualRepresentation")]
	public PlayerVisualRepresentation? PlayerVisualRepresentation { get; set; }
}
