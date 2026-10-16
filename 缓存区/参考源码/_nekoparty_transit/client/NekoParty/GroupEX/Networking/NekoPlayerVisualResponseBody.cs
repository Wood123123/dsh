using Newtonsoft.Json;

namespace NekoParty.GroupEX.Networking;

public class NekoPlayerVisualResponseBody
{
	[JsonProperty("success")]
	public bool Success { get; set; }

	[JsonProperty("visualJson")]
	public string VisualJson { get; set; }

	[JsonProperty("error")]
	public string Error { get; set; }
}
