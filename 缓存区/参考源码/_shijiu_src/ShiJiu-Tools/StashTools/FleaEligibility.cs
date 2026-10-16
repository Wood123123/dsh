namespace StashTools;

internal static class FleaEligibility
{
	internal static string? Restriction(bool templateAllowed, bool nativeRaidAllowed, bool onlyFoundInRaid)
	{
		if (!templateAllowed)
		{
			return "此类物品禁止在跳蚤交易";
		}
		if (onlyFoundInRaid && !nativeRaidAllowed)
		{
			return "跳蚤仅允许战局内发现物品";
		}
		return null;
	}
}
