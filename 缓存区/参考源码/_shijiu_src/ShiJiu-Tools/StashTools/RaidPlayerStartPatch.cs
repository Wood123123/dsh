using System;
using Comfort.Common;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(Player), "OnGameSessionBegin")]
internal static class RaidPlayerStartPatch
{
	private static void Prefix(Player __instance)
	{
		try
		{
			if (__instance.IsYourPlayer && Singleton<GameWorld>.Instantiated)
			{
				RaidEarnings.Begin(__instance, Singleton<GameWorld>.Instance);
			}
		}
		catch (Exception ex)
		{
			RaidEarnings.Fail(ex);
		}
	}
}
