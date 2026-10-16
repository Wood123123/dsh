namespace RAID_REVIEW;

public class TrackingPlayerData
{
	public string sessionId { get; set; }

	public string profileId { get; set; }

	public long time { get; set; }

	public float x { get; set; }

	public float y { get; set; }

	public float z { get; set; }

	public float dir { get; set; }

	public float health { get; set; }

	public float maxHealth { get; set; }

	public string decision { get; set; }

	public bool ghost { get; set; }

	public TrackingPlayerData(string sessionId, string profileId, long time, float x, float y, float z, float dir, float health, float maxHealth, string decision = "", bool ghost = false)
	{
		this.sessionId = sessionId;
		this.profileId = profileId;
		this.time = time;
		this.x = x;
		this.y = y;
		this.z = z;
		this.dir = dir;
		this.health = health;
		this.maxHealth = maxHealth;
		this.decision = decision;
		this.ghost = ghost;
	}
}
