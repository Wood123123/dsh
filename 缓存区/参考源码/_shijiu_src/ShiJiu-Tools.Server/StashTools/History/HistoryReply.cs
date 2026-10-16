using System.Collections.Generic;

namespace StashTools.History;

public sealed class HistoryReply
{
	public bool Ok { get; set; }

	public string Error { get; set; } = "";

	public int Total { get; set; }

	public double SurvivalRate { get; set; }

	public int KillCount { get; set; }

	public double NetTotal { get; set; }

	public int Page { get; set; } = 1;

	public List<RaidRecord> Records { get; set; } = new List<RaidRecord>();

	public List<string> Favorites { get; set; } = new List<string>();

	public TeamRaidView? Team { get; set; }
}
