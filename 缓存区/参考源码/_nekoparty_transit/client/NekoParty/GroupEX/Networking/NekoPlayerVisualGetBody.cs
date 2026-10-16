using Newtonsoft.Json;

namespace NekoParty.GroupEX.Networking;

public class NekoPlayerVisualGetBody
{
	[JsonProperty("accountId")]
	public string AccountId { get; set; }
}
