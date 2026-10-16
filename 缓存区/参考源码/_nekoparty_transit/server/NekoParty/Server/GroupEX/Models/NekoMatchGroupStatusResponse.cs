using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public sealed class NekoMatchGroupStatusResponse
{
	[JsonPropertyName("players")]
	public List<NekoGroupCharacterDto> Players { get; set; } = new List<NekoGroupCharacterDto>();

	[JsonPropertyName("maxPveCountExceeded")]
	public bool MaxPveCountExceeded { get; set; }
}
