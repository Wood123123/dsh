using System;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GameWorld), "Dispose")]
internal static class RaidEndPatch
{
	private static void Prefix(GameWorld __instance)
	{
		try
		{
			RaidEarnings.End(__instance);
		}
		catch (Exception ex)
		{
			RaidEarnings.Fail(ex);
		}
	}
}
