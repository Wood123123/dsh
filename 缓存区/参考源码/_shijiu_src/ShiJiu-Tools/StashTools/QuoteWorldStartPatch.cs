using System;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
internal static class QuoteWorldStartPatch
{
	private static void Prefix()
	{
		try
		{
			MarketQuotes.Resume();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("进入对局报价恢复失败：" + ex));
		}
	}
}
