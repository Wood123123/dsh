using SPTarkov.Server.Core.Models.Utils;

namespace StashTools.Server;

public sealed class QueryRequest : IRequestData
{
	public string Payload { get; set; } = "";
}
