using System;

namespace StashTools.History;

public sealed class TeamJoin
{
	public CoopRaidContext Context { get; set; } = new CoopRaidContext();

	public string RecordId { get; set; } = "";

	public DateTime StartedUtc { get; set; }
}
