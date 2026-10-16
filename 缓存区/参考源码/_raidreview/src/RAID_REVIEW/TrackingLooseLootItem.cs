namespace RAID_REVIEW;

public class TrackingLooseLootItem
{
	public string itemId { get; set; }

	public string templateId { get; set; }

	public string itemName { get; set; }

	public int price { get; set; }

	public int qty { get; set; }

	public float x { get; set; }

	public float y { get; set; }

	public float z { get; set; }

	public bool inContainer { get; set; }

	public string containerName { get; set; }
}
