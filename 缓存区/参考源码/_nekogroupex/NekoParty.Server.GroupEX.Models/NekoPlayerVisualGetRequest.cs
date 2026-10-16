using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace NekoParty.Server.GroupEX.Models;

public record NekoPlayerVisualGetRequest : IRequestData
{
	[JsonPropertyName("accountId")]
	public string AccountId { get; set; } = string.Empty;
}
