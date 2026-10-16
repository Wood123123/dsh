using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPlayerVisualResponse
{
	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("visualJson")]
	public string VisualJson { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;
}
