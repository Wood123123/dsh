using System.Collections.Generic;

namespace RAID_REVIEW;

public class TrackingOrbitSquadMainObjectives
{
	public int squadId { get; set; }

	public List<string> memberProfileIds { get; set; }

	public List<TrackingOrbitMainObjective> mainObjectives { get; set; }
}
