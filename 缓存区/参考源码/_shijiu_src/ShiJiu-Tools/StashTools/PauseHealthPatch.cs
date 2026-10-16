using EFT.HealthSystem;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(ActiveHealthController), "ManualUpdate")]
internal static class PauseHealthPatch
{
	private static bool Prefix()
	{
		if (!SinglePlayerPause.Paused)
		{
			return !RaidPause.CoopPaused;
		}
		return false;
	}
}
