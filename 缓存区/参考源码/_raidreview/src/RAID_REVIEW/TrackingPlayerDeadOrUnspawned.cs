namespace RAID_REVIEW;

public class TrackingPlayerDeadOrUnspawned
{
	public string sessionId { get; set; }

	public string profileId { get; set; }

	public long time { get; set; }

	public PlayerStatus status { get; set; }
}
