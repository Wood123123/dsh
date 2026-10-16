using JsonType;
using Newtonsoft.Json;

namespace WTTClientCommonLib.Models;

public class CustomCraftResult
{
	[JsonProperty("count")]
	public int Count { get; set; }

	[JsonProperty("minStackCount")]
	public int? MinStackCount { get; set; }

	[JsonProperty("maxStackCount")]
	public int? MaxStackCount { get; set; }

	[JsonProperty("items")]
	public FlatItem[] Items { get; set; }
}
