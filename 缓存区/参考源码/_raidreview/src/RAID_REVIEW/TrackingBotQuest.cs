namespace RAID_REVIEW;

public class TrackingBotQuest
{
	public string sessionId { get; set; }

	public string profileId { get; set; }

	public long time { get; set; }

	public string questName { get; set; }

	public bool isEFTQuest { get; set; }

	public string actionType { get; set; }

	public string status { get; set; }

	public float objectiveX { get; set; }

	public float objectiveY { get; set; }

	public float objectiveZ { get; set; }
}
