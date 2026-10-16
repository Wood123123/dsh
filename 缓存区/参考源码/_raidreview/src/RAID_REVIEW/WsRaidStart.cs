using System;

namespace RAID_REVIEW;

public class WsRaidStart
{
	public string Id { get; set; }

	public DateTime Time { get; set; }

	public string Location { get; set; }

	public int TimeOfDay { get; set; }

	public long TimeInRaid { get; set; }
}
