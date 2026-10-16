using System.Collections.Generic;

namespace RAID_REVIEW;

public class TrackingPhobosField
{
	public string sessionId { get; set; }

	public long time { get; set; }

	public int gridCols { get; set; }

	public int gridRows { get; set; }

	public float worldMinX { get; set; }

	public float worldMinZ { get; set; }

	public float cellSize { get; set; }

	public List<TrackingPhobosCell> advection { get; set; }

	public List<TrackingPhobosCell> convergence { get; set; }

	public List<TrackingPhobosZone> zones { get; set; }
}
