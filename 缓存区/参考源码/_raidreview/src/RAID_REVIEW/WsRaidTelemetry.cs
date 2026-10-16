namespace RAID_REVIEW;

public class WsRaidTelemetry
{
	public string Id { get; set; }

	public TrackingPlayerData PlayerData { get; set; }

	public TrackingRaidKill KillData { get; set; }

	public TrackingLootItem LootData { get; set; }
}
