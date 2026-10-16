using System.Collections.Generic;

namespace StashTools;

public sealed class PauseRoomContext
{
	public string RoomId { get; set; } = "";

	public string LocalId { get; set; } = "";

	public string HostId { get; set; } = "";

	public List<PauseMember> Members { get; set; } = new List<PauseMember>();

	public bool Ready { get; set; }

	public bool AllMembersCompatible { get; set; }

	public string ReadinessReason { get; set; } = "";

	public double HostTime { get; set; }
}
