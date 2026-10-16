using System.Runtime.CompilerServices;
using EFT;

namespace RAID_REVIEW;

public class WsRaidEnd
{
	[CompilerGenerated]
	private ExitStatus _003CExitStatus_003Ek__BackingField;

	public string Id { get; set; }

	public string Location { get; set; }

	public ExitStatus ExitStatus
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CExitStatus_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CExitStatus_003Ek__BackingField = value;
		}
	}

	public string ExitName { get; set; }

	public string TimeInRaid { get; set; }
}
