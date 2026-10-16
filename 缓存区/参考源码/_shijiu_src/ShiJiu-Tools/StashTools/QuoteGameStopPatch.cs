using System;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(LocalGame), "Stop")]
internal static class QuoteGameStopPatch
{
	private static void Prefix()
	{
		try
		{
			RaidEarnings.StopPrices();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("退出报价清理失败：" + ex));
		}
	}
}
