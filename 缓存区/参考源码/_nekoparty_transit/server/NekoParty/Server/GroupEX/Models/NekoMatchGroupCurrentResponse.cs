using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NekoParty.Server.GroupEX.Models;

public sealed class NekoMatchGroupCurrentResponse
{
	[JsonPropertyName("squad")]
	public List<NekoGroupCharacterDto> Squad { get; set; } = new List<NekoGroupCharacterDto>();

	[JsonPropertyName("raidSettings")]
	public object? RaidSettings { get; set; }
}
