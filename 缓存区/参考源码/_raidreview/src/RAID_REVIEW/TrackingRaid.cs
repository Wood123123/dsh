using System;
using System.Runtime.CompilerServices;
using EFT;

namespace RAID_REVIEW;

public class TrackingRaid
{
	[CompilerGenerated]
	private ExitStatus _003CexitStatus_003Ek__BackingField;

	public string sessionId { get; set; }

	public string profileId { get; set; }

	public string location { get; set; }

	public string detectedMods { get; set; }

	public DateTime time { get; set; }

	public long timeInRaid { get; set; }

	public string exitName { get; set; }

	public string type { get; set; }

	public ExitStatus exitStatus
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CexitStatus_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CexitStatus_003Ek__BackingField = value;
		}
	}
}
