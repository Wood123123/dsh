using System.Collections.Generic;

namespace StashTools.History;

public sealed class TeamRaidView
{
	public string Provider { get; set; } = "";

	public string ActionId { get; set; } = "";

	public List<TeamMemberSummary> Members { get; set; } = new List<TeamMemberSummary>();
}
