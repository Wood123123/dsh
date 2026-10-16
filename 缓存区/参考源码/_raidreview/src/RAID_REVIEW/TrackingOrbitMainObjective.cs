namespace RAID_REVIEW;

public class TrackingOrbitMainObjective
{
	public string type { get; set; }

	public int cellX { get; set; }

	public int cellY { get; set; }

	public float x { get; set; }

	public float y { get; set; }

	public float z { get; set; }

	public bool completed { get; set; }

	public float killsRoamStartedAt { get; set; }

	public float killsRoamTargetDuration { get; set; }

	public float lootValueEnteredAt { get; set; }

	public float lootValueTotal { get; set; }

	public bool lootValueInterrupted { get; set; }

	public string questTriggerId { get; set; }

	public string questTitle { get; set; }
}
