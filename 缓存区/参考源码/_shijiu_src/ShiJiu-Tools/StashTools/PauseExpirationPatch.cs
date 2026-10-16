using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(EndByTimerScenario), "Update")]
internal static class PauseExpirationPatch
{
	private static bool Prefix(GameTimer ____timer)
	{
		if (SinglePlayerPause.Paused && ____timer != null)
		{
			return !SinglePlayerPause.Owns(____timer);
		}
		return true;
	}
}
