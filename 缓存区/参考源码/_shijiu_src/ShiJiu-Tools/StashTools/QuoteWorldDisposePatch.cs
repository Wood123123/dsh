using System;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GameWorld), "Dispose")]
internal static class QuoteWorldDisposePatch
{
	private static void Prefix()
	{
		try
		{
			RaidEarnings.StopPrices();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("场景报价清理失败：" + ex));
		}
	}
}
