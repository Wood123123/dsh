using System.Runtime.CompilerServices;
using EFT;

namespace RAID_REVIEW;

public class TrackingPlayer
{
	[CompilerGenerated]
	private EPlayerSide _003Cteam_003Ek__BackingField;

	public string sessionId { get; set; }

	public string profileId { get; set; }

	public int level { get; set; }

	public EPlayerSide team
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003Cteam_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003Cteam_003Ek__BackingField = value;
		}
	}

	public string name { get; set; }

	public string type { get; set; }

	public int group { get; set; }

	public long spawnTime { get; set; }

	public string mod_SAIN_brain { get; set; }

	public string mod_SAIN_difficulty { get; set; }

	public string mod_SAIN_name { get; set; }
}
