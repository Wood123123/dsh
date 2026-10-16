using System.Collections.Generic;

namespace StashTools.History;

public sealed class CoopRaidContext
{
	public string Provider { get; set; } = "";

	public string ActionId { get; set; } = "";

	public List<TeamMemberIdentity> Members { get; set; } = new List<TeamMemberIdentity>();
}
