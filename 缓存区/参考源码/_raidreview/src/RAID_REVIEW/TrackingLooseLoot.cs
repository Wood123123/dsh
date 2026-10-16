using System.Collections.Generic;

namespace RAID_REVIEW;

public class TrackingLooseLoot
{
	public string sessionId { get; set; }

	public long time { get; set; }

	public List<TrackingLooseLootItem> items { get; set; }
}
