using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPlayerVisualUploadRequest : IRequestData
{
	[JsonPropertyName("accountId")]
	public string AccountId { get; set; } = string.Empty;

	[JsonPropertyName("visualJson")]
	public string VisualJson { get; set; } = string.Empty;
}
