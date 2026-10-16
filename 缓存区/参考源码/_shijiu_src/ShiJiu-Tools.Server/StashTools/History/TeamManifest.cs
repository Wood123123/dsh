using System.Collections.Generic;

namespace StashTools.History;

public sealed class TeamManifest
{
	public CoopRaidContext Context { get; set; } = new CoopRaidContext();

	public Dictionary<string, TeamRegistration> Registrations { get; set; } = new Dictionary<string, TeamRegistration>();

	public bool AllowRosterExtension { get; set; }
}
