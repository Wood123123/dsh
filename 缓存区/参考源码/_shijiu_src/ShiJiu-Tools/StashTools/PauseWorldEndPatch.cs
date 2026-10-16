using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GameWorld), "Dispose")]
internal static class PauseWorldEndPatch
{
	[HarmonyPriority(800)]
	private static void Prefix()
	{
		RaidPause.End();
	}
}
