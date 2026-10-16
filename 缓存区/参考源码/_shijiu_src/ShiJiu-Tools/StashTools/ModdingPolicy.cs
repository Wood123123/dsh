namespace StashTools;

internal static class ModdingPolicy
{
	internal static bool Plate(bool enabled, bool owned, bool locked, bool fixedLiner, bool plateSlot, bool pinned)
	{
		if (((enabled & owned) && !locked && !fixedLiner) & plateSlot)
		{
			return !pinned;
		}
		return false;
	}
}
