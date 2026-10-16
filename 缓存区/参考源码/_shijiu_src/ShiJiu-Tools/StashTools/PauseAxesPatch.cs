using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GamePlayerOwner), "TranslateAxes")]
internal static class PauseAxesPatch
{
	private static bool Prefix(GamePlayerOwner __instance)
	{
		if (!SinglePlayerPause.Owns(__instance))
		{
			return !RaidPause.CoopPaused;
		}
		return false;
	}
}
