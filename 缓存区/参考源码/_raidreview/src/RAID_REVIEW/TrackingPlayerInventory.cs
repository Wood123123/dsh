using System.Collections.Generic;

namespace RAID_REVIEW;

public class TrackingPlayerInventory
{
	public string sessionId { get; set; }

	public string profileId { get; set; }

	public long time { get; set; }

	public List<TrackingInventoryItem> items { get; set; }
}
