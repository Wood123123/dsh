namespace StashTools.History;

public sealed class TeamMemberSummary
{
	public string Profile { get; set; } = "";

	public string Name { get; set; } = "";

	public string RecordId { get; set; } = "";

	public bool HasRecord { get; set; }

	public bool Joined { get; set; }

	public bool Final { get; set; }

	public bool Reconciled { get; set; }

	public string Status { get; set; } = "";

	public double? Income { get; set; }

	public double? Expense { get; set; }

	public int? PmcKills { get; set; }

	public int? ScavKills { get; set; }

	public int? OtherKills { get; set; }

	public int? AiKills { get; set; }

	public int? PlayerKills { get; set; }

	public int? UnknownIdentityKills { get; set; }
}
