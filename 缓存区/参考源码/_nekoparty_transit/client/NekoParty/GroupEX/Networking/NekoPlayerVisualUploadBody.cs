using Newtonsoft.Json;

namespace NekoParty.GroupEX.Networking;

public class NekoPlayerVisualUploadBody
{
	[JsonProperty("accountId")]
	public string AccountId { get; set; }

	[JsonProperty("visualJson")]
	public string VisualJson { get; set; }
}
