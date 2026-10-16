using System.Collections.Generic;

namespace RAID_REVIEW;

public class TrackingOrbitMainObjectives
{
	public string sessionId { get; set; }

	public long time { get; set; }

	public List<TrackingOrbitSquadMainObjectives> squads { get; set; }
}
