namespace RAID_REVIEW;

public class TrackingOrbitBotObjective
{
	public string sessionId { get; set; }

	public string profileId { get; set; }

	public long time { get; set; }

	public string status { get; set; }

	public string category { get; set; }

	public bool isLeader { get; set; }

	public float objectiveX { get; set; }

	public float objectiveY { get; set; }

	public float objectiveZ { get; set; }

	public string extractReason { get; set; }
}
