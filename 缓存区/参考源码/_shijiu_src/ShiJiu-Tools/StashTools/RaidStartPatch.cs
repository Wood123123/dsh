using System;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
internal static class RaidStartPatch
{
	private static void Prefix(GameWorld __instance)
	{
		try
		{
			RaidEarnings.Begin(__instance.MainPlayer, __instance);
		}
		catch (Exception ex)
		{
			RaidEarnings.Fail(ex);
		}
	}
}
