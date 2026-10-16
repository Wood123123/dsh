using System;
using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(OverallScreen), "Show")]
internal static class HistoryOwnPatch
{
	private static void Postfix(OverallScreen __instance)
	{
		try
		{
			HistoryUi.Own(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("战绩入口不可用：" + ex));
		}
	}
}
