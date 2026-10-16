using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(LocalGame), "Stop")]
internal static class PauseGameEndPatch
{
	[HarmonyPriority(800)]
	private static void Prefix()
	{
		RaidPause.End();
	}
}
