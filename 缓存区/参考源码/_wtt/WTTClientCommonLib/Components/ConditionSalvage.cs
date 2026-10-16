using EFT.Quests;

namespace WTTClientCommonLib.Components;

public class ConditionSalvage : ConditionZone
{
	public float plantTime;

	public override string FormattedDescription => string.Format(((ConditionZone)this).FormattedDescription, base.zoneId, plantTime);
}
